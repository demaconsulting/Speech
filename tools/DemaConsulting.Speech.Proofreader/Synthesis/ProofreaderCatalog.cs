using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Kokoro;
using DemaConsulting.Speech.Onnx.Piper;
using DemaConsulting.Speech.Sherpa;

namespace DemaConsulting.Speech.Proofreader.Synthesis;

/// <summary>
///     Owns the model catalog (and the reference-voice library paired with it) the Proofreader lists
///     synthesis models from.
/// </summary>
/// <remarks>
///     Composed exactly like the Demo's catalog minus the recognition model: Sherpa, Kokoro, Piper,
///     and Chatterbox Turbo. The tool never downloads anything - it only lists what the store
///     already contains - so it works offline. The catalog and the voice library are a pair and are
///     disposed together. Not thread-safe; use from the UI thread.
/// </remarks>
internal sealed class ProofreaderCatalog : IDisposable
{
    /// <summary>The reference-voice library the Chatterbox model reads its voices through.</summary>
    private readonly ReferenceVoiceLibrary _voices;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProofreaderCatalog"/> class from a built pair.
    /// </summary>
    /// <param name="catalog">The catalog with every model family registered.</param>
    /// <param name="voices">The voice library paired with the catalog.</param>
    private ProofreaderCatalog(SpeechModelCatalog catalog, ReferenceVoiceLibrary voices)
    {
        Catalog = catalog;
        _voices = voices;
    }

    /// <summary>Gets the model catalog.</summary>
    public SpeechModelCatalog Catalog { get; }

    /// <summary>
    ///     Builds the catalog.
    /// </summary>
    /// <param name="modelsDir">The model store root override, or <see langword="null"/> for the per-user default.</param>
    /// <param name="voicesDir">The Chatterbox reference-voice directory, or <see langword="null"/> for none.</param>
    /// <returns>The composed catalog; the caller disposes it.</returns>
    public static ProofreaderCatalog Create(string? modelsDir, string? voicesDir)
    {
        var storeOptions = string.IsNullOrEmpty(modelsDir) ? null : new SpeechModelStoreOptions { RootPathOverride = modelsDir };
        var catalog = new SpeechModelCatalog(storeOptions, null, null);
        ReferenceVoiceLibrary? voices = null;
        try
        {
            catalog.AddSherpaModels().AddKokoroModels().AddPiperModels();
            voices = new ReferenceVoiceLibrary(catalog.Store, null);
            catalog.AddChatterboxTurboModels(new ChatterboxTurboModelOptions
            {
                ReferenceVoiceDirectory = voicesDir,
                ReferenceVoiceLibrary = voices,
            });
            return new ProofreaderCatalog(catalog, voices);
        }
        catch
        {
            // The pair is never handed to the caller, so release the half-built composition.
            voices?.Dispose();
            catalog.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Lists the synthesis models with their current install state.
    /// </summary>
    /// <returns>The descriptors of every registered model that can synthesize speech.</returns>
    public IReadOnlyList<SpeechModelDescriptor> EnumerateSynthesisModels() =>
        Catalog.Enumerate()
            .Where(descriptor => descriptor.Role == SpeechModelRole.Synthesis && descriptor.Model is ISynthesisModel)
            .ToList();

    /// <inheritdoc/>
    public void Dispose()
    {
        _voices.Dispose();
        Catalog.Dispose();
    }
}
