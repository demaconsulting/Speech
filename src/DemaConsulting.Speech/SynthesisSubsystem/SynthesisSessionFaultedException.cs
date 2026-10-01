namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Thrown when an operation is attempted on, or surfaced from, an <see cref="ISynthesisSession"/>
///     that has transitioned to <see cref="SynthesisSessionState.Faulted"/>.
/// </summary>
/// <remarks>
///     A session faults when an in-flight <see cref="ISynthesisSession.SpeakAsync"/> or
///     <see cref="ISynthesisSession.SynthesizeAsync"/> operation fails for a reason other than its
///     own requested cancellation - including a native call that did not stop cooperatively within
///     the dedicated worker's abandon timeout and was detached without ever being requested to stop
///     via <see cref="ISynthesisSession.StopAsync"/> or <see cref="IAsyncDisposable.DisposeAsync"/>.
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
