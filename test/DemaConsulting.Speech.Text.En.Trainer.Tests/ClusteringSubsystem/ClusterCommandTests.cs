using DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.ClusteringSubsystem;

/// <summary>
///     Unit tests for <see cref="ClusterCommand"/>: argument handling, the two modes' outputs, and the dispatch from the
///     trainer's command line. The network is a fake.
/// </summary>
public sealed class ClusterCommandTests
{
    /// <summary>Proves <c>--audit --write</c> writes the removals and values may be given inline.</summary>
    [Fact]
    public void ClusterCommand_TryParse_AuditWrite_WritesRemovals()
    {
        // Act
        var options = ClusterCommand.TryParse(["--audit", "--write", "--threads=3", "--cache", "x"], out _);

        // Assert
        Assert.NotNull(options);
        Assert.Equal(ClusterCommand.ClusterMode.Audit, options.Mode);
        Assert.True(options.WriteRemovals);
        Assert.Equal(3, options.Threads);
        Assert.Equal("x", options.Cache);
        Assert.Equal(2000, options.DelayMs);
    }

    /// <summary>Proves each invalid combination or value is a usage error with a reason.</summary>
    [Theory]
    [InlineData(new string[0], "exactly one")]
    [InlineData(new[] { "--fetch", "--audit" }, "exactly one")]
    [InlineData(new[] { "--write" }, "exactly one")]
    [InlineData(new[] { "--audit", "--write-pins" }, "--write-pins")]
    [InlineData(new[] { "--fetch", "--write" }, "--write is valid only with --audit")]
    [InlineData(new[] { "--fetch", "--delay-ms", "500" }, "--delay-ms")]
    [InlineData(new[] { "--audit", "--threads", "0" }, "--threads")]
    [InlineData(new[] { "--fetch", "--mirror", "www.gutenberg.org" }, "--mirror")]
    [InlineData(new[] { "--audit", "--cache" }, "needs a value")]
    [InlineData(new[] { "--audit", "--clusters", "8" }, "unknown option")]
    [InlineData(new[] { "--check" }, "unknown option")]
    [InlineData(new[] { "--bogus" }, "unknown option")]
    public void ClusterCommand_TryParse_InvalidArguments_ReportReason(string[] args, string reason)
    {
        // Act
        var options = ClusterCommand.TryParse(args, out var error);

        // Assert
        Assert.Null(options);
        Assert.Contains(reason, error, StringComparison.Ordinal);
    }

    /// <summary>Proves the retired snapshot option is refused as unknown, and the command exits 2 without reading anything.</summary>
    [Fact]
    public void ClusterCommand_TryParse_FromSnapshot_IsUnknownOption()
    {
        // Arrange
        using var fixture = new ClusteringFixture();

        // Act
        var options = ClusterCommand.TryParse(["--audit", "--from-snapshot", "corpus.tar"], out var error);
        var (exitCode, _, runError) = fixture.Run("--audit", "--from-snapshot", "corpus.tar");

        // Assert
        Assert.Null(options);
        Assert.Contains("unknown option '--from-snapshot'", error, StringComparison.Ordinal);
        Assert.Equal(2, exitCode);
        Assert.Contains("unknown option '--from-snapshot'", runError, StringComparison.Ordinal);
        Assert.Empty(fixture.Requests);
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
        Assert.Contains("--audit [--write]", help.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("--preflight", help.Output, StringComparison.Ordinal);
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
        Assert.Contains("cluster      Fetch and audit the pinned public-domain corpus", usage.ToString(), StringComparison.Ordinal);
    }

}
