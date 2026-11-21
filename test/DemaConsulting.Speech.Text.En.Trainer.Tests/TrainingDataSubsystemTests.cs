using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests;

/// <summary>
///     Subsystem tests for the TrainingDataSubsystem: the units working together through the <c>check-data</c>
///     command on fixture data (every violation class) and on the committed <c>data/text-en</c> folder.
/// </summary>
public sealed class TrainingDataSubsystemTests
{
    /// <summary>
    ///     Runs <c>check-data</c> against a data folder with the tokenizer version of this build, as the trainer does.
    /// </summary>
    /// <param name="workingDirectory">The current directory.</param>
    /// <param name="args">The command arguments.</param>
    /// <returns>The exit code, standard output and standard error.</returns>
    private static (int ExitCode, string Output, string Error) CheckData(string workingDirectory, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = CheckDataCommand.Run(args, output, error, workingDirectory, workingDirectory, _ => null, TrainingCorpus.TaggerVersion);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>
    ///     Applies one named single-violation mutation to a valid fixture and rewrites the manifest so only that violation remains.
    /// </summary>
    /// <param name="fixture">The fixture.</param>
    /// <param name="mutation">The mutation name.</param>
    private static void Mutate(DatasetFixture fixture, string mutation)
    {
        switch (mutation)
        {
            case "format-final-newline":
                fixture.Write("dev.tsv", fixture.Read("dev.tsv").TrimEnd('\n'));
                break;
            case "schema-unknown-reading":
                fixture.EditRow("train.tsv", "syn-00001", ("reading_id", "ghost.noun"));
                break;
            case "license-cc-by-sa":
                fixture.EditRow("train.tsv", "syn-00001", ("license", "CC-BY-SA-4.0"));
                break;
            case "license-evaluation-path":
                fixture.Write("audit/heteronym-bench-copy.md", "# not allowed\n");
                break;
            case "overlay-seen-mark":
                fixture.EditRow(TextEnDataSet.RegressionFile, "reg-0001", ("seen_in_training", "no"));
                break;
            case "clusters-stale-table":
                fixture.WriteClusterFiles(TrainingCorpus.TaggerVersion);
                fixture.Replace(ClusterDataChecker.TableFile, "the\t01\t30", "the\t01\t31");
                break;
            case "clusters-used-dev-book":
                fixture.WriteClusterFiles(TrainingCorpus.TaggerVersion);
                fixture.EditRow("harvest-provenance.tsv", "harvest-00003", ("source_id", "gutenberg-1"));
                break;
            case "leakage-duplicate-sentence":
                fixture.EditRow("dev.tsv", "syn-00003", ("sentence", "The lead pipe was heavy!"), ("start", "4"), ("end", "8"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation.");
        }

        fixture.RewriteManifest();
    }

    /// <summary>Proves valid data passes every check with exit code 0, and that the optional external check says it was skipped.</summary>
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
        Assert.Contains("external leakage check skipped", output);
    }

    /// <summary>Proves every check family fails the run with exit code 1 and names the check that found the violation.</summary>
    [Theory]
    [InlineData("format-final-newline", "format")]
    [InlineData("schema-unknown-reading", "schema")]
    [InlineData("license-cc-by-sa", "license")]
    [InlineData("license-evaluation-path", "license")]
    [InlineData("leakage-duplicate-sentence", "leakage")]
    [InlineData("overlay-seen-mark", "overlay")]
    [InlineData("clusters-stale-table", "clusters")]
    [InlineData("clusters-used-dev-book", "leakage")]
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

    /// <summary>Proves the committed <c>data/text-en</c> folder passes every check, including the word-cluster record against this build's tokenizer.</summary>
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
    ///     Proves a staged generalization set is checked against the committed training data through the command, with
    ///     novelty findings that name the staged row and give a generic reason, never a training row.
    /// </summary>
    [Fact]
    public void TrainingDataSubsystem_CheckData_StagedGeneralization_ReportsGenericFindings()
    {
        // Arrange: a staged set whose first version repeats a training sentence
        using var fixture = new DatasetFixture();
        var staged = Path.Combine(fixture.EvaluationDirectory, "generalization.tsv");
        fixture.WriteGeneralization(
            staged,
            DatasetFixture.GeneralizationRowText("gen-0001", "lead.verb", "She will lead the team today!", "lead", "novel-construction", "lead:guide", "-"));

        // Act
        var failing = CheckData(fixture.BaseDirectory, "--data", fixture.Root, "--generalization", staged);

        // Assert
        Assert.Equal(1, failing.ExitCode);
        Assert.Contains($"staged:generalization.tsv:2: gen-0001: N1: {EvaluationOverlayChecker.GenericN1}", failing.Output);
        Assert.DoesNotContain(failing.Output.Split('\n'), line => line.Contains("overlay:", StringComparison.Ordinal) && line.Contains("train.tsv", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Proves every golden and test reference of the committed regression overlay resolves in the repository: the
    ///     referenced line or row exists and holds the overlay row's sentence (and, where the file names one, its reading).
    /// </summary>
    [Fact]
    public void TrainingDataSubsystem_CommittedRegression_GoldenAndTestReferencesResolve()
    {
        // Arrange: find the repository root and load the committed overlay
        var root = FindRepositoryRoot();
        Assert.SkipUnless(root is not null, "The repository root (Speech.slnx) was not found above the test binaries.");
        var dataFolder = Path.Combine(root, "data", "text-en");
        Assert.SkipUnless(File.Exists(Path.Combine(dataFolder, TextEnDataSet.RegressionFile)), "The committed regression overlay is absent in this checkout.");
        var rows = TextEnDataSet.LoadRegression(Path.Combine(dataFolder, TextEnDataSet.RegressionFile), TextEnDataSet.RegressionFile, new CheckReport());
        var testFiles = Directory.EnumerateFiles(Path.Combine(root, "test"), "*.*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .GroupBy(Path.GetFileName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key!, group => group.Count() == 1 ? group.First() : null, StringComparer.Ordinal);
        testFiles["RUN-LOG.md"] = Path.Combine(root, "tools", "DemaConsulting.Speech.Text.En.Trainer", "RUN-LOG.md");

        // Act: resolve every reference that is not a data row (data rows are resolved by check-data itself)
        var failures = new List<string>();
        foreach (var row in rows.Where(row => row.Origin != "data-row"))
        {
            var separator = row.Reference.LastIndexOf(':');
            var file = row.Reference[..separator];
            var anchor = row.Reference[(separator + 1)..];
            if (!testFiles.TryGetValue(file, out var path) || path is null || !ReferenceHolds(path, anchor, row))
            {
                failures.Add($"{row.Id}: {row.Reference}");
            }
        }

        // Assert
        Assert.NotEmpty(rows);
        Assert.Empty(failures);
    }

    /// <summary>
    ///     Determines whether a referenced line or row holds a regression row.
    /// </summary>
    /// <param name="path">The referenced file.</param>
    /// <param name="anchor">The 1-based line number, or the row id of a golden file keyed by id.</param>
    /// <param name="row">The regression row.</param>
    /// <returns><see langword="true"/> when the reference holds the row's sentence (and its reading where named).</returns>
    private static bool ReferenceHolds(string path, string anchor, RegressionRecord row)
    {
        var lines = File.ReadAllLines(path);
        var sentence = row.Sentence.TrimEnd('.');
        if (!int.TryParse(anchor, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number))
        {
            // A golden file keyed by id: the row with that id has exactly the sentence
            return lines.Select(line => line.Split('\t')).Any(cells => cells[0] == anchor && cells.Length > 1 && cells[1] == row.Sentence);
        }

        if (number < 1 || number > lines.Length)
        {
            return false;
        }

        // A line reference: the readings golden holds the reading and the sentence, a test file or the run log the
        // sentence (a wrapped run-log sentence may continue on the next line)
        var text = string.Join(' ', lines.Skip(number - 1).Take(2));
        if (path.EndsWith("readings-golden.tsv", StringComparison.Ordinal))
        {
            var cells = lines[number - 1].Split('\t');
            return cells[0] == row.ReadingId && cells[2] == row.Sentence;
        }

        return text.Contains(sentence, StringComparison.Ordinal)
            && (path.EndsWith(".md", StringComparison.Ordinal) || lines[number - 1].Contains(row.ReadingId, StringComparison.Ordinal));
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
