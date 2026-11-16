using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Text.En.AnalysisSubsystem;

/// <summary>
///     The annotation kinds the English text analysis defines, and typed readers that give a
///     synthesis backend those annotations back without hand-parsing <c>Kind</c> and <c>Value</c>
///     strings.
/// </summary>
/// <remarks>
///     The core library carries <see cref="SegmentAnnotation"/>s opaquely, so the vocabulary has
///     to live with the package that produces it. All six kinds the package family defines are
///     declared here so a backend can reference stable names from the first release, but only
///     <see cref="PartOfSpeech"/> is produced by this release; the others are reserved for later
///     releases and are never emitted today. A backend must ignore any kind it does not
///     recognize. The type is static and therefore thread-safe.
/// </remarks>
public static class TextEnAnnotations
{
    /// <summary>
    ///     Reserved, not yet emitted: the <c>Value</c> is the spoken replacement text for the
    ///     annotated source span (a normalization, for example a number spoken as words).
    /// </summary>
    public const string Normalized = "en.norm";

    /// <summary>
    ///     The <c>Value</c> is the Universal Dependencies part-of-speech name of the annotated
    ///     word token, for example <c>"NOUN"</c> or <c>"VERB"</c>. Emitted by this release, one
    ///     annotation per word token; see <see cref="TryReadPartOfSpeech"/>.
    /// </summary>
    /// <remarks>
    ///     The annotation covers exactly the characters of one tokenizer token. Tokens the tagger
    ///     labels <c>PUNCT</c> (punctuation) or <c>SYM</c> (symbols) carry no annotation, because
    ///     they are not words and a backend gains nothing from tagging them. The closed set of
    ///     values is the 17 Universal Dependencies tags: <c>ADJ</c>, <c>ADP</c>, <c>ADV</c>,
    ///     <c>AUX</c>, <c>CCONJ</c>, <c>DET</c>, <c>INTJ</c>, <c>NOUN</c>, <c>NUM</c>,
    ///     <c>PART</c>, <c>PRON</c>, <c>PROPN</c>, <c>PUNCT</c>, <c>SCONJ</c>, <c>SYM</c>,
    ///     <c>VERB</c> and <c>X</c>; this release never produces <c>PUNCT</c> or <c>SYM</c>.
    /// </remarks>
    public const string PartOfSpeech = "en.upos";

    /// <summary>
    ///     Reserved, not yet emitted: the <c>Value</c> is a self-owned pronunciation-reading id
    ///     that selects among the readings of a heteronym such as <c>record</c>.
    /// </summary>
    public const string Reading = "en.reading";

    /// <summary>
    ///     Reserved, not yet emitted: the <c>Value</c> is the invariant-culture confidence of the
    ///     reading annotation that covers the same span.
    /// </summary>
    public const string ReadingConfidence = "en.reading-confidence";

    /// <summary>
    ///     Reserved, not yet emitted: the <c>Value</c> is a host-supplied pronunciation override
    ///     for the annotated word.
    /// </summary>
    public const string Pronunciation = "en.pron";

    /// <summary>
    ///     Reserved, not yet emitted: the <c>Value</c> is the letters to speak one by one for an
    ///     initialism.
    /// </summary>
    public const string SpellOut = "en.spell";

    /// <summary>
    ///     The closed set of Universal Dependencies tag names a <see cref="PartOfSpeech"/>
    ///     annotation value may take.
    /// </summary>
    private static readonly HashSet<string> UniversalTags = new(StringComparer.Ordinal)
    {
        "ADJ", "ADP", "ADV", "AUX", "CCONJ", "DET", "INTJ", "NOUN", "NUM",
        "PART", "PRON", "PROPN", "PUNCT", "SCONJ", "SYM", "VERB", "X",
    };

    /// <summary>
    ///     Reads the part of speech out of a <see cref="PartOfSpeech"/> annotation.
    /// </summary>
    /// <param name="annotation">The annotation to read. Must not be null.</param>
    /// <param name="partOfSpeech">
    ///     When this method returns <see langword="true"/>, the Universal Dependencies tag name
    ///     (for example <c>"NOUN"</c>); otherwise an empty string.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="annotation"/> has kind
    ///     <see cref="PartOfSpeech"/> and a value from the closed tag set; otherwise
    ///     <see langword="false"/> (an annotation of another kind is simply not a part-of-speech
    ///     annotation, which is not an error).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is null.</exception>
    /// <remarks>Stateless and thread-safe.</remarks>
    public static bool TryReadPartOfSpeech(SegmentAnnotation annotation, out string partOfSpeech)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        // A backend calls this on every annotation of a segment, most of which may belong to
        // other kinds; a wrong kind or an unknown value is an ordinary "no", never an exception.
        if (string.Equals(annotation.Kind, PartOfSpeech, StringComparison.Ordinal) && UniversalTags.Contains(annotation.Value))
        {
            partOfSpeech = annotation.Value;
            return true;
        }

        partOfSpeech = string.Empty;
        return false;
    }
}
