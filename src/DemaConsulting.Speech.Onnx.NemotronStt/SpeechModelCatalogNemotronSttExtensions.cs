using DemaConsulting.Speech.ModelManagementSubsystem;
using NemotronModels = DemaConsulting.Speech.Onnx.NemotronStt.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Onnx.NemotronStt;

/// <summary>
///     Extension method that registers this package's NVIDIA Nemotron 3.5 ASR streaming ONNX
///     recognition model into a <see cref="SpeechModelCatalog"/>.
/// </summary>
/// <remarks>
///     Per this library's "core ships zero built-in models" decision, mirroring
///     <c>AddSherpaModels()</c> and <c>AddKokoroModels()</c>: call
///     <see cref="AddNemotronSttModels"/> once, immediately after constructing a
///     <see cref="SpeechModelCatalog"/>, to register this package's one offline speech-to-text
///     model (<see cref="NemotronModels.OnnxNemotronMultilingualRecognitionModel"/>).
/// </remarks>
public static class SpeechModelCatalogNemotronSttExtensions
{
    /// <summary>
    ///     Registers this package's Nemotron ONNX recognition model into <paramref name="catalog"/>.
    /// </summary>
    /// <param name="catalog">The catalog to append this package's model to. Must not be null.</param>
    /// <param name="preferredExecutionProviderNames">
    ///     The ordered, accelerated execution provider names the model's encoder attempts first
    ///     (for example <c>"DmlExecutionProvider"</c>), or <see langword="null"/> for CPU-only.
    /// </param>
    /// <returns>The same <paramref name="catalog"/> instance, to allow call chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Per <see cref="SpeechModelCatalog"/>'s builder contract, call this once, immediately
    ///     after construction, before any concurrent enumeration or download begins.
    /// </remarks>
    public static SpeechModelCatalog AddNemotronSttModels(
        this SpeechModelCatalog catalog,
        IReadOnlyList<string>? preferredExecutionProviderNames = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return catalog.AddModels(
            new NemotronModels.OnnxNemotronMultilingualRecognitionModel(preferredExecutionProviderNames));
    }
}
