using System.Security.Cryptography;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests;

/// <summary>
///     Subsystem tests for the TrainingSubsystem: the units working together through the <c>train</c> and
///     <c>evaluate</c> commands on fixture data, observed only through exit codes, written files, the loaded model
///     and the analyzer that uses it.
/// </summary>
public sealed class TrainingSubsystemTests
{
    /// <summary>
    ///     Builds an analyzer that uses a model image and the fixture's inventory.
    /// </summary>
    /// <param name="fixture">The fixture.</param>
    /// <param name="image">The model file bytes.</param>
    /// <returns>The analyzer.</returns>
    private static EnglishTextAnalyzer AnalyzerFor(TrainingFixture fixture, byte[] image)
    {
        var corpus = TrainingCorpus.Load(fixture.Root);
        var provider = new HeteronymModelProvider(() => new MemoryStream(image, writable: false), corpus.Inventory);
        return new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions(), PartOfSpeechAdapter.Shared, null, provider);
    }

    /// <summary>
    ///     Gets the reading the analyzer emits on the first occurrence of a word.
    /// </summary>
    /// <param name="analyzer">The analyzer.</param>
    /// <param name="sentence">The sentence.</param>
    /// <param name="word">The word.</param>
    /// <returns>The reading id, or null when none is emitted.</returns>
    private static string? ReadingOf(EnglishTextAnalyzer analyzer, string sentence, string word)
    {
        var start = sentence.IndexOf(word, StringComparison.Ordinal);
        var result = analyzer.Analyze(sentence, TestContext.Current.CancellationToken);
        foreach (var annotation in result.Annotations)
        {
            if (annotation.Start == start && annotation.Length == word.Length && TextEnAnnotations.TryReadReading(annotation, out var id))
            {
                return id;
            }
        }

        return null;
    }

    /// <summary>Proves training on a tiny fixture yields a model the runtime loads, that carries the provenance of its inputs and classifies clear sentences of the trained patterns.</summary>
    [Fact]
    public void TrainingSubsystem_Train_TinyFixture_ProducesLoadableModelThatClassifiesTrainingPatterns()
    {
        // Arrange
        using var fixture = new TrainingFixture();

        // Act
        var (exitCode, _, _) = CommandHarness.Train(fixture, "--model-dir", fixture.ModelDirectory, "--hash-bits", "18");
        var image = File.ReadAllBytes(Path.Combine(fixture.ModelDirectory, TrainCommand.ModelFileName));
        var model = SparsePerceptronModel.Load(image);
        var analyzer = AnalyzerFor(fixture, image);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(ReadingInventory.CurrentVersion, model.InventoryVersion);
        Assert.Equal(HeteronymFeatureExtractor.FeatureSetVersion, model.FeatureSetVersion);
        Assert.Equal("lead.verb", ReadingOf(analyzer, "You will lead the class today.", "lead"));
        Assert.Equal("lead.noun", ReadingOf(analyzer, "The lead roof was very heavy.", "lead"));
    }

    /// <summary>Proves training the same inputs twice gives byte-identical uncompressed images and identical records.</summary>
    [Fact]
    public void TrainingSubsystem_Train_SameInputsTwice_ProduceIdenticalUncompressedImagesAndRecords()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var first = Path.Combine(fixture.BaseDirectory, "first");
        var second = Path.Combine(fixture.BaseDirectory, "second");

        // Act
        CommandHarness.Train(fixture, "--model-dir", first, "--hash-bits", "18");
        CommandHarness.Train(fixture, "--model-dir", second, "--hash-bits", "18");

        // Assert
        Assert.Equal(
            SparsePerceptronModelWriter.ToUncompressedImage(File.ReadAllBytes(Path.Combine(first, TrainCommand.ModelFileName))),
            SparsePerceptronModelWriter.ToUncompressedImage(File.ReadAllBytes(Path.Combine(second, TrainCommand.ModelFileName))));
        Assert.Equal(
            File.ReadAllText(Path.Combine(first, TrainCommand.RecordFileName)),
            File.ReadAllText(Path.Combine(second, TrainCommand.RecordFileName)));
    }

    /// <summary>
    ///     Proves the fixture model has one pinned content hash. The same hash must come out on every operating
    ///     system of the build matrix, so a platform dependence of the tagger, the feature extractor or the training
    ///     fails this test on that platform.
    /// </summary>
    [Fact]
    public void TrainingSubsystem_Train_FixtureImage_MatchesPinnedHash()
    {
        // Arrange
        using var fixture = new TrainingFixture();

        // Act
        CommandHarness.Train(fixture, "--model-dir", fixture.ModelDirectory, "--hash-bits", "18");
        var uncompressed = SparsePerceptronModelWriter.ToUncompressedImage(File.ReadAllBytes(Path.Combine(fixture.ModelDirectory, TrainCommand.ModelFileName)));

        // Assert
        Assert.Equal("f55b595785bce2078cc5cd28a09c493c6ed0da305d4917f2bf89d7ac94766566", Convert.ToHexString(SHA256.HashData(uncompressed)).ToLowerInvariant());
    }

    /// <summary>Proves the holdout sets have no influence on the model: absent, altered or replaced, the image is the same.</summary>
    [Fact]
    public void TrainingSubsystem_Train_HoldoutFilesAbsentOrAltered_ImageUnchanged()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        CommandHarness.Train(fixture, "--model-dir", Path.Combine(fixture.BaseDirectory, "a"), "--hash-bits", "18");

        // Act: delete one holdout, fill the other with garbage and delete the provenance file the holdouts are clustered by
        File.Delete(fixture.PathOf("holdout-natural.tsv"));
        fixture.Write("holdout-independent.tsv", "garbage that is not a data file\n");
        File.Delete(fixture.PathOf("harvest-provenance.tsv"));
        var (exitCode, _, _) = CommandHarness.Train(fixture, "--model-dir", Path.Combine(fixture.BaseDirectory, "b"), "--hash-bits", "18");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(
            File.ReadAllBytes(Path.Combine(fixture.BaseDirectory, "a", TrainCommand.ModelFileName)),
            File.ReadAllBytes(Path.Combine(fixture.BaseDirectory, "b", TrainCommand.ModelFileName)));
        Assert.Equal(
            File.ReadAllText(Path.Combine(fixture.BaseDirectory, "a", TrainCommand.RecordFileName)),
            File.ReadAllText(Path.Combine(fixture.BaseDirectory, "b", TrainCommand.RecordFileName)));
    }

    /// <summary>Proves a training row whose target is not a runtime token is skipped and counted in the record while training still succeeds.</summary>
    [Fact]
    public void TrainingSubsystem_Train_UnalignedRow_SkippedAndCountedInRecord()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        fixture.AppendRow("train.tsv", "syn-unaligned", "lead.noun", "The leader spoke softly.", "lead", "synthetic");

        // Act
        var (exitCode, _, _) = CommandHarness.Train(fixture, "--model-dir", fixture.ModelDirectory, "--hash-bits", "18");

        // Assert
        var record = TrainingRecord.Parse(File.ReadAllText(Path.Combine(fixture.ModelDirectory, TrainCommand.RecordFileName)));
        Assert.Equal(0, exitCode);
        Assert.Equal(1, record.Inputs.TrainSkippedUnaligned);
        Assert.Equal(2, record.Inputs.TrainSkippedSingleCandidate);
        Assert.Equal(34, record.Inputs.TrainUsed);
        Assert.Equal(37, record.Inputs.TrainRows);
    }

    /// <summary>Proves a label that is not a candidate of its form stops training with a data error and no model.</summary>
    [Fact]
    public void TrainingSubsystem_Train_LabelNotACandidate_ExitsTwoNamingTheRow()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        fixture.Replace("dev.tsv", "harvest-d00\tlead\tlead.verb", "harvest-d00\tlead\twind.noun");

        // Act
        var (exitCode, _, error) = CommandHarness.Train(fixture, "--model-dir", fixture.ModelDirectory);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("harvest-d00", error);
        Assert.False(Directory.Exists(fixture.ModelDirectory));
    }

    /// <summary>Proves a freshly trained model is current and each kind of change to its inputs, record or file makes it stale.</summary>
    [Fact]
    public void TrainingSubsystem_TrainCheck_FreshModelIsCurrentAndChangesMakeItStale()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        CommandHarness.TrainFixtureModel(fixture);

        // Act
        var fresh = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory);
        fixture.Replace("train.tsv", "They will lead the team today.", "They will lead the crew today.");
        var staleData = CommandHarness.Train(fixture, "--check", "--model-dir", fixture.ModelDirectory);

        // Assert
        Assert.Equal(0, fresh.ExitCode);
        Assert.Equal(1, staleData.ExitCode);
        Assert.Contains("STALE: the training data changed", staleData.Output);
    }

    /// <summary>Proves the trainer and the analyzer decide every development and holdout row of the fixture identically.</summary>
    [Fact]
    public void TrainingSubsystem_Evaluate_TrainerAndAnalyzerPredictions_AgreeOnEveryFixtureRow()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);

        // Act
        var (exitCode, output, _) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--set", "all");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("set dev:", output);
        Assert.Contains("set holdout-natural:", output);
        Assert.Contains("set holdout-independent:", output);
        Assert.Equal(3, output.Split('\n').Count(line => line.Contains("parity:", StringComparison.Ordinal) && line.Contains(" 0 mismatched, 0 suppressed", StringComparison.Ordinal)));
    }

    /// <summary>Proves evaluation computes accuracy, the three baselines, coverage and the improvement on fixture data with exact figures.</summary>
    [Fact]
    public void TrainingSubsystem_Evaluate_FixtureSets_ComputesAccuracyBaselinesAndCoverage()
    {
        // Arrange
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);

        // Act
        var (exitCode, output, _) = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--set", "holdout-natural", "--set", "holdout-independent");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("set holdout-natural: 40 rows, 40 aligned, 0 single-candidate, 40 covered", output);
        Assert.Contains("model accuracy        97.50% (39/40)", output);
        Assert.Contains("own-UPOS baseline     52.50% (21/40)", output);
        Assert.Contains("majority baseline     52.50% (21/40)", output);
        Assert.Contains("set holdout-independent: 21 rows, 20 aligned, 0 single-candidate, 20 covered", output);
        Assert.Contains("model accuracy        95.24% (20/21)", output);
    }

    /// <summary>Proves the quality gate decides from the thresholds of the record: pass above them, fail below.</summary>
    [Fact]
    public void TrainingSubsystem_Evaluate_Gate_ExitCodeFollowsThresholds()
    {
        // Arrange
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);

        // Act
        CommandHarness.SetGateThresholds(modelDirectory, ("holdout-natural", 9500));
        var passing = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--gate");
        CommandHarness.SetGateThresholds(modelDirectory, ("holdout-natural", 9800));
        var failing = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--gate");

        // Assert: the model decides 97.5% of the natural holdout
        Assert.Equal(0, passing.ExitCode);
        Assert.Equal(1, failing.ExitCode);
    }

    /// <summary>Proves the evaluation record round trips: written, then current, then stale after an edit.</summary>
    [Fact]
    public void TrainingSubsystem_Evaluate_WriteThenCheck_MatchesAndDetectsEditedRecord()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        var record = Path.Combine(fixture.BaseDirectory, "record");
        CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--write", "--record-dir", record);

        // Act
        var fresh = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--check", "--record-dir", record);
        File.WriteAllText(Path.Combine(record, EvaluationRecord.SeedsFile), "seed\n");
        var stale = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--check", "--record-dir", record);

        // Assert
        Assert.Equal(0, fresh.ExitCode);
        Assert.Equal(1, stale.ExitCode);
        Assert.Contains("seeds.tsv", stale.Output);
    }

    /// <summary>Proves the overlays are evaluated with and without the cue priors and appended to the evaluation record after the data sets, which then checks as current.</summary>
    [Fact]
    public void TrainingSubsystem_Evaluate_Overlays_ReportedWithAndWithoutCuesAndAppendedToRecord()
    {
        // Arrange
        using var fixture = new TrainingFixture("ADV", "ADV");
        var modelDirectory = CommandHarness.TrainFixtureModel(fixture);
        fixture.WriteRegression(
            ("reg-0001", "lead.verb", "They will lead the team today.", "listening", "yes"),
            ("reg-0002", "lead.noun", "The lead box was very heavy.", "data-row", "no"));
        fixture.WriteGeneralization(
            ("gen-0001", "lead.verb", "Please lead the hike now.", "novel-construction", "lead:request", "yes"),
            ("gen-0002", "lead.noun", "A lead ball fell down.", "novel-vocabulary", "lead:metal", "no"));
        var record = Path.Combine(fixture.BaseDirectory, "record");

        // Act
        var report = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--set", "regression", "--set", "generalization");
        var write = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--write", "--record-dir", record);
        var check = CommandHarness.Evaluate(fixture, "--model-dir", modelDirectory, "--check", "--record-dir", record);

        // Assert
        Assert.Equal(0, report.ExitCode);
        Assert.Contains("regression slice unseen: 1 rows", report.Output);
        Assert.Contains("generalization slice cue_unsolvable:yes: 1 rows", report.Output);
        Assert.Contains("model only", report.Output);
        Assert.Equal(0, write.ExitCode);
        Assert.Equal(0, check.ExitCode);
        var summary = File.ReadAllLines(Path.Combine(record, EvaluationRecord.SummaryFile));
        Assert.Equal(new[] { "set", "dev", "holdout-independent", "holdout-natural", "generalization", "regression" }, summary.Select(static line => line.Split('\t')[0]));
        Assert.Equal(5, File.ReadAllLines(Path.Combine(record, EvaluationRecord.OverlayRowsFile)).Length);
    }

    /// <summary>Proves the tool's entry point dispatches the train and evaluate commands and still reports an unknown command.</summary>
    [Fact]
    public void TrainingSubsystem_Program_DispatchesTrainAndEvaluate()
    {
        // Arrange
        var trainOutput = new StringWriter();
        var evaluateOutput = new StringWriter();
        var unknownError = new StringWriter();

        // Act
        var train = Program.Run(["train", "--help"], trainOutput, TextWriter.Null);
        var evaluate = Program.Run(["evaluate", "--help"], evaluateOutput, TextWriter.Null);
        var unknown = Program.Run(["bogus"], TextWriter.Null, unknownError);

        // Assert
        Assert.Equal(0, train);
        Assert.Contains("Usage: train", trainOutput.ToString());
        Assert.Equal(0, evaluate);
        Assert.Contains("Usage: evaluate", evaluateOutput.ToString());
        Assert.Equal(2, unknown);
        Assert.Contains("train", unknownError.ToString());
    }

    /// <summary>Proves the committed model and training record are exactly what retraining from the committed data produces.</summary>
    [Fact]
    public void TrainingSubsystem_TrainCheck_CommittedModelAndRecord_AreCurrent()
    {
        // Arrange
        var root = FindRepositoryRoot();
        Assert.SkipUnless(root is not null, "The repository root (Speech.slnx) was not found above the test binaries.");
        var output = new StringWriter();
        var error = new StringWriter();

        // Act
        var exitCode = TrainCommand.Run(["--check"], output, error, root, root);

        // Assert: any difference fails the test and its message names what is stale
        Assert.True(exitCode == 0, output.ToString() + error);
    }

    /// <summary>Proves the committed model meets the quality gate on the natural holdout, end to end through the analyzer that embeds it, and the committed evaluation record is exactly what that evaluation produces.</summary>
    [Fact]
    public void TrainingSubsystem_Evaluate_CommittedModelAndData_PassesGate()
    {
        // Arrange
        var root = FindRepositoryRoot();
        Assert.SkipUnless(root is not null, "The repository root (Speech.slnx) was not found above the test binaries.");
        var output = new StringWriter();
        var error = new StringWriter();

        // Act
        var exitCode = EvaluateCommand.Run(["--gate", "--check"], output, error, root, root);

        // Assert
        Assert.True(exitCode == 0, output.ToString() + error);
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
