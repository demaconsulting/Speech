using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Real <see cref="IRecognitionBackendFactory"/> implementation that builds a loaded
///     <see cref="IRecognitionBackend"/> by asking a model to construct its own backend.
/// </summary>
/// <remarks>
///     This replaces the former sherpa-onnx-coupled <c>SherpaOnnxRecognitionEngineFactory</c> now
///     that <see cref="IRecognitionModel.CreateBackend(string,System.Collections.Generic.IReadOnlyDictionary{string,object}?)"/>
///     itself returns an already-loaded <see cref="IRecognitionBackend"/>: once the model
///     interface exposes the neutral backend type directly, a per-engine-family factory
///     implementation has no remaining value - this type deliberately contains zero sherpa-onnx
///     (or any other concrete engine family) knowledge, so it keeps working unchanged no matter
///     which sibling package supplies the loaded model.
///     <para>
///     Loading failures - a missing native runtime binary, an unsupported RID, or corrupt model
///     files - propagate to the caller. <see cref="SpeechRecognizerFactory"/> catches them and
///     returns <see cref="UnavailableSpeechRecognizerEngine.Instance"/>, so composition still
///     never throws.
///     </para>
///     <para>The type is stateless and safe for concurrent use.</para>
/// </remarks>
internal sealed class DefaultRecognitionBackendFactory : IRecognitionBackendFactory
{
    /// <inheritdoc/>
    public IRecognitionBackend Create(
        IRecognitionModel model,
        string installedModelDirectory,
        IReadOnlyDictionary<string, object>? parameterValues = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrEmpty(installedModelDirectory);

        return model.CreateBackend(installedModelDirectory, parameterValues);
    }
}
