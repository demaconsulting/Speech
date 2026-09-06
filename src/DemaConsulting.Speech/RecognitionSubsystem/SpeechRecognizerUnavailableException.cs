namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Thrown when an operation is attempted on a speech recognizer that has honestly reported
///     itself as unavailable, or when a recognizer that claimed to be available fails on first
///     use.
/// </summary>
/// <remarks>
///     Per this library's "nothing throws at composition" decision, obtaining and holding an
///     <see cref="ISpeechRecognizer"/> never throws - <see cref="SpeechRecognizerFactory"/>
///     returns <see cref="UnavailableSpeechRecognizer.Instance"/> for every ordinary
///     "cannot recognize on this machine right now" state (model not installed, native runtime
///     absent, no capture device). This exception is reserved for the two genuine error cases:
///     a caller that ignored <c>IsAvailable == false</c> and invoked an operational member
///     anyway, and a recognizer whose underlying capture device failed when actually started.
///     It mirrors <c>AudioDeviceUnavailableException</c> so both subsystems signal misuse the
///     same way.
/// </remarks>
public sealed class SpeechRecognizerUnavailableException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechRecognizerUnavailableException"/>
    ///     class with a default message.
    /// </summary>
    /// <remarks>
    ///     Provided for standard .NET exception-type conformance; callers should prefer
    ///     <see cref="SpeechRecognizerUnavailableException(string)"/> to describe which
    ///     recognizer or operation was affected.
    /// </remarks>
    public SpeechRecognizerUnavailableException()
        : base("The speech recognizer is unavailable.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechRecognizerUnavailableException"/>
    ///     class with a message describing which recognizer or operation was affected.
    /// </summary>
    /// <param name="message">A message describing the unavailable recognizer or attempted operation.</param>
    public SpeechRecognizerUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechRecognizerUnavailableException"/>
    ///     class with a message and an inner exception describing the underlying cause.
    /// </summary>
    /// <param name="message">A message describing the unavailable recognizer or attempted operation.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public SpeechRecognizerUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
