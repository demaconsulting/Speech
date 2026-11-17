using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer;

/// <summary>
///     Hosts the Text.En trainer command line. Today the only command is <c>check-data</c>.
/// </summary>
/// <remarks>
///     This executable is a developer tool that is never packed or shipped; it exists so the Text.En training
///     data can be verified locally and in CI. <see cref="Main"/> only wires the real console, current directory and
///     environment to <see cref="Run"/>, which tests call with in-memory writers. Unknown or missing commands are a
///     usage error (exit code 2).
/// </remarks>
internal static class Program
{
    /// <summary>The usage text of the trainer.</summary>
    private const string Usage = """
        Usage: DemaConsulting.Speech.Text.En.Trainer <command> [options]

        Commands:
          check-data   Verify the Text.En training data (run 'check-data --help' for options).
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
    /// <returns><c>0</c> on success, <c>1</c> when the data violates a rule, <c>2</c> for a usage or I/O error.</returns>
    internal static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);

        // No command, or a help request, prints the usage text
        if (args.Count == 0 || args[0] is "--help" or "-h" or "-?")
        {
            (args.Count == 0 ? error : output).WriteLine(Usage);
            return args.Count == 0 ? 2 : 0;
        }

        if (!string.Equals(args[0], CheckDataCommand.Name, StringComparison.Ordinal))
        {
            error.WriteLine($"Unknown command '{args[0]}'.");
            error.WriteLine(Usage);
            return 2;
        }

        return CheckDataCommand.Run(
            args.Skip(1).ToList(),
            output,
            error,
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable);
    }
}
