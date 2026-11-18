namespace DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;

/// <summary>
///     Everything an <c>evaluate-external</c> test needs: a training fixture with a trained model, fake external
///     sources with their pins, a cache folder outside the fixture's repository folder and a record folder.
/// </summary>
internal sealed class ExternalEvaluationRig : IDisposable
{
    /// <summary>
    ///     Creates the rig and trains the fixture model.
    /// </summary>
    /// <param name="content">The external content, or null for the standard content.</param>
    public ExternalEvaluationRig(ExternalContent? content = null)
    {
        Training = new TrainingFixture();
        ModelDirectory = CommandHarness.TrainFixtureModel(Training);
        External = new ExternalSourceFixture(Training, content);
        CacheDirectory = External.NewFolder("cache");
        RecordDirectory = External.NewFolder("record");
    }

    /// <summary>Gets the training fixture, laid out like a repository.</summary>
    public TrainingFixture Training { get; }

    /// <summary>Gets the fake external sources.</summary>
    public ExternalSourceFixture External { get; }

    /// <summary>Gets the folder of the trained fixture model.</summary>
    public string ModelDirectory { get; }

    /// <summary>Gets a cache folder outside the fixture's repository folder.</summary>
    public string CacheDirectory { get; }

    /// <summary>Gets a record folder.</summary>
    public string RecordDirectory { get; }

    /// <summary>
    ///     Runs the command against the trained model, the fake pins and the outside cache folder.
    /// </summary>
    /// <param name="args">The further command arguments.</param>
    /// <returns>The exit code, standard output and standard error.</returns>
    public (int ExitCode, string Output, string Error) Run(params string[] args) =>
        CommandHarness.EvaluateExternal(Training, External.Fetcher(), [.. DefaultArguments(), .. args]);

    /// <summary>
    ///     Gets the arguments every run starts with: the model, the pins and the cache.
    /// </summary>
    /// <returns>The arguments.</returns>
    public string[] DefaultArguments() => ["--model-dir", ModelDirectory, "--pins", External.PinsPath, "--cache", CacheDirectory];

    /// <inheritdoc/>
    public void Dispose()
    {
        External.Dispose();
        Training.Dispose();
    }
}
