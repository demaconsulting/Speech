using DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;

/// <summary>
///     Production <see cref="ISynthesisBackend"/> for Chatterbox Turbo: tokenizes text, conditions on a
///     reference voice, runs the autoregressive speech-token loop and decodes the tokens to a waveform.
/// </summary>
/// <remarks>
///     <b>Pipeline</b> (mirrors the proven reference pipeline): the text ids (with the tokenizer's trailing
///     end-of-text pair) are embedded and prefixed with the voice's conditioning embeddings; the start token
///     is only recorded for the repetition penalty and is never embedded; each step embeds only the newest
///     token; generation ends on the stop token or a guard bound. The decoder input is the voice's prompt
///     tokens, the generated speech tokens and three silence tokens.
///     <para>
///     <b>Guards</b>: generation never runs unbounded. The loop is limited to the smaller of the configured
///     maximum and a bound proportional to the text length (a runaway sequence such as a tag at the very end
///     of a sentence otherwise generates until the hard limit). On reaching a bound the tokens so far are
///     decoded (never an exception) and <see cref="ChatterboxTurboGenerationResult.HitTokenLimit"/> is set.
///     </para>
///     <para>
///     <b>Voices</b>: the encoder runs once per voice, lazily, and its conditioning is cached. Speaker ids
///     index the supplied voices; an unknown id falls back to the first voice. The speed argument is ignored
///     because the model has no speed control. <b>Threading</b>: calls are serialized internally; the type is
///     safe to dispose from another thread, after which <see cref="Generate"/> throws
///     <see cref="ObjectDisposedException"/>. The output is not watermarked.
///     </para>
/// </remarks>
internal sealed class OnnxChatterboxTurboSynthesisEngine : ISynthesisBackend
{
    /// <summary>The output sample rate in Hz.</summary>
    public const int OutputSampleRate = 24000;

    /// <summary>The token that opens the generated sequence (never embedded).</summary>
    public const int StartSpeechToken = 6561;

    /// <summary>The token that ends generation.</summary>
    public const int StopSpeechToken = 6562;

    /// <summary>The silence token appended to the decoder input.</summary>
    public const int SilenceToken = 4299;

    /// <summary>How many silence tokens end the decoder input.</summary>
    public const int TrailingSilenceTokenCount = 3;

    /// <summary>The embedding width, in floats per token.</summary>
    public const int EmbeddingSize = 1024;

    /// <summary>Extra steps allowed on top of the proportional bound so very short texts can finish.</summary>
    private const int LengthBoundSlackTokens = 32;

    /// <summary>The graph seam.</summary>
    private readonly IChatterboxTurboNetworks _networks;

    /// <summary>The text tokenizer.</summary>
    private readonly ByteLevelBpeTokenizer _tokenizer;

    /// <summary>The supplied voices, index = speaker id.</summary>
    private readonly IReadOnlyList<ChatterboxTurboReferenceVoice> _voices;

    /// <summary>The configured hard cap on generated tokens.</summary>
    private readonly int _maxNewTokens;

    /// <summary>The configured cap on generated tokens per text token.</summary>
    private readonly double _maxAudioTokensPerTextToken;

    /// <summary>The configured cap on identical consecutive speech tokens; 0 disables it.</summary>
    private readonly int _maxConsecutiveSpeechTokenRepeats;

    /// <summary>Conditioning cache by speaker id; guarded by <see cref="_gate"/>.</summary>
    private readonly Dictionary<int, ChatterboxTurboConditioning> _conditioningCache = [];

    /// <summary>Serializes generation and cache access.</summary>
    private readonly object _gate = new();

    /// <summary>Set once disposed.</summary>
    private volatile bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OnnxChatterboxTurboSynthesisEngine"/> class, taking ownership of <paramref name="networks"/>.
    /// </summary>
    /// <param name="networks">The graph seam; disposed with this engine. Must not be null.</param>
    /// <param name="tokenizer">The text tokenizer. Must not be null.</param>
    /// <param name="voices">The reference voices; must contain at least one.</param>
    /// <param name="maxNewTokens">The hard cap on generated tokens; must be positive.</param>
    /// <param name="maxAudioTokensPerTextToken">The cap on generated tokens per text token; must be positive.</param>
    /// <param name="maxConsecutiveSpeechTokenRepeats">
    ///     The most identical speech tokens allowed in a row before that token is excluded from the next step; 0
    ///     disables the cap; must not be negative.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when a reference argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="voices"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a bound is not positive or the cap is negative.</exception>
    public OnnxChatterboxTurboSynthesisEngine(
        IChatterboxTurboNetworks networks,
        ByteLevelBpeTokenizer tokenizer,
        IReadOnlyList<ChatterboxTurboReferenceVoice> voices,
        int maxNewTokens,
        double maxAudioTokensPerTextToken,
        int maxConsecutiveSpeechTokenRepeats)
    {
        ArgumentNullException.ThrowIfNull(networks);
        ArgumentNullException.ThrowIfNull(tokenizer);
        ArgumentNullException.ThrowIfNull(voices);
        if (voices.Count == 0)
        {
            throw new ArgumentException("At least one reference voice is required.", nameof(voices));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxNewTokens, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxAudioTokensPerTextToken, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(maxConsecutiveSpeechTokenRepeats);

        _networks = networks;
        _tokenizer = tokenizer;
        _voices = voices;
        _maxNewTokens = maxNewTokens;
        _maxAudioTokensPerTextToken = maxAudioTokensPerTextToken;
        _maxConsecutiveSpeechTokenRepeats = maxConsecutiveSpeechTokenRepeats;
    }

    /// <inheritdoc/>
    public int SampleRate => OutputSampleRate;

    /// <inheritdoc/>
    /// <remarks>The <paramref name="speed"/> argument is ignored: the model exposes no speed control.</remarks>
    public EngineAudio Generate(string text, float speed, int speakerId) => GenerateDetailed(text, speakerId).Audio;

    /// <summary>
    ///     Generates speech and reports guard diagnostics.
    /// </summary>
    /// <param name="text">The text, possibly containing the model's bracketed tags. Empty or whitespace yields empty audio.</param>
    /// <param name="speakerId">The voice index; unknown values use the first voice.</param>
    /// <returns>The audio and diagnostics.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the engine is disposed.</exception>
    public ChatterboxTurboGenerationResult GenerateDetailed(string text, int speakerId)
    {
        ArgumentNullException.ThrowIfNull(text);
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Nothing to say: avoid touching the networks at all
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ChatterboxTurboGenerationResult(new EngineAudio([], OutputSampleRate), false, 0);
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var textIds = _tokenizer.Encode(text);
            var voiceIndex = speakerId >= 0 && speakerId < _voices.Count ? speakerId : 0;
            var conditioning = GetConditioning(voiceIndex);

            // Step 0 input: conditioning first, then the text embeddings (the start token is never embedded)
            var textEmbeddings = _networks.Embed(textIds);
            var inputs = new float[conditioning.ConditioningEmbeddings.Length + textEmbeddings.Length];
            conditioning.ConditioningEmbeddings.CopyTo(inputs, 0);
            textEmbeddings.CopyTo(inputs, conditioning.ConditioningEmbeddings.Length);

            var bound = Math.Min(
                _maxNewTokens,
                (int)Math.Ceiling(textIds.Length * _maxAudioTokensPerTextToken) + LengthBoundSlackTokens);

            var generated = new List<long> { StartSpeechToken };
            var penalized = new HashSet<long> { StartSpeechToken };
            var sequenceLength = conditioning.ConditioningLength + textIds.Length;
            var attentionLength = sequenceLength;
            var firstPosition = 0;
            var stopped = false;
            var lastToken = -1;
            var runLength = 0;

            using (var state = _networks.BeginGeneration())
            {
                for (var step = 0; step < bound; step++)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);

                    var logits = state.Step(inputs, sequenceLength, attentionLength, firstPosition);
                    // Run-length cap: once a token has repeated the allowed number of times, exclude it for this step
                    var blocked = _maxConsecutiveSpeechTokenRepeats > 0 && runLength >= _maxConsecutiveSpeechTokenRepeats
                        ? lastToken
                        : -1;
                    var token = ChatterboxTurboTokenSelector.Select(logits, penalized, blocked);
                    runLength = token == lastToken ? runLength + 1 : 1;
                    lastToken = token;
                    generated.Add(token);
                    penalized.Add(token);
                    if (token == StopSpeechToken)
                    {
                        stopped = true;
                        break;
                    }

                    // Next step feeds only the newest token, one position later, with a one-longer mask
                    inputs = _networks.Embed([token]);
                    firstPosition = attentionLength;
                    attentionLength++;
                    sequenceLength = 1;
                }
            }

            // Assemble the decoder input: prompt, speech (minus start/stop), trailing silence
            var speech = generated.Skip(1).Where(token => token is not StopSpeechToken and not StartSpeechToken).ToArray();
            var decoderTokens = new long[conditioning.PromptTokens.Length + speech.Length + TrailingSilenceTokenCount];
            conditioning.PromptTokens.CopyTo(decoderTokens, 0);
            speech.CopyTo(decoderTokens, conditioning.PromptTokens.Length);
            Array.Fill(decoderTokens, SilenceToken, conditioning.PromptTokens.Length + speech.Length, TrailingSilenceTokenCount);

            var waveform = _networks.Decode(decoderTokens, conditioning);
            return new ChatterboxTurboGenerationResult(new EngineAudio(waveform, OutputSampleRate), !stopped, speech.Length);
        }
    }

    /// <summary>
    ///     Releases the networks. Safe to call more than once.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Flag first so an in-flight Generate stops at its next step, then wait for it before releasing
        _disposed = true;
        lock (_gate)
        {
            _networks.Dispose();
        }
    }

    /// <summary>Returns the voice's cached conditioning, encoding it on first use.</summary>
    private ChatterboxTurboConditioning GetConditioning(int voiceIndex)
    {
        if (!_conditioningCache.TryGetValue(voiceIndex, out var conditioning))
        {
            conditioning = _networks.Encode(_voices[voiceIndex].Samples.ToArray());
            _conditioningCache[voiceIndex] = conditioning;
        }

        return conditioning;
    }
}
