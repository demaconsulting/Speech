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
///     space), so a row is always exactly one line. A field containing a double quote is wrapped in
///     double quotes with embedded quotes doubled, and a field starting with <c>=</c>, <c>+</c>,
///     <c>-</c>, or <c>@</c> gets a leading apostrophe so a spreadsheet does not evaluate it.
///     Numbers use invariant culture. The <c>sentence_index</c> column is one-based, like the sentence
///     numbers the window shows. Stateless and thread-safe.
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
                (flag.SentenceIndex + 1).ToString(CultureInfo.InvariantCulture),
            ];

            builder.Append(string.Join('\t', fields.Select(Escape))).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Makes a field safe for one TSV cell: one line, quoted when it contains a double quote,
    ///     and not evaluable as a spreadsheet formula.
    /// </summary>
    /// <param name="value">The field value.</param>
    /// <returns>The escaped field.</returns>
    private static string Escape(string value)
    {
        // Each run of tabs and line breaks becomes one space, so a row stays one line.
        var flat = ControlRunRegex().Replace(value, " ");

        // The formula guard comes first so it composes with quoting: a spreadsheet strips the
        // quotes and would otherwise still see a leading formula character.
        if (flat.Length > 0 && flat[0] is '=' or '+' or '-' or '@')
        {
            flat = "'" + flat;
        }

        return flat.Contains('"') ? string.Concat("\"", flat.Replace("\"", "\"\""), "\"") : flat;
    }

    /// <summary>Matches a run of tab, CR, and LF characters.</summary>
    [GeneratedRegex(@"[\t\r\n]+")]
    private static partial Regex ControlRunRegex();
}
