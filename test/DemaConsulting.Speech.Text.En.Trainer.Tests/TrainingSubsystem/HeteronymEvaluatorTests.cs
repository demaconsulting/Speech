using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingSubsystem;

/// <summary>
///     Unit tests for <see cref="HeteronymEvaluator"/>: the exact integer arithmetic of the paired clustered
///     bootstrap, the quality gate decision, and the metrics, baselines and parity computed on fixture data.
/// </summary>
public sealed class HeteronymEvaluatorTests
{
    /// <summary>Evaluation settings with few resamples so tests are fast; the gate threshold is the committed one.</summary>
    private static readonly EvaluationSettings Settings = EvaluationSettings.Default with { Resamples = 2000 };

    /// <summary>Proves ratios convert to basis points with integer rounding half away from zero.</summary>
    [Theory]
    [InlineData(1, 3, 3333)]
    [InlineData(-2, 3, -6667)]
    [InlineData(1, 32, 313)]
    [InlineData(-1, 32, -313)]
    [InlineData(1, 0, 0)]
    public void HeteronymEvaluator_RoundBasisPoints_RoundsHalfAwayFromZero(long numerator, long denominator, int expected)
    {
        // Act / Assert
        Assert.Equal(expected, HeteronymEvaluator.RoundBasisPoints(numerator, denominator));
    }

    /// <summary>Proves identical clusters give a degenerate interval at the exact difference: every resample has the same ratio.</summary>
    [Fact]
    public void HeteronymEvaluator_Bootstrap_IdenticalClusters_GiveExactDegenerateInterval()
    {
        // Arrange: each cluster has 4 rows, the model decides 3 and the baseline 1, so every resample has difference 2/4
        var clusters = Enumerable.Range(0, 25).Select(i => new ClusterStats($"c{i}", 4, 3, 1)).ToList();

        // Act
        var interval = HeteronymEvaluator.Bootstrap(clusters, Settings);

        // Assert
        Assert.Equal(new BootstrapInterval(25, 5000, 5000, 5000, true), interval);
    }

    /// <summary>Proves a model that is exactly as good as the baseline everywhere has a difference of zero and a lower bound that is not above zero.</summary>
    [Fact]
    public void HeteronymEvaluator_Bootstrap_ModelEqualsBaseline_LowerBoundIsNotPositive()
    {
        // Arrange
        var clusters = Enumerable.Range(0, 30).Select(i => new ClusterStats($"c{i}", 10, 7, 7)).ToList();

        // Act
        var interval = HeteronymEvaluator.Bootstrap(clusters, Settings);

        // Assert
        Assert.Equal(0, interval.DifferenceBasisPoints);
        Assert.Equal(0, interval.LowerBasisPoints);
        Assert.Equal(0, interval.UpperBasisPoints);
        Assert.False(interval.LowerIsPositive);
    }

    /// <summary>Proves a model that is mostly better has an interval around the point estimate with a positive lower bound.</summary>
    [Fact]
    public void HeteronymEvaluator_Bootstrap_ModelClearlyBetter_HasPositiveLowerBoundAroundTheEstimate()
    {
        // Arrange: 40 clusters of 10 rows; the model decides 8, 9 or 10, the baseline 5, so the differences are 3, 4 and 5
        var clusters = Enumerable.Range(0, 40).Select(i => new ClusterStats($"c{i}", 10, 8 + (i % 3), 5)).ToList();

        // Act
        var interval = HeteronymEvaluator.Bootstrap(clusters, Settings);

        // Assert: the estimate is exactly 159 rows in 400
        Assert.Equal(3975, interval.DifferenceBasisPoints);
        Assert.True(interval.LowerIsPositive);
        Assert.InRange(interval.LowerBasisPoints, 1, 3975);
        Assert.InRange(interval.UpperBasisPoints, 3975, 10000);
    }

    /// <summary>Proves a single cluster has a degenerate interval at its own difference, since there is nothing to resample.</summary>
    [Fact]
    public void HeteronymEvaluator_Bootstrap_SingleCluster_IntervalEqualsTheEstimate()
    {
        // Arrange
        var clusters = new[] { new ClusterStats("only-document", 50, 45, 30) };

        // Act
        var interval = HeteronymEvaluator.Bootstrap(clusters, Settings);

        // Assert
        Assert.Equal(new BootstrapInterval(1, 3000, 3000, 3000, true), interval);
    }

    /// <summary>Proves rows of one cluster are resampled together: the same rows as one document per group are less certain than as one cluster per row.</summary>
    [Fact]
    public void HeteronymEvaluator_Bootstrap_RowsOfOneDocumentResampleTogether_SoTheIntervalIsWider()
    {
        // Arrange: 200 rows with 50 improvements and 150 neutral rows, as 10 documents (all improvements in 5) or as 200 single-row clusters
        var documents = Enumerable.Range(0, 10).Select(i => new ClusterStats($"d{i}", 20, i < 5 ? 20 : 10, 10)).ToList();
        var singles = Enumerable.Range(0, 200).Select(i => new ClusterStats($"r{i}", 1, i < 50 ? 1 : 0, 0)).ToList();
        Assert.Equal(documents.Sum(c => c.ModelCorrect - c.BaselineCorrect), singles.Sum(c => c.ModelCorrect - c.BaselineCorrect));

        // Act
        var clustered = HeteronymEvaluator.Bootstrap(documents, Settings);
        var perRow = HeteronymEvaluator.Bootstrap(singles, Settings);

        // Assert: the same estimate, a wider interval when the rows share a document
        Assert.Equal(clustered.DifferenceBasisPoints, perRow.DifferenceBasisPoints);
        Assert.True(clustered.UpperBasisPoints - clustered.LowerBasisPoints > perRow.UpperBasisPoints - perRow.LowerBasisPoints);
    }

    /// <summary>Proves the same seed gives the same interval and no clusters give the empty interval.</summary>
    [Fact]
    public void HeteronymEvaluator_Bootstrap_FixedSeed_IsRepeatableAndEmptyInputIsEmpty()
    {
        // Arrange
        var clusters = Enumerable.Range(0, 12).Select(i => new ClusterStats($"c{i}", 3, i % 4, i % 3)).ToList();

        // Act
        var first = HeteronymEvaluator.Bootstrap(clusters, Settings);
        var second = HeteronymEvaluator.Bootstrap(clusters, Settings);
        var empty = HeteronymEvaluator.Bootstrap([], Settings);

        // Assert
        Assert.Equal(first, second);
        Assert.Equal(new BootstrapInterval(0, 0, 0, 0, false), empty);
    }

    /// <summary>Proves the gate needs both the pooled accuracy at the threshold and a lower bound above zero, and the threshold is inclusive.</summary>
    [Theory]
    [InlineData(930, 1000, true, true)]
    [InlineData(929, 1000, false, true)]
    public void HeteronymEvaluator_DecideGate_AccuracyThresholdIsInclusive(int correct, int rows, bool accuracyOk, bool intervalOk)
    {
        // Arrange
        var natural = Evaluation(rows, correct, new BootstrapInterval(10, 300, 100, 500, true));

        // Act
        var verdict = HeteronymEvaluator.DecideGate(natural, Settings);

        // Assert
        Assert.Equal(accuracyOk, verdict.AccuracyOk);
        Assert.Equal(intervalOk, verdict.IntervalOk);
        Assert.Equal(accuracyOk && intervalOk, verdict.Passed);
    }

    /// <summary>Proves a lower bound that is not above zero fails the gate however accurate the model is.</summary>
    [Fact]
    public void HeteronymEvaluator_DecideGate_LowerBoundNotPositive_Fails()
    {
        // Arrange
        var natural = Evaluation(1000, 990, new BootstrapInterval(10, 300, 0, 500, false));

        // Act
        var verdict = HeteronymEvaluator.DecideGate(natural, Settings);

        // Assert
        Assert.True(verdict.AccuracyOk);
        Assert.False(verdict.IntervalOk);
        Assert.False(verdict.Passed);
    }

    /// <summary>Proves a set without rows never passes the gate.</summary>
    [Fact]
    public void HeteronymEvaluator_DecideGate_NoRows_Fails()
    {
        // Act
        var verdict = HeteronymEvaluator.DecideGate(Evaluation(0, 0, new BootstrapInterval(0, 0, 0, 0, false)), Settings);

        // Assert
        Assert.False(verdict.Passed);
    }

    /// <summary>Proves the natural holdout fixture is scored exactly: accuracy, the three baselines, coverage, strata and clusters.</summary>
    [Fact]
    public void HeteronymEvaluator_Evaluate_NaturalHoldout_ComputesExactMetrics()
    {
        // Arrange: hints that never match make the own-UPOS baseline the first candidate, so every baseline is the verb
        using var fixture = new TrainingFixture("ADV", "ADV");
        var (evaluator, image) = Prepare(fixture);

        // Act
        var set = evaluator.Evaluate("holdout-natural", image);

        // Assert: 20 verb rows and 19 noun rows are patterns the model decides; one noun pattern is labeled verb
        Assert.Equal(40, set.Rows);
        Assert.Equal(40, set.Aligned);
        Assert.Equal(0, set.SingleCandidate);
        Assert.Equal(40, set.Covered);
        Assert.Equal(39, set.Correct);
        Assert.Equal(21, set.UposBaselineCorrect);
        Assert.Equal(21, set.MajorityBaselineCorrect);
        Assert.Equal(21, set.FirstBaselineCorrect);
        Assert.Equal(new GroupTally("harvest-masc", 40, 39, 21), Assert.Single(set.BySource));
        Assert.Equal(40, set.Interval.Clusters);
        Assert.True(set.Interval.LowerIsPositive);
        Assert.Equal(4500, set.Interval.DifferenceBasisPoints);
    }

    /// <summary>Proves the per-reading tallies are listed worst accuracy first with exact counts.</summary>
    [Fact]
    public void HeteronymEvaluator_Evaluate_NaturalHoldout_ListsReadingsWorstFirst()
    {
        // Arrange
        using var fixture = new TrainingFixture("ADV", "ADV");
        var (evaluator, image) = Prepare(fixture);

        // Act
        var set = evaluator.Evaluate("holdout-natural", image);

        // Assert: lead.verb has 21 rows and one wrong; lead.noun has 19 rows, all right
        Assert.Equal(["lead.verb", "lead.noun"], set.ByReading.Select(tally => tally.Name));
        Assert.Equal(new GroupTally("lead.verb", 21, 20, 21), set.ByReading[0]);
        Assert.Equal(new GroupTally("lead.noun", 19, 19, 0), set.ByReading[1]);
    }

    /// <summary>Proves a row whose target is not a runtime token counts wrong end to end, has no reading, and is reported as not aligned.</summary>
    [Fact]
    public void HeteronymEvaluator_Evaluate_UnalignedRow_CountsWrongAndUncovered()
    {
        // Arrange
        using var fixture = new TrainingFixture("ADV", "ADV");
        var (evaluator, image) = Prepare(fixture);

        // Act
        var set = evaluator.Evaluate("holdout-independent", image);

        // Assert: 10 verb and 10 noun patterns are decided; the 21st row has no runtime token for its target
        Assert.Equal(21, set.Rows);
        Assert.Equal(20, set.Aligned);
        Assert.Equal(20, set.Covered);
        Assert.Equal(20, set.Correct);
        Assert.Equal(10, set.FirstBaselineCorrect);
        Assert.Equal(21, set.Interval.Clusters);
        Assert.Equal(1, set.ByConfidence.Single(bucket => bucket.Name == "none").Rows);
    }

    /// <summary>Proves the own-part-of-speech baseline picks the one candidate whose hint holds the tagger's tag and falls back to the first candidate otherwise.</summary>
    [Fact]
    public void HeteronymEvaluator_Evaluate_UposBaseline_UsesTheTagWhenTheHintIsUnique()
    {
        // Arrange: informative hints; "will lead the" is a verb and "the lead pipe" a noun for the tagger
        using var fixture = new TrainingFixture("VERB", "NOUN");
        var (evaluator, image) = Prepare(fixture);

        // Act
        var informative = evaluator.Evaluate("holdout-independent", image);

        // Assert: better than always taking the first candidate, whatever else the tagger does
        Assert.True(informative.UposBaselineCorrect > informative.FirstBaselineCorrect);
    }

    /// <summary>Proves the majority baseline is the reading most often labeled for the form in the training file.</summary>
    [Fact]
    public void HeteronymEvaluator_Evaluate_MajorityBaseline_FollowsTheTrainingLabels()
    {
        // Arrange: make the noun the training majority by adding noun rows
        using var fixture = new TrainingFixture("ADV", "ADV");
        for (var i = 0; i < 6; i++)
        {
            fixture.AppendRow("train.tsv", $"syn-extra-n{i}", "lead.noun", $"The lead block number {i} was heavy.", "lead", "synthetic");
        }

        var (evaluator, image) = Prepare(fixture);

        // Act
        var set = evaluator.Evaluate("holdout-independent", image);

        // Assert: the noun is now the majority (22 against 18), the first candidate still the verb
        Assert.Equal(11, set.MajorityBaselineCorrect);
        Assert.Equal(10, set.FirstBaselineCorrect);
    }

    /// <summary>Proves the trainer's direct predictions and the analyzer's agree on every aligned row of every set, the training rows included, and that the rows whose target is not a runtime token are reported as not aligned.</summary>
    [Fact]
    public void HeteronymEvaluator_Evaluate_AllSets_TrainerAndAnalyzerAgreeOnEveryRow()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        fixture.AppendRow("train.tsv", "syn-unaligned", "lead.noun", "The leader spoke softly.", "lead", "synthetic");
        var (evaluator, image) = Prepare(fixture);

        // Act
        var sets = new[] { "train", "dev", "holdout-natural", "holdout-independent" }.Select(name => evaluator.Evaluate(name, image)).ToList();

        // Assert
        Assert.All(sets, set =>
        {
            Assert.Equal(set.Aligned, set.Parity.Compared);
            Assert.Equal(0, set.Parity.Mismatched);
            Assert.Equal(0, set.Parity.Suppressed);
        });
        Assert.Equal(37, sets[0].Rows);
        Assert.Equal(36, sets[0].Aligned);
    }

    /// <summary>Proves the direct-only evaluation decides the same rows as the end-to-end one.</summary>
    [Fact]
    public void HeteronymEvaluator_EvaluateDirect_AgreesWithEndToEndOnTheFixture()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var (evaluator, image) = Prepare(fixture);

        // Act
        var endToEnd = evaluator.Evaluate("holdout-natural", image);
        var direct = evaluator.EvaluateDirect("holdout-natural", image);

        // Assert
        Assert.Equal(endToEnd.Correct, direct.Correct);
        Assert.Equal(endToEnd.Covered, direct.Covered);
        Assert.Equal(new ParityCounts(0, 0, 0), direct.Parity);
    }

    /// <summary>Proves a missing provenance table, or a harvested row that the table does not list, stops the evaluation of a harvested set with an error that names the file or the row.</summary>
    [Fact]
    public void HeteronymEvaluator_Evaluate_MissingProvenanceFile_ThrowsInvalidDataException()
    {
        // Arrange
        using var fixture = new TrainingFixture("ADV", "ADV");
        var (evaluator, image) = Prepare(fixture);
        var table = fixture.Read("harvest-provenance.tsv");
        File.Delete(fixture.PathOf("harvest-provenance.tsv"));

        // Act
        var missingFile = Assert.Throws<InvalidDataException>(() => evaluator.Evaluate("holdout-natural", image));
        var kept = table.Split('\n').Where(line => !line.StartsWith("harvest-h05\t", StringComparison.Ordinal));
        fixture.Write("harvest-provenance.tsv", string.Join('\n', kept));
        var missingRow = Assert.Throws<InvalidDataException>(() => evaluator.Evaluate("holdout-natural", image));

        // Assert
        Assert.Contains("harvest-provenance.tsv", missingFile.Message);
        Assert.Contains("holdout-natural.tsv", missingRow.Message);
        Assert.Contains("harvest-h05", missingRow.Message);
    }

    /// <summary>Proves two rows of one source document form one cluster, so the provenance table drives the clustering.</summary>
    [Fact]
    public void HeteronymEvaluator_Evaluate_RowsOfOneDocument_FormOneCluster()
    {
        // Arrange: harvest-h01 now comes from the document of harvest-h00
        using var fixture = new TrainingFixture("ADV", "ADV");
        fixture.Replace("harvest-provenance.tsv", "harvest-h01\tmasc:nat-doc-1\t", "harvest-h01\tmasc:nat-doc-0\t");
        var (evaluator, image) = Prepare(fixture);

        // Act
        var set = evaluator.Evaluate("holdout-natural", image);

        // Assert
        Assert.Equal(40, set.Rows);
        Assert.Equal(39, set.Interval.Clusters);
    }

    /// <summary>Proves synthetic and independently written rows are units of their own and need no provenance table.</summary>
    [Fact]
    public void HeteronymEvaluator_Evaluate_IndependentAndSyntheticRows_NeedNoProvenance()
    {
        // Arrange
        using var fixture = new TrainingFixture("ADV", "ADV");
        var (evaluator, image) = Prepare(fixture);
        File.Delete(fixture.PathOf("harvest-provenance.tsv"));

        // Act
        var independent = evaluator.Evaluate("holdout-independent", image);
        var train = evaluator.Evaluate("train", image);

        // Assert
        Assert.Equal(21, independent.Interval.Clusters);
        Assert.Equal(train.Rows, train.Interval.Clusters);
    }

    /// <summary>Proves an unknown set name is rejected.</summary>
    [Fact]
    public void HeteronymEvaluator_Evaluate_UnknownSet_ThrowsInvalidDataException()
    {
        // Arrange
        using var fixture = new TrainingFixture();
        var (evaluator, image) = Prepare(fixture);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => evaluator.Evaluate("holdout-secret", image));
    }

    /// <summary>Proves the clustering supplied by the caller decides which rows resample together.</summary>
    [Fact]
    public void HeteronymEvaluator_EvaluateRows_CustomClustering_GroupsRowsOfOneCluster()
    {
        // Arrange
        using var fixture = new TrainingFixture("ADV", "ADV");
        var (evaluator, image) = Prepare(fixture);
        var rows = RowPreparer.ReadRows(fixture.PathOf("holdout-independent.tsv"), "holdout-independent.tsv");

        // Act
        var single = evaluator.EvaluateRows("one-cluster", rows, image, static _ => "everything");
        var byReading = evaluator.EvaluateRows("reading-clusters", rows, image, static row => row.ReadingId);
        var perRow = evaluator.EvaluateRows("row-clusters", rows, image, static row => row.Id);

        // Assert: 21 rows, two readings
        Assert.Equal(21, single.Rows);
        Assert.Equal(1, single.Interval.Clusters);
        Assert.Equal(2, byReading.Interval.Clusters);
        Assert.Equal(21, perRow.Interval.Clusters);
        Assert.Equal(single.Correct, perRow.Correct);
        Assert.Equal(byReading.Correct, perRow.Correct);
    }

    /// <summary>Proves the interval of the accuracy encloses its point estimate on the end-to-end path, and is absent on the direct path and on a file set.</summary>
    [Fact]
    public void HeteronymEvaluator_EvaluateRows_AccuracyInterval_EnclosesPointEstimate()
    {
        // Arrange
        using var fixture = new TrainingFixture("ADV", "ADV");
        var (evaluator, image) = Prepare(fixture);
        var rows = RowPreparer.ReadRows(fixture.PathOf("holdout-independent.tsv"), "holdout-independent.tsv");

        // Act
        var endToEnd = evaluator.EvaluateRows("with-interval", rows, image, static row => row.Id);
        var direct = evaluator.EvaluateRowsDirect("direct", rows, image, static row => row.Id);
        var fromFile = evaluator.Evaluate("holdout-independent", image);

        // Assert
        var interval = endToEnd.AccuracyInterval;
        Assert.NotNull(interval);
        var accuracy = HeteronymEvaluator.RoundBasisPoints(endToEnd.Correct, endToEnd.Rows);
        Assert.Equal(accuracy, interval.DifferenceBasisPoints);
        Assert.InRange(accuracy, interval.LowerBasisPoints, interval.UpperBasisPoints);
        Assert.Equal(endToEnd.Interval.Clusters, interval.Clusters);
        Assert.Null(direct.AccuracyInterval);
        Assert.Equal(endToEnd.Correct, direct.Correct);
        Assert.Equal(new ParityCounts(0, 0, 0), direct.Parity);
        Assert.Null(fromFile.AccuracyInterval);
    }

    /// <summary>Proves an overlay is reported with and without the cue priors per slice, with family clusters, and that without a cue the model alone decides like the shipped classifier.</summary>
    [Fact]
    public void HeteronymEvaluator_EvaluateOverlay_NoCueFires_ReportsSlicesWithEqualModelOnly()
    {
        // Arrange: two families; one row is labeled against the model's clear pattern, so it is wrong both ways
        using var fixture = new TrainingFixture("ADV", "ADV");
        var (evaluator, image) = Prepare(fixture);
        fixture.WriteGeneralization(
            ("gen-0001", "lead.verb", "They will lead the march today.", "novel-construction", "lead:future", "yes"),
            ("gen-0002", "lead.verb", "Please lead the parade now.", "novel-construction", "lead:future", "no"),
            ("gen-0003", "lead.noun", "The lead roof was very heavy.", "novel-vocabulary", "lead:metal", "no"),
            ("gen-0004", "lead.verb", "A lead plate fell down.", "novel-vocabulary", "lead:metal", "no"));
        var rows = EvaluationOverlay.Load(fixture.Root, EvaluationOverlay.Generalization);

        // Act
        var overlay = evaluator.EvaluateOverlay("generalization", rows, image);

        // Assert: the shipped evaluation
        Assert.Equal(4, overlay.Shipped.Rows);
        Assert.Equal(3, overlay.Shipped.Correct);
        Assert.Equal(2, overlay.Shipped.Interval.Clusters);
        Assert.NotNull(overlay.Shipped.AccuracyInterval);
        Assert.Equal(1, overlay.CueUnsolvableRows);
        Assert.Contains(overlay.Shipped.BySource, tally => tally.Name == "generalization/novel-vocabulary/cue-solvable" && tally.Rows == 2);

        // Assert: the rows, with and without the cues
        Assert.Equal(new[] { "gen-0001", "gen-0002", "gen-0003", "gen-0004" }, overlay.Rows.Select(static row => row.Overlay.Row.Id));
        Assert.All(overlay.Rows, static row => Assert.Equal(row.Shipped, row.ModelOnly));
        Assert.All(overlay.Rows, static row => Assert.False(row.CueFired));
        Assert.Equal("lead.noun", overlay.Rows[3].Shipped);
        Assert.False(overlay.Rows[3].ShippedCorrect);

        // Assert: the slices, all first and the rest in ordinal order
        Assert.Equal(
            new[] { "all", "cue_unsolvable:no", "cue_unsolvable:yes", "family:lead:future", "family:lead:metal", "kind:novel-construction", "kind:novel-vocabulary" },
            overlay.Slices.Select(static slice => slice.Name));
        var all = overlay.Slices[0];
        Assert.Equal(4, all.Rows);
        Assert.Equal(3, all.ShippedCorrect);
        Assert.Equal(3, all.ModelOnlyCorrect);
        Assert.Equal(7500, all.ShippedAccuracy.DifferenceBasisPoints);
        Assert.Equal(2, all.ShippedAccuracy.Clusters);
        Assert.Equal(all.ShippedAccuracy, all.ModelOnlyAccuracy);
        Assert.Equal(new BootstrapInterval(2, 0, 0, 0, false), all.CueEffect);
        Assert.Equal(0, all.CueFired);
        Assert.Equal(0, all.Changed);
        var metal = overlay.Slices.Single(static slice => slice.Name == "family:lead:metal");
        Assert.Equal(2, metal.Rows);
        Assert.Equal(1, metal.ShippedCorrect);
        Assert.Equal(1, metal.ShippedAccuracy.Clusters);
    }

    /// <summary>Proves a row marked as not solvable by the curated cues stops the evaluation with a data error that names it and the reason when a cue test fires on it or it is not one runtime token.</summary>
    [Fact]
    public void HeteronymEvaluator_EvaluateOverlay_CueUnsolvableViolation_ThrowsNamingRowsAndReasons()
    {
        // Arrange: a past-time word in the clause of the target, and a target inside a longer word
        using var fixture = new TrainingFixture("ADV", "ADV");
        var (evaluator, image) = Prepare(fixture);
        fixture.WriteGeneralization(
            ("gen-0001", "lead.verb", "They will lead the march today.", "novel-construction", "lead:future", "yes"),
            ("gen-0002", "lead.verb", "Yesterday we lead the team.", "novel-construction", "lead:past", "yes"),
            ("gen-0003", "lead.noun", "The leader spoke softly.", "novel-vocabulary", "lead:metal", "yes"),
            ("gen-0004", "lead.verb", "Yesterday we lead the class.", "novel-construction", "lead:past", "no"));
        var rows = EvaluationOverlay.Load(fixture.Root, EvaluationOverlay.Generalization);

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => evaluator.EvaluateOverlay("generalization", rows, image));

        // Assert: only the two marked rows are named; the unmarked row with the same cue is not
        Assert.Contains("2 rows marked cue_unsolvable=yes", exception.Message);
        Assert.Contains("gen-0002 (past-time cue)", exception.Message);
        Assert.Contains("gen-0003 (not one runtime token)", exception.Message);
        Assert.DoesNotContain("gen-0001", exception.Message);
        Assert.DoesNotContain("gen-0004", exception.Message);
    }

    /// <summary>
    ///     Proves a row marked as not solvable by the curated cues on which the noun-form prior is added is named with
    ///     <c>noun-form cue</c> although the prior does not change its reading, and that the same row unmarked is evaluated.
    /// </summary>
    [Fact]
    public void HeteronymEvaluator_EvaluateOverlay_CueUnsolvableNounFormCueWithoutChange_ThrowsNamingTheCue()
    {
        // Arrange: a third reading of the form lead that is a noun of another lemma only, so lead tagged NOUN gets the
        // noun-form prior on lead.noun; the model's trained noun pattern already chooses lead.noun, so nothing changes
        using var fixture = new TrainingFixture();
        fixture.Write("readings.tsv", fixture.Read("readings.tsv") + string.Join('\t', "lead.extra", "plumb", "NOUN", "Noun of another lemma only.", "active") + "\n");
        fixture.Write("forms.tsv", fixture.Read("forms.tsv") + string.Join('\t', "lead", "lead.extra", "other") + "\n");
        var (evaluator, image) = Prepare(fixture);
        const string Sentence = "The lead pipe was very heavy.";
        fixture.WriteGeneralization(("gen-0001", "lead.noun", Sentence, "novel-vocabulary", "lead:metal", "yes"));
        var marked = EvaluationOverlay.Load(fixture.Root, EvaluationOverlay.Generalization);
        fixture.WriteGeneralization(("gen-0001", "lead.noun", Sentence, "novel-vocabulary", "lead:metal", "no"));
        var unmarked = EvaluationOverlay.Load(fixture.Root, EvaluationOverlay.Generalization);

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => evaluator.EvaluateOverlay("generalization", marked, image));
        var overlay = evaluator.EvaluateOverlay("generalization", unmarked, image);

        // Assert: the cue acts without changing the reading, and the marked row is refused naming the noun-form cue
        Assert.True(overlay.Rows[0].CueFired);
        Assert.Equal("lead.noun", overlay.Rows[0].Shipped);
        Assert.Equal(overlay.Rows[0].Shipped, overlay.Rows[0].ModelOnly);
        Assert.Contains("1 rows marked cue_unsolvable=yes", exception.Message);
        Assert.Contains("gen-0001 (noun-form cue)", exception.Message);
    }

    /// <summary>Proves the rows on which a cue prior acts are counted per slice, apart from the rows whose reading it changes.</summary>
    [Fact]
    public void HeteronymEvaluator_EvaluateOverlay_CueFires_CountsFiredRowsApartFromChangedRows()
    {
        // Arrange: lead.noun is the adjective reading; a degree adverb or a coordinated adjective stands beside the
        // target in two rows, so the adjective cue acts there, and the model's own weights already decide every row
        using var fixture = new TrainingFixture("VERB", "ADJ");
        var (evaluator, image) = Prepare(fixture);
        fixture.WriteRegression(
            ("reg-0001", "lead.noun", "The metal was lead and heavy.", "listening", "no"),
            ("reg-0002", "lead.verb", "They will lead the team today.", "data-row", "no"),
            ("reg-0003", "lead.noun", "The very lead pipe fell.", "data-row", "no"),
            ("reg-0004", "lead.noun", "A grey and lead pipe fell.", "data-row", "no"));
        var rows = EvaluationOverlay.Load(fixture.Root, EvaluationOverlay.Regression);

        // Act
        var overlay = evaluator.EvaluateOverlay("regression", rows, image);

        // Assert: the cue acts on the first and third rows and changes no reading
        Assert.Equal(new[] { true, false, true, false }, overlay.Rows.Select(static row => row.CueFired));
        Assert.All(overlay.Rows, static row => Assert.Equal(row.Shipped, row.ModelOnly));
        var all = overlay.Slices[0];
        Assert.Equal(4, all.Rows);
        Assert.Equal(4, all.ShippedCorrect);
        Assert.Equal(2, all.CueFired);
        Assert.Equal(0, all.Changed);
        Assert.Equal(1, overlay.Slices.Single(static slice => slice.Name == "origin:listening").CueFired);
        Assert.Equal(1, overlay.Slices.Single(static slice => slice.Name == "origin:data-row").CueFired);
    }

    /// <summary>Proves an overlay whose row id repeats is refused.</summary>
    [Fact]
    public void HeteronymEvaluator_EvaluateOverlay_RepeatedId_ThrowsInvalidDataException()
    {
        // Arrange
        using var fixture = new TrainingFixture("ADV", "ADV");
        var (evaluator, image) = Prepare(fixture);
        fixture.WriteRegression(
            ("reg-0001", "lead.verb", "They will lead the team today.", "listening", "no"),
            ("reg-0001", "lead.noun", "The lead pipe was very heavy.", "data-row", "yes"));
        var rows = EvaluationOverlay.Load(fixture.Root, EvaluationOverlay.Regression);

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => evaluator.EvaluateOverlay("regression", rows, image));

        // Assert
        Assert.Contains("reg-0001", exception.Message);
    }

    /// <summary>
    ///     Builds an evaluator over the fixture and the image of a model trained on it.
    /// </summary>
    /// <param name="fixture">The fixture.</param>
    /// <returns>The evaluator and the compressed model image.</returns>
    private static (HeteronymEvaluator Evaluator, byte[] Image) Prepare(TrainingFixture fixture)
    {
        var corpus = TrainingCorpus.Load(fixture.Root);
        var model = ModelSelection.Train(corpus, TrainingSettings.Default with { HashBits = 18 });
        return (new HeteronymEvaluator(corpus, fixture.Root, Settings), model.CompressedImage);
    }

    /// <summary>
    ///     Builds a set evaluation with only the figures the gate reads.
    /// </summary>
    /// <param name="rows">The rows.</param>
    /// <param name="correct">The rows decided correctly.</param>
    /// <param name="interval">The interval.</param>
    /// <returns>The evaluation.</returns>
    private static SetEvaluation Evaluation(int rows, int correct, BootstrapInterval interval) =>
        new("holdout-natural", rows, rows, 0, rows, correct, 0, 0, 0, [], [], [], interval, new ParityCounts(rows, 0, 0));
}
