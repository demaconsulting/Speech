using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo.ModelCatalogSubsystem;

/// <summary>
///     Presentation state for one row of the model-catalog panel's "Reference voices" list: one
///     installable reference-voice pack.
/// </summary>
/// <remarks>
///     Mirrors <see cref="ModelListItemViewModel"/> for packs: identity is fixed, while install
///     state, progress and failure text change while the user watches. The row also carries the
///     pack's license, attribution and the library's consent notice, which the panel shows beside
///     the Install button so a user sees them before downloading. Not thread-safe: UI thread only.
/// </remarks>
public sealed partial class ReferenceVoicePackListItemViewModel : ObservableObject
{
    /// <summary>Gets the pack identifier used to request installation.</summary>
    public string Id { get; }

    /// <summary>Gets the human-readable pack name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the download size, formatted in MiB for display.</summary>
    public string SizeText { get; }

    /// <summary>Gets the license name and attribution line, shown with the pack.</summary>
    public string LicenseText { get; }

    /// <summary>Gets the consent notice shown with every voice pack.</summary>
    public string ConsentNotice { get; } = ReferenceVoiceLibrary.ConsentNotice;

    /// <summary>
    ///     Gets or sets the pack's current install state as last reported or observed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    [NotifyPropertyChangedFor(nameof(CanRemove))]
    [NotifyPropertyChangedFor(nameof(IsInstalling))]
    public partial SpeechModelState State { get; set; }

    /// <summary>
    ///     Gets or sets the fraction of the download that has completed (<c>0.0</c> to <c>1.0</c>),
    ///     or <see langword="null"/> when no measurable progress is available.
    /// </summary>
    [ObservableProperty]
    public partial double? ProgressFraction { get; set; }

    /// <summary>
    ///     Gets or sets the explanation of the most recent failed attempt, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailureMessage))]
    public partial string? FailureMessage { get; set; }

    /// <summary>Gets a value indicating whether a failure explanation is available to display.</summary>
    public bool HasFailureMessage => !string.IsNullOrEmpty(FailureMessage);

    /// <summary>Gets a value indicating whether an install may be started right now.</summary>
    public bool CanInstall => State is SpeechModelState.NotDownloaded or SpeechModelState.FailedOrCorrupt;

    /// <summary>Gets a value indicating whether the installed pack may be removed right now.</summary>
    public bool CanRemove => State == SpeechModelState.Downloaded;

    /// <summary>Gets a value indicating whether an install is currently in flight.</summary>
    public bool IsInstalling => State == SpeechModelState.Downloading;

    /// <summary>Gets the human-readable description of the pack's current state.</summary>
    public string StatusText => State switch
    {
        SpeechModelState.NotDownloaded => "Not installed",
        SpeechModelState.Downloading => "Installing",
        SpeechModelState.Downloaded => "Installed",
        SpeechModelState.FailedOrCorrupt => "Failed or corrupt",
        _ => State.ToString(),
    };

    /// <summary>
    ///     Initializes a new instance of the <see cref="ReferenceVoicePackListItemViewModel"/> class.
    /// </summary>
    /// <param name="pack">The pack descriptor supplying this row's identity. Must not be <see langword="null"/>.</param>
    /// <param name="state">The pack's current install state.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pack"/> is <see langword="null"/>.</exception>
    public ReferenceVoicePackListItemViewModel(ReferenceVoicePack pack, SpeechModelState state)
    {
        ArgumentNullException.ThrowIfNull(pack);

        Id = pack.Id;
        DisplayName = pack.DisplayName;
        SizeText = string.Create(CultureInfo.InvariantCulture, $"{pack.SizeBytes / (1024.0 * 1024.0):0} MiB download");
        LicenseText = $"{pack.Attribution} - {pack.LicenseName}";
        State = state;
    }
}
