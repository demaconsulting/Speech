using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem.Fakes;

/// <summary>
///     Deterministic <see cref="ISynthesisEngineFactory"/> test double that hands out a
///     pre-configured <see cref="FakeSynthesisEngine"/> and records the arguments it was asked
///     to load, so composition can be verified without a model directory or a native runtime.
/// </summary>
internal sealed class FakeSynthesisEngineFactory : ISynthesisEngineFactory
{
    /// <summary>The exception to throw from <see cref="Create"/>, when one was scripted.</summary>
    private readonly Exception? _createException;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeSynthesisEngineFactory"/> class.
    /// </summary>
    /// <param name="engine">
    ///     The engine to return, or <see langword="null"/> to return a new empty
    ///     <see cref="FakeSynthesisEngine"/>.
    /// </param>
    /// <param name="createException">
    ///     An exception for <see cref="Create"/> to throw, or <see langword="null"/> to load
    ///     normally. Used to prove composition degrades honestly when a native runtime or model
    ///     file is unusable.
    /// </param>
    public FakeSynthesisEngineFactory(
        FakeSynthesisEngine? engine = null,
        Exception? createException = null)
    {
        Engine = engine ?? new FakeSynthesisEngine();
        _createException = createException;
    }

    /// <summary>Gets the engine this factory returns from <see cref="Create"/>.</summary>
    public FakeSynthesisEngine Engine { get; }

    /// <summary>Gets the model most recently passed to <see cref="Create"/>.</summary>
    public ISynthesisModel? RequestedModel { get; private set; }

    /// <summary>Gets the installed-model directory most recently passed to <see cref="Create"/>.</summary>
    public string? RequestedInstalledModelDirectory { get; private set; }

    /// <summary>Gets the number of <see cref="Create"/> calls this factory has received.</summary>
    public int CreateCallCount { get; private set; }

    /// <inheritdoc/>
    public ISynthesisEngine Create(ISynthesisModel model, string installedModelDirectory)
    {
        CreateCallCount++;
        RequestedModel = model;
        RequestedInstalledModelDirectory = installedModelDirectory;

        if (_createException is not null)
        {
            throw _createException;
        }

        return Engine;
    }
}
