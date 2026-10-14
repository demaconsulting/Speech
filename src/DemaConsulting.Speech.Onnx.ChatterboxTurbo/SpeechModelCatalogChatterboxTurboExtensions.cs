using DemaConsulting.Speech.ModelManagementSubsystem;
using ChatterboxTurboModels = DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo;

/// <summary>
///     Extension method that registers this package's Chatterbox Turbo ONNX-Runtime-backed production model
///     into a <see cref="SpeechModelCatalog"/>.
/// </summary>
/// <remarks>
///     Per this library's "core ships zero built-in models" decision, mirroring the sibling packages: call
///     <see cref="AddChatterboxTurboModels"/> once, immediately after constructing a
///     <see cref="SpeechModelCatalog"/>, to register this package's one offline text-to-speech model
///     (<see cref="ChatterboxTurboModels.OnnxChatterboxTurboEnglishSynthesisModel"/>).
/// </remarks>
public static class SpeechModelCatalogChatterboxTurboExtensions
{
    /// <summary>
    ///     Registers this package's Chatterbox Turbo model into <paramref name="catalog"/>.
    /// </summary>
    /// <param name="catalog">The catalog to append the model to. Must not be null.</param>
    /// <param name="options">
    ///     The model configuration including the reference voices, or <see langword="null"/> for defaults (only the
    ///     voices found in the catalog store's reference-voice library: the user folder and installed packs).
    /// </param>
    /// <returns>The same <paramref name="catalog"/> instance, to allow call chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Per <see cref="SpeechModelCatalog"/>'s builder contract, call this once, immediately after
    ///     construction, before any concurrent enumerate or download call begins. Voice files in
    ///     <see cref="ChatterboxTurboModels.ChatterboxTurboModelOptions.ReferenceVoiceDirectory"/> are read now. Unless
    ///     <see cref="ChatterboxTurboModels.ChatterboxTurboModelOptions.IncludeStoreVoices"/> is <see langword="false"/>,
    ///     the voices of <see cref="ChatterboxTurboModels.ChatterboxTurboModelOptions.ReferenceVoiceLibrary"/> - or, when
    ///     that is <see langword="null"/>, of a <see cref="ReferenceVoiceLibrary"/> over <c>catalog.Store</c> - follow the
    ///     explicit voices; the caller's options object is not modified. The voice list is fixed when this method runs.
    /// </remarks>
    public static SpeechModelCatalog AddChatterboxTurboModels(
        this SpeechModelCatalog catalog,
        ChatterboxTurboModels.ChatterboxTurboModelOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var library = options?.ReferenceVoiceLibrary ?? new ReferenceVoiceLibrary(catalog.Store);
        return catalog.AddModels(new ChatterboxTurboModels.OnnxChatterboxTurboEnglishSynthesisModel(options, library));
    }
}
