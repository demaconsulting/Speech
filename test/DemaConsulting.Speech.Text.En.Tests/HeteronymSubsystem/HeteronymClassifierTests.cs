using System.Buffers.Binary;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests.HeteronymSubsystem;

/// <summary>
///     Unit tests for <see cref="HeteronymClassifier"/> and <see cref="HeteronymModelProvider"/>:
///     candidate-restricted scoring, the tie rule, calibrated confidence, compatibility checks, and the
///     failure-contained model supply.
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

    /// <summary>Proves the confidence is the temperature-calibrated softmax of the top candidate: two candidates 64 apart at scale 64 and temperature 2 give 1/(1+e^-0.5).</summary>
    [Fact]
    public void HeteronymClassifier_Classify_TwoCandidates64Apart_ConfidenceIsCalibratedSoftmax()
    {
        // Arrange: weight the bias feature of record.verb by 64 (one unit of real score)
        const float temperature = 2f;
        var inventory = ReadingInventory.Default;
        var (context, target, form) = Prepare(inventory, "I want to record it .", "record");
        var (_, classifier) = Create([BiasWeight(inventory, "record.verb", 64)], temperature);

        // Act
        var prediction = classifier.Classify(context, target, form);

        // Assert
        Assert.Equal("record.verb", inventory.ReadingIdAt(prediction.ReadingIndex));
        Assert.Equal(1 / (1 + Math.Exp(-1 / (double)temperature)), prediction.Confidence, 12);
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

    /// <summary>Proves a single-candidate form returns its reading with confidence 1.</summary>
    [Fact]
    public void HeteronymClassifier_Classify_SingleCandidateForm_ReturnsConfidenceOne()
    {
        // Arrange
        var inventory = ReadingInventory.Default;
        var form = Enumerable.Range(0, inventory.FormCount).First(f => inventory.GetCandidates(f).Length == 1);
        var word = inventory.FormAt(form);
        var (context, target, formIndex) = Prepare(inventory, $"We saw the {word} there .", word);
        var (_, classifier) = Create([]);

        // Act
        var prediction = classifier.Classify(context, target, formIndex);

        // Assert
        Assert.Equal(inventory.GetCandidates(form)[0], prediction.ReadingIndex);
        Assert.Equal(1.0, prediction.Confidence);
    }

    /// <summary>Proves the observer receives the running count of model-scored classifications, so a test can act at an exact one, and is not called for a single-candidate form, which no model scores.</summary>
    [Fact]
    public void HeteronymClassifier_Classify_Observer_ReceivesRunningCount()
    {
        // Arrange
        var inventory = ReadingInventory.Default;
        var (context, target, form) = Prepare(inventory, "I want to record it .", "record");
        var singleForm = Enumerable.Range(0, inventory.FormCount).First(f => inventory.GetCandidates(f).Length == 1);
        var singleWord = inventory.FormAt(singleForm);
        var (singleContext, singleTarget, singleFormIndex) = Prepare(inventory, $"We saw the {singleWord} there .", singleWord);
        var counts = new List<int>();
        var (_, classifier) = Create([], observer: counts.Add);

        // Act
        classifier.Classify(singleContext, singleTarget, singleFormIndex);
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

    /// <summary>Proves a target outside the sentence (negative, equal to the length or beyond it) is rejected for both an ambiguous and a single-candidate form.</summary>
    /// <param name="singleCandidate">Whether to classify a single-candidate form (which takes the fast path) or a multi-candidate one.</param>
    /// <param name="targetKind">Which invalid target to use: -1 negative, 0 equal to the length, 1 beyond the length, 2 int.MinValue, 3 int.MaxValue.</param>
    [Theory]
    [InlineData(false, -1)]
    [InlineData(false, 0)]
    [InlineData(false, 3)]
    [InlineData(true, 0)]
    public void HeteronymClassifier_Classify_TargetOutsideSentence_ThrowsArgumentOutOfRangeException(bool singleCandidate, int targetKind)
    {
        // Arrange: a form of the requested kind and a sentence that contains it
        var inventory = ReadingInventory.Default;
        var form = Enumerable.Range(0, inventory.FormCount).First(f => (inventory.GetCandidates(f).Length == 1) == singleCandidate);
        var word = inventory.FormAt(form);
        var (context, _, _) = Prepare(inventory, $"We saw the {word} there .", word);
        var (_, classifier) = Create([]);
        var target = targetKind switch
        {
            -1 => -1,
            0 => context.Length,
            1 => context.Length + 1,
            2 => int.MinValue,
            _ => int.MaxValue,
        };

        // Act / Assert: the target is validated before the single-candidate shortcut
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => classifier.Classify(context, target, form));
        Assert.Equal("target", exception.ParamName);
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

    /// <summary>Proves the embedded provider is configured and loads the embedded model (compatible with this build), while the provider with no source is not configured and never has a model.</summary>
    [Fact]
    public void HeteronymModelProvider_EmbeddedAndNone_EmbeddedLoadsAndNoneHasNoModel()
    {
        // Act
        var embedded = HeteronymModelProvider.Embedded.GetClassifier(out var embeddedStatus);
        var none = HeteronymModelProvider.None.GetClassifier(out var noneStatus);

        // Assert
        Assert.True(HeteronymModelProvider.Embedded.IsConfigured);
        Assert.NotNull(embedded);
        Assert.Equal(ModelLoadStatus.Loaded, embeddedStatus);
        Assert.False(HeteronymModelProvider.None.IsConfigured);
        Assert.Null(none);
        Assert.Equal(ModelLoadStatus.NotShipped, noneStatus);
    }

    /// <summary>Proves 32 concurrent first requests all get a working classifier that gives the predictions of a sequential request.</summary>
    [Fact]
    public async Task HeteronymModelProvider_GetClassifier_ConcurrentFirstCalls_GetClassifiersWithSequentialPredictions()
    {
        // Arrange
        var source = new CountingModelSource(HeteronymFixtureModel.Build(HeteronymFixtureModel.RecordExamples));
        var provider = HeteronymFixtureModel.CreateProvider(source);
        var reference = HeteronymFixtureModel.CreateProvider(new CountingModelSource(HeteronymFixtureModel.Build(HeteronymFixtureModel.RecordExamples))).GetClassifier(out _);
        Assert.NotNull(reference);
        var (context, _) = HeteronymFixtureModel.Describe("I want to record a record.");
        Assert.True(ReadingInventory.Default.TryFindForm("record", out var form));
        var expected = new[] { 3, 5 }.Select(target => reference.Classify(context, target, form)).ToArray();

        // Act
        var tasks = Enumerable.Range(0, 32).Select(index => Task.Run(
            () =>
            {
                var classifier = provider.GetClassifier(out var status);
                return (status, predictions: classifier is null ? Array.Empty<HeteronymPrediction>() : new[] { 3, 5 }.Select(target => classifier.Classify(context, target, form)).ToArray());
            },
            TestContext.Current.CancellationToken)).ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, result =>
        {
            Assert.Equal(ModelLoadStatus.Loaded, result.status);
            Assert.Equal(expected, result.predictions);
        });
        Assert.Equal(ReadingInventory.Default.Version, provider.Inventory.Version);
    }

    /// <summary>Proves a source with no model reports not shipped, quietly, however often it is asked.</summary>
    [Fact]
    public void HeteronymModelProvider_GetClassifier_NoModel_ReportsNotShipped()
    {
        // Arrange
        var provider = HeteronymFixtureModel.CreateProvider(new CountingModelSource(null));

        // Act
        var first = provider.GetClassifier(out var firstStatus);
        var second = provider.GetClassifier(out var secondStatus);

        // Assert
        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(ModelLoadStatus.NotShipped, firstStatus);
        Assert.Equal(ModelLoadStatus.NotShipped, secondStatus);
    }

    /// <summary>Proves a corrupt model and a failing source are reported as invalid values with no classifier, never thrown.</summary>
    [Fact]
    public void HeteronymModelProvider_GetClassifier_CorruptOrFailingSource_ReportsInvalid()
    {
        // Arrange
        var corruptBytes = HeteronymFixtureModel.Build(HeteronymFixtureModel.RecordExamples);
        corruptBytes[^1] ^= 0x01;
        var corruptProvider = HeteronymFixtureModel.CreateProvider(new CountingModelSource(corruptBytes));
        var failingProvider = HeteronymFixtureModel.CreateProvider(new CountingModelSource(null, new IOException("The disk is gone.")));
        var unreadableProvider = HeteronymFixtureModel.CreateProvider(new CountingModelSource(null, new UnauthorizedAccessException("No access.")));

        // Act
        var corrupt = corruptProvider.GetClassifier(out var corruptStatus);
        var failing = failingProvider.GetClassifier(out var failingStatus);
        var unreadable = unreadableProvider.GetClassifier(out var unreadableStatus);

        // Assert
        Assert.Null(corrupt);
        Assert.Null(failing);
        Assert.Null(unreadable);
        Assert.Equal(ModelLoadStatus.Invalid, corruptStatus);
        Assert.Equal(ModelLoadStatus.Invalid, failingStatus);
        Assert.Equal(ModelLoadStatus.Invalid, unreadableStatus);
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

    /// <summary>Proves a well-formed model of another feature-set version is reported as incompatible, not invalid.</summary>
    /// <param name="featureSetVersion">A feature-set version other than the one this build extracts.</param>
    [Theory]
    [InlineData(2)]
    [InlineData(65_535)]
    public void HeteronymModelProvider_GetClassifier_ModelOfOtherFeatureSet_ReportsIncompatible(int featureSetVersion)
    {
        // Arrange: a valid model whose header names another feature set (the header is not covered by the checksum)
        var bytes = HeteronymFixtureModel.Build(HeteronymFixtureModel.RecordExamples);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)featureSetVersion);
        var provider = HeteronymFixtureModel.CreateProvider(new CountingModelSource(bytes));

        // Act
        var classifier = provider.GetClassifier(out var status);

        // Assert
        Assert.Null(classifier);
        Assert.Equal(ModelLoadStatus.Incompatible, status);
    }

    /// <summary>
    ///     Proves the adjective cue: a word the tagger reads as an adjective, right after a degree adverb or coordinated with
    ///     another adjective, gets its adjective reading even when the model leans weakly toward another reading, while
    ///     the same model keeps that other reading when the grammar does not mark an adjective.
    /// </summary>
    /// <param name="sentence">The space-separated tokens.</param>
    /// <param name="tags">The tag of each token.</param>
    /// <param name="expected">The reading the classifier must choose for <c>minute</c>.</param>
    [Theory]
    [InlineData("very minute and clear", "ADV ADJ CCONJ ADJ", "minute.tiny")]
    [InlineData("so minute that", "ADV ADJ SCONJ", "minute.tiny")]
    [InlineData("grey and minute", "ADJ CCONJ ADJ", "minute.tiny")]
    [InlineData("minute and very clear", "ADJ CCONJ ADV ADJ", "minute.tiny")]
    [InlineData("wait a minute", "VERB DET NOUN", "minute.time")]
    [InlineData("the very minute he came", "DET ADJ NOUN PRON VERB", "minute.time")]
    [InlineData("very minute", "ADV NOUN", "minute.time")]
    [InlineData("a minute and clear", "DET ADJ CCONJ NOUN", "minute.time")]
    public void HeteronymClassifier_Classify_AdjectiveCue_DecidesAWeaklyContestedAdjective(string sentence, string tags, string expected)
    {
        // Arrange: a model that prefers the time reading by a small margin (a quarter of the cue's odds)
        var inventory = ReadingInventory.Default;
        var (context, target, form) = PrepareTagged(inventory, sentence, tags.Split(' '), "minute");
        var margin = (short)(Scale * HeteronymClassifier.AdjectiveCueLogOdds / 4);
        var (_, classifier) = Create([BiasWeight(inventory, "minute.time", margin)]);

        // Act
        var prediction = classifier.Classify(context, target, form);

        // Assert
        Assert.Equal(expected, inventory.ReadingIdAt(prediction.ReadingIndex));
        Assert.InRange(prediction.Confidence, 0.5, 1.0);
    }

    /// <summary>Proves the adjective cue is a prior, not a rule: a model that is sure of another reading keeps it.</summary>
    [Fact]
    public void HeteronymClassifier_Classify_AdjectiveCueAgainstAConfidentModel_ModelWins()
    {
        // Arrange: the time reading is preferred by twice the cue's odds
        var inventory = ReadingInventory.Default;
        var (context, target, form) = PrepareTagged(inventory, "very minute", ["ADV", "ADJ"], "minute");
        var margin = (short)(Scale * HeteronymClassifier.AdjectiveCueLogOdds * 2);
        var (_, classifier) = Create([BiasWeight(inventory, "minute.time", margin)]);

        // Act
        var prediction = classifier.Classify(context, target, form);

        // Assert
        Assert.Equal("minute.time", inventory.ReadingIdAt(prediction.ReadingIndex));
    }

    /// <summary>
    ///     Proves the noun-form cue: <i>wound</i> tagged as a noun gets the injury reading when the model leans weakly toward
    ///     the past of <i>wind</i> (which is never a noun in this form), while the same model keeps the coil reading when the
    ///     word is tagged as a verb, and a form whose readings are both nouns (<i>tear</i>) gets no cue.
    /// </summary>
    /// <param name="sentence">The space-separated tokens.</param>
    /// <param name="tags">The tag of each token.</param>
    /// <param name="word">The target word.</param>
    /// <param name="leaning">The reading the model prefers by a small margin.</param>
    /// <param name="expected">The reading the classifier must choose.</param>
    [Theory]
    [InlineData("a knife wound or", "DET NOUN NOUN CCONJ", "wound", "wind.coil", "wound.injury")]
    [InlineData("she wound the clock", "PRON VERB DET NOUN", "wound", "wind.coil", "wind.coil")]
    [InlineData("a paper tear and", "DET NOUN NOUN CCONJ", "tear", "tear.drop", "tear.drop")]
    public void HeteronymClassifier_Classify_NounFormCue_DecidesAFormThatOnlyOneReadingMakesANoun(string sentence, string tags, string word, string leaning, string expected)
    {
        // Arrange: a model that prefers one reading by a small margin (half of the cue's odds)
        var inventory = ReadingInventory.Default;
        var (context, target, form) = PrepareTagged(inventory, sentence, tags.Split(' '), word);
        var margin = (short)(Scale * HeteronymClassifier.NounFormCueLogOdds / 2);
        var (_, classifier) = Create([BiasWeight(inventory, leaning, margin)]);

        // Act
        var prediction = classifier.Classify(context, target, form);

        // Assert
        Assert.Equal(expected, inventory.ReadingIdAt(prediction.ReadingIndex));
    }

    /// <summary>
    ///     Proves the context cue: <i>tear</i> near a word of the body or of a material (<i>muscle</i>, <i>fabric</i>,
    ///     <i>wear</i>) gets the rip reading when the model leans weakly toward the drop from the eye, while the same model
    ///     keeps the drop reading when no such word is within three words.
    /// </summary>
    /// <param name="sentence">The space-separated tokens.</param>
    /// <param name="tags">The tag of each token.</param>
    /// <param name="expected">The reading the classifier must choose for <c>tear</c>.</param>
    [Theory]
    [InlineData("a deep muscle tear and", "DET ADJ NOUN NOUN CCONJ", "tear.rip")]
    [InlineData("a tear in the fabric", "DET NOUN ADP DET NOUN", "tear.rip")]
    [InlineData("wear and tear", "NOUN CCONJ NOUN", "tear.rip")]
    [InlineData("a tear ran down her cheek", "DET NOUN VERB ADV DET NOUN", "tear.drop")]
    [InlineData("a tear fell and later the muscle relaxed", "DET NOUN VERB CCONJ ADV DET NOUN VERB", "tear.drop")]
    public void HeteronymClassifier_Classify_ContextCue_DecidesAReadingTheTrainingDataSeldomShows(string sentence, string tags, string expected)
    {
        // Arrange: a model that prefers the drop reading by a small margin (a quarter of the cue's odds)
        var inventory = ReadingInventory.Default;
        var (context, target, form) = PrepareTagged(inventory, sentence, tags.Split(' '), "tear");
        var margin = (short)(Scale * HeteronymClassifier.ContextCueLogOdds / 4);
        var (_, classifier) = Create([BiasWeight(inventory, "tear.drop", margin)]);

        // Act
        var prediction = classifier.Classify(context, target, form);

        // Assert
        Assert.Equal(expected, inventory.ReadingIdAt(prediction.ReadingIndex));
    }

    /// <summary>
    ///     Proves the past-time cue: <i>read</i> in a sentence placed in the past by <i>yesterday</i>, <i>ago</i> or <i>last</i>
    ///     and a word of time gets the past reading when the model leans weakly toward the present, but not after an
    ///     auxiliary or a particle (which take the base form) or without such a word.
    /// </summary>
    /// <param name="sentence">The space-separated tokens.</param>
    /// <param name="tags">The tag of each token.</param>
    /// <param name="expected">The reading the classifier must choose for <c>read</c>.</param>
    [Theory]
    [InlineData("yesterday I read the book", "ADV PRON VERB DET NOUN", "read.past")]
    [InlineData("two days ago she read it", "NUM NOUN ADV PRON VERB PRON", "read.past")]
    [InlineData("last night we read aloud", "ADJ NOUN PRON VERB ADV", "read.past")]
    [InlineData("yesterday I wanted to read", "ADV PRON VERB PART VERB", "read.present")]
    [InlineData("last night I did not read", "ADJ NOUN PRON AUX PART VERB", "read.present")]
    [InlineData("I read the book", "PRON VERB DET NOUN", "read.present")]
    [InlineData("at last we read it", "ADP ADJ PRON VERB PRON", "read.present")]
    [InlineData("she read it two days ago", "PRON VERB PRON NUM NOUN ADV", "read.past")]
    [InlineData("last night , I read it", "ADJ NOUN PUNCT PRON VERB PRON", "read.past")]
    [InlineData("I read books , and yesterday she slept", "PRON VERB NOUN PUNCT CCONJ ADV PRON VERB", "read.present")]
    [InlineData("I read books because last week it rained", "PRON VERB NOUN SCONJ ADJ NOUN PRON VERB", "read.present")]
    [InlineData("I read books ; yesterday it rained", "PRON VERB NOUN PUNCT ADV PRON VERB", "read.present")]
    public void HeteronymClassifier_Classify_PastTimeCue_DecidesTheTenseOfAWeaklyContestedVerb(string sentence, string tags, string expected)
    {
        // Arrange: a model that prefers the present reading by a small margin (a quarter of the cue's odds)
        var inventory = ReadingInventory.Default;
        var (context, target, form) = PrepareTagged(inventory, sentence, tags.Split(' '), "read");
        var margin = (short)(Scale * HeteronymClassifier.PastTimeCueLogOdds / 4);
        var (_, classifier) = Create([BiasWeight(inventory, "read.present", margin)]);

        // Act
        var prediction = classifier.Classify(context, target, form);

        // Assert
        Assert.Equal(expected, inventory.ReadingIdAt(prediction.ReadingIndex));
    }

    /// <summary>
    ///     Builds a tagged context from a sentence of space-separated tokens and finds the target word.
    /// </summary>
    /// <param name="inventory">The inventory.</param>
    /// <param name="sentence">The space-separated tokens.</param>
    /// <param name="tags">The Universal Dependencies tag of each token.</param>
    /// <param name="word">The target word.</param>
    /// <returns>The context, the target index and the target's form index.</returns>
    private static (ContextToken[] Context, int Target, int Form) PrepareTagged(ReadingInventory inventory, string sentence, string[] tags, string word)
    {
        var parts = sentence.Split(' ');
        Assert.Equal(parts.Length, tags.Length);
        var starts = new List<int>();
        var offset = 0;
        foreach (var part in parts)
        {
            starts.Add(offset);
            offset += part.Length + 1;
        }

        var context = parts.Select((part, i) => HeteronymContext.Create(sentence, starts[i], part.Length, tags[i], 1.0)).ToArray();
        var target = Array.IndexOf(parts, word);
        Assert.True(target >= 0);
        Assert.True(inventory.TryFindForm(word, out var form));
        return (context, target, form);
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
