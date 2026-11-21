using System.Diagnostics;
using System.Globalization;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;

/// <summary>The seams of the <c>cluster</c> command that tests replace: the network and the pause between requests.</summary>
/// <param name="Fetcher">Opens a URL instead of the network, or null for the HTTPS mirror download.</param>
/// <param name="Sleeper">Pauses instead of the real pause, or null for the real one.</param>
internal sealed record ClusterEnvironment(Func<Uri, CancellationToken, Stream>? Fetcher = null, Action<TimeSpan>? Sleeper = null);

/// <summary>
///     The <c>cluster</c> command: fetches and pins the word-cluster corpus, audits it against the evaluation sets,
///     reports the vocabulary preflight, and induces, writes or re-verifies the word-cluster table and its record.
/// </summary>
/// <remarks>
///     <para>
///     The command is a manual developer tool: only its unit and subsystem tests run in CI, offline. Exactly one mode is
///     given per run. <c>--fetch</c> is the only mode that uses the network. <c>--write</c> refuses to run when the
///     committed removals are not what the audit computes now, so the removals are always reviewed on their own first.
///     <c>--check</c> repeats the whole induction with the settings of the committed record and compares bytes, writing
///     nothing: the snapshot is only measured and compared with the existing archive, never rebuilt. Console
///     output holds counts, ids, hashes and timings, never corpus text. Exit codes: <c>0</c> done or current, <c>1</c>
///     stale output or a failed book, <c>2</c> usage, I/O or data error.
///     </para>
/// </remarks>
internal static class ClusterCommand
{
    /// <summary>The name of the command as typed on the command line.</summary>
    public const string Name = "cluster";

    /// <summary>The default number of clusters.</summary>
    public const int DefaultClusters = 512;

    /// <summary>The default smallest corpus count of a vocabulary word.</summary>
    public const int DefaultMinCount = 5;

    /// <summary>The default polite delay between two requests to the mirror, in milliseconds.</summary>
    public const int DefaultDelayMs = 2000;

    /// <summary>The smallest delay between two requests accepted, in milliseconds.</summary>
    public const int MinimumDelayMs = 1000;

    /// <summary>The usage text.</summary>
    public const string Usage = """
        Usage: cluster <mode> [options]

        Builds the Text.En word-cluster table from the pinned public-domain corpus listed in cluster-sources.tsv.
        Manual tool: CI runs only its tests. Exactly one mode per run.

        Modes:
          --fetch [--write-pins]  Fetch the plain text and metadata of every used book that is missing from the cache or
                                  fails its pin, from an official mirror only, one request at a time with --delay-ms
                                  between requests. --write-pins fills the body pins of rows that have none.
          --audit [--write]       Compare the corpus with every evaluation sentence and print the removals per file and
                                  rule; --write writes cluster-removals.tsv, otherwise exit 1 when it is not current.
          --preflight [--words a,b,...]
                                  Print the token coverage and the count and rank of each nominated word (default: the
                                  pre-registered list) for vocabulary sizes 30,000 to 75,000. Never reads the
                                  generalization set.
          --write                 Verify the pins and the removals, count, induce, then write word-clusters.tsv and
                                  clustering.json (to --out) and the corpus snapshot (to the cache).
          --check                 Repeat --write with the settings of the record in --out and compare the table and the
                                  record byte for byte and the existing snapshot (--from-snapshot, otherwise the cache's)
                                  by hash, writing nothing; exit 1 when one is stale or the snapshot is missing.

        Options:
          --clusters <n>      The number of clusters (default 512; --write only).
          --max-words <n>     The vocabulary limit, 0 for none (default 0; --write and --preflight).
          --min-count <n>     The smallest corpus count of a vocabulary word (default 5; --write and --preflight).
          --threads <n>       Worker threads (default: the processor count). The output does not depend on it.
          --out <dir>         The folder of word-clusters.tsv and clustering.json (default: the data folder).
          --delay-ms <n>      The pause between two mirror requests (default 2000, at least 1000).
          --mirror <host>     Fetch from this allowed mirror instead of the listed one (gutenberg.pglaf.org or
                              aleph.gutenberg.org).
          --cache <dir>       The corpus cache (default: <data>/external/cluster-corpus); it must be inside
                              <data>/external (git-ignored) or outside the repository.
          --from-snapshot <file>
                              Read the bodies from a snapshot archive instead of the cache (verified against the pins).
          --data <dir>        The data folder (default: data/text-en, searched upward from the current directory).
          --help              Shows this text.

        Exit codes: 0 = done or current, 1 = stale or a book failed, 2 = usage, I/O or data error.
        """;

    /// <summary>The candidate vocabulary sizes of the preflight (pre-registered).</summary>
    public static readonly IReadOnlyList<int> PreflightSizes = [30_000, 40_000, 50_000, 60_000, 75_000];

    /// <summary>The nominated cue words of the preflight (pre-registered in the run log; lower case).</summary>
    public static readonly IReadOnlyList<string> NominatedWords =
    [
        "muscle", "muscles", "ligament", "ligaments", "tendon", "tendons", "hamstring", "meniscus", "cartilage", "rotator",
        "cuff", "acl", "labrum", "groin", "fabric", "cloth", "canvas", "sail", "seam", "sleeve", "lining", "denim", "wear",
        "gash", "gashes", "laceration", "lacerations", "sprain", "bruise", "stab", "knife", "bullet", "sinew", "calf",
        "thigh", "shin", "bandage", "garment", "linen", "silk", "satin", "tarpaulin", "curtain", "paper", "clock", "watch",
        "spool", "reel", "bobbin", "coil", "fiddle", "cello", "viola", "drum", "trout", "perch", "pike", "angler", "rod",
        "microscopic", "infinitesimal", "hour", "second",
        "very", "so", "too", "quite", "extremely", "rather", "most", "more", "less", "really", "incredibly", "exceedingly",
        "remarkably", "fairly", "somewhat", "highly", "utterly", "terribly", "awfully", "perfectly", "completely",
        "entirely", "absolutely", "particularly", "especially", "unusually", "surprisingly", "equally", "pretty",
        "astonishingly", "uncommonly", "infinitely", "singularly", "wonderfully",
        "yesterday", "ago", "previously", "formerly", "earlier", "beforehand",
    ];

    /// <summary>The mode of a run.</summary>
    internal enum ClusterMode
    {
        /// <summary>Fetch and pin the corpus.</summary>
        Fetch,

        /// <summary>Run the leakage audit.</summary>
        Audit,

        /// <summary>Report the vocabulary preflight.</summary>
        Preflight,

        /// <summary>Induce and write the table.</summary>
        Write,

        /// <summary>Induce and compare with the written table.</summary>
        Check,
    }

    /// <summary>
    ///     The parsed command line.
    /// </summary>
    /// <param name="Mode">The mode.</param>
    /// <param name="WritePins">Whether <c>--fetch</c> fills missing pins.</param>
    /// <param name="WriteRemovals">Whether <c>--audit</c> writes the removals.</param>
    /// <param name="Words">The preflight words, or null for the nominated list.</param>
    /// <param name="Clusters">The number of clusters, or null when not given.</param>
    /// <param name="MaxWords">The vocabulary limit, or null when not given.</param>
    /// <param name="MinCount">The minimum count, or null when not given.</param>
    /// <param name="Threads">The worker threads.</param>
    /// <param name="DelayMs">The delay between requests.</param>
    /// <param name="Out">The output folder, or null for the data folder.</param>
    /// <param name="Mirror">The mirror host, or null.</param>
    /// <param name="Cache">The cache folder, or null for the default.</param>
    /// <param name="FromSnapshot">The snapshot to read bodies from, or null.</param>
    /// <param name="Data">The data folder, or null to discover it.</param>
    /// <param name="ShowHelp">Whether usage was requested.</param>
    internal sealed record ClusterOptions(
        ClusterMode Mode,
        bool WritePins,
        bool WriteRemovals,
        IReadOnlyList<string>? Words,
        int? Clusters,
        int? MaxWords,
        int? MinCount,
        int Threads,
        int DelayMs,
        string? Out,
        string? Mirror,
        string? Cache,
        string? FromSnapshot,
        string? Data,
        bool ShowHelp);

    /// <summary>The resolved folders and files of a run.</summary>
    /// <param name="Data">The data folder.</param>
    /// <param name="Out">The output folder.</param>
    /// <param name="Cache">The cache folder.</param>
    /// <param name="Snapshot">The snapshot in the cache: built by <c>--write</c>, compared by <c>--check</c>.</param>
    private sealed record ClusterPaths(string Data, string Out, string Cache, string Snapshot);

    /// <summary>The products of one induction.</summary>
    /// <param name="Table">The table bytes.</param>
    /// <param name="Record">The record.</param>
    /// <param name="Snapshot">The built (write) or measured (check) snapshot.</param>
    private sealed record Induction(byte[] Table, ClusteringRecord Record, CorpusSnapshotInfo Snapshot);

    /// <summary>
    ///     Runs the command.
    /// </summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="output">Receives results and progress.</param>
    /// <param name="error">Receives usage and I/O errors.</param>
    /// <param name="currentDirectory">The directory relative paths are resolved against.</param>
    /// <param name="baseDirectory">The tool's own folder, the second place the data folder is searched from.</param>
    /// <param name="environment">The network and pause seams, or null for the real ones.</param>
    /// <returns>The exit code: 0, 1 or 2.</returns>
    public static int Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        string currentDirectory,
        string baseDirectory,
        ClusterEnvironment? environment = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var options = TryParse(args, out var parseError);
        if (options is null)
        {
            return UsageError(error, parseError!);
        }

        if (options.ShowHelp)
        {
            output.WriteLine(Usage);
            return 0;
        }

        try
        {
            var root = options.Data is not null
                ? Path.GetFullPath(options.Data, currentDirectory)
                : CheckDataCommand.FindDataRoot(currentDirectory) ?? CheckDataCommand.FindDataRoot(baseDirectory);
            if (root is null || !Directory.Exists(root))
            {
                return UsageError(error, $"data folder '{root ?? "data/text-en"}' was not found; use --data <dir>");
            }

            var paths = ResolvePaths(options, root, currentDirectory);
            environment ??= new ClusterEnvironment();
            return options.Mode switch
            {
                ClusterMode.Fetch => Fetch(options, paths, output, environment),
                ClusterMode.Audit => Audit(options, paths, output, currentDirectory),
                ClusterMode.Preflight => Preflight(options, paths, output, currentDirectory),
                _ => WriteOrCheck(options, paths, output, currentDirectory),
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException or InvalidOperationException)
        {
            return UsageError(error, exception.Message);
        }
    }

    /// <summary>
    ///     Parses the arguments.
    /// </summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="parseError">The problem when parsing failed.</param>
    /// <returns>The options, or null when the arguments are invalid.</returns>
    internal static ClusterOptions? TryParse(IReadOnlyList<string> args, out string? parseError)
    {
        ArgumentNullException.ThrowIfNull(args);
        parseError = null;
        var modes = new List<ClusterMode>();
        bool writePins = false, write = false, help = false;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        var position = 0;
        while (position < args.Count)
        {
            var token = args[position++];
            var separator = token.StartsWith("--", StringComparison.Ordinal) ? token.IndexOf('=') : -1;
            var name = separator > 0 ? token[..separator] : token;
            var inline = separator > 0 ? token[(separator + 1)..] : null;
            switch (name)
            {
                case "--help" or "-h" or "-?":
                    help = true;
                    break;
                case "--fetch":
                    modes.Add(ClusterMode.Fetch);
                    break;
                case "--audit":
                    modes.Add(ClusterMode.Audit);
                    break;
                case "--preflight":
                    modes.Add(ClusterMode.Preflight);
                    break;
                case "--check":
                    modes.Add(ClusterMode.Check);
                    break;
                case "--write":
                    write = true;
                    break;
                case "--write-pins":
                    writePins = true;
                    break;
                case "--words" or "--clusters" or "--max-words" or "--min-count" or "--threads" or "--delay-ms" or "--out"
                    or "--mirror" or "--cache" or "--from-snapshot" or "--data":
                    var value = inline ?? (position < args.Count ? args[position++] : null);
                    if (string.IsNullOrEmpty(value))
                    {
                        parseError = $"option {name} needs a value";
                        return null;
                    }

                    values[name] = value;
                    break;
                default:
                    parseError = $"unknown option '{token}'";
                    return null;
            }
        }

        if (help)
        {
            return new ClusterOptions(ClusterMode.Write, false, false, null, null, null, null, 1, DefaultDelayMs, null, null, null, null, null, true);
        }

        // --write alone is the write mode; with --audit it writes the removals
        if (write && !modes.Contains(ClusterMode.Audit))
        {
            modes.Add(ClusterMode.Write);
        }

        // The numeric options, each with its smallest valid value
        var clusters = Number(values, "--clusters", 2, ref parseError);
        var maxWords = Number(values, "--max-words", 0, ref parseError);
        var minCount = Number(values, "--min-count", 1, ref parseError);
        var threads = Number(values, "--threads", 1, ref parseError);
        var delay = Number(values, "--delay-ms", MinimumDelayMs, ref parseError);
        IReadOnlyList<string>? words = values.TryGetValue("--words", out var list)
            ? [.. list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]
            : null;
        var mirror = values.GetValueOrDefault("--mirror");
        parseError ??= Validate(modes, writePins, words, clusters, maxWords, minCount, mirror);
        if (parseError is not null)
        {
            return null;
        }

        return new ClusterOptions(
            modes[0],
            writePins,
            write && modes[0] == ClusterMode.Audit,
            words,
            clusters,
            maxWords,
            minCount,
            threads ?? Environment.ProcessorCount,
            delay ?? DefaultDelayMs,
            values.GetValueOrDefault("--out"),
            mirror,
            values.GetValueOrDefault("--cache"),
            values.GetValueOrDefault("--from-snapshot"),
            values.GetValueOrDefault("--data"),
            false);
    }

    /// <summary>
    ///     Reads an integer option.
    /// </summary>
    /// <param name="values">The option values by name.</param>
    /// <param name="name">The option name.</param>
    /// <param name="minimum">The smallest valid value.</param>
    /// <param name="parseError">Set to the problem when the value is invalid and no earlier problem was found.</param>
    /// <returns>The value, or null when the option was not given or is invalid.</returns>
    private static int? Number(Dictionary<string, string> values, string name, int minimum, ref string? parseError)
    {
        if (!values.TryGetValue(name, out var text))
        {
            return null;
        }

        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= minimum)
        {
            return number;
        }

        parseError ??= $"option {name} needs an integer of at least {minimum}";
        return null;
    }

    /// <summary>
    ///     Checks that the options fit the mode.
    /// </summary>
    /// <param name="modes">The modes given.</param>
    /// <param name="writePins">Whether <c>--write-pins</c> was given.</param>
    /// <param name="words">The preflight words.</param>
    /// <param name="clusters">The cluster count.</param>
    /// <param name="maxWords">The vocabulary limit.</param>
    /// <param name="minCount">The minimum count.</param>
    /// <param name="mirror">The mirror host.</param>
    /// <returns>The problem, or null.</returns>
    private static string? Validate(List<ClusterMode> modes, bool writePins, IReadOnlyList<string>? words, int? clusters, int? maxWords, int? minCount, string? mirror)
    {
        if (modes.Count != 1)
        {
            return "give exactly one of --fetch, --audit, --preflight, --write or --check";
        }

        var mode = modes[0];
        if (writePins && mode != ClusterMode.Fetch)
        {
            return "--write-pins is valid only with --fetch";
        }

        if (words is not null && mode != ClusterMode.Preflight)
        {
            return "--words is valid only with --preflight";
        }

        if (clusters is not null && mode != ClusterMode.Write)
        {
            return "--clusters is valid only with --write (--check uses the record's settings)";
        }

        if ((maxWords is not null || minCount is not null) && mode is not (ClusterMode.Write or ClusterMode.Preflight))
        {
            return "--max-words and --min-count are valid only with --write and --preflight (--check uses the record's settings)";
        }

        return mirror is not null && !ClusterSourceList.AllowedHosts.Contains(mirror)
            ? $"--mirror must be one of {string.Join(", ", ClusterSourceList.AllowedHosts.Order(StringComparer.Ordinal))}"
            : null;
    }

    /// <summary>
    ///     Resolves the folders of a run and refuses a cache that would put downloads into tracked files.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="root">The data folder.</param>
    /// <param name="currentDirectory">The current directory.</param>
    /// <returns>The paths.</returns>
    /// <exception cref="InvalidDataException">The cache is inside the repository but not inside the ignored external folder.</exception>
    private static ClusterPaths ResolvePaths(ClusterOptions options, string root, string currentDirectory)
    {
        var external = Path.Combine(root, "external");
        var cache = options.Cache is null ? Path.Combine(external, "cluster-corpus") : Path.GetFullPath(options.Cache, currentDirectory);
        var realRoot = LeakageChecker.ResolveRealPath(root);
        var realRepository = Path.GetFullPath(Path.Combine(realRoot, "..", ".."));
        var realCache = LeakageChecker.ResolveRealPath(cache);
        if (!LeakageChecker.IsLexicallyInside(realCache, Path.Combine(realRoot, "external")) && LeakageChecker.IsLexicallyInside(realCache, realRepository))
        {
            throw new InvalidDataException($"the cache folder must be inside {external} (git-ignored) or outside the repository; refusing {cache}");
        }

        var outDirectory = options.Out is null ? root : Path.GetFullPath(options.Out, currentDirectory);
        return new ClusterPaths(root, outDirectory, cache, Path.Combine(cache, "cluster-corpus.tar"));
    }

    /// <summary>
    ///     Runs <c>--fetch</c>.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="paths">The paths.</param>
    /// <param name="output">The output writer.</param>
    /// <param name="environment">The network and pause seams.</param>
    /// <returns>The exit code.</returns>
    private static int Fetch(ClusterOptions options, ClusterPaths paths, TextWriter output, ClusterEnvironment environment)
    {
        var sourcesPath = Path.Combine(paths.Data, ClusterSourceList.FileName);
        var sources = ClusterSourceList.Load(sourcesPath);
        var cache = new ClusterCorpusCache(paths.Cache, environment.Fetcher, environment.Sleeper, TimeSpan.FromMilliseconds(options.DelayMs), options.Mirror);
        var clock = Stopwatch.StartNew();
        var results = cache.Fetch(sources, output, CancellationToken.None);

        // Summarize, then fill the pins of rows that had none when asked to
        var failed = results.Count(static result => result.Outcome == "failed");
        var newPins = results.Where(static result => result.Outcome == "pinned" && result.Body is not null)
            .ToDictionary(static result => result.SourceId, static result => (result.Body!.Sha256, result.Body.Bytes.LongLength), StringComparer.Ordinal);
        output.WriteLine(
            $"cluster: fetch: {results.Count} used books: {results.Count(static result => result.Outcome == "cached")} cached, " +
            $"{results.Count(static result => result.Outcome == "fetched")} fetched, {newPins.Count} without a pin, {failed} failed; " +
            $"{cache.Requests} requests in {Duration(clock.Elapsed)}");
        if (newPins.Count > 0 && options.WritePins)
        {
            File.WriteAllBytes(sourcesPath, sources.WithPins(newPins));
            output.WriteLine($"cluster: fetch: wrote {newPins.Count} new pins to {ClusterSourceList.FileName}");
        }
        else if (newPins.Count > 0)
        {
            output.WriteLine($"cluster: fetch: {newPins.Count} books have no pin; run with --write-pins to record them");
        }

        return failed == 0 ? 0 : 1;
    }

    /// <summary>
    ///     Runs <c>--audit</c>.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="paths">The paths.</param>
    /// <param name="output">The output writer.</param>
    /// <param name="currentDirectory">The current directory.</param>
    /// <returns>The exit code.</returns>
    private static int Audit(ClusterOptions options, ClusterPaths paths, TextWriter output, string currentDirectory)
    {
        var (_, corpus) = ReadCorpus(options, paths, output, currentDirectory);
        var removals = RunAudit(paths, corpus, options.Threads, output);
        var bytes = CorpusLeakageAudit.Write(removals);
        var target = Path.Combine(paths.Data, CorpusLeakageAudit.FileName);
        if (options.WriteRemovals)
        {
            File.WriteAllBytes(target, bytes);
            output.WriteLine($"cluster: audit: wrote {removals.Count} removals to {CorpusLeakageAudit.FileName} (SHA-256 {GutenbergBody.Sha256Hex(bytes)})");
            return 0;
        }

        var exists = File.Exists(target);
        var current = exists && File.ReadAllBytes(target).AsSpan().SequenceEqual(bytes);
        var state = exists ? "stale" : "missing";
        output.WriteLine($"cluster: audit: {CorpusLeakageAudit.FileName} is {(current ? "current" : state)}");
        return current ? 0 : 1;
    }

    /// <summary>
    ///     Runs <c>--preflight</c>.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="paths">The paths.</param>
    /// <param name="output">The output writer.</param>
    /// <param name="currentDirectory">The current directory.</param>
    /// <returns>The exit code.</returns>
    private static int Preflight(ClusterOptions options, ClusterPaths paths, TextWriter output, string currentDirectory)
    {
        var (_, corpus) = ReadCorpus(options, paths, output, currentDirectory);
        var removalsPath = Path.Combine(paths.Data, CorpusLeakageAudit.FileName);
        var skipped = File.Exists(removalsPath) ? Skipped(CorpusLeakageAudit.Parse(File.ReadAllBytes(removalsPath))) : [];
        if (skipped.Count == 0)
        {
            output.WriteLine($"cluster: preflight: no {CorpusLeakageAudit.FileName}; counting the whole corpus");
        }

        var minCount = options.MinCount ?? DefaultMinCount;
        var counts = Count(corpus, skipped, new ReaderSettings(minCount, options.MaxWords ?? 0), options.Threads, output);
        output.WriteLine($"cluster: preflight: {counts.Tokens} tokens, {counts.Types} types, {counts.Words.Count} words with a count of at least {minCount}");
        foreach (var size in PreflightSizes)
        {
            var coverage = counts.CoveragePerTenThousand(size);
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"cluster: preflight: size {size}: token coverage {coverage / 100}.{coverage % 100:00}%"));
        }

        var rankOf = counts.Words.Select(static (word, rank) => (word, rank)).ToDictionary(static pair => pair.word, static pair => pair.rank, StringComparer.Ordinal);
        foreach (var word in options.Words ?? NominatedWords)
        {
            var count = counts.FormCounts.GetValueOrDefault(word);
            if (!rankOf.TryGetValue(word, out var rank))
            {
                output.WriteLine($"cluster: preflight: word {word}: count {count}, too rare");
                continue;
            }

            var cuts = string.Join(' ', PreflightSizes.Select(size => $"{size}:{(rank < size ? "in" : "out")}"));
            output.WriteLine($"cluster: preflight: word {word}: count {count}, rank {rank + 1}, {cuts}");
        }

        return 0;
    }

    /// <summary>
    ///     Runs <c>--write</c> or <c>--check</c>.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="paths">The paths.</param>
    /// <param name="output">The output writer.</param>
    /// <param name="currentDirectory">The current directory.</param>
    /// <returns>The exit code.</returns>
    private static int WriteOrCheck(ClusterOptions options, ClusterPaths paths, TextWriter output, string currentDirectory)
    {
        // The settings: given on the command line for --write, from the record for --check
        var tablePath = Path.Combine(paths.Out, WordClusterTable.FileName);
        var recordPath = Path.Combine(paths.Out, ClusteringRecord.FileName);
        ClusteringRecord? existing = File.Exists(recordPath) ? ClusteringRecord.Parse(File.ReadAllBytes(recordPath)) : null;
        if (options.Mode == ClusterMode.Check && existing is null)
        {
            throw new InvalidDataException($"there is no {ClusteringRecord.FileName} in {paths.Out} to check; run 'cluster --write' first");
        }

        var settings = options.Mode == ClusterMode.Check
            ? existing!.Settings with
            {
                ClustererVersion = ClusterInducer.ClustererVersion,
                ReaderVersion = ClusterCorpusReader.ReaderVersion,
                AuditVersion = CorpusLeakageAudit.AuditVersion,
            }
            : new ClusteringSettings(
                options.Clusters ?? DefaultClusters,
                options.MaxWords ?? 0,
                options.MinCount ?? DefaultMinCount,
                ClusterInducer.ClustererVersion,
                ClusterCorpusReader.ReaderVersion,
                CorpusLeakageAudit.AuditVersion);

        var total = Stopwatch.StartNew();
        var induction = Induce(options, paths, settings, existing, output, currentDirectory);
        if (induction is null)
        {
            return 1;
        }

        var recordBytes = induction.Record.Write();
        output.WriteLine($"cluster: table SHA-256 {induction.Record.Result.TableSha256}, {induction.Record.Result.Words} words, {induction.Record.Result.Paths} paths");
        output.WriteLine($"cluster: snapshot SHA-256 {induction.Snapshot.Sha256}, {induction.Snapshot.Bytes} bytes, {induction.Snapshot.Files} files");
        output.WriteLine($"cluster: total {Duration(total.Elapsed)} with {options.Threads} threads");
        if (options.Mode == ClusterMode.Write)
        {
            Directory.CreateDirectory(paths.Out);
            File.WriteAllBytes(tablePath, induction.Table);
            File.WriteAllBytes(recordPath, recordBytes);
            output.WriteLine($"cluster: wrote {WordClusterTable.FileName} and {ClusteringRecord.FileName} to {paths.Out}");
            return 0;
        }

        // --check: every product must equal the written one byte for byte, and the existing snapshot (the one read with
        // --from-snapshot, otherwise the one in the cache) must be the archive the inputs give, with the recorded hash
        var tableCurrent = File.Exists(tablePath) && File.ReadAllBytes(tablePath).AsSpan().SequenceEqual(induction.Table);
        var recordCurrent = File.ReadAllBytes(recordPath).AsSpan().SequenceEqual(recordBytes);
        var archive = options.FromSnapshot is not null ? Path.GetFullPath(options.FromSnapshot, currentDirectory) : paths.Snapshot;
        var snapshotState = SnapshotState(archive, induction.Snapshot.Sha256, existing!.Inputs.SnapshotSha256);
        output.WriteLine($"cluster: check: {WordClusterTable.FileName} {(tableCurrent ? "current" : "stale")}, {ClusteringRecord.FileName} {(recordCurrent ? "current" : "stale")}, snapshot {snapshotState}");
        return tableCurrent && recordCurrent && snapshotState == "current" ? 0 : 1;
    }

    /// <summary>
    ///     Compares an existing snapshot archive with the one the inputs give, without changing it.
    /// </summary>
    /// <remarks>
    ///     The check exists to detect a snapshot that is missing, stale or tampered with, so it hashes the archive on
    ///     disk instead of rebuilding it; only reading, no side effects.
    /// </remarks>
    /// <param name="archive">The existing archive.</param>
    /// <param name="measured">The SHA-256 of the archive the current inputs give.</param>
    /// <param name="recorded">The SHA-256 named by the committed record.</param>
    /// <returns><c>missing</c> when there is no archive, <c>current</c> when the archive, the measured and the recorded hashes all agree, otherwise <c>stale</c>.</returns>
    private static string SnapshotState(string archive, string measured, string recorded)
    {
        if (!File.Exists(archive))
        {
            return "missing";
        }

        var current = string.Equals(recorded, measured, StringComparison.Ordinal)
            && string.Equals(CorpusSnapshot.FileSha256(archive), measured, StringComparison.Ordinal);
        return current ? "current" : "stale";
    }

    /// <summary>
    ///     Verifies the inputs, audits, counts, induces and builds the table, the snapshot and the record.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="paths">The paths.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="existing">The record already in the output folder, or null.</param>
    /// <param name="output">The output writer.</param>
    /// <param name="currentDirectory">The current directory.</param>
    /// <returns>The products, or null when the committed removals are stale.</returns>
    private static Induction? Induce(ClusterOptions options, ClusterPaths paths, ClusteringSettings settings, ClusteringRecord? existing, TextWriter output, string currentDirectory)
    {
        var (sources, corpus) = ReadCorpus(options, paths, output, currentDirectory);

        // The committed removals must be exactly what the audit computes now
        var removals = RunAudit(paths, corpus, options.Threads, output);
        var removalsBytes = CorpusLeakageAudit.Write(removals);
        var removalsPath = Path.Combine(paths.Data, CorpusLeakageAudit.FileName);
        if (!File.Exists(removalsPath) || !File.ReadAllBytes(removalsPath).AsSpan().SequenceEqual(removalsBytes))
        {
            output.WriteLine($"cluster: {CorpusLeakageAudit.FileName} is {(File.Exists(removalsPath) ? "stale" : "missing")}; review and write it with 'cluster --audit --write' first");
            return null;
        }

        var counts = Count(corpus, Skipped(removals), new ReaderSettings(settings.MinCount, settings.MaxWords), options.Threads, output);
        if (counts.Words.Count < 2)
        {
            throw new InvalidDataException("the vocabulary has fewer than two words");
        }

        // Induce with progress and an estimate of the time left
        var clock = Stopwatch.StartNew();
        var initial = Math.Min(settings.Clusters, counts.Words.Count);
        var induced = ClusterInducer.Induce(
            counts.Bigrams,
            new InducerOptions(settings.Clusters, options.Threads, progress => ReportProgress(progress, initial, clock.Elapsed, output)));
        output.WriteLine($"cluster: induced {induced.Clusters} clusters over {counts.Words.Count} words in {Duration(clock.Elapsed)}");

        // The table, the snapshot of the corpus and the record
        var entries = WordClusterTable.Build(counts.Words, induced.Paths, counts.Counts, out var collisions, out var ineligible);
        var table = WordClusterTable.Write(entries);
        output.WriteLine($"cluster: table: {entries.Count} words; {collisions} dropped for a shared lookup hash, {ineligible} ineligible");
        var bodies = sources.UsedInCorpusOrder.Zip(corpus.Bodies, static (source, body) => (source.Id, System.Text.Encoding.UTF8.GetBytes(body))).ToList();

        // --check never writes the snapshot: it only measures the archive the inputs give, so an existing archive that
        // is missing, stale or tampered with stays as it is and is reported rather than silently repaired
        var snapshot = options.Mode == ClusterMode.Check
            ? CorpusSnapshot.Measure(sources.Bytes, bodies)
            : CorpusSnapshot.Build(paths.Snapshot, sources.Bytes, bodies);
        var snapshotUrl = existing is not null && string.Equals(existing.Inputs.SnapshotSha256, snapshot.Sha256, StringComparison.Ordinal)
            ? existing.Inputs.SnapshotUrl
            : string.Empty;
        var record = new ClusteringRecord(
            ClusteringRecord.CurrentSchema,
            settings,
            new ClusteringInputs(
                sources.Sha256,
                sources.Sources.Count(static source => source.IsUsed),
                sources.Sources.Count(static source => !source.IsUsed),
                GutenbergBody.Sha256Hex(removalsBytes),
                CorpusLeakageAudit.EvaluationListingSha256(paths.Data),
                TrainingCorpus.TaggerVersion,
                snapshot.Sha256,
                snapshot.Bytes,
                snapshot.Files,
                snapshotUrl),
            new ClusteringCorpus(
                counts.Paragraphs,
                counts.Sentences,
                counts.Tokens,
                counts.Types,
                counts.Bigrams.PairTypes,
                counts.RemovedSentences,
                counts.RemovedTokens,
                removals.Select(static removal => (removal.EvalFile, removal.EvalId)).Distinct().Count(),
                removals.Select(static removal => removal.SourceId).Distinct(StringComparer.Ordinal).Count()),
            new ClusteringResult(
                GutenbergBody.Sha256Hex(table),
                entries.Count,
                entries.Select(static entry => entry.Path).Distinct(StringComparer.Ordinal).Count(),
                collisions,
                ineligible,
                counts.CoveragePerTenThousand(counts.Words.Count)));
        return new Induction(table, record, snapshot);
    }

    /// <summary>
    ///     Loads the source list and the verified bodies, and tokenizes them.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="paths">The paths.</param>
    /// <param name="output">The output writer.</param>
    /// <param name="currentDirectory">The current directory.</param>
    /// <returns>The source list and the corpus.</returns>
    private static (ClusterSourceList Sources, CorpusRead Corpus) ReadCorpus(ClusterOptions options, ClusterPaths paths, TextWriter output, string currentDirectory)
    {
        var sources = ClusterSourceList.Load(Path.Combine(paths.Data, ClusterSourceList.FileName));
        var bodies = options.FromSnapshot is not null
            ? ClusterCorpusCache.ReadBodiesFromSnapshot(sources, Path.GetFullPath(options.FromSnapshot, currentDirectory))
            : new ClusterCorpusCache(paths.Cache).ReadBodies(sources);
        if (bodies.Count == 0)
        {
            throw new InvalidDataException($"{ClusterSourceList.FileName} lists no used book");
        }

        var clock = Stopwatch.StartNew();
        var tokenized = ClusterCorpusReader.Tokenize([.. bodies.Select(static body => (body.Source.Id, body.Text))], options.Threads, CancellationToken.None);
        var tokens = tokenized.Tokens;
        var seconds = Math.Max(clock.Elapsed.TotalSeconds, 0.001);
        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"cluster: tokenized {bodies.Count} books, {tokens} tokens in {Duration(clock.Elapsed)} ({tokens / seconds:F0} tokens/s with {options.Threads} threads)"));
        return (sources, new CorpusRead(tokenized, [.. bodies.Select(static body => body.Text)]));
    }

    /// <summary>
    ///     Runs the audit and prints its counts.
    /// </summary>
    /// <param name="paths">The paths.</param>
    /// <param name="corpus">The corpus.</param>
    /// <param name="threads">The worker threads.</param>
    /// <param name="output">The output writer.</param>
    /// <returns>The removals.</returns>
    private static IReadOnlyList<CorpusRemoval> RunAudit(ClusterPaths paths, CorpusRead corpus, int threads, TextWriter output)
    {
        var clock = Stopwatch.StartNew();
        var evaluation = CorpusLeakageAudit.LoadEvaluationSentences(paths.Data);
        var removals = CorpusLeakageAudit.Run(corpus.Tokenized, evaluation, threads, CancellationToken.None);
        foreach (var count in CorpusLeakageAudit.Counts(removals))
        {
            output.WriteLine($"cluster: audit: {count.EvalFile} {count.Rule}: {count.RemovedSentences} corpus sentences, {count.MatchedEvaluationSentences} evaluation sentences, {count.BooksAffected} books");
        }

        output.WriteLine($"cluster: audit: {removals.Count} sentences removed from {removals.Select(static removal => removal.SourceId).Distinct(StringComparer.Ordinal).Count()} books, {evaluation.Count} evaluation sentences compared in {Duration(clock.Elapsed)}");
        return removals;
    }

    /// <summary>
    ///     Counts the corpus and prints the time it took.
    /// </summary>
    /// <param name="corpus">The corpus.</param>
    /// <param name="skipped">The sentences to skip.</param>
    /// <param name="settings">The reader settings.</param>
    /// <param name="threads">The worker threads.</param>
    /// <param name="output">The output writer.</param>
    /// <returns>The counts.</returns>
    private static CorpusCounts Count(CorpusRead corpus, IReadOnlyList<SkippedSentence> skipped, ReaderSettings settings, int threads, TextWriter output)
    {
        var clock = Stopwatch.StartNew();
        var counts = ClusterCorpusReader.Count(corpus.Tokenized, skipped, settings, threads, CancellationToken.None);
        output.WriteLine($"cluster: counted {counts.Tokens} tokens ({counts.RemovedSentences} removed sentences, {counts.RemovedTokens} tokens), {counts.Words.Count} words, {counts.Bigrams.PairTypes} bigram types in {Duration(clock.Elapsed)}");
        return counts;
    }

    /// <summary>
    ///     Converts removals to the sentences the reader skips.
    /// </summary>
    /// <param name="removals">The removals.</param>
    /// <returns>The skipped sentences.</returns>
    private static IReadOnlyList<SkippedSentence> Skipped(IReadOnlyList<CorpusRemoval> removals) =>
        [.. removals.Select(static removal => new SkippedSentence(removal.SourceId, removal.Paragraph, removal.Sentence, removal.TextSha256))];

    /// <summary>
    ///     Prints one progress line of the induction with the time left.
    /// </summary>
    /// <param name="progress">The progress.</param>
    /// <param name="initial">The number of words placed before the first merge.</param>
    /// <param name="elapsed">The time since the induction started.</param>
    /// <param name="output">The output writer.</param>
    private static void ReportProgress(InducerProgress progress, int initial, TimeSpan elapsed, TextWriter output)
    {
        if (progress.WordsAdded < progress.Words || progress.FinalMerges == 0)
        {
            // The cost per word is about constant after the initial clusters, so the rate so far predicts the rest
            var done = progress.WordsAdded - initial;
            var left = progress.Words - progress.WordsAdded;
            var eta = done > 0 ? TimeSpan.FromTicks(elapsed.Ticks / done * left) : TimeSpan.Zero;
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"cluster: induce: {progress.WordsAdded}/{progress.Words} words ({100.0 * progress.WordsAdded / progress.Words:F1}%), elapsed {Duration(elapsed)}, left about {Duration(eta)}"));
            return;
        }

        output.WriteLine($"cluster: induce: final hierarchy {progress.FinalMerges}/{progress.FinalMergesTotal} merges, elapsed {Duration(elapsed)}");
    }

    /// <summary>
    ///     Formats a duration as hours, minutes and seconds.
    /// </summary>
    /// <param name="duration">The duration.</param>
    /// <returns>The text <c>h:mm:ss.f</c>.</returns>
    private static string Duration(TimeSpan duration) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}.{duration.Milliseconds / 100}");

    /// <summary>
    ///     Writes a usage error.
    /// </summary>
    /// <param name="error">The error writer.</param>
    /// <param name="message">The problem.</param>
    /// <returns>The exit code 2.</returns>
    private static int UsageError(TextWriter error, string message)
    {
        error.WriteLine($"cluster: {message}");
        error.WriteLine("Run 'cluster --help' for usage.");
        return 2;
    }

    /// <summary>The corpus of a run: the tokenized books and their body texts, in the same order.</summary>
    /// <param name="Tokenized">The tokenized corpus.</param>
    /// <param name="Bodies">The body texts.</param>
    private sealed record CorpusRead(TokenizedCorpus Tokenized, IReadOnlyList<string> Bodies);
}
