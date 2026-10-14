namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Event data carrying one <see cref="SpeechRecognitionResult"/> produced by an
///     <see cref="IRecognitionSession"/>.
/// </summary>
/// <param name="Result">
///     The recognition result this event reports. Must not be <see langword="null"/>; the constructor throws <see cref="ArgumentNullException"/>
///     otherwise.
/// </param>
/// <remarks>
///     Mirrors the AudioSubsystem's <c>AudioCaptureFrameEventArgs</c> pattern: a small immutable
///     record used as the payload of an <see cref="EventHandler{TEventArgs}"/>, rather than a
///     mutable <see cref="EventArgs"/> subclass. Wrapping the result in a dedicated event type
///     (instead of raising the result directly) leaves room to add further per-event context without a
///     breaking change to the event signature.
/// </remarks>
public sealed record SpeechRecognitionEvent(SpeechRecognitionResult Result)
{
    /// <summary>Backing field for <see cref="Result"/>; guaranteed non-null.</summary>
    private readonly SpeechRecognitionResult _result = Result ?? throw new ArgumentNullException(nameof(Result));

    /// <summary>Gets the recognition result this event reports; never <see langword="null"/>.</summary>
    public SpeechRecognitionResult Result
    {
        get => _result;
        init => _result = value ?? throw new ArgumentNullException(nameof(value));
    }
}
