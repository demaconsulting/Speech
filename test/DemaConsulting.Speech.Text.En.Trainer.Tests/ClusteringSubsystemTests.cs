using DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests;

/// <summary>
///     Subsystem tests for the ClusteringSubsystem: the units working together through the <c>cluster</c> command on a
///     fake mirror and fixture data, observed through exit codes, console output and the written files.
/// </summary>
public sealed class ClusteringSubsystemTests
{
    /// <summary>Proves the whole path: fetch and pin, audit and write the removals, then re-audit from the cache as current.</summary>
    [Fact]
    public void ClusteringSubsystem_Cluster_FetchAudit_EndToEnd()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        fixture.Prepare();

        // Act
        var (exitCode, output, error) = fixture.Run("--audit", "--threads", "1");

        // Assert: the removals are current, every used book is pinned, and the re-audit made no request
        Assert.True(exitCode == 0, output + error);
        Assert.Contains("cluster: audit: cluster-removals.tsv is current", output, StringComparison.Ordinal);
        Assert.Contains("cluster: tokenized 3 books", output, StringComparison.Ordinal);
        var sources = ClusterSourceList.Load(fixture.Data.PathOf(ClusterSourceList.FileName));
        Assert.Equal(3, sources.Sources.Count(static source => source.IsUsed));
        Assert.All(sources.Sources, static source => Assert.NotNull(source.BodySha256));
        Assert.Equal(ClusteringFixture.Numbers.Length * 2, fixture.Requests.Count);
    }

    /// <summary>Proves one thread and several threads write byte-identical removals.</summary>
    [Fact]
    public void ClusteringSubsystem_Cluster_DifferentThreadCounts_IdenticalRemovals()
    {
        // Arrange: a fourth book quotes an evaluation sentence, so the removals are not empty
        using var fixture = new ClusteringFixture();
        fixture.AddBook(404, "The dog runs in the field. The wind howled all night. The cat eats in the barn.");
        fixture.WriteSources([.. ClusteringFixture.Numbers.Append(404).Select(static number => (number, "used", (string?)null, (long?)null))]);
        fixture.Prepare();
        var path = fixture.Data.PathOf(CorpusLeakageAudit.FileName);

        // Act
        fixture.Run("--audit", "--write", "--threads", "1");
        var one = File.ReadAllBytes(path);
        fixture.Run("--audit", "--write", "--threads", "3");
        var three = File.ReadAllBytes(path);

        // Assert
        Assert.Single(CorpusLeakageAudit.Parse(one));
        Assert.Equal(one, three);
    }

    /// <summary>Proves a tampered cached body stops the audit before anything is written.</summary>
    [Fact]
    public void ClusteringSubsystem_Cluster_TamperedCache_IsRefused()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        fixture.Prepare();
        var removals = fixture.Data.Read(CorpusLeakageAudit.FileName);
        File.AppendAllText(Path.Combine(fixture.CacheDirectory, "bodies", "gutenberg-202.txt"), "extra\n");

        // Act
        var (exitCode, _, error) = fixture.Run("--audit", "--write");

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("gutenberg-202", error, StringComparison.Ordinal);
        Assert.Equal(removals, fixture.Data.Read(CorpusLeakageAudit.FileName));
    }

    /// <summary>Proves a book quoting an evaluation sentence loses exactly that sentence to the removals.</summary>
    [Fact]
    public void ClusteringSubsystem_Cluster_BookQuotingEvaluation_SentenceRemoved()
    {
        // Arrange: a fourth book holds a development sentence of the fixture data
        using var fixture = new ClusteringFixture();
        fixture.AddBook(404, "The dog runs in the field. The wind howled all night. The cat eats in the barn.");
        fixture.WriteSources([.. ClusteringFixture.Numbers.Append(404).Select(static number => (number, "used", (string?)null, (long?)null))]);

        // Act
        fixture.Prepare();

        // Assert
        var removals = CorpusLeakageAudit.Parse(File.ReadAllBytes(fixture.Data.PathOf(CorpusLeakageAudit.FileName)));
        var removal = Assert.Single(removals);
        Assert.Equal(("gutenberg-404", 0, 1, "exact", "dev.tsv"), (removal.SourceId, removal.Paragraph, removal.Sentence, removal.Rule, removal.EvalFile));
    }

    /// <summary>
    ///     Proves the written pins and removals pass the offline corpus check of <c>check-data</c>, and that a used book
    ///     whose pin is blanked fails it.
    /// </summary>
    [Fact]
    public void ClusteringSubsystem_Cluster_WrittenFiles_PassOfflineDataCheck()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        fixture.Prepare();
        fixture.Data.RewriteManifest();

        // Act
        (int ExitCode, string Output) CheckData()
        {
            var output = new StringWriter();
            var exitCode = CheckDataCommand.Run(["--data", fixture.Data.Root], output, new StringWriter(), fixture.BaseDirectory, fixture.BaseDirectory, _ => null);
            return (exitCode, output.ToString());
        }

        var current = CheckData();
        fixture.Data.EditRow(ClusterSourceList.FileName, "gutenberg-101", ("body_sha256", "-"));
        fixture.Data.RewriteManifest();
        var unpinned = CheckData();

        // Assert
        Assert.True(current.ExitCode == 0, current.Output);
        Assert.Contains("word-cluster sources: 3 books, 3 used, 0 not used", current.Output, StringComparison.Ordinal);
        Assert.Equal(1, unpinned.ExitCode);
        Assert.Contains("check-data: clusters: cluster-sources.tsv:2: gutenberg-101: a used book needs a lowercase 64-digit body_sha256", unpinned.Output, StringComparison.Ordinal);
    }
}
