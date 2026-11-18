using System.Globalization;
using System.Text;

namespace DemaConsulting.Speech.Text.En.Trainer.ExternalEvaluationSubsystem;

/// <summary>
///     Reads the pinned Google WikipediaHomographData files into external records.
/// </summary>
/// <remarks>
///     <para>
///     Every <c>data/&lt;split&gt;/*.tsv</c> file has the header <c>homograph, wordid, sentence, start, end</c> with
///     CSV-style quoted fields (a quote inside a field is doubled) separated by tabs; <c>start</c> and <c>end</c> are
///     UTF-8 <em>byte</em> offsets (first byte inclusive, last byte exclusive). They are converted to UTF-16 offsets. A row
///     whose converted span is not the row's homograph (ignoring case) is skipped as <c>bad-offsets</c>, never
///     guessed. The label of a row is its <c>wordid</c>; the stratum is <c>whd-train</c> or <c>whd-eval</c>. Only
///     pinned files are read, through the verified cache, in ordinal path order. A malformed file stops the read with an
///     <see cref="InvalidDataException"/> that names the file and the record number and never quotes content. Stateless.
///     </para>
/// </remarks>
internal static class WikipediaHomographAdapter
{
    /// <summary>The source id in the pins.</summary>
    public const string SourceId = "whd";

    /// <summary>The expected header fields.</summary>
    private static readonly string[] Header = ["homograph", "wordid", "sentence", "start", "end"];

    /// <summary>
    ///     Reads one split of the dataset.
    /// </summary>
    /// <param name="cache">The verified cache.</param>
    /// <param name="source">The pinned source.</param>
    /// <param name="split">The split: <c>train</c> or <c>eval</c>.</param>
    /// <returns>The records and the adapter's accounting.</returns>
    /// <exception cref="InvalidDataException">A file is malformed, including an empty file that lacks the header.</exception>
    public static ExternalReadResult ReadSplit(ExternalSourceCache cache, ExternalSourcePin source, string split)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(split);
        var prefix = $"data/{split}/";
        var stratum = "whd-" + split;
        var records = new List<ExternalRecord>();
        var badOffsets = 0;
        var nonAscii = 0;
        var converted = 0;
        foreach (var path in source.Files.Select(static file => file.Path).Where(path => path.StartsWith(prefix, StringComparison.Ordinal) && path.EndsWith(".tsv", StringComparison.Ordinal)))
        {
            var text = Decode(cache.ReadAllBytes(source, path), path);
            var recordNumber = 0;
            var sawHeader = false;
            foreach (var fields in ParseRecords(text, path))
            {
                if (recordNumber++ == 0)
                {
                    if (!fields.SequenceEqual(Header, StringComparer.Ordinal))
                    {
                        throw new InvalidDataException($"{path}: the header is not the expected five fields");
                    }

                    sawHeader = true;
                    continue;
                }

                if (fields.Count != 5
                    || !int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var byteStart)
                    || !int.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture, out var byteEnd))
                {
                    throw new InvalidDataException($"{path}: record {recordNumber} does not have five fields with integer offsets");
                }

                var sentence = fields[2];
                nonAscii += ByteOffsets.HasNonAscii(sentence) ? 1 : 0;
                if (!ByteOffsets.TryToCharOffsets(sentence, byteStart, byteEnd, out var start, out var end)
                    || !string.Equals(sentence.AsSpan(start, end - start).ToString(), fields[0], StringComparison.OrdinalIgnoreCase))
                {
                    badOffsets++;
                    continue;
                }

                converted += start != byteStart || end != byteEnd ? 1 : 0;
                records.Add(new ExternalRecord($"{stratum}:{path}:{recordNumber}", fields[1], sentence, start, end, stratum));
            }

            // An empty file (or one of blank lines) has no records at all, so the header check above never ran.
            if (!sawHeader)
            {
                throw new InvalidDataException($"{path}: the file is empty; the expected five-field header is missing");
            }
        }

        var skipped = new SortedDictionary<string, int>(StringComparer.Ordinal) { [SkipReasons.BadOffsets] = badOffsets };
        return new ExternalReadResult(records, skipped, nonAscii, converted, 0);
    }

    /// <summary>
    ///     Decodes a file strictly as UTF-8.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="path">The pinned path, for the error message.</param>
    /// <returns>The text.</returns>
    private static string Decode(byte[] bytes, string path)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidDataException($"{path}: the file is not valid UTF-8");
        }
    }

    /// <summary>
    ///     Splits a text into records of tab-separated fields; a quoted field may hold tabs, newlines and doubled quotes.
    /// </summary>
    /// <param name="text">The file text.</param>
    /// <param name="path">The pinned path, for the error message.</param>
    /// <returns>The records, each a list of unquoted fields; blank lines are skipped.</returns>
    /// <exception cref="InvalidDataException">A quote is not closed, or a closing quote is followed by something other than a separator.</exception>
    private static IEnumerable<List<string>> ParseRecords(string text, string path)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var position = 0;
        var recordHasContent = false;
        while (position < text.Length)
        {
            var c = text[position];
            if (c == '"' && field.Length == 0)
            {
                position = ReadQuoted(text, position, field, path);
                recordHasContent = true;
                if (position < text.Length && text[position] is not ('\t' or '\n' or '\r'))
                {
                    throw new InvalidDataException($"{path}: a closing quote is followed by an unexpected character");
                }

                continue;
            }

            switch (c)
            {
                case '\t':
                    fields.Add(field.ToString());
                    field.Clear();
                    recordHasContent = true;
                    position++;
                    break;
                case '\r' when position + 1 < text.Length && text[position + 1] == '\n':
                    position++;
                    break;
                case '\n':
                    if (recordHasContent || field.Length > 0)
                    {
                        fields.Add(field.ToString());
                        yield return fields;
                    }

                    fields = [];
                    field.Clear();
                    recordHasContent = false;
                    position++;
                    break;
                default:
                    field.Append(c);
                    recordHasContent = true;
                    position++;
                    break;
            }
        }

        if (recordHasContent || field.Length > 0)
        {
            fields.Add(field.ToString());
            yield return fields;
        }
    }

    /// <summary>
    ///     Reads a quoted field starting at its opening quote.
    /// </summary>
    /// <param name="text">The file text.</param>
    /// <param name="position">The position of the opening quote.</param>
    /// <param name="field">Receives the unquoted content.</param>
    /// <param name="path">The pinned path, for the error message.</param>
    /// <returns>The position just after the closing quote.</returns>
    private static int ReadQuoted(string text, int position, StringBuilder field, string path)
    {
        position++;
        while (position < text.Length)
        {
            if (text[position] != '"')
            {
                field.Append(text[position++]);
                continue;
            }

            if (position + 1 < text.Length && text[position + 1] == '"')
            {
                field.Append('"');
                position += 2;
                continue;
            }

            return position + 1;
        }

        throw new InvalidDataException($"{path}: a quoted field is not closed");
    }
}
