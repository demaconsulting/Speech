using DemaConsulting.Speech.Onnx.OnnxRuntimeSubsystem;
using Microsoft.ML.OnnxRuntime;

namespace DemaConsulting.Speech.Onnx.Tests.OnnxRuntimeSubsystem;

/// <summary>
///     Serialization boundary for every test that calls
///     <see cref="OnnxExecutionProviderSelector.Create"/>. The selector exposes a process-wide
///     static test hook (<see cref="OnnxExecutionProviderSelector.OnCandidateOptionsCreated"/>), so
///     any concurrently running test that creates a session could be observed by, or race with, a
///     test that installs the hook. Disabling parallelization for this collection guarantees the
///     hook is only ever observed by the test that installed it.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OnnxSelectorHookCollection
{
    /// <summary>The name of the xUnit collection that serializes selector-hook tests.</summary>
    public const string Name = "OnnxExecutionProviderSelector hook (serialized)";

    /// <summary>Prevents instantiation; the class exists only to carry the collection definition.</summary>
    private OnnxSelectorHookCollection()
    {
    }
}

/// <summary>
///     Disposable scope that installs a callback on the static
///     <see cref="OnnxExecutionProviderSelector.OnCandidateOptionsCreated"/> hook and restores the
///     previously installed value when disposed, so a failing test can never leak its hook into
///     another test.
/// </summary>
internal sealed class SelectorHookScope : IDisposable
{
    /// <summary>The hook value that was installed before this scope was created.</summary>
    private readonly Action<SessionOptions>? _previous;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SelectorHookScope"/> class by installing the
    ///     supplied callback.
    /// </summary>
    /// <param name="hook">The callback to install for the lifetime of this scope.</param>
    private SelectorHookScope(Action<SessionOptions>? hook)
    {
        _previous = OnnxExecutionProviderSelector.OnCandidateOptionsCreated;
        OnnxExecutionProviderSelector.OnCandidateOptionsCreated = hook;
    }

    /// <summary>Installs <paramref name="hook"/> and returns the scope that restores the prior hook.</summary>
    /// <param name="hook">The callback to install until the returned scope is disposed.</param>
    /// <returns>A scope that restores the previously installed hook when disposed.</returns>
    public static SelectorHookScope Install(Action<SessionOptions>? hook) => new(hook);

    /// <summary>Restores the hook that was installed before this scope was created.</summary>
    public void Dispose() => OnnxExecutionProviderSelector.OnCandidateOptionsCreated = _previous;
}
