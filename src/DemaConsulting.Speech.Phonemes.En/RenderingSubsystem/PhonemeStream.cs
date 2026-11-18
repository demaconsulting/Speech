using System.Text;
using DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.RenderingSubsystem;

/// <summary>
///     The layout both renderers share: words are separated by one space, a closing punctuation mark attaches to
///     the word before it, and an opening mark (an opening quote or bracket) attaches to the word after it.
/// </summary>
internal static class PhonemeStream
{
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
}
