namespace DemaConsulting.Speech.ModelBundler.Tests;

/// <summary>
///     Unit tests for <see cref="CommandLineOptions"/>: each command's options, defaults and rejected combinations.
/// </summary>
public sealed class CommandLineOptionsTests
{
    /// <summary>Validates a full build command line.</summary>
    [Fact]
    public void CommandLineOptions_Parse_BuildWithBundles_ReadsEveryOption()
    {
        // Arrange / Act: parse a build with two bundles and every option
        var options = CommandLineOptions.Parse(
            ["build", "--bundle", "a-r1-int8-b1", "--bundle", "b-r1-int4-b1", "--output", "out", "--source-store", "store", "--cache", "cache", "--skip-unavailable"]);

        // Assert: every value is read
        Assert.Equal(BundlerCommand.Build, options.Command);
        Assert.False(options.All);
        Assert.Equal(["a-r1-int8-b1", "b-r1-int4-b1"], options.Bundles);
        Assert.Equal("out", options.Output);
        Assert.Equal("store", options.SourceStore);
        Assert.Equal("cache", options.Cache);
        Assert.True(options.SkipUnavailable);
    }

    /// <summary>Validates the defaults of verify and upload.</summary>
    [Fact]
    public void CommandLineOptions_Parse_VerifyAndUpload_UseDefaults()
    {
        // Arrange / Act: parse minimal verify and upload command lines
        var verify = CommandLineOptions.Parse(["verify", "--input", "in"]);
        var upload = CommandLineOptions.Parse(["upload", "--input", "in", "--dry-run"]);

        // Assert: the default cache, repository and the dry-run flag
        Assert.Equal(CommandLineOptions.DefaultCache, verify.Cache);
        Assert.Null(verify.SourceStore);
        Assert.Equal("demaconsulting/Speech", upload.Repository);
        Assert.True(upload.DryRun);
    }

    /// <summary>Validates that invalid command lines are rejected.</summary>
    /// <param name="commandLine">The command line, space-separated.</param>
    [Theory]
    [InlineData("")]
    [InlineData("pack --all --output out")]
    [InlineData("build --output out")]
    [InlineData("build --all --bundle a-r1-int8-b1 --output out")]
    [InlineData("build --all")]
    [InlineData("build --all --output")]
    [InlineData("verify --source-store store")]
    [InlineData("upload --input in --clobber")]
    [InlineData("verify --input in --dry-run")]
    public void CommandLineOptions_Parse_InvalidCommandLine_ThrowsArgumentException(string commandLine)
    {
        // Arrange: split the command line
        var args = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Act / Assert: it is rejected
        Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse(args));
    }
}
