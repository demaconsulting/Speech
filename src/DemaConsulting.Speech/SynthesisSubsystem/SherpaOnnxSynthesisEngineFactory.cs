using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Real <see cref="ISynthesisEngineFactory"/> implementation that builds a
///     <see cref="SherpaOnnxSynthesisEngine"/> from a model's own declared engine configuration.
/// </summary>
/// <remarks>
///     This type deliberately contains no model-specific knowledge: architecture.md makes each
///     per-model backing class responsible for "sherpa-onnx configuration for its own model
///     architecture", so all this factory does is ask the model for its configuration and hand it
///     to the engine. Adding a model therefore never requires changing this class.
///     <para>
///     Loading failures - a missing <c>org.k2fsa.sherpa.onnx.runtime.{RID}</c> native binary, an
///     unsupported RID, or corrupt model files - propagate to the caller.
///     <see cref="SpeechSynthesizerFactory"/> catches them and returns
///     <see cref="UnavailableSpeechSynthesizer.Instance"/>, so composition still never throws.
///     </para>
///     <para>The type is stateless and safe for concurrent use.</para>
/// </remarks>
internal sealed class SherpaOnnxSynthesisEngineFactory : ISynthesisEngineFactory
{
    /// <inheritdoc/>
    public ISynthesisEngine Create(ISynthesisModel model, string installedModelDirectory)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrEmpty(installedModelDirectory);

        var config = model.CreateEngineConfig(installedModelDirectory);

        return new SherpaOnnxSynthesisEngine(config);
    }
}
