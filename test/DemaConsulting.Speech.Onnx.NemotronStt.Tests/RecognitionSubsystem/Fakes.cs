using DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.RecognitionSubsystem;

/// <summary>A scripted <see cref="IRnntNetwork"/> double: returns queued tokens, then blank.</summary>
internal sealed class FakeRnntNetwork : IRnntNetwork
{
    /// <summary>The scripted tokens, consumed one per prediction.</summary>
    private readonly Queue<int> _script;

    /// <summary>Initializes the fake with a blank id and a token script.</summary>
    public FakeRnntNetwork(int blankId, params int[] script)
    {
        BlankId = blankId;
        _script = new Queue<int>(script);
    }

    /// <inheritdoc/>
    public int BlankId { get; }

    /// <summary>Gets the tokens the decoder advanced the prediction network with.</summary>
    public List<int> Advanced { get; } = [];

    /// <summary>Gets the number of resets.</summary>
    public int ResetCount { get; private set; }

    /// <summary>Gets the number of predictions requested.</summary>
    public int Predictions { get; private set; }

    /// <summary>Gets a value indicating whether the fake was disposed.</summary>
    public bool Disposed { get; private set; }

    /// <summary>Queues more scripted tokens.</summary>
    public void Enqueue(params int[] tokens)
    {
        foreach (var token in tokens)
        {
            _script.Enqueue(token);
        }
    }

    /// <inheritdoc/>
    public void Reset() => ResetCount++;

    /// <inheritdoc/>
    public void Advance(int token) => Advanced.Add(token);

    /// <inheritdoc/>
    public int PredictToken(ReadOnlySpan<float> encoderFrame)
    {
        Predictions++;
        return _script.Count > 0 ? _script.Dequeue() : BlankId;
    }

    /// <inheritdoc/>
    public void Dispose() => Disposed = true;
}

/// <summary>A recording <see cref="INemotronEncoder"/> double returning two zero frames per call.</summary>
internal sealed class FakeNemotronEncoder : INemotronEncoder
{
    /// <summary>The frames returned per call.</summary>
    private readonly float[] _output = new float[2 * 4];

    /// <inheritdoc/>
    public int HiddenSize => 4;

    /// <summary>Gets the number of Encode calls.</summary>
    public int EncodeCalls { get; private set; }

    /// <summary>Gets the number of resets.</summary>
    public int ResetCount { get; private set; }

    /// <summary>Gets a value indicating whether the fake was disposed.</summary>
    public bool Disposed { get; private set; }

    /// <summary>Gets the frame count the last call received.</summary>
    public int LastFrameCount { get; private set; }

    /// <summary>Gets the feature length the last call received.</summary>
    public int LastFeatureLength { get; private set; }

    /// <inheritdoc/>
    public ReadOnlySpan<float> Encode(ReadOnlySpan<float> features, int frameCount, out int outputFrames)
    {
        EncodeCalls++;
        LastFrameCount = frameCount;
        LastFeatureLength = features.Length;
        outputFrames = 2;
        return _output;
    }

    /// <inheritdoc/>
    public void Reset() => ResetCount++;

    /// <inheritdoc/>
    public void Dispose() => Disposed = true;
}

/// <summary>
///     An <see cref="INemotronEncoder"/> double that reacts to loudness: each output frame's first
///     element is the largest log-mel value among the chunk rows the frame covers (the pre-encode
///     context rows are ignored), so a network double can tell loud input from silence.
/// </summary>
internal sealed class LoudnessFakeEncoder : INemotronEncoder
{
    /// <summary>The floats per output frame.</summary>
    private const int Hidden = 4;

    /// <summary>The output frames per call.</summary>
    private readonly int _outputFrames;

    /// <summary>The output buffer.</summary>
    private readonly float[] _output;

    /// <summary>Initializes the fake with the number of output frames per chunk (7 for the real model).</summary>
    /// <param name="outputFrames">The output frames per call.</param>
    public LoudnessFakeEncoder(int outputFrames = 7)
    {
        _outputFrames = outputFrames;
        _output = new float[outputFrames * Hidden];
    }

    /// <inheritdoc/>
    public int HiddenSize => Hidden;

    /// <summary>Gets the number of Encode calls.</summary>
    public int EncodeCalls { get; private set; }

    /// <summary>Gets the number of resets.</summary>
    public int ResetCount { get; private set; }

    /// <summary>Gets the largest log-mel value of every chunk row received, in stream order.</summary>
    public List<float> RowMaxima { get; } = [];

    /// <inheritdoc/>
    public ReadOnlySpan<float> Encode(ReadOnlySpan<float> features, int frameCount, out int outputFrames)
    {
        const int mels = NemotronFeatureExtractor.MelBands;
        EncodeCalls++;
        var chunkRows = frameCount - NemotronEngineOptions.PreEncodeFrames;
        var rowMaxima = new float[chunkRows];
        for (var row = 0; row < chunkRows; row++)
        {
            var values = features.Slice((NemotronEngineOptions.PreEncodeFrames + row) * mels, mels);
            var max = float.NegativeInfinity;
            foreach (var value in values)
            {
                max = Math.Max(max, value);
            }

            rowMaxima[row] = max;
            RowMaxima.Add(max);
        }

        Array.Clear(_output);
        for (var k = 0; k < _outputFrames; k++)
        {
            var first = k * chunkRows / _outputFrames;
            var last = (k + 1) * chunkRows / _outputFrames;
            var max = float.NegativeInfinity;
            for (var row = first; row < last; row++)
            {
                max = Math.Max(max, rowMaxima[row]);
            }

            _output[k * Hidden] = max;
        }

        outputFrames = _outputFrames;
        return _output;
    }

    /// <inheritdoc/>
    public void Reset() => ResetCount++;

    /// <inheritdoc/>
    public void Dispose()
    {
        // Nothing to release.
    }
}

/// <summary>
///     An <see cref="IRnntNetwork"/> double that reacts to loudness: on each quiet-to-loud
///     transition of the encoder frames (first element above <see cref="LoudThreshold"/>) it emits
///     one word, cycling through the given word ids, and otherwise predicts blank. Silence
///     features stay far below the threshold, while an all-zero log-mel row is "loud" - so
///     zero-row padding after a pause would produce an extra word and silence padding does not.
/// </summary>
internal sealed class LoudnessFakeNetwork : IRnntNetwork
{
    /// <summary>The log-mel level above which a frame counts as loud.</summary>
    public const float LoudThreshold = -8f;

    /// <summary>The word ids emitted in turn.</summary>
    private readonly int[] _words;

    /// <summary>Whether the previous frame was loud.</summary>
    private bool _wasLoud;

    /// <summary>Whether a word was already emitted for the current frame.</summary>
    private bool _emittedThisFrame;

    /// <summary>The index of the next word to emit.</summary>
    private int _next;

    /// <summary>Initializes the fake.</summary>
    /// <param name="blankId">The blank id.</param>
    /// <param name="words">The word ids emitted in turn.</param>
    public LoudnessFakeNetwork(int blankId, params int[] words)
    {
        BlankId = blankId;
        _words = words;
    }

    /// <inheritdoc/>
    public int BlankId { get; }

    /// <summary>Gets the number of resets.</summary>
    public int ResetCount { get; private set; }

    /// <summary>Gets the number of words emitted.</summary>
    public int Emitted { get; private set; }

    /// <inheritdoc/>
    public void Reset() => ResetCount++;

    /// <inheritdoc/>
    public void Advance(int token)
    {
        // The emitted token needs no state beyond what PredictToken tracks.
    }

    /// <inheritdoc/>
    public int PredictToken(ReadOnlySpan<float> encoderFrame)
    {
        var loud = encoderFrame[0] > LoudThreshold;
        if (loud && !_wasLoud && !_emittedThisFrame)
        {
            _emittedThisFrame = true;
            Emitted++;
            return _words[_next++ % _words.Length];
        }

        // Blank moves the decoder to the next frame
        _wasLoud = loud;
        _emittedThisFrame = false;
        return BlankId;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Nothing to release.
    }
}
