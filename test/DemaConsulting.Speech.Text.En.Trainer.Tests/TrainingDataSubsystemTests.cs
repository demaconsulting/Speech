using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests;

/// <summary>
///     Subsystem tests for the TrainingDataSubsystem: the units working together through the <c>check-data</c>
///     command on fixture data (every violation class) and on the committed <c>data/text-en</c> folder.
/// </summary>
public sealed class TrainingDataSubsystemTests
{
    /// <summary>
    ///     Runs <c>check-data</c> against a data folder.
    /// </summary>
    /// <param name="workingDirectory">The current directory.</param>
    /// <param name="args">The command arguments.</param>
    /// <returns>The exit code, standard output and standard error.</returns>
    private static (int ExitCode, string Output, string Error) CheckData(string workingDirectory, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = CheckDataCommand.Run(args, output, error, workingDirectory, workingDirectory, _ => null);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>
    ///     Applies one named single-violation mutation to a valid fixture and rewrites the manifest (unless the mutation is the drift itself).
    /// </summary>
    /// <param name="fixture">The fixture.</param>
    /// <param name="mutation">The mutation name.</param>
    private static void Mutate(DatasetFixture fixture, string mutation)
    {
        switch (mutation)
        {
            case "manifest-drift":
                fixture.Replace("README.md", "Fixture data", "Fixture date");
                return;
            case "format-final-newline":
                fixture.Write("dev.tsv", fixture.Read("dev.tsv").TrimEnd('\n'));
                break;
            case "schema-unknown-reading":
                fixture.EditRow("train.tsv", "syn-00001", ("reading_id", "ghost.noun"));
                break;
            case "schema-span-off-by-one":
                fixture.EditRow("train.tsv", "harvest-00001", ("start", "10"));
                break;
            case "schema-duplicate-id":
                fixture.EditRow("dev.tsv", "syn-00003", ("id", "syn-00001"));
                break;
            case "license-cc-by-sa":
                fixture.EditRow("train.tsv", "syn-00001", ("license", "CC-BY-SA-4.0"));
                break;
            case "license-generated-not-cc0":
                fixture.EditRow("train.tsv", "syn-00001", ("license", "Public-Domain"));
                break;
            case "license-missing-attribution":
                File.Delete(fixture.PathOf("ATTRIBUTION.md"));
                break;
            case "license-evaluation-path":
                fixture.Write("audit/heteronym-bench-copy.md", "# not allowed\n");
                break;
            case "leakage-duplicate-sentence":
                fixture.EditRow("dev.tsv", "syn-00003", ("sentence", "The lead pipe was heavy!"), ("start", "4"), ("end", "8"));
                break;
            case "leakage-document-across-splits":
                fixture.EditRow("harvest-provenance.tsv", "harvest-00003", ("source_id", "masc:written/blog/one"));
                break;
            case "leakage-template-across-files":
                fixture.AppendDataRow("dev.tsv", "syn-00004", "lead.noun", "The lead pipe was rusty.", "lead", "synthetic", "CC0-1.0");
                fixture.AppendRow("synthetic-provenance.tsv", "syn-00004", "s4", "lead", "the _ pipe was");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation.");
        }

        fixture.RewriteManifest();
    }

    /// <summary>Proves valid data passes every check with exit code 0.</summary>
    [Fact]
    public void TrainingDataSubsystem_CheckData_ValidFixture_PassesAllChecks()
    {
        // Arrange
        using var fixture = new DatasetFixture();

        // Act
        var (exitCode, output, error) = CheckData(fixture.BaseDirectory, "--data", fixture.Root);

        // Assert
        Assert.True(exitCode == 0, output + error);
        Assert.Contains("check-data: OK (0 findings)", output);
    }

    /// <summary>Proves every violation class fails the run with exit code 1 and names the check that found it.</summary>
    [Theory]
    [InlineData("manifest-drift", "manifest")]
    [InlineData("format-final-newline", "format")]
    [InlineData("schema-unknown-reading", "schema")]
    [InlineData("schema-span-off-by-one", "schema")]
    [InlineData("schema-duplicate-id", "schema")]
    [InlineData("license-cc-by-sa", "license")]
    [InlineData("license-generated-not-cc0", "license")]
    [InlineData("license-missing-attribution", "license")]
    [InlineData("license-evaluation-path", "license")]
    [InlineData("leakage-duplicate-sentence", "leakage")]
    [InlineData("leakage-document-across-splits", "leakage")]
    [InlineData("leakage-template-across-files", "leakage")]
    public void TrainingDataSubsystem_CheckData_SingleViolation_ExitsOneNamingTheCheck(string mutation, string check)
    {
        // Arrange
        using var fixture = new DatasetFixture();
        Mutate(fixture, mutation);

        // Act
        var (exitCode, output, _) = CheckData(fixture.BaseDirectory, "--data", fixture.Root);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains($"check-data: {check}: ", output);
        Assert.Contains("check-data: FAILED", output);
    }

    /// <summary>Proves a changed file is a manifest finding naming the file until the manifest is regenerated.</summary>
    [Fact]
    public void TrainingDataSubsystem_CheckData_ManifestDrift_NamesFileAndPassesAfterRegeneration()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.Replace("README.md", "Fixture data", "Fixture date");

        // Act
        var drifted = CheckData(fixture.BaseDirectory, "--data", fixture.Root);
        fixture.RewriteManifest();
        var regenerated = CheckData(fixture.BaseDirectory, "--data", fixture.Root);

        // Assert
        Assert.Equal(1, drifted.ExitCode);
        Assert.Contains("check-data: manifest: README.md: SHA-256 is", drifted.Output);
        Assert.Equal(0, regenerated.ExitCode);
    }

    /// <summary>Proves an overlap with an external evaluation set is reported when sets are supplied.</summary>
    [Fact]
    public void TrainingDataSubsystem_CheckData_ExternalOverlap_IsReportedWhenSetsSupplied()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.WriteEvaluationSet("gold", "A cold wind blew hard");

        // Act
        var (exitCode, output, _) = CheckData(fixture.BaseDirectory, "--data", fixture.Root, "--eval-sets", fixture.EvaluationDirectory);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("check-data: leakage: train.tsv:4: syn-00002: exact match with evaluation set gold", output);
    }

    /// <summary>Proves the external check is optional: without evaluation sets the data passes and says so.</summary>
    [Fact]
    public void TrainingDataSubsystem_CheckData_NoEvaluationSets_PassesAndSaysSkipped()
    {
        // Arrange
        using var fixture = new DatasetFixture();

        // Act
        var (exitCode, output, _) = CheckData(fixture.BaseDirectory, "--data", fixture.Root);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("external leakage check skipped", output);
    }

    /// <summary>Proves the committed <c>data/text-en</c> folder passes every check.</summary>
    [Fact]
    public void TrainingDataSubsystem_CheckData_CommittedData_PassesAllChecks()
    {
        // Arrange: find the repository root (the folder with Speech.slnx) above the test binaries
        var root = FindRepositoryRoot();
        Assert.SkipUnless(root is not null, "The repository root (Speech.slnx) was not found above the test binaries.");
        var dataFolder = Path.Combine(root, "data", "text-en");
        Assert.SkipUnless(Directory.Exists(dataFolder), $"The committed data folder '{dataFolder}' is absent in this checkout.");

        // Act
        var (exitCode, output, error) = CheckData(root, "--data", dataFolder);

        // Assert: any finding fails the test and its message shows the full report
        Assert.True(exitCode == 0, output + error);
        Assert.Contains("check-data: OK (0 findings)", output);
    }

    /// <summary>
    ///     Finds the repository root by walking up from the test binaries to the folder that holds <c>Speech.slnx</c>.
    /// </summary>
    /// <returns>The repository root, or <see langword="null"/> when none is found.</returns>
    private static string? FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Speech.slnx")))
            {
                return directory.FullName;
            }
        }

        return null;
    }
}
