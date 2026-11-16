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
///     <see cref="PartOfSpeech"/> and <see cref="Normalized"/> are produced by this release; the
///     others are reserved for later releases and are never emitted today. A backend must ignore
///     any kind it does not recognize. The type is static and therefore thread-safe.
/// </remarks>
public static class TextEnAnnotations
{
    /// <summary>
    ///     The <c>Value</c> is the exact spoken replacement for the annotated source span (for
    ///     example <c>Doctor</c> for <c>Dr.</c>); an empty <c>Value</c> means the span is spoken
    ///     as nothing. Emitted by this release; see <see cref="TryReadNormalized"/>.
    /// </summary>
    /// <remarks>
    ///     The analyzed text is never rewritten: a span is changed only where an annotation says
    ///     so, and unchanged text is never annotated. The <c>en.norm</c> annotations of one text
    ///     never overlap each other.
    ///     To obtain the spoken text of a segment, sort its <c>en.norm</c> annotations by
    ///     <c>Start</c>, then emit the text between them unchanged and each annotation's
    ///     <c>Value</c> in place of its span. A <c>Value</c> already carries any single space it
    ///     needs next to a touching word, so nothing is inserted between the pieces. Annotations
    ///     of other kinds (such as <see cref="PartOfSpeech"/>) may overlap an <c>en.norm</c>
    ///     span and are independent of it. A span contains no whitespace, comma, semicolon or
    ///     colon, so it survives the core library's chunking.
    /// </remarks>
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

    /// <summary>
    ///     Reads the spoken replacement out of a <see cref="Normalized"/> annotation.
    /// </summary>
    /// <param name="annotation">The annotation to read. Must not be null.</param>
    /// <param name="spokenText">
    ///     When this method returns <see langword="true"/>, the exact replacement for the
    ///     annotated span, possibly empty (the span is spoken as nothing); otherwise an empty
    ///     string.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="annotation"/> has kind
    ///     <see cref="Normalized"/> (any value, including an empty one); otherwise
    ///     <see langword="false"/> (an annotation of another kind is simply not a normalization,
    ///     which is not an error).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is null.</exception>
    /// <remarks>Stateless and thread-safe.</remarks>
    public static bool TryReadNormalized(SegmentAnnotation annotation, out string spokenText)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        // An empty value is meaningful here (speak nothing), so unlike the part-of-speech reader
        // there is no value check: the kind alone decides.
        if (string.Equals(annotation.Kind, Normalized, StringComparison.Ordinal))
        {
            spokenText = annotation.Value;
            return true;
        }

        spokenText = string.Empty;
        return false;
    }
}
