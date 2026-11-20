using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DemaConsulting.Speech.Phonemes.En.LetterToSoundSubsystem;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;

/// <summary>
///     A pronounced word: the pronunciation and where it came from.
/// </summary>
/// <param name="Pronunciation">The ARPAbet pronunciation.</param>
/// <param name="Source">The source.</param>
/// <param name="ElementStarts">The index of the first phone of each element after the first of a hyphenated compound pronounced part by part, or <see langword="null"/> for any other word.</param>
internal sealed record PronouncedWord(Pronunciation Pronunciation, PronunciationSource Source, IReadOnlyList<int>? ElementStarts = null)
{
    /// <summary>Gets the index of the first phone of the second part of a compound pronounced part by part, or -1 for any other word.</summary>
    public int CompoundBreak => ElementStarts is { Count: > 0 } ? ElementStarts[0] : -1;
}

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

    /// <summary>
    ///     The prefixes that, written with a hyphen before a word, keep the primary stress on that word (<c>re-enter</c>,
    ///     <c>non-fiction</c>, <c>ex-wife</c>), with the sound and weak stress they have as a prefix. In the dictionary
    ///     the primary stress is on the root for 99 percent of the words with <c>un-</c>, 96 of <c>dis-</c>, 95 of <c>mis-</c>,
    ///     92 of <c>re-</c>, 87 of <c>ex-</c>, 85 of <c>de-</c>, 83 of <c>pre-</c>, 81 of <c>semi-</c>, 77 of <c>non-</c>, 76 of
    ///     <c>anti-</c> and 73 of <c>inter-</c>; for <c>co-</c>, <c>sub-</c>, <c>post-</c>, <c>multi-</c>, <c>super-</c> and
    ///     <c>pro-</c> it is a toss-up (and for <c>over-</c>, <c>under-</c>, <c>out-</c>, <c>self-</c> and the like it is on the
    ///     first element), so those are compounds like any other.
    /// </summary>
    private static readonly Dictionary<string, Pronunciation> CompoundPrefixForms = BuildCompoundPrefixForms();

    /// <summary>
    ///     Builds the prefix table of hyphenated words: the productive prefixes whose root keeps the primary stress.
    /// </summary>
    /// <returns>The sound of each such prefix.</returns>
    private static Dictionary<string, Pronunciation> BuildCompoundPrefixForms()
    {
        string[] names = ["re", "pre", "un", "dis", "mis", "non", "anti", "semi", "inter"];
        var forms = new Dictionary<string, Pronunciation>(StringComparer.Ordinal);
        foreach (var (prefix, phones) in Prefixes)
        {
            if (Array.IndexOf(names, prefix) >= 0)
            {
                forms[prefix] = phones;
            }
        }

        // Prefixes that are not productive for the words the dictionary lacks (exacted and decide are not ex-wife and de-ice)
        forms["ex"] = Pronunciation.Parse("EH2 K S");
        forms["de"] = Pronunciation.Parse("D IY0");
        return forms;
    }

    /// <summary>The most British-spelling rules combined in one respelling.</summary>
    private const int MaxSpellingRules = 3;

    /// <summary>The shortest first element of a place name in <c>-mouth</c>.</summary>
    private const int MinPlaceNameStem = 2;

    /// <summary>The reduced last element of an English place name in <c>-mouth</c>.</summary>
    private static readonly Pronunciation ReducedMouth = Pronunciation.Parse("M AH0 TH");

    /// <summary>The time any one spelling rule may take on one word (the words are short; this only bounds a pathological input).</summary>
    private static readonly TimeSpan SpellingRuleTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     The correspondences between British and American spellings, each a pattern on the lower-case word and its
    ///     replacement (see <see cref="TryAmericanSpelling"/>). The contexts (a consonant before, a minimum length) keep
    ///     each rule to the words where the letters are the spelling convention and not part of the root.
    /// </summary>
    private static readonly (Regex Pattern, string Replacement)[] BritishSpellingRules =
    [
        (SpellingRule("oeu"), "eu"),
        (SpellingRule("(?<=^(?=.{7}).*[bcdfghjklmnpqrstvwxz])ae(?=[bcdfghjklmnpqrstvwxz])"), "e"),
        (SpellingRule("^oe(?=[bcdfghjklmnpqrstvwxz])|(?<=rh)oe(?=a)"), "e"),
        (SpellingRule("(?<=[a-z]{2}[bcdfghjklmnpqrstvwxz])our(?=$|s$|ed$|ing$|ite|ful|able|ably|less|ism|ist)"), "or"),
        (SpellingRule("(?<=[a-z]{3}[tbv])re$"), "er"),
        (SpellingRule("(?<=[a-z]{3}[tbv])res$"), "ers"),
        (SpellingRule("(?<=[a-z]{3}[tbv])red$"), "ered"),
        (SpellingRule("(?<=[a-z]{3}[tbv])ring$"), "ering"),
        (SpellingRule("(?<=[a-z]{3}[bcdfghjklmnpqrstvwxz])is(?=(e|es|ed|ing|ation|ations)$)"), "iz"),
        (SpellingRule("(?<=[a-z]{3})ys(?=(e|ed|ing)$)"), "yz"),
        (SpellingRule("(?<=[a-z]{2}[bcdfghjklmnpqrstvwxz][ae])ll(?=(ed|ing|er|ers)$)"), "l"),
        (SpellingRule("(?<=[a-z]{2}[fc])ence(?=s?$)"), "ense"),
        (SpellingRule("(?<=[a-z]{3})ogue(?=s?$)"), "og"),
        (SpellingRule("(?<=[a-z]{4})amme(?=s?$)"), "am"),
    ];
    /// <summary>The lexicon.</summary>
    private readonly CmuLexicon _lexicon;

    /// <summary>The reading tables.</summary>
    private readonly ReadingPronunciations _readings;

    /// <summary>The reviewed corrections of wrong dictionary entries.</summary>
    private readonly LexiconCorrections _corrections;

    /// <summary>The reviewed additions: words the dictionary lacks, consulted after it.</summary>
    private readonly LexiconCorrections _additions;

    /// <summary>The out-of-vocabulary fallback.</summary>
    private readonly LetterToSound _letterToSound;

    /// <summary>
    ///     Initializes a pronouncer.
    /// </summary>
    /// <param name="lexicon">The lexicon.</param>
    /// <param name="readings">The reading tables.</param>
    /// <param name="letterToSound">The fallback for words the lexicon and the morphology cannot pronounce.</param>
    /// <param name="corrections">The reviewed corrections of wrong dictionary entries, or <see langword="null"/> for the embedded table.</param>
    /// <param name="additions">The reviewed additions of words the dictionary lacks, or <see langword="null"/> for the embedded table.</param>
    public WordPronouncer(CmuLexicon lexicon, ReadingPronunciations readings, LetterToSound letterToSound, LexiconCorrections? corrections = null, LexiconCorrections? additions = null)
    {
        _lexicon = lexicon;
        _readings = readings;
        _letterToSound = letterToSound;
        _corrections = corrections ?? LexiconCorrections.Default;
        _additions = additions ?? LexiconCorrections.Additions;
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

        // A word written with a capital is treated as a name by the rules that read the spelling of an unknown word
        var capitalized = word.Length > 0 && char.IsUpper(word[0]);
        return normalized.Contains('-') ? PronounceCompound(normalized, capitalized) : PronounceSimple(normalized, readingId, capitalized);
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
    /// <param name="capitalized">Whether the word was written with a capital letter (a name, or the first word of a sentence).</param>
    /// <returns>The pronounced word.</returns>
    private PronouncedWord PronounceSimple(string word, string? readingId, bool capitalized)
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

        // A word the dictionary lacks whose pronunciation was reviewed and added (agave, aquiver)
        if (_additions.TryGet(word, out var added))
        {
            return new PronouncedWord(added, PronunciationSource.Lexicon);
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

        // A British spelling of a dictionary word (manoeuvring, analyse, civilisation); not for a name, whose letters
        // are not a spelling convention (Kraemer is not Kremer)
        var american = capitalized ? null : TryAmericanSpelling(word);
        if (american is not null)
        {
            return new PronouncedWord(american, PronunciationSource.Morphology);
        }

        // An English place name in -mouth, whose last element is reduced (Portsmouth, Plymouth: M AH0 TH)
        var placeName = capitalized ? TryPlaceNameInMouth(word) : null;
        if (placeName is not null)
        {
            return new PronouncedWord(placeName, PronunciationSource.Morphology);
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
    ///     Pronounces a word written in a British spelling as the dictionary's American spelling of it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     The dictionary is American, so a British spelling it lacks (<i>manoeuvring</i>, <i>analyse</i>, <i>civilisation</i>,
    ///     <i>labours</i>, <i>quarrelling</i>) would go to the letter-to-sound model, which reads the unfamiliar letters
    ///     literally (<i>manoeuvring</i> as <c>M AE1 N OW0 UW2 V R IH0 NG</c>). The two spellings of these words sound the
    ///     same, so the word is respelled by the systematic correspondences (<c>oeu</c> to <c>eu</c>, <c>ae</c> and <c>oe</c>
    ///     to <c>e</c> in the classical words, <c>-our</c> to <c>-or</c>, <c>-tre</c>, <c>-bre</c> and <c>-vre</c> to
    ///     <c>-ter</c>, <c>-ber</c> and <c>-ver</c>, <c>-ise</c> and <c>-yse</c> to <c>-ize</c> and <c>-yze</c>, the doubled
    ///     <c>l</c> of <i>travelled</i>, <c>-ence</c> to <c>-ense</c> after <c>f</c> or <c>c</c>, <c>-ogue</c> to
    ///     <c>-og</c>, <c>-amme</c> to <c>-am</c>), alone and up to three together, and the first respelling that is a
    ///     dictionary word is taken.
    ///     </para>
    ///     <para>
    ///     A respelling is accepted only when it is itself a dictionary word, so a word that merely contains the letters
    ///     is never changed into a non-word. The rules are measured in <c>docs/evaluation/phonemes-en/renderers.md</c>:
    ///     on the lower-case words of the repository's text corpora that the dictionary lacks they rewrite 23 words, 22 of
    ///     them right; the wrong rewrites on dictionary words are names (<i>Kraemer</i>, <i>Oehler</i>), which is why the
    ///     caller does not apply them to a capitalized word.
    ///     </para>
    /// </remarks>
    /// <param name="word">The normalized word (lower case).</param>
    /// <returns>The pronunciation of the American spelling, or <see langword="null"/> when no respelling is a dictionary word.</returns>
    private Pronunciation? TryAmericanSpelling(string word)
    {
        // The rules that change this word, in table order
        var applicable = BritishSpellingRules.Where(rule => rule.Pattern.IsMatch(word)).ToList();

        // Fewer changes first: every single rule, then every pair, then every triple, in table order
        for (var size = 1; size <= Math.Min(MaxSpellingRules, applicable.Count); size++)
        {
            foreach (var combination in Combinations(applicable.Count, size))
            {
                var respelled = word;
                foreach (var index in combination)
                {
                    respelled = applicable[index].Pattern.Replace(respelled, applicable[index].Replacement);
                }

                var listed = respelled == word ? [] : _lexicon.Lookup(respelled);
                if (listed.Count > 0)
                {
                    return Morphology.OrderVariants(respelled, listed, _lexicon)[0];
                }
            }
        }

        return null;
    }

    /// <summary>
    ///     Enumerates the combinations of a size from the indexes 0 to <paramref name="count"/> - 1, in lexicographic order.
    /// </summary>
    /// <param name="count">The number of items.</param>
    /// <param name="size">The size of each combination, at most <paramref name="count"/>.</param>
    /// <returns>The combinations, each an ascending array of indexes.</returns>
    private static IEnumerable<int[]> Combinations(int count, int size)
    {
        var indexes = Enumerable.Range(0, size).ToArray();
        while (true)
        {
            yield return (int[])indexes.Clone();

            // Advance the rightmost index that can still move, then reset the ones after it
            var position = size - 1;
            while (position >= 0 && indexes[position] == count - size + position)
            {
                position--;
            }

            if (position < 0)
            {
                yield break;
            }

            indexes[position]++;
            for (var next = position + 1; next < size; next++)
            {
                indexes[next] = indexes[next - 1] + 1;
            }
        }
    }

    /// <summary>
    ///     Pronounces a capitalized word in <c>-mouth</c> that the dictionary lacks as an English place name.
    /// </summary>
    /// <remarks>
    ///     The <c>-mouth</c> of an English place name is reduced: the dictionary lists every such place name it has with
    ///     <c>M AH0 TH</c> (<i>Dartmouth</i>, <i>Falmouth</i>, <i>Monmouth</i>, <i>Plymouth</i>, <i>Portsmouth</i>,
    ///     <i>Weymouth</i>) and its only common nouns in <c>-mouth</c> with the full vowel (<i>cottonmouth</i>,
    ///     <i>frogmouth</i>). The letter-to-sound model gives an unknown one the full diphthong (<i>Rhinemouth</i> as
    ///     <c>R AY1 N M AW2 TH</c>), which the voice reads as two words, <i>Rhine</i> and <i>mouth</i>, lengthened and
    ///     with a pause between them. The first element is pronounced as a word of its own (dictionary, morphology or
    ///     letter-to-sound) and keeps its stress.
    /// </remarks>
    /// <param name="word">The normalized word (lower case).</param>
    /// <returns>The pronunciation, or <see langword="null"/> when the word does not end in <c>mouth</c> after at least two letters.</returns>
    private Pronunciation? TryPlaceNameInMouth(string word)
    {
        const string suffix = "mouth";
        if (word.Length < suffix.Length + MinPlaceNameStem || !word.EndsWith(suffix, StringComparison.Ordinal))
        {
            return null;
        }

        var stem = PronounceSimple(word[..^suffix.Length], null, capitalized: false).Pronunciation;
        return stem.Count == 0 ? null : stem.Append(ReducedMouth.Phones);
    }
    /// <summary>
    ///     Pronounces a hyphenated compound.
    /// </summary>
    /// <param name="word">The normalized word with at least one hyphen.</param>
    /// <param name="capitalized">Whether the word was written with a capital letter; it applies to the first part only.</param>
    /// <returns>The pronounced word.</returns>
    private PronouncedWord PronounceCompound(string word, bool capitalized)
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

        // Part by part; the small function words inside a compound (mother-in-law, state-of-the-art) are unstressed, and a
        // productive prefix (re-enter, non-fiction) has the sound and the weak stress of a prefix, not of the word of the same spelling
        var result = Pronunciation.Empty;
        var source = PronunciationSource.Lexicon;
        var starts = new List<int>();
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var prefixForm = i == 0 && parts.Length > 1 && CompoundPrefixForms.TryGetValue(part, out var form0) ? form0 : null;
            var pronounced = prefixForm is null ? PronounceSimple(part, null, capitalized && i == 0) : new PronouncedWord(prefixForm, PronunciationSource.Lexicon);
            source = (PronunciationSource)Math.Max((int)source, (int)pronounced.Source);
            var phones = pronounced.Pronunciation.Phones;
            if (i > 0 && UnstressedCompoundParts.Contains(part))
            {
                phones = CompoundPartForms.TryGetValue(part, out var form) ? form.Phones : WithoutStress(pronounced.Pronunciation).Phones;
            }

            // Remember where each element after the first starts: a renderer whose voice stresses only the first part can demote the rest, and a sound rule must not look across the joint
            if (i > 0)
            {
                starts.Add(result.Count);
            }

            result = result.Append(phones);
        }

        // A single letter as the first element (e-mail) is spoken by its name with its own stress, so the closed spelling's pattern does not apply
        return new PronouncedWord(CompoundStress(result, starts, parts[0].Length == 1 ? Array.Empty<Pronunciation>() : listed), source, starts);
    }

    /// <summary>
    ///     Gives the phones of a compound pronounced part by part the stress of one word.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Pronounced part by part, every part of <c>battle-field</c> keeps a primary stress, and a voice then reads the
    ///     compound as two stressed words with a break between them. When the dictionary lists the closed spelling with
    ///     exactly the same sounds (<c>battlefield</c>, <c>cooperate</c>, <c>nonfiction</c>) its stress pattern is the
    ///     pattern of the word, wherever its primary stress falls (<c>K OW0 AA1 P ER0 EY2 T</c> has it on the root). The
    ///     closed spelling of a word that merely sounds different (<c>resign</c> is not <c>re-sign</c>) is not used.
    ///     </para>
    ///     <para>
    ///     Otherwise the stress is that of the parts with one primary stress: the first one stays and a later one becomes
    ///     a secondary stress, which is the pattern of a true compound (one main stress on its first element:
    ///     <c>B AE1 T AH0 L F IY2 L D</c>). A productive prefix has been given its own weak stress before, so the root of
    ///     <c>re-enter</c> or <c>non-fiction</c> keeps the primary stress it has as a word, and the prefix has none.
    ///     </para>
    /// </remarks>
    /// <param name="phones">The phones of all parts.</param>
    /// <param name="starts">The index of the first phone of each element after the first.</param>
    /// <param name="closed">The dictionary's variants of the closed spelling; may be empty.</param>
    /// <returns>The phones with the compound's stress.</returns>
    private static Pronunciation CompoundStress(Pronunciation phones, IReadOnlyList<int> starts, IReadOnlyList<Pronunciation> closed)
    {
        var stressed = phones.Phones.ToArray();
        var secondPartStart = starts.Count > 0 ? starts[0] : int.MaxValue;

        // The closed spelling's stress pattern, when it has the sounds of the parts
        foreach (var variant in closed)
        {
            if (variant.Count == stressed.Length && SameSounds(variant.Phones, stressed))
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
    ///     Builds one British-spelling pattern.
    /// </summary>
    /// <param name="pattern">The regular expression on the lower-case word.</param>
    /// <returns>The compiled pattern, culture-invariant and bounded in time.</returns>
    private static Regex SpellingRule(string pattern) => new(pattern, RegexOptions.CultureInvariant, SpellingRuleTimeout);
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
