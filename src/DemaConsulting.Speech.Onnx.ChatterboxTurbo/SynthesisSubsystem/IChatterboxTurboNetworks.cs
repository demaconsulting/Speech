namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;

/// <summary>
///     The mockable seam between the generation algorithm and the four ONNX graphs.
/// </summary>
/// <remarks>
///     Keeping every ONNX Runtime call behind this interface lets the engine's algorithm (prompt assembly,
///     sampling, guards, caching) be verified with hand-written fakes, and the real implementation be verified
///     separately against tiny synthetic graphs, so no multi-gigabyte model is needed in automated tests.
///     Implementations are not required to be thread-safe; the engine serializes calls.
/// </remarks>
internal interface IChatterboxTurboNetworks : IDisposable
{
    /// <summary>Runs the speech encoder over a mono 24 kHz reference clip.</summary>
    /// <param name="referenceAudio">The mono 24 kHz samples.</param>
    /// <returns>The conditioning for that voice.</returns>
    ChatterboxTurboConditioning Encode(float[] referenceAudio);

    /// <summary>Runs the token-embedding graph.</summary>
    /// <param name="tokenIds">The token ids.</param>
    /// <returns>The flattened embeddings, one embedding-size row per token.</returns>
    float[] Embed(long[] tokenIds);

    /// <summary>Starts a new language-model generation with an empty key/value cache.</summary>
    /// <returns>The generation state; the caller disposes it.</returns>
    IChatterboxTurboLanguageModelState BeginGeneration();

    /// <summary>Runs the conditional decoder (vocoder).</summary>
    /// <param name="speechTokens">The full decoder token sequence (prompt, speech, trailing silence).</param>
    /// <param name="conditioning">The voice conditioning supplying speaker embeddings and features.</param>
    /// <returns>The mono 24 kHz waveform.</returns>
    float[] Decode(long[] speechTokens, ChatterboxTurboConditioning conditioning);
}
