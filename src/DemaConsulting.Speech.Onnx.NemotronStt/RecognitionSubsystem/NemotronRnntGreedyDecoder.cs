namespace DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;

/// <summary>
///     RNN-T greedy decoder: walks the encoder frames of one chunk and, for each, repeatedly asks
///     the joint network for the best token until it predicts blank or a per-frame symbol limit
///     is reached.
/// </summary>
/// <remarks>
///     Mirrors the reference spike: at most <see cref="MaxSymbolsPerFrame"/> (10) tokens are
///     emitted per encoder frame; each emitted token advances the prediction network; blank
///     moves to the next frame without advancing it. The decoder state persists across calls so
///     decoding continues seamlessly across chunk boundaries; <see cref="Reset"/> restores the
///     start-of-stream state.
/// </remarks>
internal sealed class NemotronRnntGreedyDecoder : IDisposable
{
    /// <summary>The maximum number of tokens emitted for a single encoder frame.</summary>
    public const int MaxSymbolsPerFrame = 10;

    /// <summary>The prediction/joint networks.</summary>
    private readonly IRnntNetwork _network;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NemotronRnntGreedyDecoder"/> class.
    /// </summary>
    /// <param name="network">The networks to decode with; ownership transfers to this instance.</param>
    public NemotronRnntGreedyDecoder(IRnntNetwork network)
    {
        ArgumentNullException.ThrowIfNull(network);
        _network = network;
        _network.Reset();
    }

    /// <summary>Restores the start-of-stream decoder state.</summary>
    public void Reset() => _network.Reset();

    /// <summary>
    ///     Decodes <paramref name="frameCount"/> encoder frames, appending emitted token ids to
    ///     <paramref name="tokens"/>.
    /// </summary>
    /// <param name="encoderOutput">The frames, <paramref name="hiddenSize"/> floats each.</param>
    /// <param name="frameCount">The number of frames.</param>
    /// <param name="hiddenSize">The floats per frame.</param>
    /// <param name="tokens">Receives the emitted (non-blank) token ids.</param>
    /// <returns>The number of tokens emitted by this call.</returns>
    public int Decode(ReadOnlySpan<float> encoderOutput, int frameCount, int hiddenSize, List<int> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        var emitted = 0;
        for (var t = 0; t < frameCount; t++)
        {
            var frame = encoderOutput.Slice(t * hiddenSize, hiddenSize);
            for (var symbol = 0; symbol < MaxSymbolsPerFrame; symbol++)
            {
                var token = _network.PredictToken(frame);
                if (token == _network.BlankId)
                {
                    break;
                }

                tokens.Add(token);
                emitted++;
                _network.Advance(token);
            }
        }

        return emitted;
    }

    /// <inheritdoc/>
    public void Dispose() => _network.Dispose();
}
