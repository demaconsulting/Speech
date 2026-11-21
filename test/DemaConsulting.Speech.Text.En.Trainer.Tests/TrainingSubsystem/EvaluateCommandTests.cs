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

    /// <summary>Proves the gate exits one when a gate set is below its threshold.</summary>
    [Fact]
    public void EvaluateCommand_Run_GateAccuracyBelowThreshold_ExitsOne()
    {
        // Arrange: the threshold is above the 97.5% the model reaches
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        CommandHarness.SetGateThresholds(modelDirectory, ("holdout-natural", 9900));

        // Act
        var (exitCode, output, _) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--gate");

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("BELOW", output);
        Assert.Contains("GATE: FAIL", output);
    }

    /// <summary>Proves a gate set without its file is a data error: the gate exits two and names the set.</summary>
    [Fact]
    public void EvaluateCommand_Gate_MissingGateSet_ExitsTwo()
    {
        // Arrange: the committed thresholds name all four gate sets, and the fixture has no regression overlay
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);

        // Act
        var (exitCode, output, error) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--gate");

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("the gate set", error);
        Assert.DoesNotContain("GATE:", output);
    }

    /// <summary>Proves the gate run on a candidate model prints its flips against the committed record's current readings.</summary>
    [Fact]
    public void EvaluateCommand_Gate_CandidateModel_PrintsFlipsAgainstCommittedRecord()
    {
        // Arrange: a record of the trained model, then a model with no weights that always takes the first candidate
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        CommandHarness.SetGateThresholds(modelDirectory, ("holdout-natural", 1));
        var recordDirectory = Path.Combine(fixture.BaseDirectory, "record");
        CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--write", "--record-dir", recordDirectory);
        var empty = Path.Combine(fixture.BaseDirectory, "empty.bin");
        File.WriteAllBytes(empty, SparsePerceptronModelWriter.Compile(18, ReadingInventory.CurrentVersion, 1f, 64f, []));

        // Act
        var (exitCode, output, _) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--model", empty, "--gate", "--record-dir", recordDirectory);

        // Assert: the record is unchanged, and the flips name the set, the row and the change
        Assert.Equal(0, exitCode);
        Assert.Contains("evaluate: FLIP holdout-natural ", output);
        Assert.Contains(" broken", output);
        Assert.Matches(@"FLIPS holdout-natural: fixed \d+, broken [1-9]\d*", output);
        Assert.Contains("holdout-natural\t", File.ReadAllText(Path.Combine(recordDirectory, EvaluationRecord.GateRowsFile)));
        Assert.Equal("set\tid\tgold\tprevious\tcurrent\tchange\n", File.ReadAllText(Path.Combine(recordDirectory, EvaluationRecord.FlipsFile)));
    }
    /// <summary>Proves the gate decision is made on the natural holdout even when the whole record is written in the same run.</summary>
    [Fact]
    public void EvaluateCommand_Run_GateWithWrite_DecidesOnTheNaturalHoldout()
    {
        // Arrange: a threshold the natural holdout (97.5%) meets but a lower-scoring set would not
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        CommandHarness.SetGateThresholds(modelDirectory, ("holdout-natural", 9600));
        var recordDirectory = Path.Combine(fixture.BaseDirectory, "record");

        // Act
        var (exitCode, output, _) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--gate", "--write", "--record-dir", recordDirectory);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("GATE holdout-natural: 39/40 97.50%", output);
        Assert.Contains("threshold 96.00%: ok", output);
        Assert.Contains("GATE: PASS", output);
        Assert.Equal(10, Directory.GetFiles(recordDirectory).Length);
    }

    /// <summary>Proves the gate and the check together exit zero only when the gate passes and the record is current, and print both results.</summary>
    [Theory]
    [InlineData(9000, false, 0, "GATE: PASS", "--check: OK (the evaluation record is current)")]
    [InlineData(9900, false, 1, "GATE: FAIL", "--check: OK (the evaluation record is current)")]
    [InlineData(9000, true, 1, "GATE: PASS", "STALE: summary.tsv")]
    public void EvaluateCommand_Run_GatePassesRecordCurrent_ExitsZero(int threshold, bool tamper, int expectedExit, string gate, string record)
    {
        // Arrange: the record is written with the threshold under test, so it is current unless it is edited afterwards
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        CommandHarness.SetGateThresholds(modelDirectory, ("holdout-natural", threshold));
        var recordDirectory = Path.Combine(fixture.BaseDirectory, "record");
        CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--write", "--record-dir", recordDirectory);
        if (tamper)
        {
            File.AppendAllText(Path.Combine(recordDirectory, EvaluationRecord.SummaryFile), "tampered\n");
        }

        // Act
        var (exitCode, output, _) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--gate", "--check", "--record-dir", recordDirectory);

        // Assert
        Assert.Equal(expectedExit, exitCode);
        Assert.Contains(gate, output);
        Assert.Contains(record, output);
    }

    /// <summary>Proves a harvested row without a source document stops the evaluation with a data error that names the row, and no gate verdict is printed.</summary>
    [Fact]
    public void EvaluateCommand_Run_HarvestedRowWithoutProvenance_ExitsTwoNamingTheProblem()
    {
        // Arrange
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        CommandHarness.SetGateThresholds(modelDirectory, ("holdout-natural", 9000));
        var kept = fixture.Read("harvest-provenance.tsv").Split('\n').Where(line => !line.StartsWith("harvest-h00\t", StringComparison.Ordinal));
        fixture.Write("harvest-provenance.tsv", string.Join('\n', kept));

        // Act
        var (exitCode, output, error) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--gate", "--set", "holdout-natural");

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("harvest-h00", error);
        Assert.DoesNotContain("GATE:", output);
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
            Assert.Contains($"seed {seed}: image sha256 ", output);
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
        Assert.Equal(10, written);
        Assert.Equal(0, fresh.ExitCode);
        Assert.Contains("OK", fresh.Output);
        Assert.Equal(1, stale.ExitCode);
        Assert.Contains("STALE: summary.tsv", stale.Output);
        Assert.Contains("STALE: gate.tsv", stale.Output);
    }

    /// <summary>Proves the overlays are evaluated on request with and without the cue priors, by slice, with the cue-unsolvable rows verified, and read no holdout.</summary>
    [Fact]
    public void EvaluateCommand_Run_OverlaySets_ReportedWithAndWithoutCues()
    {
        // Arrange
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        WriteOverlays(fixture);

        // Act
        var (exitCode, output, _) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--set", "regression", "--set", "generalization");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("HOLDOUT", output);
        Assert.Contains("set regression: 2 rows", output);
        Assert.Contains("set generalization: 3 rows", output);
        Assert.Contains("regression slice all: 2 rows, 2 clusters; with cues 2/2 100.00%", output);
        Assert.Contains("model only 2/2 100.00%", output);
        Assert.Contains("generalization slice all: 3 rows, 2 clusters; with cues 3/3 100.00%", output);
        Assert.Contains("cue effect 0.00% [0.00%, 0.00%]; cue fired 0, changed 0", output);
        Assert.Contains("cue-unsolvable check: 1 rows", output);
        Assert.Contains("generalization slice kind:novel-vocabulary", output);
    }

    /// <summary>Proves a missing overlay file asked for by name and a row marked cue_unsolvable=yes that a cue test fires on are data errors, and an overlay without rows is skipped.</summary>
    [Fact]
    public void EvaluateCommand_Run_OverlayMissingViolatedOrEmpty_ExitsTwoOrSkips()
    {
        // Arrange
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        var missing = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--set", "regression");
        fixture.WriteGeneralization();
        var empty = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--set", "generalization");
        fixture.WriteGeneralization(("gen-0001", "lead.verb", "Yesterday we lead the team.", "novel-construction", "lead:past", "yes"));

        // Act
        var violated = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--set", "generalization");

        // Assert
        Assert.Equal(2, missing.ExitCode);
        Assert.Contains("regression.tsv", missing.Error);
        Assert.Equal(0, empty.ExitCode);
        Assert.Contains("overlay generalization: the file has no rows; not evaluated", empty.Output);
        Assert.Equal(2, violated.ExitCode);
        Assert.Contains("gen-0001 (past-time cue)", violated.Error);
    }

    /// <summary>Proves the record appends the overlays after the data sets, so every earlier line stays byte-identical, and the record with overlays checks as current.</summary>
    [Fact]
    public void EvaluateCommand_Run_WriteWithOverlays_AppendsAfterTheDataSets()
    {
        // Arrange: a record without overlays, then one with them, gated on the natural holdout only
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        CommandHarness.SetGateThresholds(modelDirectory, ("holdout-natural", 9000));
        var before = Path.Combine(fixture.BaseDirectory, "before");
        var after = Path.Combine(fixture.BaseDirectory, "after");
        var plain = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--write", "--record-dir", before);
        WriteOverlays(fixture);

        // Act
        var write = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--write", "--record-dir", after);
        var check = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--check", "--record-dir", after);

        // Assert
        Assert.Equal(0, plain.ExitCode);
        Assert.Contains("overlay regression: regression.tsv is not in the data folder; not evaluated", plain.Output);
        Assert.Equal(0, write.ExitCode);
        Assert.Equal(0, check.ExitCode);
        foreach (var name in new[] { EvaluationRecord.SummaryFile, EvaluationRecord.BySourceFile, EvaluationRecord.ByReadingFile, EvaluationRecord.ConfidenceFile })
        {
            var old = File.ReadAllText(Path.Combine(before, name));
            var appended = File.ReadAllText(Path.Combine(after, name));
            Assert.StartsWith(old, appended, StringComparison.Ordinal);
            Assert.StartsWith("generalization\t", appended[old.Length..], StringComparison.Ordinal);
            Assert.Contains("\nregression\t", appended[old.Length..], StringComparison.Ordinal);
        }

        Assert.Equal(File.ReadAllText(Path.Combine(before, EvaluationRecord.GateFile)), File.ReadAllText(Path.Combine(after, EvaluationRecord.GateFile)));
        Assert.Equal(6, File.ReadAllLines(Path.Combine(after, EvaluationRecord.OverlayRowsFile)).Length);
        Assert.Contains("regression\tall\t2\t2\t2\t10000", File.ReadAllText(Path.Combine(after, EvaluationRecord.CuratedAblationFile)));
    }

    /// <summary>
    ///     Writes a regression overlay of two rows and a generalization set of three rows in two families, all of the
    ///     fixture's clear patterns.
    /// </summary>
    /// <param name="fixture">The fixture.</param>
    private static void WriteOverlays(TrainingFixture fixture)
    {
        fixture.WriteRegression(
            ("reg-0001", "lead.verb", "They will lead the team today.", "listening", "yes"),
            ("reg-0002", "lead.noun", "The lead box was very heavy.", "data-row", "no"));
        fixture.WriteGeneralization(
            ("gen-0001", "lead.verb", "Please lead the hike now.", "novel-construction", "lead:request", "yes"),
            ("gen-0002", "lead.verb", "Please lead the tour now.", "novel-construction", "lead:request", "no"),
            ("gen-0003", "lead.noun", "A lead ball fell down.", "novel-vocabulary", "lead:metal", "no"));
    }
}
