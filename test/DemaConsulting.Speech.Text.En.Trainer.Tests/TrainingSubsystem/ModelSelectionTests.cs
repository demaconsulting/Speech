using System.Security.Cryptography;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingSubsystem;

/// <summary>
///     Unit tests for <see cref="ModelSelection"/>: the development-set choices of epoch count and calibration
///     temperature, and the model images built from them.
/// </summary>
public sealed class ModelSelectionTests
{
    /// <summary>Settings small enough to train in a moment.</summary>
    private static readonly TrainingSettings Small = TrainingSettings.Default with { HashBits = 18, PruneThreshold = 0, NaturalWeight = 1, MaxEpochs = 6 };

    /// <summary>Proves the kept epoch is the first one that decides the most natural development rows correctly.</summary>
    [Fact]
    public void ModelSelection_Train_Fixture_KeepsTheEarliestEpochWithTheMostNaturalDevRowsCorrect()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var corpus = TrainingCorpus.Load(fixture.Root);

        // Act
        var model = ModelSelection.Train(corpus, Small);

        // Assert
        var best = model.Epochs.Max(epoch => epoch.NaturalCorrect);
        Assert.Equal(6, model.Epochs.Count);
        Assert.Equal(model.Epochs.First(epoch => epoch.NaturalCorrect == best).Epoch, model.Result.SelectedEpochs);
        Assert.Equal(best, model.Result.DevNaturalCorrect);
        Assert.Equal(8, model.Result.DevNaturalRows);
        Assert.Equal(8, model.Result.DevAllRows);
    }

    /// <summary>Proves the chosen temperature is a value of the grid and is the one stored in the image.</summary>
    [Fact]
    public void ModelSelection_Train_Fixture_ChoosesAGridTemperatureAndStoresIt()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var corpus = TrainingCorpus.Load(fixture.Root);

        // Act
        var model = ModelSelection.Train(corpus, Small);

        // Assert
        Assert.InRange(model.Result.TemperatureStep, 1, 1000);
        Assert.Equal((float)model.Result.TemperatureStep / 20, model.Result.Temperature);
        Assert.Equal(model.Result.Temperature, SparsePerceptronModel.Load(model.CompressedImage).Temperature);
    }

    /// <summary>Proves the calibration temperature is the grid value that minimizes the development loss: no other grid value gives a smaller loss.</summary>
    [Fact]
    public void ModelSelection_Train_Fixture_TemperatureMinimizesTheLossOnTheSelectionRows()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var corpus = TrainingCorpus.Load(fixture.Root);

        // Act
        var model = ModelSelection.Train(corpus, Small);
        var chosen = Loss(corpus, model, model.Result.TemperatureStep);

        // Assert: a nearby and a far temperature are no better
        foreach (var step in new[] { 1, 5, 20, 100, 1000, model.Result.TemperatureStep - 1, model.Result.TemperatureStep + 1 }.Where(step => step is >= 1 and <= 1000))
        {
            Assert.True(Loss(corpus, model, step) >= chosen - 1e-12, $"step {step} has a smaller loss than the chosen step {model.Result.TemperatureStep}");
        }
    }

    /// <summary>Proves both images load, hold the same weights, and carry the versions, the data hash and the trainer version.</summary>
    [Fact]
    public void ModelSelection_Train_Fixture_ImagesAreEquivalentAndCarryProvenance()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var corpus = TrainingCorpus.Load(fixture.Root);

        // Act
        var model = ModelSelection.Train(corpus, Small);
        var loaded = SparsePerceptronModel.Load(model.CompressedImage);

        // Assert
        Assert.Equal(model.UncompressedImage, SparsePerceptronModelWriter.ToUncompressedImage(model.CompressedImage));
        Assert.Equal(model.Result.WeightCount, loaded.RowCount);
        Assert.Equal(ReadingInventory.CurrentVersion, loaded.InventoryVersion);
        Assert.Equal(HeteronymFeatureExtractor.FeatureSetVersion, loaded.FeatureSetVersion);
        Assert.Equal(ModelSelection.TrainerVersion, loaded.TrainerVersion);
        Assert.Equal(corpus.DataSha256Bytes, loaded.GetDataManifestSha256());
        Assert.Equal(Small.HashBits, loaded.HashBits);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(model.UncompressedImage)).ToLowerInvariant(), model.Result.ImageSha256);
        Assert.Equal(model.UncompressedImage.Length, model.Result.ImageBytes);
    }

    /// <summary>Proves the same corpus and settings give byte-identical uncompressed images.</summary>
    [Fact]
    public void ModelSelection_Train_SameInputsTwice_GivesIdenticalImages()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var corpus = TrainingCorpus.Load(fixture.Root);

        // Act
        var first = ModelSelection.Train(corpus, Small);
        var second = ModelSelection.Train(corpus, Small);

        // Assert
        Assert.Equal(first.UncompressedImage, second.UncompressedImage);
        Assert.Equal(first.Result, second.Result);
    }

    /// <summary>Proves that without natural development rows the choices are made on all development rows.</summary>
    [Fact]
    public void ModelSelection_Train_NoNaturalDevRows_SelectsOnAllDevRows()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        fixture.Write("dev.tsv", string.Join('\n', fixture.Read("dev.tsv").Split('\n').Take(1)) + "\n");
        fixture.AppendRow("dev.tsv", "syn-d1", "lead.verb", "We lead the team again.", "lead", "synthetic");
        fixture.AppendRow("dev.tsv", "syn-d2", "lead.noun", "That lead pipe is old.", "lead", "synthetic");
        var corpus = TrainingCorpus.Load(fixture.Root);

        // Act
        var model = ModelSelection.Train(corpus, Small);

        // Assert
        Assert.Equal(0, model.Result.DevNaturalRows);
        Assert.Equal(2, model.Result.DevAllRows);
        Assert.Equal(model.Result.DevAllCorrect, model.Epochs.Max(epoch => epoch.AllCorrect));
    }

    /// <summary>Proves that without any development row the first epoch is kept and the temperature is one.</summary>
    [Fact]
    public void ModelSelection_Train_NoDevRows_KeepsFirstEpochAtTemperatureOne()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        fixture.Write("dev.tsv", string.Join('\n', fixture.Read("dev.tsv").Split('\n').Take(1)) + "\n");
        var corpus = TrainingCorpus.Load(fixture.Root);

        // Act
        var model = ModelSelection.Train(corpus, Small);

        // Assert
        Assert.Equal(1, model.Result.SelectedEpochs);
        Assert.Equal(1.0f, model.Result.Temperature);
        Assert.Equal(0, model.Result.DevAllRows);
    }

    /// <summary>Proves the inputs record counts the rows used and the rows skipped as not aligned or without signal.</summary>
    [Fact]
    public void ModelSelection_BuildInputs_Fixture_CountsUsedAndSkippedRows()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        fixture.AppendRow("train.tsv", "syn-unaligned", "lead.noun", "The leader spoke softly.", "lead", "synthetic");
        var corpus = TrainingCorpus.Load(fixture.Root);

        // Act
        var inputs = ModelSelection.BuildInputs(corpus);

        // Assert
        Assert.Equal(37, inputs.TrainRows);
        Assert.Equal(34, inputs.TrainUsed);
        Assert.Equal(1, inputs.TrainSkippedUnaligned);
        Assert.Equal(2, inputs.TrainSkippedSingleCandidate);
        Assert.Equal(8, inputs.DevRows);
        Assert.Equal(8, inputs.DevUsed);
        Assert.Equal(corpus.DataSha256, inputs.DataSha256);
        Assert.Equal(corpus.FeaturesSha256, inputs.FeaturesSha256);
        Assert.Equal(TrainingCorpus.TaggerVersion, inputs.TaggerVersion);
        Assert.Equal(ReadingInventory.CurrentVersion, inputs.InventoryVersion);
    }

    /// <summary>Proves invalid settings are rejected.</summary>
    [Fact]
    public void ModelSelection_Train_InvalidSettings_ThrowsInvalidDataException()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var corpus = TrainingCorpus.Load(fixture.Root);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => ModelSelection.Train(corpus, Small with { MaxEpochs = 0 }));
        Assert.Throws<ArgumentNullException>(() => ModelSelection.Train(null!, Small));
    }

    /// <summary>
    ///     Computes the total negative log-likelihood of the natural development rows at a temperature step, from
    ///     the scores of the trained model, with the framework's own math.
    /// </summary>
    /// <param name="corpus">The corpus.</param>
    /// <param name="model">The trained model.</param>
    /// <param name="step">The temperature step.</param>
    /// <returns>The loss.</returns>
    private static double Loss(TrainingCorpus corpus, TrainedModel model, int step)
    {
        var loaded = SparsePerceptronModel.Load(model.UncompressedImage);
        var weights = new LoadedWeights(loaded);
        var temperature = (float)step / 20;
        var divisor = (double)64 * temperature;
        var loss = 0.0;
        foreach (var row in corpus.Dev.Where(row => row.IsAligned && row.IsAmbiguous && row.Row.IsNatural))
        {
            var scores = row.Candidates
                .Select(reading => HeteronymClassifier.ScoreCandidate(weights, row.Conjoined, row.Shared, corpus.Inventory.LabelSalt(reading), corpus.Inventory.RoleSalt(reading), loaded.Mask))
                .ToArray();
            var top = scores.Max();
            var sum = scores.Sum(score => Math.Exp((score - top) / divisor));
            loss += Math.Log(sum) - ((scores[row.Candidates.ToList().IndexOf(row.LabelIndex)] - top) / divisor);
        }

        return loss;
    }

    /// <summary>A loaded model as a weight source.</summary>
    /// <param name="Model">The model.</param>
    private readonly record struct LoadedWeights(SparsePerceptronModel Model) : HeteronymClassifier.IWeightSource
    {
        /// <inheritdoc/>
        public int GetWeight(uint bucket) => Model.GetWeight(bucket);
    }
}
