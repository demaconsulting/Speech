namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Event data carrying an <see cref="IRecognitionSession"/>'s previous and current
///     <see cref="RecognitionSessionState"/> around one state transition.
/// </summary>
/// <param name="Previous">The state the session transitioned from.</param>
/// <param name="Current">The state the session transitioned to.</param>
/// <remarks>
///     Raised by <see cref="IRecognitionSession.StateChanged"/>. See that event's remarks for the
///     threading and fault-isolation guarantees that apply to every raise of this event.
/// </remarks>
public sealed record SessionStateChangedEventArgs(RecognitionSessionState Previous, RecognitionSessionState Current);
