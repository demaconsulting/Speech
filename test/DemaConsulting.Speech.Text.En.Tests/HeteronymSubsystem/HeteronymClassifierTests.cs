using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests.HeteronymSubsystem;

/// <summary>
///     Unit tests for <see cref="HeteronymClassifier"/> and <see cref="HeteronymModelProvider"/>:
///     candidate-restricted scoring, the tie rule, calibrated confidence, compatibility checks, and the lazy,
///     shared, failure-contained model supply.
/// </summary>
public sealed class HeteronymClassifierTests
{
    /// <summary>The hash bits of the hand-built models; wide so a chance collision between features is negligible.</summary>
    private const int HashBits = 24;

    /// <summary>The fixed-point scale of the hand-built models.</summary>
    private const float Scale = 64f;

    /// <summary>Proves that, with no weights at all, the tie goes to the first candidate in the inventory's row order and the confidence is exactly one half.</summary>
    [Fact]
    public void HeteronymClassifier_Classify_NoWeights_TieGoesToFirstCandidate()
    {
        // Arrange
        var (inventory, classifier) = Create([]);
        var (context, target, form) = Prepare(inventory, "I want to record it .", "record");

        // Act
        var prediction = classifier.Classify(context, target, form);

        // Assert: record.noun is listed before record.verb in forms.tsv
        Assert.Equal("record.noun", inventory.ReadingIdAt(prediction.ReadingIndex));
        Assert.Equal(0.5, prediction.Confidence, 12);
    }

    /// <summary>Proves the confidence is the temperature-calibrated softmax of the top candidate: two candidates 64 apart at scale 64 give 1/(1+e^-1), and a higher temperature lowers it.</summary>
    [Theory]
    [InlineData(1f, 0.7310585786300049)]
    [InlineData(2f, 0.6224593312018546)]
    [InlineData(0.5f, 0.8807970779778823)]
    public void HeteronymClassifier_Classify_TwoCandidates64Apart_ConfidenceIsCalibratedSoftmax(float temperature, double expected)
    {
        // Arrange: weight the bias feature of record.verb by 64 (one unit of real score)
        var inventory = ReadingInventory.Default;
        var (context, target, form) = Prepare(inventory, "I want to record it .", "record");
        var (_, classifier) = Create([BiasWeight(inventory, "record.verb", 64)], temperature);

        // Act
        var prediction = classifier.Classify(context, target, form);

        // Assert
        Assert.Equal("record.verb", inventory.ReadingIdAt(prediction.ReadingIndex));
        Assert.Equal(expected, prediction.Confidence, 12);
    }

    /// <summary>Proves a reading that is not a candidate of the word can never win, however large the weight of its own features.</summary>
    [Fact]
    public void HeteronymClassifier_Classify_NonCandidateWithHugeWeight_NeverWins()
    {
        // Arrange: a huge bias for read.past (not a candidate of "record") and none for the candidates
        var inventory = ReadingInventory.Default;
        var (context, target, form) = Prepare(inventory, "I want to record it .", "record");
        var (_, classifier) = Create([BiasWeight(inventory, "read.past", short.MaxValue)]);

        // Act
        var prediction = classifier.Classify(context, target, form);

        // Assert: the choice is a record reading and the tie rule applies
        Assert.StartsWith("record.", inventory.ReadingIdAt(prediction.ReadingIndex), StringComparison.Ordinal);
        Assert.Equal(0.5, prediction.Confidence, 12);
    }

    /// <summary>Proves a form with three candidates gets a confidence from one third to one, that picks the top-scoring candidate.</summary>
    [Fact]
    public void HeteronymClassifier_Classify_ThreeCandidates_ConfidenceIsInRangeAndTopOne()
    {
        // Arrange: find a form with three candidates and favor its middle one
        var inventory = ReadingInventory.Default;
        var form = Enumerable.Range(0, inventory.FormCount).First(f => inventory.GetCandidates(f).Length == 3);
        var candidates = inventory.GetCandidates(form).ToArray();
        var word = inventory.FormAt(form);
        var (context, target, formIndex) = Prepare(inventory, $"We saw the {word} there .", word);
        var (_, classifier) = Create([BiasWeight(inventory, inventory.ReadingIdAt(candidates[1]), 128)]);

        // Act
        var prediction = classifier.Classify(context, target, formIndex);

        // Assert: softmax with scores 0, 128, 0 at scale 64 and temperature 1 is e^2 / (e^2 + 2)
        Assert.Equal(candidates[1], prediction.ReadingIndex);
        Assert.Equal(Math.Exp(2) / (Math.Exp(2) + 2), prediction.Confidence, 12);
        Assert.InRange(prediction.Confidence, 1.0 / 3, 1.0);
    }

    /// <summary>Proves a single-candidate form returns its reading with confidence 1 without scoring (no observer call).</summary>
    [Fact]
    public void HeteronymClassifier_Classify_SingleCandidateForm_ReturnsConfidenceOneWithoutScoring()
    {
        // Arrange
        var inventory = ReadingInventory.Default;
        var form = Enumerable.Range(0, inventory.FormCount).First(f => inventory.GetCandidates(f).Length == 1);
        var word = inventory.FormAt(form);
        var (context, target, formIndex) = Prepare(inventory, $"We saw the {word} there .", word);
        var calls = 0;
        var (_, classifier) = Create([], observer: _ => calls++);

        // Act
        var prediction = classifier.Classify(context, target, formIndex);

        // Assert
        Assert.Equal(inventory.GetCandidates(form)[0], prediction.ReadingIndex);
        Assert.Equal(1.0, prediction.Confidence);
        Assert.Equal(0, calls);
    }

    /// <summary>Proves the observer receives the running count of model-scored classifications, so a test can act at an exact one.</summary>
    [Fact]
    public void HeteronymClassifier_Classify_Observer_ReceivesRunningCount()
    {
        // Arrange
        var inventory = ReadingInventory.Default;
        var (context, target, form) = Prepare(inventory, "I want to record it .", "record");
        var counts = new List<int>();
        var (_, classifier) = Create([], observer: counts.Add);

        // Act
        classifier.Classify(context, target, form);
        classifier.Classify(context, target, form);
        classifier.Classify(context, target, form);

        // Assert
        Assert.Equal([1, 2, 3], counts);
    }

    /// <summary>Proves a model trained for a newer inventory than the runtime has is rejected, while an older model works with a newer inventory.</summary>
    [Fact]
    public void HeteronymClassifier_Constructor_InventoryVersionCompatibility_IsChecked()
    {
        // Arrange
        var inventory = ReadingInventory.Default;
        var newer = SparsePerceptronModel.Load(SparsePerceptronModelWriter.Compile(HashBits, inventory.Version + 1, 1f, Scale, []));
        var older = SparsePerceptronModel.Load(SparsePerceptronModelWriter.Compile(HashBits, 1, 1f, Scale, []));
        var same = SparsePerceptronModel.Load(SparsePerceptronModelWriter.Compile(HashBits, inventory.Version, 1f, Scale, []));

        // Act / Assert
        var exception = Assert.Throws<InvalidDataException>(() => new HeteronymClassifier(inventory, newer));
        Assert.Contains("newer reading inventory", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(new HeteronymClassifier(inventory, older));
        Assert.NotNull(new HeteronymClassifier(inventory, same));
    }

    /// <summary>Proves null arguments are rejected by the constructor.</summary>
    [Fact]
    public void HeteronymClassifier_Constructor_NullArgument_ThrowsArgumentNullException()
    {
        // Arrange
        var model = SparsePerceptronModel.Load(SparsePerceptronModelWriter.Compile(HashBits, 1, 1f, Scale, []));

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new HeteronymClassifier(null!, model));
        Assert.Throws<ArgumentNullException>(() => new HeteronymClassifier(ReadingInventory.Default, null!));
    }

    /// <summary>Proves a target outside the sentence is rejected for an ambiguous form.</summary>
    [Fact]
    public void HeteronymClassifier_Classify_TargetOutsideSentence_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var inventory = ReadingInventory.Default;
        var (context, _, form) = Prepare(inventory, "I want to record it .", "record");
        var (_, classifier) = Create([]);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => classifier.Classify(context, context.Length, form));
    }

    /// <summary>Proves the classifier is thread-safe and deterministic: many threads classifying the same contexts get the single-threaded results.</summary>
    [Fact]
    public async Task HeteronymClassifier_Classify_ConcurrentCallers_GetSingleThreadedResults()
    {
        // Arrange: a real model trained on the record examples and four contexts
        var inventory = ReadingInventory.Default;
        var model = SparsePerceptronModel.Load(HeteronymFixtureModel.Build(HeteronymFixtureModel.RecordExamples));
        var classifier = new HeteronymClassifier(inventory, model);
        string[] sentences = ["I want to record a record.", "They will record the meeting.", "This record is old.", "Nobody keeps a record now."];
        var work = sentences
            .Select(sentence =>
            {
                var (context, words) = HeteronymFixtureModel.Describe(sentence);
                var target = Array.FindIndex(words, word => word.Equals("record", StringComparison.OrdinalIgnoreCase));
                Assert.True(inventory.TryFindForm("record", out var form));
                return (context, target, form);
            })
            .ToArray();
        var expected = work.Select(item => classifier.Classify(item.context, item.target, item.form)).ToArray();

        // Act: 16 threads, 200 rounds each
        var tasks = Enumerable.Range(0, 16).Select(_ => Task.Run(
            () =>
            {
                for (var round = 0; round < 200; round++)
                {
                    for (var i = 0; i < work.Length; i++)
                    {
                        var prediction = classifier.Classify(work[i].context, work[i].target, work[i].form);
                        if (prediction != expected[i])
                        {
                            return false;
                        }
                    }
                }

                return true;
            },
            TestContext.Current.CancellationToken)).ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, Assert.True);
    }

    /// <summary>Proves the embedded provider and the provider with no source are not configured, and neither ever has a model.</summary>
    [Fact]
    public void HeteronymModelProvider_EmbeddedAndNone_AreNotConfiguredAndHaveNoModel()
    {
        // Act / Assert
        Assert.False(HeteronymModelProvider.Embedded.IsConfigured);
        Assert.False(HeteronymModelProvider.None.IsConfigured);
        Assert.Null(HeteronymModelProvider.Embedded.GetClassifier(out var embeddedStatus));
        Assert.Null(HeteronymModelProvider.None.GetClassifier(out var noneStatus));
        Assert.Equal(ModelLoadStatus.NotShipped, embeddedStatus);
        Assert.Equal(ModelLoadStatus.NotShipped, noneStatus);
    }

    /// <summary>Proves constructing a provider and asking whether it is configured never opens the model source.</summary>
    [Fact]
    public void HeteronymModelProvider_IsConfigured_NeverOpensTheSource()
    {
        // Arrange
        var source = new CountingModelSource(HeteronymFixtureModel.Build(HeteronymFixtureModel.RecordExamples));

        // Act
        var provider = HeteronymFixtureModel.CreateProvider(source);
        var configured = provider.IsConfigured;

        // Assert
        Assert.True(configured);
        Assert.Equal(0, source.Count);
    }

    /// <summary>Proves 32 concurrent first requests load the model once and share one classifier.</summary>
    [Fact]
    public async Task HeteronymModelProvider_GetClassifier_ConcurrentFirstCalls_LoadOnceAndShare()
    {
        // Arrange
        var source = new CountingModelSource(HeteronymFixtureModel.Build(HeteronymFixtureModel.RecordExamples));
        var loads = 0;
        var provider = new HeteronymModelProvider(source.Open, loadObserver: () => Interlocked.Increment(ref loads));

        // Act
        var tasks = Enumerable.Range(0, 32).Select(index => Task.Run(() => provider.GetClassifier(out _), TestContext.Current.CancellationToken)).ToArray();
        var classifiers = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(1, source.Count);
        Assert.Equal(1, loads);
        Assert.NotNull(classifiers[0]);
        Assert.All(classifiers, classifier => Assert.Same(classifiers[0], classifier));
        Assert.Same(ReadingInventory.Default, provider.Inventory);
    }

    /// <summary>Proves a source with no model reports not shipped, quietly and once.</summary>
    [Fact]
    public void HeteronymModelProvider_GetClassifier_NoModel_ReportsNotShippedOnce()
    {
        // Arrange
        var source = new CountingModelSource(null);
        var provider = HeteronymFixtureModel.CreateProvider(source);

        // Act
        var first = provider.GetClassifier(out var firstStatus);
        var second = provider.GetClassifier(out var secondStatus);

        // Assert
        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(ModelLoadStatus.NotShipped, firstStatus);
        Assert.Equal(ModelLoadStatus.NotShipped, secondStatus);
        Assert.Equal(1, source.Count);
    }

    /// <summary>Proves a corrupt model and a failing source are reported as invalid values, never thrown, and the failure is cached rather than retried.</summary>
    [Fact]
    public void HeteronymModelProvider_GetClassifier_CorruptOrFailingSource_ReportsInvalidAndCachesIt()
    {
        // Arrange
        var corruptBytes = HeteronymFixtureModel.Build(HeteronymFixtureModel.RecordExamples);
        corruptBytes[^1] ^= 0x01;
        var corrupt = new CountingModelSource(corruptBytes);
        var failing = new CountingModelSource(null, new IOException("The disk is gone."));
        var unreadable = new CountingModelSource(null, new UnauthorizedAccessException("No access."));

        // Act
        var corruptProvider = HeteronymFixtureModel.CreateProvider(corrupt);
        var failingProvider = HeteronymFixtureModel.CreateProvider(failing);
        var unreadableProvider = HeteronymFixtureModel.CreateProvider(unreadable);
        corruptProvider.GetClassifier(out var corruptStatus);
        corruptProvider.GetClassifier(out _);
        failingProvider.GetClassifier(out var failingStatus);
        unreadableProvider.GetClassifier(out var unreadableStatus);

        // Assert
        Assert.Equal(ModelLoadStatus.Invalid, corruptStatus);
        Assert.Equal(ModelLoadStatus.Invalid, failingStatus);
        Assert.Equal(ModelLoadStatus.Invalid, unreadableStatus);
        Assert.Equal(1, corrupt.Count);
        Assert.Equal(1, failing.Count);
    }

    /// <summary>Proves a well-formed model for a newer inventory than the build has is reported as incompatible.</summary>
    [Fact]
    public void HeteronymModelProvider_GetClassifier_ModelOfNewerInventory_ReportsIncompatible()
    {
        // Arrange
        var bytes = HeteronymFixtureModel.Build(HeteronymFixtureModel.RecordExamples, inventoryVersion: ReadingInventory.CurrentVersion + 1);
        var provider = HeteronymFixtureModel.CreateProvider(new CountingModelSource(bytes));

        // Act
        var classifier = provider.GetClassifier(out var status);

        // Assert
        Assert.Null(classifier);
        Assert.Equal(ModelLoadStatus.Incompatible, status);
    }

    /// <summary>
    ///     Builds an inventory and a classifier over a model made of weights.
    /// </summary>
    /// <param name="weights">The (bucket, weight) pairs.</param>
    /// <param name="temperature">The calibration temperature.</param>
    /// <param name="observer">An optional classification observer.</param>
    /// <returns>The default inventory and the classifier.</returns>
    private static (ReadingInventory Inventory, HeteronymClassifier Classifier) Create(IEnumerable<(uint Bucket, short Weight)> weights, float temperature = 1f, Action<int>? observer = null)
    {
        var inventory = ReadingInventory.Default;
        var model = SparsePerceptronModel.Load(SparsePerceptronModelWriter.Compile(HashBits, inventory.Version, temperature, Scale, weights));
        return (inventory, new HeteronymClassifier(inventory, model, observer));
    }

    /// <summary>
    ///     Gives the bias feature of a reading a weight: the one feature every sentence has, bound to the reading's label salt.
    /// </summary>
    /// <param name="inventory">The inventory.</param>
    /// <param name="readingId">The reading id.</param>
    /// <param name="weight">The weight.</param>
    /// <returns>The (bucket, weight) pair.</returns>
    private static (uint Bucket, short Weight) BiasWeight(ReadingInventory inventory, string readingId, short weight)
    {
        Assert.True(inventory.TryGetReadingIndex(readingId, out var index));
        var mask = (uint)((1L << HashBits) - 1);
        return (FeatureHashing.Mix(FeatureHashing.Feature(0, 0, 0), inventory.LabelSalt(index)) & mask, weight);
    }

    /// <summary>
    ///     Builds an untagged context from a sentence of space-separated tokens and finds the target word.
    /// </summary>
    /// <param name="inventory">The inventory.</param>
    /// <param name="sentence">The space-separated tokens.</param>
    /// <param name="word">The target word.</param>
    /// <returns>The context, the target index and the target's form index.</returns>
    private static (ContextToken[] Context, int Target, int Form) Prepare(ReadingInventory inventory, string sentence, string word)
    {
        var parts = sentence.Split(' ');
        var starts = new List<int>();
        var offset = 0;
        foreach (var part in parts)
        {
            starts.Add(offset);
            offset += part.Length + 1;
        }

        var context = parts.Select((part, i) => HeteronymContext.Create(sentence, starts[i], part.Length, null, 0)).ToArray();
        var target = Array.IndexOf(parts, word);
        Assert.True(target >= 0);
        Assert.True(inventory.TryFindForm(word, out var form));
        return (context, target, form);
    }
}
