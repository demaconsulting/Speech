using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Real <see cref="IRecognitionEngineFactory"/> implementation that builds a
///     <see cref="SherpaOnnxRecognitionEngine"/> from a model's own declared engine
///     configuration.
/// </summary>
/// <remarks>
///     This type deliberately contains no model-specific knowledge: this library makes each
///     per-model backing class responsible for "sherpa-onnx configuration for its own model
///     architecture", so all this factory does is ask the model for its configuration and
///     declared input format and hand both to the engine. Adding a model therefore never requires
///     changing this class.
///     <para>
///     Loading failures - a missing <c>org.k2fsa.sherpa.onnx.runtime.{RID}</c> native binary, an
///     unsupported RID, or corrupt model files - propagate to the caller.
///     <see cref="SpeechRecognizerFactory"/> catches them and returns
///     <see cref="UnavailableSpeechRecognizer.Instance"/>, so composition still never throws.
///     </para>
///     <para>The type is stateless and safe for concurrent use.</para>
/// </remarks>
internal sealed class SherpaOnnxRecognitionEngineFactory : IRecognitionEngineFactory
{
    /// <inheritdoc/>
    public IRecognitionEngine Create(IRecognitionModel model, string installedModelDirectory)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrEmpty(installedModelDirectory);

        // Ask the model for its own configuration, resolved against where its files were
        // actually installed, then load it at the sample rate the same model declared.
        var config = model.CreateEngineConfig(installedModelDirectory);

        return new SherpaOnnxRecognitionEngine(
            config,
            model.AudioFormat.SampleRate,
            model.PostEndpointWarmupWindowMs);
    }
}
