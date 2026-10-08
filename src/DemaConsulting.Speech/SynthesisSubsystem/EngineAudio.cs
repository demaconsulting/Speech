namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     The plain-data audio result of one <see cref="ISynthesisBackend.Generate"/> call.
/// </summary>
/// <param name="Samples">
///     The synthesized mono samples, normalized to <c>[-1.0, 1.0]</c>. Never null; may be empty.
/// </param>
/// <param name="SampleRate">The rate, in Hz, at which <paramref name="Samples"/> was produced.</param>
/// <remarks>
///     Kept distinct from the public <see cref="SynthesizedSpeech"/> record, which additionally
///     carries pause metadata the engine itself has no opinion on - this is the engine seam's raw
///     output shape only. Public, mirroring <see cref="ISynthesisBackend"/>'s public visibility,
///     since a third-party <see cref="ISynthesisBackend"/> implementation must be able to return
///     one.
/// </remarks>
public sealed record EngineAudio(float[] Samples, int SampleRate);
