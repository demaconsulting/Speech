using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingSubsystem;

/// <summary>
///     Unit tests for <see cref="EvaluationOverlay"/>: the loading of the regression overlay and the generalization set
///     with their strata, slices and bootstrap clusters, and the refusal of malformed files.
/// </summary>
public sealed class EvaluationOverlayTests
{
    /// <summary>Proves a regression row carries its origin and seen mark as stratum and slices and is its own cluster.</summary>
    [Fact]
    public void EvaluationOverlay_Load_Regression_RowsCarryStrataSlicesAndRowClusters()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        fixture.WriteRegression(
            ("reg-0001", "lead.verb", "They will lead the team today.", "listening", "no"),
            ("reg-0002", "lead.noun", "The lead pipe was very heavy.", "data-row", "yes"));

        // Act
        var rows = EvaluationOverlay.Load(fixture.Root, EvaluationOverlay.Regression);

        // Assert
        Assert.Equal(2, rows.Count);
        var first = rows[0];
        Assert.Equal("reg-0001", first.Row.Id);
        Assert.Equal("lead.verb", first.Row.ReadingId);
        Assert.Equal(10, first.Row.Start);
        Assert.Equal(14, first.Row.End);
        Assert.Equal("regression/listening/unseen", first.Row.Source);
        Assert.Equal("row:reg-0001", first.Cluster);
        Assert.Equal("listening", first.Group);
        Assert.Equal("-", first.Kind);
        Assert.Equal("no", first.SeenInTraining);
        Assert.False(first.CueUnsolvable);
        Assert.Equal(new[] { "all", "unseen", "origin:listening" }, first.Slices);
        Assert.Equal("regression/data-row/seen", rows[1].Row.Source);
        Assert.Equal(new[] { "all", "seen", "origin:data-row" }, rows[1].Slices);
    }

    /// <summary>Proves a generalization row carries its kind and cue-unsolvable mark as stratum and slices and resamples with its family.</summary>
    [Fact]
    public void EvaluationOverlay_Load_Generalization_RowsCarryStrataSlicesAndFamilyClusters()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        fixture.WriteGeneralization(
            ("gen-0001", "lead.verb", "Will they lead the hike?", "novel-construction", "lead:question", "yes"),
            ("gen-0002", "lead.noun", "A lead weight sank.", "novel-vocabulary", "lead:metal", "no"));

        // Act
        var rows = EvaluationOverlay.Load(fixture.Root, EvaluationOverlay.Generalization);

        // Assert
        Assert.Equal(2, rows.Count);
        Assert.Equal("generalization/novel-construction/cue-unsolvable", rows[0].Row.Source);
        Assert.Equal("family:lead:question", rows[0].Cluster);
        Assert.Equal("lead:question", rows[0].Group);
        Assert.Equal("novel-construction", rows[0].Kind);
        Assert.Equal("-", rows[0].SeenInTraining);
        Assert.True(rows[0].CueUnsolvable);
        Assert.Equal(new[] { "all", "kind:novel-construction", "cue_unsolvable:yes", "family:lead:question" }, rows[0].Slices);
        Assert.Equal("generalization/novel-vocabulary/cue-solvable", rows[1].Row.Source);
        Assert.False(rows[1].CueUnsolvable);
        Assert.Equal(new[] { "all", "kind:novel-vocabulary", "cue_unsolvable:no", "family:lead:metal" }, rows[1].Slices);
    }

    /// <summary>Proves a file with its header only loads as no rows.</summary>
    [Fact]
    public void EvaluationOverlay_Load_HeaderOnly_HasNoRows()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        fixture.WriteGeneralization();

        // Act
        var rows = EvaluationOverlay.Load(fixture.Root, EvaluationOverlay.Generalization);

        // Assert
        Assert.Empty(rows);
    }

    /// <summary>Proves a missing file, an offset that is not an integer and a span outside the sentence stop the load with a data error that names the file.</summary>
    [Fact]
    public void EvaluationOverlay_Load_MissingOrMalformed_ThrowsInvalidDataException()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var missing = Record.Exception(() => EvaluationOverlay.Load(fixture.Root, EvaluationOverlay.Regression));
        fixture.WriteRegression(("reg-0001", "lead.verb", "They will lead the team today.", "listening", "no"));
        fixture.Replace("regression.tsv", "\t10\t14\t", "\tten\t14\t");
        var notInteger = Record.Exception(() => EvaluationOverlay.Load(fixture.Root, EvaluationOverlay.Regression));
        fixture.Replace("regression.tsv", "\tten\t14\t", "\t10\t99\t");

        // Act
        var outside = Record.Exception(() => EvaluationOverlay.Load(fixture.Root, EvaluationOverlay.Regression));

        // Assert
        Assert.IsType<InvalidDataException>(missing);
        Assert.Contains("regression.tsv", missing.Message);
        Assert.IsType<InvalidDataException>(notInteger);
        Assert.Contains("regression.tsv", notInteger.Message);
        Assert.IsType<InvalidDataException>(outside);
        Assert.Contains("reg-0001", outside.Message);
    }

    /// <summary>Proves the overlay names, their files and their order, and that another name is not an overlay.</summary>
    [Fact]
    public void EvaluationOverlay_Names_MapToFilesInOrdinalOrder()
    {
        // Act / Assert
        Assert.Equal(new[] { "generalization", "regression" }, EvaluationOverlay.Sets);
        Assert.Equal("regression.tsv", EvaluationOverlay.FileOf(EvaluationOverlay.Regression));
        Assert.Equal("generalization.tsv", EvaluationOverlay.FileOf(EvaluationOverlay.Generalization));
        Assert.True(EvaluationOverlay.IsOverlay("regression"));
        Assert.False(EvaluationOverlay.IsOverlay("dev"));
        Assert.Throws<ArgumentException>(() => EvaluationOverlay.FileOf("dev"));
    }
}
