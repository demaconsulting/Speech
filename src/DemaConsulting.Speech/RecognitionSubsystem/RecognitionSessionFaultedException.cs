namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Thrown from <see cref="IRecognitionSession.GetResultsAsync"/>'s enumerator when the
///     session transitions to <see cref="RecognitionSessionState.Faulted"/> while being
///     enumerated.
/// </summary>
/// <remarks>
///     A session faults when an unrecoverable error occurs while
///     <see cref="RecognitionSessionState.Starting"/>, <see cref="RecognitionSessionState.Running"/>,
///     or <see cref="RecognitionSessionState.Stopping"/> - for example the bound capture device
///     being lost mid-session. The fault is surfaced to a consumer currently enumerating
///     <see cref="IRecognitionSession.GetResultsAsync"/> as this exception, with the underlying
///     cause available as <see cref="Exception.InnerException"/>.
/// </remarks>
public sealed class RecognitionSessionFaultedException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="RecognitionSessionFaultedException"/>
    ///     class with a default message.
    /// </summary>
    public RecognitionSessionFaultedException()
        : base("The recognition session has faulted.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecognitionSessionFaultedException"/>
    ///     class with a message describing the fault.
    /// </summary>
    /// <param name="message">A message describing the fault.</param>
    public RecognitionSessionFaultedException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecognitionSessionFaultedException"/>
    ///     class with a message and the inner exception that caused the session to fault.
    /// </summary>
    /// <param name="message">A message describing the fault.</param>
    /// <param name="innerException">The exception that caused the session to fault.</param>
    public RecognitionSessionFaultedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
