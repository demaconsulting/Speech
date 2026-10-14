namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     One recognition result produced while streaming audio through an
///     <see cref="IRecognitionSession"/>.
/// </summary>
/// <param name="Text">
///     The recognized text for the current utterance. Must not be <see langword="null"/>; the constructor throws
///     <see cref="ArgumentNullException"/> otherwise. May be an empty string only when a caller
///     constructs the record directly, since the recognizer suppresses empty results rather than
///     raising them.
/// </param>
/// <param name="IsFinal">
///     <see langword="true"/> when the recognizer has decided the utterance is complete and the
///     text will not change again; <see langword="false"/> when this is a provisional result that
///     a later result for the same utterance may extend or revise.
/// </param>
/// <remarks>
///     This library requires streaming speech-to-text that emits "progressive provisional and
///     final results as audio arrives", so a single flag - not two separate event types - is
///     enough to tell a host whether to replace an in-progress transcript line or commit it.
///     <see cref="Text"/> is always the full text of the current utterance, not a delta, so a
///     host can render it directly without accumulating fragments itself. The type is an
///     immutable record and is therefore safe to share across threads.
/// </remarks>
public sealed record SpeechRecognitionResult(string Text, bool IsFinal)
{
    /// <summary>Backing field for <see cref="Text"/>; guaranteed non-null.</summary>
    private readonly string _text = Text ?? throw new ArgumentNullException(nameof(Text));

    /// <summary>Gets the recognized text for the current utterance; never <see langword="null"/>.</summary>
    public string Text
    {
        get => _text;
        init => _text = value ?? throw new ArgumentNullException(nameof(value));
    }
}
