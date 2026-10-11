using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;

/// <summary>
///     The detailed outcome of one generation, exposing guard diagnostics that <see cref="ISynthesisBackend"/> hides.
/// </summary>
/// <param name="Audio">The synthesized audio.</param>
/// <param name="HitTokenLimit">True when generation stopped on a guard bound instead of the model's stop token.</param>
/// <param name="SpeechTokenCount">The number of speech tokens that were decoded.</param>
internal sealed record ChatterboxTurboGenerationResult(EngineAudio Audio, bool HitTokenLimit, int SpeechTokenCount);
