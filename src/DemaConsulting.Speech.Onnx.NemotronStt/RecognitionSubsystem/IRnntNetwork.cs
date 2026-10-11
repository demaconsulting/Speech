namespace DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;

/// <summary>
///     Seam over the RNN-T prediction network (decoder) and joint network: holds the decoder's
///     recurrent state and the latest decoder output, and scores encoder frames against it.
/// </summary>
/// <remarks>
///     The seam lets <see cref="NemotronRnntGreedyDecoder"/>'s greedy loop be unit tested with a
///     scripted managed fake; <see cref="NemotronRnntNetwork"/> is the ONNX Runtime implementation.
/// </remarks>
internal interface IRnntNetwork : IDisposable
{
    /// <summary>Gets the RNN-T blank token id.</summary>
    int BlankId { get; }

    /// <summary>Zeroes the recurrent state and advances the decoder with the blank token, as at stream start.</summary>
    void Reset();

    /// <summary>Advances the decoder by one emitted token.</summary>
    /// <param name="token">The token id just emitted.</param>
    void Advance(int token);

    /// <summary>
    ///     Runs the joint network on <paramref name="encoderFrame"/> and the current decoder output.
    /// </summary>
    /// <param name="encoderFrame">One encoder output frame.</param>
    /// <returns>The arg-max token id (the blank id when no token is emitted).</returns>
    int PredictToken(ReadOnlySpan<float> encoderFrame);
}
