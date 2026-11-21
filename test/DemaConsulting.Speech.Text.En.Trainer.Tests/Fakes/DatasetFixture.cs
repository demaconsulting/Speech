using System.Security.Cryptography;
using System.Text;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;

/// <summary>
///     Builds a small, valid Text.En training data folder in a temporary directory and offers the edits the
///     negative tests use to break exactly one thing at a time.
/// </summary>
/// <remarks>
///     The fixture has three readings (<c>lead.verb</c>, <c>lead.noun</c>, <c>wind.noun</c>), nine labeled rows spread
///     over the four splits (with the matching provenance rows), two regression overlay rows, a generalization set with
///     its header only, the documents that declare licenses, the attribution
///     file and a computed <c>MANIFEST.tsv</c>. Offsets are computed from the target word, never typed by hand. The
///     folder is created in code so no fixture data files are checked in, and it is deleted on disposal.
/// </remarks>
internal sealed class DatasetFixture : IDisposable
{
    /// <summary>The UTF-8 encoding without byte order mark used for every file the fixture writes.</summary>
    private static readonly UTF8Encoding Utf8 = new(false);

    /// <summary>The folder that holds the data root and, optionally, evaluation sets.</summary>
    private readonly string _baseDirectory;

    /// <summary>The evaluation-set folder, created on first use.</summary>
    private string? _evaluationDirectory;

    /// <summary>
    ///     Creates the fixture.
    /// </summary>
    /// <param name="rootRelativeToBase">
    ///     The data root relative to the fixture's base folder; empty to use the base folder itself.
    /// </param>
    public DatasetFixture(string rootRelativeToBase = "")
    {
        _baseDirectory = Path.Combine(Path.GetTempPath(), "texten-trainer-" + Guid.NewGuid().ToString("N"));
        Root = rootRelativeToBase.Length == 0 ? _baseDirectory : Path.Combine(_baseDirectory, rootRelativeToBase);
        Directory.CreateDirectory(Root);
        WriteInitialFiles();
        RewriteManifest();
    }

    /// <summary>Gets the data root folder.</summary>
    public string Root { get; }

    /// <summary>Gets the folder that contains the data root.</summary>
    public string BaseDirectory => _baseDirectory;

    /// <summary>Gets the evaluation-set folder, creating it (outside the data root) on first use.</summary>
    public string EvaluationDirectory
    {
        get
        {
            if (_evaluationDirectory is null)
            {
                _evaluationDirectory = Path.Combine(_baseDirectory + "-eval");
                Directory.CreateDirectory(_evaluationDirectory);
            }

            return _evaluationDirectory;
        }
    }

    /// <summary>
    ///     Determines whether a report holds a finding of a check whose message contains a text.
    /// </summary>
    /// <param name="report">The report.</param>
    /// <param name="check">The check name.</param>
    /// <param name="messagePart">A text the message must contain.</param>
    /// <returns><see langword="true"/> when such a finding exists.</returns>
    public static bool Has(CheckReport report, string check, string messagePart) =>
        report.Findings.Any(finding => finding.Check == check && finding.Message.Contains(messagePart, StringComparison.Ordinal));

    /// <summary>
    ///     Gets the full path of a file of the data root.
    /// </summary>
    /// <param name="relativePath">The path relative to the data root.</param>
    /// <returns>The full path.</returns>
    public string PathOf(string relativePath) => Path.Combine(Root, relativePath);

    /// <summary>
    ///     Reads a file of the data root as text.
    /// </summary>
    /// <param name="relativePath">The path relative to the data root.</param>
    /// <returns>The text.</returns>
    public string Read(string relativePath) => File.ReadAllText(PathOf(relativePath), Utf8);

    /// <summary>
    ///     Writes a text file in the data root (UTF-8 without byte order mark), creating folders as needed.
    /// </summary>
    /// <param name="relativePath">The path relative to the data root.</param>
    /// <param name="content">The content.</param>
    public void Write(string relativePath, string content)
    {
        var path = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, Utf8);
    }

    /// <summary>
    ///     Writes raw bytes in the data root, creating folders as needed.
    /// </summary>
    /// <param name="relativePath">The path relative to the data root.</param>
    /// <param name="bytes">The bytes.</param>
    public void WriteBytes(string relativePath, byte[] bytes)
    {
        var path = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    /// <summary>
    ///     Replaces a text in a file; the text must occur exactly once so a test cannot silently edit nothing.
    /// </summary>
    /// <param name="relativePath">The path relative to the data root.</param>
    /// <param name="oldText">The text to replace.</param>
    /// <param name="newText">The replacement.</param>
    public void Replace(string relativePath, string oldText, string newText)
    {
        var text = Read(relativePath);
        var first = text.IndexOf(oldText, StringComparison.Ordinal);
        if (first < 0 || text.IndexOf(oldText, first + 1, StringComparison.Ordinal) >= 0)
        {
            throw new InvalidOperationException($"'{oldText}' must occur exactly once in {relativePath}.");
        }

        Write(relativePath, text.Replace(oldText, newText, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Sets cells of the row with an id, addressed by column name.
    /// </summary>
    /// <param name="relativePath">The TSV file.</param>
    /// <param name="id">The first cell of the row.</param>
    /// <param name="changes">The column names and their new values.</param>
    public void EditRow(string relativePath, string id, params (string Column, string Value)[] changes)
    {
        var lines = ReadLines(relativePath);
        var header = lines[0].Split('\t');
        var index = lines.FindIndex(line => line.StartsWith(id + "\t", StringComparison.Ordinal));
        if (index < 0)
        {
            throw new InvalidOperationException($"No row '{id}' in {relativePath}.");
        }

        var cells = lines[index].Split('\t');
        foreach (var (column, value) in changes)
        {
            cells[Array.IndexOf(header, column)] = value;
        }

        lines[index] = string.Join('\t', cells);
        WriteLines(relativePath, lines);
    }

    /// <summary>
    ///     Removes the row with an id.
    /// </summary>
    /// <param name="relativePath">The TSV file.</param>
    /// <param name="id">The first cell of the row.</param>
    public void RemoveRow(string relativePath, string id)
    {
        var lines = ReadLines(relativePath);
        if (lines.RemoveAll(line => line.StartsWith(id + "\t", StringComparison.Ordinal)) != 1)
        {
            throw new InvalidOperationException($"Expected exactly one row '{id}' in {relativePath}.");
        }

        WriteLines(relativePath, lines);
    }

    /// <summary>
    ///     Appends a row to a TSV file.
    /// </summary>
    /// <param name="relativePath">The TSV file.</param>
    /// <param name="cells">The cells of the new row.</param>
    public void AppendRow(string relativePath, params string[] cells)
    {
        var lines = ReadLines(relativePath);
        lines.Add(string.Join('\t', cells));
        WriteLines(relativePath, lines);
    }

    /// <summary>
    ///     Appends a labeled data row whose offsets are computed from the target word.
    /// </summary>
    /// <param name="relativePath">The data file.</param>
    /// <param name="id">The row id.</param>
    /// <param name="readingId">The reading id; the lemma is its prefix.</param>
    /// <param name="sentence">The sentence.</param>
    /// <param name="word">The target word, which must occur in the sentence.</param>
    /// <param name="source">The source column.</param>
    /// <param name="license">The license column.</param>
    public void AppendDataRow(string relativePath, string id, string readingId, string sentence, string word, string source, string license)
    {
        var split = relativePath switch
        {
            "holdout-independent.tsv" => "test",
            _ => Path.GetFileNameWithoutExtension(relativePath),
        };
        AppendRow(relativePath, DataRowText(id, readingId, sentence, word, source, license, split));
    }

    /// <summary>
    ///     Writes a small, consistent pair of corpus files (source list and removals) and the manifest.
    /// </summary>
    /// <remarks>
    ///     <c>gutenberg-1</c> is the used book and <c>gutenberg-2</c> a development book excluded from the corpus; the
    ///     removals name one sentence of the used book.
    /// </remarks>
    public void WriteClusterFiles()
    {
        Write(
            ClusterDataChecker.SourcesFile,
            string.Join('\t', ClusterDataChecker.SourcesHeader) + "\n" +
            $"gutenberg-1\tcore-open\tA Book\tA. Writer, 1800-1870\t1850\thttps://gutenberg.pglaf.org/cache/epub/1/pg1.txt\tUS public domain: Public domain in the USA\tfixture\t{new string('a', 64)}\t120\tused\n" +
            "gutenberg-2\tcore-silver\tB Book\tB. Writer, 1810-1880\t-\thttps://gutenberg.pglaf.org/cache/epub/2/pg2.txt\tUS public domain: Public domain in the USA\tfixture\t-\t-\texcluded-dev\n");
        Write(
            ClusterDataChecker.RemovalsFile,
            string.Join('\t', ClusterDataChecker.RemovalsHeader) + "\n" + $"gutenberg-1\t0\t1\texact\tdev.tsv\tsyn-00003\t{new string('b', 64)}\n");
        RewriteManifest();
    }
    /// <summary>
    ///     Replaces <c>MANIFEST.tsv</c> with one that matches the files as they are now.
    /// </summary>
    public void RewriteManifest()
    {
        var builder = new StringBuilder();
        builder.Append("# Checksums of the files of the fixture; MANIFEST.tsv itself and local caches are not listed\n");
        builder.Append("# columns = file, sha256, bytes, rows (lines minus the header for .tsv files)\n");
        var files = Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(Root, path).Replace('\\', '/'))
            .Where(path => path != "MANIFEST.tsv" && !path.StartsWith("external/", StringComparison.Ordinal) && !path.StartsWith("work/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(PathOf(file));
            var lines = bytes.Count(b => b == (byte)'\n');
            var rows = file.EndsWith(".tsv", StringComparison.Ordinal) ? lines - 1 : lines;
            builder.Append($"{file}\t{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}\t{bytes.Length}\t{rows}\n");
        }

        Write("MANIFEST.tsv", builder.ToString());
    }

    /// <summary>
    ///     Edits the lines of <c>MANIFEST.tsv</c> (without the trailing newline) and writes it back.
    /// </summary>
    /// <param name="edit">The edit to apply to the line list.</param>
    public void EditManifestLines(Action<List<string>> edit)
    {
        var lines = ReadLines("MANIFEST.tsv");
        edit(lines);
        WriteLines("MANIFEST.tsv", lines);
    }

    /// <summary>
    ///     Writes an evaluation set as a text file, one sentence per line.
    /// </summary>
    /// <param name="name">The set name (file name without extension).</param>
    /// <param name="sentences">The sentences.</param>
    public void WriteEvaluationSet(string name, params string[] sentences) =>
        File.WriteAllText(Path.Combine(EvaluationDirectory, name + ".txt"), string.Join('\n', sentences) + "\n", Utf8);

    /// <summary>
    ///     Loads the data set and runs the manifest check.
    /// </summary>
    /// <returns>The report.</returns>
    public CheckReport CheckManifest()
    {
        var report = new CheckReport(1000);
        DataManifest.Verify(Root, report);
        return report;
    }

    /// <summary>
    ///     Loads the data set and runs the schema check.
    /// </summary>
    /// <returns>The report (including load findings).</returns>
    public CheckReport CheckSchema()
    {
        var report = new CheckReport(1000);
        new SchemaChecker(TextEnDataSet.Load(Root, report), report).Run();
        return report;
    }

    /// <summary>
    ///     Loads the data set and runs the license check.
    /// </summary>
    /// <returns>The report (including load findings).</returns>
    public CheckReport CheckLicense()
    {
        var report = new CheckReport(1000);
        new LicenseChecker(Root, TextEnDataSet.Load(Root, report), report).Run();
        return report;
    }

    /// <summary>
    ///     Loads the data set and runs the internal leakage check.
    /// </summary>
    /// <returns>The report (including load findings).</returns>
    public CheckReport CheckLeakage()
    {
        var report = new CheckReport(1000);
        new LeakageChecker(TextEnDataSet.Load(Root, report), report).CheckInternal();
        return report;
    }

    /// <summary>
    ///     Loads the data set and runs the evaluation overlay checks on the committed overlay files.
    /// </summary>
    /// <param name="shape">The quotas of a populated generalization set; <see langword="null"/> for the default.</param>
    /// <returns>The report (including load findings).</returns>
    public CheckReport CheckOverlays(GeneralizationShape? shape = null)
    {
        var report = new CheckReport(1000);
        new EvaluationOverlayChecker(TextEnDataSet.Load(Root, report), report, shape).Run();
        return report;
    }

    /// <summary>
    ///     Loads the data set and runs the corpus check.
    /// </summary>
    /// <returns>The report (including load findings).</returns>
    public CheckReport CheckClusters()
    {
        var report = new CheckReport(1000);
        new ClusterDataChecker(TextEnDataSet.Load(Root, report), report).Run();
        return report;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var directory in new[] { _baseDirectory, _baseDirectory + "-eval" })
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    /// <summary>
    ///     Renders a data row as a line of cells.
    /// </summary>
    /// <param name="id">The row id.</param>
    /// <param name="readingId">The reading id.</param>
    /// <param name="sentence">The sentence.</param>
    /// <param name="word">The target word.</param>
    /// <param name="source">The source.</param>
    /// <param name="license">The license.</param>
    /// <param name="split">The split column value.</param>
    /// <returns>The cells.</returns>
    private static string[] DataRowText(string id, string readingId, string sentence, string word, string source, string license, string split)
    {
        var start = sentence.IndexOf(word, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException($"'{word}' is not in '{sentence}'.");
        }

        var lemma = readingId.Split('.')[0];
        return [id, lemma, readingId, sentence, start.ToString(System.Globalization.CultureInfo.InvariantCulture), (start + word.Length).ToString(System.Globalization.CultureInfo.InvariantCulture), source, license, split];
    }

    /// <summary>
    ///     Reads the lines of a file without the empty element after the final newline.
    /// </summary>
    /// <param name="relativePath">The file.</param>
    /// <returns>The lines.</returns>
    private List<string> ReadLines(string relativePath)
    {
        var lines = Read(relativePath).Split('\n').ToList();
        if (lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    /// <summary>
    ///     Writes lines with LF terminators and a final newline.
    /// </summary>
    /// <param name="relativePath">The file.</param>
    /// <param name="lines">The lines.</param>
    private void WriteLines(string relativePath, List<string> lines) => Write(relativePath, string.Join('\n', lines) + "\n");

    /// <summary>
    ///     Writes a table from a header and rows.
    /// </summary>
    /// <param name="relativePath">The file.</param>
    /// <param name="header">The header cells.</param>
    /// <param name="rows">The rows.</param>
    private void WriteTable(string relativePath, string[] header, IEnumerable<string[]> rows) =>
        Write(relativePath, string.Join('\n', new[] { header }.Concat(rows).Select(cells => string.Join('\t', cells))) + "\n");

    /// <summary>
    ///     Writes every file of the valid fixture except the manifest.
    /// </summary>
    private void WriteInitialFiles()
    {
        WriteTable(
            "readings.tsv",
            ["reading_id", "lemma", "pos_hint", "description", "status"],
            [
                ["lead.verb", "lead", "VERB", "Verb use of \"lead\", to guide, as opposed to the metal.", "active"],
                ["lead.noun", "lead", "NOUN", "Noun use of \"lead\", the heavy metal, as opposed to the verb.", "active"],
                ["wind.noun", "wind", "NOUN", "Noun use of \"wind\", moving air.", "active"],
            ]);
        WriteTable(
            "whd-mapping.tsv",
            ["whd_wordid", "reading_id"],
            [["lead_vrb", "lead.verb"], ["lead_nou", "lead.noun"], ["wind_nou", "wind.noun"]]);
        WriteTable(
            "forms.tsv",
            ["form", "reading_id", "kind"],
            [["lead", "lead.verb", "base"], ["lead", "lead.noun", "base"], ["leads", "lead.verb", "inflection"], ["wind", "wind.noun", "base"]]);
        WriteTable(
            "extension-decisions.tsv",
            ["rank", "lemma", "forms", "kind", "class", "decision", "reason", "cmudict_evidence", "readings", "readings_added"],
            [
                ["1", "wind", "wind", "new-forms", "R", "add", "class R", "wind: 2 entries", "wind.noun", "1"],
                ["2", "bow", "bow", "new-lemma", "R", "defer", "deferred", "none", string.Empty, "0"],
            ]);

        string[] dataHeader = ["id", "lemma", "reading_id", "sentence", "start", "end", "source", "license", "split"];
        WriteTable(
            "train.tsv",
            dataHeader,
            [
                DataRowText("harvest-00001", "lead.verb", "She will lead the team today.", "lead", "harvest-masc", "CC-BY-3.0-US", "train"),
                DataRowText("syn-00001", "lead.noun", "The lead pipe was heavy.", "lead", "synthetic", "CC0-1.0", "train"),
                DataRowText("syn-00002", "wind.noun", "A cold wind blew hard.", "wind", "synthetic", "CC0-1.0", "train"),
            ]);
        WriteTable(
            "dev.tsv",
            dataHeader,
            [
                DataRowText("syn-00003", "lead.verb", "They lead the march.", "lead", "synthetic", "CC0-1.0", "dev"),
                DataRowText("harvest-00003", "wind.noun", "The wind howled all night.", "wind", "harvest-imperative", "Public-Domain", "dev"),
            ]);
        WriteTable(
            "holdout-natural.tsv",
            dataHeader,
            [DataRowText("harvest-u00001", "lead.noun", "Lead is a heavy metal.", "Lead", "harvest-masc-llm", "CC-BY-3.0-US", "holdout-natural")]);
        WriteTable(
            "holdout-independent.tsv",
            dataHeader,
            [
                DataRowText("hind-1", "lead.verb", "Please lead the way now.", "lead", "independent-holdout", "CC0-1.0", "test"),
                DataRowText("hind-2", "lead.noun", "Some lead remained in the pipe.", "lead", "independent-holdout", "CC0-1.0", "test"),
                DataRowText("hind-3", "wind.noun", "Wind turbines turn slowly.", "Wind", "independent-holdout", "CC0-1.0", "test"),
            ]);
        WriteTable(
            "harvest-provenance.tsv",
            ["id", "source_id", "corpus", "sent_id", "label_method"],
            [
                ["harvest-00001", "masc:written/blog/one", "masc", "masc:written/blog/one#1", "upos"],
                ["harvest-00003", "imperative:doc/two", "imperative", "imperative:doc/two#1", "upos"],
                ["harvest-u00001", "masc:written/blog/three", "masc", "masc:written/blog/three#1", "llm"],
            ]);
        WriteTable(
            "synthetic-provenance.tsv",
            ["id", "spike_id", "batch", "template"],
            [["syn-00001", "s1", "lead", "the _ pipe was"], ["syn-00002", "s2", "wind", "a cold _ blew"], ["syn-00003", "s3", "lead", "they _ the march"]]);
        WriteTable(
            "gaps-v4.tsv",
            ["reading_id", "train_natural", "train_synthetic", "dev", "holdout_natural", "holdout_independent", "need_synthetic", "need_natural"],
            [
                ["lead.verb", "1", "0", "1", "0", "1", "39", "9"],
                ["lead.noun", "0", "1", "0", "1", "1", "39", "10"],
                ["wind.noun", "0", "1", "1", "0", "1", "39", "10"],
            ]);

        Write("README.md", "# Fixture data\n\nLicense: CC0-1.0 AND CC-BY-3.0-US AND Public-Domain\n");
        Write("prompts/README.md", "# Prompts\n\nLicense: CC0-1.0\n");
        Write("audit/audit-summary.md", "# Audit\n\nLicense: CC0-1.0\n");
        Write("ATTRIBUTION.md", "# MASC Attribution\n\nLicense: CC-BY-3.0-US\n\nSentences are adapted from MASC.\n");

        // The evaluation overlays: a regression row that refers to a training row and one from a golden test file, and
        // a generalization set that is not written yet (header only)
        WriteTable(
            TextEnDataSet.RegressionFile,
            [.. TextEnDataSet.RegressionHeader],
            [
                RegressionRowText("reg-0001", "lead.noun", "The lead pipe was heavy.", "lead", "data-row", "train.tsv:syn-00001", "yes"),
                RegressionRowText("reg-0002", "lead.verb", "Guides lead the hikers uphill.", "lead", "golden-readings", "readings-golden.tsv:2", "no"),
            ]);
        WriteTable(TextEnDataSet.GeneralizationFile, [.. TextEnDataSet.GeneralizationHeader], []);
    }

    /// <summary>
    ///     Renders a regression overlay row whose offsets are computed from the target word.
    /// </summary>
    /// <param name="id">The row id.</param>
    /// <param name="readingId">The reading id; the lemma is its prefix.</param>
    /// <param name="sentence">The sentence.</param>
    /// <param name="word">The target word, which must occur in the sentence.</param>
    /// <param name="origin">The origin.</param>
    /// <param name="reference">The reference.</param>
    /// <param name="seen">The seen-in-training mark.</param>
    /// <param name="license">The license.</param>
    /// <returns>The cells.</returns>
    public static string[] RegressionRowText(string id, string readingId, string sentence, string word, string origin, string reference, string seen, string license = "CC0-1.0")
    {
        var row = DataRowText(id, readingId, sentence, word, "unused", license, "unused");
        return [row[0], row[1], row[2], row[3], row[4], row[5], origin, reference, seen, license];
    }

    /// <summary>
    ///     Renders a generalization set row whose offsets are computed from the target word.
    /// </summary>
    /// <param name="id">The row id.</param>
    /// <param name="readingId">The reading id; the lemma is its prefix.</param>
    /// <param name="sentence">The sentence.</param>
    /// <param name="word">The target word, which must occur in the sentence.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="family">The family.</param>
    /// <param name="cueForms">The cue forms.</param>
    /// <param name="cueUnsolvable">The cue-unsolvable mark.</param>
    /// <returns>The cells.</returns>
    public static string[] GeneralizationRowText(string id, string readingId, string sentence, string word, string kind, string family, string cueForms, string cueUnsolvable = "no")
    {
        var row = DataRowText(id, readingId, sentence, word, "unused", "CC0-1.0", "unused");
        return [row[0], row[1], row[2], row[3], row[4], row[5], kind, family, cueForms, cueUnsolvable, "generalization", "CC0-1.0"];
    }

    /// <summary>
    ///     Writes a generalization set file (outside the data root when <paramref name="path"/> is absolute).
    /// </summary>
    /// <param name="path">The path, relative to the data root or absolute.</param>
    /// <param name="rows">The rows.</param>
    public void WriteGeneralization(string path, params string[][] rows) =>
        File.WriteAllText(
            Path.Combine(Root, path),
            string.Join('\n', new[] { TextEnDataSet.GeneralizationHeader.ToArray() }.Concat(rows).Select(cells => string.Join('\t', cells))) + "\n",
            Utf8);
}
