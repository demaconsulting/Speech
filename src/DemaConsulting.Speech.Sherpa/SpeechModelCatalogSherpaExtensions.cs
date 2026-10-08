using DemaConsulting.Speech.ModelManagementSubsystem;
using SherpaModels = DemaConsulting.Speech.Sherpa.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Sherpa;

/// <summary>
///     Extension method that registers this package's four sherpa-onnx-backed production models
///     into a <see cref="SpeechModelCatalog"/>.
/// </summary>
/// <remarks>
///     Per this library's "core ships zero built-in models" decision, <c>DemaConsulting.Speech</c>
///     itself has no reference to sherpa-onnx or any concrete model, and a host must opt in to a
///     set of real models before first use. This package is that opt-in: call
///     <see cref="AddSherpaModels"/> once, immediately after constructing a
///     <see cref="SpeechModelCatalog"/>, to register two streaming speech-to-text models
///     (<see cref="SherpaModels.SherpaOnnxZipformerEnRecognitionModel"/>,
///     <see cref="SherpaModels.SherpaOnnxNemotronStreamingEnRecognitionModel"/>) and two offline
///     text-to-speech models
///     (<see cref="SherpaModels.SherpaOnnxVitsLibriTtsEnglishSynthesisModel"/>,
///     <see cref="SherpaModels.SherpaOnnxKokoroEnglishSynthesisModel"/>) - the exact same four
///     models, in the exact same order, this library shipped built-in before the
///     sherpa-onnx-coupled code was split out of core into this package.
/// </remarks>
public static class SpeechModelCatalogSherpaExtensions
{
    /// <summary>
    ///     Registers this package's four sherpa-onnx-backed models into <paramref name="catalog"/>.
    /// </summary>
    /// <param name="catalog">The catalog to append this package's models to. Must not be null.</param>
    /// <returns>The same <paramref name="catalog"/> instance, to allow call chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Per <see cref="SpeechModelCatalog"/>'s builder contract, call this once, immediately
    ///     after construction, before any concurrent <see cref="SpeechModelCatalog.Enumerate"/> or
    ///     <see cref="SpeechModelCatalog.DownloadAsync"/> call begins.
    /// </remarks>
    public static SpeechModelCatalog AddSherpaModels(this SpeechModelCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return catalog.AddModels(
            new SherpaModels.SherpaOnnxZipformerEnRecognitionModel(),
            new SherpaModels.SherpaOnnxNemotronStreamingEnRecognitionModel(),
            new SherpaModels.SherpaOnnxVitsLibriTtsEnglishSynthesisModel(),
            new SherpaModels.SherpaOnnxKokoroEnglishSynthesisModel());
    }
}
