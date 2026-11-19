using System.Globalization;
using System.Text;
using DemaConsulting.Speech.Phonemes.En.LetterToSoundSubsystem;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;

/// <summary>
///     A pronounced word: the pronunciation and where it came from.
/// </summary>
/// <param name="Pronunciation">The ARPAbet pronunciation.</param>
/// <param name="Source">The source.</param>
/// <param name="CompoundBreak">The index of the first phone of the second part of a hyphenated compound pronounced part by part, or -1 for any other word.</param>
internal sealed record PronouncedWord(Pronunciation Pronunciation, PronunciationSource Source, int CompoundBreak = -1);

/// <summary>
///     Pronounces one word by the phonemizer's priority list: the reading the text analysis chose, then the
///     lexicon's first variant (the velar comparative first for <i>stronger</i>, see <see cref="Morphology.OrderVariants"/>), then the morphology rules, then the letter-to-sound fallback.
/// </summary>
/// <remarks>
///     <para>
///     A host-supplied override is handled by the phonemizer before it asks this class, because an override
///     replaces the word entirely. A hyphenated compound is pronounced as the lexicon's joined spelling when it has
///     one and a part is not a word of its own, otherwise part by part, with the stress of one compound word: a primary
///     stress on the first element and a secondary stress where a later element had a primary one
///     (<c>well-known</c>: <c>W EH1 L N OW2 N</c>).
///     A word with a diacritic is looked up without it (<c>café</c> as <c>cafe</c>). A word the lexicon and the
///     inflection rules do not know is tried as a productive prefix (<c>un</c>, <c>re</c>, <c>over</c> ...) plus a
///     known word (<c>unfollowed</c>) before it is handed to the letter-to-sound fallback.
///     </para>
///     <para>Stateless apart from its immutable collaborators; thread-safe.</para>
/// </remarks>
internal sealed class WordPronouncer
{
    /// <summary>The shortest remainder after a prefix for the split to be trusted.</summary>
    private const int MinPrefixedRemainder = 4;

    /// <summary>The names of the digits, spoken as lexicon words.</summary>
    private static readonly string[] DigitWords = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    /// <summary>The small function words that are unstressed when they are a later part of a hyphenated compound (<c>mother-in-law</c>).</summary>
    private static readonly HashSet<string> UnstressedCompoundParts = new(
        ["of", "the", "a", "an", "in", "to", "for", "and", "or", "on", "at", "by", "as"],
        StringComparer.Ordinal);

    /// <summary>The reduced sound of <i>to</i> and <i>of</i> inside a compound (<c>day-to-day</c>, <c>state-of-the-art</c>): the vowel of <i>of</i> stays the full one, without a stress mark.</summary>
    private static readonly Dictionary<string, Pronunciation> CompoundPartForms = new(StringComparer.Ordinal)
    {
        ["to"] = Pronunciation.Parse("T AH0"),
        ["of"] = Pronunciation.Parse("AH2 V"),
    };

    /// <summary>The productive prefixes and their sounds, longest first so that <c>counter</c> is tried before <c>co</c>-like shorter ones.</summary>
    private static readonly (string Prefix, Pronunciation Phones)[] Prefixes =
    [
        ("counter", Pronunciation.Parse("K AW2 N T ER0")),
        ("under", Pronunciation.Parse("AH2 N D ER0")),
        ("super", Pronunciation.Parse("S UW2 P ER0")),
        ("inter", Pronunciation.Parse("IH2 N T ER0")),
        ("multi", Pronunciation.Parse("M AH2 L T IY0")),
        ("semi", Pronunciation.Parse("S EH2 M IY0")),
        ("anti", Pronunciation.Parse("AE2 N T IY0")),
        ("over", Pronunciation.Parse("OW2 V ER0")),
        ("non", Pronunciation.Parse("N AA2 N")),
        ("out", Pronunciation.Parse("AW2 T")),
        ("sub", Pronunciation.Parse("S AH0 B")),
        ("dis", Pronunciation.Parse("D IH0 S")),
        ("mis", Pronunciation.Parse("M IH0 S")),
        ("pre", Pronunciation.Parse("P R IY0")),
        ("un", Pronunciation.Parse("AH0 N")),
        ("re", Pronunciation.Parse("R IY0")),
    ];

    /// <summary>The lexicon.</summary>
    private readonly CmuLexicon _lexicon;

    /// <summary>The reading tables.</summary>
    private readonly ReadingPronunciations _readings;

    /// <summary>The reviewed corrections of wrong dictionary entries.</summary>
    private readonly LexiconCorrections _corrections;

    /// <summary>The out-of-vocabulary fallback.</summary>
    private readonly LetterToSound _letterToSound;

    /// <summary>
    ///     Initializes a pronouncer.
    /// </summary>
    /// <param name="lexicon">The lexicon.</param>
    /// <param name="readings">The reading tables.</param>
    /// <param name="letterToSound">The fallback for words the lexicon and the morphology cannot pronounce.</param>
    /// <param name="corrections">The reviewed corrections of wrong dictionary entries, or <see langword="null"/> for the embedded table.</param>
    public WordPronouncer(CmuLexicon lexicon, ReadingPronunciations readings, LetterToSound letterToSound, LexiconCorrections? corrections = null)
    {
        _lexicon = lexicon;
        _readings = readings;
        _letterToSound = letterToSound;
        _corrections = corrections ?? LexiconCorrections.Default;
    }

    /// <summary>
    ///     Pronounces a word.
    /// </summary>
    /// <param name="word">The word as written; any case, apostrophes straight or typographic.</param>
    /// <param name="readingId">The reading id the text analysis chose for it, or <see langword="null"/>.</param>
    /// <returns>The pronounced word, or <see langword="null"/> when the word contains letters that cannot be pronounced.</returns>
    public PronouncedWord? Pronounce(string word, string? readingId)
    {
        var normalized = Normalize(word);
        if (normalized is null)
        {
            return null;
        }

        return normalized.Contains('-') ? PronounceCompound(normalized) : PronounceSimple(normalized, readingId);
    }

    /// <summary>
    ///     Gets the name of a letter as a pronounced word.
    /// </summary>
    /// <param name="letter">A Latin letter.</param>
    /// <returns>The name, or <see langword="null"/> when the letter has no ASCII form.</returns>
    public static PronouncedWord? Letter(char letter)
    {
        var folded = Normalize(letter.ToString());
        if (folded is { Length: 1 } && LetterNames.TryGet(folded[0], out var name))
        {
            return new PronouncedWord(name, PronunciationSource.Spelled);
        }

        return null;
    }

    /// <summary>
    ///     Gets the name of a digit as a pronounced word.
    /// </summary>
    /// <param name="digit">An ASCII digit.</param>
    /// <returns>The word.</returns>
    public PronouncedWord Digit(char digit)
    {
        var variants = _lexicon.Lookup(DigitWords[digit - '0']);
        return new PronouncedWord(variants[0], PronunciationSource.Digits);
    }

    /// <summary>
    ///     Pronounces a word that has no hyphen.
    /// </summary>
    /// <param name="word">The normalized word (lower case, ASCII letters and apostrophes).</param>
    /// <param name="readingId">The reading id, or <see langword="null"/>.</param>
    /// <returns>The pronounced word.</returns>
    private PronouncedWord PronounceSimple(string word, string? readingId)
    {
        // The reading the text analysis chose, when the table can resolve this spelling under it
        if (readingId is not null && _readings.TryResolve(word, readingId, out var read) && read is not null)
        {
            return new PronouncedWord(read, PronunciationSource.Reading);
        }

        // A word whose dictionary entry is a known transcription error (nobly)
        if (_corrections.TryGet(word, out var corrected))
        {
            return new PronouncedWord(corrected, PronunciationSource.Lexicon);
        }

        // The dictionary's first variant is the default
        var listed = _lexicon.Lookup(word);
        if (listed.Count > 0)
        {
            return new PronouncedWord(Morphology.OrderVariants(word, listed, _lexicon)[0], PronunciationSource.Lexicon);
        }

        // A form the dictionary lacks, derived from its stem (cats, unfollowed, zebra's)
        if (Morphology.TryDerive(word, _lexicon, out var derivation))
        {
            return new PronouncedWord(derivation.Pronunciations[0], PronunciationSource.Morphology);
        }

        // A productive prefix on a known word (unfollow, rebuild, overpriced)
        var prefixed = TryPrefixed(word);
        if (prefixed is not null)
        {
            return new PronouncedWord(prefixed, PronunciationSource.Morphology);
        }

        // Out of vocabulary: never silent
        if (_letterToSound.TryConvert(word, out var guessed))
        {
            return new PronouncedWord(guessed, PronunciationSource.LetterToSound);
        }

        return new PronouncedWord(Pronunciation.Empty, PronunciationSource.LetterToSound);
    }

    /// <summary>
    ///     Pronounces a word as a productive prefix followed by a word the dictionary or the morphology knows.
    /// </summary>
    /// <remarks>
    ///     Only a word the dictionary does not list reaches here, and only a remainder of at least
    ///     <see cref="MinPrefixedRemainder"/> letters that is itself known is accepted, so a real word that merely begins like a
    ///     prefix is never split. The prefix is unstressed (or secondary) and the remainder keeps its stress.
    /// </remarks>
    /// <param name="word">The normalized word.</param>
    /// <returns>The pronunciation, or <see langword="null"/> when no prefix applies.</returns>
    private Pronunciation? TryPrefixed(string word)
    {
        foreach (var (prefix, phones) in Prefixes)
        {
            if (word.Length - prefix.Length < MinPrefixedRemainder || !word.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var remainder = word[prefix.Length..];
            var listed = _lexicon.Lookup(remainder);
            if (listed.Count > 0)
            {
                return phones.Append(listed[0].Phones);
            }

            if (Morphology.TryDerive(remainder, _lexicon, out var derivation))
            {
                return phones.Append(derivation.Pronunciations[0].Phones);
            }
        }

        return null;
    }

    /// <summary>
    ///     Pronounces a hyphenated compound.
    /// </summary>
    /// <param name="word">The normalized word with at least one hyphen.</param>
    /// <returns>The pronounced word.</returns>
    private PronouncedWord PronounceCompound(string word)
    {
        var parts = word.Split('-', StringSplitOptions.RemoveEmptyEntries);

        // A compound the dictionary lists closed is used as listed only when a part is not a word of its own (wi-fi:
        // wifi); when every part is a word the sounds are those of the parts (re-sign is not resign), and the closed
        // spelling is consulted only for its stress pattern
        var joined = word.Replace("-", string.Empty, StringComparison.Ordinal);
        var listed = _lexicon.Lookup(joined);
        if (listed.Count > 0 && !parts.All(_lexicon.Contains))
        {
            return new PronouncedWord(listed[0], PronunciationSource.Lexicon);
        }

        // Part by part; the small function words inside a compound (mother-in-law, state-of-the-art) are unstressed
        var result = Pronunciation.Empty;
        var source = PronunciationSource.Lexicon;
        var secondPartStart = -1;
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var pronounced = PronounceSimple(part, null);
            source = (PronunciationSource)Math.Max((int)source, (int)pronounced.Source);
            var phones = pronounced.Pronunciation.Phones;
            if (i > 0 && UnstressedCompoundParts.Contains(part))
            {
                phones = CompoundPartForms.TryGetValue(part, out var form) ? form.Phones : WithoutStress(pronounced.Pronunciation).Phones;
            }

            // Remember where the second part starts: a renderer whose voice stresses only the first part can demote the rest
            if (i == 1)
            {
                secondPartStart = result.Count;
            }

            result = result.Append(phones);
        }

        return new PronouncedWord(CompoundStress(result, secondPartStart, listed), source, secondPartStart);
    }

    /// <summary>
    ///     Gives the phones of a compound pronounced part by part the stress of one word: one primary stress, on the
    ///     first part (where the dictionary lists the closed spelling with the same sounds, its stress pattern), and
    ///     a secondary stress where a later part had a primary one.
    /// </summary>
    /// <remarks>
    ///     Pronounced part by part, every part of <c>battle-field</c> keeps a primary stress, and a voice then reads the
    ///     compound as two stressed words with a break between them. The stress pattern of an English compound is one
    ///     main stress on its first element and a lesser stress on the later ones (<c>battlefield</c>: <c>B AE1 T AH0 L F IY2 L D</c>).
    ///     The closed spelling of the dictionary is trusted for the pattern only when it sounds exactly like the parts
    ///     (<c>battlefield</c> does; <c>resign</c> is not <c>re-sign</c>, so it is not used).
    /// </remarks>
    /// <param name="phones">The phones of all parts.</param>
    /// <param name="secondPartStart">The index of the first phone of the second part.</param>
    /// <param name="closed">The dictionary's variants of the closed spelling; may be empty.</param>
    /// <returns>The phones with the compound's stress.</returns>
    private static Pronunciation CompoundStress(Pronunciation phones, int secondPartStart, IReadOnlyList<Pronunciation> closed)
    {
        var stressed = phones.Phones.ToArray();

        // The closed spelling's stress pattern, when it has the sounds of the parts, one primary stress on the vowel
        // that has it in the parts, and a stress on every later vowel that had a primary one (a later part never
        // loses its stress: the hyphen shows the writer meant two elements)
        foreach (var variant in closed)
        {
            if (variant.Count == stressed.Length && SameSounds(variant.Phones, stressed) && KeepsStressedParts(variant.Phones, stressed, secondPartStart))
            {
                stressed = variant.Phones.ToArray();
                break;
            }
        }

        // One primary stress: the first; any later one is a secondary stress
        var seenPrimary = false;
        for (var i = 0; i < stressed.Length; i++)
        {
            if (stressed[i].Stress != 1)
            {
                continue;
            }

            if (seenPrimary && i >= secondPartStart)
            {
                stressed[i] = stressed[i].WithStress(2);
            }

            seenPrimary = true;
        }

        return new Pronunciation(stressed);
    }

    /// <summary>
    ///     Tests whether the stress pattern of a closed spelling can stand for the stress pattern of a compound's parts.
    /// </summary>
    /// <param name="closed">The phones of the closed spelling.</param>
    /// <param name="parts">The phones of the parts, of the same length and sounds.</param>
    /// <param name="secondPartStart">The index of the first phone of the second part.</param>
    /// <returns>
    ///     <see langword="true"/> when the closed spelling has one primary stress, on the vowel that has the first primary
    ///     stress of the parts, and keeps a stress on every vowel of a later part that had a primary stress.
    /// </returns>
    private static bool KeepsStressedParts(ReadOnlySpan<Phone> closed, Phone[] parts, int secondPartStart)
    {
        var primaries = 0;
        var firstPrimary = -1;
        for (var i = 0; i < parts.Length; i++)
        {
            if (closed[i].Stress == 1)
            {
                primaries++;
            }

            if (parts[i].Stress == 1 && firstPrimary < 0)
            {
                firstPrimary = i;
            }
            else if (parts[i].Stress == 1 && i >= secondPartStart && closed[i].Stress == 0)
            {
                return false;
            }
        }

        return primaries == 1 && firstPrimary >= 0 && closed[firstPrimary].Stress == 1;
    }

    /// <summary>
    ///     Tests whether two phone sequences have the same sounds, whatever their stress.
    /// </summary>
    /// <param name="first">The first sequence.</param>
    /// <param name="second">The second sequence, of the same length.</param>
    /// <returns><see langword="true"/> when every pair of phones has the same base symbol.</returns>
    private static bool SameSounds(ReadOnlySpan<Phone> first, Phone[] second)
    {
        for (var i = 0; i < second.Length; i++)
        {
            if (first[i].BaseSymbol != second[i].BaseSymbol)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Removes every stress from a pronunciation.
    /// </summary>
    /// <param name="pronunciation">The pronunciation.</param>
    /// <returns>The pronunciation with all vowels unstressed.</returns>
    private static Pronunciation WithoutStress(Pronunciation pronunciation)
    {
        var phones = pronunciation.Phones.ToArray();
        for (var i = 0; i < phones.Length; i++)
        {
            if (phones[i].IsVowel)
            {
                phones[i] = phones[i].WithStress(0);
            }
        }

        return new Pronunciation(phones);
    }

    /// <summary>
    ///     Lower-cases a word, straightens its apostrophes and removes diacritics.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <returns>The word in <c>a</c> to <c>z</c>, apostrophes and hyphens, or <see langword="null"/> when it contains anything else after folding.</returns>
    private static string? Normalize(string word)
    {
        var folded = new StringBuilder(word.Length);
        foreach (var c in word.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (WordTokenizer.IsApostrophe(c))
            {
                folded.Append('\'');
            }
            else if (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '-')
            {
                folded.Append(char.ToLowerInvariant(c));
            }
            else if (c == '\u00DF')
            {
                folded.Append("ss");
            }
            else
            {
                return null;
            }
        }

        return folded.ToString();
    }
}
