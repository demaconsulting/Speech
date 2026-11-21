using System.Text;
using System.Text.Json;

namespace DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;

/// <summary>The settings a word-cluster table was induced with.</summary>
/// <param name="Clusters">The number of clusters.</param>
/// <param name="MaxWords">The vocabulary limit, or 0 for none.</param>
/// <param name="MinCount">The smallest corpus count of a vocabulary word.</param>
/// <param name="ClustererVersion">The version of the clustering procedure.</param>
/// <param name="ReaderVersion">The version of the reading rules.</param>
/// <param name="AuditVersion">The version of the audit rules.</param>
internal sealed record ClusteringSettings(int Clusters, int MaxWords, int MinCount, int ClustererVersion, int ReaderVersion, int AuditVersion);

/// <summary>The inputs a word-cluster table was induced from, by hash.</summary>
/// <param name="SourcesSha256">The SHA-256 of <c>cluster-sources.tsv</c>.</param>
/// <param name="UsedBooks">The number of books in the corpus.</param>
/// <param name="ExcludedBooks">The number of listed books not in the corpus.</param>
/// <param name="RemovalsSha256">The SHA-256 of <c>cluster-removals.tsv</c>.</param>
/// <param name="EvaluationListingSha256">The SHA-256 of the listing of the evaluation files the audit read.</param>
/// <param name="TokenizerVersion">The version of the tokenizer package.</param>
/// <param name="SnapshotSha256">The SHA-256 of the corpus snapshot archive.</param>
/// <param name="SnapshotBytes">The size of the snapshot archive.</param>
/// <param name="SnapshotFiles">The number of files in the snapshot archive.</param>
/// <param name="SnapshotUrl">Where the snapshot is published, or empty when it is not.</param>
internal sealed record ClusteringInputs(
    string SourcesSha256,
    int UsedBooks,
    int ExcludedBooks,
    string RemovalsSha256,
    string EvaluationListingSha256,
    string TokenizerVersion,
    string SnapshotSha256,
    long SnapshotBytes,
    int SnapshotFiles,
    string SnapshotUrl);

/// <summary>The size of the counted corpus and of the audit's removals.</summary>
/// <param name="Paragraphs">The paragraphs read.</param>
/// <param name="Sentences">The sentences counted.</param>
/// <param name="Tokens">The tokens counted.</param>
/// <param name="Types">The distinct eligible forms.</param>
/// <param name="BigramTypes">The distinct bigrams of vocabulary words.</param>
/// <param name="RemovedSentences">The sentences removed by the audit.</param>
/// <param name="RemovedTokens">The tokens of the removed sentences.</param>
/// <param name="MatchedEvaluationSentences">The distinct evaluation sentences named by a removal.</param>
/// <param name="BooksAffected">The books with at least one removal.</param>
internal sealed record ClusteringCorpus(
    long Paragraphs,
    long Sentences,
    long Tokens,
    long Types,
    long BigramTypes,
    long RemovedSentences,
    long RemovedTokens,
    long MatchedEvaluationSentences,
    long BooksAffected);

/// <summary>The table that was produced.</summary>
/// <param name="TableSha256">The SHA-256 of <c>word-clusters.tsv</c>.</param>
/// <param name="Words">The number of table rows.</param>
/// <param name="Paths">The number of distinct paths.</param>
/// <param name="DroppedHashCollisions">The vocabulary words left out because their lookup hash is shared.</param>
/// <param name="DroppedIneligible">The vocabulary words left out because of an ineligible character.</param>
/// <param name="TokenCoveragePerTenThousand">The share of counted tokens covered by the vocabulary, per ten thousand.</param>
internal sealed record ClusteringResult(
    string TableSha256,
    long Words,
    long Paths,
    long DroppedHashCollisions,
    long DroppedIneligible,
    long TokenCoveragePerTenThousand);

/// <summary>
///     The record of a word-cluster table, <c>clustering.json</c>: the settings, the input hashes, the corpus counts
///     and the table hash, so the table's origin can be verified offline and its induction repeated exactly.
/// </summary>
/// <remarks>
///     Every field is an integer or a string and nothing depends on the machine, the time or the thread count, so the
///     same induction always writes the same bytes: two-space indentation, a fixed field order, LF line endings and a
///     final LF. The field names are constants so a consistency check can read them without this type. Immutable.
/// </remarks>
/// <param name="Schema">The record schema version.</param>
/// <param name="Settings">The settings.</param>
/// <param name="Inputs">The inputs.</param>
/// <param name="Corpus">The corpus counts.</param>
/// <param name="Result">The result.</param>
internal sealed record ClusteringRecord(int Schema, ClusteringSettings Settings, ClusteringInputs Inputs, ClusteringCorpus Corpus, ClusteringResult Result)
{
    /// <summary>The file name of the record in the data folder.</summary>
    public const string FileName = "clustering.json";

    /// <summary>The current schema version.</summary>
    public const int CurrentSchema = 1;

    /// <summary>The field name of the schema version.</summary>
    public const string SchemaField = "schema";

    /// <summary>The field name of the settings object.</summary>
    public const string SettingsField = "settings";

    /// <summary>The field name of the inputs object.</summary>
    public const string InputsField = "inputs";

    /// <summary>The field name of the corpus object.</summary>
    public const string CorpusField = "corpus";

    /// <summary>The field name of the result object.</summary>
    public const string ResultField = "result";

    /// <summary>The field name of the number of clusters.</summary>
    public const string ClustersField = "clusters";

    /// <summary>The field name of the vocabulary limit.</summary>
    public const string MaxWordsField = "maxWords";

    /// <summary>The field name of the minimum count.</summary>
    public const string MinCountField = "minCount";

    /// <summary>The field name of the sources hash.</summary>
    public const string SourcesShaField = "sourcesSha256";

    /// <summary>The field name of the removals hash.</summary>
    public const string RemovalsShaField = "removalsSha256";

    /// <summary>The field name of the tokenizer version.</summary>
    public const string TokenizerVersionField = "tokenizerVersion";

    /// <summary>The field name of the table hash.</summary>
    public const string TableShaField = "tableSha256";

    /// <summary>The field name of the number of table rows.</summary>
    public const string WordsField = "words";

    /// <summary>The field name of the number of distinct paths.</summary>
    public const string PathsField = "paths";

    /// <summary>
    ///     Writes the record.
    /// </summary>
    /// <returns>The UTF-8 bytes of the JSON document, ending with LF.</returns>
    public byte[] Write()
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, NewLine = "\n", IndentSize = 2 }))
        {
            json.WriteStartObject();
            json.WriteNumber(SchemaField, Schema);
            json.WriteStartObject(SettingsField);
            json.WriteNumber(ClustersField, Settings.Clusters);
            json.WriteNumber(MaxWordsField, Settings.MaxWords);
            json.WriteNumber(MinCountField, Settings.MinCount);
            json.WriteNumber("clustererVersion", Settings.ClustererVersion);
            json.WriteNumber("readerVersion", Settings.ReaderVersion);
            json.WriteNumber("auditVersion", Settings.AuditVersion);
            json.WriteEndObject();
            json.WriteStartObject(InputsField);
            json.WriteString(SourcesShaField, Inputs.SourcesSha256);
            json.WriteNumber("usedBooks", Inputs.UsedBooks);
            json.WriteNumber("excludedBooks", Inputs.ExcludedBooks);
            json.WriteString(RemovalsShaField, Inputs.RemovalsSha256);
            json.WriteString("evaluationListingSha256", Inputs.EvaluationListingSha256);
            json.WriteString(TokenizerVersionField, Inputs.TokenizerVersion);
            json.WriteString("snapshotSha256", Inputs.SnapshotSha256);
            json.WriteNumber("snapshotBytes", Inputs.SnapshotBytes);
            json.WriteNumber("snapshotFiles", Inputs.SnapshotFiles);
            json.WriteString("snapshotUrl", Inputs.SnapshotUrl);
            json.WriteEndObject();
            json.WriteStartObject(CorpusField);
            json.WriteNumber("paragraphs", Corpus.Paragraphs);
            json.WriteNumber("sentences", Corpus.Sentences);
            json.WriteNumber("tokens", Corpus.Tokens);
            json.WriteNumber("types", Corpus.Types);
            json.WriteNumber("bigramTypes", Corpus.BigramTypes);
            json.WriteNumber("removedSentences", Corpus.RemovedSentences);
            json.WriteNumber("removedTokens", Corpus.RemovedTokens);
            json.WriteNumber("matchedEvaluationSentences", Corpus.MatchedEvaluationSentences);
            json.WriteNumber("booksAffected", Corpus.BooksAffected);
            json.WriteEndObject();
            json.WriteStartObject(ResultField);
            json.WriteString(TableShaField, Result.TableSha256);
            json.WriteNumber(WordsField, Result.Words);
            json.WriteNumber(PathsField, Result.Paths);
            json.WriteNumber("droppedHashCollisions", Result.DroppedHashCollisions);
            json.WriteNumber("droppedIneligible", Result.DroppedIneligible);
            json.WriteNumber("tokenCoveragePerTenThousand", Result.TokenCoveragePerTenThousand);
            json.WriteEndObject();
            json.WriteEndObject();
        }

        buffer.WriteByte((byte)'\n');
        return buffer.ToArray();
    }

    /// <summary>
    ///     Parses a record.
    /// </summary>
    /// <param name="bytes">The UTF-8 bytes of the JSON document.</param>
    /// <returns>The record.</returns>
    /// <exception cref="InvalidDataException">The document is not valid JSON, has another schema, or a field is missing or has the wrong type.</exception>
    public static ClusteringRecord Parse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            var schema = root.GetProperty(SchemaField).GetInt32();
            if (schema != CurrentSchema)
            {
                throw new InvalidDataException($"{FileName}: schema {schema} is not {CurrentSchema}");
            }

            var settings = root.GetProperty(SettingsField);
            var inputs = root.GetProperty(InputsField);
            var corpus = root.GetProperty(CorpusField);
            var result = root.GetProperty(ResultField);
            return new ClusteringRecord(
                schema,
                new ClusteringSettings(
                    settings.GetProperty(ClustersField).GetInt32(),
                    settings.GetProperty(MaxWordsField).GetInt32(),
                    settings.GetProperty(MinCountField).GetInt32(),
                    settings.GetProperty("clustererVersion").GetInt32(),
                    settings.GetProperty("readerVersion").GetInt32(),
                    settings.GetProperty("auditVersion").GetInt32()),
                new ClusteringInputs(
                    String(inputs, SourcesShaField),
                    inputs.GetProperty("usedBooks").GetInt32(),
                    inputs.GetProperty("excludedBooks").GetInt32(),
                    String(inputs, RemovalsShaField),
                    String(inputs, "evaluationListingSha256"),
                    String(inputs, TokenizerVersionField),
                    String(inputs, "snapshotSha256"),
                    inputs.GetProperty("snapshotBytes").GetInt64(),
                    inputs.GetProperty("snapshotFiles").GetInt32(),
                    String(inputs, "snapshotUrl")),
                new ClusteringCorpus(
                    corpus.GetProperty("paragraphs").GetInt64(),
                    corpus.GetProperty("sentences").GetInt64(),
                    corpus.GetProperty("tokens").GetInt64(),
                    corpus.GetProperty("types").GetInt64(),
                    corpus.GetProperty("bigramTypes").GetInt64(),
                    corpus.GetProperty("removedSentences").GetInt64(),
                    corpus.GetProperty("removedTokens").GetInt64(),
                    corpus.GetProperty("matchedEvaluationSentences").GetInt64(),
                    corpus.GetProperty("booksAffected").GetInt64()),
                new ClusteringResult(
                    String(result, TableShaField),
                    result.GetProperty(WordsField).GetInt64(),
                    result.GetProperty(PathsField).GetInt64(),
                    result.GetProperty("droppedHashCollisions").GetInt64(),
                    result.GetProperty("droppedIneligible").GetInt64(),
                    result.GetProperty("tokenCoveragePerTenThousand").GetInt64()));
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{FileName}: {exception.Message}");
        }
    }

    /// <summary>
    ///     Reads a required string property.
    /// </summary>
    /// <param name="element">The object.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The string.</returns>
    private static string String(JsonElement element, string name) =>
        element.GetProperty(name).GetString() ?? throw new InvalidDataException($"{FileName}: {name} is null");

    /// <summary>
    ///     Gets the UTF-8 text of the record, for messages and comparisons.
    /// </summary>
    /// <returns>The JSON text.</returns>
    public string ToText() => Encoding.UTF8.GetString(Write());
}
