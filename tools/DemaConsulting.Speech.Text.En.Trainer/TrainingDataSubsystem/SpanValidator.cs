using System.Globalization;
using System.Text;

namespace DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

/// <summary>
///     Strict whole-word span validation for the <c>start</c>/<c>end</c> offsets of a data row.
/// </summary>
/// <remarks>
///     Offsets are UTF-16 code units (what a .NET <see cref="string"/> indexes), end exclusive. A span is valid
///     only when it is inside the sentence, neither edge splits a surrogate pair, the lower-cased slice (invariant
///     culture) is a listed form of the row's reading, and the characters just outside the span are not letters or
///     numbers (or the sentence edge), so the slice is a whole word and not a part of a longer one. The neighbor is
///     examined as a Unicode scalar, so a letter outside the Basic Multilingual Plane is recognized as one. Pure and
///     thread-safe.
/// </remarks>
internal static class SpanValidator
{
    /// <summary>
    ///     Checks one span.
    /// </summary>
    /// <param name="sentence">The sentence text.</param>
    /// <param name="start">The first UTF-16 code unit of the span.</param>
    /// <param name="end">The UTF-16 code unit after the last one of the span.</param>
    /// <param name="listedForms">The lower-case forms that <c>forms.tsv</c> lists for the row's reading.</param>
    /// <returns>An error description, or <see langword="null"/> when the span is a valid whole word.</returns>
    public static string? Validate(string sentence, int start, int end, IReadOnlySet<string> listedForms)
    {
        ArgumentNullException.ThrowIfNull(sentence);
        ArgumentNullException.ThrowIfNull(listedForms);

        // The span must be non-empty and inside the sentence
        if (start < 0 || start >= end || end > sentence.Length)
        {
            return $"offsets {start}..{end} are out of range for a sentence of length {sentence.Length}";
        }

        // Neither edge may fall between the two halves of a surrogate pair
        if (SplitsSurrogatePair(sentence, start) || SplitsSurrogatePair(sentence, end))
        {
            return $"offsets {start}..{end} split a surrogate pair";
        }

        // The slice must be a form the reading is allowed to have (case-insensitive)
        var slice = sentence[start..end];
        if (!listedForms.Contains(slice.ToLowerInvariant()))
        {
            return $"'{slice}' is not a listed form of the reading";
        }

        // The neighbors decide whether the slice is a whole word
        if (start > 0 && IsLetterOrNumber(ScalarEndingAt(sentence, start)))
        {
            return $"'{slice}' is preceded by a letter or digit";
        }

        if (end < sentence.Length && IsLetterOrNumber(ScalarStartingAt(sentence, end)))
        {
            return $"'{slice}' is followed by a letter or digit";
        }

        return null;
    }

    /// <summary>
    ///     Determines whether <paramref name="rune"/> is a letter or a number.
    /// </summary>
    /// <param name="rune">The scalar to classify, or <see langword="null"/> for a lone surrogate.</param>
    /// <returns><see langword="true"/> for a letter (L*) or number (N*) scalar.</returns>
    /// <remarks>
    ///     Letters plus all numbers (including superscripts such as the digit two) match Python's <c>isalnum</c>,
    ///     which the reference implementation used, and are stricter than <see cref="char.IsLetterOrDigit(char)"/>.
    /// </remarks>
    internal static bool IsLetterOrNumber(Rune? rune)
    {
        if (rune is not { } value)
        {
            return false;
        }

        var category = Rune.GetUnicodeCategory(value);
        return category is UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter
            or UnicodeCategory.DecimalDigitNumber
            or UnicodeCategory.LetterNumber
            or UnicodeCategory.OtherNumber;
    }

    /// <summary>
    ///     Determines whether <paramref name="offset"/> lies between a high and a low surrogate.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <param name="offset">The offset, in [0, length].</param>
    /// <returns><see langword="true"/> when the offset is inside a surrogate pair.</returns>
    private static bool SplitsSurrogatePair(string sentence, int offset) =>
        offset > 0
        && offset < sentence.Length
        && char.IsHighSurrogate(sentence[offset - 1])
        && char.IsLowSurrogate(sentence[offset]);

    /// <summary>
    ///     Reads the scalar that ends just before <paramref name="offset"/>.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <param name="offset">The offset, greater than zero.</param>
    /// <returns>The scalar, or <see langword="null"/> for a lone surrogate.</returns>
    private static Rune? ScalarEndingAt(string sentence, int offset)
    {
        // A low surrogate preceded by a high surrogate is one scalar that started one code unit earlier
        var index = offset >= 2 && char.IsLowSurrogate(sentence[offset - 1]) && char.IsHighSurrogate(sentence[offset - 2])
            ? offset - 2
            : offset - 1;
        return Rune.TryGetRuneAt(sentence, index, out var rune) ? rune : null;
    }

    /// <summary>
    ///     Reads the scalar that starts at <paramref name="offset"/>.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <param name="offset">The offset, less than the sentence length.</param>
    /// <returns>The scalar, or <see langword="null"/> for a lone surrogate.</returns>
    private static Rune? ScalarStartingAt(string sentence, int offset) =>
        Rune.TryGetRuneAt(sentence, offset, out var rune) ? rune : null;
}
