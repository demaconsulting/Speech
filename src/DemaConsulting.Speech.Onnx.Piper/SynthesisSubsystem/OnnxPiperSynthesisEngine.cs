using System.Collections.Concurrent;
using DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace DemaConsulting.Speech.Onnx.Piper.SynthesisSubsystem;

/// <summary>
///     Production <see cref="IAnnotatedSynthesisBackend"/> running a Piper (VITS) voice's ONNX graph directly through ONNX
///     Runtime, with no bundled native inference engine and no espeak-ng.
/// </summary>
/// <remarks>
///     <para>
///     <b>Inference contract</b> (the standard Piper export, read from the loaded model's own input metadata in this
///     repository's local real-model test): a single forward pass with inputs <c>input</c> (<see cref="long"/>,
///     <c>[1, N]</c>, the framed phoneme ids), <c>input_lengths</c> (<see cref="long"/>, <c>[1]</c>, N),
///     <c>scales</c> (<see cref="float"/>, <c>[3]</c>: noise scale, length scale, noise width) and <c>sid</c>
///     (<see cref="long"/>, <c>[1]</c>, the speaker). The output is the raw waveform at the voice configuration's own
///     sample rate. Speed is applied through the length scale: the voice's own recommended length scale divided by the
///     requested speed, so a speed above 1 is faster.
///     </para>
///     <para>
///     Phonemization is owned by the constructor-supplied <see cref="PiperPhonemePipeline"/>, not by the graph. As an
///     <see cref="IAnnotatedSynthesisBackend"/> the engine uses the annotations of the model's English text analysis
///     (spoken forms of numbers and abbreviations, the chosen reading of each heteronym); without annotations the text is
///     spoken as written, with default readings. A segment too long for one utterance is spoken in parts and the audio
///     joined.
///     </para>
/// </remarks>
internal sealed class OnnxPiperSynthesisEngine : IAnnotatedSynthesisBackend
{
    /// <summary>The graph's phoneme-id input name.</summary>
    private const string InputTensorName = "input";

    /// <summary>The graph's input-length input name.</summary>
    private const string InputLengthsTensorName = "input_lengths";

    /// <summary>The graph's inference-scales input name.</summary>
    private const string ScalesTensorName = "scales";

    /// <summary>The graph's speaker input name.</summary>
    private const string SpeakerTensorName = "sid";

    /// <summary>A sentence-sized text for the probe inference.</summary>
    private const string ProbeText = "This is a probe sentence for the execution provider.";

    private readonly InferenceSession _session;
    private readonly PiperVoiceConfig _config;
    private readonly PiperPhonemePipeline _pipeline;
    private readonly ConcurrentDictionary<string, long> _unknownSymbols = new(StringComparer.Ordinal);
    private long _unknownSymbolCount;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OnnxPiperSynthesisEngine"/> class, taking ownership of
    ///     <paramref name="session"/> (disposed together with this engine).
    /// </summary>
    /// <param name="session">The loaded ONNX Runtime session of the voice's graph. Must not be null.</param>
    /// <param name="config">The voice configuration. Must not be null.</param>
    /// <param name="pipeline">The text to phoneme-id converter. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is <see langword="null"/>.</exception>
    public OnnxPiperSynthesisEngine(InferenceSession session, PiperVoiceConfig config, PiperPhonemePipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(pipeline);
        _session = session;
        _config = config;
        _pipeline = pipeline;
    }

    /// <inheritdoc/>
    public int SampleRate => _config.SampleRate;

    /// <summary>
    ///     Gets how many phoneme symbols the engine has had to skip because the voice's symbol table does not contain
    ///     them, over its lifetime. It stays zero for text the shipped phonemizer produced; a non-zero value is a defect
    ///     to investigate.
    /// </summary>
    public long UnknownSymbolCount => Interlocked.Read(ref _unknownSymbolCount);

    /// <summary>Gets the distinct symbols that were skipped, in no particular order.</summary>
    public IReadOnlyCollection<string> UnknownSymbols => [.. _unknownSymbols.Keys];

    /// <inheritdoc/>
    /// <remarks>
    ///     Speaks <paramref name="text"/> as written: every word gets its default reading and nothing is normalized.
    ///     Text with nothing to say returns an empty <see cref="BackendAudio"/> without running the graph.
    /// </remarks>
    public BackendAudio Generate(string text, float speed, int speakerId)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Synthesize(text, null, speed, speakerId);
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Uses the <c>en.norm</c>, <c>en.spell</c>, <c>en.reading</c> and <c>en.pron</c> annotations of
    ///     <c>DemaConsulting.Speech.Text.En</c> (offsets relative to <paramref name="text"/>) and ignores every other kind.
    /// </remarks>
    public BackendAudio GenerateAnnotated(string text, IReadOnlyList<SegmentAnnotation> annotations, float speed, int speakerId)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(annotations);
        return Synthesize(text, annotations, speed, speakerId);
    }

    /// <summary>
    ///     Runs one representative inference on <paramref name="session"/> (roughly sentence-length, so shape-dependent
    ///     operator failures surface) and throws <see cref="OnnxRuntimeException"/> if the session's execution provider
    ///     cannot run this graph. Used by <see cref="Onnx.OnnxRuntimeSubsystem.OnnxExecutionProviderSelector"/> to fall back
    ///     to the next provider.
    /// </summary>
    /// <param name="session">The session to probe.</param>
    /// <param name="config">The voice configuration.</param>
    /// <param name="pipeline">The pipeline whose encoding builds the probe input.</param>
    /// <exception cref="InvalidOperationException">The voice's symbol table cannot encode the probe text.</exception>
    internal static void RunProbeInference(InferenceSession session, PiperVoiceConfig config, PiperPhonemePipeline pipeline)
    {
        var probe = pipeline.ToUtterances(ProbeText, null).FirstOrDefault()
            ?? throw new InvalidOperationException("The Piper voice's symbol table cannot encode any symbol of the probe text.");

        _ = RunInference(session, probe.Ids, [config.NoiseScale, config.LengthScale, config.NoiseW], 0);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Mark disposed before releasing the native session so a concurrent Generate or Dispose observes the flag
        // rather than racing the release.
        _disposed = true;
        _session.Dispose();
    }

    /// <summary>
    ///     Phonemizes <paramref name="text"/> and runs the graph once per utterance.
    /// </summary>
    private BackendAudio Synthesize(string text, IReadOnlyList<SegmentAnnotation>? annotations, float speed, int speakerId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var effectiveSpeed = speed > 0f && float.IsFinite(speed) ? speed : 1.0f;
        float[] scales = [_config.NoiseScale, _config.LengthScale / effectiveSpeed, _config.NoiseW];
        var speaker = Math.Clamp(speakerId, 0, Math.Max(0, _config.NumSpeakers - 1));

        List<float[]> audio = [];
        foreach (var utterance in _pipeline.ToUtterances(text, annotations))
        {
            ReportUnknownSymbols(utterance);
            audio.Add(RunInference(_session, utterance.Ids, scales, speaker));
        }

        return audio.Count switch
        {
            0 => new BackendAudio([], SampleRate),
            1 => new BackendAudio(audio[0], SampleRate),
            _ => new BackendAudio(audio.SelectMany(samples => samples).ToArray(), SampleRate),
        };
    }

    /// <summary>
    ///     Counts the symbols of <paramref name="utterance"/> that the voice's table does not contain.
    /// </summary>
    private void ReportUnknownSymbols(PiperUtterance utterance)
    {
        if (utterance.UnknownSymbols.Count == 0)
        {
            return;
        }

        foreach (var symbol in utterance.UnknownSymbols)
        {
            _unknownSymbols.AddOrUpdate(symbol, 1, (_, count) => count + 1);
        }

        Interlocked.Add(ref _unknownSymbolCount, utterance.UnknownSymbols.Count);
    }

    /// <summary>
    ///     Runs the graph once.
    /// </summary>
    private static float[] RunInference(InferenceSession session, IReadOnlyList<long> ids, float[] scales, int speaker)
    {
        var idArray = ids.ToArray();
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(InputTensorName, new DenseTensor<long>(idArray, [1, idArray.Length])),
            NamedOnnxValue.CreateFromTensor(InputLengthsTensorName, new DenseTensor<long>(new long[] { idArray.Length }, [1])),
            NamedOnnxValue.CreateFromTensor(ScalesTensorName, new DenseTensor<float>(scales, [scales.Length])),
            NamedOnnxValue.CreateFromTensor(SpeakerTensorName, new DenseTensor<long>(new long[] { speaker }, [1])),
        };

        using var results = session.Run(inputs);
        return results[0].AsTensor<float>().ToArray();
    }
}
