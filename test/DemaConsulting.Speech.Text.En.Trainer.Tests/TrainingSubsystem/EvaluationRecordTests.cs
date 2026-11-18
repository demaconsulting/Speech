using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingSubsystem;

/// <summary>
///     Unit tests for <see cref="EvaluationRecord"/>: the deterministic tab-separated record files, their exact
///     content for a hand-built evaluation, and the comparison with a folder.
/// </summary>
public sealed class EvaluationRecordTests : IDisposable
{
    /// <summary>A temporary folder for record files.</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "texten-record-" + Guid.NewGuid().ToString("N"));

    /// <summary>A hand-built evaluation: one natural holdout set and two seeds.</summary>
    private static readonly EvaluationReport Sample = new(
        "abc123",
        EvaluationSettings.Default,
        [
            new SetEvaluation(
                "holdout-natural",
                1167,
                1167,
                10,
                1167,
                1100,
                1058,
                800,
                790,
                [new GroupTally("harvest-masc", 358, 340, 330), new GroupTally("harvest-masc-llm", 148, 135, 100)],
                [new GroupTally("read.past", 20, 12, 20), new GroupTally("record.noun", 30, 30, 29)],
                [new GroupTally("<0.600", 40, 20, 0), new GroupTally("0.600-0.899", 100, 80, 0), new GroupTally(">=0.900", 1027, 1000, 0), new GroupTally("none", 0, 0, 0)],
                new BootstrapInterval(266, 360, 150, 580, true),
                new ParityCounts(1167, 0, 0)),
        ],
        [new SeedResult(1, 1167, 1100, 1972, 1900), new SeedResult(2, 1167, 1090, 1972, 1910)]);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    /// <summary>Proves the summary row holds the exact integer figures and basis-point ratios of the evaluation.</summary>
    [Fact]
    public void EvaluationRecord_Render_Summary_HasExactFigures()
    {
        // Act
        var files = EvaluationRecord.Render(Sample);

        // Assert: 1100/1167 = 94.26%, 1058/1167 = 90.66%, 800/1167 = 68.55%, 790/1167 = 67.69%
        var lines = files[EvaluationRecord.SummaryFile].Split('\n');
        Assert.StartsWith("set\trows\taligned", lines[0], StringComparison.Ordinal);
        Assert.Equal("holdout-natural\t1167\t1167\t10\t1167\t1100\t9426\t1058\t9066\t800\t6855\t790\t6769\t360\t150\t580\tyes\t266\t1167\t0\t0", lines[1]);
        Assert.Equal(string.Empty, lines[2]);
    }

    /// <summary>Proves the gate file decides the verdict from the natural holdout and shows the thresholds.</summary>
    [Fact]
    public void EvaluationRecord_Render_Gate_HasThresholdsAndVerdict()
    {
        // Act
        var gate = EvaluationRecord.Render(Sample)[EvaluationRecord.GateFile];

        // Assert
        Assert.Contains("image_sha256\tabc123\n", gate);
        Assert.Contains("gate_min_pooled_accuracy_per_thousand\t930\n", gate);
        Assert.Contains("confidence_percent\t95\n", gate);
        Assert.Contains("accuracy_ok\tyes\n", gate);
        Assert.Contains("interval_ok\tyes\n", gate);
        Assert.EndsWith("verdict\tpass\n", gate, StringComparison.Ordinal);
    }

    /// <summary>Proves the verdict is fail when a condition does not hold and not evaluated when there is no natural holdout.</summary>
    [Fact]
    public void EvaluationRecord_Render_Gate_FailAndNotEvaluated()
    {
        // Arrange
        var weak = Sample with { Sets = [Sample.Sets[0] with { Correct = 1000, Interval = new BootstrapInterval(266, 360, 0, 580, false) }] };
        var none = Sample with { Sets = [] };

        // Act
        var failing = EvaluationRecord.Render(weak)[EvaluationRecord.GateFile];
        var skipped = EvaluationRecord.Render(none)[EvaluationRecord.GateFile];

        // Assert
        Assert.Contains("accuracy_ok\tno\n", failing);
        Assert.Contains("interval_ok\tno\n", failing);
        Assert.EndsWith("verdict\tfail\n", failing, StringComparison.Ordinal);
        Assert.EndsWith("verdict\tnot evaluated\n", skipped, StringComparison.Ordinal);
    }

    /// <summary>Proves the group files list every group of every set with its basis-point accuracy.</summary>
    [Fact]
    public void EvaluationRecord_Render_Groups_ListEveryGroup()
    {
        // Act
        var files = EvaluationRecord.Render(Sample);

        // Assert
        Assert.Equal(
            "set\tsource\trows\tcorrect\taccuracy_bp\tupos_baseline_correct\nholdout-natural\tharvest-masc\t358\t340\t9497\t330\nholdout-natural\tharvest-masc-llm\t148\t135\t9122\t100\n",
            files[EvaluationRecord.BySourceFile]);
        Assert.Equal(
            "set\treading\trows\tcorrect\taccuracy_bp\tupos_baseline_correct\nholdout-natural\tread.past\t20\t12\t6000\t20\nholdout-natural\trecord.noun\t30\t30\t10000\t29\n",
            files[EvaluationRecord.ByReadingFile]);
        Assert.Contains("holdout-natural\t>=0.900\t1027\t1000\t9737\t0\n", files[EvaluationRecord.ConfidenceFile]);
    }

    /// <summary>Proves the seed file lists each seed and the mean, minimum and maximum of the basis-point accuracies.</summary>
    [Fact]
    public void EvaluationRecord_Render_Seeds_HasRowsAndSpread()
    {
        // Act
        var seeds = EvaluationRecord.Render(Sample)[EvaluationRecord.SeedsFile];

        // Assert: natural 1100/1167 = 9426 and 1090/1167 = 9340; independent 1900/1972 = 9635 and 1910/1972 = 9686
        Assert.Equal(
            "seed\tnatural_rows\tnatural_correct\tnatural_bp\tindependent_rows\tindependent_correct\tindependent_bp\n"
            + "1\t1167\t1100\t9426\t1972\t1900\t9635\n"
            + "2\t1167\t1090\t9340\t1972\t1910\t9686\n"
            + "mean\t\t\t9383\t\t\t9661\n"
            + "min\t\t\t9340\t\t\t9635\n"
            + "max\t\t\t9426\t\t\t9686\n",
            seeds);
    }

    /// <summary>Proves rendering is deterministic, in ordinal file-name order, with line feeds only.</summary>
    [Fact]
    public void EvaluationRecord_Render_IsDeterministicWithLineFeedsOnly()
    {
        // Act
        var first = EvaluationRecord.Render(Sample);
        var second = EvaluationRecord.Render(Sample);

        // Assert
        Assert.Equal(first.Keys, second.Keys);
        Assert.Equal(first.Keys.Order(StringComparer.Ordinal), first.Keys);
        Assert.All(first, pair =>
        {
            Assert.Equal(pair.Value, second[pair.Key]);
            Assert.DoesNotContain('\r', pair.Value);
            Assert.EndsWith("\n", pair.Value, StringComparison.Ordinal);
        });
    }

    /// <summary>Proves a written record compares as current, and an edited, deleted or never written file is named as stale.</summary>
    [Fact]
    public void EvaluationRecord_WriteThenCompare_DetectsEditedAndMissingFiles()
    {
        // Arrange
        EvaluationRecord.Write(Sample, _directory);
        var current = EvaluationRecord.Compare(Sample, _directory);
        File.AppendAllText(Path.Combine(_directory, EvaluationRecord.SummaryFile), "extra\n");
        File.Delete(Path.Combine(_directory, EvaluationRecord.SeedsFile));

        // Act
        var stale = EvaluationRecord.Compare(Sample, _directory);

        // Assert
        Assert.Empty(current);
        Assert.Equal([EvaluationRecord.SeedsFile, EvaluationRecord.SummaryFile], stale);
    }

    /// <summary>Proves percentages format from basis points with two decimals and an optional sign.</summary>
    [Theory]
    [InlineData(9312, false, "93.12%")]
    [InlineData(246, true, "+2.46%")]
    [InlineData(-5, true, "-0.05%")]
    public void EvaluationRecord_FormatPercent_FormatsBasisPoints(int basisPoints, bool signed, string expected)
    {
        // Act / Assert
        Assert.Equal(expected, EvaluationRecord.FormatPercent(basisPoints, signed));
    }
}
