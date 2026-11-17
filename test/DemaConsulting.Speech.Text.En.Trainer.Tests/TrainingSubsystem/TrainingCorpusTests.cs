using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingSubsystem;

/// <summary>
///     Unit tests for <see cref="TrainingCorpus"/> and <see cref="RowPreparer"/>: which files are read, how rows
///     are prepared with the analyzer's own tokenization, and what the data and feature hashes cover.
/// </summary>
public sealed class TrainingCorpusTests
{
    /// <summary>Proves the corpus holds the training and development rows in file order with their prepared state.</summary>
    [Fact]
    public void TrainingCorpus_Load_Fixture_PreparesRowsInFileOrder()
    {
        // Arrange
        using var fixture = new TrainingFixture();

        // Act
        var corpus = TrainingCorpus.Load(fixture.Root);

        // Assert
        Assert.Equal(36, corpus.Train.Count);
        Assert.Equal(8, corpus.Dev.Count);
        Assert.Equal("syn-v000", corpus.Train[0].Row.Id);
        Assert.All(corpus.Train, row => Assert.True(row.IsAligned));
        Assert.Equal(2, corpus.Train.Count(row => !row.IsAmbiguous));
        Assert.All(corpus.Dev, row => Assert.True(row.Row.IsNatural));
    }

    /// <summary>Proves the corpus never opens a holdout file: deleting both of them or filling them with garbage changes nothing.</summary>
    [Fact]
    public void TrainingCorpus_Load_HoldoutFilesMissingOrGarbage_GivesTheSameCorpus()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var before = TrainingCorpus.Load(fixture.Root);
        File.Delete(fixture.PathOf("holdout-natural.tsv"));
        fixture.Write("holdout-independent.tsv", "not a data file\u0000\n");
        File.Delete(fixture.PathOf("harvest-provenance.tsv"));

        // Act
        var after = TrainingCorpus.Load(fixture.Root);

        // Assert
        Assert.Equal(before.DataSha256, after.DataSha256);
        Assert.Equal(before.FeaturesSha256, after.FeaturesSha256);
        Assert.Equal(before.Train.Count, after.Train.Count);
    }

    /// <summary>Proves the data hash changes with a training input and ignores a file the trainer does not read.</summary>
    [Fact]
    public void TrainingCorpus_DataSha256_ChangesWithTrainingInputOnly()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var original = TrainingCorpus.Load(fixture.Root);

        // Act
        fixture.Write("README.md", "# Edited\n");
        var afterReadme = TrainingCorpus.Load(fixture.Root);
        fixture.Replace("train.tsv", "Please lead the team now.", "Please lead the crew now.");
        var afterTrain = TrainingCorpus.Load(fixture.Root);

        // Assert
        Assert.Equal(original.DataSha256, afterReadme.DataSha256);
        Assert.NotEqual(original.DataSha256, afterTrain.DataSha256);
        Assert.Equal(64, original.DataSha256.Length);
        Assert.Equal(32, original.DataSha256Bytes.Length);
    }

    /// <summary>Proves the features hash changes when a row's sentence changes the features, even if the label stays.</summary>
    [Fact]
    public void TrainingCorpus_FeaturesSha256_ChangesWithTheFeaturesOfARow()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var original = TrainingCorpus.Load(fixture.Root);

        // Act
        fixture.Replace("train.tsv", "They will lead the team today.", "They will lead the crew today.");
        var changed = TrainingCorpus.Load(fixture.Root);

        // Assert
        Assert.NotEqual(original.FeaturesSha256, changed.FeaturesSha256);
    }

    /// <summary>Proves a row whose target is only part of a longer token is not aligned and carries no features.</summary>
    [Fact]
    public void TrainingCorpus_Load_TargetInsideLongerToken_IsNotAligned()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        fixture.AppendRow("train.tsv", "syn-unaligned", "lead.noun", "The leader spoke softly.", "lead", "synthetic");

        // Act
        var corpus = TrainingCorpus.Load(fixture.Root);

        // Assert
        var row = corpus.Train.Single(item => item.Row.Id == "syn-unaligned");
        Assert.False(row.IsAligned);
        Assert.Empty(row.Conjoined);
        Assert.Empty(row.Shared);
        Assert.Equal(1, corpus.Train.Count(item => !item.IsAligned));
    }

    /// <summary>Proves aligned rows are tagged and have features and the one-candidate form is marked unambiguous.</summary>
    [Fact]
    public void TrainingCorpus_Load_AlignedRows_HaveFeaturesAndUposAndAmbiguity()
    {
        // Arrange
        using var fixture = new TrainingFixture();

        // Act
        var corpus = TrainingCorpus.Load(fixture.Root);

        // Assert
        var lead = corpus.Train.First(row => row.Row.ReadingId == "lead.verb");
        Assert.True(lead.IsAmbiguous);
        Assert.NotEmpty(lead.Conjoined);
        Assert.NotEmpty(lead.Shared);
        Assert.NotNull(lead.Upos);
        Assert.Equal(2, lead.Candidates.Count);
        var wind = corpus.Train.First(row => row.Row.ReadingId == "wind.noun");
        Assert.False(wind.IsAmbiguous);
    }

    /// <summary>Proves the part-of-speech hints of every reading are read from the readings table.</summary>
    [Fact]
    public void TrainingCorpus_Load_Fixture_ReadsPartOfSpeechHints()
    {
        // Arrange
        using var fixture = new TrainingFixture("VERB|AUX", "NOUN");

        // Act
        var corpus = TrainingCorpus.Load(fixture.Root);

        // Assert
        Assert.True(corpus.Inventory.TryGetReadingIndex("lead.verb", out var verb));
        Assert.True(corpus.Inventory.TryGetReadingIndex("lead.noun", out var noun));
        Assert.Equal(["VERB", "AUX"], corpus.PosHints[verb]);
        Assert.Equal(["NOUN"], corpus.PosHints[noun]);
    }

    /// <summary>Proves a labeled reading that is not a candidate of the target's form is a data error naming the row.</summary>
    [Fact]
    public void TrainingCorpus_Load_LabelNotACandidate_ThrowsNamingTheRow()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        fixture.Replace("train.tsv", "syn-v000\tlead\tlead.verb", "syn-v000\tlead\twind.noun");

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => TrainingCorpus.Load(fixture.Root));

        // Assert
        Assert.Contains("syn-v000", exception.Message);
        Assert.Contains("not a candidate", exception.Message);
    }

    /// <summary>Proves a target that is not a form of the inventory is a data error naming the row.</summary>
    [Fact]
    public void TrainingCorpus_Load_TargetNotAForm_ThrowsNamingTheRow()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        fixture.AppendRow("train.tsv", "syn-bad", "lead.verb", "Birds fly south.", "Birds", "synthetic");

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => TrainingCorpus.Load(fixture.Root));

        // Assert
        Assert.Contains("syn-bad", exception.Message);
        Assert.Contains("not a form", exception.Message);
    }

    /// <summary>Proves each required input file that is missing is a data error naming the file.</summary>
    [Theory]
    [InlineData("train.tsv")]
    [InlineData("dev.tsv")]
    [InlineData("readings.tsv")]
    [InlineData("forms.tsv")]
    public void TrainingCorpus_Load_MissingInputFile_ThrowsNamingTheFile(string file)
    {
        // Arrange
        using var fixture = new TrainingFixture();
        File.Delete(fixture.PathOf(file));

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => TrainingCorpus.Load(fixture.Root));

        // Assert
        Assert.Contains(file, exception.Message);
    }

    /// <summary>Proves a malformed span is a data error naming the file and line.</summary>
    [Fact]
    public void RowPreparer_ReadRows_SpanOutsideSentence_ThrowsNamingFileAndLine()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        fixture.Replace("dev.tsv", "We lead the team again.\t3\t7\t", "We lead the team again.\t3\t700\t");
        // (the first dev row now has an end beyond its sentence)

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => TrainingCorpus.Load(fixture.Root));

        // Assert
        Assert.Contains("dev.tsv", exception.Message);
        Assert.Contains("span", exception.Message);
    }

    /// <summary>Proves the reader keeps the strict file format rules: a file with the wrong header is rejected.</summary>
    [Fact]
    public void RowPreparer_ReadRows_WrongHeader_ThrowsInvalidDataException()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        fixture.Replace("train.tsv", "id\tlemma\treading_id", "id\tlemma\treading");

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => TrainingCorpus.Load(fixture.Root));
    }
}
