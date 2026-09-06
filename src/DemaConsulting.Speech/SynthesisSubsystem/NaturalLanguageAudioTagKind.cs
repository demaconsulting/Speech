namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Groups every <see cref="NaturalLanguageAudioTag"/> value into the kind categories
///     architecture.md defines for the closed Natural Language Audio Tag vocabulary.
/// </summary>
/// <remarks>
///     Grouping tags by kind lets Layer 2 per-model rendering (a later phase) and host UI code
///     (for example a demo application's "example tags" hint) reason about a tag's broad
///     category without hard-coding the full tag list. This enum carries no rendering behavior
///     itself; see <see cref="AudioTagCatalog"/> for the tag-to-kind mapping.
/// </remarks>
public enum NaturalLanguageAudioTagKind
{
    /// <summary>
    ///     An emotional delivery tag, for example <see cref="NaturalLanguageAudioTag.Excited"/>
    ///     or <see cref="NaturalLanguageAudioTag.Sad"/>.
    /// </summary>
    Emotion,

    /// <summary>
    ///     A speaking-rate tag, for example <see cref="NaturalLanguageAudioTag.Slow"/> or
    ///     <see cref="NaturalLanguageAudioTag.VeryFast"/>.
    /// </summary>
    Pace,

    /// <summary>
    ///     A delivery-volume tag, for example <see cref="NaturalLanguageAudioTag.Whispers"/> or
    ///     <see cref="NaturalLanguageAudioTag.Loud"/>.
    /// </summary>
    DeliveryVolume,

    /// <summary>
    ///     A non-verbal sound tag, for example <see cref="NaturalLanguageAudioTag.Laughs"/> or
    ///     <see cref="NaturalLanguageAudioTag.Gasp"/>.
    /// </summary>
    NonVerbal,

    /// <summary>
    ///     A tag that always renders as real inserted silence, regardless of model capability:
    ///     <see cref="NaturalLanguageAudioTag.ShortPause"/> or
    ///     <see cref="NaturalLanguageAudioTag.LongPause"/>.
    /// </summary>
    Pause,

    /// <summary>
    ///     The stress/emphasis tag, <see cref="NaturalLanguageAudioTag.Emphasis"/>.
    /// </summary>
    Emphasis,
}
