namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     The closed, fixed vocabulary of Natural Language Audio Tags recognized by
///     <see cref="AudioTagParser"/>, one value per canonical tag with every documented synonym
///     collapsed onto it (see <see cref="AudioTagCatalog"/> for the alias table).
/// </summary>
/// <remarks>
///     Per this library's "Natural Language Audio Tags replace SSML" decision, this
///     vocabulary is fixed for now and expands only through a new library release; it is not a
///     host-configurable or model-configurable set. Each value belongs to exactly one
///     <see cref="NaturalLanguageAudioTagKind"/>, reported alongside it by
///     <see cref="AudioTagCatalog"/>.
/// </remarks>
public enum NaturalLanguageAudioTag
{
    /// <summary>Excited/enthusiastic delivery (<c>[excited]</c>, <c>[excitedly]</c>).</summary>
    Excited,

    /// <summary>Serious, measured delivery (<c>[serious]</c>).</summary>
    Serious,

    /// <summary>Sarcastic delivery (<c>[sarcastic]</c>).</summary>
    Sarcastic,

    /// <summary>Panicked/shocked delivery (<c>[panicked]</c>, <c>[shocked]</c>).</summary>
    Panicked,

    /// <summary>Bored/tired delivery (<c>[bored]</c>, <c>[tired]</c>).</summary>
    Bored,

    /// <summary>Sad/crying delivery (<c>[sad]</c>, <c>[crying]</c>).</summary>
    Sad,

    /// <summary>Slow pace (<c>[slow]</c>).</summary>
    Slow,

    /// <summary>Very slow pace (<c>[very slow]</c>).</summary>
    VerySlow,

    /// <summary>Fast pace (<c>[fast]</c>).</summary>
    Fast,

    /// <summary>Very fast pace (<c>[very fast]</c>).</summary>
    VeryFast,

    /// <summary>
    ///     A short pause. Always renders as real inserted silence regardless of model
    ///     capability (<c>[short pause]</c>).
    /// </summary>
    ShortPause,

    /// <summary>
    ///     A long pause. Always renders as real inserted silence regardless of model
    ///     capability (<c>[long pause]</c>).
    /// </summary>
    LongPause,

    /// <summary>Stressed/emphasized delivery of the adjacent words (<c>[emphasis]</c>).</summary>
    Emphasis,

    /// <summary>Whispered delivery (<c>[whispers]</c>, <c>[whispering]</c>).</summary>
    Whispers,

    /// <summary>Soft/quiet delivery (<c>[soft]</c>).</summary>
    Soft,

    /// <summary>
    ///     Loud/shouted delivery (<c>[loud]</c>, <c>[shouting]</c>, <c>[screams]</c>).
    /// </summary>
    Loud,

    /// <summary>Breathy delivery (<c>[breathy]</c>).</summary>
    Breathy,

    /// <summary>Laughter (<c>[laughs]</c>, <c>[laughing]</c>, <c>[giggles]</c>).</summary>
    Laughs,

    /// <summary>Sighing (<c>[sighs]</c>, <c>[sigh]</c>).</summary>
    Sighs,

    /// <summary>Gasping/inhaling (<c>[gasp]</c>, <c>[inhale]</c>).</summary>
    Gasp,

    /// <summary>Clearing the throat/coughing (<c>[clears throat]</c>, <c>[cough]</c>).</summary>
    ClearsThroat,

    /// <summary>Snorting (<c>[snorts]</c>).</summary>
    Snorts,
}
