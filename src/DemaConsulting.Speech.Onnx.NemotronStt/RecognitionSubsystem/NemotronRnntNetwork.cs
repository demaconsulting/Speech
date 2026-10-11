using Microsoft.ML.OnnxRuntime;

namespace DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;

/// <summary>
///     ONNX Runtime implementation of <see cref="IRnntNetwork"/>: the LSTM prediction network
///     (<c>decoder.onnx</c>) and the joint network (<c>joint.onnx</c>).
/// </summary>
/// <remarks>
///     Both sessions are tiny and called many times per chunk, so each is created with
///     <see cref="CreateSessionOptions"/>: a single intra-op thread and
///     <c>session.intra_op.allow_spinning=0</c>. A multi-threaded or spinning pool makes these
///     microsecond-scale calls slower (thread hand-off and busy-wait cost) and steals CPU from the
///     encoder. They always run on the CPU provider. All tensors are backed by arrays allocated
///     once and reused, so the per-token hot path performs no managed allocation beyond ONNX
///     Runtime's own result collection.
///     <para>
///     Tensor contract: decoder inputs <c>targets</c> [1, 1] int64, <c>h_in</c>/<c>c_in</c>
///     [2, 1, 640] float; outputs <c>decoder_output</c> [1, 640, 1] float and <c>h_out</c>/<c>c_out</c>.
///     Joint inputs <c>encoder_output</c> [1, 1, 1024] and <c>decoder_output</c> [1, 1, 640];
///     output <c>joint_output</c> [1, 1, 1, vocabulary] logits.
///     </para>
/// </remarks>
internal sealed class NemotronRnntNetwork : IRnntNetwork
{
    /// <summary>
    ///     Score penalty applied to the blank token. The model is under-confident on quiet,
    ///     isolated microphone words and otherwise emits blank for them; values above about 4
    ///     cause repeated or spurious tokens.
    /// </summary>
    public const float BlankPenalty = 3.0f;

    /// <summary>The prediction-network hidden size.</summary>
    private const int DecoderHidden = 640;

    /// <summary>The LSTM layer count.</summary>
    private const int DecoderLayers = 2;

    /// <summary>The decoder input names.</summary>
    private static readonly string[] DecoderInputNames = ["targets", "h_in", "c_in"];

    /// <summary>The decoder output names.</summary>
    private static readonly string[] DecoderOutputNames = ["decoder_output", "h_out", "c_out"];

    /// <summary>The joint input names.</summary>
    private static readonly string[] JointInputNames = ["encoder_output", "decoder_output"];

    /// <summary>The joint output names.</summary>
    private static readonly string[] JointOutputNames = ["joint_output"];

    /// <summary>The decoder session.</summary>
    private readonly InferenceSession _decoder;

    /// <summary>The joint session.</summary>
    private readonly InferenceSession _joint;

    /// <summary>Per-call run options.</summary>
    private readonly RunOptions _runOptions = new();

    /// <summary>The decoder's target token input.</summary>
    private readonly long[] _target = new long[1];

    /// <summary>LSTM hidden state.</summary>
    private readonly float[] _hidden = new float[DecoderLayers * DecoderHidden];

    /// <summary>LSTM cell state.</summary>
    private readonly float[] _cell = new float[DecoderLayers * DecoderHidden];

    /// <summary>The latest decoder output (joint input).</summary>
    private readonly float[] _decoderOutput = new float[DecoderHidden];

    /// <summary>The encoder frame staged for the joint network.</summary>
    private readonly float[] _encoderFrame;

    /// <summary>Tensor over <see cref="_target"/>.</summary>
    private readonly OrtValue _targetValue;

    /// <summary>Tensor over <see cref="_hidden"/>.</summary>
    private readonly OrtValue _hiddenValue;

    /// <summary>Tensor over <see cref="_cell"/>.</summary>
    private readonly OrtValue _cellValue;

    /// <summary>Tensor over <see cref="_decoderOutput"/>.</summary>
    private readonly OrtValue _decoderOutputValue;

    /// <summary>Tensor over <see cref="_encoderFrame"/>.</summary>
    private readonly OrtValue _encoderFrameValue;

    /// <summary>Whether <see cref="Dispose"/> has run.</summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NemotronRnntNetwork"/> class.
    /// </summary>
    /// <param name="decoder">The decoder session; ownership transfers to this instance.</param>
    /// <param name="joint">The joint session; ownership transfers to this instance.</param>
    /// <param name="blankId">The RNN-T blank token id.</param>
    /// <param name="encoderHiddenSize">The encoder output frame size.</param>
    public NemotronRnntNetwork(
        InferenceSession decoder,
        InferenceSession joint,
        int blankId,
        int encoderHiddenSize = NemotronEncoder.EncoderHiddenSize)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        ArgumentNullException.ThrowIfNull(joint);
        _decoder = decoder;
        _joint = joint;
        BlankId = blankId;
        _encoderFrame = new float[encoderHiddenSize];

        _targetValue = OrtValue.CreateTensorValueFromMemory(_target, [1, 1]);
        _hiddenValue = OrtValue.CreateTensorValueFromMemory(_hidden, [DecoderLayers, 1, DecoderHidden]);
        _cellValue = OrtValue.CreateTensorValueFromMemory(_cell, [DecoderLayers, 1, DecoderHidden]);
        _decoderOutputValue = OrtValue.CreateTensorValueFromMemory(_decoderOutput, [1, 1, DecoderHidden]);
        _encoderFrameValue = OrtValue.CreateTensorValueFromMemory(_encoderFrame, [1, 1, encoderHiddenSize]);
    }

    /// <inheritdoc/>
    public int BlankId { get; }

    /// <summary>
    ///     Creates the session options used for the decoder and joint sessions: one intra-op
    ///     thread, no spinning (see the type remarks).
    /// </summary>
    /// <returns>New options; the caller disposes them after creating the session.</returns>
    public static SessionOptions CreateSessionOptions()
    {
        var options = new SessionOptions { IntraOpNumThreads = 1 };
        options.AddSessionConfigEntry("session.intra_op.allow_spinning", "0");
        return options;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        Array.Clear(_hidden);
        Array.Clear(_cell);
        Advance(BlankId);
    }

    /// <inheritdoc/>
    public void Advance(int token)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _target[0] = token;
        using var results = _decoder.Run(
            _runOptions,
            DecoderInputNames,
            [_targetValue, _hiddenValue, _cellValue],
            DecoderOutputNames);

        // The prediction network is run for one target token, so its output holds one step
        // (the reference takes the last step); copy it and the new recurrent state back.
        var output = results[0].GetTensorDataAsSpan<float>();
        output[^DecoderHidden..].CopyTo(_decoderOutput);
        results[1].GetTensorDataAsSpan<float>().CopyTo(_hidden);
        results[2].GetTensorDataAsSpan<float>().CopyTo(_cell);
    }

    /// <inheritdoc/>
    public int PredictToken(ReadOnlySpan<float> encoderFrame)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        encoderFrame.CopyTo(_encoderFrame);
        using var results = _joint.Run(
            _runOptions,
            JointInputNames,
            [_encoderFrameValue, _decoderOutputValue],
            JointOutputNames);

        return SelectToken(results[0].GetTensorDataAsSpan<float>(), BlankId);
    }

    /// <summary>
    ///     Selects the arg-max token after subtracting <see cref="BlankPenalty"/> from the blank score.
    /// </summary>
    /// <param name="logits">The joint network logits over the vocabulary.</param>
    /// <param name="blankId">The RNN-T blank token id.</param>
    /// <returns>The index of the highest penalized score (the first on ties).</returns>
    internal static int SelectToken(ReadOnlySpan<float> logits, int blankId)
    {
        var best = 0;
        var bestValue = float.NegativeInfinity;
        for (var i = 0; i < logits.Length; i++)
        {
            var value = i == blankId ? logits[i] - BlankPenalty : logits[i];
            if (value > bestValue)
            {
                bestValue = value;
                best = i;
            }
        }

        return best;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _targetValue.Dispose();
        _hiddenValue.Dispose();
        _cellValue.Dispose();
        _decoderOutputValue.Dispose();
        _encoderFrameValue.Dispose();
        _runOptions.Dispose();
        _decoder.Dispose();
        _joint.Dispose();
    }
}
