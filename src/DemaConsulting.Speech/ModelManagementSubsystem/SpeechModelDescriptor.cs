namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     A catalog read-model pairing one <see cref="ISpeechModel"/> with its current
///     <see cref="SpeechModelState"/>, as returned by <see cref="SpeechModelCatalog.Enumerate"/>.
/// </summary>
/// <remarks>
///     This type is a pure, immutable snapshot: it does not itself query
///     <see cref="SpeechModelStore"/> or <see cref="SpeechModelDownloader"/> and never changes
///     after construction. A host wanting an up-to-date state calls
///     <see cref="SpeechModelCatalog.Enumerate"/> again, which builds a fresh snapshot.
/// </remarks>
public sealed record SpeechModelDescriptor
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechModelDescriptor"/> record.
    /// </summary>
    /// <param name="model">The model this descriptor describes. Must not be null.</param>
    /// <param name="state">The model's install state at the moment this descriptor was built.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is <see langword="null"/>.</exception>
    public SpeechModelDescriptor(ISpeechModel model, SpeechModelState state)
    {
        ArgumentNullException.ThrowIfNull(model);

        Model = model;
        State = state;
    }

    /// <summary>Gets the model this descriptor describes.</summary>
    public ISpeechModel Model { get; }

    /// <summary>Gets the model's install state at the moment this descriptor was built.</summary>
    public SpeechModelState State { get; }

    /// <summary>Gets the model's stable identifier. Equivalent to <c>Model.Id</c>.</summary>
    public string Id => Model.Id;

    /// <summary>Gets the model's human-readable display name. Equivalent to <c>Model.DisplayName</c>.</summary>
    public string DisplayName => Model.DisplayName;

    /// <summary>Gets which speech capability the model provides. Equivalent to <c>Model.Role</c>.</summary>
    public SpeechModelRole Role => Model.Role;

    /// <summary>Gets the model's declared license name or identifier. Equivalent to <c>Model.LicenseName</c>.</summary>
    public string LicenseName => Model.LicenseName;

    /// <summary>Gets the canonical URL to the full text of the model's declared license, when known. Equivalent to <c>Model.LicenseUrl</c>.</summary>
    public Uri? LicenseUrl => Model.LicenseUrl;
}
