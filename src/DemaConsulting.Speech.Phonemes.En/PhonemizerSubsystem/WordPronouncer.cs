using System.Globalization;
using System.Text;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;
using DemaConsulting.Speech.Phonemes.En.MorphologySubsystem;
using DemaConsulting.Speech.Phonemes.En.ReadingSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;

/// <summary>
///     A pronounced word: the pronunciation and where it came from.
/// </summary>
/// <param name="Pronunciation">The ARPAbet pronunciation.</param>
/// <param name="Source">The source.</param>
internal sealed record PronouncedWord(Pronunciation Pronunciation, PronunciationSource Source);

/// <summary>
///     Pronounces one word by the phonemizer's priority list: the reading the text analysis chose, then the
///     lexicon's first variant, then the morphology rules, then the letter-to-sound fallback.
/// </summary>
/// <remarks>
///     <para>
///     A host-supplied override is handled by the phonemizer before it asks this class, because an override
///     replaces the word entirely. A hyphenated compound is pronounced as the lexicon's joined spelling when it has
///     one, otherwise part by part, the later parts taking secondary stress where the first part keeps the primary.
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

    /// <summary>The out-of-vocabulary fallback.</summary>
    private readonly ILetterToSound _letterToSound;

    /// <summary>
    ///     Initializes a pronouncer.
    /// </summary>
    /// <param name="lexicon">The lexicon.</param>
    /// <param name="readings">The reading tables.</param>
    /// <param name="letterToSound">The fallback for words the lexicon and the morphology cannot pronounce.</param>
    public WordPronouncer(CmuLexicon lexicon, ReadingPronunciations readings, ILetterToSound letterToSound)
    {
        _lexicon = lexicon;
        _readings = readings;
        _letterToSound = letterToSound;
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

        // The dictionary's first variant is the default
        var listed = _lexicon.Lookup(word);
        if (listed.Count > 0)
        {
            return new PronouncedWord(listed[0], PronunciationSource.Lexicon);
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
        // A compound the dictionary lists closed (icecream) is used as listed
        var joined = word.Replace("-", string.Empty, StringComparison.Ordinal);
        var listed = _lexicon.Lookup(joined);
        if (listed.Count > 0)
        {
            return new PronouncedWord(listed[0], PronunciationSource.Lexicon);
        }

        // Otherwise part by part: the first part keeps its stress, later primary stresses become secondary
        var result = Pronunciation.Empty;
        var source = PronunciationSource.Lexicon;
        var first = true;
        foreach (var part in word.Split('-', StringSplitOptions.RemoveEmptyEntries))
        {
            var pronounced = PronounceSimple(part, null);
            source = (PronunciationSource)Math.Max((int)source, (int)pronounced.Source);
            result = result.Append(first ? pronounced.Pronunciation.Phones : DemoteStress(pronounced.Pronunciation).Phones);
            first = false;
        }

        return new PronouncedWord(result, source);
    }

    /// <summary>
    ///     Lowers every primary stress of a pronunciation to secondary stress.
    /// </summary>
    /// <param name="pronunciation">The pronunciation.</param>
    /// <returns>The pronunciation with no primary stress.</returns>
    private static Pronunciation DemoteStress(Pronunciation pronunciation)
    {
        var phones = pronunciation.Phones.ToArray();
        for (var i = 0; i < phones.Length; i++)
        {
            if (phones[i].Stress == 1)
            {
                phones[i] = phones[i].WithStress(2);
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
