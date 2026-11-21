using System.Text;
using System.Text.Json;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;
using static DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem.ClusterDataChecker;

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
///     final LF. The field names are the constants of <see cref="ClusterDataChecker"/>, the offline consistency check
///     that reads the record without this type, so the two cannot drift. Immutable.
/// </remarks>
/// <param name="Schema">The record schema version.</param>
/// <param name="Settings">The settings.</param>
/// <param name="Inputs">The inputs.</param>
/// <param name="Corpus">The corpus counts.</param>
/// <param name="Result">The result.</param>
internal sealed record ClusteringRecord(int Schema, ClusteringSettings Settings, ClusteringInputs Inputs, ClusteringCorpus Corpus, ClusteringResult Result)
{
    /// <summary>The file name of the record in the data folder.</summary>
    public const string FileName = ClusterDataChecker.RecordFile;

    /// <summary>The current schema version.</summary>
    public const int CurrentSchema = ClusterDataChecker.RecordSchema;

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
            json.WriteNumber(ClustererVersionField, Settings.ClustererVersion);
            json.WriteNumber(ReaderVersionField, Settings.ReaderVersion);
            json.WriteNumber(AuditVersionField, Settings.AuditVersion);
            json.WriteEndObject();
            json.WriteStartObject(InputsField);
            json.WriteString(SourcesShaField, Inputs.SourcesSha256);
            json.WriteNumber(UsedBooksField, Inputs.UsedBooks);
            json.WriteNumber(ExcludedBooksField, Inputs.ExcludedBooks);
            json.WriteString(RemovalsShaField, Inputs.RemovalsSha256);
            json.WriteString(EvaluationListingShaField, Inputs.EvaluationListingSha256);
            json.WriteString(TokenizerVersionField, Inputs.TokenizerVersion);
            json.WriteString(SnapshotShaField, Inputs.SnapshotSha256);
            json.WriteNumber(SnapshotBytesField, Inputs.SnapshotBytes);
            json.WriteNumber(SnapshotFilesField, Inputs.SnapshotFiles);
            json.WriteString(SnapshotUrlField, Inputs.SnapshotUrl);
            json.WriteEndObject();
            json.WriteStartObject(CorpusField);
            json.WriteNumber(ParagraphsField, Corpus.Paragraphs);
            json.WriteNumber(SentencesField, Corpus.Sentences);
            json.WriteNumber(TokensField, Corpus.Tokens);
            json.WriteNumber(TypesField, Corpus.Types);
            json.WriteNumber(BigramTypesField, Corpus.BigramTypes);
            json.WriteNumber(RemovedSentencesField, Corpus.RemovedSentences);
            json.WriteNumber(RemovedTokensField, Corpus.RemovedTokens);
            json.WriteNumber(MatchedEvaluationSentencesField, Corpus.MatchedEvaluationSentences);
            json.WriteNumber(BooksAffectedField, Corpus.BooksAffected);
            json.WriteEndObject();
            json.WriteStartObject(ResultField);
            json.WriteString(TableShaField, Result.TableSha256);
            json.WriteNumber(WordsField, Result.Words);
            json.WriteNumber(PathsField, Result.Paths);
            json.WriteNumber(DroppedHashCollisionsField, Result.DroppedHashCollisions);
            json.WriteNumber(DroppedIneligibleField, Result.DroppedIneligible);
            json.WriteNumber(TokenCoverageField, Result.TokenCoveragePerTenThousand);
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
                    settings.GetProperty(ClustererVersionField).GetInt32(),
                    settings.GetProperty(ReaderVersionField).GetInt32(),
                    settings.GetProperty(AuditVersionField).GetInt32()),
                new ClusteringInputs(
                    String(inputs, SourcesShaField),
                    inputs.GetProperty(UsedBooksField).GetInt32(),
                    inputs.GetProperty(ExcludedBooksField).GetInt32(),
                    String(inputs, RemovalsShaField),
                    String(inputs, EvaluationListingShaField),
                    String(inputs, TokenizerVersionField),
                    String(inputs, SnapshotShaField),
                    inputs.GetProperty(SnapshotBytesField).GetInt64(),
                    inputs.GetProperty(SnapshotFilesField).GetInt32(),
                    String(inputs, SnapshotUrlField)),
                new ClusteringCorpus(
                    corpus.GetProperty(ParagraphsField).GetInt64(),
                    corpus.GetProperty(SentencesField).GetInt64(),
                    corpus.GetProperty(TokensField).GetInt64(),
                    corpus.GetProperty(TypesField).GetInt64(),
                    corpus.GetProperty(BigramTypesField).GetInt64(),
                    corpus.GetProperty(RemovedSentencesField).GetInt64(),
                    corpus.GetProperty(RemovedTokensField).GetInt64(),
                    corpus.GetProperty(MatchedEvaluationSentencesField).GetInt64(),
                    corpus.GetProperty(BooksAffectedField).GetInt64()),
                new ClusteringResult(
                    String(result, TableShaField),
                    result.GetProperty(WordsField).GetInt64(),
                    result.GetProperty(PathsField).GetInt64(),
                    result.GetProperty(DroppedHashCollisionsField).GetInt64(),
                    result.GetProperty(DroppedIneligibleField).GetInt64(),
                    result.GetProperty(TokenCoverageField).GetInt64()));
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
