namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Event data carrying one <see cref="SpeechRecognitionResult"/> raised by an
///     <see cref="ISpeechRecognizer"/>.
/// </summary>
/// <param name="Result">
///     The recognition result this event reports. Never <see langword="null"/>.
/// </param>
/// <remarks>
///     Mirrors the AudioSubsystem's <c>AudioCaptureFrameEventArgs</c> pattern: a small immutable
///     record used as the payload of an <see cref="EventHandler{TEventArgs}"/>, rather than a
///     mutable <see cref="EventArgs"/> subclass. Wrapping the result in a dedicated event type
///     (instead of raising the result directly) leaves room to add further per-event context in a
///     later phase without a breaking change to the event signature.
/// </remarks>
public sealed record SpeechRecognitionEvent(SpeechRecognitionResult Result);
