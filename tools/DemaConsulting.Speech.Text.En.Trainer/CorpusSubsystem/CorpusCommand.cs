using System.Diagnostics;
using System.Globalization;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.CorpusSubsystem;

/// <summary>The seams of the <c>corpus</c> command that tests replace: the network and the pause between requests.</summary>
/// <param name="Fetcher">Opens a URL instead of the network, or null for the HTTPS mirror download.</param>
/// <param name="Sleeper">Pauses instead of the real pause, or null for the real one.</param>
internal sealed record CorpusEnvironment(Func<Uri, CancellationToken, Stream>? Fetcher = null, Action<TimeSpan>? Sleeper = null);

/// <summary>
///     The <c>corpus</c> command: fetches and pins the public-domain corpus and audits it against the evaluation sets.
/// </summary>
/// <remarks>
///     <para>
///     The command is a manual developer tool: only its unit and subsystem tests run in CI, offline. Exactly one mode is
///     given per run. <c>--fetch</c> is the only mode that uses the network; a book whose mirror text no longer matches
///     its pin fails the run. <c>--audit</c> reads only pin-verified bodies and compares the removals it computes with the
///     committed <c>cluster-removals.tsv</c> byte for byte, writing them only with <c>--write</c>, so the removals are
///     always reviewed before they change. Console output holds counts, ids, hashes and timings, never corpus text. Exit
///     codes: <c>0</c> done or current, <c>1</c> stale removals or a failed book, <c>2</c> usage, I/O or data error.
///     </para>
/// </remarks>
internal static class CorpusCommand
{
    /// <summary>The name of the command as typed on the command line.</summary>
    public const string Name = "corpus";

    /// <summary>The default polite delay between two requests to the mirror, in milliseconds.</summary>
    public const int DefaultDelayMs = 2000;

    /// <summary>The smallest delay between two requests accepted, in milliseconds.</summary>
    public const int MinimumDelayMs = 1000;

    /// <summary>The usage text.</summary>
    public const string Usage = """
        Usage: corpus <mode> [options]

        Fetches and audits the pinned public-domain corpus listed in cluster-sources.tsv.
        Manual tool: CI runs only its tests. Exactly one mode per run.

        Modes:
          --fetch [--write-pins]  Fetch the plain text and metadata of every used book that is missing from the cache or
                                  fails its pin, from an official mirror only, one request at a time with --delay-ms
                                  between requests. --write-pins fills the body pins of rows that have none.
          --audit [--write]       Compare the corpus with every evaluation sentence and print the removals per file and
                                  rule; --write writes cluster-removals.tsv, otherwise exit 1 when it is not current.

        Options:
          --threads <n>       Worker threads (default: the processor count). The output does not depend on it.
          --delay-ms <n>      The pause between two mirror requests (default 2000, at least 1000).
          --mirror <host>     Fetch from this allowed mirror instead of the listed one (gutenberg.pglaf.org or
                              aleph.gutenberg.org).
          --cache <dir>       The corpus cache (default: <data>/external/cluster-corpus); it must be inside
                              <data>/external (git-ignored) or outside the repository.
          --data <dir>        The data folder (default: data/text-en, searched upward from the current directory).
          --help              Shows this text.

        Exit codes: 0 = done or current, 1 = stale or a book failed, 2 = usage, I/O or data error.
        """;

    /// <summary>The mode of a run.</summary>
    internal enum CorpusMode
    {
        /// <summary>Fetch and pin the corpus.</summary>
        Fetch,

        /// <summary>Run the leakage audit.</summary>
        Audit,
    }

    /// <summary>
    ///     The parsed command line.
    /// </summary>
    /// <param name="Mode">The mode.</param>
    /// <param name="WritePins">Whether <c>--fetch</c> fills missing pins.</param>
    /// <param name="WriteRemovals">Whether <c>--audit</c> writes the removals.</param>
    /// <param name="Threads">The worker threads.</param>
    /// <param name="DelayMs">The delay between requests.</param>
    /// <param name="Mirror">The mirror host, or null.</param>
    /// <param name="Cache">The cache folder, or null for the default.</param>
    /// <param name="Data">The data folder, or null to discover it.</param>
    /// <param name="ShowHelp">Whether usage was requested.</param>
    internal sealed record CorpusOptions(
        CorpusMode Mode,
        bool WritePins,
        bool WriteRemovals,
        int Threads,
        int DelayMs,
        string? Mirror,
        string? Cache,
        string? Data,
        bool ShowHelp);

    /// <summary>The resolved folders of a run.</summary>
    /// <param name="Data">The data folder.</param>
    /// <param name="Cache">The cache folder.</param>
    private sealed record CorpusPaths(string Data, string Cache);

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
        CorpusEnvironment? environment = null)
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
            environment ??= new CorpusEnvironment();
            return options.Mode == CorpusMode.Fetch
                ? Fetch(options, paths, output, environment)
                : Audit(options, paths, output);
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
    internal static CorpusOptions? TryParse(IReadOnlyList<string> args, out string? parseError)
    {
        ArgumentNullException.ThrowIfNull(args);
        parseError = null;
        var modes = new List<CorpusMode>();
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
                    modes.Add(CorpusMode.Fetch);
                    break;
                case "--audit":
                    modes.Add(CorpusMode.Audit);
                    break;
                case "--write":
                    write = true;
                    break;
                case "--write-pins":
                    writePins = true;
                    break;
                case "--threads" or "--delay-ms" or "--mirror" or "--cache" or "--data":
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
            return new CorpusOptions(CorpusMode.Fetch, false, false, 1, DefaultDelayMs, null, null, null, true);
        }

        // The numeric options, each with its smallest valid value
        var threads = Number(values, "--threads", 1, ref parseError);
        var delay = Number(values, "--delay-ms", MinimumDelayMs, ref parseError);
        var mirror = values.GetValueOrDefault("--mirror");
        parseError ??= Validate(modes, writePins, write, mirror);
        if (parseError is not null)
        {
            return null;
        }

        return new CorpusOptions(
            modes[0],
            writePins,
            write,
            threads ?? Environment.ProcessorCount,
            delay ?? DefaultDelayMs,
            mirror,
            values.GetValueOrDefault("--cache"),
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
    /// <param name="write">Whether <c>--write</c> was given.</param>
    /// <param name="mirror">The mirror host.</param>
    /// <returns>The problem, or null.</returns>
    private static string? Validate(List<CorpusMode> modes, bool writePins, bool write, string? mirror)
    {
        if (modes.Count != 1)
        {
            return "give exactly one of --fetch or --audit";
        }

        var mode = modes[0];
        if (writePins && mode != CorpusMode.Fetch)
        {
            return "--write-pins is valid only with --fetch";
        }

        if (write && mode != CorpusMode.Audit)
        {
            return "--write is valid only with --audit";
        }

        return mirror is not null && !CorpusSourceList.AllowedHosts.Contains(mirror)
            ? $"--mirror must be one of {string.Join(", ", CorpusSourceList.AllowedHosts.Order(StringComparer.Ordinal))}"
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
    private static CorpusPaths ResolvePaths(CorpusOptions options, string root, string currentDirectory)
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

        return new CorpusPaths(root, cache);
    }

    /// <summary>
    ///     Runs <c>--fetch</c>.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="paths">The paths.</param>
    /// <param name="output">The output writer.</param>
    /// <param name="environment">The network and pause seams.</param>
    /// <returns>The exit code.</returns>
    private static int Fetch(CorpusOptions options, CorpusPaths paths, TextWriter output, CorpusEnvironment environment)
    {
        var sourcesPath = Path.Combine(paths.Data, CorpusSourceList.FileName);
        var sources = CorpusSourceList.Load(sourcesPath);
        var cache = new CorpusCache(paths.Cache, environment.Fetcher, environment.Sleeper, TimeSpan.FromMilliseconds(options.DelayMs), options.Mirror);
        var clock = Stopwatch.StartNew();
        var results = cache.Fetch(sources, output, CancellationToken.None);

        // Summarize, then fill the pins of rows that had none when asked to
        var failed = results.Count(static result => result.Outcome == "failed");
        var newPins = results.Where(static result => result.Outcome == "pinned" && result.Body is not null)
            .ToDictionary(static result => result.SourceId, static result => (result.Body!.Sha256, result.Body.Bytes.LongLength), StringComparer.Ordinal);
        output.WriteLine(
            $"corpus: fetch: {results.Count} used books: {results.Count(static result => result.Outcome == "cached")} cached, " +
            $"{results.Count(static result => result.Outcome == "fetched")} fetched, {newPins.Count} without a pin, {failed} failed; " +
            $"{cache.Requests} requests in {Duration(clock.Elapsed)}");
        if (newPins.Count > 0 && options.WritePins)
        {
            File.WriteAllBytes(sourcesPath, sources.WithPins(newPins));
            output.WriteLine($"corpus: fetch: wrote {newPins.Count} new pins to {CorpusSourceList.FileName}");
        }
        else if (newPins.Count > 0)
        {
            output.WriteLine($"corpus: fetch: {newPins.Count} books have no pin; run with --write-pins to record them");
        }

        return failed == 0 ? 0 : 1;
    }

    /// <summary>
    ///     Runs <c>--audit</c>.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="paths">The paths.</param>
    /// <param name="output">The output writer.</param>
    /// <returns>The exit code.</returns>
    private static int Audit(CorpusOptions options, CorpusPaths paths, TextWriter output)
    {
        var corpus = ReadCorpus(options, paths, output);
        var removals = RunAudit(paths, corpus, options.Threads, output);
        var bytes = CorpusLeakageAudit.Write(removals);
        var target = Path.Combine(paths.Data, CorpusLeakageAudit.FileName);
        if (options.WriteRemovals)
        {
            File.WriteAllBytes(target, bytes);
            output.WriteLine($"corpus: audit: wrote {removals.Count} removals to {CorpusLeakageAudit.FileName} (SHA-256 {GutenbergBody.Sha256Hex(bytes)})");
            return 0;
        }

        var exists = File.Exists(target);
        var current = exists && File.ReadAllBytes(target).AsSpan().SequenceEqual(bytes);
        var state = exists ? "stale" : "missing";
        output.WriteLine($"corpus: audit: {CorpusLeakageAudit.FileName} is {(current ? "current" : state)}");
        return current ? 0 : 1;
    }

    /// <summary>
    ///     Loads the source list and the verified bodies, and tokenizes them.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="paths">The paths.</param>
    /// <param name="output">The output writer.</param>
    /// <returns>The tokenized corpus.</returns>
    /// <exception cref="InvalidDataException">The source list lists no used book, or a body is missing, unpinned or changed.</exception>
    private static TokenizedCorpus ReadCorpus(CorpusOptions options, CorpusPaths paths, TextWriter output)
    {
        var sources = CorpusSourceList.Load(Path.Combine(paths.Data, CorpusSourceList.FileName));
        var bodies = new CorpusCache(paths.Cache).ReadBodies(sources);
        if (bodies.Count == 0)
        {
            throw new InvalidDataException($"{CorpusSourceList.FileName} lists no used book");
        }

        var clock = Stopwatch.StartNew();
        var tokenized = CorpusReader.Tokenize([.. bodies.Select(static body => (body.Source.Id, body.Text))], options.Threads, CancellationToken.None);
        var tokens = tokenized.Tokens;
        var seconds = Math.Max(clock.Elapsed.TotalSeconds, 0.001);
        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"corpus: tokenized {bodies.Count} books, {tokens} tokens in {Duration(clock.Elapsed)} ({tokens / seconds:F0} tokens/s with {options.Threads} threads)"));
        return tokenized;
    }

    /// <summary>
    ///     Runs the audit and prints its counts.
    /// </summary>
    /// <param name="paths">The paths.</param>
    /// <param name="corpus">The tokenized corpus.</param>
    /// <param name="threads">The worker threads.</param>
    /// <param name="output">The output writer.</param>
    /// <returns>The removals.</returns>
    private static IReadOnlyList<CorpusRemoval> RunAudit(CorpusPaths paths, TokenizedCorpus corpus, int threads, TextWriter output)
    {
        var clock = Stopwatch.StartNew();
        var evaluation = CorpusLeakageAudit.LoadEvaluationSentences(paths.Data);
        var removals = CorpusLeakageAudit.Run(corpus, evaluation, threads, CancellationToken.None);
        foreach (var count in CorpusLeakageAudit.Counts(removals))
        {
            output.WriteLine($"corpus: audit: {count.EvalFile} {count.Rule}: {count.RemovedSentences} corpus sentences, {count.MatchedEvaluationSentences} evaluation sentences, {count.BooksAffected} books");
        }

        output.WriteLine($"corpus: audit: {removals.Count} sentences removed from {removals.Select(static removal => removal.SourceId).Distinct(StringComparer.Ordinal).Count()} books, {evaluation.Count} evaluation sentences compared in {Duration(clock.Elapsed)}");
        return removals;
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
        error.WriteLine($"corpus: {message}");
        error.WriteLine("Run 'corpus --help' for usage.");
        return 2;
    }
}
