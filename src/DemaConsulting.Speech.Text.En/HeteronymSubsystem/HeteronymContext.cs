using System.Text;

namespace DemaConsulting.Speech.Text.En.HeteronymSubsystem;

/// <summary>
///     One token of a classification context: everything the feature extractor needs to know about
///     the token, derived once so extraction never touches the text again.
/// </summary>
/// <param name="Start">The UTF-16 offset of the token's first code unit in the analyzed text.</param>
/// <param name="Length">The number of UTF-16 code units of the token; always positive.</param>
/// <param name="Upos">The Universal Dependencies tag code (alphabetical 1 to 17), or 0 when the token was not tagged.</param>
/// <param name="ConfidenceBucket">The tag confidence bucket: 3 for at least 0.99, 2 for at least 0.9, 1 for at least 0.7, otherwise 0.</param>
/// <param name="Class">The closed class of the token (see <see cref="HeteronymContext"/>).</param>
/// <param name="Shape">The shape code of the token (see <see cref="HeteronymContext"/>).</param>
/// <param name="IsWord">Whether the token holds at least one letter or digit, counted per Unicode scalar.</param>
/// <param name="LowerHash">The FNV-1a hash of the normalized lower-case form (ASCII folding, curly apostrophes plain, digit-only tokens <c>&lt;num&gt;</c>).</param>
/// <param name="LowerLength">The number of UTF-16 code units of the normalized lower-case form.</param>
/// <param name="Suffix1Hash">The hash of the last code unit of the normalized form; 0 when the form is shorter.</param>
/// <param name="Suffix2Hash">The hash of the last two code units of the normalized form; 0 when the form is shorter.</param>
/// <param name="Suffix3Hash">The hash of the last three code units of the normalized form; 0 when the form is shorter.</param>
/// <param name="SurfaceHash">The hash of the token's exact text when it is punctuation; otherwise 0.</param>
/// <remarks>An immutable value; comparing two tokens compares every field, which the golden tests rely on.</remarks>
internal readonly record struct ContextToken(
    int Start,
    int Length,
    byte Upos,
    byte ConfidenceBucket,
    byte Class,
    byte Shape,
    bool IsWord,
    uint LowerHash,
    int LowerLength,
    uint Suffix1Hash,
    uint Suffix2Hash,
    uint Suffix3Hash,
    uint SurfaceHash);

/// <summary>
///     Builds the <see cref="ContextToken"/>s the heteronym feature extractor works on, from a
///     token's span in the text and the part-of-speech tag the tagger gave it.
/// </summary>
/// <remarks>
///     <para>
///     This is the single entry point through which both the analyzer (at run time) and the trainer
///     (offline) describe a token, so the two cannot disagree about a token's class, shape or
///     hashes. Every decision is made per Unicode scalar with culture-free tests
///     (<see cref="Rune"/>), a lone surrogate counting as U+FFFD, which is not a letter; nothing
///     consults the current culture.
///     </para>
///     <para>
///     <b>Class codes.</b> 0 none (an ordinary word), 1 punctuation (no letter or digit), 2 number,
///     3 determiner, 4 pronoun, 5 modal, 6 auxiliary, 7 <c>to</c>, 8 negation, 9 preposition,
///     10 conjunction, and 255 for a position outside the sentence. <b>Shape codes.</b> 1 no letter or
///     digit, 2 all digits, 3 no capitals, 4 one capital letter alone, 5 all capitals (two or more),
///     6 initial capital only, 7 mixed, and 0 for a position outside the sentence. <b>Tag codes.</b>
///     the 17 Universal Dependencies names in alphabetical order are 1 (<c>ADJ</c>) to 17 (<c>X</c>),
///     and anything else is 0.
///     </para>
///     <para>
///     The type is static and thread-safe.
///     </para>
/// </remarks>
internal static class HeteronymContext
{
    /// <summary>The class code of an ordinary word.</summary>
    internal const byte ClassNone = 0;

    /// <summary>The class code of punctuation.</summary>
    internal const byte ClassPunct = 1;

    /// <summary>The class code of a number.</summary>
    internal const byte ClassNum = 2;

    /// <summary>The class code of a determiner; the first function-word class.</summary>
    internal const byte ClassDet = 3;

    /// <summary>The class code of a pronoun.</summary>
    internal const byte ClassPron = 4;

    /// <summary>The class code of a modal verb.</summary>
    internal const byte ClassModal = 5;

    /// <summary>The class code of an auxiliary verb.</summary>
    internal const byte ClassAux = 6;

    /// <summary>The class code of the word <c>to</c>.</summary>
    internal const byte ClassTo = 7;

    /// <summary>The class code of a negation.</summary>
    internal const byte ClassNeg = 8;

    /// <summary>The class code of a preposition.</summary>
    internal const byte ClassPrep = 9;

    /// <summary>The class code of a conjunction.</summary>
    internal const byte ClassConj = 10;

    /// <summary>The class code of a position outside the sentence.</summary>
    internal const byte ClassOutside = 255;

    /// <summary>The longest normalized form of a function word, in code units; longer tokens skip the word lists.</summary>
    private const int MaxFunctionWordLength = 12;

    /// <summary>The normalized form of a digit-only token.</summary>
    private const string NumberForm = "<num>";

    /// <summary>
    ///     Describes one token.
    /// </summary>
    /// <param name="text">The analyzed text the token indexes. Must not be null.</param>
    /// <param name="start">The UTF-16 offset of the token's first code unit.</param>
    /// <param name="length">The token's length in code units; positive.</param>
    /// <param name="tag">The Universal Dependencies tag name (for example <c>NOUN</c>), or <see langword="null"/> when untagged; an unknown name counts as untagged.</param>
    /// <param name="confidence">The tag's confidence; NaN and values below 0.7 fall in bucket 0.</param>
    /// <returns>The token description.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The span is empty or lies outside the text.</exception>
    /// <remarks>
    ///     The span is validated without computing <c>start + length</c>, which could overflow:
    ///     <paramref name="start"/> must not exceed the text length and <paramref name="length"/> must not
    ///     exceed what remains after it. Stateless and thread-safe.
    /// </remarks>
    internal static ContextToken Create(string text, int start, int length, string? tag, double confidence)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (start < 0 || start > text.Length || length <= 0 || length > text.Length - start)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "The token is empty or outside the text.");
        }

        var span = text.AsSpan(start, length);

        // Pass 1: letters, digits, capitals and the scalar count, per Unicode scalar.
        var isWord = false;
        var hasLetter = false;
        var hasDigit = false;
        var allDigits = true;
        var upper = 0;
        var lower = 0;
        var scalars = 0;
        var firstIsUpper = false;
        foreach (var rune in span.EnumerateRunes())
        {
            var isLetter = Rune.IsLetter(rune);
            var isDigit = Rune.IsDigit(rune);
            isWord |= isLetter || isDigit;
            hasLetter |= isLetter;
            hasDigit |= isDigit;
            allDigits &= isDigit;
            if (Rune.IsUpper(rune))
            {
                upper++;
                firstIsUpper |= scalars == 0;
            }
            else if (Rune.IsLower(rune))
            {
                lower++;
            }

            scalars++;
        }

        // The normalized lower-case form: digit-only tokens share one form, everything else is folded.
        var isNumber = hasDigit && !hasLetter;
        uint lowerHash;
        int lowerLength;
        uint suffix1 = 0, suffix2 = 0, suffix3 = 0;
        Span<char> folded = stackalloc char[MaxFunctionWordLength];
        var foldedLength = 0;
        if (isNumber)
        {
            lowerHash = FeatureHashing.Fnv1a(NumberForm);
            lowerLength = NumberForm.Length;
            suffix1 = FeatureHashing.Fnv1a(NumberForm.AsSpan(NumberForm.Length - 1));
            suffix2 = FeatureHashing.Fnv1a(NumberForm.AsSpan(NumberForm.Length - 2));
            suffix3 = FeatureHashing.Fnv1a(NumberForm.AsSpan(NumberForm.Length - 3));
        }
        else
        {
            lowerLength = length;
            var hash = FeatureHashing.FnvOffsetBasis;
            for (var i = 0; i < span.Length; i++)
            {
                var unit = Normalize(span[i]);
                hash = FeatureHashing.Update(hash, unit);
                if (i < folded.Length)
                {
                    folded[i] = unit;
                }
            }

            lowerHash = hash;
            foldedLength = Math.Min(length, folded.Length);
            suffix1 = SuffixHash(span, 1);
            suffix2 = SuffixHash(span, 2);
            suffix3 = SuffixHash(span, 3);
        }

        // The class: punctuation, a function word, a number, or an ordinary word.
        byte cls;
        if (!isWord)
        {
            cls = ClassPunct;
        }
        else if (!isNumber && length <= MaxFunctionWordLength)
        {
            cls = ClassifyWord(folded[..foldedLength]);
        }
        else
        {
            cls = isNumber ? ClassNum : ClassNone;
        }

        // The shape and, for punctuation only, the hash of the exact text (which punctuation mark it is).
        var shape = ShapeOf(isWord, allDigits, upper, lower, scalars, firstIsUpper);
        var surface = cls == ClassPunct ? FeatureHashing.Fnv1a(span) : 0u;
        return new ContextToken(start, length, UposCode(tag), ConfidenceBucket(confidence), cls, shape, isWord, lowerHash, lowerLength, suffix1, suffix2, suffix3, surface);
    }

    /// <summary>
    ///     Normalizes one code unit for the lower-case form: an ASCII capital becomes lower case and the
    ///     two curly single quotes become the plain apostrophe.
    /// </summary>
    /// <param name="unit">The code unit.</param>
    /// <returns>The normalized code unit.</returns>
    private static char Normalize(char unit) =>
        unit is '\u2018' or '\u2019' ? '\'' : FeatureHashing.FoldAscii(unit);

    /// <summary>
    ///     Hashes the last code units of the normalized form.
    /// </summary>
    /// <param name="token">The token's raw characters.</param>
    /// <param name="count">The number of trailing code units, 1 to 3.</param>
    /// <returns>The FNV-1a hash of the normalized suffix, or 0 when the token has fewer code units.</returns>
    private static uint SuffixHash(ReadOnlySpan<char> token, int count)
    {
        if (token.Length < count)
        {
            return 0;
        }

        var hash = FeatureHashing.FnvOffsetBasis;
        for (var i = token.Length - count; i < token.Length; i++)
        {
            hash = FeatureHashing.Update(hash, Normalize(token[i]));
        }

        return hash;
    }

    /// <summary>
    ///     Computes the shape code of a token.
    /// </summary>
    /// <param name="isWord">Whether the token holds a letter or a digit.</param>
    /// <param name="allDigits">Whether every scalar is a digit.</param>
    /// <param name="upper">The number of upper-case scalars.</param>
    /// <param name="lower">The number of lower-case scalars.</param>
    /// <param name="scalars">The number of scalars.</param>
    /// <param name="firstIsUpper">Whether the first scalar is upper case.</param>
    /// <returns>The shape code, 1 to 7.</returns>
    private static byte ShapeOf(bool isWord, bool allDigits, int upper, int lower, int scalars, bool firstIsUpper)
    {
        if (!isWord)
        {
            return 1;
        }

        if (allDigits)
        {
            return 2;
        }

        if (upper == 0)
        {
            return 3;
        }

        if (lower == 0)
        {
            return scalars > 1 ? (byte)5 : (byte)4;
        }

        return firstIsUpper && upper == 1 ? (byte)6 : (byte)7;
    }

    /// <summary>
    ///     Maps a confidence to its bucket.
    /// </summary>
    /// <param name="confidence">The confidence.</param>
    /// <returns>3 for at least 0.99, 2 for at least 0.9, 1 for at least 0.7, otherwise 0 (also for NaN).</returns>
    private static byte ConfidenceBucket(double confidence)
    {
        if (confidence >= 0.99)
        {
            return 3;
        }

        if (confidence >= 0.9)
        {
            return 2;
        }

        return confidence >= 0.7 ? (byte)1 : (byte)0;
    }

    /// <summary>
    ///     Maps a Universal Dependencies tag name to its code.
    /// </summary>
    /// <param name="tag">The tag name, or null.</param>
    /// <returns>The code 1 to 17 (alphabetical order), or 0 for null or an unknown name.</returns>
    internal static byte UposCode(string? tag) => tag switch
    {
        "ADJ" => 1,
        "ADP" => 2,
        "ADV" => 3,
        "AUX" => 4,
        "CCONJ" => 5,
        "DET" => 6,
        "INTJ" => 7,
        "NOUN" => 8,
        "NUM" => 9,
        "PART" => 10,
        "PRON" => 11,
        "PROPN" => 12,
        "PUNCT" => 13,
        "SCONJ" => 14,
        "SYM" => 15,
        "VERB" => 16,
        "X" => 17,
        _ => 0,
    };

    /// <summary>
    ///     Classifies a normalized lower-case word against the closed word lists.
    /// </summary>
    /// <param name="word">The normalized form; at most <see cref="MaxFunctionWordLength"/> code units.</param>
    /// <returns>The function-word class, or <see cref="ClassNone"/> for an ordinary word.</returns>
    /// <remarks>
    ///     A <see langword="switch"/> over the span compares against compile-time constants, so there is no
    ///     dictionary, no allocation and no hashing; the lists are the feature set's definition and
    ///     change only with a feature-set version.
    /// </remarks>
    private static byte ClassifyWord(ReadOnlySpan<char> word) => word switch
    {
        "a" or "an" or "the" or "this" or "that" or "these" or "those" or "my" or "your" or "his" or "her" or "its"
            or "our" or "their" or "each" or "every" or "some" or "any" or "no" or "all" or "both" or "either"
            or "neither" or "another" or "such" or "what" or "which" or "whose" or "many" or "much" or "few"
            or "several" or "more" or "most" or "less" or "least" or "enough" or "own" => ClassDet,
        "i" or "me" or "we" or "us" or "you" or "he" or "him" or "she" or "it" or "they" or "them" or "myself"
            or "yourself" or "himself" or "herself" or "itself" or "ourselves" or "themselves" or "who" or "whom"
            or "whoever" or "whatever" or "whichever" or "someone" or "anyone" or "everyone" or "nobody"
            or "somebody" or "everybody" or "anybody" or "something" or "anything" or "everything" or "nothing"
            or "one" or "ones" or "mine" or "yours" or "hers" or "ours" or "theirs" or "there" or "here" => ClassPron,
        "can" or "could" or "may" or "might" or "must" or "shall" or "should" or "will" or "would" or "cannot"
            or "ought" or "'ll" or "'d" or "wo" or "ca" or "sha" => ClassModal,
        "be" or "is" or "am" or "are" or "was" or "were" or "been" or "being" or "have" or "has" or "had"
            or "having" or "do" or "does" or "did" or "doing" or "'re" or "'m" or "'ve" or "'s" => ClassAux,
        "to" => ClassTo,
        "not" or "n't" or "never" or "nor" or "nt" => ClassNeg,
        "of" or "in" or "on" or "at" or "by" or "for" or "with" or "from" or "into" or "onto" or "upon" or "over"
            or "under" or "about" or "above" or "below" or "between" or "among" or "through" or "during"
            or "before" or "after" or "against" or "within" or "without" or "across" or "toward" or "towards"
            or "along" or "around" or "behind" or "beyond" or "near" or "off" or "out" or "up" or "down"
            or "since" or "until" or "till" or "like" or "per" or "via" or "than" or "as" or "despite" or "except"
            or "inside" or "outside" or "beside" or "besides" or "beneath" or "amid" or "unlike" or "throughout"
            or "underneath" or "alongside" or "atop" => ClassPrep,
        "and" or "or" or "but" or "if" or "because" or "although" or "though" or "while" or "whereas" or "unless"
            or "yet" or "when" or "where" or "whenever" or "once" or "whether" or "how" or "why" or "so" or "then"
            or "also" => ClassConj,
        _ => ClassNone,
    };
}
