using DemaConsulting.Speech.Phonemes.En.LetterToSoundSubsystem;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;

/// <summary>
///     Turns English text, optionally with the annotations of the English text analysis, into a stream of words
///     with ARPAbet pronunciations: the phoneme-level input a text-to-speech model needs.
/// </summary>
/// <remarks>
///     <para>
///     <b>Input.</b> The text of one speech segment and, optionally, the annotations the
///     <see cref="EnglishTextAnalyzer"/> produced for it (offsets relative to the segment text, as an annotated
///     synthesis backend receives them). With no annotations the phonemizer speaks the text as written: every word gets
///     its default pronunciation (the first variant of the CMU Pronouncing Dictionary) and nothing is normalized, so
///     numbers and abbreviations are only as good as the text. With annotations it applies the replacements
///     (<c>en.norm</c> and <c>en.spell</c>), speaks spelled letters by name, uses the pronunciation of the heteronym
///     reading the text analysis chose (<c>en.reading</c>) and honors host pronunciation overrides (<c>en.pron</c>).
///     </para>
///     <para>
///     <b>Priority for each word.</b> host override, then the reading's pronunciation, then the lexicon's first
///     variant, then the morphology rules (plurals, past tenses, <c>-ing</c>, possessives, contractions), then the
///     letter-to-sound fallback, which marks the word as out of vocabulary so an unknown word is reported, never
///     silent. The fallback is the trained model of the letter-to-sound subsystem (see
///     <c>docs/evaluation/phonemes-en/oov.md</c> for its measured quality).
///     </para>
///     <para>
///     <b>Tokens.</b> Words are runs of Latin letters with inner apostrophes and hyphens (a hyphenated compound is one
///     item); a contraction or possessive stays part of its word; the clause punctuation passes through as
///     punctuation items; a bare run of digits (numbers should arrive already written out by the text analysis) is
///     spoken digit by digit as a safe fallback; anything that cannot be pronounced is kept as an
///     <see cref="PhonemizedItemKind.Unsupported"/> item and reported.
///     </para>
///     <para>Instances are immutable and thread-safe; the dictionary is shared and loaded on first use.</para>
/// </remarks>
public sealed class EnglishPhonemizer
{
    /// <summary>The lexicon.</summary>
    private readonly CmuLexicon _lexicon;

    /// <summary>The word pronouncer.</summary>
    private readonly WordPronouncer _pronouncer;

    /// <summary>
    ///     Initializes a phonemizer over the embedded pronunciation data (the lexicon, the reading tables and the
    ///     letter-to-sound model, each loaded once on first use and shared).
    /// </summary>
    public EnglishPhonemizer()
    {
        _lexicon = CmuLexicon.Default;
        _pronouncer = new WordPronouncer(_lexicon, ReadingPronunciations.Default, LetterToSound.Default);
    }

    /// <summary>
    ///     Phonemizes text as written, without annotations.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The phonemized text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <remarks>Equivalent to <see cref="Phonemize(string, IReadOnlyList{SegmentAnnotation})"/> with no annotations: default readings and no normalization.</remarks>
    public PhonemizedText Phonemize(string text) => Phonemize(text, null);

    /// <summary>
    ///     Phonemizes the text of one segment with the annotations of the English text analysis.
    /// </summary>
    /// <param name="text">The segment text.</param>
    /// <param name="annotations">
    ///     The segment's annotations, offsets relative to <paramref name="text"/>, or <see langword="null"/> or
    ///     empty for none. Kinds the phonemizer does not use (such as <c>en.upos</c>) are ignored.
    /// </param>
    /// <returns>The words and punctuation of the spoken text, each with its pronunciation and provenance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public PhonemizedText Phonemize(string text, IReadOnlyList<SegmentAnnotation>? annotations)
    {
        ArgumentNullException.ThrowIfNull(text);

        // The text as it is spoken, then its tokens
        var spoken = SpokenText.Build(text, annotations);
        var index = AnnotationIndex.Build(annotations);
        var tokens = WordTokenizer.Tokenize(spoken, _lexicon);

        var items = new List<PhonemizedItem>(tokens.Count);
        var previousWordEnd = -1;
        foreach (var token in tokens)
        {
            var added = Add(token, spoken, index, items, previousWordEnd);
            previousWordEnd = added ? token.End : -1;
        }

        return new PhonemizedText(items);
    }

    /// <summary>
    ///     Maps a clitic spelling to its morphology kind.
    /// </summary>
    /// <param name="ending">The letters after the apostrophe.</param>
    /// <param name="kind">Receives the kind.</param>
    /// <returns><see langword="true"/> for <c>s</c>, <c>d</c>, <c>ll</c>, <c>ve</c>, <c>re</c> and <c>m</c>.</returns>
    private static bool TryCliticKind(string ending, out SuffixKind kind)
    {
        SuffixKind? found = ending.ToLowerInvariant() switch
        {
            "s" => SuffixKind.PossessiveS,
            "d" => SuffixKind.ContractionD,
            "ll" => SuffixKind.ContractionLl,
            "ve" => SuffixKind.ContractionVe,
            "re" => SuffixKind.ContractionRe,
            "m" => SuffixKind.ContractionM,
            _ => null,
        };
        kind = found.GetValueOrDefault();
        return found is not null;
    }

    /// <summary>
    ///     Parses the phones of an ARPAbet override.
    /// </summary>
    /// <param name="phonemes">The phones separated by spaces, each a consonant or a vowel with an optional stress digit.</param>
    /// <returns>The pronunciation, or <see langword="null"/> when a phone is not ARPAbet.</returns>
    /// <remarks>When no vowel carries a stress digit the first vowel gets primary stress so the word is not spoken flat.</remarks>
    private static Pronunciation? ParseArpabet(string phonemes)
    {
        var phones = new List<Phone>();
        var anyExplicitStress = false;
        var firstVowel = -1;
        foreach (var symbol in phonemes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Phone.TryParse(symbol, out var phone))
            {
                anyExplicitStress |= phone.IsVowel;
            }
            else if (!Phone.TryParse(symbol + "0", out phone))
            {
                return null;
            }

            if (phone.IsVowel && firstVowel < 0)
            {
                firstVowel = phones.Count;
            }

            phones.Add(phone);
        }

        if (!anyExplicitStress && firstVowel >= 0)
        {
            phones[firstVowel] = phones[firstVowel].WithStress(1);
        }

        return new Pronunciation([.. phones]);
    }

    /// <summary>
    ///     Adds the items of one token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="spoken">The spoken text.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="items">The items so far.</param>
    /// <param name="previousWordEnd">The spoken index where the previous item, if a word, ended; otherwise <c>-1</c>.</param>
    /// <returns><see langword="true"/> when the token produced a word item that a following clitic may attach to.</returns>
    private bool Add(Token token, SpokenText spoken, AnnotationIndex index, List<PhonemizedItem> items, int previousWordEnd)
    {
        var text = spoken.Text[token.Start..token.End];
        switch (token.Kind)
        {
            case TokenKind.Punctuation:
                items.Add(new PhonemizedItem(PhonemizedItemKind.Punctuation, text, null, PronunciationSource.None));
                return false;
            case TokenKind.Unsupported:
                items.Add(new PhonemizedItem(PhonemizedItemKind.Unsupported, text, null, PronunciationSource.None));
                return false;
            case TokenKind.Digits:
                AddDigits(text, items);
                return true;
            case TokenKind.Letter:
                items.Add(Item(text, WordPronouncer.Letter(text[0]), null));
                return true;
            case TokenKind.Clitic:
                return AddClitic(text, items, previousWordEnd == token.Start);
            default:
                items.Add(PronounceWord(token, text, spoken, index));
                return true;
        }
    }

    /// <summary>
    ///     Adds one item per digit.
    /// </summary>
    /// <param name="digits">The digits.</param>
    /// <param name="items">Receives the items.</param>
    private void AddDigits(string digits, List<PhonemizedItem> items)
    {
        foreach (var digit in digits)
        {
            items.Add(Item(digit.ToString(), _pronouncer.Digit(digit), null));
        }
    }

    /// <summary>
    ///     Attaches a clitic token to the preceding word item, or speaks it as an apostrophe and a word when there is none.
    /// </summary>
    /// <param name="text">The token text, an apostrophe and the ending.</param>
    /// <param name="items">The items so far; the last is replaced when the clitic attaches.</param>
    /// <param name="adjacentToWord">Whether a word item ends directly before the clitic.</param>
    /// <returns><see langword="true"/> when the result ends in a word item.</returns>
    private bool AddClitic(string text, List<PhonemizedItem> items, bool adjacentToWord)
    {
        if (adjacentToWord && items.Count > 0 && items[^1] is { Kind: PhonemizedItemKind.Word } host
            && host.Pronunciation.Count > 0 && TryCliticKind(text[1..], out var kind))
        {
            var joined = Morphology.AppendClitic(host.Pronunciation, kind);
            items[^1] = new PhonemizedItem(PhonemizedItemKind.Word, host.Text + text, joined, host.Source, host.ReadingId, elementStarts: host.ElementStarts);
            return true;
        }

        // Nothing to attach to: the apostrophe is a mark and the letters are a word
        items.Add(new PhonemizedItem(PhonemizedItemKind.Punctuation, text[..1], null, PronunciationSource.None));
        items.Add(Item(text[1..], _pronouncer.Pronounce(text[1..], null), null));
        return true;
    }

    /// <summary>
    ///     Pronounces a word token, applying an override or a reading annotation when the token is unchanged original text.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="text">The token text.</param>
    /// <param name="spoken">The spoken text.</param>
    /// <param name="index">The annotation index.</param>
    /// <returns>The item.</returns>
    /// <remarks>
    ///     Annotations are anchored to the headword of the original text (the text analysis tokenizes <c>record's</c> as
    ///     <c>record</c> and <c>'s</c>) while this tokenizer keeps the possessive or contraction in the same token, so an
    ///     annotation may cover only the start of the token. The ending, with a straight or a typographic apostrophe, is
    ///     then attached to the annotated headword's pronunciation. An override beats a reading of the same headword.
    /// </remarks>
    private PhonemizedItem PronounceWord(Token token, string text, SpokenText spoken, AnnotationIndex index)
    {
        // Annotations are anchored to the original text: a token that starts with unchanged original text can carry one
        if (spoken.TryGetSourceSpan(token.Start, token.End, out var sourceStart, out var sourceLength, out var unchangedPrefix))
        {
            var hasOverride = index.TryGetPronunciation(sourceStart, sourceLength, unchangedPrefix, out var scheme, out var phonemes, out var overrideCovered);
            var hasReading = index.TryGetReading(sourceStart, sourceLength, unchangedPrefix, out var readingId, out var readingCovered);
            if (hasOverride && (!hasReading || overrideCovered >= readingCovered))
            {
                return Override(text, SpokenLength(overrideCovered, sourceLength, text), scheme, phonemes) ?? Item(text, _pronouncer.Pronounce(text, null), null);
            }

            if (hasReading)
            {
                return WithReading(text, readingId, SpokenLength(readingCovered, sourceLength, text));
            }
        }

        return Item(text, _pronouncer.Pronounce(text, null), null);
    }

    /// <summary>
    ///     Converts the length of an annotated span in the original text to its length in the token's spoken text.
    /// </summary>
    /// <param name="covered">The annotated length in the original text.</param>
    /// <param name="sourceLength">The length of the whole token's original span.</param>
    /// <param name="text">The token text.</param>
    /// <returns>The whole token's length for an annotation of the whole span; otherwise the same length, which is the unchanged headword.</returns>
    private static int SpokenLength(int covered, int sourceLength, string text) => covered == sourceLength ? text.Length : covered;

    /// <summary>
    ///     Pronounces a word whose reading is known; the reading may cover only the start of the token.
    /// </summary>
    /// <param name="text">The token text.</param>
    /// <param name="readingId">The reading id.</param>
    /// <param name="covered">The length of the annotated part of the token.</param>
    /// <returns>The item.</returns>
    private PhonemizedItem WithReading(string text, string readingId, int covered)
    {
        if (covered == text.Length)
        {
            return Item(text, _pronouncer.Pronounce(text, readingId), readingId);
        }

        // record's: the reading applies to record and the ending follows it
        var baseWord = _pronouncer.Pronounce(text[..covered], readingId);
        if (baseWord is not null && baseWord.Pronunciation.Count > 0 && TryEnding(text[covered..], out var kind))
        {
            var joined = kind is null ? baseWord.Pronunciation : Morphology.AppendClitic(baseWord.Pronunciation, kind.Value);
            return new PhonemizedItem(PhonemizedItemKind.Word, text, joined, baseWord.Source, readingId);
        }

        // Not a clitic ending: the annotation does not describe this token, so speak the token as usual
        return Item(text, _pronouncer.Pronounce(text, null), null);
    }

    /// <summary>
    ///     Builds the item of a host pronunciation override; the override may cover only the headword of the token.
    /// </summary>
    /// <param name="text">The token text.</param>
    /// <param name="covered">The length of the overridden part of the token.</param>
    /// <param name="scheme">The override's scheme.</param>
    /// <param name="phonemes">The override's phonemes.</param>
    /// <returns>The item, or <see langword="null"/> when an ARPAbet override cannot be parsed or the token's ending is not a clitic.</returns>
    private PhonemizedItem? Override(string text, int covered, PronunciationScheme scheme, string phonemes)
    {
        SuffixKind? kind = null;
        if (covered < text.Length && !TryEnding(text[covered..], out kind))
        {
            return null;
        }

        if (scheme == PronunciationScheme.Ipa)
        {
            var ipa = kind is null ? phonemes : AppendIpaClitic(phonemes, kind.Value);
            if (ipa is null)
            {
                return null;
            }

            // The word's normal pronunciation travels with the IPA: a renderer whose voice cannot represent the IPA speaks it instead
            var normal = _pronouncer.Pronounce(text, null);
            return new PhonemizedItem(PhonemizedItemKind.Word, text, normal?.Pronunciation, PronunciationSource.Override, null, ipa, normal?.ElementStarts);
        }

        var parsed = ParseArpabet(phonemes);
        if (parsed is null)
        {
            return null;
        }

        var joined = kind is null ? parsed : Morphology.AppendClitic(parsed, kind.Value);
        return new PhonemizedItem(PhonemizedItemKind.Word, text, joined, PronunciationSource.Override);
    }

    /// <summary>
    ///     Appends the sound of a contraction or possessive ending to an IPA pronunciation, by the same sound rules as the ARPAbet morphology.
    /// </summary>
    /// <param name="ipa">The IPA of the headword.</param>
    /// <param name="kind">The ending.</param>
    /// <returns>The IPA of the whole word, or <see langword="null"/> when the last sound of the headword is not recognized.</returns>
    /// <remarks>
    ///     The possessive/is ending is <c>ɪz</c> after a sibilant, <c>s</c> after another voiceless consonant and <c>z</c>
    ///     otherwise; the other endings are the full consonant after a vowel and are reduced with a schwa after a consonant
    ///     (<c>əd</c>, <c>əl</c>, <c>əv</c>, <c>əm</c>, and <c>ɚ</c> for <c>'re</c>, <c>ɹ</c> after a vowel).
    /// </remarks>
    private static string? AppendIpaClitic(string ipa, SuffixKind kind)
    {
        var last = ipa.TrimEnd('ː', 'ˈ', 'ˌ').LastOrDefault();
        if (last == default)
        {
            return null;
        }

        const string Vowels = "aeiouyɑɐæɒɔəɘɚɛɜɝɞɨɪɯɵøʉʊʌᵻᵿᵊ";
        const string Sibilants = "szʃʒʧʤ";
        var voiceless = new string(['p', 't', 'k', 'f', 'θ', 'χ', 'ç', 'h']);
        var vowel = Vowels.Contains(last);
        if (!vowel && !char.IsLetter(last))
        {
            return null;
        }

        var possessive = Sibilants.Contains(last) ? "ɪz" : "z";
        if (voiceless.Contains(last))
        {
            possessive = "s";
        }

        return kind switch
        {
            SuffixKind.PossessiveS => ipa + possessive,
            SuffixKind.ContractionD => ipa + (vowel ? "d" : "əd"),
            SuffixKind.ContractionLl => ipa + (vowel ? "l" : "əl"),
            SuffixKind.ContractionVe => ipa + (vowel ? "v" : "əv"),
            SuffixKind.ContractionRe => ipa + (vowel ? "ɹ" : "ɚ"),
            SuffixKind.ContractionM => ipa + (vowel ? "m" : "əm"),
            _ => null,
        };
    }

    /// <summary>
    ///     Reads the ending of a token after its annotated headword.
    /// </summary>
    /// <param name="ending">The rest of the token: an apostrophe (straight or typographic) and optionally the clitic letters.</param>
    /// <param name="kind">Receives the clitic kind, or <see langword="null"/> for the bare apostrophe of a plural possessive, which adds no sound.</param>
    /// <returns><see langword="true"/> when the ending is a plural-possessive apostrophe or a supported clitic.</returns>
    private static bool TryEnding(string ending, out SuffixKind? kind)
    {
        kind = null;
        if (ending.Length == 0 || !WordTokenizer.IsApostrophe(ending[0]))
        {
            return false;
        }

        if (ending.Length == 1)
        {
            return true;
        }

        if (TryCliticKind(ending[1..], out var found))
        {
            kind = found;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Builds a word item from a pronounced word, or an unsupported item when the word could not be pronounced.
    /// </summary>
    /// <param name="text">The spoken text.</param>
    /// <param name="pronounced">The pronounced word, or <see langword="null"/>.</param>
    /// <param name="readingId">The reading id that was requested for the word, if any.</param>
    /// <returns>The item.</returns>
    private static PhonemizedItem Item(string text, PronouncedWord? pronounced, string? readingId) =>
        pronounced is null
            ? new PhonemizedItem(PhonemizedItemKind.Unsupported, text, null, PronunciationSource.None)
            : new PhonemizedItem(PhonemizedItemKind.Word, text, pronounced.Pronunciation, pronounced.Source, readingId, elementStarts: pronounced.ElementStarts);
}
