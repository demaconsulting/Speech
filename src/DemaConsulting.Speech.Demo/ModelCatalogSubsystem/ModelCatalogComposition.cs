using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo.ModelCatalogSubsystem;

/// <summary>
///     One composed catalog together with the single <see cref="ReferenceVoiceLibrary"/> that was
///     created alongside it, so a catalog rebuild always replaces both as a pair.
/// </summary>
/// <remarks>
///     The Chatterbox Turbo model reads its voice list from the library when the catalog is built,
///     and the demo's pack install/uninstall commands drive the very same library, so the two must
///     share one store root and mirror. Keeping them together in one value makes that sharing explicit
///     and lets <see cref="ModelCatalogService"/> dispose both when it replaces them.
/// </remarks>
public sealed record ModelCatalogComposition
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ModelCatalogComposition"/> record.
    /// </summary>
    /// <param name="catalog">The composed model catalog. Must not be <see langword="null"/>.</param>
    /// <param name="voiceLibrary">The reference-voice library shared with <paramref name="catalog"/>. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="catalog"/> or <paramref name="voiceLibrary"/> is <see langword="null"/>.
    /// </exception>
    public ModelCatalogComposition(SpeechModelCatalog catalog, ReferenceVoiceLibrary voiceLibrary)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(voiceLibrary);

        Catalog = catalog;
        VoiceLibrary = voiceLibrary;
    }

    /// <summary>Gets the composed model catalog.</summary>
    public SpeechModelCatalog Catalog { get; }

    /// <summary>Gets the reference-voice library shared with <see cref="Catalog"/>.</summary>
    public ReferenceVoiceLibrary VoiceLibrary { get; }
}
