using System.Text;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;

/// <summary>
///     The text of a segment as it is spoken: the original text with each replacement annotation
///     (<c>en.norm</c>, <c>en.spell</c>) applied, remembering for every spoken character where it came from.
/// </summary>
/// <remarks>
///     <para>
///     The text analysis never rewrites text; it annotates spans with their spoken replacement. The phonemizer
///     therefore builds the spoken string itself and tokenizes <i>that</i>, so a replacement that is several words
///     (<c>$5</c> to <c>five dollars</c>) or a spelled initialism becomes ordinary tokens. For each spoken character it
///     keeps the index in the original text (or <c>-1</c> for a character a replacement introduced) and whether the
///     character belongs to a spelled replacement, so reading and pronunciation annotations, which are anchored to
///     original spans, can still be matched to the tokens that are unchanged text.
///     </para>
///     <para>
///     Replacement annotations are expected not to overlap (the text analysis guarantees it); a malformed or
///     overlapping one is ignored rather than allowed to corrupt the text. Immutable once built.
///     </para>
/// </remarks>
internal sealed class SpokenText
{
    /// <summary>The original index of each spoken character, or <c>-1</c> for replacement text.</summary>
    private readonly int[] _origin;

    /// <summary>Whether each spoken character belongs to a spelled replacement.</summary>
    private readonly bool[] _spelled;

    /// <summary>
    ///     Initializes the spoken text.
    /// </summary>
    /// <param name="text">The spoken string.</param>
    /// <param name="origin">The origin of each character.</param>
    /// <param name="spelled">The spelled flag of each character.</param>
    private SpokenText(string text, int[] origin, bool[] spelled)
    {
        Text = text;
        _origin = origin;
        _spelled = spelled;
    }

    /// <summary>Gets the spoken string.</summary>
    public string Text { get; }

    /// <summary>Gets the length of the spoken string.</summary>
    public int Length => Text.Length;

    /// <summary>
    ///     Builds the spoken text of a segment.
    /// </summary>
    /// <param name="text">The segment text.</param>
    /// <param name="annotations">The segment's annotations, or <see langword="null"/>; only the replacement kinds are used.</param>
    /// <returns>The spoken text.</returns>
    public static SpokenText Build(string text, IReadOnlyList<SegmentAnnotation>? annotations)
    {
        ArgumentNullException.ThrowIfNull(text);

        var builder = new StringBuilder(text.Length + 16);
        var origin = new List<int>(text.Length + 16);
        var spelled = new List<bool>(text.Length + 16);
        var cursor = 0;
        foreach (var (start, length, value, isSpelled) in Replacements(annotations, text.Length))
        {
            // Overlapping replacements cannot both apply: the earlier one wins
            if (start < cursor)
            {
                continue;
            }

            AppendOriginal(text, cursor, start, builder, origin, spelled);
            builder.Append(value);
            origin.AddRange(Enumerable.Repeat(-1, value.Length));
            spelled.AddRange(Enumerable.Repeat(isSpelled, value.Length));
            cursor = start + length;
        }

        AppendOriginal(text, cursor, text.Length, builder, origin, spelled);
        return new SpokenText(builder.ToString(), [.. origin], [.. spelled]);
    }

    /// <summary>
    ///     Gets the original index of a spoken character.
    /// </summary>
    /// <param name="index">The index in <see cref="Text"/>.</param>
    /// <returns>The index in the original text, or <c>-1</c> when the character came from a replacement.</returns>
    public int OriginAt(int index) => _origin[index];

    /// <summary>
    ///     Tells whether a spoken character belongs to a spelled replacement.
    /// </summary>
    /// <param name="index">The index in <see cref="Text"/>.</param>
    /// <returns><see langword="true"/> for a character of an <c>en.spell</c> replacement.</returns>
    public bool IsSpelledAt(int index) => _spelled[index];

    /// <summary>
    ///     Finds the original span of a spoken range that is unchanged text.
    /// </summary>
    /// <param name="start">The first spoken index.</param>
    /// <param name="end">One past the last spoken index.</param>
    /// <param name="sourceStart">Receives the start in the original text.</param>
    /// <returns><see langword="true"/> when every character of the range is original text, in order and without a gap.</returns>
    public bool TryGetSourceStart(int start, int end, out int sourceStart)
    {
        sourceStart = _origin[start];
        if (sourceStart < 0)
        {
            return false;
        }

        for (var i = start; i < end; i++)
        {
            if (_origin[i] != sourceStart + (i - start))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Appends a run of original text with its origins.
    /// </summary>
    /// <param name="text">The original text.</param>
    /// <param name="from">The first original index.</param>
    /// <param name="to">One past the last original index.</param>
    /// <param name="builder">Receives the characters.</param>
    /// <param name="origin">Receives the origins.</param>
    /// <param name="spelled">Receives the spelled flags (false).</param>
    private static void AppendOriginal(string text, int from, int to, StringBuilder builder, List<int> origin, List<bool> spelled)
    {
        for (var i = from; i < to; i++)
        {
            builder.Append(text[i]);
            origin.Add(i);
            spelled.Add(false);
        }
    }

    /// <summary>
    ///     Reads the valid replacement annotations in order of position.
    /// </summary>
    /// <param name="annotations">The annotations, or <see langword="null"/>.</param>
    /// <param name="textLength">The length of the original text.</param>
    /// <returns>The replacements sorted by start.</returns>
    private static IEnumerable<(int Start, int Length, string Value, bool IsSpelled)> Replacements(IReadOnlyList<SegmentAnnotation>? annotations, int textLength)
    {
        if (annotations is null)
        {
            return [];
        }

        var list = new List<(int, int, string, bool)>();
        foreach (var annotation in annotations)
        {
            if (annotation.End <= textLength && TextEnAnnotations.TryReadReplacement(annotation, out var value, out var isSpelled))
            {
                list.Add((annotation.Start, annotation.Length, value, isSpelled));
            }
        }

        return list.OrderBy(r => r.Item1).ThenBy(r => r.Item2);
    }
}
