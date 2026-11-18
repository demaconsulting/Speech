using DemaConsulting.Speech.Phonemes.En.Tools.LexiconSubsystem;
using DemaConsulting.Speech.Phonemes.En.Tools.MorphologySubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tools;

/// <summary>
///     Hosts the Phonemes.En tools command line: <c>build-lexicon</c> and <c>measure-morphology</c>.
/// </summary>
/// <remarks>
///     This executable is a developer tool that is never packed or shipped; it builds and verifies the embedded
///     lexicon and measures the pronunciation rules, locally and in CI. <see cref="Main"/> only wires the real
///     console, current directory and environment to <see cref="Run"/>. Unknown or missing commands are a usage
///     error (exit code 2). Further commands (reading proposer, OOV benchmark, renderer measurement) are added
///     here by later work.
/// </remarks>
internal static class Program
{
    /// <summary>The usage text of the tool.</summary>
    private const string Usage = """
        Usage: DemaConsulting.Speech.Phonemes.En.Tools <command> [options]

        Commands:
          build-lexicon        Build the embedded CMUdict lexicon, or verify it with --check (run 'build-lexicon --help').
          measure-morphology   Measure the morphology rules against the lexicon (run 'measure-morphology --help').
        """;

    /// <summary>
    ///     The process entry point.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    internal static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    /// <summary>
    ///     Dispatches the command line to a command.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="output">Receives normal output.</param>
    /// <param name="error">Receives usage and I/O errors.</param>
    /// <returns>The command's exit code; <c>2</c> for a usage error.</returns>
    internal static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);

        // No command, or a help request, prints the usage text
        if (args.Count == 0 || args[0] is "--help" or "-h" or "-?")
        {
            (args.Count == 0 ? error : output).WriteLine(Usage);
            return args.Count == 0 ? 2 : 0;
        }

        var rest = args.Skip(1).ToList();
        if (string.Equals(args[0], BuildLexiconCommand.Name, StringComparison.Ordinal))
        {
            return BuildLexiconCommand.Run(rest, output, error, Directory.GetCurrentDirectory(), AppContext.BaseDirectory);
        }

        if (string.Equals(args[0], MeasureMorphologyCommand.Name, StringComparison.Ordinal))
        {
            return MeasureMorphologyCommand.Run(rest, output, error);
        }

        error.WriteLine($"Unknown command '{args[0]}'.");
        error.WriteLine(Usage);
        return 2;
    }
}
