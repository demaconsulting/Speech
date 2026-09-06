namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Thrown when an explicit, user-invoked <see cref="SpeechModelStore"/> operation (currently,
///     only uninstall) cannot complete.
/// </summary>
/// <remarks>
///     Per this library's "nothing throws at composition" decision, this exception is never
///     thrown by store construction, install-state queries, or catalog enumeration - those
///     operations degrade to an honest state (e.g. "not installed") instead. It is reserved for
///     explicit, first-use-style calls a host makes intentionally, most notably uninstalling a
///     model whose <c>current/</c> directory cannot be removed (typically because another
///     process still holds an open file handle into it, most commonly seen on Windows).
/// </remarks>
public sealed class SpeechModelStoreException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechModelStoreException"/> class with a
    ///     default message.
    /// </summary>
    /// <remarks>
    ///     Provided for standard .NET exception-type conformance; callers should prefer
    ///     <see cref="SpeechModelStoreException(string)"/> to describe which model or operation
    ///     was affected.
    /// </remarks>
    public SpeechModelStoreException()
        : base("The model store operation could not complete.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechModelStoreException"/> class with a
    ///     message describing which model or operation was affected.
    /// </summary>
    /// <param name="message">A message describing the affected model or operation.</param>
    public SpeechModelStoreException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechModelStoreException"/> class with a
    ///     message and an inner exception describing the underlying cause.
    /// </summary>
    /// <param name="message">A message describing the affected model or operation.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public SpeechModelStoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
