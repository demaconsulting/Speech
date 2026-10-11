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
/// <param name="Catalog">The composed model catalog. Must not be <see langword="null"/>.</param>
/// <param name="VoiceLibrary">The reference-voice library shared with <paramref name="Catalog"/>. Must not be <see langword="null"/>.</param>
public sealed record ModelCatalogComposition(SpeechModelCatalog Catalog, ReferenceVoiceLibrary VoiceLibrary);
