using System.Text.Json;

namespace DemaConsulting.Speech.Text.En.Trainer.ExternalEvaluationSubsystem;

/// <summary>
///     Reads the pinned English heteronym-bench file into external records.
/// </summary>
/// <remarks>
///     <para>
///     The file is one JSON document whose <c>items</c> object maps an item id to an object with <c>text</c>,
///     <c>word</c>, <c>pos</c> (the target's character span), <c>answer</c> (the benchmark's own reading key) and
///     <c>src</c> (where the item came from). The label of an item is <c>word:answer</c>; its stratum is
///     <c>bench-&lt;src&gt;</c>. Items whose <c>src</c> is <c>whd</c> are verbatim copies of WikipediaHomographData
///     sentences, so they are skipped as <c>duplicate-of-whd</c> before anything else is looked at. When an item's
///     <c>pos</c> does not slice to its word (ignoring case) the span is read as UTF-8 byte offsets and converted; if
///     that does not give the word either, the item is skipped as <c>bad-offsets</c>. A missing or mistyped field stops
///     the read with an <see cref="InvalidDataException"/> that names the item and never quotes content. The file is read
///     through the verified cache and is limited in size. Stateless.
///     </para>
/// </remarks>
internal static class HeteronymBenchAdapter
{
    /// <summary>The source id in the pins.</summary>
    public const string SourceId = "bench";

    /// <summary>The pinned path of the English read set.</summary>
    public const string ReadSetPath = "data/readset_en_v2.json";

    /// <summary>The source value of the items copied from WikipediaHomographData.</summary>
    private const string WhdSource = "whd";

    /// <summary>The largest file accepted, in bytes.</summary>
    private const int MaximumBytes = 16 * 1024 * 1024;

    /// <summary>
    ///     Reads the English items.
    /// </summary>
    /// <param name="cache">The verified cache.</param>
    /// <param name="source">The pinned source.</param>
    /// <returns>The records and the adapter's accounting.</returns>
    /// <exception cref="InvalidDataException">The file is too large, is not the expected JSON, or an item is malformed.</exception>
    public static ExternalReadResult Read(ExternalSourceCache cache, ExternalSourcePin source)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(source);
        var bytes = cache.ReadAllBytes(source, ReadSetPath);
        if (bytes.Length > MaximumBytes)
        {
            throw new InvalidDataException($"{ReadSetPath}: the file is larger than {MaximumBytes} bytes");
        }

        try
        {
            using var document = JsonDocument.Parse(bytes);
            return ReadItems(document.RootElement);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{ReadSetPath}: not valid JSON ({exception.GetType().Name})");
        }
    }

    /// <summary>
    ///     Reads the items of the document.
    /// </summary>
    /// <param name="root">The JSON root.</param>
    /// <returns>The records and the accounting.</returns>
    private static ExternalReadResult ReadItems(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("lang", out var language)
            || language.ValueKind != JsonValueKind.String
            || language.GetString() != "en")
        {
            throw new InvalidDataException($"{ReadSetPath}: the document is not an English read set");
        }

        if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{ReadSetPath}: the document has no items object");
        }

        var records = new List<ExternalRecord>();
        var skipped = new SortedDictionary<string, int>(StringComparer.Ordinal)
        {
            [SkipReasons.BadOffsets] = 0,
            [SkipReasons.DuplicateOfWhd] = 0,
        };
        var nonAscii = 0;
        var repaired = 0;
        foreach (var property in items.EnumerateObject())
        {
            var id = property.Name;
            var item = property.Value;
            var text = StringField(item, id, "text");
            var word = StringField(item, id, "word");
            var answer = StringField(item, id, "answer");
            var src = StringField(item, id, "src");
            var (positionStart, positionEnd) = PositionField(item, id);
            nonAscii += ByteOffsets.HasNonAscii(text) ? 1 : 0;

            // Items copied from WikipediaHomographData are evaluated as part of that dataset, not again here.
            if (src == WhdSource)
            {
                skipped[SkipReasons.DuplicateOfWhd]++;
                continue;
            }

            if (!TryPlace(text, word, positionStart, positionEnd, out var start, out var end, out var wasRepaired))
            {
                skipped[SkipReasons.BadOffsets]++;
                continue;
            }

            repaired += wasRepaired ? 1 : 0;
            records.Add(new ExternalRecord(id, $"{word}:{answer}", text, start, end, "bench-" + src));
        }

        return new ExternalReadResult(records, skipped, nonAscii, 0, repaired);
    }

    /// <summary>
    ///     Places an item's span on its text as the item's word, repairing a span that is in UTF-8 bytes.
    /// </summary>
    /// <param name="text">The item text.</param>
    /// <param name="word">The item word.</param>
    /// <param name="positionStart">The first offset of <c>pos</c>.</param>
    /// <param name="positionEnd">The second offset of <c>pos</c>.</param>
    /// <param name="start">The span start in UTF-16 units.</param>
    /// <param name="end">The span end in UTF-16 units.</param>
    /// <param name="repaired">Whether the offsets had to be read as bytes.</param>
    /// <returns><see langword="true"/> when the span is the word.</returns>
    private static bool TryPlace(string text, string word, int positionStart, int positionEnd, out int start, out int end, out bool repaired)
    {
        repaired = false;
        start = positionStart;
        end = positionEnd;
        if (positionStart >= 0 && positionEnd > positionStart && positionEnd <= text.Length
            && string.Equals(text.AsSpan(positionStart, positionEnd - positionStart).ToString(), word, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        repaired = true;
        return ByteOffsets.TryToCharOffsets(text, positionStart, positionEnd, out start, out end)
            && string.Equals(text.AsSpan(start, end - start).ToString(), word, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Reads a required string field of an item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="id">The item id, for the error message.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The value.</returns>
    private static string StringField(JsonElement item, string id, string name)
    {
        if (item.ValueKind == JsonValueKind.Object
            && item.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 } text)
        {
            return text;
        }

        throw new InvalidDataException($"{ReadSetPath}: item '{id}' has no usable '{name}' field");
    }

    /// <summary>
    ///     Reads the <c>pos</c> field of an item: two integers.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="id">The item id, for the error message.</param>
    /// <returns>The two offsets.</returns>
    private static (int Start, int End) PositionField(JsonElement item, string id)
    {
        if (item.TryGetProperty("pos", out var pos)
            && pos.ValueKind == JsonValueKind.Array
            && pos.GetArrayLength() == 2
            && pos[0].ValueKind == JsonValueKind.Number
            && pos[1].ValueKind == JsonValueKind.Number
            && pos[0].TryGetInt32(out var start)
            && pos[1].TryGetInt32(out var end))
        {
            return (start, end);
        }

        throw new InvalidDataException($"{ReadSetPath}: item '{id}' has no usable 'pos' field");
    }
}
