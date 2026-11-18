using DemaConsulting.Speech.ModelManagementSubsystem;
using PiperModels = DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Onnx.Piper;

/// <summary>
///     Extension method that registers this package's Piper LibriTTS-R English ONNX-Runtime-backed production model into a
///     <see cref="SpeechModelCatalog"/>.
/// </summary>
/// <remarks>
///     Per this library's "core ships zero built-in models" decision, mirroring <c>AddSherpaModels()</c> and
///     <c>AddKokoroModels()</c>: call <see cref="AddPiperModels"/> once, immediately after constructing a
///     <see cref="SpeechModelCatalog"/>, to register this package's one offline text-to-speech model
///     (<see cref="PiperModels.OnnxPiperLibriTtsEnSynthesisModel"/>).
/// </remarks>
public static class SpeechModelCatalogPiperExtensions
{
    /// <summary>
    ///     Registers this package's Piper LibriTTS-R English ONNX model into <paramref name="catalog"/>.
    /// </summary>
    /// <param name="catalog">The catalog to append this package's model to. Must not be null.</param>
    /// <param name="preferredExecutionProviderNames">
    ///     The ordered, accelerated execution provider names the registered model attempts first (for example
    ///     <c>"DmlExecutionProvider"</c>), or <see langword="null"/> for CPU-only. See
    ///     <see cref="PiperModels.OnnxPiperLibriTtsEnSynthesisModel"/>'s constructor remarks.
    /// </param>
    /// <returns>The same <paramref name="catalog"/> instance, to allow call chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Per <see cref="SpeechModelCatalog"/>'s builder contract, call this once, immediately after construction, before
    ///     any concurrent <see cref="SpeechModelCatalog.Enumerate"/> or <see cref="SpeechModelCatalog.DownloadAsync"/> call
    ///     begins.
    /// </remarks>
    public static SpeechModelCatalog AddPiperModels(
        this SpeechModelCatalog catalog,
        IReadOnlyList<string>? preferredExecutionProviderNames = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return catalog.AddModels(new PiperModels.OnnxPiperLibriTtsEnSynthesisModel(preferredExecutionProviderNames));
    }
}
