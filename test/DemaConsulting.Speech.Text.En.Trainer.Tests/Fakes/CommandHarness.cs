using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;

/// <summary>
///     Runs the <c>train</c> and <c>evaluate</c> commands in memory against a <see cref="TrainingFixture"/>.
/// </summary>
internal static class CommandHarness
{
    /// <summary>
    ///     Runs <c>train</c> with the fixture folder as the current directory.
    /// </summary>
    /// <param name="fixture">The fixture.</param>
    /// <param name="args">The command arguments.</param>
    /// <returns>The exit code, standard output and standard error.</returns>
    public static (int ExitCode, string Output, string Error) Train(TrainingFixture fixture, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = TrainCommand.Run(args, output, error, fixture.BaseDirectory, fixture.BaseDirectory);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>
    ///     Runs <c>evaluate</c> with the fixture folder as the current directory.
    /// </summary>
    /// <param name="fixture">The fixture.</param>
    /// <param name="args">The command arguments.</param>
    /// <returns>The exit code, standard output and standard error.</returns>
    public static (int ExitCode, string Output, string Error) Evaluate(TrainingFixture fixture, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = EvaluateCommand.Run(args, output, error, fixture.BaseDirectory, fixture.BaseDirectory);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>
    ///     Trains the fixture into its model folder with a small hash table, as most tests need a model.
    /// </summary>
    /// <param name="fixture">The fixture.</param>
    /// <returns>The path of the model folder.</returns>
    public static string TrainFixtureModel(TrainingFixture fixture)
    {
        var (exitCode, output, error) = Train(fixture, "--model-dir", fixture.ModelDirectory, "--hash-bits", "18");
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"training the fixture failed with {exitCode}: {output}{error}");
        }

        return fixture.ModelDirectory;
    }
}
