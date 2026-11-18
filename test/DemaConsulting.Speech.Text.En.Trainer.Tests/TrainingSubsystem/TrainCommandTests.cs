using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingSubsystem;

/// <summary>
///     Unit tests for <see cref="TrainCommand"/>: the command line, the files it writes, the experiment guard and
///     the <c>--check</c> verification of a committed model and record.
/// </summary>
public sealed class TrainCommandTests
{
    /// <summary>The model file name in a model folder.</summary>
    private const string Model = TrainCommand.ModelFileName;

    /// <summary>The record file name in a model folder.</summary>
    private const string Record = TrainCommand.RecordFileName;

    /// <summary>Proves help prints the usage and exits with zero.</summary>
    [Fact]
    public void TrainCommand_Run_Help_PrintsUsageAndExitsZero()
    {
        // Arrange
        using var fixture = new TrainingFixture();

        // Act
        var (exitCode, output, _) = CommandHarness.Train(fixture, "--help");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Usage: train", output);
    }

    /// <summary>Proves a bad command line is a usage error that names the problem.</summary>
    [Theory]
    [InlineData("--bogus", "unknown option")]
    [InlineData("--seed", "needs a value")]
    [InlineData("--model-dir", "needs a value")]
    public void TrainCommand_Run_BadCommandLine_ExitsTwoNamingTheProblem(string argument, string message)
    {
        // Arrange
        using var fixture = new TrainingFixture();

        // Act
        var (exitCode, _, error) = CommandHarness.Train(fixture, argument);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains(message, error);
    }

    /// <summary>Proves an experiment value that is not a number is a usage error.</summary>
    [Fact]
    public void TrainCommand_Run_NonNumericExperimentValue_ExitsTwo()
    {
        // Arrange
        using var fixture = new TrainingFixture();

        // Act
        var (exitCode, _, error) = CommandHarness.Train(fixture, "--model-dir", fixture.ModelDirectory, "--seed", "abc");

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("--seed", error);
    }

    /// <summary>Proves an out-of-range experiment value is a usage error and nothing is written.</summary>
    [Fact]
    public void TrainCommand_Run_ExperimentValueOutOfRange_ExitsTwoAndWritesNothing()
    {
        // Arrange
        using var fixture = new TrainingFixture();

        // Act
        var (exitCode, _, error) = CommandHarness.Train(fixture, "--model-dir", fixture.ModelDirectory, "--hash-bits", "40");

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("hashBits", error);
        Assert.False(Directory.Exists(fixture.ModelDirectory));
    }

    /// <summary>Proves an experiment option without a model folder is refused, and the committed locations are left untouched.</summary>
    [Fact]
    public void TrainCommand_Run_ExperimentOptionWithoutModelDir_ExitsTwoAndLeavesCommittedFilesUntouched()
    {
        // Arrange
        using var fixture = new TrainingFixture();

        // Act
        var (exitCode, _, error) = CommandHarness.Train(fixture, "--seed", "5");

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("--model-dir", error);
        Assert.False(Directory.Exists(Path.Combine(fixture.BaseDirectory, "src")));
        Assert.False(Directory.Exists(Path.Combine(fixture.BaseDirectory, "tools")));
    }

    /// <summary>Proves an experiment option cannot be combined with the check.</summary>
    [Fact]
    public void TrainCommand_Run_ExperimentOptionWithCheck_ExitsTwo()
    {
        // Arrange
        using var fixture = new TrainingFixture();

        // Act
        var (exitCode, _, error) = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory, "--seed", "5");

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("--check", error);
    }

    /// <summary>Proves a missing data folder is a usage error.</summary>
    [Fact]
    public void TrainCommand_Run_MissingDataFolder_ExitsTwo()
    {
        // Arrange
        using var fixture = new TrainingFixture();

        // Act
        var (exitCode, _, error) = CommandHarness.Train(fixture, "--data", Path.Combine(fixture.BaseDirectory, "nowhere"), "--model-dir", fixture.ModelDirectory);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("not found", error);
    }

    /// <summary>Proves training writes a loadable model and a parseable record and reports the choices.</summary>
    [Fact]
    public void TrainCommand_Run_Fixture_WritesLoadableModelAndRecord()
    {
        // Arrange
        using var fixture = new TrainingFixture();

        // Act
        var (exitCode, output, _) = CommandHarness.Train(fixture, "--model-dir", fixture.ModelDirectory, "--hash-bits", "18");

        // Assert
        Assert.Equal(0, exitCode);
        var model = SparsePerceptronModel.Load(File.ReadAllBytes(Path.Combine(fixture.ModelDirectory, Model)));
        var record = TrainingRecord.Parse(File.ReadAllText(Path.Combine(fixture.ModelDirectory, Record)));
        Assert.Equal(18, model.HashBits);
        Assert.Equal(18, record.Training.HashBits);
        Assert.Equal(model.RowCount, record.Result.WeightCount);
        Assert.Contains("image sha256 " + record.Result.ImageSha256, output);
        Assert.Contains("kept " + record.Result.SelectedEpochs + " epochs", output);
    }

    /// <summary>Proves retraining keeps the evaluation and gate settings of an existing record, so retraining cannot move the gate.</summary>
    [Fact]
    public void TrainCommand_Run_ExistingRecord_KeepsItsEvaluationSettings()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        CommandHarness.TrainFixtureModel(fixture);
        var recordPath = Path.Combine(fixture.ModelDirectory, Record);
        File.WriteAllText(recordPath, File.ReadAllText(recordPath).Replace("\"gateMinPooledAccuracyPerThousand\": 930", "\"gateMinPooledAccuracyPerThousand\": 500", StringComparison.Ordinal));

        // Act
        var (exitCode, _, _) = CommandHarness.Train(fixture, "--model-dir", fixture.ModelDirectory, "--hash-bits", "18");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(500, TrainingRecord.Parse(File.ReadAllText(recordPath)).Evaluation.GateMinPooledAccuracyPerThousand);
    }

    /// <summary>Proves without a model folder the committed locations of the repository are written and checked.</summary>
    [Fact]
    public void TrainCommand_Run_DefaultLocations_WritesCommittedFilesThatCheckAccepts()
    {
        // Arrange
        using var fixture = new TrainingFixture();

        // Act
        var (trainExit, _, _) = CommandHarness.Train(fixture);
        var (checkExit, checkOutput, _) = CommandHarness.Train(fixture, "--check");

        // Assert
        Assert.Equal(0, trainExit);
        Assert.True(File.Exists(Path.Combine(fixture.BaseDirectory, "src", "DemaConsulting.Speech.Text.En", "Resources", Model)));
        Assert.True(File.Exists(Path.Combine(fixture.BaseDirectory, "tools", "DemaConsulting.Speech.Text.En.Trainer", Record)));
        Assert.Equal(0, checkExit);
        Assert.Contains("OK", checkOutput);
    }

    /// <summary>Proves a model trained from the data passes the check.</summary>
    [Fact]
    public void TrainCommand_Run_CheckFreshModel_ExitsZero()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        CommandHarness.TrainFixtureModel(fixture);

        // Act
        var (exitCode, output, _) = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("OK", output);
    }

    /// <summary>Proves a model whose file is the same model with an uncompressed payload still passes: only content is compared, never the compressed bytes.</summary>
    [Fact]
    public void TrainCommand_Run_CheckRecompressedButEqualModel_ExitsZero()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        CommandHarness.TrainFixtureModel(fixture);
        var modelPath = Path.Combine(fixture.ModelDirectory, Model);
        File.WriteAllBytes(modelPath, SparsePerceptronModelWriter.ToUncompressedImage(File.ReadAllBytes(modelPath)));

        // Act
        var (exitCode, _, _) = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory);

        // Assert
        Assert.Equal(0, exitCode);
    }

    /// <summary>Proves a change to a training row makes the committed model stale and the message names the data.</summary>
    [Fact]
    public void TrainCommand_Run_CheckAfterTrainingRowChanged_ExitsOneNamingTheData()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        CommandHarness.TrainFixtureModel(fixture);
        fixture.Replace("train.tsv", "They will lead the team today.", "They will lead the crew today.");

        // Act
        var (exitCode, output, _) = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("STALE: the training data changed", output);
        Assert.Contains("FAILED", output);
    }

    /// <summary>Proves a change to a development row makes the committed model stale.</summary>
    [Fact]
    public void TrainCommand_Run_CheckAfterDevRowChanged_ExitsOne()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        CommandHarness.TrainFixtureModel(fixture);
        fixture.Replace("dev.tsv", "We lead the team again.", "We lead the crew again.");

        // Act
        var (exitCode, output, _) = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("training data changed", output);
    }

    /// <summary>Proves a change to a recorded setting makes the committed model stale and the message names the weights.</summary>
    [Fact]
    public void TrainCommand_Run_CheckAfterRecordedSettingChanged_ExitsOneNamingTheWeights()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        CommandHarness.TrainFixtureModel(fixture);
        var recordPath = Path.Combine(fixture.ModelDirectory, Record);
        File.WriteAllText(recordPath, File.ReadAllText(recordPath).Replace("\"scale\": 64", "\"scale\": 32", StringComparison.Ordinal));

        // Act
        var (exitCode, output, _) = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("weights differ", output);
    }

    /// <summary>Proves a committed model file that holds other weights is stale.</summary>
    [Fact]
    public void TrainCommand_Run_CheckAfterModelFileReplaced_ExitsOneNamingTheModelFile()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        CommandHarness.TrainFixtureModel(fixture);
        File.WriteAllBytes(Path.Combine(fixture.ModelDirectory, Model), SparsePerceptronModelWriter.Compile(18, ReadingInventory.CurrentVersion, 1f, 64f, []));

        // Act
        var (exitCode, output, _) = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("committed model file differs", output);
    }

    /// <summary>Proves a corrupt or missing model file is stale, not an error.</summary>
    [Fact]
    public void TrainCommand_Run_CheckCorruptOrMissingModelFile_ExitsOne()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        CommandHarness.TrainFixtureModel(fixture);
        var modelPath = Path.Combine(fixture.ModelDirectory, Model);
        File.WriteAllBytes(modelPath, [1, 2, 3]);

        // Act
        var corrupt = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory);
        File.Delete(modelPath);
        var missing = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory);

        // Assert
        Assert.Equal(1, corrupt.ExitCode);
        Assert.Contains("not a valid model", corrupt.Output);
        Assert.Equal(1, missing.ExitCode);
        Assert.Contains("is missing", missing.Output);
    }

    /// <summary>Proves a missing or unreadable record is stale.</summary>
    [Fact]
    public void TrainCommand_Run_CheckMissingOrInvalidRecord_ExitsOne()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        CommandHarness.TrainFixtureModel(fixture);
        var recordPath = Path.Combine(fixture.ModelDirectory, Record);
        File.WriteAllText(recordPath, "{ broken");

        // Act
        var invalid = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory);
        File.Delete(recordPath);
        var missing = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory);

        // Assert
        Assert.Equal(1, invalid.ExitCode);
        Assert.Contains("not valid", invalid.Output);
        Assert.Equal(1, missing.ExitCode);
        Assert.Contains("record", missing.Output);
    }

    /// <summary>Proves a record that names another tagger version makes the model stale and the message names the tagger.</summary>
    [Fact]
    public void TrainCommand_Run_CheckAfterTaggerVersionChanged_ExitsOneNamingTheTagger()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        CommandHarness.TrainFixtureModel(fixture);
        var recordPath = Path.Combine(fixture.ModelDirectory, Record);
        File.WriteAllText(recordPath, File.ReadAllText(recordPath).Replace(TrainingCorpus.TaggerVersion, "0.0.1", StringComparison.Ordinal));

        // Act
        var (exitCode, output, _) = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("tagger changed", output);
    }

    /// <summary>Proves a data error while checking (a label that is not a candidate) is a usage or input error, not staleness.</summary>
    [Fact]
    public void TrainCommand_Run_CheckWithDataError_ExitsTwo()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        CommandHarness.TrainFixtureModel(fixture);
        fixture.Replace("train.tsv", "syn-v000\tlead\tlead.verb", "syn-v000\tlead\twind.noun");

        // Act
        var (exitCode, _, error) = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("syn-v000", error);
    }

    /// <summary>Proves a row whose labeled reading is not a candidate of its form stops training with exit two and names the row.</summary>
    [Fact]
    public void TrainCommand_Run_LabelNotACandidate_ExitsTwoNamingTheRow()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        fixture.Replace("train.tsv", "syn-v000\tlead\tlead.verb", "syn-v000\tlead\twind.noun");

        // Act
        var (exitCode, _, error) = CommandHarness.Train(fixture, "--model-dir", fixture.ModelDirectory);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("syn-v000", error);
        Assert.False(File.Exists(Path.Combine(fixture.ModelDirectory, Model)));
    }
}
