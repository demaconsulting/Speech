using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem.Fakes;

/// <summary>
///     Deterministic <see cref="IRecognitionEngineFactory"/> test double that hands out a
///     pre-configured <see cref="FakeRecognitionEngine"/> and records the arguments it was asked
///     to load, so composition can be verified without a model directory or a native runtime.
/// </summary>
internal sealed class FakeRecognitionEngineFactory : IRecognitionEngineFactory
{
    /// <summary>The exception to throw from <see cref="Create"/>, when one was scripted.</summary>
    private readonly Exception? _createException;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeRecognitionEngineFactory"/> class.
    /// </summary>
    /// <param name="engine">
    ///     The engine to return, or <see langword="null"/> to return a new empty
    ///     <see cref="FakeRecognitionEngine"/>.
    /// </param>
    /// <param name="createException">
    ///     An exception for <see cref="Create"/> to throw, or <see langword="null"/> to load
    ///     normally. Used to prove composition degrades honestly when a native runtime or model
    ///     file is unusable.
    /// </param>
    public FakeRecognitionEngineFactory(
        FakeRecognitionEngine? engine = null,
        Exception? createException = null)
    {
        Engine = engine ?? new FakeRecognitionEngine();
        _createException = createException;
    }

    /// <summary>Gets the engine this factory returns from <see cref="Create"/>.</summary>
    public FakeRecognitionEngine Engine { get; }

    /// <summary>Gets the model most recently passed to <see cref="Create"/>.</summary>
    public IRecognitionModel? RequestedModel { get; private set; }

    /// <summary>Gets the installed-model directory most recently passed to <see cref="Create"/>.</summary>
    public string? RequestedInstalledModelDirectory { get; private set; }

    /// <summary>Gets the parameter value bag most recently passed to <see cref="Create"/>.</summary>
    public IReadOnlyDictionary<string, object>? RequestedParameterValues { get; private set; }

    /// <summary>
    ///     Gets the <see cref="SherpaOnnx.OnlineRecognizerConfig"/> most recently built by calling
    ///     the requested model's <c>CreateEngineConfig</c>, mirroring what
    ///     <c>SherpaOnnxRecognitionEngineFactory</c> genuinely does, so a test can prove a supplied
    ///     <c>parameterValues</c> bag actually reached the model rather than merely reached this
    ///     factory.
    /// </summary>
    public SherpaOnnx.OnlineRecognizerConfig? RequestedConfig { get; private set; }

    /// <summary>Gets the number of <see cref="Create"/> calls this factory has received.</summary>
    public int CreateCallCount { get; private set; }

    /// <inheritdoc/>
    public IRecognitionEngine Create(
        IRecognitionModel model,
        string installedModelDirectory,
        IReadOnlyDictionary<string, object>? parameterValues = null)
    {
        CreateCallCount++;
        RequestedModel = model;
        RequestedInstalledModelDirectory = installedModelDirectory;
        RequestedParameterValues = parameterValues;
        RequestedConfig = model.CreateEngineConfig(installedModelDirectory, parameterValues);

        if (_createException is not null)
        {
            throw _createException;
        }

        return Engine;
    }
}
