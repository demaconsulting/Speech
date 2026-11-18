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
///     three classes a word belongs to, and the table records those classes, not engine output. Only the Piper
///     profile uses it: that voice was trained on text in which function words are unstressed, whereas Kokoro's full
///     forms are rendered as the dictionary has them.
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

    /// <summary>Gets the number of words in the table.</summary>
    public static int Count => Forms.Count + Demoted.Count;

    /// <summary>Gets every word of the table (explicit forms and demoted words).</summary>
    public static IEnumerable<string> Words => Forms.Keys.Concat(Demoted);

    /// <summary>Gets every IPA form of the table (all variants), so a test can prove they are in the voice's inventory.</summary>
    public static IEnumerable<string> AllForms => Forms.Values.SelectMany(f => new[] { f.BeforeConsonant, f.BeforeVowel }).Concat(EspeakFlapForms.Values);

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
            grouped[i] = IsWord(previous) && IsWord(current) && GroupsWithArticle(previous.Text.ToLowerInvariant(), current.Text.ToLowerInvariant());
        }

        return grouped;
    }

    /// <summary>
    ///     Looks a word up.
    /// </summary>
    /// <param name="word">The word as spoken (any case).</param>
    /// <param name="nextStartsWithVowel">Whether the next word starts with a vowel.</param>
    /// <param name="nextWord">The next word as spoken (any case), or <see langword="null"/> when there is none or it is not a word.</param>
    /// <param name="espeakStyle">Whether the entries that mirror the espeak-style voices' training text (stress of a few more words, the flap of <i>it</i> and <i>at</i>) apply; misaki's profile does not use them.</param>
    /// <param name="form">Receives the explicit weak form; empty when the word is only demoted.</param>
    /// <param name="demoted">Receives whether the word keeps its dictionary vowels with a secondary stress.</param>
    /// <returns><see langword="true"/> when the word is in the table.</returns>
    public static bool TryGet(string word, bool nextStartsWithVowel, string? nextWord, bool espeakStyle, out string form, out bool demoted)
    {
        var key = word.ToLowerInvariant();
        demoted = Demoted.Contains(key) || (espeakStyle && EspeakDemoted.Contains(key));
        if (Forms.TryGetValue(key, out var forms))
        {
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
}
