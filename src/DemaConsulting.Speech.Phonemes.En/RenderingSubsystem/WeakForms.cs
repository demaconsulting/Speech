using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;
using DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.RenderingSubsystem;

/// <summary>
///     How running English speech says the small function words when they do not carry the sentence stress: the weak
///     form of the articles and a few prepositions (<i>the</i> is <c>ðə</c>), the full vowel without the stress mark
///     for most of the others (<i>and</i> is <c>ænd</c>), and a secondary stress for the short words that are still
///     somewhat prominent (<i>but</i>, <i>not</i>, <i>how</i>).
/// </summary>
/// <remarks>
///     <para>
///     The table is authored from the standard description of English weak forms; the measurement against the speech
///     engine the Piper voice was trained on (see <c>docs/evaluation/phonemes-en/renderers.md</c>) decided which of
///     three classes a word belongs to, and the table records those classes, not engine output. Both renderers can
///     apply it: the Piper profile always does, and the Kokoro profile does when its weak forms are switched on (the
///     Kokoro pipeline does so, because that model was trained on text in which function words are reduced).
///     </para>
///     <para>
///     A word with a reduced form keeps its full dictionary form right after a word whose readings differ only by the
///     voicing of the final consonant (<i>close to</i>, <i>a house to</i>, <i>the use of</i>), because the reduced
///     vowel of the function word blurs the <c>s</c> or <c>z</c> that tells the readings apart.
///     </para>
///     <para>
///     A few words have a second form before a vowel (<i>the</i> is <c>ðɪ</c> in <i>the apple</i>, <i>to</i> is
///     <c>tʊ</c> in <i>to eat</i>). The table does not know the sense of a word, so it holds only words whose
///     unstressed use is by far the most common.
///     </para>
/// </remarks>
internal static class WeakForms
{
    /// <summary>The forms by lower-case word; a word that is only demoted has no explicit form.</summary>
    private static readonly Dictionary<string, (string BeforeConsonant, string BeforeVowel)> Forms = BuildForms();

    /// <summary>The full (not reduced) vowel symbols of the voice's table that open a weak form: a secondary stress mark goes before the first of them.</summary>
    private static readonly char[] FullVowelSymbols = ['a', 'e', 'i', 'o', 'u', 'æ', 'ɑ', 'ɒ', 'ɔ', 'ɛ', 'ɜ', 'ɪ', 'ʊ', 'ʌ'];

    /// <summary>The modal spellings of the table that are also main verbs (<i>to will</i>, <i>to can</i>), stressed when the text analysis tags them as verbs.</summary>
    private static readonly HashSet<string> MainVerbModals = new(["will", "can"], StringComparer.Ordinal);

    /// <summary>The words that keep their dictionary vowels but carry a secondary instead of a primary stress.</summary>
    private static readonly HashSet<string> Demoted = new(
        ["but", "not", "so", "how", "up", "us", "him", "me", "our", "over", "under", "down", "been", "about", "above", "before", "who", "why", "where", "while", "near", "them"],
        StringComparer.Ordinal);

    /// <summary>
    ///     The words the espeak-style voices also say with a secondary stress (their training text has them so: the
    ///     measurement against the engine showed it for every occurrence), where misaki keeps the primary stress.
    /// </summary>
    private static readonly HashSet<string> EspeakDemoted = new(
        ["made", "make", "put", "which", "what", "between", "behind", "beside", "beneath", "any", "already"],
        StringComparer.Ordinal);

    /// <summary>The words whose final <c>t</c> is a flap before a vowel-initial word in the espeak-style training text (<i>it is</i>, <i>at all</i>).</summary>
    private static readonly Dictionary<string, string> EspeakFlapForms = new(StringComparer.Ordinal)
    {
        ["it"] = "ɪɾ",
        ["at"] = "æɾ",
    };

    /// <summary>
    ///     The spellings whose pronunciations differ only by the voicing of the final consonant (<i>close</i> is
    ///     <c>kloʊz</c> as a verb and <c>kloʊs</c> as an adjective, <i>use</i>, <i>house</i>, <i>excuse</i>, <i>abuse</i>, ...),
    ///     derived from the lexicon and the reading table on first use.
    /// </summary>
    private static readonly Lazy<HashSet<string>> VoicingAlternations = new(BuildVoicingAlternations, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Gets the number of words in the table.</summary>
    public static int Count => Forms.Count + Demoted.Count;

    /// <summary>Gets every word of the table (explicit forms and demoted words).</summary>
    public static IEnumerable<string> Words => Forms.Keys.Concat(Demoted);

    /// <summary>Gets every IPA form of the table (all variants), so a test can prove they are in the voice's inventory.</summary>
    public static IEnumerable<string> AllForms => Forms.Values.SelectMany(f => new[] { f.BeforeConsonant, f.BeforeVowel }).Concat(EspeakFlapForms.Values);

    /// <summary>Gets the spellings whose pronunciations differ only by the voicing of the final consonant, which keep a following function word in its full form.</summary>
    public static IReadOnlyCollection<string> VoicingAlternatingWords => VoicingAlternations.Value;

    /// <summary>
    ///     Finds the words that are spoken together with the word before them: a short function word and the
    ///     article after it form one group (<i>of the</i> is one unit of speech), so no space separates them.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <returns>For each item whether it is written without a space before it.</returns>
    public static bool[] GroupedWithPrevious(IReadOnlyList<PhonemizedItem> items)
    {
        var grouped = new bool[items.Count];
        for (var i = 1; i < items.Count; i++)
        {
            var previous = items[i - 1];
            var current = items[i];

            // A function word that stays in its full form after a voicing-alternating word is not grouped with the article after it
            var fullForm = i >= 2 && items[i - 2].Kind == PhonemizedItemKind.Word && VoicingAlternations.Value.Contains(items[i - 2].Text.ToLowerInvariant());
            grouped[i] = !fullForm && IsWord(previous) && IsWord(current) && GroupsWithArticle(previous.Text.ToLowerInvariant(), current.Text.ToLowerInvariant());
        }

        return grouped;
    }

    /// <summary>
    ///     Tells whether a word of the table is used here as the content word of the same spelling, which is never weak.
    /// </summary>
    /// <param name="item">The word item, with the part of speech the text analysis gave it (if any).</param>
    /// <returns>
    ///     <see langword="true"/> when the word is tagged as a noun or a proper noun (<i>my will</i>, <i>the can</i>,
    ///     <i>a must</i>, <i>Will</i> as a name), or as a main verb when it is a modal spelling that is also a verb
    ///     (<i>to will</i>, <i>to can fruit</i>); <see langword="false"/> for any other tag and for an untagged word.
    /// </returns>
    /// <remarks>
    ///     The table holds the unstressed function use of each spelling. Several spellings are also content words that
    ///     carry their own stress: said with the weak form, <i>my will was all the other way</i> sounds like the
    ///     auxiliary (<c>maɪ wɪl</c>) where it is the noun (<c>maɪ wˈɪl</c>). The tag of the text analysis tells the two
    ///     apart; without a tag (text that was not analyzed) the table applies as before. An auxiliary or a function-word
    ///     tag (<c>AUX</c>, <c>ADP</c>, <c>DET</c>, <c>PRON</c>, <c>CCONJ</c> ...) keeps the weak form, and so does
    ///     <c>VERB</c> for every other word of the table, because the tagger gives the auxiliaries <i>be</i>, <i>have</i>
    ///     and <i>do</i> that tag too.
    /// </remarks>
    public static bool IsContentUse(PhonemizedItem item) => item.PartOfSpeech switch
    {
        "NOUN" or "PROPN" => true,
        "VERB" => MainVerbModals.Contains(item.Text.ToLowerInvariant()),
        _ => false,
    };

    /// <summary>
    ///     Gives the forms a function word and the article joined to it have together, written as one group.
    /// </summary>
    /// <param name="first">The function word as spoken (any case).</param>
    /// <param name="firstForm">The form rendered for it.</param>
    /// <param name="second">The article as spoken (any case).</param>
    /// <param name="secondForm">The form rendered for it.</param>
    /// <returns>
    ///     The group form of <i>of a</i>, <c>əv</c> and <c>ə</c> (<c>əvə</c>, as the espeak-style training text writes it),
    ///     when both words have their plain weak forms (<c>ʌv</c> and <c>ɐ</c>); otherwise the two forms unchanged.
    /// </returns>
    /// <remarks>
    ///     The open <c>ɐ</c> is the espeak-style symbol of a reduced vowel at the start of a word (<i>a</i>, <i>above</i>).
    ///     Joined into <c>ʌvɐ</c>, the article reads as the start of a new word after a full vowel, and the voice lengthens
    ///     the group and pauses inside it: measured with the noise off, <i>the wall of a gallery</i> is 127 ms longer
    ///     with <c>ʌvɐ</c> than with <c>əvə</c>, and over five phrases <c>ʌvɐ</c> was the longest of five spellings every time
    ///     (see <c>docs/evaluation/phonemes-en/renderers.md</c>). <i>was a</i> keeps <c>wʌzɐ</c>: the training text writes it
    ///     so and the voice does not lengthen it.
    /// </remarks>
    public static (string First, string Second) InGroup(string first, string firstForm, string second, string secondForm) =>
        first.Equals("of", StringComparison.OrdinalIgnoreCase) && second.Equals("a", StringComparison.OrdinalIgnoreCase) && firstForm == "ʌv" && secondForm == "ɐ"
            ? ("əv", "ə")
            : (firstForm, secondForm);
    /// <summary>
    ///     Gives a weak form the secondary stress it needs as the first word of a text: the mark goes before its first full vowel.
    /// </summary>
    /// <param name="word">The word as spoken (any case).</param>
    /// <param name="form">The weak form the table gave for it.</param>
    /// <returns>
    ///     The form with a secondary stress mark before its first full vowel (<c>hiː</c> becomes <c>hˌiː</c>, <c>ʃʊd</c> becomes
    ///     <c>ʃˌʊd</c>); the form itself when it has a stress mark already, when its vowels are all reduced (<c>ðə</c>, <c>ɐ</c>, <c>ɐn</c>)
    ///     or when the word is <i>the</i> or <i>for</i>.
    /// </returns>
    /// <remarks>
    ///     Each text the renderer sees is one utterance of the voice, and at the start of an utterance an unmarked full-vowel
    ///     weak form is often swallowed or heard as another word (the recognizer proxy hears <i>should</i>, <i>could</i> and <i>my</i> in
    ///     none of 24 renderings each; with the mark in 24, 19 and 16). The reduced forms stay as they are: they sound the same
    ///     either way and the reference engine says them so. <i>for</i> is the exception among the full forms: with any stress mark its
    ///     vowel is heard as <i>war</i> or <i>or</i>, so it keeps <c>fɔːɹ</c>. A form that already has a mark (<i>on</i>, <i>because</i>) is not touched.
    /// </remarks>
    public static string AtTextStart(string word, string form)
    {
        if (word.Equals("for", StringComparison.OrdinalIgnoreCase) || word.Equals("the", StringComparison.OrdinalIgnoreCase)
            || form.Contains('ˈ', StringComparison.Ordinal) || form.Contains('ˌ', StringComparison.Ordinal))
        {
            return form;
        }

        var index = form.IndexOfAny(FullVowelSymbols);
        return index < 0 ? form : form.Insert(index, "ˌ");
    }

    /// <summary>
    ///     Looks a word up.
    /// </summary>
    /// <param name="word">The word as spoken (any case).</param>
    /// <param name="nextStartsWithVowel">Whether the next word starts with a vowel.</param>
    /// <param name="nextWord">The next word as spoken (any case), or <see langword="null"/> when there is none or it is not a word.</param>
    /// <param name="previousWord">The word immediately before it (any case), or <see langword="null"/> when there is none or it is not a word.</param>
    /// <param name="espeakStyle">Whether the entries that mirror the espeak-style voices' training text (stress of a few more words, the flap of <i>it</i> and <i>at</i>) apply; misaki's profile does not use them.</param>
    /// <param name="form">Receives the explicit weak form; empty when the word is only demoted.</param>
    /// <param name="demoted">Receives whether the word keeps its dictionary vowels with a secondary stress.</param>
    /// <returns><see langword="true"/> when the word is in the table.</returns>
    /// <remarks>
    ///     A word with a reduced form (<i>to</i>, <i>a</i>, <i>the</i>, <i>and</i>, <i>of</i>, ...) keeps its full dictionary form right after a
    ///     voicing-alternating word (<i>close to</i>, <i>a house to</i>): the reduced vowel of the function word would
    ///     blur the final <c>s</c> or <c>z</c> of the word before it that tells the readings apart, so the table does not apply.
    /// </remarks>
    public static bool TryGet(string word, bool nextStartsWithVowel, string? nextWord, string? previousWord, bool espeakStyle, out string form, out bool demoted)
    {
        var key = word.ToLowerInvariant();
        demoted = Demoted.Contains(key) || (espeakStyle && EspeakDemoted.Contains(key));
        if (Forms.TryGetValue(key, out var forms))
        {
            // The full form: after a word whose last consonant carries the difference between its readings
            if (previousWord is not null && VoicingAlternations.Value.Contains(previousWord.ToLowerInvariant()))
            {
                form = string.Empty;
                demoted = false;
                return false;
            }

            form = nextStartsWithVowel ? forms.BeforeVowel : forms.BeforeConsonant;

            // The espeak-style voices flap the t of it and at before a vowel, leave on unstressed in on the, and say for the
            // as one group with the reduced r-colored vowel
            if (espeakStyle && nextStartsWithVowel && EspeakFlapForms.TryGetValue(key, out var flap))
            {
                form = flap;
            }
            else if (espeakStyle && key == "on" && string.Equals(nextWord, "the", StringComparison.OrdinalIgnoreCase))
            {
                form = "ɔn";
            }
            else if (espeakStyle && key == "for" && string.Equals(nextWord, "the", StringComparison.OrdinalIgnoreCase))
            {
                form = "fɚ";
            }

            return true;
        }

        form = string.Empty;
        return demoted;
    }

    /// <summary>
    ///     Tests whether an item is a dictionary word (the only kind the groups are made of).
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns><see langword="true"/> for a word from the lexicon.</returns>
    private static bool IsWord(PhonemizedItem item) => item.Kind == PhonemizedItemKind.Word && item.Source == PronunciationSource.Lexicon;

    /// <summary>
    ///     Tests whether a function word and the word after it form one group.
    /// </summary>
    /// <param name="first">The first word, lower case.</param>
    /// <param name="second">The second word, lower case.</param>
    /// <returns><see langword="true"/> for <i>in the</i>, <i>of the</i>, <i>on the</i>, <i>with the</i>, <i>from the</i>, <i>for the</i>, <i>that the</i>, <i>was the</i>, <i>was a</i> and <i>of a</i>.</returns>
    private static bool GroupsWithArticle(string first, string second) =>
        (second == "the" && first is "in" or "of" or "on" or "with" or "from" or "for" or "that" or "was")
        || (second == "a" && first is "was" or "of");

    /// <summary>
    ///     Builds the table of explicit forms.
    /// </summary>
    /// <returns>The table.</returns>
    private static Dictionary<string, (string, string)> BuildForms()
    {
        var forms = new Dictionary<string, (string, string)>(StringComparer.Ordinal);

        // Reduced vowel: the articles and the particle to
        Add(forms, "the", "ðə", "ðɪ");
        Add(forms, "a", "ɐ");
        Add(forms, "to", "tə", "tʊ");
        Add(forms, "into", "ˌɪntʊ");

        // Unstressed with the full vowel: conjunctions, prepositions, auxiliaries
        Add(forms, "and", "ænd");
        Add(forms, "of", "ʌv");
        Add(forms, "at", "æt");
        Add(forms, "as", "æz");
        Add(forms, "an", "ɐn");
        Add(forms, "than", "ðɐn");
        Add(forms, "from", "fɹʌm");
        Add(forms, "for", "fɔːɹ");
        Add(forms, "or", "ɔːɹ");
        Add(forms, "when", "wɛn");
        Add(forms, "through", "θɹuː");
        Add(forms, "that", "ðæt");
        Add(forms, "by", "baɪ");
        Add(forms, "if", "ɪf");
        Add(forms, "in", "ɪn");
        Add(forms, "on", "ˌɔn");
        Add(forms, "with", "wɪð");
        Add(forms, "can", "kæn");
        Add(forms, "could", "kʊd");
        Add(forms, "would", "wʊd");
        Add(forms, "should", "ʃʊd");
        Add(forms, "must", "mʌst");
        Add(forms, "shall", "ʃæl");
        Add(forms, "have", "hæv");
        Add(forms, "has", "hɐz");
        Add(forms, "had", "hæd");
        Add(forms, "was", "wʌz");
        Add(forms, "were", "wɜː");
        Add(forms, "be", "biː");
        Add(forms, "am", "æm");
        Add(forms, "are", "ɑːɹ");
        Add(forms, "is", "ɪz");
        Add(forms, "does", "dʌz");
        Add(forms, "will", "wɪl");

        // Pronouns and possessives
        Add(forms, "he", "hiː");
        Add(forms, "she", "ʃiː");
        Add(forms, "we", "wiː");
        Add(forms, "you", "juː");
        Add(forms, "they", "ðeɪ");
        Add(forms, "her", "hɜː");
        Add(forms, "his", "hɪz");
        Add(forms, "your", "jʊɹ");
        Add(forms, "their", "ðɛɹ");
        Add(forms, "there", "ðɛɹ");
        Add(forms, "my", "maɪ");
        Add(forms, "i", "aɪ");
        Add(forms, "it", "ɪt");
        Add(forms, "its", "ɪts");
        Add(forms, "it's", "ɪts");
        Add(forms, "get", "ɡɛt");
        Add(forms, "because", "bɪkˈʌz");
        Add(forms, "these", "ðiːz");
        Add(forms, "this", "ðɪs");
        return forms;
    }

    /// <summary>
    ///     Adds a word to the table.
    /// </summary>
    /// <param name="forms">The table.</param>
    /// <param name="word">The word.</param>
    /// <param name="beforeConsonant">The form before a consonant or a pause.</param>
    /// <param name="beforeVowel">The form before a vowel; the form before a consonant when omitted.</param>
    private static void Add(Dictionary<string, (string, string)> forms, string word, string beforeConsonant, string? beforeVowel = null) =>
        forms.Add(word, (beforeConsonant, beforeVowel ?? beforeConsonant));

    /// <summary>
    ///     Derives the spellings whose pronunciations differ only by the voicing of the final consonant.
    /// </summary>
    /// <remarks>
    ///     Two sources are read: the variants the dictionary lists for one spelling (<i>abuse</i>, <i>excuse</i>,
    ///     <i>diffuse</i>, <i>paths</i>) and the pronunciations of the readings of one heteronym lemma
    ///     (<i>close</i>, <i>use</i>, <i>house</i>: the reading table says which sense is which, the dictionary does
    ///     not). The function words of this table are left out: their own variants are what the table already says.
    /// </remarks>
    /// <returns>The lower-case spellings.</returns>
    private static HashSet<string> BuildVoicingAlternations()
    {
        var words = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in CmuLexicon.Default.Entries())
        {
            if (entry.Pronunciations.Count > 1 && AnyPairDiffersOnlyByFinalVoicing(entry.Pronunciations))
            {
                words.Add(entry.Word);
            }
        }

        foreach (var lemma in ReadingPronunciations.Default.Entries.GroupBy(e => ReadingPronunciations.LemmaOf(e.ReadingId), StringComparer.Ordinal))
        {
            var pronunciations = lemma.Select(e => e.Pronunciation).ToList();
            if (pronunciations.Count > 1 && AnyPairDiffersOnlyByFinalVoicing(pronunciations))
            {
                words.Add(lemma.Key);
            }
        }

        words.ExceptWith(Forms.Keys);
        words.ExceptWith(Demoted);
        return words;
    }

    /// <summary>
    ///     Tells whether two of some pronunciations differ only by the voicing of the final consonants.
    /// </summary>
    /// <param name="pronunciations">The pronunciations of one spelling.</param>
    /// <returns><see langword="true"/> when some pair has the same phones except that a fricative in the final consonant run is voiced in one and voiceless in the other.</returns>
    private static bool AnyPairDiffersOnlyByFinalVoicing(IReadOnlyList<Pronunciation> pronunciations)
    {
        for (var i = 0; i < pronunciations.Count; i++)
        {
            for (var j = i + 1; j < pronunciations.Count; j++)
            {
                if (DiffersOnlyByFinalVoicing(pronunciations[i], pronunciations[j]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Tells whether two pronunciations differ only by the voicing of the final consonants: <c>S</c>/<c>Z</c>, <c>F</c>/<c>V</c> or
    ///     <c>TH</c>/<c>DH</c> after the last vowel, and a <c>T</c>/<c>D</c> that follows such a pair (the ending of <i>used</i>).
    /// </summary>
    /// <param name="a">The first pronunciation.</param>
    /// <param name="b">The second pronunciation.</param>
    /// <returns><see langword="true"/> for such a pair.</returns>
    private static bool DiffersOnlyByFinalVoicing(Pronunciation a, Pronunciation b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        var tail = a.Count;
        while (tail > 0 && !a[tail - 1].IsVowel)
        {
            tail--;
        }

        var differs = false;
        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] == b[i])
            {
                continue;
            }

            var pair = (a[i].BaseSymbol, b[i].BaseSymbol);
            var fricative = pair is ("S", "Z") or ("Z", "S") or ("F", "V") or ("V", "F") or ("TH", "DH") or ("DH", "TH");
            var stop = pair is ("T", "D") or ("D", "T");
            if (i < tail || !(fricative || (stop && i > 0 && a[i - 1] != b[i - 1])))
            {
                return false;
            }

            differs = true;
        }

        return differs;
    }
}
