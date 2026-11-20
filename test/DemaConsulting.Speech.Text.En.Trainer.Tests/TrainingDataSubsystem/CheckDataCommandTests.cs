using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingDataSubsystem;

/// <summary>
///     Unit tests for <see cref="CheckDataCommand"/> and the <c>Program</c> dispatch: option parsing, discovery,
///     environment handling and the exit codes 0, 1 and 2.
/// </summary>
public sealed class CheckDataCommandTests
{
    /// <summary>
    ///     Runs the command in memory.
    /// </summary>
    /// <param name="workingDirectory">The current directory the command resolves relative paths against.</param>
    /// <param name="environment">The environment variables the command may read, or <see langword="null"/> for none.</param>
    /// <param name="args">The command arguments.</param>
    /// <returns>The exit code, standard output and standard error.</returns>
    private static (int ExitCode, string Output, string Error) Run(
        string workingDirectory,
        Dictionary<string, string>? environment,
        params string[] args) =>
        RunFrom(workingDirectory, workingDirectory, environment, args);

    /// <summary>
    ///     Runs the command in memory with a separate tool folder.
    /// </summary>
    /// <param name="workingDirectory">The current directory.</param>
    /// <param name="baseDirectory">The tool's own folder.</param>
    /// <param name="environment">The environment variables, or <see langword="null"/>.</param>
    /// <param name="args">The command arguments.</param>
    /// <returns>The exit code, standard output and standard error.</returns>
    private static (int ExitCode, string Output, string Error) RunFrom(
        string workingDirectory,
        string baseDirectory,
        Dictionary<string, string>? environment,
        params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = CheckDataCommand.Run(
            args,
            output,
            error,
            workingDirectory,
            baseDirectory,
            name => environment is not null && environment.TryGetValue(name, out var value) ? value : null);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>Proves valid data exits with 0, reports OK and notes that the external check was skipped.</summary>
    [Fact]
    public void CheckDataCommand_Run_ValidData_ExitsZeroAndSkipsExternalCheck()
    {
        // Arrange
        using var fixture = new DatasetFixture();

        // Act
        var (exitCode, output, error) = Run(fixture.BaseDirectory, null, "--data", fixture.Root);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("check-data: OK (0 findings)", output);
        Assert.Contains("external leakage check skipped (no evaluation sets supplied)", output);
        Assert.Equal(string.Empty, error);
    }

    /// <summary>
    ///     Proves <c>--generalization</c> checks a staged file in place of the committed set: the staged rows' novelty
    ///     findings carry the staged location and a generic reason, and the committed set is not checked.
    /// </summary>
    [Fact]
    public void CheckDataCommand_Run_StagedGeneralization_ChecksStagedFileWithGenericReasons()
    {
        // Arrange: a staged row that repeats a training sentence, outside the data folder
        using var fixture = new DatasetFixture();
        var staged = Path.Combine(fixture.EvaluationDirectory, "generalization.tsv");
        fixture.WriteGeneralization(
            staged,
            DatasetFixture.GeneralizationRowText("gen-0001", "lead.verb", "She will lead the team today!", "lead", "novel-construction", "lead:guide", "-"));

        // Act
        var (exitCode, output, _) = Run(fixture.BaseDirectory, null, "--data", fixture.Root, "--generalization", staged);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains($"check-data: overlay: staged:generalization.tsv:2: gen-0001: N1: {EvaluationOverlayChecker.GenericN1}", output);
        Assert.Contains("generalization set checked from the staged file", output);
        Assert.DoesNotContain("harvest-00001", output);
    }

    /// <summary>Proves a staged generalization set that does not exist is a usage error with exit code 2.</summary>
    [Fact]
    public void CheckDataCommand_Run_StagedGeneralizationMissing_ExitsTwo()
    {
        // Arrange
        using var fixture = new DatasetFixture();

        // Act
        var (exitCode, _, error) = Run(fixture.BaseDirectory, null, "--data", fixture.Root, "--generalization", "absent.tsv");

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("staged generalization set", error);
    }

    /// <summary>Proves <c>--generalization</c> is parsed in both spellings.</summary>
    [Theory]
    [InlineData("--generalization", "staged.tsv")]
    [InlineData("--generalization=staged.tsv", null)]
    public void CheckDataCommand_TryParse_GeneralizationOption_IsParsed(string first, string? second)
    {
        // Arrange
        var args = second is null ? new[] { first } : [first, second];

        // Act
        var options = CheckDataCommand.TryParse(args, out var error);

        // Assert
        Assert.Null(error);
        Assert.Equal("staged.tsv", options!.Generalization);
    }

    /// <summary>Proves a violation exits with 1 and the output names the check, file and line.</summary>
    [Fact]
    public void CheckDataCommand_Run_Violation_ExitsOneAndNamesCheckFileAndLine()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "syn-00001", ("license", "CC-BY-SA-4.0"));
        fixture.RewriteManifest();

        // Act
        var (exitCode, output, _) = Run(fixture.BaseDirectory, null, "--data", fixture.Root);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("check-data: license: train.tsv:3: syn-00001: license 'CC-BY-SA-4.0' is not allowed", output);
        Assert.Contains("check-data: FAILED", output);
    }

    /// <summary>Proves the <c>--data=value</c> form is accepted.</summary>
    [Fact]
    public void CheckDataCommand_Run_DataOptionWithEquals_IsAccepted()
    {
        // Arrange
        using var fixture = new DatasetFixture();

        // Act
        var (exitCode, _, _) = Run(fixture.BaseDirectory, null, $"--data={fixture.Root}");

        // Assert
        Assert.Equal(0, exitCode);
    }

    /// <summary>Proves an unknown option is a usage error with exit code 2.</summary>
    [Fact]
    public void CheckDataCommand_Run_UnknownOption_ExitsTwoWithMessage()
    {
        // Arrange / Act
        var (exitCode, output, error) = Run(Path.GetTempPath(), null, "--bogus");

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, output);
        Assert.Contains("unknown option '--bogus'", error);
    }

    /// <summary>Proves an option that needs a value but has none is a usage error.</summary>
    [Fact]
    public void CheckDataCommand_Run_OptionWithoutValue_ExitsTwo()
    {
        // Arrange / Act
        var (exitCode, _, error) = Run(Path.GetTempPath(), null, "--data");

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("option --data needs a value", error);
    }

    /// <summary>Proves a non-positive or non-numeric <c>--max-findings</c> is a usage error.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("many")]
    public void CheckDataCommand_Run_InvalidMaxFindings_ExitsTwo(string value)
    {
        // Arrange / Act
        var (exitCode, _, error) = Run(Path.GetTempPath(), null, "--max-findings", value);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("--max-findings needs a positive integer", error);
    }

    /// <summary>Proves a data folder that does not exist, or that cannot be discovered, is a usage error.</summary>
    [Fact]
    public void CheckDataCommand_Run_DataFolderMissing_ExitsTwo()
    {
        // Arrange: a fixture folder has no data/text-en below or above it on a clean temp tree
        using var fixture = new DatasetFixture();

        // Act
        var explicitFolder = Run(fixture.BaseDirectory, null, "--data", Path.Combine(fixture.BaseDirectory, "nope"));
        var discovered = Run(fixture.BaseDirectory, null);

        // Assert
        Assert.Equal(2, explicitFolder.ExitCode);
        Assert.Contains("was not found", explicitFolder.Error);
        Assert.Equal(2, discovered.ExitCode);
        Assert.Contains("data folder 'data/text-en' was not found", discovered.Error);
    }

    /// <summary>Proves the data folder is discovered by walking up from the current directory, then from the tool's own folder.</summary>
    [Fact]
    public void CheckDataCommand_Run_NoDataOption_DiscoversFolderFromCurrentDirectory()
    {
        // Arrange: the data lives in <base>/data/text-en; one run starts in <base>/sub/deeper, the other in an unrelated folder with the tool below <base>/tools
        using var fixture = new DatasetFixture(Path.Combine("data", "text-en"));
        var workingDirectory = Path.Combine(fixture.BaseDirectory, "sub", "deeper");
        Directory.CreateDirectory(workingDirectory);
        using var unrelated = new DatasetFixture();
        var toolDirectory = Path.Combine(fixture.BaseDirectory, "tools", "bin");
        Directory.CreateDirectory(toolDirectory);

        // Act
        var fromCurrent = Run(workingDirectory, null);
        var fromTool = RunFrom(unrelated.BaseDirectory, toolDirectory, null);

        // Assert
        Assert.Equal(0, fromCurrent.ExitCode);
        Assert.Contains($"data folder {fixture.Root}", fromCurrent.Output);
        Assert.Equal(0, fromTool.ExitCode);
        Assert.Contains($"data folder {fixture.Root}", fromTool.Output);
    }

    /// <summary>Proves the environment variable supplies the evaluation sets when the option is absent.</summary>
    [Fact]
    public void CheckDataCommand_Run_EvalSetsFromEnvironment_RunsExternalCheck()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.WriteEvaluationSet("gold", "the lead pipe was heavy");
        var environment = new Dictionary<string, string> { [CheckDataCommand.EvalSetsVariable] = fixture.EvaluationDirectory };

        // Act
        var (exitCode, output, _) = Run(fixture.BaseDirectory, environment, "--data", fixture.Root);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("exact match with evaluation set gold", output);
    }

    /// <summary>Proves the option wins over the environment variable.</summary>
    [Fact]
    public void CheckDataCommand_Run_EvalSetsOptionAndEnvironment_OptionTakesPrecedence()
    {
        // Arrange: the environment points at overlapping sets, the option at harmless ones
        using var fixture = new DatasetFixture();
        fixture.WriteEvaluationSet("gold", "the lead pipe was heavy");
        var clean = Path.Combine(fixture.EvaluationDirectory, "clean");
        Directory.CreateDirectory(clean);
        File.WriteAllText(Path.Combine(clean, "other.txt"), "nothing shared with the fixture at all\n");
        var environment = new Dictionary<string, string> { [CheckDataCommand.EvalSetsVariable] = fixture.EvaluationDirectory };

        // Act
        var (exitCode, output, _) = Run(fixture.BaseDirectory, environment, "--data", fixture.Root, "--eval-sets", clean);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("evaluation sets 1 sentences 1", output);
    }

    /// <summary>Proves an evaluation folder that is missing or without sets, or a set file without sentences, is a usage error, never a silent pass.</summary>
    [Fact]
    public void CheckDataCommand_Run_EvalSetsFolderMissing_ExitsTwo()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        var missing = Path.Combine(fixture.BaseDirectory + "-eval", "nope");

        // Act
        var missingFolder = Run(fixture.BaseDirectory, null, "--data", fixture.Root, "--eval-sets", missing);
        var emptyFolder = Run(fixture.BaseDirectory, null, "--data", fixture.Root, "--eval-sets", fixture.EvaluationDirectory);
        File.WriteAllText(Path.Combine(fixture.EvaluationDirectory, "gold.txt"), string.Empty);
        var emptyFile = Run(fixture.BaseDirectory, null, "--data", fixture.Root, "--eval-sets", fixture.EvaluationDirectory);

        // Assert
        Assert.Equal(2, missingFolder.ExitCode);
        Assert.Contains("does not exist", missingFolder.Error);
        Assert.Equal(2, emptyFolder.ExitCode);
        Assert.Contains("contains no *.txt or *.tsv file", emptyFolder.Error);
        Assert.Equal(2, emptyFile.ExitCode);
        Assert.Equal(string.Empty, emptyFile.Output);
        Assert.Contains("gold.txt' has no sentences", emptyFile.Error);
    }

    /// <summary>Proves a malformed data or evaluation path is a usage error rather than an unhandled exception.</summary>
    [Theory]
    [InlineData("--data")]
    [InlineData("--eval-sets")]
    public void CheckDataCommand_Run_MalformedPath_ExitsTwo(string option)
    {
        // Arrange
        using var fixture = new DatasetFixture();
        string[] args = option == "--data"
            ? ["--data", "bad\0path"]
            : ["--data", fixture.Root, "--eval-sets", "bad\0path"];

        // Act
        var (exitCode, output, error) = Run(fixture.BaseDirectory, null, args);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, output);
        Assert.Contains("check-data: error:", error);
    }

    /// <summary>Proves an evaluation folder inside the data folder is itself a finding.</summary>
    [Fact]
    public void CheckDataCommand_Run_EvalSetsInsideDataFolder_ExitsOneWithFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        var inside = Path.Combine(fixture.Root, "eval-copy");
        Directory.CreateDirectory(inside);
        File.WriteAllText(Path.Combine(inside, "gold.txt"), "something\n");

        // Act
        var (exitCode, output, _) = Run(fixture.BaseDirectory, null, "--data", fixture.Root, "--eval-sets", inside);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("the evaluation-set folder is inside the data folder", output);
    }

    /// <summary>Proves the leakage report is written when requested with evaluation sets.</summary>
    [Fact]
    public void CheckDataCommand_Run_LeakageReportOption_WritesReportFile()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.WriteEvaluationSet("gold", "nothing shared with the fixture at all");
        var reportPath = Path.Combine(fixture.BaseDirectory + "-eval", "out.tsv");

        // Act
        var (exitCode, _, _) = Run(
            fixture.BaseDirectory, null, "--data", fixture.Root, "--eval-sets", fixture.EvaluationDirectory, "--leakage-report", reportPath);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.StartsWith("eval_set\teval_sentences\tsplit\trows\texact\tnear_duplicate\tshare_5gram\n", File.ReadAllText(reportPath));
    }

    /// <summary>Proves a leakage report without evaluation sets, or inside the data folder, is a usage error.</summary>
    [Fact]
    public void CheckDataCommand_Run_LeakageReportWithoutSetsOrInsideData_ExitsTwo()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.WriteEvaluationSet("gold", "something else entirely");

        // Act
        var withoutSets = Run(fixture.BaseDirectory, null, "--data", fixture.Root, "--leakage-report", Path.Combine(fixture.BaseDirectory, "r.tsv"));
        var insideData = Run(
            fixture.BaseDirectory, null, "--data", fixture.Root, "--eval-sets", fixture.EvaluationDirectory, "--leakage-report", Path.Combine(fixture.Root, "r.tsv"));

        // Assert
        Assert.Equal(2, withoutSets.ExitCode);
        Assert.Equal(2, insideData.ExitCode);
        Assert.Contains("--leakage-report needs evaluation sets", withoutSets.Error);
    }

    /// <summary>Proves <c>--counts</c> prints the per-reading table.</summary>
    [Fact]
    public void CheckDataCommand_Run_CountsOption_PrintsPerReadingTable()
    {
        // Arrange
        using var fixture = new DatasetFixture();

        // Act
        var (exitCode, output, _) = Run(fixture.BaseDirectory, null, "--data", fixture.Root, "--counts");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("reading_id\ttrain_natural\ttrain_synthetic\tdev\tholdout_natural\tholdout_independent\tneed_synthetic\tneed_natural", output);
        Assert.Contains("wind.noun\t0\t1\t1\t0\t1\t39\t10", output);
    }

    /// <summary>Proves <c>--max-findings</c> caps the printed findings per check but not the exit code.</summary>
    [Fact]
    public void CheckDataCommand_Run_MaxFindings_CapsPrintedFindings()
    {
        // Arrange: three rows with a disallowed license
        using var fixture = new DatasetFixture();
        foreach (var id in new[] { "syn-00001", "syn-00002" })
        {
            fixture.EditRow("train.tsv", id, ("license", "CC-BY-SA-4.0"));
        }

        fixture.EditRow("dev.tsv", "syn-00003", ("license", "CC-BY-SA-4.0"));

        // Act
        var (exitCode, output, _) = Run(fixture.BaseDirectory, null, "--data", fixture.Root, "--max-findings", "1");

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("check-data: license: (", output);
        Assert.Contains("more findings not shown)", output);
    }

    /// <summary>Proves <c>Program</c> reports a missing or unknown command as a usage error.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("build")]
    public void Program_Run_MissingOrUnknownCommand_ExitsTwoWithUsage(string command)
    {
        // Arrange: an empty command stands for no argument at all
        string[] args = command.Length == 0 ? [] : [command];
        var output = new StringWriter();
        var error = new StringWriter();

        // Act
        var exitCode = Program.Run(args, output, error);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("Usage: DemaConsulting.Speech.Text.En.Trainer <command>", error.ToString());
    }

    /// <summary>Proves <c>Program</c> forwards <c>check-data --help</c> to the command, which prints its usage and exit codes.</summary>
    [Fact]
    public void Program_Run_CheckDataHelp_PrintsCommandUsage()
    {
        // Arrange
        var output = new StringWriter();

        // Act
        var exitCode = Program.Run(["check-data", "--help"], output, new StringWriter());

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Usage: check-data [options]", output.ToString());
        Assert.Contains("Exit codes: 0 = no findings, 1 = findings, 2 = usage or I/O error.", output.ToString());
    }
}
