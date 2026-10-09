using DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace DemaConsulting.Speech.Onnx.Kokoro.SynthesisSubsystem;

/// <summary>
///     Real, production <see cref="ISynthesisBackend"/> running Kokoro-82M v1.0's bare ONNX graph
///     directly through ONNX Runtime - this package's first raw-ONNX synthesis engine, with no
///     bundled native inference engine (unlike the sibling <c>DemaConsulting.Speech.Sherpa</c>
///     package's sherpa-onnx-backed engines).
/// </summary>
/// <remarks>
///     <b>Inference contract</b> (confirmed directly from <c>onnx-community/Kokoro-82M-v1.0-ONNX</c>'s
///     own README, downloaded and re-read in this project's development sandbox, and proven
///     end-to-end against a real loaded session in this package's Python validation spike before
///     this class was written): a single forward pass, no autoregressive loop and no KV-cache.
///     Inputs - <c>input_ids</c> (<see cref="long"/>, shape <c>[1, N]</c>, the phoneme token ids
///     padded with <see cref="KokoroPhonemeVocabulary.PadTokenId"/> at both the start and end),
///     <c>style</c> (<see cref="float"/>, shape <c>[1, 256]</c>, a voice's own style vector,
///     selected by phoneme-token count - see <see cref="SelectStyleVector"/>), and <c>speed</c>
///     (<see cref="float"/>, shape <c>[1]</c>, <c>1.0</c> = this voice's normal rate). Output: a
///     single raw waveform tensor at this model's own fixed <see cref="SampleRate"/>
///     (<c>24000</c>), confirmed the same way.
///     <para>
///     <b>Phonemization is owned entirely by this class's constructor-supplied
///     <see cref="KokoroLexiconPhonemizer"/></b>, not by the ONNX graph itself (unlike sherpa-onnx,
///     which bundles phonemization inside its own native library) - see that class's remarks for
///     why a verified lexicon lookup is used instead of a hand-written grapheme-to-phoneme
///     implementation, and for this package's honestly-documented Stage-1 out-of-vocabulary-word
///     limitation.
///     </para>
/// </remarks>
internal sealed class OnnxKokoroSynthesisEngine : ISynthesisBackend
{
    /// <summary>This model's own fixed output sample rate, in Hz, confirmed from its own README and this package's validation spike.</summary>
    internal const int SampleRate = 24000;

    /// <summary>The length, in float32 elements, of one voice's single style vector row.</summary>
    private const int StyleVectorWidth = 256;

    private readonly InferenceSession _session;
    private readonly KokoroPhonemeVocabulary _vocabulary;
    private readonly KokoroLexiconPhonemizer _phonemizer;
    private readonly IReadOnlyDictionary<int, float[]> _voiceStylesBySpeakerId;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OnnxKokoroSynthesisEngine"/> class, taking
    ///     ownership of <paramref name="session"/> (disposed together with this engine).
    /// </summary>
    /// <param name="session">The loaded ONNX Runtime session for Kokoro v1.0's model graph. Must not be null.</param>
    /// <param name="vocabulary">The phoneme character -&gt; token id vocabulary. Must not be null.</param>
    /// <param name="phonemizer">The text -&gt; phoneme-string converter. Must not be null.</param>
    /// <param name="voiceStylesBySpeakerId">
    ///     Every installed voice's raw style-vector file contents (each a flattened
    ///     <c>(rows, 256)</c> float32 array, read directly from that voice's <c>.bin</c> file),
    ///     keyed by the engine-specific integer speaker id <c>ISynthesisModel.ResolveSpeakerId</c>
    ///     resolves a selection to. Must not be null or empty.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when any parameter is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="voiceStylesBySpeakerId"/> is empty.
    /// </exception>
    public OnnxKokoroSynthesisEngine(
        InferenceSession session,
        KokoroPhonemeVocabulary vocabulary,
        KokoroLexiconPhonemizer phonemizer,
        IReadOnlyDictionary<int, float[]> voiceStylesBySpeakerId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(vocabulary);
        ArgumentNullException.ThrowIfNull(phonemizer);
        ArgumentNullException.ThrowIfNull(voiceStylesBySpeakerId);
        if (voiceStylesBySpeakerId.Count == 0)
        {
            throw new ArgumentException(
                "At least one voice style vector must be supplied.",
                nameof(voiceStylesBySpeakerId));
        }

        _session = session;
        _vocabulary = vocabulary;
        _phonemizer = phonemizer;
        _voiceStylesBySpeakerId = voiceStylesBySpeakerId;
    }

    /// <inheritdoc/>
    int ISynthesisBackend.SampleRate => SampleRate;

    /// <inheritdoc/>
    /// <remarks>
    ///     An empty <paramref name="text"/>, or text whose every word is absent from the embedded
    ///     lexicon (see <see cref="KokoroLexiconPhonemizer"/>'s remarks), produces no phoneme
    ///     tokens at all; this method recognizes that case and returns an empty
    ///     <see cref="EngineAudio"/> directly rather than running the ONNX graph on a
    ///     content-free, all-padding input.
    /// </remarks>
    public EngineAudio Generate(string text, float speed, int speakerId)
    {
        ArgumentNullException.ThrowIfNull(text);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var (phonemes, _) = _phonemizer.Phonemize(text);
        var ids = _vocabulary.ToTokenIds(phonemes);
        if (ids.Count == 0)
        {
            return new EngineAudio([], SampleRate);
        }

        var paddedIds = new long[ids.Count + 2];
        paddedIds[0] = KokoroPhonemeVocabulary.PadTokenId;
        for (var i = 0; i < ids.Count; i++)
        {
            paddedIds[i + 1] = ids[i];
        }

        paddedIds[^1] = KokoroPhonemeVocabulary.PadTokenId;

        var inputIdsTensor = new DenseTensor<long>(paddedIds, [1, paddedIds.Length]);
        var styleTensor = new DenseTensor<float>(SelectStyleVector(speakerId, ids.Count), [1, StyleVectorWidth]);
        var speedTensor = new DenseTensor<float>(new[] { speed }, [1]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("style", styleTensor),
            NamedOnnxValue.CreateFromTensor("speed", speedTensor),
        };

        using var results = _session.Run(inputs);
        var samples = results[0].AsTensor<float>().ToArray();

        return new EngineAudio(samples, SampleRate);
    }

    /// <summary>
    ///     Selects the 256-element style vector row for <paramref name="speakerId"/> at the row
    ///     index matching <paramref name="tokenCount"/>, exactly mirroring the proven Python
    ///     reference pipeline's own <c>voices[len(ids)]</c> lookup - Kokoro's style vectors are
    ///     indexed by utterance phoneme-token length, not merely by voice identity.
    /// </summary>
    private float[] SelectStyleVector(int speakerId, int tokenCount)
    {
        // An unrecognized speaker id degrades to the first available voice rather than throwing -
        // ISynthesisModel.ResolveSpeakerId already guarantees a recognized id for every id it
        // itself returns, but this engine is defensive against being handed an id from outside
        // that contract too.
        if (!_voiceStylesBySpeakerId.TryGetValue(speakerId, out var voice))
        {
            voice = _voiceStylesBySpeakerId.Values.First();
        }

        var rowCount = voice.Length / StyleVectorWidth;
        var rowIndex = Math.Clamp(tokenCount, 0, rowCount - 1);

        var row = new float[StyleVectorWidth];
        Array.Copy(voice, rowIndex * StyleVectorWidth, row, 0, StyleVectorWidth);
        return row;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _session.Dispose();
        _disposed = true;
    }
}
