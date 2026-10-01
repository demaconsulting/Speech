namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Event arguments describing an <see cref="ISynthesisSession"/> state transition.
/// </summary>
/// <param name="Previous">The state the session transitioned from.</param>
/// <param name="Current">The state the session transitioned to.</param>
/// <remarks>
///     Raised by <see cref="ISynthesisSession.StateChanged"/>. This record is deliberately
///     declared separately from <c>RecognitionSubsystem.SessionStateChangedEventArgs</c> (which
///     carries <c>RecognitionSessionState</c> instead) rather than shared, matching this
///     library's existing "no cross-subsystem public type" boundary (for example
///     <c>SpeechRecognizerUnavailableException</c>/<see cref="SpeechSynthesizerUnavailableException"/>
///     are similarly duplicated per subsystem).
/// </remarks>
public sealed record SessionStateChangedEventArgs(SynthesisSessionState Previous, SynthesisSessionState Current);
