using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Real <see cref="ISynthesisBackendFactory"/> implementation that builds a loaded
///     <see cref="ISynthesisBackend"/> by asking a model to construct its own backend.
/// </summary>
/// <remarks>
///     This replaces the former sherpa-onnx-coupled <c>SherpaOnnxSynthesisEngineFactory</c> now
///     that <see cref="ISynthesisModel.CreateBackend"/> itself returns an already-loaded
///     <see cref="ISynthesisBackend"/>: once the model interface exposes the neutral backend type
///     directly, a per-engine-family factory implementation has no remaining value - this type
///     deliberately contains zero sherpa-onnx (or any other concrete engine family) knowledge, so
///     it keeps working unchanged no matter which sibling package supplies the loaded model.
///     <para>
///     Loading failures - a missing native runtime binary, an unsupported RID, or corrupt model
///     files - propagate to the caller. <see cref="SpeechSynthesizerFactory"/> catches them and
///     returns <see cref="UnavailableSpeechSynthesizerEngine.Instance"/>, so composition still
///     never throws.
///     </para>
///     <para>The type is stateless and safe for concurrent use.</para>
/// </remarks>
internal sealed class DefaultSynthesisBackendFactory : ISynthesisBackendFactory
{
    /// <inheritdoc/>
    public ISynthesisBackend Create(ISynthesisModel model, string installedModelDirectory)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrEmpty(installedModelDirectory);

        return model.CreateBackend(installedModelDirectory);
    }
}
