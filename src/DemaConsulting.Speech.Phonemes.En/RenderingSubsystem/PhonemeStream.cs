using System.Text;
using DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.RenderingSubsystem;

/// <summary>
///     The layout both renderers share: words are separated by one space, a closing punctuation mark attaches to
///     the word before it, and an opening mark (an opening quote or bracket) attaches to the word after it.
/// </summary>
internal static class PhonemeStream
{
    /// <summary>The smallest <c>maxSymbols</c> a caller may ask for: below it the reserve would leave no budget at all.</summary>
    public const int MinPartSymbols = 16;

    /// <summary>The symbols kept free in every part for the edge effects of rendering a run of items (see <see cref="Split"/>).</summary>
    private const int PartReserve = 10;

    /// <summary>The marks after which a long text is preferably split.</summary>
    private const string ClauseMarks = ",;:.!?—…";

    /// <summary>The words that start a clause: a long text may be split before one of them.</summary>
    private static readonly HashSet<string> Conjunctions = new(
        [
            "and", "but", "or", "nor", "yet", "so", "that", "which", "who", "whom", "whose", "because", "although", "though",
            "while", "whereas", "when", "whenever", "where", "wherever", "if", "unless", "until", "since", "than", "whether",
        ],
        StringComparer.Ordinal);

    /// <summary>The function words of the renderers' weak-form table (articles, prepositions, auxiliaries, pronouns): a long text is not split after one.</summary>
    private static readonly Lazy<HashSet<string>> FunctionWords = new(
        () => new HashSet<string>(WeakForms.Words, StringComparer.Ordinal),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    ///     Joins the rendered pieces of a sequence of items.
    /// </summary>
    /// <param name="pieces">The rendered text of every item (an empty string for an item that is dropped).</param>
    /// <param name="items">The items, parallel to <paramref name="pieces"/>.</param>
    /// <param name="openingMarks">The punctuation characters that open a phrase (no space after them).</param>
    /// <param name="standaloneMarks">The punctuation characters that stand between spaces on both sides (a dash).</param>
    /// <param name="attached">For each item whether it is written without a space before it (a word that forms one group with the word before it); <see langword="null"/> when none is.</param>
    /// <returns>The joined phoneme string.</returns>
    public static string Join(IReadOnlyList<string> pieces, IReadOnlyList<PhonemizedItem> items, string openingMarks, string standaloneMarks = "", IReadOnlyList<bool>? attached = null)
    {
        var builder = new StringBuilder();
        var afterOpening = false;
        for (var i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            if (piece.Length == 0)
            {
                continue;
            }

            if (items[i].Kind == PhonemizedItemKind.Punctuation)
            {
                var opening = openingMarks.Contains(piece[0], StringComparison.Ordinal);
                if ((opening || standaloneMarks.Contains(piece[0], StringComparison.Ordinal)) && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(piece);
                afterOpening = opening;
                continue;
            }

            if (builder.Length > 0 && !afterOpening && !(attached is not null && attached[i]))
            {
                builder.Append(' ');
            }

            builder.Append(piece);
            afterOpening = false;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Gets whether the first phone of the word an item holds is a vowel (the weak form of <c>the</c> depends on it).
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="index">The index of the item after the word in question.</param>
    /// <returns><see langword="true"/> when the item at <paramref name="index"/> is a word that starts with a vowel.</returns>
    public static bool StartsWithVowel(IReadOnlyList<PhonemizedItem> items, int index)
    {
        if (index >= items.Count)
        {
            return false;
        }

        var item = items[index];
        return item.Kind == PhonemizedItemKind.Word && item.Pronunciation.Count > 0 && item.Pronunciation[0].IsVowel;
    }

    /// <summary>
    ///     Gets the text of the word an item holds (some function words are rendered differently before a particular word).
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="index">The index of the item after the word in question.</param>
    /// <returns>The text of the item at <paramref name="index"/> when it is a word; otherwise <see langword="null"/>.</returns>
    public static string? NextWordText(IReadOnlyList<PhonemizedItem> items, int index) =>
        index < items.Count && items[index].Kind == PhonemizedItemKind.Word ? items[index].Text : null;

    /// <summary>
    ///     Gets the text of the word immediately before an item (a function word is rendered in its full form after some words).
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="index">The index of the item before the word in question.</param>
    /// <returns>The text of the item at <paramref name="index"/> when it exists and is a word; otherwise <see langword="null"/>.</returns>
    public static string? PreviousWordText(IReadOnlyList<PhonemizedItem> items, int index) =>
        index >= 0 && items[index].Kind == PhonemizedItemKind.Word ? items[index].Text : null;

    /// <summary>
    ///     Splits a long text between items into parts that each render to at most a given number of symbols.
    /// </summary>
    /// <param name="items">The items of the whole text.</param>
    /// <param name="pieces">The rendered piece of every item, parallel to <paramref name="items"/>.</param>
    /// <param name="maxSymbols">The largest number of symbols (Unicode scalar values) a part may render to.</param>
    /// <param name="render">Renders a run of items to the phoneme string of one part.</param>
    /// <returns>The rendered parts in speaking order; empty parts are omitted.</returns>
    /// <remarks>
    ///     Each part takes as many items as fit its budget (the symbols of the piece plus one for the space that
    ///     separates it). When that is not all the remaining items, the part ends at the best phrase boundary among them
    ///     (after a clause mark, else before a conjunction, else after a word that is not a function word; see
    ///     <see cref="PlanPart"/>), so no text is ever dropped. The budget
    ///     is <see cref="PartReserve"/> symbols below <paramref name="maxSymbols"/> because rendering a run of items
    ///     can differ from the pieces by a few symbols at its edges (a linking <c>ɹ</c>, a different weak form of
    ///     <c>the</c>), and a part must never exceed the limit. A single item longer than the budget is still one part.
    /// </remarks>
    public static IReadOnlyList<string> Split(
        IReadOnlyList<PhonemizedItem> items,
        IReadOnlyList<string> pieces,
        int maxSymbols,
        Func<IReadOnlyList<PhonemizedItem>, string> render)
    {
        var budget = maxSymbols - PartReserve;
        var parts = new List<string>();
        var start = 0;
        while (start < items.Count)
        {
            var end = PlanPart(items, pieces, start, budget);
            var part = render(start == 0 && end == items.Count ? items : items.Skip(start).Take(end - start).ToList());
            if (part.Length > 0)
            {
                parts.Add(part);
            }

            start = end;
        }

        return parts;
    }

    /// <summary>
    ///     Chooses where the part that starts at an item ends.
    /// </summary>
    /// <param name="items">The items of the whole text.</param>
    /// <param name="pieces">The rendered piece of each item.</param>
    /// <param name="start">The index of the first item of the part.</param>
    /// <param name="budget">The symbol budget of the part.</param>
    /// <returns>The index after the last item of the part; always greater than <paramref name="start"/>.</returns>
    /// <remarks>
    ///     Among the items that fit the budget the part ends, in this order of preference, after the last clause mark
    ///     (when the part holds at least a fifth of the budget), before the last conjunction or relative word (at
    ///     least two fifths), after the last word that is not a function word (at least half), and otherwise after
    ///     the last item that fits. A part is never ended between a function word and the word it belongs to
    ///     (<i>of | devotion</i>) when a boundary that is not one is within reach.
    /// </remarks>
    private static int PlanPart(IReadOnlyList<PhonemizedItem> items, IReadOnlyList<string> pieces, int start, int budget)
    {
        var symbols = 0;
        var end = start;
        var clause = -1;
        var conjunction = -1;
        var phrase = -1;
        while (end < pieces.Count)
        {
            var cost = pieces[end].EnumerateRunes().Count() + 1;
            if (symbols + cost > budget && end > start)
            {
                break;
            }

            symbols += cost;
            end++;
            if (end >= pieces.Count)
            {
                continue;
            }

            // A boundary after the item just added: the next item must be a word or a mark that has a sound
            var last = items[end - 1];
            if (pieces[end - 1].Length > 0 && ClauseMarks.Contains(pieces[end - 1][^1], StringComparison.Ordinal) && symbols * 5 >= budget)
            {
                clause = end;
            }

            if (last.Kind != PhonemizedItemKind.Word || items[end].Kind != PhonemizedItemKind.Word || IsFunctionWord(last.Text))
            {
                continue;
            }

            if (symbols * 5 >= budget * 2 && Conjunctions.Contains(items[end].Text.ToLowerInvariant()))
            {
                conjunction = end;
            }

            if (symbols * 2 >= budget)
            {
                phrase = end;
            }
        }

        if (end == pieces.Count)
        {
            return end;
        }

        return new[] { clause, conjunction, phrase }.FirstOrDefault(boundary => boundary > start, end);
    }

    /// <summary>
    ///     Tests whether a word needs the word after it (an article, preposition, auxiliary, pronoun or conjunction).
    /// </summary>
    /// <param name="text">The text of the word.</param>
    /// <returns><see langword="true"/> for a word of the weak-form table (the function words of the renderers) or a conjunction.</returns>
    private static bool IsFunctionWord(string text)
    {
        var word = text.ToLowerInvariant();
        return FunctionWords.Value.Contains(word) || Conjunctions.Contains(word);
    }
}
