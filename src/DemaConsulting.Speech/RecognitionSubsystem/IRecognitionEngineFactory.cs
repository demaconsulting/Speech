using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Internal, mockable factory seam that builds a loaded <see cref="IRecognitionEngine"/> for
///     one installed recognition model.
/// </summary>
/// <remarks>
///     Separating engine construction from engine use lets <see cref="SpeechRecognizerFactory"/>
///     decide, without any sherpa-onnx knowledge of its own, whether a real engine could be
///     loaded, and lets tests inject a fake engine without a model directory, a native runtime,
///     or an inference session. It mirrors the <c>IModelDownloadClient</c> seam introduced in
///     Phase 2a for exactly the same testability reason.
/// </remarks>
internal interface IRecognitionEngineFactory
{
    /// <summary>
    ///     Loads the recognition engine described by a model, from that model's installed files.
    /// </summary>
    /// <param name="model">The installed recognition model to load. Must not be null.</param>
    /// <param name="installedModelDirectory">
    ///     The absolute path of the directory holding the model's installed files. Must not be
    ///     null or empty.
    /// </param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag forwarded unchanged to
    ///     <see cref="IRecognitionModel.CreateEngineConfig(string,System.Collections.Generic.IReadOnlyDictionary{string,object}?)"/>,
    ///     or <see langword="null"/> when the caller supplied none.
    /// </param>
    /// <returns>A loaded engine ready to accept samples. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="installedModelDirectory"/> is null or empty.
    /// </exception>
    /// <remarks>
    ///     Loading allocates real native inference resources and can fail for ordinary machine
    ///     reasons - a missing native runtime binary, an unsupported RID, or corrupt model files.
    ///     Implementations surface those failures as exceptions;
    ///     <see cref="SpeechRecognizerFactory"/> converts them into the honest
    ///     <see cref="UnavailableSpeechRecognizer"/> fallback so composition still never throws.
    /// </remarks>
    IRecognitionEngine Create(
        IRecognitionModel model,
        string installedModelDirectory,
        IReadOnlyDictionary<string, object>? parameterValues = null);
}
