using System.Globalization;
using System.Text;
using DemaConsulting.PartOfSpeech;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.NormalizationSubsystem;

namespace DemaConsulting.Speech.Text.En.Tests.Fakes;

/// <summary>
///     One row of the golden normalization file: an input text and what the normalization must
///     make of it.
/// </summary>
/// <param name="Id">The row's identifier, quoted in every failure message.</param>
/// <param name="Input">The input text (placeholders as U+FFFC).</param>
/// <param name="Spoken">The text with every normalization applied.</param>
/// <param name="Sentences">The reconciled sentences, each trimmed, joined by U+00A6.</param>
/// <param name="Ambiguous">The expected number of ambiguous resolutions.</param>
/// <param name="Fallbacks">The expected number of numeric fallbacks; 0 for a file without that column.</param>
internal sealed record NormalizationCase(string Id, string Input, string Spoken, string Sentences, int Ambiguous, int Fallbacks = 0);

/// <summary>
///     One row of the override golden file: an input text and what the dictionary must make of it.
/// </summary>
/// <param name="Id">The row's identifier, quoted in every failure message.</param>
/// <param name="Input">The input text (placeholders as U+FFFC).</param>
/// <param name="Spoken">The text with every replacement (<c>en.norm</c> and <c>en.spell</c>) applied.</param>
/// <param name="Pronunciations">The expected pronunciation marks as <c>source=value</c>, in order; empty for none.</param>
/// <param name="Spelled">The expected source text of every spelled replacement, in order; empty for none.</param>
/// <param name="Applied">The expected number of overrides applied.</param>
/// <param name="Ignored">The expected number of overrides ignored.</param>
internal sealed record OverrideCase(string Id, string Input, string Spoken, IReadOnlyList<string> Pronunciations, IReadOnlyList<string> Spelled, int Applied, int Ignored);

/// <summary>
///     Test helper that feeds the normalizer with the tokens the real tokenizer produces (no
///     tagger is involved), applies annotations independently of the production
///     <c>AlignedText</c> code, and reads the golden file.
/// </summary>
internal static class NormalizationHarness
{
    /// <summary>The separator that joins the sentences in the golden file.</summary>
    private const char SentenceSeparator = '\u00A6';

    /// <summary>The number of tab-separated columns of a golden file without the fallbacks column.</summary>
    private const int GoldenColumnCount = 6;

    /// <summary>The number of tab-separated columns of a golden file with the fallbacks column.</summary>
    private const int GoldenColumnCountWithFallbacks = 7;

    /// <summary>
    ///     Replaces every tag placeholder by a space, as the analyzer does before normalizing.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The same-length neutralized text.</returns>
    public static string Neutralize(string text) => text.Replace(TextAnalysisConventions.TagPlaceholder, ' ');

    /// <summary>
    ///     Splits a text with the real tokenizer, exactly as the analyzer feeds the normalizer.
    /// </summary>
    /// <param name="neutralizedText">The text without placeholders.</param>
    /// <returns>The tokens of every sentence in order, the first of each sentence flagged.</returns>
    public static NormalizationToken[] Tokenize(string neutralizedText)
    {
        var tokens = new List<NormalizationToken>();
        foreach (var sentence in Tokenizer.Tokenize(neutralizedText))
        {
            for (var i = 0; i < sentence.Tokens.Count; i++)
            {
                tokens.Add(new NormalizationToken(sentence.Tokens[i].Start, sentence.Tokens[i].Length, i == 0));
            }
        }

        return [.. tokens];
    }

    /// <summary>
    ///     Normalizes a text with the default lexicons, over the real tokenizer's tokens.
    /// </summary>
    /// <param name="text">The text; placeholders are neutralized first.</param>
    /// <param name="classifyClitics">Whether to classify clitics.</param>
    /// <param name="directives">User override directives resolved to token indices; none when null.</param>
    /// <returns>The aligned result over the neutralized text.</returns>
    public static AlignedText Normalize(string text, bool classifyClitics = false, TokenDirective[]? directives = null)
    {
        var neutralized = Neutralize(text);
        return TextNormalizer.Default.Normalize(neutralized, Tokenize(neutralized), classifyClitics, CancellationToken.None, directives);
    }

    /// <summary>
    ///     Resolves a dictionary against a text with the real tokenizer and normalizes the text
    ///     with the resulting directives, exactly as the analyzer does.
    /// </summary>
    /// <param name="text">The text; placeholders are neutralized first.</param>
    /// <param name="overrides">The dictionary.</param>
    /// <returns>The aligned result over the neutralized text.</returns>
    public static AlignedText NormalizeWithOverrides(string text, DemaConsulting.Speech.Text.En.AnalysisSubsystem.PronunciationOverrides overrides)
    {
        var neutralized = Neutralize(text);
        var tokens = Tokenize(neutralized);
        var directives = overrides.Resolve(neutralized, tokens, CancellationToken.None);
        return TextNormalizer.Default.Normalize(neutralized, tokens, classifyClitics: false, CancellationToken.None, directives);
    }

    /// <summary>
    ///     Gets the tokenizer's sentence breaks for a text (the offset of the first token of every
    ///     sentence after the first).
    /// </summary>
    /// <param name="neutralizedText">The text without placeholders.</param>
    /// <returns>The ascending break offsets.</returns>
    public static List<int> TokenizerBreaks(string neutralizedText)
    {
        var breaks = new List<int>();
        var first = true;
        foreach (var sentence in Tokenizer.Tokenize(neutralizedText))
        {
            if (sentence.Tokens.Count == 0)
            {
                continue;
            }

            if (!first)
            {
                breaks.Add(sentence.Tokens[0].Start);
            }

            first = false;
        }

        return breaks;
    }

    /// <summary>
    ///     Splits a text at the reconciled breaks of a normalization and trims each piece.
    /// </summary>
    /// <param name="text">The (original) text.</param>
    /// <param name="breaks">The reconciled break offsets, ascending.</param>
    /// <returns>The trimmed, non-empty sentences joined by the golden file's separator.</returns>
    public static string JoinSentences(string text, IReadOnlyList<int> breaks)
    {
        var pieces = new List<string>();
        var start = 0;
        foreach (var offset in breaks)
        {
            pieces.Add(text[start..offset].Trim());
            start = offset;
        }

        pieces.Add(text[start..].Trim());
        return string.Join(SentenceSeparator, pieces.Where(piece => piece.Length > 0));
    }

    /// <summary>
    ///     Applies the replacement annotations (<c>en.norm</c> and <c>en.spell</c>) to a text the
    ///     way a backend would, without using any production alignment code: sort by start, copy
    ///     the gaps, insert each value.
    /// </summary>
    /// <param name="text">The text the annotations index.</param>
    /// <param name="annotations">The annotations; those of other kinds are ignored.</param>
    /// <returns>The spoken text.</returns>
    /// <exception cref="InvalidOperationException">Two replacement annotations overlap or one lies outside the text.</exception>
    public static string ApplyNormalizations(string text, IEnumerable<SegmentAnnotation> annotations)
    {
        var builder = new StringBuilder();
        var position = 0;
        foreach (var annotation in annotations.Where(a => a.Kind is "en.norm" or "en.spell").OrderBy(a => a.Start))
        {
            if (annotation.Start < position || annotation.End > text.Length)
            {
                throw new InvalidOperationException($"Annotation [{annotation.Start},{annotation.End}) overlaps or is out of range.");
            }

            builder.Append(text, position, annotation.Start - position);
            builder.Append(annotation.Value);
            position = annotation.End;
        }

        builder.Append(text, position, text.Length - position);
        return builder.ToString();
    }

    /// <summary>
    ///     Reads the golden file next to the test assembly.
    /// </summary>
    /// <param name="fileName">The file name inside the <c>TestData</c> folder.</param>
    /// <returns>The rows in file order; the fallbacks column is optional (0 when the file has six columns).</returns>
    /// <exception cref="InvalidDataException">A row is malformed (the message names the line).</exception>
    public static IReadOnlyList<NormalizationCase> ReadCases(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        var cases = new List<NormalizationCase>();
        var columnCount = 0;
        var lineNumber = 0;
        foreach (var rawLine in File.ReadAllLines(path))
        {
            lineNumber++;
            if (rawLine.Length == 0 || rawLine[0] == '#')
            {
                continue;
            }

            var columns = rawLine.Split('\t');

            // The first data line is the header and fixes the column count of the file.
            if (columnCount == 0)
            {
                columnCount = columns.Length;
                if (columnCount is not (GoldenColumnCount or GoldenColumnCountWithFallbacks) || columns[0] != "id")
                {
                    throw new InvalidDataException($"{fileName} line {lineNumber}: the header must start with 'id' and have {GoldenColumnCount} or {GoldenColumnCountWithFallbacks} columns.");
                }

                continue;
            }

            if (columns.Length != columnCount)
            {
                throw new InvalidDataException($"{fileName} line {lineNumber}: expected {columnCount} columns, found {columns.Length}.");
            }

            cases.Add(new NormalizationCase(
                columns[0],
                Unescape(columns[1]),
                Unescape(columns[2]),
                Unescape(columns[3]),
                int.Parse(columns[4], CultureInfo.InvariantCulture),
                columnCount == GoldenColumnCountWithFallbacks ? int.Parse(columns[5], CultureInfo.InvariantCulture) : 0));
        }

        return cases;
    }

    /// <summary>
    ///     Reads the override golden file next to the test assembly.
    /// </summary>
    /// <param name="fileName">The file name inside the <c>TestData</c> folder.</param>
    /// <returns>The rows in file order.</returns>
    /// <exception cref="InvalidDataException">A row is malformed (the message names the line).</exception>
    public static IReadOnlyList<OverrideCase> ReadOverrideCases(string fileName)
    {
        const int columnCount = 8;
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        var cases = new List<OverrideCase>();
        var headerSeen = false;
        var lineNumber = 0;
        foreach (var rawLine in File.ReadAllLines(path))
        {
            lineNumber++;
            if (rawLine.Length == 0 || rawLine[0] == '#')
            {
                continue;
            }

            var columns = rawLine.Split('\t');
            if (columns.Length != columnCount)
            {
                throw new InvalidDataException($"{fileName} line {lineNumber}: expected {columnCount} columns, found {columns.Length}.");
            }

            if (!headerSeen)
            {
                headerSeen = true;
                continue;
            }

            cases.Add(new OverrideCase(
                columns[0],
                Unescape(columns[1]),
                Unescape(columns[2]),
                SplitList(columns[3]),
                SplitList(columns[4]),
                int.Parse(columns[5], CultureInfo.InvariantCulture),
                int.Parse(columns[6], CultureInfo.InvariantCulture)));
        }

        return cases;
    }

    /// <summary>
    ///     Splits a list cell of the override golden file at U+00A6; a lone hyphen is the empty list.
    /// </summary>
    /// <param name="cell">The cell text.</param>
    /// <returns>The items.</returns>
    private static string[] SplitList(string cell) => cell == "-" ? [] : cell.Split(SentenceSeparator);

    /// <summary>
    ///     Decodes the escapes of the golden file: <c>\t</c>, <c>\n</c>, <c>\r</c>,
    ///     <c>\uXXXX</c> and <c>\\</c>.
    /// </summary>
    /// <param name="cell">The cell text.</param>
    /// <returns>The decoded text.</returns>
    /// <exception cref="InvalidDataException">The cell holds an unknown or truncated escape.</exception>
    public static string Unescape(string cell)
    {
        var builder = new StringBuilder(cell.Length);
        var i = 0;
        while (i < cell.Length)
        {
            if (cell[i] != '\\')
            {
                builder.Append(cell[i]);
                i++;
                continue;
            }

            if (i + 1 >= cell.Length)
            {
                throw new InvalidDataException($"Dangling backslash in '{cell}'.");
            }

            var escape = cell[i + 1];
            i += 2;
            switch (escape)
            {
                case 't':
                    builder.Append('\t');
                    break;
                case 'n':
                    builder.Append('\n');
                    break;
                case 'r':
                    builder.Append('\r');
                    break;
                case '\\':
                    builder.Append('\\');
                    break;
                case 'u' when i + 4 <= cell.Length:
                    builder.Append((char)int.Parse(cell.AsSpan(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    i += 4;
                    break;
                default:
                    throw new InvalidDataException($"Unknown escape '\\{escape}' in '{cell}'.");
            }
        }

        return builder.ToString();
    }
}
