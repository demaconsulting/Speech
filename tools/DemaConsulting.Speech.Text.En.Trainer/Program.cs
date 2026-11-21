using DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.ExternalEvaluationSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer;

/// <summary>
///     Hosts the Text.En trainer command line: <c>check-data</c>, <c>train</c>, <c>evaluate</c>, <c>evaluate-external</c> and <c>cluster</c>.
/// </summary>
/// <remarks>
///     This executable is a developer tool that is never packed or shipped; it exists so the Text.En training
///     data can be verified, the heteronym model trained and verified, the model evaluated, and the word-cluster table induced,
///     locally and in CI.
///     <see cref="Main"/> only wires the real console, current directory and
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
          train        Train the heteronym model, or verify the committed one with --check (run 'train --help').
          evaluate     Measure the heteronym model and decide the quality gate (run 'evaluate --help').
          evaluate-external
                       Measure the embedded model on external evaluation-only datasets (run 'evaluate-external --help').
          cluster      Fetch, audit and cluster the word-cluster corpus, or re-verify the table (run 'cluster --help').
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
    /// <returns><c>0</c> on success, <c>1</c> when the data violates a rule or a model is stale, <c>2</c> for a usage or I/O error.</returns>
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
        var currentDirectory = Directory.GetCurrentDirectory();
        if (string.Equals(args[0], TrainCommand.Name, StringComparison.Ordinal))
        {
            return TrainCommand.Run(rest, output, error, currentDirectory, AppContext.BaseDirectory);
        }

        if (string.Equals(args[0], EvaluateCommand.Name, StringComparison.Ordinal))
        {
            return EvaluateCommand.Run(rest, output, error, currentDirectory, AppContext.BaseDirectory);
        }

        if (string.Equals(args[0], EvaluateExternalCommand.Name, StringComparison.Ordinal))
        {
            return EvaluateExternalCommand.Run(rest, output, error, currentDirectory, AppContext.BaseDirectory);
        }

        if (string.Equals(args[0], ClusterCommand.Name, StringComparison.Ordinal))
        {
            return ClusterCommand.Run(rest, output, error, currentDirectory, AppContext.BaseDirectory);
        }

        if (!string.Equals(args[0], CheckDataCommand.Name, StringComparison.Ordinal))
        {
            error.WriteLine($"Unknown command '{args[0]}'.");
            error.WriteLine(Usage);
            return 2;
        }

        return CheckDataCommand.Run(
            rest,
            output,
            error,
            currentDirectory,
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable);
    }
}
