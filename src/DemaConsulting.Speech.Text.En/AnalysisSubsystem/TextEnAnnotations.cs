using System.Globalization;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.NormalizationSubsystem;

namespace DemaConsulting.Speech.Text.En.AnalysisSubsystem;

/// <summary>
///     The annotation kinds the English text analysis defines, and typed readers that give a
///     synthesis backend those annotations back without hand-parsing <c>Kind</c> and <c>Value</c>
///     strings.
/// </summary>
/// <remarks>
///     The core library carries <see cref="SegmentAnnotation"/>s opaquely, so the vocabulary has
///     to live with the package that produces it. All six kinds the package family defines are
///     declared here so a backend can reference stable names from the first release; this release
///     produces all of them: <see cref="PartOfSpeech"/>, <see cref="Normalized"/>,
///     <see cref="SpellOut"/>, <see cref="Pronunciation"/>, and the pair <see cref="Reading"/> and
///     <see cref="ReadingConfidence"/>, the last two only when a heteronym model is available or a
///     <see cref="OverrideAction.Reading"/> override names the word (this release embeds no model). A
///     backend must ignore any kind it does not recognize. The type is static and therefore thread-safe.
///     <para>
///     <b>Replacement family.</b> <see cref="Normalized"/> and <see cref="SpellOut"/> are the two
///     annotations that replace the spoken text of a span. Together they form one sequence of
///     ascending, non-overlapping spans. To obtain the spoken text of a segment, read each with
///     <see cref="TryReadReplacement"/>, sort by <c>Start</c>, emit the text between them unchanged
///     and each value in place of its span; when <c>isSpelled</c> is true, speak each capital
///     letter of the value by its letter name, never as a word (so a lone <c>A</c> or <c>I</c> is
///     never the article or the pronoun). Only <see cref="SpellOut"/> (that is, a true
///     <c>isSpelled</c>) means spell out: the value of every <see cref="Normalized"/> annotation
///     is spoken as words, whatever letters it contains, so a backend must never infer
///     spelling from the shape or content of a <see cref="Normalized"/> value (a host's
///     <c>Say("foo", "I")</c> override is the word <c>I</c>). A meridiem written glued to its
///     number (<c>5pm</c>, <c>5:30pm</c>) is therefore two adjacent spans inside the token: the
///     number or time as <see cref="Normalized"/> and the meridiem letters as
///     <see cref="SpellOut"/>. <see cref="Pronunciation"/> annotations replace nothing:
///     a backend that supports the scheme uses the phonemes for the word the annotation covers
///     and a backend that does not speaks the word as usual. Pronunciation annotations never
///     overlap each other or a replacement.
///     </para>
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
    ///     never overlap each other (nor an <see cref="SpellOut"/> annotation).
    ///     To obtain the spoken text of a segment, sort its replacement annotations (<c>en.norm</c>
    ///     and <c>en.spell</c>, see <see cref="TryReadReplacement"/>) by
    ///     <c>Start</c>, then emit the text between them unchanged and each annotation's
    ///     <c>Value</c> in place of its span. A <c>Value</c> already carries any single space it
    ///     needs next to a touching word, so nothing is inserted between the pieces. The <c>Value</c>
    ///     is always spoken as words: <c>en.norm</c> carries no spell-out promise, even for a value
    ///     that is a single capital letter (only <see cref="SpellOut"/> means spell out). Annotations
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
    ///     The <c>Value</c> is a self-owned pronunciation-reading id that selects among the readings of a
    ///     heteronym such as <c>record</c>, for example <c>record.noun</c> or <c>record.verb</c>.
    ///     Emitted together with <see cref="ReadingConfidence"/> on the same span; see
    ///     <see cref="TryReadReading"/>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     The annotation covers exactly one whole tokenizer token, an ASCII-letter form of the reading
    ///     inventory (<see cref="ReadingInventory"/>), so its span starts and ends on a letter, never
    ///     splits a surrogate pair and contains no whitespace, comma, semicolon or colon; it survives the
    ///     core library's chunking. It is emitted for a word the classifier decided (which needs a trained
    ///     heteronym model, none of which this release embeds), for a word whose form has only one
    ///     candidate reading (confidence <c>1.000</c>, again only when the classifier is available), and
    ///     for a word that a <see cref="OverrideAction.Reading"/> override names (confidence
    ///     <c>1.000</c>, no model needed).
    ///     </para>
    ///     <para>
    ///     <b>Coexistence.</b> It coexists on the same span with <see cref="PartOfSpeech"/>, and it is
    ///     never emitted for a token that overlaps an <see cref="Normalized"/>, <see cref="SpellOut"/>
    ///     or <see cref="Pronunciation"/> span or that carries any other override, so a backend never has
    ///     to reconcile a reading with a replacement. At most one reading annotation is emitted per
    ///     token. A backend that does not understand readings ignores it and speaks the word as usual.
    ///     </para>
    /// </remarks>
    public const string Reading = "en.reading";

    /// <summary>
    ///     The <c>Value</c> is the invariant-culture confidence, from <c>0.000</c> to <c>1.000</c>, of the
    ///     <see cref="Reading"/> annotation that covers the same span; see
    ///     <see cref="TryReadReadingConfidence"/>.
    /// </summary>
    /// <remarks>
    ///     Always three decimals (<c>0.873</c>, <c>1.000</c>) written with a period whatever the current
    ///     culture. A backend may ignore it. A confidence below the analyzer's
    ///     <see cref="EnglishTextAnalyzerOptions.LowConfidenceThreshold"/> is still emitted.
    /// </remarks>
    public const string ReadingConfidence = "en.reading-confidence";

    /// <summary>
    ///     The <c>Value</c> is a host-supplied pronunciation for the annotated word, always written
    ///     <c>scheme:phonemes</c> with a lower-case scheme from the closed list <c>ipa</c> and
    ///     <c>arpabet</c> (see <see cref="PronunciationScheme"/>). Emitted by this release when a
    ///     <see cref="PronunciationOverrides"/> entry with the pronunciation action matches; see
    ///     <see cref="TryReadPronunciation"/>.
    /// </summary>
    /// <remarks>
    ///     The span is exactly one word token and the word is not replaced. A backend that cannot
    ///     use the scheme or the phonemes speaks the word as it would without the annotation.
    ///     The value never contains a tag placeholder; the library checks the syntax of the
    ///     phonemes but not whether they belong to a particular phoneme inventory.
    /// </remarks>
    public const string Pronunciation = "en.pron";

    /// <summary>
    ///     The <c>Value</c> is the letters to speak one by one, by letter name: single capital
    ///     ASCII letters separated by single spaces (<c>F B I</c>), with at most one padding space on
    ///     either side where the span touches a letter. Emitted by this release for initialisms,
    ///     dotted initialisms, single-letter initials, meridiem letters and the spell-out
    ///     override; see <see cref="TryReadSpellOut"/>.
    /// </summary>
    /// <remarks>
    ///     It replaces the span exactly like <see cref="Normalized"/> (the two form one sequence of
    ///     non-overlapping replacement spans, see <see cref="TryReadReplacement"/>), with the
    ///     added promise that every letter is a letter name, never a word: a lone <c>A</c> is the
    ///     letter, not the article, and a lone <c>I</c> is the letter, not the pronoun.
    /// </remarks>
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

    /// <summary>
    ///     Reads the letters out of a <see cref="SpellOut"/> annotation.
    /// </summary>
    /// <param name="annotation">The annotation to read. Must not be null.</param>
    /// <param name="spokenLetters">
    ///     When this method returns <see langword="true"/>, the value exactly as stored (single
    ///     capital letters separated by single spaces, possibly with one padding space on either
    ///     side); otherwise an empty string.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="annotation"/> has kind
    ///     <see cref="SpellOut"/> and a well-formed letter sequence; otherwise
    ///     <see langword="false"/> (a wrong kind or malformed value is not an error).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is null.</exception>
    /// <remarks>Stateless and thread-safe.</remarks>
    public static bool TryReadSpellOut(SegmentAnnotation annotation, out string spokenLetters)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        if (string.Equals(annotation.Kind, SpellOut, StringComparison.Ordinal) && SpelledGrammar.IsLetterSequence(annotation.Value))
        {
            spokenLetters = annotation.Value;
            return true;
        }

        spokenLetters = string.Empty;
        return false;
    }

    /// <summary>
    ///     Reads the scheme and phonemes out of a <see cref="Pronunciation"/> annotation.
    /// </summary>
    /// <param name="annotation">The annotation to read. Must not be null.</param>
    /// <param name="scheme">The phoneme notation when this method returns <see langword="true"/>; otherwise the default value.</param>
    /// <param name="phonemes">
    ///     When this method returns <see langword="true"/>, the phonemes after the first colon of
    ///     the value, in the syntax of <paramref name="scheme"/>; otherwise an empty string.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="annotation"/> has kind
    ///     <see cref="Pronunciation"/> and a value <c>scheme:phonemes</c> with a known scheme and
    ///     phonemes that satisfy that scheme's syntax; otherwise <see langword="false"/> (a wrong
    ///     kind, an unknown scheme or malformed phonemes are not an error: the backend then speaks
    ///     the word as usual).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is null.</exception>
    /// <remarks>Stateless and thread-safe.</remarks>
    public static bool TryReadPronunciation(SegmentAnnotation annotation, out PronunciationScheme scheme, out string phonemes)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        if (string.Equals(annotation.Kind, Pronunciation, StringComparison.Ordinal)
            && OverrideSyntax.ParsePronunciation(annotation.Value, out scheme, out phonemes) is null)
        {
            return true;
        }

        scheme = default;
        phonemes = string.Empty;
        return false;
    }

    /// <summary>
    ///     Reads the spoken replacement out of either replacement annotation: a
    ///     <see cref="Normalized"/> one (words) or a <see cref="SpellOut"/> one (letters).
    /// </summary>
    /// <param name="annotation">The annotation to read. Must not be null.</param>
    /// <param name="spokenText">
    ///     When this method returns <see langword="true"/>, the exact replacement for the
    ///     annotated span (possibly empty for a normalization that is spoken as nothing);
    ///     otherwise an empty string.
    /// </param>
    /// <param name="isSpelled">
    ///     <see langword="true"/> when the replacement is a letter sequence to be spoken by letter
    ///     name (a <see cref="SpellOut"/> annotation); <see langword="false"/> for words.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> for a <see cref="Normalized"/> annotation, or for a
    ///     <see cref="SpellOut"/> annotation with a well-formed letter sequence; otherwise
    ///     <see langword="false"/> (another kind or a malformed spelled value).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is null.</exception>
    /// <remarks>
    ///     The single entry point a backend uses to collect the replacement annotations of a
    ///     segment. Stateless and thread-safe.
    /// </remarks>
    public static bool TryReadReplacement(SegmentAnnotation annotation, out string spokenText, out bool isSpelled)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        if (TryReadNormalized(annotation, out spokenText))
        {
            isSpelled = false;
            return true;
        }

        if (TryReadSpellOut(annotation, out spokenText))
        {
            isSpelled = true;
            return true;
        }

        isSpelled = false;
        return false;
    }

    /// <summary>
    ///     Reads the reading id out of a <see cref="Reading"/> annotation.
    /// </summary>
    /// <param name="annotation">The annotation to read. Must not be null.</param>
    /// <param name="readingId">
    ///     When this method returns <see langword="true"/>, the reading id (for example
    ///     <c>record.noun</c>); otherwise an empty string.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="annotation"/> has kind <see cref="Reading"/> and a
    ///     value that satisfies the reading-id grammar (two or three dot-separated segments of lower-case
    ///     letters, digits and inner hyphens, at most 64 characters); otherwise
    ///     <see langword="false"/>. A wrong kind or a malformed value is an ordinary "no", never an
    ///     exception, and the id is not looked up in the inventory (see
    ///     <see cref="ReadingInventory.Contains"/> for that).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is null.</exception>
    /// <remarks>Stateless and thread-safe.</remarks>
    public static bool TryReadReading(SegmentAnnotation annotation, out string readingId)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        if (string.Equals(annotation.Kind, Reading, StringComparison.Ordinal) && ReadingIdSyntax.IsValid(annotation.Value))
        {
            readingId = annotation.Value;
            return true;
        }

        readingId = string.Empty;
        return false;
    }

    /// <summary>
    ///     Reads the confidence out of a <see cref="ReadingConfidence"/> annotation.
    /// </summary>
    /// <param name="annotation">The annotation to read. Must not be null.</param>
    /// <param name="confidence">
    ///     When this method returns <see langword="true"/>, the confidence from 0 to 1; otherwise 0.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="annotation"/> has kind
    ///     <see cref="ReadingConfidence"/> and a value of the form <c>0</c> or <c>1</c> optionally followed
    ///     by a period and one to six ASCII digits, not above 1 (so <c>0.5</c>, <c>0.873</c>,
    ///     <c>1.000</c> and <c>1</c> are accepted; a sign, an exponent, whitespace, a comma decimal
    ///     separator, non-ASCII digits and <c>1.001</c> are not); otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is null.</exception>
    /// <remarks>
    ///     The value is parsed with the invariant culture whatever the current culture. Stateless and
    ///     thread-safe; never throws for a malformed value.
    /// </remarks>
    public static bool TryReadReadingConfidence(SegmentAnnotation annotation, out double confidence)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        if (string.Equals(annotation.Kind, ReadingConfidence, StringComparison.Ordinal)
            && IsConfidenceText(annotation.Value)
            && double.TryParse(annotation.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed)
            && parsed <= 1.0)
        {
            confidence = parsed;
            return true;
        }

        confidence = 0.0;
        return false;
    }

    /// <summary>
    ///     Tells whether text has the exact shape of a confidence: <c>0</c> or <c>1</c>, optionally a period
    ///     and one to six ASCII digits.
    /// </summary>
    /// <param name="value">The candidate.</param>
    /// <returns><see langword="true"/> for the grammar; the numeric bound is checked by the caller.</returns>
    private static bool IsConfidenceText(string value)
    {
        const int MaxFractionDigits = 6;
        if (value.Length == 0 || value[0] is not ('0' or '1'))
        {
            return false;
        }

        if (value.Length == 1)
        {
            return true;
        }

        // A period and one to six ASCII digits; nothing else (no sign, exponent, space or other digit script).
        if (value[1] != '.' || value.Length < 3 || value.Length - 2 > MaxFractionDigits)
        {
            return false;
        }

        for (var i = 2; i < value.Length; i++)
        {
            if (value[i] is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}
