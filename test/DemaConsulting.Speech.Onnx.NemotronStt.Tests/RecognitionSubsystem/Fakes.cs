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
