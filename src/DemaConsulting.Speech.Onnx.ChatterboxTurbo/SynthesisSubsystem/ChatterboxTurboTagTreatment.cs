namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;

/// <summary>
///     One row of the per-tag table: how a library tag is rendered.
/// </summary>
/// <param name="Kind">The treatment.</param>
/// <param name="Token">The exact bracketed token text for <see cref="ChatterboxTurboTagKind.Native"/> tags; otherwise null.</param>
/// <param name="PauseMilliseconds">The silence length for <see cref="ChatterboxTurboTagKind.Pause"/> tags; otherwise zero.</param>
/// <param name="EndOfSentenceSuffix">A trailing suffix known to make a sentence-final native tag audible, or null when none is safe.</param>
/// <param name="Mood">The voice-pack mood clip name for <see cref="ChatterboxTurboTagKind.Mood"/> tags (see <c>ReferenceVoiceInfo.KnownMoods</c>); otherwise null.</param>
internal sealed record ChatterboxTurboTagTreatment(
    ChatterboxTurboTagKind Kind,
    string? Token,
    int PauseMilliseconds,
    string? EndOfSentenceSuffix,
    string? Mood = null);
