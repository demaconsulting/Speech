namespace DemaConsulting.Speech.ModelBundler;

/// <summary>The tool's commands.</summary>
internal enum BundlerCommand
{
    /// <summary>Build bundle zips, <c>SHA256SUMS</c> and <c>release-notes.md</c>.</summary>
    Build,

    /// <summary>Rebuild and byte-compare a previous build.</summary>
    Verify,

    /// <summary>Upload a build to the <c>models</c> release without replacing any asset.</summary>
    Upload,
}

/// <summary>
///     The parsed command line:
///     <c>build (--all | --bundle &lt;name&gt;...) --output &lt;dir&gt; [--source-store &lt;root&gt;] [--cache &lt;dir&gt;] [--skip-unavailable]</c>,
///     <c>verify --input &lt;dir&gt; [--source-store &lt;root&gt;] [--cache &lt;dir&gt;]</c> or
///     <c>upload --input &lt;dir&gt; [--repo &lt;owner/name&gt;] [--dry-run]</c>.
/// </summary>
/// <param name="Command">The command.</param>
/// <param name="All">Whether <c>--all</c> selects every declared bundle (build).</param>
/// <param name="Bundles">The bundle names selected with <c>--bundle</c> (build), in order.</param>
/// <param name="Output">The output directory (build).</param>
/// <param name="Input">The input directory (verify, upload).</param>
/// <param name="SourceStore">The model store root searched first, or <see langword="null"/> (build, verify).</param>
/// <param name="Cache">The SHA-keyed cache directory (build, verify).</param>
/// <param name="SkipUnavailable">Whether an unavailable bundle is skipped instead of failing (build).</param>
/// <param name="Repository">The <c>owner/name</c> repository (upload).</param>
/// <param name="DryRun">Whether upload only prints its commands (upload).</param>
internal sealed record CommandLineOptions(
    BundlerCommand Command,
    bool All,
    IReadOnlyList<string> Bundles,
    string? Output,
    string? Input,
    string? SourceStore,
    string Cache,
    bool SkipUnavailable,
    string Repository,
    bool DryRun)
{
    /// <summary>The default SHA-keyed cache directory, relative to the working directory (git-ignored).</summary>
    internal static readonly string DefaultCache = Path.Combine("artifacts", "model-bundler", "cache");

    /// <summary>The usage text printed for a command-line error.</summary>
    internal const string Usage =
        "Usage:\n"
        + "  build (--all | --bundle <name>...) --output <dir> [--source-store <root>] [--cache <dir>] [--skip-unavailable]\n"
        + "  verify --input <dir> [--source-store <root>] [--cache <dir>]\n"
        + "  upload --input <dir> [--repo <owner/name>] [--dry-run]";

    /// <summary>
    ///     Parses the command line, rejecting an unknown command or option, a missing value, or an option the command
    ///     does not take.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The parsed options.</returns>
    /// <exception cref="ArgumentException">Thrown when the command line is invalid; the message says why.</exception>
    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Count == 0)
        {
            throw new ArgumentException("No command was given.", nameof(args));
        }

        var command = args[0] switch
        {
            "build" => BundlerCommand.Build,
            "verify" => BundlerCommand.Verify,
            "upload" => BundlerCommand.Upload,
            _ => throw new ArgumentException($"'{args[0]}' is not a command.", nameof(args)),
        };

        // Read the options; each command accepts only its own
        var all = false;
        var skipUnavailable = false;
        var dryRun = false;
        var bundles = new List<string>();
        string? output = null;
        string? input = null;
        string? sourceStore = null;
        string? cache = null;
        string? repository = null;
        for (var index = 1; index < args.Count; index++)
        {
            var option = args[index];
            if (!IsAllowed(command, option))
            {
                throw new ArgumentException($"'{option}' is not an option of '{args[0]}'.", nameof(args));
            }

            switch (option)
            {
                case "--all": all = true; break;
                case "--skip-unavailable": skipUnavailable = true; break;
                case "--dry-run": dryRun = true; break;
                case "--bundle": bundles.Add(ReadValue(args, ref index)); break;
                case "--output": output = ReadValue(args, ref index); break;
                case "--input": input = ReadValue(args, ref index); break;
                case "--source-store": sourceStore = ReadValue(args, ref index); break;
                case "--cache": cache = ReadValue(args, ref index); break;
                default: repository = ReadValue(args, ref index); break;
            }
        }

        // Check the required combinations
        if (command == BundlerCommand.Build && (all == (bundles.Count > 0) || output is null))
        {
            throw new ArgumentException("build needs exactly one of --all or --bundle, and --output.", nameof(args));
        }

        if (command != BundlerCommand.Build && input is null)
        {
            throw new ArgumentException($"{args[0]} needs --input.", nameof(args));
        }

        return new CommandLineOptions(
            command,
            all,
            bundles,
            output,
            input,
            sourceStore,
            cache ?? DefaultCache,
            skipUnavailable,
            repository ?? ReleaseUploader.DefaultRepository,
            dryRun);
    }

    /// <summary>Gets whether a command accepts an option.</summary>
    /// <param name="command">The command.</param>
    /// <param name="option">The option.</param>
    /// <returns><see langword="true"/> when the command accepts the option.</returns>
    private static bool IsAllowed(BundlerCommand command, string option) =>
        command switch
        {
            BundlerCommand.Build => option is "--all" or "--bundle" or "--output" or "--source-store" or "--cache" or "--skip-unavailable",
            BundlerCommand.Verify => option is "--input" or "--source-store" or "--cache",
            _ => option is "--input" or "--repo" or "--dry-run",
        };

    /// <summary>Reads the value after an option.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="index">The option's index; advanced to the value.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentException">Thrown when the value is missing or empty.</exception>
    private static string ReadValue(IReadOnlyList<string> args, ref int index)
    {
        var option = args[index];
        index++;
        if (index >= args.Count || string.IsNullOrEmpty(args[index]) || args[index].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{option}' needs a value.", nameof(args));
        }

        return args[index];
    }
}
