using DemaConsulting.Speech.ModelManagementSubsystem;
using KokoroModels = DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Onnx.Kokoro;

/// <summary>
///     Extension method that registers this package's Kokoro-82M v1.0 ONNX-Runtime-backed
///     production model into a <see cref="SpeechModelCatalog"/>.
/// </summary>
/// <remarks>
///     Per this library's "core ships zero built-in models" decision, mirroring the sibling
///     <c>DemaConsulting.Speech.Sherpa</c> package's <c>AddSherpaModels()</c>: call
///     <see cref="AddKokoroModels"/> once, immediately after constructing a
///     <see cref="SpeechModelCatalog"/>, to register this package's one offline text-to-speech
///     model (<see cref="KokoroModels.OnnxKokoroEnglishSynthesisModel"/>).
/// </remarks>
public static class SpeechModelCatalogKokoroExtensions
{
    /// <summary>
    ///     Registers this package's Kokoro-82M v1.0 ONNX model into <paramref name="catalog"/>.
    /// </summary>
    /// <param name="catalog">The catalog to append this package's model to. Must not be null.</param>
    /// <param name="preferredExecutionProviderNames">
    ///     The ordered, accelerated execution provider names the registered model attempts first
    ///     (for example <c>"DmlExecutionProvider"</c>), or <see langword="null"/> for CPU-only.
    ///     See <see cref="KokoroModels.OnnxKokoroEnglishSynthesisModel"/>'s constructor remarks.
    /// </param>
    /// <returns>The same <paramref name="catalog"/> instance, to allow call chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Per <see cref="SpeechModelCatalog"/>'s builder contract, call this once, immediately
    ///     after construction, before any concurrent <see cref="SpeechModelCatalog.Enumerate"/> or
    ///     <see cref="SpeechModelCatalog.DownloadAsync"/> call begins.
    /// </remarks>
    public static SpeechModelCatalog AddKokoroModels(
        this SpeechModelCatalog catalog,
        IReadOnlyList<string>? preferredExecutionProviderNames = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return catalog.AddModels(
            new KokoroModels.OnnxKokoroEnglishSynthesisModel(preferredExecutionProviderNames));
    }
}
