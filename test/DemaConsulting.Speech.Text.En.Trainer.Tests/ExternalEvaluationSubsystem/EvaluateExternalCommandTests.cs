using System.Text.RegularExpressions;
using DemaConsulting.Speech.Text.En.Trainer.ExternalEvaluationSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.ExternalEvaluationSubsystem;

/// <summary>
///     Unit tests for <see cref="EvaluateExternalCommand"/>: the command line, the cache guard, the fetch option, the
///     fail-closed behavior, the printed report, the seed study and the write and check of the external record, run in
///     memory on a trained fixture model and fake archives.
/// </summary>
public sealed class EvaluateExternalCommandTests
{
    /// <summary>The names of the seven record files in ordinal order.</summary>
    private static readonly string[] RecordFiles =
    [
        "by-source.tsv", "confidence.tsv", "conversion.tsv", "per-reading.tsv", "seeds.tsv", "sources.tsv", "summary.tsv",
    ];

    /// <summary>Proves help prints the usage and exits with zero.</summary>
    [Fact]
    public void EvaluateExternalCommand_Run_Help_PrintsUsageAndExitsZero()
    {
        // Arrange
        using var training = new TrainingFixture();

        // Act
        var (exitCode, output, _) = CommandHarness.EvaluateExternal(training, null, "--help");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Usage: evaluate-external", output, StringComparison.Ordinal);
        Assert.Contains("--fetch", output, StringComparison.Ordinal);
    }

    /// <summary>Proves a bad command line is a usage error that names the problem.</summary>
    [Theory]
    [InlineData("--bogus", "unknown option")]
    [InlineData("--set", "needs a value")]
    [InlineData("--pins=", "needs a value")]
    public void EvaluateExternalCommand_Run_BadCommandLine_ExitsTwoNamingTheProblem(string argument, string message)
    {
        // Arrange
        using var training = new TrainingFixture();

        // Act
        var (exitCode, output, error) = CommandHarness.EvaluateExternal(training, null, argument);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains(message, error, StringComparison.Ordinal);
        Assert.Equal(string.Empty, output);
    }

    /// <summary>Proves an unknown set name and the combination of write and check are usage errors.</summary>
    [Fact]
    public void EvaluateExternalCommand_Run_UnknownSetOrWriteWithCheck_ExitsTwo()
    {
        // Arrange
        using var training = new TrainingFixture();

        // Act
        var unknown = CommandHarness.EvaluateExternal(training, null, "--set", "whd-secret");
        var both = CommandHarness.EvaluateExternal(training, null, "--write", "--check");

        // Assert
        Assert.Equal(2, unknown.ExitCode);
        Assert.Contains("unknown set", unknown.Error, StringComparison.Ordinal);
        Assert.Equal(2, both.ExitCode);
        Assert.Contains("cannot be combined", both.Error, StringComparison.Ordinal);
    }

    /// <summary>Proves a missing model, missing pins and a missing data folder are input errors that name the problem.</summary>
    [Fact]
    public void EvaluateExternalCommand_Run_MissingPinsOrModel_ExitsTwo()
    {
        // Arrange: one run without a trained model, one with a model but pins that do not exist, one without any pins option, one with a data folder that does not exist
        using var rig = new ExternalEvaluationRig();
        using var untrained = new TrainingFixture();
        var missingPins = Path.Combine(rig.External.Folder, "missing.json");

        // Act
        var noModel = CommandHarness.EvaluateExternal(untrained, null, "--model-dir", untrained.ModelDirectory, "--pins", missingPins);
        var noPins = CommandHarness.EvaluateExternal(rig.Training, rig.External.Fetcher(), "--model-dir", rig.ModelDirectory, "--pins", missingPins, "--cache", rig.CacheDirectory);
        var defaultPins = CommandHarness.EvaluateExternal(rig.Training, rig.External.Fetcher(), "--model-dir", rig.ModelDirectory, "--cache", rig.CacheDirectory);
        var noData = CommandHarness.EvaluateExternal(rig.Training, null, "--data", Path.Combine(rig.External.Folder, "no-data"));

        // Assert
        Assert.Equal(2, noModel.ExitCode);
        Assert.Contains("heteronyms.bin", noModel.Error, StringComparison.Ordinal);
        Assert.Equal(2, noPins.ExitCode);
        Assert.Contains("pins file", noPins.Error, StringComparison.Ordinal);
        Assert.Equal(2, defaultPins.ExitCode);
        Assert.Contains("pins file", defaultPins.Error, StringComparison.Ordinal);
        Assert.Equal(2, noData.ExitCode);
        Assert.Contains("data folder", noData.Error, StringComparison.Ordinal);
        Assert.All(new[] { noModel, noPins, defaultPins, noData }, result => Assert.Equal(string.Empty, result.Output));
        Assert.Equal(0, rig.External.FetchCount);
    }

    /// <summary>Proves a cache folder inside the repository but outside the ignored external folder is refused before anything is fetched.</summary>
    [Fact]
    public void EvaluateExternalCommand_Run_CacheInRepositoryOutsideIgnoredFolder_ExitsTwo()
    {
        // Arrange
        using var rig = new ExternalEvaluationRig();
        var insideRepository = Path.Combine(rig.Training.BaseDirectory, "cache");
        var insideData = Path.Combine(rig.Training.Root, "other");

        // Act
        var first = rig.Run("--fetch", "--cache", insideRepository);
        var second = rig.Run("--fetch", "--cache", insideData);
        var third = rig.Run("--fetch", "--cache", rig.Training.Root);

        // Assert
        Assert.All(new[] { first, second, third }, result =>
        {
            Assert.Equal(2, result.ExitCode);
            Assert.Contains("cache folder must be", result.Error, StringComparison.Ordinal);
            Assert.Equal(string.Empty, result.Output);
        });
        Assert.Equal(0, rig.External.FetchCount);
        Assert.False(Directory.Exists(insideRepository));
        Assert.False(Directory.Exists(insideData));
    }

    /// <summary>Proves the default cache is the external folder of the data folder, a folder inside it and a folder outside the repository are accepted.</summary>
    [Fact]
    public void EvaluateExternalCommand_Run_DefaultAndOutsideCache_AreAccepted()
    {
        // Arrange
        using var rig = new ExternalEvaluationRig();
        var defaultCache = Path.Combine(rig.Training.Root, "external");

        // Act
        var byDefault = CommandHarness.EvaluateExternal(rig.Training, rig.External.Fetcher(), "--model-dir", rig.ModelDirectory, "--pins", rig.External.PinsPath, "--fetch", "--set", "bench");
        var inside = rig.Run("--fetch", "--set", "bench", "--cache", Path.Combine(defaultCache, "sub"));
        var outside = rig.Run("--fetch", "--set", "bench");

        // Assert
        Assert.Equal(0, byDefault.ExitCode);
        Assert.Contains($"cache {defaultCache}", byDefault.Output, StringComparison.Ordinal);
        Assert.True(Directory.Exists(Path.Combine(defaultCache, "bench", ExternalContent.BenchCommit)));
        Assert.Equal(0, inside.ExitCode);
        Assert.True(Directory.Exists(Path.Combine(defaultCache, "sub", "bench", ExternalContent.BenchCommit)));
        Assert.Equal(0, outside.ExitCode);
        Assert.True(Directory.Exists(Path.Combine(rig.CacheDirectory, "bench", ExternalContent.BenchCommit)));
    }

    /// <summary>Proves selecting sets fetches and evaluates only the sources those sets need.</summary>
    [Fact]
    public void EvaluateExternalCommand_Run_SetSelection_FetchesAndEvaluatesOnlyNeededSources()
    {
        // Arrange
        using var rig = new ExternalEvaluationRig();
        var otherCache = rig.External.NewFolder("other-cache");

        // Act
        var bench = rig.Run("--fetch", "--set", "bench");
        var benchFetches = rig.External.FetchedUris.ToList();
        var whd = rig.Run("--fetch", "--set", "whd-eval", "--cache", otherCache);

        // Assert
        Assert.Equal(0, bench.ExitCode);
        Assert.Equal([rig.External.Pins.Find("bench").Archive], benchFetches);
        Assert.Contains("set bench", bench.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("set whd", bench.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("source whd", bench.Output, StringComparison.Ordinal);
        Assert.All(ExternalSourceFixture.FilesBelow(rig.CacheDirectory), path => Assert.StartsWith("bench/", path, StringComparison.Ordinal));
        Assert.Equal(0, whd.ExitCode);
        Assert.Equal([rig.External.Pins.Find("bench").Archive, rig.External.Pins.Find("whd").Archive], rig.External.FetchedUris);
        Assert.Contains("set whd-eval", whd.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("set whd-train", whd.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("set bench", whd.Output, StringComparison.Ordinal);
    }

    /// <summary>Proves a pinned file that is not in the expected format stops the command with an error that names the file and no figures.</summary>
    [Fact]
    public void EvaluateExternalCommand_Run_MalformedData_ExitsTwoWithNoFigures()
    {
        // Arrange: the pins describe the malformed file exactly, so only the format is wrong
        var content = ExternalContent.Standard();
        content.WhdFiles["data/eval/lead.tsv"] = "not the expected header\n";
        using var rig = new ExternalEvaluationRig(content);

        // Act
        var (exitCode, output, error) = rig.Run("--fetch");

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("data/eval/lead.tsv", error, StringComparison.Ordinal);
        Assert.Equal(string.Empty, output);
    }

    /// <summary>Proves a pinned benchmark whose item has a non-numeric or wrong-length position stops the command with data-error exit code 2 and no figures.</summary>
    [Theory]
    [InlineData("""["0",4]""")]
    [InlineData("""[null,4]""")]
    [InlineData("""[true,4]""")]
    [InlineData("""[0]""")]
    public void EvaluateExternalCommand_Run_MalformedBenchPosition_ExitsTwoWithNoFigures(string position)
    {
        // Arrange: the pins describe the malformed file exactly, so only the item is wrong
        var content = ExternalContent.Standard();
        content.BenchDocument =
            """{"lang":"en","items":{"item-1":{"text":"lead","word":"lead","pos":POSITION,"answer":"a","src":"v1"}}}"""
                .Replace("POSITION", position, StringComparison.Ordinal);
        using var rig = new ExternalEvaluationRig(content);

        // Act
        var (exitCode, output, error) = rig.Run("--fetch");

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Contains("item 'item-1' has no usable 'pos'", error, StringComparison.Ordinal);
        Assert.Equal(string.Empty, output);
    }

    /// <summary>Proves the report prints, for each set, the row counts, the skips by reason, the accuracy with its interval, the three baselines, the improvement and parity, and never a sentence or an external label.</summary>
    [Fact]
    public void EvaluateExternalCommand_Run_Report_PrintsCountsBaselinesAndIntervalsWithoutDatasetText()
    {
        // Arrange
        using var rig = new ExternalEvaluationRig();

        // Act
        var (exitCode, output, error) = rig.Run("--fetch");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error);
        Assert.Contains("set whd-eval: 5 rows read, 5 used; skipped: bad-offsets 0, duplicate-of-whd 0, reading-not-candidate 0, unknown-form 0, unmapped-label 0", output, StringComparison.Ordinal);
        Assert.Contains("set whd-train: 3 rows read, 3 used; skipped: bad-offsets 0, duplicate-of-whd 0, reading-not-candidate 0, unknown-form 0, unmapped-label 0", output, StringComparison.Ordinal);
        Assert.Contains("set bench: 6 rows read, 4 used; skipped: bad-offsets 0, duplicate-of-whd 2, reading-not-candidate 0, unknown-form 0, unmapped-label 0", output, StringComparison.Ordinal);
        Assert.Matches(@"model accuracy\s+\d+\.\d\d% \(\d+/5\), interval \[\d+\.\d\d%, \d+\.\d\d%\] over \d+ clusters", output);
        Assert.Matches(@"own-UPOS baseline\s+\d+\.\d\d% \(\d+/5\)", output);
        Assert.Matches(@"majority baseline\s+\d+\.\d\d% \(\d+/5\)", output);
        Assert.Matches(@"always-default \(first-candidate\) \d+\.\d\d% \(\d+/5\)", output);
        Assert.Matches(@"improvement over own-UPOS [+-]?\d+\.\d\d%, interval \[[+-]?\d+\.\d\d%, [+-]?\d+\.\d\d%\]", output);
        Assert.Matches(@"parity: \d+ compared, 0 mismatched, 0 suppressed", output);
        Assert.Contains("evaluate-external:   confidence ", output, StringComparison.Ordinal);
        AssertNoDatasetText(rig.External.Content, output + error);
    }

    /// <summary>Proves the seed study prints the model of each evaluation seed with its figures and the mean, minimum and maximum of each set, consistent with the per-seed lines.</summary>
    [Fact]
    public void EvaluateExternalCommand_Run_Seeds_PrintsEachSeedAndSpread()
    {
        // Arrange
        using var rig = new ExternalEvaluationRig();

        // Act
        var (exitCode, output, _) = rig.Run("--fetch", "--seeds");

        // Assert: seeds 1 to 5, each with a model hash and a line per set that carries an interval
        Assert.Equal(0, exitCode);
        var hashes = Regex.Matches(output, @"seed (\d+): model image sha256 ([0-9a-f]{64})");
        Assert.Equal(["1", "2", "3", "4", "5"], hashes.Select(static match => match.Groups[1].Value));
        foreach (var set in new[] { "whd-eval", "whd-train", "bench" })
        {
            var perSeed = Regex.Matches(output, $@"seed (\d+): set {set}: accuracy (\d+)\.(\d\d)%, improvement over own-UPOS [+-]?\d+\.\d\d%, interval \[[+-]?\d+\.\d\d%, [+-]?\d+\.\d\d%\]");
            Assert.Equal(["1", "2", "3", "4", "5"], perSeed.Select(static match => match.Groups[1].Value));
            var basisPoints = perSeed.Select(static match => (int.Parse(match.Groups[2].Value) * 100) + int.Parse(match.Groups[3].Value)).ToList();
            var mean = (int)(((2L * basisPoints.Sum()) + basisPoints.Count) / (2 * basisPoints.Count));
            Assert.Contains($"seeds {set}: mean {Percent(mean)}, min {Percent(basisPoints.Min())}, max {Percent(basisPoints.Max())}", output, StringComparison.Ordinal);
        }
    }

    /// <summary>Proves the models the seed study trains are the same whatever the external content is, so external data cannot influence training.</summary>
    [Fact]
    public void EvaluateExternalCommand_Run_Seeds_AreIndependentOfExternalContent()
    {
        // Arrange: a second external source with other sentences and the opposite labels
        var other = ExternalContent.Standard();
        other.WhdMapping.Clear();
        other.WhdMapping.AddRange([["lead_guide", "lead.noun"], ["lead_metal", "lead.verb"], ["wind_air", "wind.noun"]]);
        other.AddWhdRow("eval", "extra", "lead", "lead_guide", "Nobody wanted to lead the others.");
        other.AddWhdRow("train", "extra", "lead", "lead_metal", "A lead sheet covered the window.");
        using var first = new ExternalEvaluationRig();
        using var second = new ExternalEvaluationRig(other);

        // Act
        var firstRun = first.Run("--fetch", "--seeds");
        var secondRun = second.Run("--fetch", "--seeds");

        // Assert: every seed trains the same model, equal to the model under test for the first seed, while the figures on the external rows differ
        Assert.Equal(0, firstRun.ExitCode);
        Assert.Equal(0, secondRun.ExitCode);
        var firstHashes = SeedHashes(firstRun.Output);
        Assert.Equal(5, firstHashes.Count);
        Assert.Equal(firstHashes, SeedHashes(secondRun.Output));
        Assert.Equal(ModelHash(firstRun.Output), firstHashes[0]);
        Assert.NotEqual(AccuracyLines(firstRun.Output), AccuracyLines(secondRun.Output));
    }

    /// <summary>Proves writing the record twice gives byte-identical files, and the files are the seven expected ones.</summary>
    [Fact]
    public void EvaluateExternalCommand_Run_WriteTwice_ProducesIdenticalFiles()
    {
        // Arrange
        using var rig = new ExternalEvaluationRig();
        var first = rig.External.NewFolder("record-1");
        var second = rig.External.NewFolder("record-2");

        // Act
        var one = rig.Run("--fetch", "--write", "--record-dir", first);
        var two = rig.Run("--write", "--record-dir", second);

        // Assert
        Assert.Equal(0, one.ExitCode);
        Assert.Equal(0, two.ExitCode);
        Assert.Equal(RecordFiles, ExternalSourceFixture.FilesBelow(first));
        Assert.Equal(RecordFiles, ExternalSourceFixture.FilesBelow(second));
        Assert.All(RecordFiles, name => Assert.Equal(File.ReadAllBytes(Path.Combine(first, name)), File.ReadAllBytes(Path.Combine(second, name))));
    }

    /// <summary>Proves checking a current record exits zero, and checking an edited record, a record with a missing file and a missing record exits one and names the files.</summary>
    [Fact]
    public void EvaluateExternalCommand_Run_CheckStaleOrMissingRecord_ExitsOneNamingFile()
    {
        // Arrange
        using var rig = new ExternalEvaluationRig();
        rig.Run("--fetch", "--write", "--record-dir", rig.RecordDirectory);
        var current = rig.Run("--check", "--record-dir", rig.RecordDirectory);
        File.AppendAllText(Path.Combine(rig.RecordDirectory, "summary.tsv"), "tampered\n");
        File.Delete(Path.Combine(rig.RecordDirectory, "seeds.tsv"));

        // Act
        var stale = rig.Run("--check", "--record-dir", rig.RecordDirectory);
        var missing = rig.Run("--check", "--record-dir", rig.External.NewFolder("no-record"));

        // Assert
        Assert.Equal(0, current.ExitCode);
        Assert.Contains("--check: OK", current.Output, StringComparison.Ordinal);
        Assert.Equal(1, stale.ExitCode);
        Assert.Contains("STALE: summary.tsv", stale.Output, StringComparison.Ordinal);
        Assert.Contains("STALE: seeds.tsv", stale.Output, StringComparison.Ordinal);
        Assert.Contains("FAILED (2 files)", stale.Output, StringComparison.Ordinal);
        Assert.Equal(1, missing.ExitCode);
        Assert.All(RecordFiles, name => Assert.Contains($"STALE: {name}", missing.Output, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Asserts that a text holds no sentence and no external label of the fake content.
    /// </summary>
    /// <param name="content">The fake content.</param>
    /// <param name="text">The text.</param>
    private static void AssertNoDatasetText(ExternalContent content, string text)
    {
        Assert.NotEmpty(content.Sentences);
        Assert.All(content.Sentences, sentence => Assert.DoesNotContain(sentence, text, StringComparison.Ordinal));
        Assert.All(content.Labels, label => Assert.DoesNotContain(label, text, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Formats basis points as a percentage with two decimals.
    /// </summary>
    /// <param name="basisPoints">The value in basis points.</param>
    /// <returns>The text, for example <c>96.17%</c>.</returns>
    private static string Percent(int basisPoints) => $"{basisPoints / 100}.{basisPoints % 100:00}%";

    /// <summary>
    ///     Gets the model hash of each seed line of the output in seed order.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <returns>The hashes.</returns>
    private static List<string> SeedHashes(string output) =>
        [.. Regex.Matches(output, @"seed \d+: model image sha256 ([0-9a-f]{64})").Select(static match => match.Groups[1].Value)];

    /// <summary>
    ///     Gets the hash of the model under test from the first line of the output.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <returns>The hash.</returns>
    private static string ModelHash(string output) =>
        Regex.Match(output, @"\(image sha256 ([0-9a-f]{64})\)").Groups[1].Value;

    /// <summary>
    ///     Gets the accuracy lines of the output.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <returns>The lines.</returns>
    private static List<string> AccuracyLines(string output) =>
        [.. Regex.Matches(output, @"model accuracy[^\n]*").Select(static match => match.Value)];
}
