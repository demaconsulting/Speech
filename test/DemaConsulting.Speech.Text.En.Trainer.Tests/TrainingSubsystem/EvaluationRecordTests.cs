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

    /// <summary>A hand-built evaluation: one natural holdout set, gated at 94.00%, with three rows of readings, and two seeds.</summary>
    private static readonly EvaluationReport Sample = new(
        "abc123",
        EvaluationSettings.Default with { GateThresholds = [new GateThreshold("holdout-natural", 9400)] },
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
                new ParityCounts(1167, 0, 0),
                new BootstrapInterval(266, 9426, 9300, 9550, true),
                [new RowReading("h-2", "read.past", "read.past"), new RowReading("h-1", "read.past", "read.present"), new RowReading("h-3", "bass.fish", "bass.music")]),
        ],
        [
            new SeedResult(1, [new SeedSetCount("holdout-independent", 1972, 1900), new SeedSetCount("holdout-natural", 1167, 1100)]),
            new SeedResult(2, [new SeedSetCount("holdout-independent", 1972, 1910), new SeedSetCount("holdout-natural", 1167, 1090)]),
        ]);

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

    /// <summary>Proves the gate file holds per gate set the counts, the accuracy interval, the threshold, the flips and the decision, then the verdict.</summary>
    [Fact]
    public void EvaluationRecord_Render_Gate_HasPerSetFiguresAndVerdict()
    {
        // Act
        var gate = EvaluationRecord.Render(Sample)[EvaluationRecord.GateFile];

        // Assert: 1100/1167 = 94.26% reaches 94.00%
        Assert.StartsWith("key\tvalue\nimage_sha256\tabc123\nprevious_image_sha256\tabc123\nconfidence_percent\t95\n", gate, StringComparison.Ordinal);
        Assert.Contains("holdout-natural_rows\t1167\nholdout-natural_clusters\t266\nholdout-natural_correct\t1100\nholdout-natural_accuracy_bp\t9426\n", gate);
        Assert.Contains("holdout-natural_ci_lower_bp\t9300\nholdout-natural_ci_upper_bp\t9550\nholdout-natural_threshold_bp\t9400\n", gate);
        Assert.Contains("holdout-natural_fixed\t0\nholdout-natural_broken\t0\nholdout-natural_pass\tyes\n", gate);
        Assert.EndsWith("verdict\tpass\n", gate, StringComparison.Ordinal);
    }

    /// <summary>Proves the verdict is fail when a set is below its threshold and not evaluated when a gate set is missing.</summary>
    [Fact]
    public void EvaluationRecord_Render_Gate_FailAndNotEvaluated()
    {
        // Arrange
        var weak = Sample with { Sets = [Sample.Sets[0] with { Correct = 1000 }] };
        var none = Sample with { Sets = [] };

        // Act
        var failing = EvaluationRecord.Render(weak)[EvaluationRecord.GateFile];
        var skipped = EvaluationRecord.Render(none)[EvaluationRecord.GateFile];

        // Assert
        Assert.Contains("holdout-natural_pass\tno\n", failing);
        Assert.EndsWith("verdict\tfail\n", failing, StringComparison.Ordinal);
        Assert.EndsWith("verdict\tnot evaluated\n", skipped, StringComparison.Ordinal);
    }

    /// <summary>Proves a first write lists every gate row ordered by id with the previous reading equal to the current one, so the flip report is empty.</summary>
    [Fact]
    public void EvaluationRecord_Write_FirstWrite_PreviousEqualsCurrentAndNoFlips()
    {
        // Act
        EvaluationRecord.Write(Sample, _directory);

        // Assert
        Assert.Equal(
            "set\tid\tgold\tprevious\tcurrent\n"
            + "holdout-natural\th-1\tread.past\tread.present\tread.present\n"
            + "holdout-natural\th-2\tread.past\tread.past\tread.past\n"
            + "holdout-natural\th-3\tbass.fish\tbass.music\tbass.music\n",
            File.ReadAllText(Path.Combine(_directory, EvaluationRecord.GateRowsFile)));
        Assert.Equal("set\tid\tgold\tprevious\tcurrent\tchange\n", File.ReadAllText(Path.Combine(_directory, EvaluationRecord.FlipsFile)));
        Assert.Empty(EvaluationRecord.Compare(Sample, _directory));
    }

    /// <summary>Proves a rewrite with the same model image keeps the committed previous column and image, and a new row gets '-'.</summary>
    [Fact]
    public void EvaluationRecord_Write_SameModel_KeepsThePreviousColumn()
    {
        // Arrange: a committed record of the model before, then the current model, then the current model with one row more
        var before = WithReadings(Sample with { ImageSha256 = "old" }, ("h-1", "read.past"), ("h-2", "read.past"), ("h-3", "bass.music"));
        EvaluationRecord.Write(before, _directory);
        EvaluationRecord.Write(Sample, _directory);
        var grown = WithReadings(Sample, ("h-1", "read.present"), ("h-2", "read.past"), ("h-3", "bass.music"), ("h-4", "read.past"));

        // Act
        EvaluationRecord.Write(grown, _directory);

        // Assert: the previous column and image stay those of the model before; h-4 is new
        var rows = File.ReadAllText(Path.Combine(_directory, EvaluationRecord.GateRowsFile));
        Assert.Contains("holdout-natural\th-1\tread.past\tread.past\tread.present\n", rows);
        Assert.Contains("holdout-natural\th-4\tread.past\t-\tread.past\n", rows);
        Assert.Contains("previous_image_sha256\told\n", File.ReadAllText(Path.Combine(_directory, EvaluationRecord.GateFile)));
        Assert.Equal(
            "set\tid\tgold\tprevious\tcurrent\tchange\nholdout-natural\th-1\tread.past\tread.past\tread.present\tbroken\n",
            File.ReadAllText(Path.Combine(_directory, EvaluationRecord.FlipsFile)));
    }

    /// <summary>Proves a write with a new model image rotates: the committed current column becomes the previous one, and the flips and their counts follow.</summary>
    [Fact]
    public void EvaluationRecord_Write_NewModel_RotatesAndReportsFlips()
    {
        // Arrange
        EvaluationRecord.Write(Sample, _directory);
        var retrained = WithReadings(Sample with { ImageSha256 = "def456" }, ("h-1", "read.past"), ("h-2", "none"), ("h-3", "bass.music"));

        // Act
        EvaluationRecord.Write(retrained, _directory);

        // Assert
        Assert.Equal(
            "set\tid\tgold\tprevious\tcurrent\tchange\n"
            + "holdout-natural\th-1\tread.past\tread.present\tread.past\tfixed\n"
            + "holdout-natural\th-2\tread.past\tread.past\tnone\tbroken\n",
            File.ReadAllText(Path.Combine(_directory, EvaluationRecord.FlipsFile)));
        var gate = File.ReadAllText(Path.Combine(_directory, EvaluationRecord.GateFile));
        Assert.Contains("image_sha256\tdef456\nprevious_image_sha256\tabc123\n", gate);
        Assert.Contains("holdout-natural_fixed\t1\nholdout-natural_broken\t1\n", gate);
        Assert.Empty(EvaluationRecord.Compare(retrained, _directory));
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

    /// <summary>Proves the seed file lists each seed per gate set and the mean, minimum, maximum and threshold by rule.</summary>
    [Fact]
    public void EvaluationRecord_Render_Seeds_HasRowsAndSpread()
    {
        // Act
        var seeds = EvaluationRecord.Render(Sample)[EvaluationRecord.SeedsFile];

        // Assert: natural 1100/1167 = 9426 and 1090/1167 = 9340; independent 1900/1972 = 9635 and 1910/1972 = 9686
        Assert.Equal(
            "seed\tholdout-independent_rows\tholdout-independent_correct\tholdout-independent_bp\tholdout-natural_rows\tholdout-natural_correct\tholdout-natural_bp\n"
            + "1\t1972\t1900\t9635\t1167\t1100\t9426\n"
            + "2\t1972\t1910\t9686\t1167\t1090\t9340\n"
            + "mean\t\t\t9661\t\t\t9383\n"
            + "min\t\t\t9635\t\t\t9340\n"
            + "max\t\t\t9686\t\t\t9426\n"
            + $"threshold_by_rule\t\t\t{HeteronymEvaluator.DeriveGateThreshold([1900, 1910], 1972)}\t\t\t{HeteronymEvaluator.DeriveGateThreshold([1100, 1090], 1167)}\n",
            seeds);
    }

    /// <summary>Proves the overlays are appended after the data sets, leaving every line of the data sets unchanged, and are listed per row and per slice with and without the cue priors.</summary>
    [Fact]
    public void EvaluationRecord_Render_Overlays_AppendedWithRowsAndCuratedAblation()
    {
        // Arrange: a generalization overlay of two rows in one family; the cue priors fix one row
        var shipped = new SetEvaluation(
            "generalization",
            2,
            2,
            0,
            2,
            2,
            1,
            1,
            1,
            [new GroupTally("generalization/novel-vocabulary/cue-solvable", 2, 2, 1)],
            [new GroupTally("lead.noun", 2, 2, 1)],
            [new GroupTally("<0.600", 0, 0, 0), new GroupTally("0.600-0.899", 1, 1, 0), new GroupTally(">=0.900", 1, 1, 0), new GroupTally("none", 0, 0, 0)],
            new BootstrapInterval(1, 5000, 5000, 5000, true),
            new ParityCounts(2, 0, 0),
            new BootstrapInterval(1, 10000, 10000, 10000, true));
        OverlayRow Row(string id) => new(
            new LabeledRow(id, "lead.noun", "The lead pipe.", 4, 8, "generalization/novel-vocabulary/cue-solvable"),
            "family:lead:metal",
            "lead:metal",
            "novel-vocabulary",
            "-",
            false,
            ["all", "family:lead:metal"]);
        var overlay = new OverlayEvaluation(
            shipped,
            [new OverlayRowOutcome(Row("gen-0001"), "lead.noun", "lead.noun", false), new OverlayRowOutcome(Row("gen-0002"), "lead.noun", "lead.verb", true)],
            [new OverlaySlice("all", 2, 2, new BootstrapInterval(1, 10000, 10000, 10000, true), 1, new BootstrapInterval(1, 5000, 5000, 5000, true), new BootstrapInterval(1, 5000, 5000, 5000, true), 1, 1)],
            0);
        var report = Sample with { Sets = [.. Sample.Sets, shipped], Overlays = [overlay] };

        // Act
        var plain = EvaluationRecord.Render(Sample);
        var files = EvaluationRecord.Render(report);

        // Assert: the data-set lines are a byte-identical prefix and the overlay lines follow
        foreach (var name in new[] { EvaluationRecord.SummaryFile, EvaluationRecord.BySourceFile, EvaluationRecord.ByReadingFile, EvaluationRecord.ConfidenceFile })
        {
            Assert.StartsWith(plain[name], files[name], StringComparison.Ordinal);
            Assert.StartsWith("generalization\t", files[name][plain[name].Length..], StringComparison.Ordinal);
        }

        Assert.Equal(plain[EvaluationRecord.GateFile], files[EvaluationRecord.GateFile]);
        Assert.Equal(plain[EvaluationRecord.SeedsFile], files[EvaluationRecord.SeedsFile]);

        // Assert: the per-row and the per-slice files
        Assert.Equal(
            "set\tid\tgroup\tkind\tseen_in_training\tcue_unsolvable\tgold\tshipped\tmodel_only\tcue_fired\n"
            + "generalization\tgen-0001\tlead:metal\tnovel-vocabulary\t-\tno\tlead.noun\tlead.noun\tlead.noun\tno\n"
            + "generalization\tgen-0002\tlead:metal\tnovel-vocabulary\t-\tno\tlead.noun\tlead.noun\tlead.verb\tyes\n",
            files[EvaluationRecord.OverlayRowsFile]);
        var ablation = files[EvaluationRecord.CuratedAblationFile].Split('\n');
        Assert.StartsWith("set\tslice\trows\tclusters\tshipped_correct\tshipped_bp", ablation[0], StringComparison.Ordinal);
        Assert.Equal("generalization\tall\t2\t1\t2\t10000\t10000\t10000\t1\t5000\t5000\t5000\t5000\t5000\t5000\t1\t1", ablation[1]);
        Assert.Equal(string.Empty, ablation[2]);

        // Assert: without overlays both files have their header only
        Assert.Single(plain[EvaluationRecord.OverlayRowsFile].Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Single(plain[EvaluationRecord.CuratedAblationFile].Split('\n', StringSplitOptions.RemoveEmptyEntries));
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

    /// <summary>
    ///     Returns a report whose holdout-natural set has the given current readings, labeled as in <see cref="Sample"/>
    ///     (a new id is labeled <c>read.past</c>).
    /// </summary>
    /// <param name="report">The report.</param>
    /// <param name="readings">The id and the emitted reading of every row.</param>
    /// <returns>The report with the readings.</returns>
    private static EvaluationReport WithReadings(EvaluationReport report, params (string Id, string Emitted)[] readings)
    {
        var gold = report.Sets[0].RowReadings!.ToDictionary(static row => row.Id, static row => row.Gold, StringComparer.Ordinal);
        var rows = readings.Select(reading => new RowReading(reading.Id, gold.GetValueOrDefault(reading.Id, "read.past"), reading.Emitted)).ToList();
        return report with { Sets = [report.Sets[0] with { RowReadings = rows }] };
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
