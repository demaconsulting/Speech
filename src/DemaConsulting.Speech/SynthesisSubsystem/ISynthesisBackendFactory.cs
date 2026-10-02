using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Internal, mockable factory seam that builds a loaded <see cref="ISynthesisBackend"/> for
///     one installed synthesis model.
/// </summary>
/// <remarks>
///     Separating engine construction from engine use lets <see cref="SpeechSynthesizerFactory"/>
///     decide, without any sherpa-onnx knowledge of its own, whether a real engine could be
///     loaded, and lets tests inject a fake engine without a model directory, a native runtime, or
///     an inference session. It mirrors <c>RecognitionSubsystem.IRecognitionBackendFactory</c> for
///     exactly the same testability reason. Renamed from <c>ISynthesisEngineFactory</c> alongside
///     <see cref="ISynthesisBackend"/>; members unchanged.
/// </remarks>
internal interface ISynthesisBackendFactory
{
    /// <summary>
    ///     Loads the synthesis engine described by a model, from that model's installed files.
    /// </summary>
    /// <param name="model">The installed synthesis model to load. Must not be null.</param>
    /// <param name="installedModelDirectory">
    ///     The absolute path of the directory holding the model's installed files. Must not be
    ///     null or empty.
    /// </param>
    /// <returns>A loaded engine ready to synthesize segments. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="installedModelDirectory"/> is null or empty.
    /// </exception>
    /// <remarks>
    ///     Loading allocates real native inference resources and can fail for ordinary machine
    ///     reasons - a missing native runtime binary, an unsupported RID, or corrupt model files.
    ///     Implementations surface those failures as exceptions;
    ///     <see cref="SpeechSynthesizerFactory"/> converts them into the honest
    ///     <see cref="UnavailableSpeechSynthesizerEngine"/> fallback so composition still never
    ///     throws.
    /// </remarks>
    ISynthesisBackend Create(ISynthesisModel model, string installedModelDirectory);
}
