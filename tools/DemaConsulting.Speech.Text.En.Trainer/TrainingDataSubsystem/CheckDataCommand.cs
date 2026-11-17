using System.Globalization;

namespace DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

/// <summary>
///     The parsed command line of <c>check-data</c>.
/// </summary>
/// <param name="Data">The <c>--data</c> folder, or <see langword="null"/> to discover it.</param>
/// <param name="EvalSets">The <c>--eval-sets</c> folder, or <see langword="null"/> when not given.</param>
/// <param name="LeakageReport">The <c>--leakage-report</c> file to write, or <see langword="null"/>.</param>
/// <param name="MaxFindings">The number of findings printed per check.</param>
/// <param name="ShowCounts">Whether to print the per-reading count table.</param>
/// <param name="ShowHelp">Whether usage was requested.</param>
internal sealed record CheckDataOptions(
    string? Data,
    string? EvalSets,
    string? LeakageReport,
    int MaxFindings,
    bool ShowCounts,
    bool ShowHelp);

/// <summary>
///     The <c>check-data</c> command: verifies the Text.En training data folder and reports every violation.
/// </summary>
/// <remarks>
///     The checks run in a fixed order so the output is deterministic: manifest, dataset load (file formats),
///     schema, license, internal leakage, optional external leakage, summary. Every check runs even when an earlier
///     one found problems, so one run lists everything. The exit code is <c>0</c> when no check found a violation,
///     <c>1</c> when at least one did, and <c>2</c> for a usage or I/O error (bad option, missing folder or malformed
///     path, an evaluation-set folder that is missing or empty, an evaluation file with no sentences, an unreadable
///     file). The command is a pure function of its
///     arguments, writers, current directory and environment accessor, so tests can run it in memory.
/// </remarks>
internal static class CheckDataCommand
{
    /// <summary>The name of the command as typed on the command line.</summary>
    public const string Name = "check-data";

    /// <summary>The environment variable that supplies the evaluation-set folder when <c>--eval-sets</c> is absent.</summary>
    public const string EvalSetsVariable = "TEXTEN_EVAL_SETS";

    /// <summary>The usage text.</summary>
    public const string Usage = """
        Usage: check-data [options]

        Verifies the Text.En training data (manifest, file format, schema, strict whole-word spans,
        licenses, split separation) and optionally its separation from evaluation sets.

        Options:
          --data <dir>            The data folder (default: data/text-en, searched upward from the current
                                  directory and then from the tool's folder).
          --eval-sets <dir>       A folder of local evaluation sets (*.txt: one sentence per line; *.tsv: a header
                                  row and a 'sentence' or 'text' column). Enables the external leakage check.
                                  Default: the folder named by the TEXTEN_EVAL_SETS environment variable.
          --leakage-report <file> Writes the per-set, per-split leakage counts (requires
                                  evaluation sets supplied via --eval-sets or TEXTEN_EVAL_SETS).
          --max-findings <n>      Findings printed per check (default 25); the exit code counts all of them.
          --counts                Prints the per-reading row counts.
          --help                  Shows this text.

        Exit codes: 0 = no findings, 1 = findings, 2 = usage or I/O error.
        """;

    /// <summary>
    ///     Runs the command.
    /// </summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="output">Receives progress and the report.</param>
    /// <param name="error">Receives usage and I/O errors.</param>
    /// <param name="currentDirectory">The directory relative paths are resolved against.</param>
    /// <param name="baseDirectory">The tool's own folder, the second place the default data folder is searched from.</param>
    /// <param name="getEnvironmentVariable">Reads an environment variable (null when unset).</param>
    /// <returns>The exit code: 0, 1 or 2.</returns>
    public static int Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        string currentDirectory,
        string baseDirectory,
        Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        // Parse first: a bad command line is a usage error before any file is touched
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

        // Every path is resolved inside the guard: a malformed --data or --eval-sets path (an invalid character, a bad
        // volume syntax, a name that is too long) is an operator error and ends with the usage exit code
        try
        {
            var root = ResolveRoot(options, currentDirectory, baseDirectory);
            if (root is null || !Directory.Exists(root))
            {
                return UsageError(error, $"data folder '{root ?? "data/text-en"}' was not found; use --data <dir>");
            }

            var evalSets = options.EvalSets ?? EmptyToNull(getEnvironmentVariable(EvalSetsVariable));
            var evalPath = evalSets is null ? null : Path.GetFullPath(evalSets, currentDirectory);
            var reportPath = options.LeakageReport is null ? null : Path.GetFullPath(options.LeakageReport, currentDirectory);
            if (reportPath is not null && (evalPath is null || LeakageChecker.IsInside(reportPath, root)))
            {
                return UsageError(error, "--leakage-report needs evaluation sets and must be written outside the data folder");
            }

            return RunChecks(options, root, evalPath, reportPath, output);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException)
        {
            return UsageError(error, exception.Message);
        }
    }

    /// <summary>
    ///     Parses the arguments.
    /// </summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="parseError">The problem when parsing failed.</param>
    /// <returns>The options, or <see langword="null"/> when the arguments are invalid.</returns>
    internal static CheckDataOptions? TryParse(IReadOnlyList<string> args, out string? parseError)
    {
        parseError = null;
        string? data = null;
        string? evalSets = null;
        string? leakageReport = null;
        var maxFindings = CheckReport.DefaultMaxFindingsPerCheck;
        var showCounts = false;
        var showHelp = false;

        var position = 0;
        while (position < args.Count)
        {
            // Accept both '--name value' and '--name=value'
            var token = args[position++];
            var separator = token.StartsWith("--", StringComparison.Ordinal) ? token.IndexOf('=') : -1;
            var name = separator > 0 ? token[..separator] : token;
            string? inline = separator > 0 ? token[(separator + 1)..] : null;

            switch (name)
            {
                case "--help" or "-h" or "-?":
                    showHelp = true;
                    break;
                case "--counts":
                    showCounts = true;
                    break;
                case "--data" or "--eval-sets" or "--leakage-report" or "--max-findings":
                    var value = inline ?? (position < args.Count ? args[position++] : null);
                    if (string.IsNullOrEmpty(value))
                    {
                        parseError = $"option {name} needs a value";
                        return null;
                    }

                    if (name == "--data")
                    {
                        data = value;
                    }
                    else if (name == "--eval-sets")
                    {
                        evalSets = value;
                    }
                    else if (name == "--leakage-report")
                    {
                        leakageReport = value;
                    }
                    else if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out maxFindings) || maxFindings < 1)
                    {
                        parseError = "option --max-findings needs a positive integer";
                        return null;
                    }

                    break;
                default:
                    parseError = $"unknown option '{token}'";
                    return null;
            }
        }

        return new CheckDataOptions(data, evalSets, leakageReport, maxFindings, showCounts, showHelp);
    }

    /// <summary>
    ///     Finds <c>data/text-en</c> by walking up from a directory.
    /// </summary>
    /// <param name="startDirectory">The directory to start from.</param>
    /// <returns>The data folder, or <see langword="null"/> when no ancestor holds one.</returns>
    internal static string? FindDataRoot(string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "data", "text-en");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    ///     Resolves the data folder from the option or by discovery.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="currentDirectory">The current directory.</param>
    /// <param name="baseDirectory">The tool's folder.</param>
    /// <returns>The full path, or <see langword="null"/> when none was found.</returns>
    private static string? ResolveRoot(CheckDataOptions options, string currentDirectory, string baseDirectory) =>
        options.Data is not null
            ? Path.GetFullPath(options.Data, currentDirectory)
            : FindDataRoot(currentDirectory) ?? FindDataRoot(baseDirectory);

    /// <summary>
    ///     Runs the checks in order and prints the report.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="root">The data folder.</param>
    /// <param name="evalPath">The evaluation-set folder, or <see langword="null"/>.</param>
    /// <param name="reportPath">The leakage report file, or <see langword="null"/>.</param>
    /// <param name="output">The output writer.</param>
    /// <returns>The exit code, 0 or 1.</returns>
    private static int RunChecks(CheckDataOptions options, string root, string? evalPath, string? reportPath, TextWriter output)
    {
        var report = new CheckReport(options.MaxFindings);
        report.AddInfo($"check-data: data folder {root}");

        // Manifest first (hashes of the raw bytes), then the typed load, then the semantic checks
        DataManifest.Verify(root, report);
        var data = TextEnDataSet.Load(root, report);
        var counts = new SchemaChecker(data, report).Run();
        new LicenseChecker(root, data, report).Run();

        var leakage = new LeakageChecker(data, report);
        leakage.CheckInternal();
        RunExternalLeakage(leakage, report, root, evalPath, reportPath);

        report.WriteTo(output);
        if (options.ShowCounts)
        {
            counts.WriteTable(output);
        }

        return report.ExitCode;
    }

    /// <summary>
    ///     Runs the optional external leakage check, or notes that it was skipped.
    /// </summary>
    /// <param name="leakage">The leakage checker.</param>
    /// <param name="report">The report.</param>
    /// <param name="root">The data folder.</param>
    /// <param name="evalPath">The evaluation-set folder, or <see langword="null"/>.</param>
    /// <param name="reportPath">The leakage report file, or <see langword="null"/>.</param>
    private static void RunExternalLeakage(LeakageChecker leakage, CheckReport report, string root, string? evalPath, string? reportPath)
    {
        if (evalPath is null)
        {
            report.AddInfo("external leakage check skipped (no evaluation sets supplied)");
            return;
        }

        // Evaluation data is never committed: a folder inside the data root is itself a violation
        if (LeakageChecker.IsInside(evalPath, root))
        {
            report.Add("leakage", evalPath, "the evaluation-set folder is inside the data folder (evaluation data must never be committed)");
            return;
        }

        var rows = leakage.CheckExternal(LeakageChecker.LoadEvaluationSets(evalPath));
        if (reportPath is not null)
        {
            LeakageChecker.WriteReport(reportPath, rows);
            report.AddInfo($"leakage report written to {reportPath}");
        }
    }

    /// <summary>
    ///     Writes a usage error.
    /// </summary>
    /// <param name="error">The error writer.</param>
    /// <param name="message">The message.</param>
    /// <returns>The usage-error exit code, 2.</returns>
    private static int UsageError(TextWriter error, string message)
    {
        error.WriteLine($"check-data: error: {message}");
        error.WriteLine("Run 'check-data --help' for usage.");
        return 2;
    }

    /// <summary>
    ///     Converts an empty string to <see langword="null"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The value, or <see langword="null"/> when it is null or empty.</returns>
    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
