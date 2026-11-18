using System.Globalization;
using DemaConsulting.Speech.Text.En.Trainer.ExternalEvaluationSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.ExternalEvaluationSubsystem;

/// <summary>
///     Unit tests for <see cref="ExternalEvaluationRecord"/>: the deterministic rendering of the external evaluation
///     record from hand-built evaluation figures, and writing and comparing it.
/// </summary>
public sealed class ExternalEvaluationRecordTests
{
    /// <summary>The names of the three sets in report order.</summary>
    private static readonly string[] SetNames = ["whd-eval", "whd-train", "bench"];

    /// <summary>The names of the seven record files in ordinal order.</summary>
    private static readonly string[] FileNames =
    [
        "by-source.tsv", "confidence.tsv", "conversion.tsv", "per-reading.tsv", "seeds.tsv", "sources.tsv", "summary.tsv",
    ];

    /// <summary>Proves rendering twice gives the same files, with the fixed set of files, LF line endings, a final newline and no byte order mark.</summary>
    [Fact]
    public void ExternalEvaluationRecord_Render_IsDeterministicWithFixedFilesAndLineFeeds()
    {
        // Arrange
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training);

        // Act
        var first = ExternalEvaluationRecord.Render(Report(external.Pins));
        var second = ExternalEvaluationRecord.Render(Report(external.Pins));
        var directory = external.NewFolder("record");
        ExternalEvaluationRecord.Write(Report(external.Pins), directory);

        // Assert
        Assert.Equal(FileNames, first.Keys);
        Assert.Equal(first.Values, second.Values);
        Assert.All(first, pair =>
        {
            Assert.DoesNotContain("\r", pair.Value, StringComparison.Ordinal);
            Assert.EndsWith("\n", pair.Value, StringComparison.Ordinal);
            Assert.False(pair.Value.StartsWith('\uFEFF'));
            var bytes = File.ReadAllBytes(Path.Combine(directory, pair.Key));
            Assert.Equal(System.Text.Encoding.UTF8.GetBytes(pair.Value), bytes);
            Assert.NotEqual(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
        });
    }

    /// <summary>Proves the sources file lists the model, the settings and the pins of each source, and the conversion and summary files list the counts and figures of each set.</summary>
    [Fact]
    public void ExternalEvaluationRecord_Render_SourcesAndConversion_ListPinsAndCounts()
    {
        // Arrange
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training);
        var report = Report(external.Pins);

        // Act
        var files = ExternalEvaluationRecord.Render(report);

        // Assert: the sources file, with the sources in id order
        var sources = Lines(files["sources.tsv"]);
        Assert.Contains($"image_sha256\t{report.ImageSha256}", sources);
        Assert.Contains("resamples\t10000", sources);
        Assert.Contains("confidence_percent\t95", sources);
        Assert.Contains("bootstrap_seed\t7", sources);
        Assert.Contains("seeds\t1,2,3", sources);
        foreach (var pin in external.Pins.Sources)
        {
            Assert.Contains($"{pin.Id}.repository\t{pin.Repository}", sources);
            Assert.Contains($"{pin.Id}.commit\t{pin.Commit}", sources);
            Assert.Contains($"{pin.Id}.files\t{pin.Files.Count}", sources);
            Assert.Contains($"{pin.Id}.bytes\t{pin.TotalBytes.ToString(CultureInfo.InvariantCulture)}", sources);
            Assert.Contains($"{pin.Id}.files_sha256\t{pin.FilesSha256}", sources);
            Assert.Contains($"{pin.Id}.license\t{pin.License}", sources);
        }

        Assert.True(sources.IndexOf($"bench.commit\t{ExternalContent.BenchCommit}") < sources.IndexOf($"whd.commit\t{ExternalContent.WhdCommit}"));

        // Assert: one conversion row and one summary row per set
        var conversion = Lines(files["conversion.tsv"]);
        var summary = Lines(files["summary.tsv"]);
        Assert.Equal(4, conversion.Count);
        Assert.Equal(4, summary.Count);
        foreach (var name in SetNames)
        {
            Assert.Contains($"{name}\t210\t200\t3\t0\t2\t4\t1\t12\t9\t0\t7\t198\t3\t199", conversion);
            Assert.Contains($"{name}\t200\t190\t9500\t9450\t9540\t180\t9000\t182\t9100\t170\t8500\t500\t300\t700\tyes\t40\t198\t0\t0", summary);
        }
    }

    /// <summary>Proves the rendered files hold no sentence, no external label and no word of the fake sources: only counts, our ids, set names and pins.</summary>
    [Fact]
    public void ExternalEvaluationRecord_Render_HoldsNoDatasetText()
    {
        // Arrange
        var content = ExternalContent.Standard();
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training, content);

        // Act
        var files = ExternalEvaluationRecord.Render(Report(external.Pins));

        // Assert
        var all = string.Concat(files.Values);
        Assert.NotEmpty(content.Sentences);
        Assert.NotEmpty(content.Labels);
        Assert.All(content.Sentences, sentence => Assert.DoesNotContain(sentence, all, StringComparison.Ordinal));
        Assert.All(content.Labels, label => Assert.DoesNotContain(label, all, StringComparison.Ordinal));
        Assert.All(files.Values.SelectMany(Lines), line => Assert.Matches("^[^\t]+(\t[^\t]*)+$", line));
    }

    /// <summary>Proves the seeds file has one row per set and seed and then the mean, minimum and maximum of each set in integer basis points rounded half up.</summary>
    [Fact]
    public void ExternalEvaluationRecord_Render_Seeds_HasPerSeedRowsAndSpread()
    {
        // Arrange
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training);

        // Act
        var seeds = Lines(ExternalEvaluationRecord.Render(Report(external.Pins))["seeds.tsv"]);

        // Assert: three seeds and three summary rows for each of the three sets, after the header
        Assert.Equal(1 + (3 * 6), seeds.Count);
        Assert.Equal(
            [
                "bench\t1\t200\t190\t9500\t100\t50\t150",
                "bench\t2\t200\t192\t9600\t125\t60\t160",
                "bench\t3\t200\t195\t9750\t150\t80\t170",
                "bench\tmean\t\t\t9617\t125\t63\t160",
                "bench\tmin\t\t\t9500\t100\t50\t150",
                "bench\tmax\t\t\t9750\t150\t80\t170",
            ],
            seeds.Where(static line => line.StartsWith("bench\t", StringComparison.Ordinal)));
        Assert.Equal(["whd-eval", "whd-train", "bench"], seeds.Skip(1).Select(static line => line.Split('\t')[0]).Distinct());
    }

    /// <summary>Proves a written record compares as current, and an edited file and a missing file are named as stale in file name order.</summary>
    [Fact]
    public void ExternalEvaluationRecord_WriteThenCompare_DetectsEditedAndMissingFiles()
    {
        // Arrange
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training);
        var directory = external.NewFolder("record");
        var report = Report(external.Pins);
        ExternalEvaluationRecord.Write(report, directory);

        // Act
        var current = ExternalEvaluationRecord.Compare(report, directory);
        File.AppendAllText(Path.Combine(directory, "summary.tsv"), "tampered\n");
        File.Delete(Path.Combine(directory, "seeds.tsv"));
        var stale = ExternalEvaluationRecord.Compare(report, directory);
        var absent = ExternalEvaluationRecord.Compare(report, external.NewFolder("nothing"));

        // Assert
        Assert.Empty(current);
        Assert.Equal(["seeds.tsv", "summary.tsv"], stale);
        Assert.Equal(FileNames, absent);
    }

    /// <summary>
    ///     Builds a report of three sets with fixed figures and a three-seed study.
    /// </summary>
    /// <param name="pins">The pins.</param>
    /// <returns>The report.</returns>
    private static ExternalEvaluationReport Report(ExternalSourcePins pins)
    {
        var skipped = new Dictionary<string, int>
        {
            [SkipReasons.BadOffsets] = 3,
            [SkipReasons.DuplicateOfWhd] = 0,
            [SkipReasons.ReadingNotCandidate] = 2,
            [SkipReasons.UnknownForm] = 4,
            [SkipReasons.UnmappedLabel] = 1,
        };
        var sets = SetNames
            .Select(name => new ExternalSetResult(Evaluation(name), new ConversionCounts(210, 200, skipped, 12, 9, 0, 7)))
            .ToList();
        var seeds = new List<ExternalSeedResult>
        {
            new(1, SetNames.Select(name => Direct(name, 190, 100, 50, 150)).ToList()),
            new(2, SetNames.Select(name => Direct(name, 192, 125, 60, 160)).ToList()),
            new(3, SetNames.Select(name => Direct(name, 195, 150, 80, 170)).ToList()),
        };
        return new ExternalEvaluationReport(
            new string('a', 64),
            EvaluationSettings.Default with { Seeds = [1UL, 2UL, 3UL] },
            pins,
            sets,
            seeds);
    }

    /// <summary>
    ///     Builds the end-to-end evaluation of a set with fixed figures: 190 of 200 correct.
    /// </summary>
    /// <param name="name">The set name.</param>
    /// <returns>The evaluation.</returns>
    private static SetEvaluation Evaluation(string name) =>
        new(
            name,
            200,
            198,
            3,
            199,
            190,
            180,
            182,
            170,
            [new GroupTally("source-a", 120, 115, 108), new GroupTally("source-b", 80, 75, 72)],
            [new GroupTally("lead.noun", 10, 8, 7), new GroupTally("lead.verb", 190, 182, 173)],
            [new GroupTally("<0.600", 5, 2, 0), new GroupTally("0.600-0.899", 15, 12, 0), new GroupTally(">=0.900", 179, 175, 0), new GroupTally("none", 1, 1, 0)],
            new BootstrapInterval(40, 500, 300, 700, true),
            new ParityCounts(198, 0, 0),
            new BootstrapInterval(40, 9500, 9450, 9540, true));

    /// <summary>
    ///     Builds the direct-path evaluation of a set for one seed.
    /// </summary>
    /// <param name="name">The set name.</param>
    /// <param name="correct">The rows decided correctly of 200.</param>
    /// <param name="difference">The improvement over the baseline in basis points.</param>
    /// <param name="lower">The lower bound in basis points.</param>
    /// <param name="upper">The upper bound in basis points.</param>
    /// <returns>The evaluation.</returns>
    private static SetEvaluation Direct(string name, int correct, int difference, int lower, int upper) =>
        new(
            name,
            200,
            198,
            3,
            199,
            correct,
            180,
            182,
            170,
            [],
            [],
            [],
            new BootstrapInterval(40, difference, lower, upper, lower > 0),
            new ParityCounts(0, 0, 0));

    /// <summary>
    ///     Gets all the lines of a file text, the header first.
    /// </summary>
    /// <param name="text">The text, which ends with a line feed.</param>
    /// <returns>The lines.</returns>
    private static List<string> Lines(string text) => [.. text.TrimEnd('\n').Split('\n')];
}
