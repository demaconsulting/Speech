namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Per-user speech model storage, download/install orchestration, and the empty-by-default,
///     host-populated catalog of speech-to-text and text-to-speech models.
/// </summary>
/// <remarks>
///     Contains <see cref="SpeechModelStore"/>'s atomic install/repair/uninstall of per-user
///     model files, the queued download-verify-install pipeline and its mockable HTTP download
///     seam, the typed tunable-parameter descriptors and common <see cref="ISpeechModel"/>
///     contract (with its <see cref="IRecognitionModel"/>/<see cref="ISynthesisModel"/>
///     role-marker interfaces), and <see cref="SpeechModelCatalog"/>'s enumeration of a known-model
///     list alongside install state. This library core ships zero built-in models; a host
///     populates the catalog by calling <see cref="SpeechModelCatalog.AddModels"/> directly or
///     through an extension method such as the sibling <c>DemaConsulting.Speech.Sherpa</c>
///     package's <c>AddSherpaModels</c>, which supplies the real, concrete model-backing classes.
/// </remarks>
internal static class NamespaceDoc
{
}
