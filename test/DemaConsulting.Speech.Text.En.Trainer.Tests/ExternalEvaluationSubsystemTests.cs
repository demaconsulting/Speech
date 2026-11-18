using System.Text.RegularExpressions;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.ExternalEvaluationSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests;

/// <summary>
///     Subsystem tests for the ExternalEvaluationSubsystem: the units working together through the
///     <c>evaluate-external</c> command on fake archives and fixture data, observed only through exit codes, console
///     output, written record files and the committed mapping and record.
/// </summary>
public sealed class ExternalEvaluationSubsystemTests
{
    /// <summary>The grammar of a set name.</summary>
    private const string SetCell = "(?:whd-eval|whd-train|bench)";

    /// <summary>The grammar of a non-negative integer.</summary>
    private const string NaturalCell = @"\d+";

    /// <summary>The grammar of an integer.</summary>
    private const string IntegerCell = @"-?\d+";

    /// <summary>The grammar of one of our reading ids.</summary>
    private const string ReadingCell = @"[a-z0-9-]+\.[a-z0-9.-]+";

    /// <summary>The grammar of a stratum name.</summary>
    private const string StratumCell = @"[a-z0-9._-]+";

    /// <summary>The grammar of a confidence bucket name.</summary>
    private const string BucketCell = @"(?:<0\.600|0\.600-0\.899|>=0\.900|none)";

    /// <summary>The names of the seven record files in ordinal order.</summary>
    private static readonly string[] RecordFiles =
    [
        "by-source.tsv", "confidence.tsv", "conversion.tsv", "per-reading.tsv", "seeds.tsv", "sources.tsv", "summary.tsv",
    ];

    /// <summary>The set names of the external evaluation.</summary>
    private static readonly string[] SetNames = ["whd-eval", "whd-train", "bench"];

    /// <summary>Proves the whole path works: the pinned archives are fetched and verified, converted, evaluated and written as a record that lists the pins and the row counts.</summary>
    [Fact]
    public void ExternalEvaluationSubsystem_Evaluate_FetchConvertEvaluateAndRecord_EndToEnd()
    {
        // Arrange
        using var rig = new ExternalEvaluationRig();

        // Act
        var (exitCode, output, error) = rig.Run("--fetch", "--write", "--record-dir", rig.RecordDirectory);

        // Assert: both archives were fetched, the cache holds exactly the pinned files and the record has the seven files
        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error);
        Assert.Equal(2, rig.External.FetchCount);
        var pinned = rig.External.Pins.Sources
            .SelectMany(source => source.Files.Select(file => $"{source.Id}/{source.Commit}/{file.Path}"))
            .Order(StringComparer.Ordinal);
        Assert.Equal(pinned, ExternalSourceFixture.FilesBelow(rig.CacheDirectory));
        Assert.Equal(RecordFiles, ExternalSourceFixture.FilesBelow(rig.RecordDirectory));

        // Assert: the record lists the pins and the counts of each set
        var sources = File.ReadAllLines(Path.Combine(rig.RecordDirectory, "sources.tsv"));
        foreach (var pin in rig.External.Pins.Sources)
        {
            Assert.Contains($"{pin.Id}.commit\t{pin.Commit}", sources);
            Assert.Contains($"{pin.Id}.files_sha256\t{pin.FilesSha256}", sources);
        }

        var conversion = File.ReadAllLines(Path.Combine(rig.RecordDirectory, "conversion.tsv"));
        Assert.Contains(conversion, static line => line.StartsWith("whd-eval\t5\t5\t0\t0\t0\t0\t0\t0\t0\t0\t0\t", StringComparison.Ordinal));
        Assert.Contains(conversion, static line => line.StartsWith("whd-train\t3\t3\t0\t0\t0\t0\t0\t0\t0\t0\t0\t", StringComparison.Ordinal));
        Assert.Contains(conversion, static line => line.StartsWith("bench\t6\t4\t0\t2\t0\t0\t0\t0\t0\t0\t0\t", StringComparison.Ordinal));
        var summary = File.ReadAllLines(Path.Combine(rig.RecordDirectory, "summary.tsv"));
        Assert.Equal(SetNames, summary.Skip(1).Select(static line => line.Split('\t')[0]));
        Assert.Contains("wrote the external evaluation record", output, StringComparison.Ordinal);
    }

    /// <summary>Proves a cache that does not match the pins is refused: no figure is printed and no record is written.</summary>
    [Fact]
    public void ExternalEvaluationSubsystem_Evaluate_TamperedCache_IsRefusedWithNoFigures()
    {
        // Arrange
        using var rig = new ExternalEvaluationRig();
        rig.External.WriteCache(rig.CacheDirectory);
        rig.External.Tamper(rig.CacheDirectory, "bench", "data/readset_en_v2.json");

        // Act
        var (exitCode, output, error) = rig.Run("--write", "--record-dir", rig.RecordDirectory);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, output);
        Assert.Contains("bench/data/readset_en_v2.json", error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(rig.RecordDirectory));
        Assert.Equal(0, rig.External.FetchCount);
    }

    /// <summary>Proves rows that cannot be used are reported by reason and are not fatal, and the record counts them.</summary>
    [Fact]
    public void ExternalEvaluationSubsystem_Evaluate_SkippedRows_AreReportedNotFatal()
    {
        // Arrange: an unmapped label and a target that is not a form of the inventory
        var content = ExternalContent.Standard();
        content.AddWhdRow("eval", "extra", "lead", "lead_unlisted", "Please lead the hikers home now.");
        content.AddWhdRow("eval", "extra", "gust", "wind_air", "A gust of air came in.");
        using var rig = new ExternalEvaluationRig(content);

        // Act
        var evaluated = rig.Run("--fetch", "--set", "whd-eval");
        var written = rig.Run("--fetch", "--write", "--record-dir", rig.RecordDirectory);

        // Assert
        Assert.Equal(0, evaluated.ExitCode);
        Assert.Contains("set whd-eval: 7 rows read, 5 used; skipped: bad-offsets 0, duplicate-of-whd 0, reading-not-candidate 0, unknown-form 1, unmapped-label 1", evaluated.Output, StringComparison.Ordinal);
        Assert.Equal(0, written.ExitCode);
        var conversion = File.ReadAllLines(Path.Combine(rig.RecordDirectory, "conversion.tsv"));
        Assert.Contains(conversion, static line => line.StartsWith("whd-eval\t7\t5\t0\t0\t0\t1\t1\t", StringComparison.Ordinal));
    }

    /// <summary>Proves the committed benchmark mapping has one row per reading key of each word, every mapped id is a reading of the inventory that is a candidate of the word, and the two keys of a word map to different readings.</summary>
    [Fact]
    public void ExternalEvaluationSubsystem_CommittedBenchMapping_ResolvesToInventoryReadings()
    {
        // Arrange
        var root = ExternalSourceFixture.FindRepositoryRoot();
        Assert.SkipUnless(root is not null, "The repository root (Speech.slnx) was not found above the test binaries.");
        var data = Path.Combine(root, "data", "text-en");
        ReadingInventory inventory;
        using (var readings = new StreamReader(Path.Combine(data, "readings.tsv")))
        using (var forms = new StreamReader(Path.Combine(data, "forms.tsv")))
        {
            inventory = ReadingInventory.Parse(readings, forms, ReadingInventory.CurrentVersion);
        }

        // Act
        var map = ReadingIdMap.Load(Path.Combine(data, "bench-mapping.tsv"), ReadingIdMap.BenchHeader, inventory);
        var rows = File.ReadAllLines(Path.Combine(data, "bench-mapping.tsv")).Skip(1).Select(static line => line.Split('\t')).ToList();

        // Assert
        Assert.Equal(60, map.Count);
        Assert.Equal(60, rows.Count);
        Assert.Equal(rows.Select(static row => row[0]).Order(StringComparer.Ordinal), rows.Select(static row => row[0]));
        Assert.All(rows, row =>
        {
            Assert.Matches("^[a-z0-9]+:[a-z0-9]+$", row[0]);
            Assert.Matches($"^{ReadingCell}$", row[1]);
            Assert.True(map.TryGet(row[0], out var id));
            Assert.Equal(row[1], id);
            Assert.Contains(row[1], inventory.CandidatesFor(row[0].Split(':')[0]));
        });
        var byWord = rows.GroupBy(static row => row[0].Split(':')[0]).ToList();
        Assert.Equal(30, byWord.Count);
        Assert.All(byWord, word =>
        {
            Assert.Equal(2, word.Count());
            Assert.Equal(2, word.Select(static row => row[1]).Distinct().Count());
        });
    }

    /// <summary>Proves a non-ASCII sentence is evaluated at its target: the byte offsets are converted so no row is lost and every target is found in the text.</summary>
    [Fact]
    public void ExternalEvaluationSubsystem_Evaluate_NonAsciiSentence_IsEvaluatedAtTheTarget()
    {
        // Arrange: the target follows an accented letter in both rows
        var content = ExternalContent.Standard();
        content.AddWhdRow("eval", "unicode", "lead", "lead_guide", "Zo\u00eb will lead the hikers home.");
        content.AddWhdRow("eval", "unicode", "lead", "lead_metal", "Caf\u00e9 owners hung a lead sign.");
        using var rig = new ExternalEvaluationRig(content);

        // Act
        var (exitCode, output, _) = rig.Run("--fetch", "--set", "whd-eval");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("set whd-eval: 7 rows read, 7 used; skipped: bad-offsets 0", output, StringComparison.Ordinal);
        Assert.Contains("2 non-ASCII rows, 2 offsets converted", output, StringComparison.Ordinal);
        Assert.Contains("7 aligned", output, StringComparison.Ordinal);
    }

    /// <summary>Proves the seed study reports every evaluation seed with its interval, and the record's seed table has the per-seed rows and a consistent mean, minimum and maximum.</summary>
    [Fact]
    public void ExternalEvaluationSubsystem_Seeds_ReportSpreadAndIntervals()
    {
        // Arrange
        using var rig = new ExternalEvaluationRig();

        // Act
        var (exitCode, output, _) = rig.Run("--fetch", "--write", "--record-dir", rig.RecordDirectory);

        // Assert: the console has a line with an interval for each seed and set
        Assert.Equal(0, exitCode);
        Assert.Equal(15, Regex.Matches(output, @"seed \d+: set (?:whd-eval|whd-train|bench): accuracy \d+\.\d\d%, improvement over own-UPOS [+-]?\d+\.\d\d%, interval \[").Count);

        // Assert: the record has the five seeds and the spread of each set
        var rows = File.ReadAllLines(Path.Combine(rig.RecordDirectory, "seeds.tsv")).Skip(1).Select(static line => line.Split('\t')).ToList();
        foreach (var set in SetNames)
        {
            var own = rows.Where(row => row[0] == set).ToList();
            Assert.Equal(["1", "2", "3", "4", "5", "mean", "min", "max"], own.Select(static row => row[1]));
            var seeds = own.Take(5).ToList();
            Assert.All(seeds, row =>
            {
                var correct = int.Parse(row[3], System.Globalization.CultureInfo.InvariantCulture);
                var total = int.Parse(row[2], System.Globalization.CultureInfo.InvariantCulture);
                Assert.Equal(((2 * correct * 10000) + total) / (2 * total), int.Parse(row[4], System.Globalization.CultureInfo.InvariantCulture));
                Assert.True(int.Parse(row[6], System.Globalization.CultureInfo.InvariantCulture) <= int.Parse(row[7], System.Globalization.CultureInfo.InvariantCulture));
            });
            var accuracy = seeds.Select(static row => int.Parse(row[4], System.Globalization.CultureInfo.InvariantCulture)).ToList();
            Assert.Equal(accuracy.Min(), int.Parse(own[6][4], System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(accuracy.Max(), int.Parse(own[7][4], System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(((2L * accuracy.Sum()) + 5) / 10, int.Parse(own[5][4], System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    /// <summary>Proves neither the console nor any record file holds a sentence or an external label.</summary>
    [Fact]
    public void ExternalEvaluationSubsystem_Output_HoldsNoDatasetText()
    {
        // Arrange
        using var rig = new ExternalEvaluationRig();

        // Act
        var (exitCode, output, error) = rig.Run("--fetch", "--write", "--record-dir", rig.RecordDirectory);

        // Assert
        Assert.Equal(0, exitCode);
        var everything = output + error + string.Concat(RecordFiles.Select(name => File.ReadAllText(Path.Combine(rig.RecordDirectory, name))));
        var content = rig.External.Content;
        Assert.NotEmpty(content.Sentences);
        Assert.NotEmpty(content.Labels);
        Assert.All(content.Sentences, sentence => Assert.DoesNotContain(sentence, everything, StringComparison.Ordinal));
        Assert.All(content.Labels, label => Assert.DoesNotContain(label, everything, StringComparison.Ordinal));
    }

    /// <summary>Proves every failure exits two and prints no figure, a stale record exits one and a successful run exits zero.</summary>
    [Fact]
    public void ExternalEvaluationSubsystem_Failures_ExitTwoWithNoFigures()
    {
        // Arrange
        using var rig = new ExternalEvaluationRig();
        var originalMapping = rig.Training.Read("whd-mapping.tsv");
        var tamperedCache = rig.External.WriteCache(rig.External.NewFolder("tampered-cache"));
        rig.External.Tamper(tamperedCache, "whd", "data/train/lead.tsv");
        var goodCache = rig.External.WriteCache(rig.External.NewFolder("good-cache"));

        // Act
        var noCache = rig.Run();
        var tampered = rig.Run("--cache", tamperedCache);
        var unknownSet = rig.Run("--set", "nothing");
        rig.Training.Write("whd-mapping.tsv", "whd_wordid\treading_id\nlead_guide\tno.such-reading\n");
        var badMapping = rig.Run("--cache", goodCache, "--set", "whd-eval");
        rig.Training.Write("whd-mapping.tsv", originalMapping);
        var success = rig.Run("--cache", goodCache, "--set", "whd-eval");
        var stale = rig.Run("--cache", goodCache, "--check", "--record-dir", rig.RecordDirectory);

        // Assert
        Assert.All(new[] { noCache, tampered, unknownSet, badMapping }, result =>
        {
            Assert.Equal(2, result.ExitCode);
            Assert.Equal(string.Empty, result.Output);
            Assert.NotEqual(string.Empty, result.Error);
        });
        Assert.Contains("whd/data/train/lead.tsv", tampered.Error, StringComparison.Ordinal);
        Assert.Contains("whd-mapping.tsv", badMapping.Error, StringComparison.Ordinal);
        Assert.Equal(0, success.ExitCode);
        Assert.Equal(1, stale.ExitCode);
    }

    /// <summary>Proves the committed external record has the seven expected tables and that every row of every table matches a strict grammar of numbers, our own reading ids, set and stratum names, hashes and the documented source rows, so no dataset text can be in it.</summary>
    [Fact]
    public void ExternalEvaluationSubsystem_CommittedRecord_HasExpectedFilesAndNumericGrammar()
    {
        // Arrange
        var root = ExternalSourceFixture.FindRepositoryRoot();
        Assert.SkipUnless(root is not null, "The repository root (Speech.slnx) was not found above the test binaries.");
        var folder = Path.Combine(root, "docs", "evaluation", "text-en", "external");
        Assert.True(Directory.Exists(folder), $"The committed external evaluation record folder '{folder}' does not exist.");

        // Act
        var files = Directory.GetFiles(folder, "*.tsv").Select(static path => Path.GetFileName(path)).Order(StringComparer.Ordinal).ToList();

        // Assert: the seven tables, each LF only with no byte order mark and a final newline
        Assert.Equal(RecordFiles, files);
        var tables = RecordFiles.ToDictionary(static name => name, name => ReadTable(Path.Combine(folder, name)));

        // Assert: headers and the grammar of each row
        AssertGrammar(tables["conversion.tsv"], "set\trows_read\trows_used\tskipped_bad_offsets\tskipped_duplicate_of_whd\tskipped_reading_not_candidate\tskipped_unknown_form\tskipped_unmapped_label\tnon_ascii_rows\toffsets_converted\toffsets_repaired\texact_overlap_with_training\taligned\tsingle_candidate\tcovered", [SetCell, .. Enumerable.Repeat(NaturalCell, 14)]);
        AssertGrammar(tables["summary.tsv"], "set\trows\tcorrect\taccuracy_bp\taccuracy_ci_lower_bp\taccuracy_ci_upper_bp\tupos_baseline_correct\tupos_baseline_bp\tmajority_baseline_correct\tmajority_baseline_bp\tfirst_baseline_correct\tfirst_baseline_bp\tdifference_bp\tci_lower_bp\tci_upper_bp\tci_lower_positive\tclusters\tparity_compared\tparity_mismatched\tparity_suppressed", [SetCell, .. Enumerable.Repeat(NaturalCell, 11), IntegerCell, IntegerCell, IntegerCell, "(?:yes|no)", .. Enumerable.Repeat(NaturalCell, 4)]);
        AssertGrammar(tables["by-source.tsv"], "set\tsource\trows\tcorrect\taccuracy_bp\tupos_baseline_correct", [SetCell, StratumCell, .. Enumerable.Repeat(NaturalCell, 4)]);
        AssertGrammar(tables["per-reading.tsv"], "set\treading\trows\tcorrect\taccuracy_bp\tupos_baseline_correct", [SetCell, ReadingCell, .. Enumerable.Repeat(NaturalCell, 4)]);
        AssertGrammar(tables["confidence.tsv"], "set\tconfidence\trows\tcorrect\taccuracy_bp\tupos_baseline_correct", [SetCell, BucketCell, .. Enumerable.Repeat(NaturalCell, 4)]);
        AssertGrammar(tables["seeds.tsv"], "set\tseed\trows\tcorrect\taccuracy_bp\tdifference_bp\tci_lower_bp\tci_upper_bp", [SetCell, @"(?:\d+|mean|min|max)", @"\d*", @"\d*", NaturalCell, IntegerCell, IntegerCell, IntegerCell]);

        // Assert: the sources table has each documented key once with a value of the documented shape, and its pins are the committed pins
        Assert.Equal("key\tvalue", tables["sources.tsv"].Header);
        var pins = ExternalSourcePins.Load(Path.Combine(root, "tools", "DemaConsulting.Speech.Text.En.Trainer", "external-sources.json"));
        var sources = tables["sources.tsv"].Rows.ToDictionary(static row => row[0], static row => row[1]);
        Assert.Equal(tables["sources.tsv"].Rows.Count, sources.Count);
        var expectedKeys = new Dictionary<string, string>
        {
            ["image_sha256"] = "[0-9a-f]{64}",
            ["resamples"] = NaturalCell,
            ["confidence_percent"] = NaturalCell,
            ["bootstrap_seed"] = NaturalCell,
            ["seeds"] = @"\d+(?:,\d+)*",
        };
        foreach (var pin in pins.Sources)
        {
            expectedKeys[$"{pin.Id}.repository"] = @"https://github\.com/[A-Za-z0-9._-]+/[A-Za-z0-9._-]+";
            expectedKeys[$"{pin.Id}.commit"] = "[0-9a-f]{40}";
            expectedKeys[$"{pin.Id}.files"] = NaturalCell;
            expectedKeys[$"{pin.Id}.bytes"] = NaturalCell;
            expectedKeys[$"{pin.Id}.files_sha256"] = "[0-9a-f]{64}";
            expectedKeys[$"{pin.Id}.license"] = @"[A-Za-z0-9 .;,/()-]{1,200}";
            Assert.Equal(pin.Repository, sources[$"{pin.Id}.repository"]);
            Assert.Equal(pin.Commit, sources[$"{pin.Id}.commit"]);
            Assert.Equal(pin.Files.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), sources[$"{pin.Id}.files"]);
            Assert.Equal(pin.TotalBytes.ToString(System.Globalization.CultureInfo.InvariantCulture), sources[$"{pin.Id}.bytes"]);
            Assert.Equal(pin.FilesSha256, sources[$"{pin.Id}.files_sha256"]);
            Assert.Equal(pin.License, sources[$"{pin.Id}.license"]);
        }

        Assert.Equal(expectedKeys.Keys.Order(StringComparer.Ordinal), sources.Keys.Order(StringComparer.Ordinal));
        Assert.All(sources, pair => Assert.Matches($"^(?:{expectedKeys[pair.Key]})$", pair.Value));

        // Assert: the figures are consistent: rows read are the rows used plus the rows skipped, and no set has more correct rows than rows
        Assert.Equal(SetNames, tables["conversion.tsv"].Rows.Select(static row => row[0]));
        Assert.All(tables["conversion.tsv"].Rows, static row => Assert.Equal(int.Parse(row[1], System.Globalization.CultureInfo.InvariantCulture), int.Parse(row[2], System.Globalization.CultureInfo.InvariantCulture) + row.Skip(3).Take(5).Sum(static cell => int.Parse(cell, System.Globalization.CultureInfo.InvariantCulture))));
        Assert.Equal(SetNames, tables["summary.tsv"].Rows.Select(static row => row[0]));
        Assert.All(tables["summary.tsv"].Rows, static row => Assert.True(int.Parse(row[2], System.Globalization.CultureInfo.InvariantCulture) <= int.Parse(row[1], System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <summary>
    ///     Reads a record table, checking its bytes: LF only, no byte order mark and a final newline.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The header and the rows split into cells.</returns>
    private static (string Header, List<string[]> Rows) ReadTable(string path)
    {
        var bytes = File.ReadAllBytes(path);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, $"{path} starts with a byte order mark");
        var text = new System.Text.UTF8Encoding(false, true).GetString(bytes);
        Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
        Assert.EndsWith("\n", text, StringComparison.Ordinal);
        var lines = text[..^1].Split('\n');
        return (lines[0], [.. lines.Skip(1).Select(static line => line.Split('\t'))]);
    }

    /// <summary>
    ///     Asserts the header of a table and that every cell of every row matches the grammar of its column.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="header">The expected header line.</param>
    /// <param name="columns">The regular expression of each column.</param>
    private static void AssertGrammar((string Header, List<string[]> Rows) table, string header, string[] columns)
    {
        Assert.Equal(header, table.Header);
        Assert.NotEmpty(table.Rows);
        foreach (var row in table.Rows)
        {
            Assert.Equal(columns.Length, row.Length);
            for (var i = 0; i < columns.Length; i++)
            {
                Assert.Matches($"^(?:{columns[i]})$", row[i]);
            }
        }
    }
}
