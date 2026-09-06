using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo.Tests.Fakes;

/// <summary>
///     Test-only <see cref="ISpeechModel"/> used to build catalog descriptors with controlled
///     identity, role, and state.
/// </summary>
/// <remarks>
///     The library ships no real models yet, and its catalog's model-injecting constructor is
///     internal to the library's own test assembly, so the demo's tests must supply their own
///     model instances through the demo's <c>IModelCatalogService</c> seam. That is what lets the
///     catalog panel's non-empty behavior be exercised at all rather than only its empty state.
/// </remarks>
/// <param name="id">The model identifier this fake reports.</param>
/// <param name="displayName">The human-readable name this fake reports.</param>
/// <param name="role">The capability this fake claims to provide.</param>
/// <param name="parameters">The tunable parameters this fake declares, or <see langword="null"/> for none.</param>
public sealed class FakeSpeechModel(
    string id = "fake-model",
    string displayName = "Fake Model",
    SpeechModelRole role = SpeechModelRole.Recognition,
    IReadOnlyList<ISpeechModelParameter>? parameters = null) : ISpeechModel
{
    /// <summary>A syntactically valid SHA-256 checksum; never dereferenced because these tests never download.</summary>
    private const string PlaceholderChecksum =
        "0000000000000000000000000000000000000000000000000000000000000000";

    /// <inheritdoc/>
    public string Id => id;

    /// <inheritdoc/>
    public string DisplayName => displayName;

    /// <inheritdoc/>
    public SpeechModelRole Role => role;

    /// <inheritdoc/>
    public IReadOnlyList<ISpeechModelParameter> Parameters => parameters ?? [];

    /// <inheritdoc/>
    public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.None;

    /// <inheritdoc/>
    public SpeechModelDownloadDescriptor DownloadDescriptor { get; } = new(
    [
        new SpeechModelDownloadFile(
            new Uri($"https://example.test/{id}.bin"),
            PlaceholderChecksum,
            "model.bin")
    ]);

    /// <summary>
    ///     Builds a catalog descriptor for a fake model with the supplied identity and state.
    /// </summary>
    /// <param name="id">The model identifier.</param>
    /// <param name="state">The install state the descriptor reports.</param>
    /// <param name="displayName">The human-readable model name.</param>
    /// <param name="role">The capability the model claims to provide.</param>
    /// <param name="parameters">The tunable parameters the model declares, or <see langword="null"/> for none.</param>
    /// <returns>A descriptor suitable for returning from a faked catalog service.</returns>
    public static SpeechModelDescriptor Descriptor(
        string id = "fake-model",
        SpeechModelState state = SpeechModelState.NotDownloaded,
        string displayName = "Fake Model",
        SpeechModelRole role = SpeechModelRole.Recognition,
        IReadOnlyList<ISpeechModelParameter>? parameters = null) =>
        new(new FakeSpeechModel(id, displayName, role, parameters), state);
}
