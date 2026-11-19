using System.Globalization;
using System.Text;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tools.ReadingSubsystem;

/// <summary>
///     The <c>propose-readings</c> command: proposes pronunciations for the reading ids of the text analysis
///     package for human review, and with <c>--check</c> verifies the frozen table against the inventory.
/// </summary>
/// <remarks>
///     <para>
///     <b>Propose</b> (the default) reads <c>data/text-en/readings.tsv</c>, proposes a pronunciation per reading
///     from CMUdict by stress signature and named rules (<see cref="ReadingProposer"/>) and writes a review sheet and
///     a proposed table into the git-ignored <c>data/phonemes-en/work</c> folder (or <c>--out-dir</c>). The proposal
///     is never the frozen table: the committed <c>reading-pronunciations.tsv</c> is reviewed and edited by hand.
///     </para>
///     <para>
///     <b>Check</b> verifies that the committed table has exactly the inventory's reading ids, that every
///     <c>cmu#N</c> row is variant <c>N</c> of its lemma in the lexicon, that every (form, reading id) pair of
///     <c>data/text-en/forms.tsv</c> resolves to a non-empty pronunciation unless it is listed in the committed
///     <c>reading-gaps.tsv</c>, and that every listed gap really is one. It prints the row counts by source kind.
///     <c>--write-gaps</c> rewrites the gap list from the computed result (the header only when there are none).
///     </para>
///     <para>
///     Exit codes: <c>0</c> proposed or consistent, <c>1</c> an inconsistent table, <c>2</c> a usage or I/O error.
///     </para>
/// </remarks>
internal static class ProposeReadingsCommand
{
    /// <summary>The name of the command as typed on the command line.</summary>
    public const string Name = "propose-readings";

    /// <summary>The header line of the gap list.</summary>
    private const string GapHeader = "form\treading_id\treason";

    /// <summary>The usage text.</summary>
    public const string Usage = """
        Usage: propose-readings [options]
               propose-readings --check [--write-gaps] [options]

        Proposes a pronunciation for every reading id of data/text-en/readings.tsv from CMUdict (stress signature and
        named rules) into reading-review.tsv and reading-pronunciations.proposed.tsv in the output folder, or with
        --check verifies the committed data/phonemes-en/reading-pronunciations.tsv, form-overrides.tsv and
        reading-gaps.tsv against the inventory and the lexicon.

        Options:
          --root <dir>      The repository root (default: searched upward from the current directory for Speech.slnx).
          --out-dir <dir>   Where the proposal is written (default: data/phonemes-en/work).
          --check           Verify the committed table instead of proposing.
          --write-gaps      With --check: rewrite reading-gaps.tsv from the computed unresolved pairs.
          --dump <file>     With --check: write every (form, reading id) pair with its pronunciation, for review.
          --help            Shows this text.

        Exit codes: 0 = proposed or consistent, 1 = inconsistent, 2 = usage or I/O error.
        """;

    /// <summary>
    ///     Runs the command.
    /// </summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="output">Receives progress and results.</param>
    /// <param name="error">Receives usage and I/O errors.</param>
    /// <param name="currentDirectory">The directory relative paths are resolved against and the root is searched from.</param>
    /// <param name="baseDirectory">The tool's own folder, the second place the root is searched from.</param>
    /// <returns>The exit code: 0, 1 or 2.</returns>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error, string currentDirectory, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        string? root = null;
        string? outDirectory = null;
        string? dumpPath = null;
        var check = false;
        var writeGaps = false;
        var pending = new Queue<string>(args);
        while (pending.TryDequeue(out var option))
        {
            switch (option)
            {
                case "--help" or "-h":
                    output.WriteLine(Usage);
                    return 0;
                case "--check":
                    check = true;
                    break;
                case "--write-gaps":
                    writeGaps = true;
                    break;
                case "--root" when pending.Count > 0:
                    root = Path.GetFullPath(pending.Dequeue(), currentDirectory);
                    break;
                case "--out-dir" when pending.Count > 0:
                    outDirectory = Path.GetFullPath(pending.Dequeue(), currentDirectory);
                    break;
                case "--dump" when pending.Count > 0:
                    dumpPath = Path.GetFullPath(pending.Dequeue(), currentDirectory);
                    break;
                default:
                    error.WriteLine($"Unknown or incomplete option '{option}'.");
                    error.WriteLine(Usage);
                    return 2;
            }
        }

        try
        {
            root ??= ToolPaths.FindRepositoryRoot(currentDirectory) ?? ToolPaths.FindRepositoryRoot(baseDirectory);
            if (root is null)
            {
                error.WriteLine("The repository root (Speech.slnx) was not found; use --root <dir>.");
                return 2;
            }

            return check
                ? Check(root, writeGaps, dumpPath, output)
                : Propose(root, outDirectory ?? Path.Combine(ToolPaths.DataDirectory(root), "work"), output);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            error.WriteLine("propose-readings failed: " + exception.Message);
            return 2;
        }
    }

    /// <summary>
    ///     Writes the review sheet and the proposed table.
    /// </summary>
    /// <param name="root">The repository root.</param>
    /// <param name="outDirectory">The output folder.</param>
    /// <param name="output">Receives the summary.</param>
    /// <returns>Always <c>0</c>.</returns>
    private static int Propose(string root, string outDirectory, TextWriter output)
    {
        var readings = ReadingProposer.LoadReadings(Path.Combine(root, "data", "text-en", "readings.tsv"));
        var lexicon = CmuLexicon.Default;
        var proposals = ReadingProposer.Propose(readings, lexicon);

        Directory.CreateDirectory(outDirectory);
        File.WriteAllText(Path.Combine(outDirectory, "reading-review.tsv"), ReadingProposer.FormatReview(proposals, lexicon), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outDirectory, "reading-pronunciations.proposed.tsv"), ReadingProposer.FormatTable(proposals), new UTF8Encoding(false));

        output.WriteLine($"Proposed {proposals.Count} readings into {outDirectory}");
        foreach (var group in proposals.GroupBy(p => SourceKind(p.Source)).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            output.WriteLine($"  {group.Key}: {group.Count()}");
        }

        return 0;
    }

    /// <summary>
    ///     Verifies the committed tables against the inventory and the lexicon.
    /// </summary>
    /// <param name="root">The repository root.</param>
    /// <param name="writeGaps">Whether to rewrite the gap list.</param>
    /// <param name="dumpPath">Where to write the audit sheet of every resolved pair, or <see langword="null"/>.</param>
    /// <param name="output">Receives the report.</param>
    /// <returns><c>0</c> when consistent, otherwise <c>1</c>.</returns>
    private static int Check(string root, bool writeGaps, string? dumpPath, TextWriter output)
    {
        var data = ToolPaths.DataDirectory(root);
        var readings = ReadingProposer.LoadReadings(Path.Combine(root, "data", "text-en", "readings.tsv"));
        using var tableReader = new StreamReader(Path.Combine(data, "reading-pronunciations.tsv"));
        using var overrideReader = new StreamReader(Path.Combine(data, "form-overrides.tsv"));
        var tables = ReadingPronunciations.Parse(tableReader, overrideReader);
        var lexicon = CmuLexicon.Default;
        var problems = new List<string>();

        // The table covers exactly the inventory's readings
        var ids = readings.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in ids.Where(id => !tables.TryGetEntry(id, out _)).OrderBy(id => id, StringComparer.Ordinal))
        {
            problems.Add("missing row: " + id);
        }

        foreach (var entry in tables.Entries.Where(e => !ids.Contains(e.ReadingId)).OrderBy(e => e.ReadingId, StringComparer.Ordinal))
        {
            problems.Add("row for an id that is not in the inventory: " + entry.ReadingId);
        }

        // Every cmu#N row is variant N of its lemma
        foreach (var entry in tables.Entries.Where(e => e.Source.StartsWith("cmu#", StringComparison.Ordinal)).OrderBy(e => e.ReadingId, StringComparer.Ordinal))
        {
            var variants = lexicon.Lookup(ReadingPronunciations.LemmaOf(entry.ReadingId));
            var number = int.Parse(entry.Source.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture);
            if (number > variants.Count || !variants[number - 1].Equals(entry.Pronunciation))
            {
                problems.Add($"{entry.ReadingId}: '{entry.Source}' is not the lexicon's variant ({entry.Pronunciation})");
            }
        }

        // Every (form, reading) pair resolves, or is a listed gap
        var unresolved = new List<(string Form, string ReadingId)>();
        var pairs = 0;
        foreach (var line in File.ReadLines(Path.Combine(root, "data", "text-en", "forms.tsv")).Skip(1))
        {
            var fields = line.Split('\t');
            if (fields.Length < 2)
            {
                continue;
            }

            pairs++;
            if (!tables.TryResolve(fields[0], fields[1], out var pronunciation) || pronunciation is null || pronunciation.Count == 0)
            {
                unresolved.Add((fields[0], fields[1]));
            }
        }

        var gapPath = Path.Combine(data, "reading-gaps.tsv");
        if (dumpPath is not null)
        {
            // The audit sheet: every pair with the pronunciation it resolves to
            var sheet = new StringBuilder("form\treading_id\tarpabet\n");
            foreach (var line in File.ReadLines(Path.Combine(root, "data", "text-en", "forms.tsv")).Skip(1))
            {
                var fields = line.Split('\t');
                if (fields.Length >= 2)
                {
                    tables.TryResolve(fields[0], fields[1], out var resolved);
                    sheet.Append(fields[0]).Append('\t').Append(fields[1]).Append('\t').Append(resolved?.ToString() ?? "?").Append('\n');
                }
            }

            File.WriteAllText(dumpPath, sheet.ToString(), new UTF8Encoding(false));
        }
        if (writeGaps)
        {
            File.WriteAllText(gapPath, GapHeader + "\n" + string.Concat(unresolved.Select(u => $"{u.Form}\t{u.ReadingId}\tno regular inflection and no form override\n")), new UTF8Encoding(false));
        }

        var listed = File.Exists(gapPath)
            ? File.ReadLines(gapPath).Skip(1).Where(l => l.Length > 0).Select(l => l.Split('\t')).Select(f => (f[0], f[1])).ToHashSet()
            : [];
        foreach (var gap in unresolved.Where(u => !listed.Contains(u)))
        {
            problems.Add($"unresolved and not listed as a gap: ({gap.Form}, {gap.ReadingId})");
        }

        foreach (var gap in listed.Where(g => !unresolved.Contains(g)))
        {
            problems.Add($"listed as a gap but it resolves: ({gap.Item1}, {gap.Item2})");
        }

        output.WriteLine($"Reading table: {tables.Entries.Count} rows for {ids.Count} inventory readings; {tables.OverrideCount} form overrides");
        foreach (var group in tables.Entries.GroupBy(e => SourceKind(e.Source)).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            output.WriteLine($"  {group.Key}: {group.Count()}");
        }

        output.WriteLine($"Form pairs: {pairs}; unresolved gaps: {unresolved.Count}");
        foreach (var problem in problems)
        {
            output.WriteLine("PROBLEM: " + problem);
        }

        output.WriteLine(problems.Count == 0 ? "Consistent." : $"{problems.Count} problem(s).");
        return problems.Count == 0 ? 0 : 1;
    }

    /// <summary>
    ///     Gets the kind of a provenance field.
    /// </summary>
    /// <param name="source">The field.</param>
    /// <returns><c>cmu</c>, <c>rule</c> or <c>authored</c>.</returns>
    private static string SourceKind(string source)
    {
        if (source.StartsWith("cmu#", StringComparison.Ordinal))
        {
            return "cmu";
        }

        return source.StartsWith("rule:", StringComparison.Ordinal) ? "rule" : "authored";
    }
}
