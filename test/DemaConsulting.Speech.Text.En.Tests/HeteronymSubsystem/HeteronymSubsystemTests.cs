using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests.HeteronymSubsystem;

/// <summary>
///     Subsystem tests for the HeteronymSubsystem: the inventory, the feature extractor, the sparse model,
///     the classifier and the model provider working together. The tagger that feeds the contexts is the
///     real one (reached through the test fixture, the same path the analyzer uses).
/// </summary>
public sealed class HeteronymSubsystemTests
{
    /// <summary>Proves a model supplied through the provider chooses the noun or the verb reading of the same spelling by context.</summary>
    [Fact]
    public void HeteronymSubsystem_ProviderToClassifier_FixtureModel_ChoosesReadingByContext()
    {
        // Arrange
        var source = new CountingModelSource(HeteronymFixtureModel.Build(HeteronymFixtureModel.RecordExamples));
        var provider = HeteronymFixtureModel.CreateProvider(source);
        var classifier = provider.GetClassifier(out var status);
        Assert.Equal(ModelLoadStatus.Loaded, status);
        Assert.NotNull(classifier);

        // Act: the same sentence has the verb first and the noun second
        var (context, words) = HeteronymFixtureModel.Describe("I want to record a record.");
        Assert.True(classifier.Inventory.TryFindForm("record", out var form));
        var verb = classifier.Classify(context, 3, form);
        var noun = classifier.Classify(context, 5, form);

        // Assert
        Assert.Equal(["I", "want", "to", "record", "a", "record", "."], words);
        Assert.Equal("record.verb", classifier.Inventory.ReadingIdAt(verb.ReadingIndex));
        Assert.Equal("record.noun", classifier.Inventory.ReadingIdAt(noun.ReadingIndex));
        Assert.True(verb.Confidence > 0.5 && noun.Confidence > 0.5);
    }

    /// <summary>Proves the model is bound to reading ids, not to table positions: an inventory that lists the same readings in another order makes the same decisions.</summary>
    [Fact]
    public void HeteronymSubsystem_ModelBindsToReadingIds_NotTablePositions()
    {
        // Arrange: the same two readings in the opposite order
        const string readings = "reading_id\tlemma\tpos_hint\tdescription\tstatus\nrecord.verb\trecord\tVERB\td\tactive\nrecord.noun\trecord\tNOUN\td\tactive\n";
        const string forms = "form\treading_id\tkind\nrecord\trecord.verb\torigin\nrecord\trecord.noun\torigin\n";
        var reordered = ReadingInventory.Parse(new StringReader(readings), new StringReader(forms), ReadingInventory.CurrentVersion);
        var model = SparsePerceptronModel.Load(HeteronymFixtureModel.Build(HeteronymFixtureModel.RecordExamples));
        var original = new HeteronymClassifier(ReadingInventory.Default, model);
        var swapped = new HeteronymClassifier(reordered, model);
        var (context, _) = HeteronymFixtureModel.Describe("I want to record a record.");
        Assert.True(ReadingInventory.Default.TryFindForm("record", out var originalForm));
        Assert.True(reordered.TryFindForm("record", out var swappedForm));

        // Act / Assert: the decisions name the same readings
        foreach (var target in new[] { 3, 5 })
        {
            var expected = ReadingInventory.Default.ReadingIdAt(original.Classify(context, target, originalForm).ReadingIndex);
            var actual = reordered.ReadingIdAt(swapped.Classify(context, target, swappedForm).ReadingIndex);
            Assert.Equal(expected, actual);
        }
    }

    /// <summary>Proves an empty model classifies every form of the real inventory without error, inside its candidates and with a confidence from 0 to 1.</summary>
    [Fact]
    public void HeteronymSubsystem_EveryInventoryForm_ClassifiesWithinItsCandidates()
    {
        // Arrange
        var inventory = ReadingInventory.Default;
        var classifier = new HeteronymClassifier(inventory, SparsePerceptronModel.Load(SparsePerceptronModelWriter.Compile(18, inventory.Version, 1f, 64f, [])));

        // Act / Assert
        for (var form = 0; form < inventory.FormCount; form++)
        {
            var word = inventory.FormAt(form);
            var text = $"We said {word} there.";
            var (context, words) = HeteronymFixtureModel.Describe(text);
            var target = Array.IndexOf(words, word);
            Assert.True(target >= 0, word);
            var prediction = classifier.Classify(context, target, form);
            Assert.Contains(prediction.ReadingIndex, inventory.GetCandidates(form).ToArray());
            Assert.InRange(prediction.Confidence, 0.0, 1.0);
        }
    }

    /// <summary>Proves a provider that is never asked for a classifier never opens its source, however it is used otherwise.</summary>
    [Fact]
    public void HeteronymSubsystem_Provider_NeverAskedForClassifier_NeverOpensSource()
    {
        // Arrange
        var source = new CountingModelSource(HeteronymFixtureModel.Build(HeteronymFixtureModel.RecordExamples));
        var provider = HeteronymFixtureModel.CreateProvider(source);

        // Act
        var configured = provider.IsConfigured;
        var inventory = provider.Inventory;

        // Assert
        Assert.True(configured);
        Assert.Same(ReadingInventory.Default, inventory);
        Assert.Equal(0, source.Count);
    }
}
