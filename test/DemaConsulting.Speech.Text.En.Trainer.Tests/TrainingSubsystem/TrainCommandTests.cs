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
    [InlineData("--seed abc", "non-negative integer")]
    public void TrainCommand_Run_BadCommandLine_ExitsTwoNamingTheProblem(string argument, string message)
    {
        // Arrange
        using var fixture = new TrainingFixture();

        // Act
        var (exitCode, _, error) = CommandHarness.Train(fixture, argument.Split(' '));

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains(message, error);
    }

    /// <summary>Proves an out-of-range experiment value, an experiment option without a model folder and an experiment option combined with the check are usage errors that write nothing.</summary>
    [Theory]
    [InlineData("--model-dir {model} --hash-bits 40", "hashBits")]
    [InlineData("--seed 5", "--model-dir")]
    [InlineData("--check --model-dir {model} --seed 5", "--check")]
    public void TrainCommand_Run_ExperimentOptionMisuse_ExitsTwoAndWritesNothing(string arguments, string message)
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var args = arguments.Split(' ').Select(argument => argument == "{model}" ? fixture.ModelDirectory : argument).ToArray();

        // Act
        var (exitCode, _, error) = CommandHarness.Train(fixture, args);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains(message, error);
        Assert.False(Directory.Exists(fixture.ModelDirectory));
        Assert.False(Directory.Exists(Path.Combine(fixture.BaseDirectory, "src")));
        Assert.False(Directory.Exists(Path.Combine(fixture.BaseDirectory, "tools")));
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

    /// <summary>Proves a model trained from the data passes the check, also when its file is the same model with an uncompressed payload: only content is compared, never the compressed bytes.</summary>
    [Fact]
    public void TrainCommand_Run_CheckRecompressedButEqualModel_ExitsZero()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        CommandHarness.TrainFixtureModel(fixture);
        var fresh = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory);
        var modelPath = Path.Combine(fixture.ModelDirectory, Model);
        File.WriteAllBytes(modelPath, SparsePerceptronModelWriter.ToUncompressedImage(File.ReadAllBytes(modelPath)));

        // Act
        var (exitCode, output, _) = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory);

        // Assert
        Assert.Equal(0, fresh.ExitCode);
        Assert.Contains("OK", fresh.Output);
        Assert.Equal(0, exitCode);
        Assert.Contains("OK", output);
    }

    /// <summary>Proves a change to the training data, to a recorded setting or to the tagger version makes the committed model stale and the message names the cause.</summary>
    [Theory]
    [InlineData("train.tsv", "They will lead the team today.", "They will lead the crew today.", "STALE: the training data changed")]
    [InlineData("record", "\"scale\": 64", "\"scale\": 32", "weights differ")]
    [InlineData("record", "{tagger}", "0.0.1", "tagger changed")]
    public void TrainCommand_Run_CheckAfterInputOrRecordChanged_ExitsOneNamingTheCause(string target, string original, string replacement, string message)
    {
        // Arrange
        using var fixture = new TrainingFixture();
        CommandHarness.TrainFixtureModel(fixture);
        var from = original.Replace("{tagger}", TrainingCorpus.TaggerVersion, StringComparison.Ordinal);
        if (target == "record")
        {
            var recordPath = Path.Combine(fixture.ModelDirectory, Record);
            File.WriteAllText(recordPath, File.ReadAllText(recordPath).Replace(from, replacement, StringComparison.Ordinal));
        }
        else
        {
            fixture.Replace(target, from, replacement);
        }

        // Act
        var (exitCode, output, _) = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains(message, output);
    }

    /// <summary>Proves a committed model file that holds other weights, is corrupt or is missing is stale, not an error.</summary>
    [Fact]
    public void TrainCommand_Run_CheckAfterModelFileReplaced_ExitsOneNamingTheModelFile()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        CommandHarness.TrainFixtureModel(fixture);
        var modelPath = Path.Combine(fixture.ModelDirectory, Model);
        File.WriteAllBytes(modelPath, SparsePerceptronModelWriter.Compile(18, ReadingInventory.CurrentVersion, 1f, 64f, []));

        // Act
        var replaced = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory);
        File.WriteAllBytes(modelPath, [1, 2, 3]);
        var corrupt = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory);
        File.Delete(modelPath);
        var missing = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory);

        // Assert
        Assert.Equal(1, replaced.ExitCode);
        Assert.Contains("committed model file differs", replaced.Output);
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
}
