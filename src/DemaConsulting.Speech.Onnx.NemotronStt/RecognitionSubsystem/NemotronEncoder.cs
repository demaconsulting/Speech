using Microsoft.ML.OnnxRuntime;

namespace DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;

/// <summary>
///     ONNX Runtime implementation of <see cref="INemotronEncoder"/>: runs the cache-aware
///     streaming FastConformer encoder one chunk at a time and feeds its three cache outputs back
///     as the next call's cache inputs.
/// </summary>
/// <remarks>
///     Tensor contract (from the model's <c>genai_config.json</c> and the reference spike):
///     inputs <c>audio_signal</c> [1, frames, 128] float, <c>length</c> [1] int64,
///     <c>cache_last_channel</c> [1, 24, 56, 1024] float, <c>cache_last_time</c> [1, 24, 1024, 8]
///     float, <c>cache_last_channel_len</c> [1] int64, and <c>lang_id</c> [1] int64; outputs
///     <c>outputs</c> [1, T, 1024] float, <c>encoded_lengths</c> [1] int64, and the three
///     <c>*_next</c> caches. The caches stay in native <see cref="OrtValue"/>s and are passed back
///     without copying; the previous call's outputs are released only after the next call has
///     consumed them.
/// </remarks>
internal sealed class NemotronEncoder : INemotronEncoder
{
    /// <summary>The encoder output hidden size.</summary>
    public const int EncoderHiddenSize = 1024;

    /// <summary>The number of encoder layers (cache first dimension after batch).</summary>
    private const int Layers = 24;

    /// <summary>The attention (left-context) cache length in frames.</summary>
    private const int AttentionCache = 56;

    /// <summary>The convolution cache length in frames.</summary>
    private const int ConvolutionCache = 8;

    /// <summary>The model's input tensor names, in call order.</summary>
    private static readonly string[] InputNames =
    [
        "audio_signal", "length", "cache_last_channel", "cache_last_time", "cache_last_channel_len", "lang_id",
    ];

    /// <summary>The model's output tensor names, in result order.</summary>
    private static readonly string[] OutputNames =
    [
        "outputs", "encoded_lengths", "cache_last_channel_next", "cache_last_time_next", "cache_last_channel_len_next",
    ];

    /// <summary>The ONNX Runtime session.</summary>
    private readonly InferenceSession _session;

    /// <summary>Whether this instance disposes <see cref="_session"/>.</summary>
    private readonly bool _ownsSession;

    /// <summary>The language id fed on every call.</summary>
    private readonly long[] _languageId;

    /// <summary>The per-call options (never cancelled; kept to avoid per-call allocation).</summary>
    private readonly RunOptions _runOptions = new();

    /// <summary>The managed feature array backing the audio input tensor.</summary>
    private float[] _input = [];

    /// <summary>The cache tensors currently fed to the next call: results of the previous call, or zero-initialized tensors.</summary>
    private readonly OrtValue[] _caches = new OrtValue[3];

    /// <summary>The previous call's outputs, released after the next call.</summary>
    private IDisposableReadOnlyCollection<OrtValue>? _previousResults;

    /// <summary>The zero-initialized cache tensors (and their backing arrays) awaiting disposal after the first call.</summary>
    private readonly List<IDisposable> _initialCaches = [];

    /// <summary>Whether <see cref="Dispose"/> has run.</summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NemotronEncoder"/> class.
    /// </summary>
    /// <param name="session">The loaded encoder session.</param>
    /// <param name="languageId">The <c>lang_id</c> input value.</param>
    /// <param name="ownsSession">Whether disposing this instance also disposes <paramref name="session"/>.</param>
    public NemotronEncoder(InferenceSession session, int languageId, bool ownsSession = true)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _ownsSession = ownsSession;
        _languageId = [languageId];
        Reset();
    }

    /// <inheritdoc/>
    public int HiddenSize => EncoderHiddenSize;

    /// <summary>
    ///     Probe inference for <see cref="Onnx.OnnxRuntimeSubsystem.OnnxExecutionProviderSelector.Create"/>:
    ///     runs one all-zero chunk so an accelerated provider that loads yet cannot run the graph
    ///     is discarded in favor of the CPU provider.
    /// </summary>
    /// <param name="session">The candidate session.</param>
    internal static void RunProbeInference(InferenceSession session)
    {
        using var encoder = new NemotronEncoder(session, 0, ownsSession: false);
        const int frames = NemotronEngineOptions.PreEncodeFrames + NemotronEngineOptions.ChunkFrames;
        encoder.Encode(new float[frames * NemotronFeatureExtractor.MelBands], frames, out _);
    }

    /// <inheritdoc/>
    public ReadOnlySpan<float> Encode(ReadOnlySpan<float> features, int frameCount, out int outputFrames)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var floats = frameCount * NemotronFeatureExtractor.MelBands;
        if (_input.Length != floats)
        {
            _input = new float[floats];
        }

        features[..floats].CopyTo(_input);

        var lengthArray = new long[] { frameCount };
        using var audio = OrtValue.CreateTensorValueFromMemory(
            _input, [1, frameCount, NemotronFeatureExtractor.MelBands]);
        using var length = OrtValue.CreateTensorValueFromMemory(lengthArray, [1]);
        using var language = OrtValue.CreateTensorValueFromMemory(_languageId, [1]);

        var results = _session.Run(
            _runOptions,
            InputNames,
            [audio, length, _caches[0], _caches[1], _caches[2], language],
            OutputNames);

        _previousResults?.Dispose();
        _previousResults = results;
        DisposeInitialCaches();
        _caches[0] = results[2];
        _caches[1] = results[3];
        _caches[2] = results[4];

        outputFrames = (int)results[1].GetTensorDataAsSpan<long>()[0];
        var output = results[0].GetTensorDataAsSpan<float>();
        outputFrames = Math.Min(outputFrames, output.Length / EncoderHiddenSize);
        return output;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _previousResults?.Dispose();
        _previousResults = null;
        DisposeInitialCaches();

        _caches[0] = CreateZeroTensor(Layers * AttentionCache * EncoderHiddenSize, [1, Layers, AttentionCache, EncoderHiddenSize]);
        _caches[1] = CreateZeroTensor(Layers * EncoderHiddenSize * ConvolutionCache, [1, Layers, EncoderHiddenSize, ConvolutionCache]);
        _caches[2] = CreateZeroInt64Tensor();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _previousResults?.Dispose();
        _previousResults = null;
        DisposeInitialCaches();
        _runOptions.Dispose();
        if (_ownsSession)
        {
            _session.Dispose();
        }
    }

    /// <summary>Creates a zero-filled float tensor over a managed array that stays alive until disposed.</summary>
    private OrtValue CreateZeroTensor(int length, long[] shape)
    {
        var tensor = OrtValue.CreateTensorValueFromMemory(new float[length], shape);
        _initialCaches.Add(tensor);
        return tensor;
    }

    /// <summary>Creates the zero-valued <c>cache_last_channel_len</c> tensor.</summary>
    private OrtValue CreateZeroInt64Tensor()
    {
        var tensor = OrtValue.CreateTensorValueFromMemory(new long[1], [1]);
        _initialCaches.Add(tensor);
        return tensor;
    }

    /// <summary>Disposes the zero-initialized cache tensors.</summary>
    private void DisposeInitialCaches()
    {
        foreach (var initial in _initialCaches)
        {
            initial.Dispose();
        }

        _initialCaches.Clear();
    }
}
