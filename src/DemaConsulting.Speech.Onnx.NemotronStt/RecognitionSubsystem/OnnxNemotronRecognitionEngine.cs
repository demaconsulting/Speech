using System.Runtime.InteropServices;
using DemaConsulting.Speech.Onnx.NemotronStt.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;

/// <summary>
///     The streaming <see cref="IRecognitionBackend"/> for the Nemotron ONNX model: runs incoming
///     audio through the leading-silence limiter, dither, log-mel feature extraction, the
///     cache-aware encoder (in 56-frame chunks with a 9-frame pre-encode context), and the RNN-T
///     greedy decoder, and reports provisional and final text.
/// </summary>
/// <remarks>
///     <b>Flow</b>: <see cref="AcceptSamples"/> only buffers - samples pass through
///     <see cref="SilenceRunLimiter"/>, then <see cref="DitherNoise"/>, then
///     <see cref="NemotronFeatureExtractor"/>; <see cref="TryDecode"/> runs every complete chunk
///     through the encoder and decoder and reports the utterance text so far as a provisional
///     result whenever it changes. Nothing here is thread-safe, matching the backend contract.
///     <para>
///     <b>Endpointing</b>: an utterance is finalized (a final result is reported, the text
///     cleared) when either (1) the input has been quiet for <see cref="NemotronEngineOptions.EndpointQuietMs"/>
///     after speech - measured on the <i>input</i> clock using the limiter's quiet-run length,
///     because the limiter itself removes most of a long quiet run so the chunk clock stalls - in
///     which case the remaining buffered audio is padded and decoded first and the model state is
///     reset for a clean next utterance, or (2) <see cref="NemotronEngineOptions.EndpointEmptyChunks"/>
///     consecutive chunks emit no token after text exists (the fallback for steady noise that never
///     reads as quiet), in which case the model state is kept so decoding continues seamlessly.
///     </para>
///     <para>
///     <b>Flush</b> (<see cref="TryFlush"/>): feeds the limiter's partial frame, applies the
///     feature extractor's right reflect pad, zero-pads the final partial chunk, decodes it plus
///     <see cref="NemotronEngineOptions.FlushTailChunks"/> all-zero chunks, and finalizes the text.
///     </para>
/// </remarks>
internal sealed class OnnxNemotronRecognitionEngine : IRecognitionBackend
{
    /// <summary>Frames in one encoder input: pre-encode context plus the new chunk.</summary>
    private const int EncoderFrames = NemotronEngineOptions.PreEncodeFrames + NemotronEngineOptions.ChunkFrames;

    /// <summary>The quiet-run length (the limiter's 400 ms cap) after which a partial chunk is padded.</summary>
    private const int PadAfterQuietSamples = 6400;

    /// <summary>The encoder.</summary>
    private readonly INemotronEncoder _encoder;

    /// <summary>The greedy decoder.</summary>
    private readonly NemotronRnntGreedyDecoder _decoder;

    /// <summary>The vocabulary used to detokenize.</summary>
    private readonly NemotronVocabulary _vocabulary;

    /// <summary>The constants in use.</summary>
    private readonly NemotronEngineOptions _options;

    /// <summary>The leading-silence limiter.</summary>
    private readonly SilenceRunLimiter _limiter;

    /// <summary>The dither source.</summary>
    private readonly DitherNoise _dither;

    /// <summary>The feature extractor.</summary>
    private readonly NemotronFeatureExtractor _extractor = new();

    /// <summary>The limiter output for the current call.</summary>
    private readonly List<float> _limited = [];

    /// <summary>The encoder input: pre-encode context rows followed by the chunk rows.</summary>
    private readonly float[] _chunkInput = new float[EncoderFrames * NemotronFeatureExtractor.MelBands];

    /// <summary>The pre-encode context carried to the next chunk (the previous chunk's last rows).</summary>
    private readonly float[] _preEncode = new float[NemotronEngineOptions.PreEncodeFrames * NemotronFeatureExtractor.MelBands];

    /// <summary>The token ids of the utterance in progress.</summary>
    private readonly List<int> _tokens = [];

    /// <summary>Results waiting to be returned.</summary>
    private readonly Queue<SpeechRecognitionResult> _results = new();

    /// <summary>The input-clock quiet duration, in samples, that triggers endpointing.</summary>
    private readonly long _endpointQuietSamples;

    /// <summary>The text last reported as a provisional result.</summary>
    private string _lastReported = string.Empty;

    /// <summary>Whether speech (or any token) has been seen since the last model reset.</summary>
    private bool _dirty;

    /// <summary>Whether any audio has reached the feature extractor since the last model reset.</summary>
    private bool _audioSinceReset;

    /// <summary>Whether an input-clock endpoint is waiting to be processed.</summary>
    private bool _endpointRequested;

    /// <summary>The count of consecutive token-free chunks since text last changed.</summary>
    private int _emptyChunks;

    /// <summary>The number of chunks decoded since the last model reset.</summary>
    private int _chunksDecoded;

    /// <summary>Whether <see cref="Dispose"/> has run.</summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OnnxNemotronRecognitionEngine"/> class.
    /// </summary>
    /// <param name="encoder">The encoder; ownership transfers to this instance.</param>
    /// <param name="decoder">The greedy decoder; ownership transfers to this instance.</param>
    /// <param name="vocabulary">The vocabulary.</param>
    /// <param name="options">The constants, or <see langword="null"/> for the documented defaults.</param>
    public OnnxNemotronRecognitionEngine(
        INemotronEncoder encoder,
        NemotronRnntGreedyDecoder decoder,
        NemotronVocabulary vocabulary,
        NemotronEngineOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(encoder);
        ArgumentNullException.ThrowIfNull(decoder);
        ArgumentNullException.ThrowIfNull(vocabulary);

        _encoder = encoder;
        _decoder = decoder;
        _vocabulary = vocabulary;
        _options = options ?? new NemotronEngineOptions();
        _limiter = new SilenceRunLimiter(_options.Limiter);
        _dither = new DitherNoise(_options.DitherAmplitude, _options.DitherSeed);
        _endpointQuietSamples = (long)_options.EndpointQuietMs * NemotronFeatureExtractor.SampleRate / 1000;
    }

    /// <summary>Gets the model's required input sample rate, in hertz.</summary>
    public static int SampleRate => NemotronFeatureExtractor.SampleRate;

    /// <inheritdoc/>
    public void AcceptSamples(ReadOnlySpan<float> monoSamples)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (monoSamples.IsEmpty)
        {
            return;
        }

        var loudBefore = _limiter.LoudSamples;
        _limited.Clear();
        _limiter.Process(monoSamples, _limited);
        FeedExtractor();

        if (_limiter.LoudSamples > loudBefore)
        {
            _dirty = true;
        }

        if (_dirty && _limiter.QuietRunSamples >= _endpointQuietSamples)
        {
            _endpointRequested = true;
        }
        else if (_dirty && _limiter.QuietRunSamples >= PadAfterQuietSamples)
        {
            PadPartialChunk();
        }
    }

    /// <summary>
    ///     Completes a partly filled chunk with silence. Once the limiter starts dropping a quiet
    ///     run no further audio arrives, so the last words would otherwise wait in the incomplete
    ///     chunk until the endpoint; the silence stands in for the dropped quiet audio.
    /// </summary>
    private void PadPartialChunk()
    {
        if (_extractor.FrameCount == 0 || _extractor.FrameCount >= NemotronEngineOptions.ChunkFrames)
        {
            return;
        }

        var silence = new float[NemotronFeatureExtractor.HopLength];
        while (_extractor.FrameCount < NemotronEngineOptions.ChunkFrames)
        {
            Array.Clear(silence);
            _dither.Apply(silence);
            _extractor.Accept(silence);
        }
    }

    /// <inheritdoc/>
    public bool TryDecode(out SpeechRecognitionResult? result)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_results.Count == 0)
        {
            Pump();
        }

        return _results.TryDequeue(out result);
    }

    /// <inheritdoc/>
    public bool TryFlush(out SpeechRecognitionResult? result)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _limited.Clear();
        _limiter.Flush(_limited);
        FeedExtractor();
        Pump();
        FinishUtterance();

        // Return the final result; any earlier provisional ones are superseded by it.
        result = null;
        while (_results.TryDequeue(out var queued))
        {
            if (queued.IsFinal)
            {
                result = queued;
            }
        }

        return result is not null;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _limiter.Reset();
        _dither.Reset();
        _results.Clear();
        ResetModelState();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _decoder.Dispose();
        _encoder.Dispose();
    }

    /// <summary>Dithers the limiter's output and hands it to the feature extractor.</summary>
    private void FeedExtractor()
    {
        if (_limited.Count == 0)
        {
            return;
        }

        var span = CollectionsMarshal.AsSpan(_limited);
        _dither.Apply(span);
        _extractor.Accept(span);
        _audioSinceReset = true;
    }

    /// <summary>Decodes every complete chunk, reports changed text, and handles a pending endpoint.</summary>
    private void Pump()
    {
        while (_extractor.FrameCount >= NemotronEngineOptions.ChunkFrames)
        {
            DecodeChunk(NemotronEngineOptions.ChunkFrames);
            if (_emptyChunks >= _options.EndpointEmptyChunks && _tokens.Count > 0)
            {
                ReportProvisional();
                ReportFinalKeepingState();
            }
        }

        ReportProvisional();

        if (_endpointRequested)
        {
            FinishUtterance();
        }
    }

    /// <summary>
    ///     Decodes the audio still buffered (zero-padded to whole chunks plus the tail chunks),
    ///     reports the final text, and resets the model state for the next utterance.
    /// </summary>
    private void FinishUtterance()
    {
        if (_audioSinceReset)
        {
            _extractor.Flush();
            while (_extractor.FrameCount >= NemotronEngineOptions.ChunkFrames)
            {
                DecodeChunk(NemotronEngineOptions.ChunkFrames);
            }

            if (_extractor.FrameCount > 0)
            {
                DecodeChunk(_extractor.FrameCount);
            }

            if (_chunksDecoded > 0)
            {
                for (var i = 0; i < _options.FlushTailChunks; i++)
                {
                    DecodeChunk(0);
                }
            }
        }

        ReportFinalKeepingState();
        ResetModelState();
    }

    /// <summary>
    ///     Runs one encoder chunk made of <paramref name="realFrames"/> queued feature frames
    ///     zero-padded to the full chunk length, then decodes its output.
    /// </summary>
    private void DecodeChunk(int realFrames)
    {
        const int mels = NemotronFeatureExtractor.MelBands;
        const int preFloats = NemotronEngineOptions.PreEncodeFrames * mels;
        const int chunkFloats = NemotronEngineOptions.ChunkFrames * mels;

        _preEncode.CopyTo(_chunkInput, 0);
        var chunk = _chunkInput.AsSpan(preFloats, chunkFloats);
        _extractor.Dequeue(realFrames, chunk);
        chunk[(realFrames * mels)..].Clear();
        chunk[(chunkFloats - preFloats)..].CopyTo(_preEncode);

        var output = _encoder.Encode(_chunkInput, EncoderFrames, out var outputFrames);
        var emitted = _decoder.Decode(output, outputFrames, _encoder.HiddenSize, _tokens);
        _chunksDecoded++;

        if (emitted > 0)
        {
            _emptyChunks = 0;
            _dirty = true;
        }
        else if (_tokens.Count > 0)
        {
            _emptyChunks++;
        }
    }

    /// <summary>Queues a provisional result when the utterance text has changed.</summary>
    private void ReportProvisional()
    {
        if (_tokens.Count == 0)
        {
            return;
        }

        var text = _vocabulary.Detokenize(_tokens);
        if (text.Length > 0 && !string.Equals(text, _lastReported, StringComparison.Ordinal))
        {
            _lastReported = text;
            _results.Enqueue(new SpeechRecognitionResult(text, IsFinal: false));
        }
    }

    /// <summary>Queues the utterance text as a final result (when non-empty) and clears it, keeping the model state.</summary>
    private void ReportFinalKeepingState()
    {
        var text = _tokens.Count == 0 ? string.Empty : _vocabulary.Detokenize(_tokens);
        if (text.Length > 0)
        {
            _results.Enqueue(new SpeechRecognitionResult(text, IsFinal: true));
        }

        _tokens.Clear();
        _lastReported = string.Empty;
        _emptyChunks = 0;
    }

    /// <summary>Returns the encoder, decoder, extractor, and utterance bookkeeping to the start-of-stream state (the limiter is untouched).</summary>
    private void ResetModelState()
    {
        _encoder.Reset();
        _decoder.Reset();
        _extractor.Reset();
        Array.Clear(_preEncode);
        _tokens.Clear();
        _lastReported = string.Empty;
        _dirty = false;
        _audioSinceReset = false;
        _endpointRequested = false;
        _emptyChunks = 0;
        _chunksDecoded = 0;
    }
}
