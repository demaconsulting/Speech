namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Thrown when an operation is attempted on, or surfaced from, an <see cref="ISynthesisSession"/>
///     that has transitioned to <see cref="SynthesisSessionState.Faulted"/>.
/// </summary>
/// <remarks>
///     A session faults when an in-flight <see cref="ISynthesisSession.SpeakAsync"/> or
///     <see cref="ISynthesisSession.SynthesizeAsync"/> operation fails for a reason other than its
///     own promptly-honored cancellation - including a native call that did not stop cooperatively
///     within the dedicated worker's abandon timeout and was detached, whether or not it was ever
///     requested to stop via <see cref="ISynthesisSession.StopAsync"/> or
///     <see cref="IAsyncDisposable.DisposeAsync"/>: either way, the native call may still be
///     running against the shared backend, so the session cannot safely be treated as a clean,
///     reusable stop.
///     Once faulted, the session is terminal: every subsequent
///     <see cref="ISynthesisSession.SpeakAsync"/>/<see cref="ISynthesisSession.SynthesizeAsync"/>
///     call throws this exception (carrying the original fault as its inner exception) rather than
///     attempting to run; callers must dispose the faulted session and create a new one.
/// </remarks>
public sealed class SynthesisSessionFaultedException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SynthesisSessionFaultedException"/> class
    ///     with a default message.
    /// </summary>
    /// <remarks>
    ///     Provided for standard .NET exception-type conformance; callers should prefer
    ///     <see cref="SynthesisSessionFaultedException(string,Exception)"/> to carry the original
    ///     fault cause.
    /// </remarks>
    public SynthesisSessionFaultedException()
        : base("The synthesis session has faulted and can no longer perform operations.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SynthesisSessionFaultedException"/> class
    ///     with a message describing the faulted session.
    /// </summary>
    /// <param name="message">A message describing the faulted session.</param>
    public SynthesisSessionFaultedException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SynthesisSessionFaultedException"/> class
    ///     with a message and the original exception that caused the session to fault.
    /// </summary>
    /// <param name="message">A message describing the faulted session.</param>
    /// <param name="innerException">The exception that caused the session to fault.</param>
    public SynthesisSessionFaultedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
