namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Thrown when an operation is attempted on a speech synthesizer that has honestly reported
///     itself as unavailable, or when a synthesizer that claimed to be available fails on first
///     use.
/// </summary>
/// <remarks>
///     Per this library's "nothing throws at composition" decision, loading an
///     <see cref="ISpeechSynthesizerEngine"/> never throws for an ordinary machine state -
///     <see cref="SpeechSynthesizerFactory"/> returns
///     <see cref="UnavailableSpeechSynthesizerEngine.Instance"/> for every ordinary "cannot
///     synthesize on this machine right now" state (model not installed, native runtime absent).
///     <see cref="UnavailableSpeechSynthesizerEngine"/> and <see cref="UnavailableSynthesisSession"/>
///     report themselves honestly through <c>IsAvailable == false</c> rather than throwing this
///     exception on an operational call. This exception is reserved for a
///     <see cref="SherpaOnnxSynthesisSession"/> whose underlying backend or playback device failed
///     when actually used, and for a caller who continued to use a session after it reported a
///     terminal state. It mirrors <see cref="RecognitionSubsystem.SpeechRecognizerUnavailableException"/>
///     so both subsystems signal misuse the same way.
/// </remarks>
public sealed class SpeechSynthesizerUnavailableException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechSynthesizerUnavailableException"/>
    ///     class with a default message.
    /// </summary>
    /// <remarks>
    ///     Provided for standard .NET exception-type conformance; callers should prefer
    ///     <see cref="SpeechSynthesizerUnavailableException(string)"/> to describe which
    ///     synthesizer or operation was affected.
    /// </remarks>
    public SpeechSynthesizerUnavailableException()
        : base("The speech synthesizer is unavailable.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechSynthesizerUnavailableException"/>
    ///     class with a message describing which synthesizer or operation was affected.
    /// </summary>
    /// <param name="message">A message describing the unavailable synthesizer or attempted operation.</param>
    public SpeechSynthesizerUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechSynthesizerUnavailableException"/>
    ///     class with a message and an inner exception describing the underlying cause.
    /// </summary>
    /// <param name="message">A message describing the unavailable synthesizer or attempted operation.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public SpeechSynthesizerUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
