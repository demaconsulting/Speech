using DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.ClusteringSubsystem;

/// <summary>
///     Unit tests for <see cref="ClusterCommand"/>: argument handling, the modes' preconditions and outputs, and the
///     dispatch from the trainer's command line. The network is a fake.
/// </summary>
public sealed class ClusterCommandTests
{
    /// <summary>Proves the defaults: 512 clusters at run time, the processor count of threads and a 2,000 ms delay.</summary>
    [Fact]
    public void ClusterCommand_TryParse_Write_UsesDefaults()
    {
        // Act
        var options = ClusterCommand.TryParse(["--write"], out var error);

        // Assert
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal(ClusterCommand.ClusterMode.Write, options.Mode);
        Assert.Null(options.Clusters);
        Assert.Equal(512, ClusterCommand.DefaultClusters);
        Assert.Equal(Environment.ProcessorCount, options.Threads);
        Assert.Equal(2000, options.DelayMs);
    }

    /// <summary>Proves <c>--audit --write</c> writes the removals rather than the table, and values may be given inline.</summary>
    [Fact]
    public void ClusterCommand_TryParse_AuditWrite_WritesRemovals()
    {
        // Act
        var options = ClusterCommand.TryParse(["--audit", "--write", "--threads=3", "--out", "x"], out _);

        // Assert
        Assert.NotNull(options);
        Assert.Equal(ClusterCommand.ClusterMode.Audit, options.Mode);
        Assert.True(options.WriteRemovals);
        Assert.Equal(3, options.Threads);
        Assert.Equal("x", options.Out);
    }

    /// <summary>Proves each invalid combination or value is a usage error with a reason.</summary>
    [Theory]
    [InlineData(new string[0], "exactly one")]
    [InlineData(new[] { "--fetch", "--check" }, "exactly one")]
    [InlineData(new[] { "--write", "--write-pins" }, "--write-pins")]
    [InlineData(new[] { "--audit", "--words", "a" }, "--words")]
    [InlineData(new[] { "--check", "--clusters", "8" }, "--clusters")]
    [InlineData(new[] { "--check", "--min-count", "2" }, "--min-count")]
    [InlineData(new[] { "--fetch", "--delay-ms", "500" }, "--delay-ms")]
    [InlineData(new[] { "--write", "--threads", "0" }, "--threads")]
    [InlineData(new[] { "--write", "--clusters", "1" }, "--clusters")]
    [InlineData(new[] { "--fetch", "--mirror", "www.gutenberg.org" }, "--mirror")]
    [InlineData(new[] { "--write", "--out" }, "needs a value")]
    [InlineData(new[] { "--bogus" }, "unknown option")]
    public void ClusterCommand_TryParse_InvalidArguments_ReportReason(string[] args, string reason)
    {
        // Act
        var options = ClusterCommand.TryParse(args, out var error);

        // Assert
        Assert.Null(options);
        Assert.Contains(reason, error, StringComparison.Ordinal);
    }

    /// <summary>Proves help prints the usage with exit 0 and a usage error exits 2 on the error writer.</summary>
    [Fact]
    public void ClusterCommand_Run_HelpAndUsageError()
    {
        // Arrange
        using var fixture = new ClusteringFixture();

        // Act
        var help = fixture.Run("--help");
        var bad = fixture.Run("--fetch", "--audit");

        // Assert
        Assert.Equal(0, help.ExitCode);
        Assert.Contains("--preflight", help.Output, StringComparison.Ordinal);
        Assert.Equal(2, bad.ExitCode);
        Assert.Contains("exactly one", bad.Error, StringComparison.Ordinal);
    }

    /// <summary>Proves a cache inside the repository but outside the ignored external folder is refused.</summary>
    [Fact]
    public void ClusterCommand_Run_CacheInsideRepository_IsRefused()
    {
        // Arrange
        using var fixture = new ClusteringFixture();

        // Act
        var (exitCode, _, error) = fixture.Run("--fetch", "--cache", Path.Combine(fixture.BaseDirectory, "src", "cache"));

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("outside the repository", error, StringComparison.Ordinal);
        Assert.Empty(fixture.Requests);
    }

    /// <summary>Proves <c>--fetch --write-pins</c> records each body pin in the source list, with the requested delay.</summary>
    [Fact]
    public void ClusterCommand_Run_FetchWritePins_RecordsPins()
    {
        // Arrange
        using var fixture = new ClusteringFixture();

        // Act
        var (exitCode, output, _) = fixture.Run("--fetch", "--write-pins", "--delay-ms", "1500");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("wrote 3 new pins", output, StringComparison.Ordinal);
        Assert.All(fixture.Sleeps, static sleep => Assert.Equal(TimeSpan.FromMilliseconds(1500), sleep));
        var sources = ClusterSourceList.Load(fixture.Data.PathOf(ClusterSourceList.FileName));
        var body = GutenbergBody.Extract(ClusteringFixture.RawBook(ClusteringFixture.BookBody(202)));
        var row = sources.Sources.Single(static source => source.Id == "gutenberg-202");
        Assert.Equal((body.Sha256, (long?)body.Bytes.LongLength), (row.BodySha256, row.BodyBytes));
    }

    /// <summary>Proves <c>--fetch</c> without <c>--write-pins</c> leaves the list unchanged and says so; a failed book exits 1.</summary>
    [Fact]
    public void ClusterCommand_Run_FetchWithoutPins_LeavesListAndReportsFailure()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        var before = fixture.Data.Read(ClusterSourceList.FileName);
        fixture.Mirror.Remove(ClusteringFixture.TextUrl(303));

        // Act
        var (exitCode, output, _) = fixture.Run("--fetch", "--delay-ms", "1000");

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("1 failed", output, StringComparison.Ordinal);
        Assert.Contains("run with --write-pins", output, StringComparison.Ordinal);
        Assert.Equal(before, fixture.Data.Read(ClusterSourceList.FileName));
    }

    /// <summary>Proves <c>--audit</c> without <c>--write</c> exits 1 while the removals file is missing and 0 once written.</summary>
    [Fact]
    public void ClusterCommand_Run_Audit_ReportsMissingThenCurrent()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        fixture.Run("--fetch", "--write-pins", "--delay-ms", "1000");

        // Act
        var missing = fixture.Run("--audit");
        var write = fixture.Run("--audit", "--write");
        var current = fixture.Run("--audit");

        // Assert
        Assert.Equal(1, missing.ExitCode);
        Assert.Contains("is missing", missing.Output, StringComparison.Ordinal);
        Assert.Equal(0, write.ExitCode);
        Assert.Equal(0, current.ExitCode);
        Assert.Contains("dev.tsv exact: 0 corpus sentences", current.Output, StringComparison.Ordinal);
    }

    /// <summary>Proves <c>--write</c> refuses to induce while the committed removals are missing.</summary>
    [Fact]
    public void ClusterCommand_Run_WriteWithoutRemovals_Exits1()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        fixture.Run("--fetch", "--write-pins", "--delay-ms", "1000");

        // Act
        var (exitCode, output, _) = fixture.Run("--write", "--clusters", "4", "--min-count", "1");

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("cluster --audit --write", output, StringComparison.Ordinal);
        Assert.False(File.Exists(fixture.Data.PathOf(WordClusterTable.FileName)));
    }

    /// <summary>Proves <c>--preflight</c> reports the coverage per size and each word's count, rank and cut, or that it is too rare.</summary>
    [Fact]
    public void ClusterCommand_Run_Preflight_ReportsWords()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        fixture.Prepare();

        // Act
        var (exitCode, output, _) = fixture.Run("--preflight", "--words", "the,zebra", "--min-count", "1");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("size 30000: token coverage 100.00%", output, StringComparison.Ordinal);
        Assert.Contains("word the: count 180, rank 1, 30000:in 40000:in 50000:in 60000:in 75000:in", output, StringComparison.Ordinal);
        Assert.Contains("word zebra: count 0, too rare", output, StringComparison.Ordinal);
    }

    /// <summary>Proves <c>--check</c> without a record is a usage error.</summary>
    [Fact]
    public void ClusterCommand_Run_CheckWithoutRecord_Exits2()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        fixture.Prepare();

        // Act
        var (exitCode, _, error) = fixture.Run("--check");

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("cluster --write", error, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves <c>--write</c> writes the table and the record and prints progress, timings and hashes, and that
    ///     <c>--check</c> then reports current (exit 0) and, after the record is edited, stale (exit 1).
    /// </summary>
    [Fact]
    public void ClusterCommand_Run_WriteThenCheck_CurrentThenStale()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        fixture.Prepare();

        // Act
        var write = fixture.Run("--write", "--clusters", "3", "--min-count", "2", "--max-words", "12", "--threads", "2");
        var current = fixture.Run("--check");
        var recordPath = fixture.Data.PathOf(ClusteringRecord.FileName);
        File.WriteAllText(recordPath, File.ReadAllText(recordPath).Replace("\"usedBooks\": 3", "\"usedBooks\": 4", StringComparison.Ordinal));
        var stale = fixture.Run("--check");

        // Assert
        Assert.Equal(0, write.ExitCode);
        Assert.Contains("cluster: induce: 12/12 words (100.0%)", write.Output, StringComparison.Ordinal);
        Assert.Contains("cluster: total ", write.Output, StringComparison.Ordinal);
        Assert.Contains("with 2 threads", write.Output, StringComparison.Ordinal);
        Assert.Contains("cluster: table SHA-256 ", write.Output, StringComparison.Ordinal);
        var record = ClusteringRecord.Parse(File.ReadAllBytes(recordPath));
        Assert.Equal((3, 12, 2), (record.Settings.Clusters, record.Settings.MaxWords, record.Settings.MinCount));
        Assert.Equal(0, current.ExitCode);
        Assert.Equal(1, stale.ExitCode);
        Assert.Contains("clustering.json stale", stale.Output, StringComparison.Ordinal);
    }

    /// <summary>Proves <c>--check</c> with a current snapshot in the cache reports it current and leaves every file of the cache as it was.</summary>
    [Fact]
    public void ClusterCommand_Run_CheckCurrentSnapshot_LeavesCacheUnchanged()
    {
        // Arrange: write, then note every cache file with its time and bytes
        using var fixture = new ClusteringFixture();
        fixture.Prepare();
        fixture.Run("--write", "--clusters", "3", "--min-count", "1");
        var before = CacheFiles(fixture);

        // Act
        var (exitCode, output, error) = fixture.Run("--check");

        // Assert
        Assert.True(exitCode == 0, output + error);
        Assert.Contains("snapshot current", output, StringComparison.Ordinal);
        Assert.Equal(before, CacheFiles(fixture));
    }

    /// <summary>
    ///     Proves <c>--check</c> reports a missing snapshot as missing and a tampered one as stale (exit 1) without
    ///     writing, repairing or leaving any file in the cache.
    /// </summary>
    [Fact]
    public void ClusterCommand_Run_CheckMissingOrTamperedSnapshot_ReportsWithoutRepairing()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        fixture.Prepare();
        fixture.Run("--write", "--clusters", "3", "--min-count", "1");
        var snapshot = Path.Combine(fixture.CacheDirectory, "cluster-corpus.tar");
        var original = File.ReadAllBytes(snapshot);

        // Act: once with the snapshot deleted, once with one byte of a body changed
        File.Delete(snapshot);
        var withoutSnapshot = CacheFiles(fixture);
        var missing = fixture.Run("--check");
        var missingAfter = CacheFiles(fixture);
        var tampered = (byte[])original.Clone();
        tampered[^(original.Length / 2)] ^= 0x01;
        File.WriteAllBytes(snapshot, tampered);
        var withTampered = CacheFiles(fixture);
        var stale = fixture.Run("--check");

        // Assert: both are reported, and the cache is exactly as before each check
        Assert.Equal(1, missing.ExitCode);
        Assert.Contains("clustering.json current, snapshot missing", missing.Output, StringComparison.Ordinal);
        Assert.Equal(withoutSnapshot, missingAfter);
        Assert.DoesNotContain(missingAfter, static line => line.StartsWith("cluster-corpus.tar", StringComparison.Ordinal));
        Assert.Equal(1, stale.ExitCode);
        Assert.Contains("clustering.json current, snapshot stale", stale.Output, StringComparison.Ordinal);
        Assert.Equal(withTampered, CacheFiles(fixture));
        Assert.Equal(tampered, File.ReadAllBytes(snapshot));
    }

    /// <summary>Proves the trainer's command line dispatches <c>cluster</c> and lists it in its usage.</summary>
    [Fact]
    public void Program_Run_Cluster_IsDispatched()
    {
        // Arrange
        var output = new StringWriter();
        var error = new StringWriter();

        // Act
        var exitCode = Program.Run(["cluster", "--help"], output, error);
        var usage = new StringWriter();
        Program.Run(["--help"], usage, error);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Usage: cluster <mode>", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("cluster      Fetch, audit and cluster", usage.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Lists every file of the fixture's cache with its content hash and last write time, to prove a run changed none.
    /// </summary>
    /// <param name="fixture">The fixture.</param>
    /// <returns>One line per file, <c>relative path|SHA-256|ticks</c>, in ordinal order.</returns>
    private static List<string> CacheFiles(ClusteringFixture fixture) =>
    [
        .. Directory.GetFiles(fixture.CacheDirectory, "*", SearchOption.AllDirectories)
            .Select(path => $"{Path.GetRelativePath(fixture.CacheDirectory, path)}|{CorpusSnapshot.FileSha256(path)}|{File.GetLastWriteTimeUtc(path).Ticks}")
            .Order(StringComparer.Ordinal),
    ];
}
