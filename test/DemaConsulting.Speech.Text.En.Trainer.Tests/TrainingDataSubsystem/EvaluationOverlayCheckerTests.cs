using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingDataSubsystem;

/// <summary>
///     Unit tests for <see cref="EvaluationOverlayChecker"/>: the regression overlay rules, the generalization novelty rules
///     N1 to N3, the generic reasons of a staged set and the shape quotas.
/// </summary>
public sealed class EvaluationOverlayCheckerTests
{
    /// <summary>The quotas the shape tests use: small enough for a fixture of a few rows.</summary>
    private static readonly GeneralizationShape SmallShape = new(1, 1, 12, 1, 1, 1, 1, 2);

    /// <summary>A novel-vocabulary row of <c>lead.noun</c> whose cue words do not occur with <i>lead</i> in the fixture.</summary>
    private static readonly string[] NovelVocabularyRow = DatasetFixture.GeneralizationRowText(
        "gen-0001", "lead.noun", "The plumber replaced every old lead fitting in the cellar.", "lead", "novel-vocabulary", "lead:plumbing", "plumber|fitting", "yes");

    /// <summary>A novel-construction row of <c>lead.verb</c> whose words around the target are new for <i>lead</i>.</summary>
    private static readonly string[] NovelConstructionRow = DatasetFixture.GeneralizationRowText(
        "gen-0002", "lead.verb", "Could a seasoned ranger lead hikers safely through fog?", "lead", "novel-construction", "lead:guide-question", "-");

    /// <summary>
    ///     Writes the generalization set of the fixture and runs the overlay checks.
    /// </summary>
    /// <param name="fixture">The fixture.</param>
    /// <param name="shape">The quotas.</param>
    /// <param name="rows">The rows.</param>
    /// <returns>The report.</returns>
    private static CheckReport CheckGeneralization(DatasetFixture fixture, GeneralizationShape shape, params string[][] rows)
    {
        fixture.WriteGeneralization(TextEnDataSet.GeneralizationFile, rows);
        return fixture.CheckOverlays(shape);
    }

    /// <summary>Proves the valid fixture overlays pass and the empty generalization set skips the shape quotas.</summary>
    [Fact]
    public void EvaluationOverlayChecker_Run_ValidFixture_NoFindingsAndShapeSkipped()
    {
        // Arrange
        using var fixture = new DatasetFixture();

        // Act
        var report = fixture.CheckOverlays();

        // Assert
        Assert.Empty(report.Findings);
        Assert.Contains(report.Info, line => line.Contains("regression overlay: 2 rows, seen in training 1, unseen 1", StringComparison.Ordinal));
        Assert.Contains(report.Info, line => line.Contains("the shape quotas apply once the set is written", StringComparison.Ordinal));
    }

    /// <summary>Proves a data-row reference must resolve to the same example with the same license.</summary>
    [Theory]
    [InlineData("ref", "train.tsv:syn-99999", "does not resolve")]
    [InlineData("sentence", "The lead pipe was very heavy.", "differ from train.tsv:syn-00001")]
    [InlineData("license", "Public-Domain", "differs from 'CC0-1.0'")]
    public void EvaluationOverlayChecker_CheckRegression_DataRowMismatch_IsFinding(string column, string value, string message)
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow(TextEnDataSet.RegressionFile, "reg-0001", (column, value));

        // Act
        var report = fixture.CheckOverlays();

        // Assert
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, message), string.Join('\n', report.Findings));
    }

    /// <summary>Proves an overlay row may neither refer to nor repeat a holdout sentence.</summary>
    [Fact]
    public void EvaluationOverlayChecker_CheckRegression_HoldoutReferenceOrRepeat_IsFinding()
    {
        // Arrange: one row refers into a holdout file, another repeats a holdout sentence
        using var fixture = new DatasetFixture();
        fixture.EditRow(TextEnDataSet.RegressionFile, "reg-0001", ("ref", "holdout-natural.tsv:harvest-u00001"));
        fixture.AppendRow(TextEnDataSet.RegressionFile, DatasetFixture.RegressionRowText("reg-0003", "lead.verb", "Please lead the way now.", "lead", "listening", "RUN-LOG.md:1", "no"));

        // Act
        var report = fixture.CheckOverlays();

        // Assert
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, "reg-0001: refers to holdout-natural.tsv"));
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, "reg-0003: repeats a holdout sentence (holdout-independent.tsv hind-1)"));
    }

    /// <summary>Proves the seen-in-training mark must equal the one recomputed from the training sentences.</summary>
    [Theory]
    [InlineData("reg-0001", "no", "but the training data says 'yes'")]
    [InlineData("reg-0002", "yes", "but the training data says 'no'")]
    [InlineData("reg-0002", "maybe", "must be yes or no")]
    public void EvaluationOverlayChecker_CheckRegression_WrongSeenMark_IsFinding(string id, string seen, string message)
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow(TextEnDataSet.RegressionFile, id, ("seen_in_training", seen));

        // Act
        var report = fixture.CheckOverlays();

        // Assert
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, message), string.Join('\n', report.Findings));
    }

    /// <summary>Proves a golden row whose sentence equals a training sentence must be marked as seen.</summary>
    [Fact]
    public void EvaluationOverlayChecker_CheckRegression_GoldenSentenceInTraining_MustBeSeen()
    {
        // Arrange: a golden row that repeats a training sentence but is marked unseen
        using var fixture = new DatasetFixture();
        fixture.AppendRow(TextEnDataSet.RegressionFile, DatasetFixture.RegressionRowText("reg-0003", "wind.noun", "A cold wind blew hard.", "wind", "golden-sentences", "sentences-golden.tsv:s001", "no"));

        // Act
        var report = fixture.CheckOverlays();

        // Assert
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, "reg-0003: seen_in_training is 'no'"));
    }

    /// <summary>Proves the origin, the reference grammar, the id, the license and the span of a regression row are checked.</summary>
    [Theory]
    [InlineData("origin", "rumor", "unknown origin 'rumor'")]
    [InlineData("ref", "readings-golden.tsv:two", "does not fit origin 'golden-readings'")]
    [InlineData("id", "reg-2", "id must be unique and match reg-NNNN")]
    [InlineData("license", "CC-BY-SA-4.0", "license 'CC-BY-SA-4.0' is not allowed")]
    [InlineData("start", "1", "span:")]
    [InlineData("reading_id", "ghost.verb", "unknown reading 'ghost.verb'")]
    public void EvaluationOverlayChecker_CheckRegression_MalformedRow_IsFinding(string column, string value, string message)
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow(TextEnDataSet.RegressionFile, "reg-0002", (column, value));

        // Act
        var report = fixture.CheckOverlays();

        // Assert
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, message), string.Join('\n', report.Findings));
    }

    /// <summary>Proves rows that are novel in vocabulary and in construction pass, and the populated set meets small quotas.</summary>
    [Fact]
    public void EvaluationOverlayChecker_CheckGeneralization_NovelRows_PassWithSmallQuotas()
    {
        // Arrange
        using var fixture = new DatasetFixture();

        // Act
        var report = CheckGeneralization(fixture, SmallShape, NovelVocabularyRow, NovelConstructionRow);

        // Assert
        Assert.Empty(report.Findings);
        Assert.Contains(report.Info, line => line.Contains("generalization set: 2 rows", StringComparison.Ordinal));
    }

    /// <summary>Proves rule N1: a row that repeats a training sentence or a regression sentence is a finding naming the match.</summary>
    [Theory]
    [InlineData("She will lead the team today!", "a sentence (train.tsv harvest-00001)")]
    [InlineData("Guides lead the hikers uphill!", "a regression overlay sentence")]
    public void EvaluationOverlayChecker_CheckGeneralization_N1RepeatedSentence_IsFinding(string sentence, string match)
    {
        // Arrange
        using var fixture = new DatasetFixture();
        var row = DatasetFixture.GeneralizationRowText("gen-0001", "lead.verb", sentence, "lead", "novel-construction", "lead:guide", "-");

        // Act
        var report = CheckGeneralization(fixture, SmallShape, row, NovelVocabularyRow);

        // Assert
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, "N1: repeats or nearly repeats " + match), string.Join('\n', report.Findings));
    }

    /// <summary>Proves rule N2: a designated cue form that occurs with the lemma in training or development data is a finding.</summary>
    [Fact]
    public void EvaluationOverlayChecker_CheckGeneralization_N2KnownCueForm_IsFinding()
    {
        // Arrange: 'pipe' occurs with 'lead' in the training data, 'cellar' does not
        using var fixture = new DatasetFixture();
        var row = DatasetFixture.GeneralizationRowText("gen-0001", "lead.noun", "Each old lead pipe in the cellar was replaced.", "lead", "novel-vocabulary", "lead:plumbing", "pipe|cellar");

        // Act
        var report = CheckGeneralization(fixture, SmallShape, row, NovelConstructionRow);

        // Assert
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, "N2: cue form 'pipe' already occurs with this lemma group (train.tsv syn-00001)"));
        Assert.False(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, "cue form 'cellar'"));
    }

    /// <summary>Proves rule N3: a construction trigram around the target that a training or development row has is a finding.</summary>
    [Fact]
    public void EvaluationOverlayChecker_CheckGeneralization_N3KnownConstruction_IsFinding()
    {
        // Arrange: 'will lead the' and 'lead the team' occur around 'lead' in the training data
        using var fixture = new DatasetFixture();
        var row = DatasetFixture.GeneralizationRowText("gen-0002", "lead.verb", "Tomorrow the guides will lead the team across the glacier.", "lead", "novel-construction", "lead:guide", "-");

        // Act
        var report = CheckGeneralization(fixture, SmallShape, NovelVocabularyRow, row);

        // Assert
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, "N3: construction 'C|will <T> the' already occurs around this lemma group (train.tsv harvest-00001)"));
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, "N3: construction 'R|<T> the team'"));
    }

    /// <summary>Proves the findings of a staged set give the generic reason and name no training or development row.</summary>
    [Fact]
    public void EvaluationOverlayChecker_CheckGeneralization_StagedRows_GiveGenericReasonsOnly()
    {
        // Arrange: one row of each novelty violation, loaded as a staged file
        using var fixture = new DatasetFixture();
        var staged = Path.Combine(fixture.BaseDirectory + "-eval", "generalization.tsv");
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        fixture.WriteGeneralization(
            staged,
            DatasetFixture.GeneralizationRowText("gen-0001", "lead.verb", "She will lead the team today!", "lead", "novel-construction", "lead:guide", "-"),
            DatasetFixture.GeneralizationRowText("gen-0002", "lead.noun", "Each old lead pipe in the cellar was replaced.", "lead", "novel-vocabulary", "lead:plumbing", "pipe"),
            DatasetFixture.GeneralizationRowText("gen-0003", "lead.verb", "Tomorrow the guides will lead the team across the glacier.", "lead", "novel-construction", "lead:guide", "-"));
        var report = new CheckReport(1000);
        var checker = new EvaluationOverlayChecker(TextEnDataSet.Load(fixture.Root, report), report, SmallShape);

        // Act
        checker.CheckGeneralization(TextEnDataSet.LoadGeneralization(staged, "staged:generalization.tsv", report), revealMatches: false);

        // Assert
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, "gen-0001: N1: " + EvaluationOverlayChecker.GenericN1));
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, "gen-0002: N2: cue form 'pipe' " + EvaluationOverlayChecker.GenericN2));
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, "gen-0003: N3: " + EvaluationOverlayChecker.GenericN3));
        Assert.DoesNotContain(report.Findings, finding => finding.Message.Contains(".tsv", StringComparison.Ordinal) || finding.Message.Contains("syn-", StringComparison.Ordinal));
        Assert.All(report.Findings, finding => Assert.StartsWith("staged:generalization.tsv", finding.Location, StringComparison.Ordinal));
    }

    /// <summary>Proves the shape quotas apply to a populated set and name every quota that is not met.</summary>
    [Fact]
    public void EvaluationOverlayChecker_CheckGeneralization_PopulatedSetBelowDefaultQuotas_ReportsEveryQuota()
    {
        // Arrange
        using var fixture = new DatasetFixture();

        // Act: two rows against the default quotas of the protocol
        var report = CheckGeneralization(fixture, GeneralizationShape.Default, NovelVocabularyRow, NovelConstructionRow);

        // Assert
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, "shape: 2 families (needs at least 30)"));
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, "shape: family 'lead:plumbing' has 1 rows (allowed 2 to 12)"));
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, "shape: 1 novel-vocabulary rows (needs at least 90)"));
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, "shape: 1 novel-construction rows (needs at least 70)"));
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, "shape: 1 cue_unsolvable=yes rows (needs at least 60)"));
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, "shape: 1 lemmas among the cue_unsolvable=yes rows (needs at least 8)"));
    }

    /// <summary>Proves a lemma group with rows for only one of its readings is a shape finding.</summary>
    [Fact]
    public void EvaluationOverlayChecker_CheckGeneralization_OneReadingOfLemma_IsShapeFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();

        // Act
        var report = CheckGeneralization(fixture, SmallShape, NovelVocabularyRow);

        // Assert
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, "shape: lemma group 'lead' has rows for 1 reading(s) (needs at least 2)"));
    }

    /// <summary>Proves the columns, length, single target and cue forms of a generalization row are checked.</summary>
    [Theory]
    [InlineData(6, "novel-grammar", "kind 'novel-grammar' must be")]
    [InlineData(7, "wind:air", "family 'wind:air' must be")]
    [InlineData(8, "-", "a novel-vocabulary row must name its cue forms")]
    [InlineData(8, "plumber|basement", "cue form 'basement' does not occur")]
    [InlineData(8, "Plumber", "cue form 'Plumber' must be one lower-case word")]
    [InlineData(8, "leads", "cue form 'leads' must be one lower-case word that is not a form of the target")]
    [InlineData(9, "maybe", "cue_unsolvable 'maybe' must be yes or no")]
    [InlineData(10, "synthetic", "source 'synthetic' must be 'generalization'")]
    [InlineData(11, "Public-Domain", "license 'Public-Domain' must be CC0-1.0")]
    public void EvaluationOverlayChecker_CheckGeneralization_MalformedColumn_IsFinding(int column, string value, string message)
    {
        // Arrange
        using var fixture = new DatasetFixture();
        var row = NovelVocabularyRow.ToArray();
        row[column] = value;

        // Act
        var report = CheckGeneralization(fixture, SmallShape, row, NovelConstructionRow);

        // Assert
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, message), string.Join('\n', report.Findings));
    }

    /// <summary>Proves a sentence outside 6 to 30 words or with a second form of the target's lemma is a finding.</summary>
    [Theory]
    [InlineData("Old lead pipes rust.", "lead", "the sentence has 4 words (allowed 6 to 30)")]
    [InlineData("The plumber said the lead fitting leads nowhere now.", "lead", "words of the target's lemma (exactly one allowed)")]
    public void EvaluationOverlayChecker_CheckGeneralization_LengthOrSecondTarget_IsFinding(string sentence, string word, string message)
    {
        // Arrange
        using var fixture = new DatasetFixture();
        var row = DatasetFixture.GeneralizationRowText("gen-0001", "lead.noun", sentence, word, "novel-construction", "lead:plumbing", "-");

        // Act
        var report = CheckGeneralization(fixture, SmallShape, row, NovelConstructionRow);

        // Assert
        Assert.True(DatasetFixture.Has(report, EvaluationOverlayChecker.CheckName, message), string.Join('\n', report.Findings));
    }

    /// <summary>Proves lemmas whose readings share a form are one lemma group, named by the smallest lemma.</summary>
    [Fact]
    public void EvaluationOverlayChecker_GroupOf_SharedForm_JoinsLemmas()
    {
        // Arrange: 'wound' becomes a form of both the wind reading and a new wound reading
        using var fixture = new DatasetFixture();
        fixture.AppendRow("readings.tsv", "wound.injury", "wound", "NOUN", "An injury.", "active");
        fixture.AppendRow("forms.tsv", "wound", "wound.injury", "base");
        fixture.AppendRow("forms.tsv", "wound", "wind.noun", "inflection");
        var report = new CheckReport();

        // Act
        var checker = new EvaluationOverlayChecker(TextEnDataSet.Load(fixture.Root, report), report);

        // Assert
        Assert.Equal("wind", checker.GroupOf("wound"));
        Assert.Equal("wind", checker.GroupOf("wind"));
        Assert.Equal("lead", checker.GroupOf("lead"));
        Assert.Equal("unknown", checker.GroupOf("unknown"));
    }
}
