using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DemaConsulting.Speech.Proofreader.Session;

/// <summary>
///     Exports flags as tab-separated text for spreadsheets and for lifting rows into golden test data.
/// </summary>
/// <remarks>
///     The output is UTF-8 text with LF line endings, one header row, and one row per flag sorted by
///     sentence and then span. Fields never contain a tab or line break (each run of them becomes one
///     space), so a row is always exactly one line. Numbers use invariant culture. Stateless and
///     thread-safe.
/// </remarks>
internal static partial class TsvExporter
{
    /// <summary>The column names, in output order.</summary>
    public static IReadOnlyList<string> Columns { get; } =
        ["id", "sentence", "span", "word", "category", "note", "model", "settings", "readings", "phonemes", "sentence_index"];

    /// <summary>
    ///     Builds the TSV text for a set of flags.
    /// </summary>
    /// <param name="flags">The flags to export. Must not be <see langword="null"/>.</param>
    /// <returns>The header row and one row per flag, each terminated by LF.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="flags"/> is <see langword="null"/>.</exception>
    public static string Export(IEnumerable<ProofreadFlag> flags)
    {
        ArgumentNullException.ThrowIfNull(flags);

        var builder = new StringBuilder();
        builder.Append(string.Join('\t', Columns)).Append('\n');

        foreach (var flag in flags.OrderBy(f => f.SentenceIndex).ThenBy(f => f.SpanStart).ThenBy(f => f.Id))
        {
            // The span is sentence-relative, end exclusive, and empty when the whole sentence is flagged.
            var span = flag.HasSpan
                ? string.Create(CultureInfo.InvariantCulture, $"{flag.SpanStart}-{flag.SpanStart + flag.SpanLength}")
                : string.Empty;

            string[] fields =
            [
                flag.Id.ToString(CultureInfo.InvariantCulture),
                flag.SentenceText,
                span,
                flag.Word,
                FlagCategories.Key(flag.Category),
                flag.Note,
                flag.Model.DisplayName.Length > 0 ? flag.Model.DisplayName : flag.Model.ModelId,
                string.Join("; ", flag.Model.Parameters
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key}={Synthesis.ModelSelection.FormatValue(pair.Value)}")),
                flag.Readings,
                flag.Phonemes,
                flag.SentenceIndex.ToString(CultureInfo.InvariantCulture),
            ];

            builder.Append(string.Join('\t', fields.Select(Flatten))).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Replaces every run of tabs and line breaks with a single space.
    /// </summary>
    /// <param name="value">The field value.</param>
    /// <returns>A single-line field value.</returns>
    private static string Flatten(string value) => ControlRunRegex().Replace(value, " ");

    /// <summary>Matches a run of tab, CR, and LF characters.</summary>
    [GeneratedRegex(@"[\t\r\n]+")]
    private static partial Regex ControlRunRegex();
}
