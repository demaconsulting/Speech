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
    ///     separates it). When that is not all the remaining items, the part ends after the last clause mark in its
    ///     second half if there is one, otherwise after the last word that fits, so no text is ever dropped. The budget
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
            var end = PlanPart(pieces, start, budget);
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
    /// <param name="pieces">The rendered piece of each item.</param>
    /// <param name="start">The index of the first item of the part.</param>
    /// <param name="budget">The symbol budget of the part.</param>
    /// <returns>The index after the last item of the part; always greater than <paramref name="start"/>.</returns>
    private static int PlanPart(IReadOnlyList<string> pieces, int start, int budget)
    {
        var symbols = 0;
        var end = start;
        var lastClauseEnd = -1;
        while (end < pieces.Count)
        {
            var cost = pieces[end].EnumerateRunes().Count() + 1;
            if (symbols + cost > budget && end > start)
            {
                break;
            }

            symbols += cost;
            end++;
            if (pieces[end - 1].Length > 0 && ClauseMarks.Contains(pieces[end - 1][^1], StringComparison.Ordinal))
            {
                lastClauseEnd = end;
            }
        }

        if (end == pieces.Count)
        {
            return end;
        }

        // Prefer a clause boundary when it leaves a reasonably full part
        return lastClauseEnd > start + ((end - start) / 2) ? lastClauseEnd : end;
    }
}
