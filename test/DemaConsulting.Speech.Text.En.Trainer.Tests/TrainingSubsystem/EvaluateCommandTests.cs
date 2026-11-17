using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingSubsystem;

/// <summary>
///     Unit tests for <see cref="EvaluateCommand"/>: the command line, which sets are read, the quality gate exit
///     codes, the multi-seed report and the write and check of the evaluation record.
/// </summary>
public sealed class EvaluateCommandTests
{
    /// <summary>Proves help prints the usage and exits with zero.</summary>
    [Fact]
    public void EvaluateCommand_Run_Help_PrintsUsageAndExitsZero()
    {
        // Arrange
        using var fixture = new TrainingFixture();

        // Act
        var (exitCode, output, _) = CommandHarness.Evaluate(fixture, "--help");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Usage: evaluate", output);
    }

    /// <summary>Proves a bad command line is a usage error that names the problem.</summary>
    [Theory]
    [InlineData("--bogus", "unknown option")]
    [InlineData("--set", "needs a value")]
    public void EvaluateCommand_Run_BadCommandLine_ExitsTwoNamingTheProblem(string argument, string message)
    {
        // Arrange
        using var fixture = new TrainingFixture();

        // Act
        var (exitCode, _, error) = CommandHarness.Evaluate(fixture, argument);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains(message, error);
    }

    /// <summary>Proves an unknown set name and the combination of write and check are usage errors.</summary>
    [Fact]
    public void EvaluateCommand_Run_UnknownSetOrWriteWithCheck_ExitsTwo()
    {
        // Arrange
        using var fixture = new TrainingFixture();

        // Act
        var unknown = CommandHarness.Evaluate(fixture, "--set", "holdout-secret");
        var both = CommandHarness.Evaluate(fixture, "--write", "--check");

        // Assert
        Assert.Equal(2, unknown.ExitCode);
        Assert.Contains("unknown set", unknown.Error);
        Assert.Equal(2, both.ExitCode);
        Assert.Contains("cannot be combined", both.Error);
    }

    /// <summary>Proves a missing model file is an input error that names the file.</summary>
    [Fact]
    public void EvaluateCommand_Run_MissingModel_ExitsTwo()
    {
        // Arrange
        using var fixture = new TrainingFixture();

        // Act
        var (exitCode, _, error) = CommandHarness.Evaluate(fixture, "--model-dir", fixture.ModelDirectory);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("heteronyms.bin", error);
    }

    /// <summary>Proves evaluating the development set never reads a holdout file and does not say it did.</summary>
    [Fact]
    public void EvaluateCommand_Run_DevSet_DoesNotReadHoldoutFiles()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        File.Delete(fixture.PathOf("holdout-natural.tsv"));
        File.Delete(fixture.PathOf("holdout-independent.tsv"));

        // Act
        var (exitCode, output, _) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("set dev: 8 rows", output);
        Assert.DoesNotContain("HOLDOUT", output);
    }

    /// <summary>Proves evaluating a holdout set announces that a holdout is read, and prints accuracy, baselines, the interval and parity.</summary>
    [Fact]
    public void EvaluateCommand_Run_HoldoutSet_AnnouncesAndPrintsTheFigures()
    {
        // Arrange
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);

        // Act
        var (exitCode, output, _) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--set", "holdout-natural");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("HOLDOUT EVALUATION", output);
        Assert.Contains("model accuracy        97.50% (39/40)", output);
        Assert.Contains("first-candidate       52.50% (21/40)", output);
        Assert.Contains("improvement over own-UPOS +45.00%", output);
        Assert.Contains("parity: 40 compared, 0 mismatched, 0 suppressed", output);
        Assert.Contains("worst reading lead.verb: 20/21", output);
    }

    /// <summary>Proves the gate exits zero when both conditions hold and says so.</summary>
    [Fact]
    public void EvaluateCommand_Run_GateConditionsMet_ExitsZero()
    {
        // Arrange: 97.5% on the natural holdout, improvement over a baseline that is the first candidate
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        SetGateThreshold(modelDirectory, 900);

        // Act
        var (exitCode, output, _) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--gate");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("GATE: PASS", output);
        Assert.Contains("GATE RUN on holdout-natural", output);
    }

    /// <summary>Proves the gate exits one when the pooled accuracy is below the threshold.</summary>
    [Fact]
    public void EvaluateCommand_Run_GateAccuracyBelowThreshold_ExitsOne()
    {
        // Arrange: the threshold is above the 97.5% the model reaches
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        SetGateThreshold(modelDirectory, 990);

        // Act
        var (exitCode, output, _) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--gate");

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("BELOW", output);
        Assert.Contains("GATE: FAIL", output);
    }

    /// <summary>Proves the gate exits one when the lower bound of the improvement is not above zero, even if the accuracy threshold is met.</summary>
    [Fact]
    public void EvaluateCommand_Run_GateLowerBoundNotPositive_ExitsOne()
    {
        // Arrange: a model with no weights always takes the first candidate, exactly like the baseline, and a threshold it meets
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        SetGateThreshold(modelDirectory, 1);
        var empty = Path.Combine(fixture.BaseDirectory, "empty.bin");
        File.WriteAllBytes(empty, SparsePerceptronModelWriter.Compile(18, ReadingInventory.CurrentVersion, 1f, 64f, []));

        // Act
        var (exitCode, output, _) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--model", empty, "--gate");

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("NOT above zero", output);
        Assert.Contains("GATE: FAIL", output);
    }

    /// <summary>Proves the gate decision is made on the natural holdout even when the whole record is written in the same run.</summary>
    [Fact]
    public void EvaluateCommand_Run_GateWithWrite_DecidesOnTheNaturalHoldout()
    {
        // Arrange: a threshold the natural holdout (97.5%) meets but a lower-scoring set would not
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        SetGateThreshold(modelDirectory, 960);
        var recordDirectory = Path.Combine(fixture.BaseDirectory, "record");

        // Act
        var (exitCode, output, _) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--gate", "--write", "--record-dir", recordDirectory);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("pooled accuracy 97.50%", output);
        Assert.Contains("GATE: PASS", output);
        Assert.Equal(6, Directory.GetFiles(recordDirectory).Length);
    }

    /// <summary>Proves a failing gate is not masked by a current record: both results are printed and the exit code is one.</summary>
    [Fact]
    public void EvaluateCommand_Run_GateFailsRecordCurrent_ExitsOne()
    {
        // Arrange: the record is written with a threshold the model (97.5%) does not reach, so it is current but the gate fails
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        SetGateThreshold(modelDirectory, 990);
        var recordDirectory = Path.Combine(fixture.BaseDirectory, "record");
        var write = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--write", "--record-dir", recordDirectory);

        // Act
        var (exitCode, output, _) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--gate", "--check", "--record-dir", recordDirectory);

        // Assert
        Assert.Equal(0, write.ExitCode);
        Assert.Equal(1, exitCode);
        Assert.Contains("GATE: FAIL", output);
        Assert.Contains("--check: OK (the evaluation record is current)", output);
    }

    /// <summary>Proves a passing gate does not hide a stale record: both results are printed and the exit code is one.</summary>
    [Fact]
    public void EvaluateCommand_Run_GatePassesRecordStale_ExitsOne()
    {
        // Arrange
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        SetGateThreshold(modelDirectory, 900);
        var recordDirectory = Path.Combine(fixture.BaseDirectory, "record");
        CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--write", "--record-dir", recordDirectory);
        File.AppendAllText(Path.Combine(recordDirectory, EvaluationRecord.SummaryFile), "tampered\n");

        // Act
        var (exitCode, output, _) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--gate", "--check", "--record-dir", recordDirectory);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("GATE: PASS", output);
        Assert.Contains("STALE: summary.tsv", output);
    }

    /// <summary>Proves the gate and the check together exit zero only when the gate passes and the record is current.</summary>
    [Fact]
    public void EvaluateCommand_Run_GatePassesRecordCurrent_ExitsZero()
    {
        // Arrange
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        SetGateThreshold(modelDirectory, 900);
        var recordDirectory = Path.Combine(fixture.BaseDirectory, "record");
        CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--write", "--record-dir", recordDirectory);

        // Act
        var (exitCode, output, _) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--gate", "--check", "--record-dir", recordDirectory);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("GATE: PASS", output);
        Assert.Contains("--check: OK (the evaluation record is current)", output);
    }

    /// <summary>Proves a failing gate and a stale record together exit one and print both results.</summary>
    [Fact]
    public void EvaluateCommand_Run_GateFailsRecordStale_ExitsOne()
    {
        // Arrange
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        SetGateThreshold(modelDirectory, 990);
        var recordDirectory = Path.Combine(fixture.BaseDirectory, "record");
        CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--write", "--record-dir", recordDirectory);
        File.AppendAllText(Path.Combine(recordDirectory, EvaluationRecord.SummaryFile), "tampered\n");

        // Act
        var (exitCode, output, _) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--gate", "--check", "--record-dir", recordDirectory);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("GATE: FAIL", output);
        Assert.Contains("STALE: summary.tsv", output);
    }

    /// <summary>Proves writing the record in the same run does not hide a failing gate, and the record is still written.</summary>
    [Fact]
    public void EvaluateCommand_Run_GateFailsWithWrite_ExitsOneAndWritesRecord()
    {
        // Arrange
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        SetGateThreshold(modelDirectory, 990);
        var recordDirectory = Path.Combine(fixture.BaseDirectory, "record");

        // Act
        var (exitCode, output, _) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--gate", "--write", "--record-dir", recordDirectory);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("GATE: FAIL", output);
        Assert.Equal(6, Directory.GetFiles(recordDirectory).Length);
    }

    /// <summary>Proves a harvested row without a source document stops the evaluation with a data error that names the file or the row, and no gate verdict is printed.</summary>
    [Theory]
    [InlineData(true, "holdout-natural", "harvest-provenance.tsv")]
    [InlineData(false, "holdout-natural", "harvest-h00")]
    public void EvaluateCommand_Run_HarvestedRowWithoutProvenance_ExitsTwoNamingTheProblem(bool deleteFile, string set, string expected)
    {
        // Arrange
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        if (deleteFile)
        {
            File.Delete(fixture.PathOf("harvest-provenance.tsv"));
        }
        else
        {
            var kept = fixture.Read("harvest-provenance.tsv").Split('\n').Where(line => !line.StartsWith(expected + "\t", StringComparison.Ordinal));
            fixture.Write("harvest-provenance.tsv", string.Join('\n', kept));
        }

        // Act
        var (exitCode, output, error) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--gate", "--set", set);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains(expected, error);
        Assert.DoesNotContain("GATE:", output);
    }

    /// <summary>Proves the development set, whose rows are harvested too, also stops with a data error that names the row.</summary>
    [Fact]
    public void EvaluateCommand_Run_DevRowWithoutProvenance_ExitsTwoNamingTheRow()
    {
        // Arrange
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        var kept = fixture.Read("harvest-provenance.tsv").Split('\n').Where(line => !line.StartsWith("harvest-d00\t", StringComparison.Ordinal));
        fixture.Write("harvest-provenance.tsv", string.Join('\n', kept));

        // Act
        var (exitCode, _, error) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--set", "dev");

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("harvest-d00", error);
    }

    /// <summary>Proves the multi-seed report trains once per evaluation seed and prints the holdout accuracy of each.</summary>
    [Fact]
    public void EvaluateCommand_Run_Seeds_ReportsEachSeed()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);

        // Act
        var (exitCode, output, _) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--seeds");

        // Assert
        Assert.Equal(0, exitCode);
        foreach (var seed in EvaluationSettings.Default.Seeds)
        {
            Assert.Contains($"seed {seed}: natural", output);
        }
    }

    /// <summary>Proves the evaluation record can be written, then checks as current, and an edited or missing file is reported as stale.</summary>
    [Fact]
    public void EvaluateCommand_Run_WriteThenCheck_MatchesAndDetectsEditedRecord()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        var recordDirectory = Path.Combine(fixture.BaseDirectory, "record");

        // Act
        var write = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--write", "--record-dir", recordDirectory);
        var written = Directory.GetFiles(recordDirectory).Length;
        var fresh = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--check", "--record-dir", recordDirectory);
        File.AppendAllText(Path.Combine(recordDirectory, EvaluationRecord.SummaryFile), "tampered\n");
        File.Delete(Path.Combine(recordDirectory, EvaluationRecord.GateFile));
        var stale = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--check", "--record-dir", recordDirectory);

        // Assert
        Assert.Equal(0, write.ExitCode);
        Assert.Equal(6, written);
        Assert.Equal(0, fresh.ExitCode);
        Assert.Contains("OK", fresh.Output);
        Assert.Equal(1, stale.ExitCode);
        Assert.Contains("STALE: summary.tsv", stale.Output);
        Assert.Contains("STALE: gate.tsv", stale.Output);
    }

    /// <summary>Proves the record is the same when written twice: it holds no time stamp or other varying value.</summary>
    [Fact]
    public void EvaluateCommand_Run_WriteTwice_ProducesIdenticalRecords()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        var first = Path.Combine(fixture.BaseDirectory, "record-1");
        var second = Path.Combine(fixture.BaseDirectory, "record-2");

        // Act
        CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--write", "--record-dir", first);
        CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--write", "--record-dir", second);

        // Assert
        foreach (var file in Directory.GetFiles(first))
        {
            Assert.Equal(File.ReadAllBytes(file), File.ReadAllBytes(Path.Combine(second, Path.GetFileName(file))));
        }
    }

    /// <summary>
    ///     Sets the gate threshold in the record of a model folder.
    /// </summary>
    /// <param name="modelDirectory">The model folder.</param>
    /// <param name="thousandths">The threshold in thousandths.</param>
    private static void SetGateThreshold(string modelDirectory, int thousandths)
    {
        var path = Path.Combine(modelDirectory, TrainCommand.RecordFileName);
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"gateMinPooledAccuracyPerThousand\": 930", $"\"gateMinPooledAccuracyPerThousand\": {thousandths}", StringComparison.Ordinal));
    }
}
