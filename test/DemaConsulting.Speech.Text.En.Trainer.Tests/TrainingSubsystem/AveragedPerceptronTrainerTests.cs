using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingSubsystem;

/// <summary>
///     Unit tests for <see cref="AveragedPerceptronTrainer"/>: the learning rule, the exact averaging and
///     quantization of the weights, pruning and repeatability, observed through the weights it reports.
/// </summary>
public sealed class AveragedPerceptronTrainerTests
{
    /// <summary>Settings small enough to train in a moment.</summary>
    private static readonly TrainingSettings Small = TrainingSettings.Default with { HashBits = 18, PruneThreshold = 0, NaturalWeight = 1, MaxEpochs = 1 };

    /// <summary>
    ///     Trains and captures a copy of the weights after every epoch.
    /// </summary>
    /// <param name="corpus">The corpus.</param>
    /// <param name="settings">The settings.</param>
    /// <returns>The weights after each epoch.</returns>
    private static List<short[]> TrainEpochs(TrainingCorpus corpus, TrainingSettings settings)
    {
        var epochs = new List<short[]>();
        new AveragedPerceptronTrainer(corpus, settings).Train(epoch => epochs.Add((short[])epoch.Weights.Clone()));
        return epochs;
    }

    /// <summary>Proves one mistake on one row gives exactly the averaged update: the gold candidate's buckets at plus the scale, the predicted candidate's at minus it, and a total of zero.</summary>
    [Fact]
    public void AveragedPerceptronTrainer_Train_SingleMistakenRow_GivesExactAveragedWeights()
    {
        // Arrange: one training row labeled with the second candidate, which loses the initial tie
        using var fixture = new TrainingFixture();
        fixture.Write("train.tsv", string.Join('\n', fixture.Read("train.tsv").Split('\n').Take(1)) + "\n");
        fixture.AppendRow("train.tsv", "syn-only", "lead.noun", "The lead pipe was very heavy.", "lead", "synthetic");
        var corpus = TrainingCorpus.Load(fixture.Root);

        // Act
        var weights = TrainEpochs(corpus, Small).Single();

        // Assert: after one step T = 1, W = +-1 and U = +-1, so the average is +-1 and the quantized weight is +-64
        var nonZero = weights.Where(weight => weight != 0).ToList();
        Assert.NotEmpty(nonZero);
        Assert.All(nonZero, weight => Assert.Equal(0, weight % 64));
        Assert.Contains((short)64, nonZero);
        Assert.Contains((short)-64, nonZero);
        Assert.Equal(0, weights.Sum(weight => (int)weight));
    }

    /// <summary>Proves that after training on the row it got wrong, the weights decide that row for its label.</summary>
    [Fact]
    public void AveragedPerceptronTrainer_Train_SingleMistakenRow_WeightsThenDecideTheLabel()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        fixture.Write("train.tsv", string.Join('\n', fixture.Read("train.tsv").Split('\n').Take(1)) + "\n");
        fixture.AppendRow("train.tsv", "syn-only", "lead.noun", "The lead pipe was very heavy.", "lead", "synthetic");
        var corpus = TrainingCorpus.Load(fixture.Root);
        var weights = TrainEpochs(corpus, Small).Single();
        var row = corpus.Train.Single();
        var scores = new int[2];

        // Act
        var decided = AveragedPerceptronTrainer.Decide(corpus.Inventory, row, new ShortWeights(weights), (1u << 18) - 1, scores);

        // Assert
        Assert.Equal(row.LabelIndex, row.Candidates[decided]);
        Assert.True(scores[1] > scores[0]);
    }

    /// <summary>Proves training on separable rows ends with weights that decide every training row correctly.</summary>
    [Fact]
    public void AveragedPerceptronTrainer_Train_SeparableFixture_DecidesEveryTrainingRow()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var corpus = TrainingCorpus.Load(fixture.Root);

        // Act
        var epochs = TrainEpochs(corpus, Small with { MaxEpochs = 8 });
        var weights = epochs[^1];

        // Assert
        var scores = new int[2];
        foreach (var row in corpus.Train.Where(row => row.IsAligned && row.IsAmbiguous))
        {
            var decided = AveragedPerceptronTrainer.Decide(corpus.Inventory, row, new ShortWeights(weights), (1u << 18) - 1, scores);
            Assert.Equal(row.LabelIndex, row.Candidates[decided]);
        }
    }

    /// <summary>Proves every epoch is reported once, in order, with its own weights.</summary>
    [Fact]
    public void AveragedPerceptronTrainer_Train_ReportsEachEpochInOrder()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var corpus = TrainingCorpus.Load(fixture.Root);
        var numbers = new List<int>();

        // Act
        new AveragedPerceptronTrainer(corpus, Small with { MaxEpochs = 4 }).Train(epoch => numbers.Add(epoch.Epoch));

        // Assert
        Assert.Equal([1, 2, 3, 4], numbers);
    }

    /// <summary>Proves the same seed and settings give identical weights after every epoch.</summary>
    [Fact]
    public void AveragedPerceptronTrainer_Train_SameSeedTwice_GivesIdenticalWeights()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var corpus = TrainingCorpus.Load(fixture.Root);
        var settings = Small with { MaxEpochs = 3, Seed = 11 };

        // Act
        var first = TrainEpochs(corpus, settings);
        var second = TrainEpochs(corpus, settings);

        // Assert
        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i], second[i]);
        }
    }

    /// <summary>Proves a prune threshold above every weight leaves no weight, while no pruning leaves some.</summary>
    [Fact]
    public void AveragedPerceptronTrainer_Train_PruneThreshold_DropsSmallWeights()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var corpus = TrainingCorpus.Load(fixture.Root);

        // Act
        var unpruned = TrainEpochs(corpus, Small).Single();
        var pruned = TrainEpochs(corpus, Small with { PruneThreshold = 1000 }).Single();

        // Assert
        Assert.Contains(unpruned, weight => weight != 0);
        Assert.All(pruned, weight => Assert.Equal(0, weight));
    }

    /// <summary>Proves the weight given to natural rows changes what is learned when the training file has natural rows.</summary>
    [Fact]
    public void AveragedPerceptronTrainer_Train_NaturalWeight_ChangesTheWeights()
    {
        // Arrange: natural training rows, some contradicting the synthetic patterns
        using var fixture = new TrainingFixture();
        fixture.AppendRow("train.tsv", "harvest-t1", "lead.noun", "They will lead the team today.", "lead", "harvest-masc");
        fixture.AppendRow("train.tsv", "harvest-t2", "lead.verb", "The lead pipe was very heavy.", "lead", "harvest-masc");
        var corpus = TrainingCorpus.Load(fixture.Root);

        // Act
        var once = TrainEpochs(corpus, Small with { NaturalWeight = 1 }).Single();
        var thrice = TrainEpochs(corpus, Small with { NaturalWeight = 3 }).Single();

        // Assert
        Assert.NotEqual(once, thrice);
    }

    /// <summary>Proves rows that are not aligned or have one candidate carry no signal: adding them does not change the weights.</summary>
    [Fact]
    public void AveragedPerceptronTrainer_Train_UnalignedAndSingleCandidateRows_AreIgnored()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var baseline = TrainEpochs(TrainingCorpus.Load(fixture.Root), Small).Single();
        fixture.AppendRow("train.tsv", "syn-unaligned", "lead.noun", "The leader spoke softly.", "lead", "synthetic");
        fixture.AppendRow("train.tsv", "syn-w3", "wind.noun", "A strong wind blew.", "wind", "synthetic");

        // Act
        var withExtras = TrainEpochs(TrainingCorpus.Load(fixture.Root), Small).Single();

        // Assert
        Assert.Equal(baseline, withExtras);
    }

    /// <summary>Proves settings out of their range are rejected before any training.</summary>
    [Fact]
    public void AveragedPerceptronTrainer_Constructor_InvalidSettings_ThrowsInvalidDataException()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var corpus = TrainingCorpus.Load(fixture.Root);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => new AveragedPerceptronTrainer(corpus, Small with { HashBits = 40 }));
        Assert.Throws<InvalidDataException>(() => new AveragedPerceptronTrainer(corpus, Small with { NaturalWeight = 0 }));
        Assert.Throws<ArgumentNullException>(() => new AveragedPerceptronTrainer(null!, Small));
    }
}
