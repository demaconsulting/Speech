namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Thrown when <see cref="ISpeechRecognizerEngine.CreateSessionAsync"/> is called while a
///     previously created session's exclusivity lease is still held.
/// </summary>
/// <remarks>
///     Per this library's engine-exclusivity decision, exactly one <see cref="IRecognitionSession"/>
///     may be leased on an <see cref="ISpeechRecognizerEngine"/> at a time, from the moment it is
///     created until its own <see cref="IAsyncDisposable.DisposeAsync"/> completes. A concurrent
///     <see cref="ISpeechRecognizerEngine.CreateSessionAsync"/> call made while that lease is held
///     - including while the prior session is still tearing down - fails fast with this exception
///     rather than queuing or awaiting the prior session's release, so a caller's composition
///     latency never depends on an unrelated session's teardown.
/// </remarks>
public sealed class RecognitionEngineBusyException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="RecognitionEngineBusyException"/> class
    ///     with a default message.
    /// </summary>
    public RecognitionEngineBusyException()
        : base("The recognition engine already has a session leased.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecognitionEngineBusyException"/> class
    ///     with a message describing the busy engine.
    /// </summary>
    /// <param name="message">A message describing why the engine is busy.</param>
    public RecognitionEngineBusyException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecognitionEngineBusyException"/> class
    ///     with a message and an inner exception describing the underlying cause.
    /// </summary>
    /// <param name="message">A message describing why the engine is busy.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public RecognitionEngineBusyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
