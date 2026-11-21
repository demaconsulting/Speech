using System.Text;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests;

/// <summary>
///     Subsystem tests for the ClusteringSubsystem: the units working together through the <c>cluster</c> command on a
///     fake mirror and fixture data, observed through exit codes, console output and the written files.
/// </summary>
public sealed class ClusteringSubsystemTests
{
    /// <summary>The golden table of the fixture corpus with four clusters and a minimum count of one, line by line.</summary>
    private static readonly string[] GoldenTable =
    [
        "word\tpath\tcount", ".\t01\t90", "barn\t1\t15", "cat\t1\t22", "cow\t1\t12",
        "dog\t1\t16", "eats\t01\t22", "field\t1\t14", "garden\t1\t23", "goat\t1\t7",
        "hill\t1\t18", "horse\t1\t21", "in\t001\t90", "jumps\t01\t18", "river\t1\t20",
        "runs\t01\t20", "sheep\t1\t12", "sleeps\t01\t14", "the\t000\t180", "walks\t01\t16",
    ];

    /// <summary>Proves the whole path: fetch and pin, audit, induce and write, then re-verify from the cache as current.</summary>
    [Fact]
    public void ClusteringSubsystem_Cluster_FetchAuditWriteCheck_EndToEnd()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        fixture.Prepare();

        // Act
        var write = fixture.Run("--write", "--clusters", "4", "--min-count", "1", "--threads", "2");
        var check = fixture.Run("--check", "--threads", "1");

        // Assert: both succeed, the table and record exist, the record matches the table and the snapshot
        Assert.Equal(0, write.ExitCode);
        Assert.Equal(0, check.ExitCode);
        Assert.Contains("check: word-clusters.tsv current, clustering.json current, snapshot current", check.Output, StringComparison.Ordinal);
        var table = File.ReadAllBytes(fixture.Data.PathOf(WordClusterTable.FileName));
        var record = ClusteringRecord.Parse(File.ReadAllBytes(fixture.Data.PathOf(ClusteringRecord.FileName)));
        Assert.Equal(GutenbergBody.Sha256Hex(table), record.Result.TableSha256);
        Assert.Equal(WordClusterTable.Parse(table, 4, 1).Count, record.Result.Words);
        Assert.Equal(GutenbergBody.Sha256Hex(File.ReadAllBytes(fixture.Data.PathOf(ClusterSourceList.FileName))), record.Inputs.SourcesSha256);
        Assert.Equal(CorpusSnapshot.FileSha256(Path.Combine(fixture.CacheDirectory, "cluster-corpus.tar")), record.Inputs.SnapshotSha256);
        Assert.Equal((4, 0, 1), (record.Settings.Clusters, record.Settings.MaxWords, record.Settings.MinCount));
        Assert.Equal(3, record.Inputs.UsedBooks);
        Assert.Contains("cluster: induce: ", write.Output, StringComparison.Ordinal);
    }

    /// <summary>Proves the fixture corpus gives exactly the recorded table on every platform (tokenizer, counts and clustering).</summary>
    [Fact]
    public void ClusteringSubsystem_Cluster_FixtureCorpus_GoldenTable()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        fixture.Prepare();

        // Act
        var (exitCode, _, _) = fixture.Run("--write", "--clusters", "4", "--min-count", "1", "--threads", "1");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(string.Concat(GoldenTable.Select(static line => line + "\n")), fixture.Data.Read(WordClusterTable.FileName));
    }

    /// <summary>Proves one thread and several threads write byte-identical tables and records.</summary>
    [Fact]
    public void ClusteringSubsystem_Cluster_DifferentThreadCounts_IdenticalFiles()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        fixture.Prepare();
        var one = Path.Combine(fixture.BaseDirectory, "one");
        var three = Path.Combine(fixture.BaseDirectory, "three");

        // Act
        fixture.Run("--write", "--clusters", "3", "--min-count", "1", "--threads", "1", "--out", one);
        fixture.Run("--write", "--clusters", "3", "--min-count", "1", "--threads", "3", "--out", three);

        // Assert
        Assert.Equal(File.ReadAllBytes(Path.Combine(one, WordClusterTable.FileName)), File.ReadAllBytes(Path.Combine(three, WordClusterTable.FileName)));
        Assert.Equal(File.ReadAllBytes(Path.Combine(one, ClusteringRecord.FileName)), File.ReadAllBytes(Path.Combine(three, ClusteringRecord.FileName)));
    }

    /// <summary>Proves a tampered cached body stops the induction before anything is written.</summary>
    [Fact]
    public void ClusteringSubsystem_Cluster_TamperedCache_IsRefused()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        fixture.Prepare();
        File.AppendAllText(Path.Combine(fixture.CacheDirectory, "bodies", "gutenberg-202.txt"), "extra\n");

        // Act
        var (exitCode, _, error) = fixture.Run("--write", "--clusters", "4", "--min-count", "1");

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("gutenberg-202", error, StringComparison.Ordinal);
        Assert.False(File.Exists(fixture.Data.PathOf(WordClusterTable.FileName)));
    }

    /// <summary>Proves a book quoting an evaluation sentence loses that sentence, and the removal is applied to the counts.</summary>
    [Fact]
    public void ClusteringSubsystem_Cluster_BookQuotingEvaluation_SentenceRemoved()
    {
        // Arrange: a fourth book holds a development sentence of the fixture data
        using var fixture = new ClusteringFixture();
        fixture.AddBook(404, "The dog runs in the field. The wind howled all night. The cat eats in the barn.");
        fixture.WriteSources([.. ClusteringFixture.Numbers.Append(404).Select(static number => (number, "used", (string?)null, (long?)null))]);
        fixture.Prepare();

        // Act
        var write = fixture.Run("--write", "--clusters", "4", "--min-count", "1");

        // Assert
        var removals = CorpusLeakageAudit.Parse(File.ReadAllBytes(fixture.Data.PathOf(CorpusLeakageAudit.FileName)));
        var removal = Assert.Single(removals);
        Assert.Equal(("gutenberg-404", 0, 1, "exact", "dev.tsv"), (removal.SourceId, removal.Paragraph, removal.Sentence, removal.Rule, removal.EvalFile));
        Assert.Equal(0, write.ExitCode);
        var record = ClusteringRecord.Parse(File.ReadAllBytes(fixture.Data.PathOf(ClusteringRecord.FileName)));
        Assert.Equal((1L, 6L, 1L, 1L), (record.Corpus.RemovedSentences, record.Corpus.RemovedTokens, record.Corpus.MatchedEvaluationSentences, record.Corpus.BooksAffected));
        Assert.DoesNotContain("howled", fixture.Data.Read(WordClusterTable.FileName), StringComparison.Ordinal);
    }

    /// <summary>Proves the written table's words are unique runtime keys and each word is its own runtime normalization.</summary>
    [Fact]
    public void ClusteringSubsystem_Cluster_TableWords_AreRuntimeKeys()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        fixture.Prepare();
        fixture.Run("--write", "--clusters", "4", "--min-count", "1");

        // Act
        var entries = WordClusterTable.Parse(File.ReadAllBytes(fixture.Data.PathOf(WordClusterTable.FileName)), 4, 1);

        // Assert
        Assert.Equal(entries.Count, entries.Select(static entry => FeatureHashing.Fnv1a(entry.Word)).Distinct().Count());
        Assert.All(entries, static entry => Assert.Equal(FeatureHashing.Fnv1a(entry.Word), HeteronymContext.Create(entry.Word, 0, entry.Word.Length, null, 0).LowerHash));
    }

    /// <summary>Proves <c>--check</c> reports a hand-edited table as stale.</summary>
    [Fact]
    public void ClusteringSubsystem_Cluster_EditedTable_CheckIsStale()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        fixture.Prepare();
        fixture.Run("--write", "--clusters", "4", "--min-count", "1");
        var path = fixture.Data.PathOf(WordClusterTable.FileName);
        File.WriteAllText(path, File.ReadAllText(path).Replace("\t1", "\t0", StringComparison.Ordinal), new UTF8Encoding(false));

        // Act
        var (exitCode, output, _) = fixture.Run("--check");

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("word-clusters.tsv stale", output, StringComparison.Ordinal);
    }

    /// <summary>Proves the snapshot alone reproduces the table: the cache's bodies can be deleted and <c>--check --from-snapshot</c> passes.</summary>
    [Fact]
    public void ClusteringSubsystem_Cluster_FromSnapshot_ReproducesTable()
    {
        // Arrange: write, keep the snapshot outside the cache, and delete the cache
        using var fixture = new ClusteringFixture();
        fixture.Prepare();
        fixture.Run("--write", "--clusters", "4", "--min-count", "1");
        var kept = Path.Combine(fixture.BaseDirectory, "kept.tar");
        File.Copy(Path.Combine(fixture.CacheDirectory, "cluster-corpus.tar"), kept);
        Directory.Delete(fixture.CacheDirectory, true);
        var requests = fixture.Requests.Count;

        // Act
        var (exitCode, output, error) = fixture.Run("--check", "--from-snapshot", kept);

        // Assert
        Assert.True(exitCode == 0, output + error);
        Assert.Equal(requests, fixture.Requests.Count);
    }
}
