namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Per-user speech model storage, download/install orchestration, and the catalog of
///     concrete speech-to-text and text-to-speech models the library ships.
/// </summary>
/// <remarks>
///     Contains <see cref="SpeechModelStore"/>'s atomic install/repair/uninstall of per-user
///     model files, the queued download-verify-install pipeline and its mockable HTTP download
///     seam, the typed tunable-parameter descriptors and common <see cref="ISpeechModel"/>
///     contract (with its <see cref="IRecognitionModel"/>/<see cref="ISynthesisModel"/>
///     role-marker interfaces), <see cref="SpeechModelCatalog"/>'s enumeration of the compiled-in
///     known-model registry alongside install state, and the real, concrete model-backing
///     classes - one per shipped model, each owning its own download descriptor and any
///     model-specific configuration or output correction.
/// </remarks>
internal static class NamespaceDoc
{
}
