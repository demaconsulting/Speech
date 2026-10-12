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

    /// <summary>Gets the download size, formatted in MiB for display (for example <c>Download: 570 MiB</c>).</summary>
    public string SizeText { get; }

    /// <summary>
    ///     Gets a value indicating whether the pack offers optional emotion (mood) clips, so the emotion option and the
    ///     "Add emotions" upgrade are meaningful.
    /// </summary>
    public bool HasEmotionVariants { get; }

    /// <summary>Gets the installed size of the base voices in bytes (zero when unknown).</summary>
    private long VoicesBytes { get; }

    /// <summary>Gets the installed size of the emotion clips in bytes (zero when none or unknown).</summary>
    private long EmotionsBytes { get; }

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
    [NotifyPropertyChangedFor(nameof(InstalledSizeText))]
    [NotifyPropertyChangedFor(nameof(CanUpgradeToEmotions))]
    [NotifyPropertyChangedFor(nameof(ShowEmotionOption))]
    [NotifyPropertyChangedFor(nameof(InstallButtonText))]
    public partial SpeechModelState State { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether a fresh install also extracts the emotion (mood) clips; <see langword="true"/>
    ///     by default. Ignored when the pack is already installed (see <see cref="CanUpgradeToEmotions"/>).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstalledSizeText))]
    public partial bool IncludeEmotions { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the installed pack was extracted with its emotion clips.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(InstalledSizeText))]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    [NotifyPropertyChangedFor(nameof(CanUpgradeToEmotions))]
    [NotifyPropertyChangedFor(nameof(InstallButtonText))]
    public partial bool HasEmotions { get; set; }

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
    public bool CanInstall => (State is SpeechModelState.NotDownloaded or SpeechModelState.FailedOrCorrupt) || CanUpgradeToEmotions;

    /// <summary>
    ///     Gets a value indicating whether the pack is installed without its emotion clips and could add them (the
    ///     "Add emotions" action, which downloads the archive again).
    /// </summary>
    public bool CanUpgradeToEmotions => State == SpeechModelState.Downloaded && HasEmotionVariants && !HasEmotions;

    /// <summary>Gets a value indicating whether the "Include emotion variants" option is shown (a not-yet-installed pack that has emotions).</summary>
    public bool ShowEmotionOption => HasEmotionVariants && (State is SpeechModelState.NotDownloaded or SpeechModelState.FailedOrCorrupt);

    /// <summary>Gets the label of the install button: <c>Add emotions</c> for an upgrade, otherwise <c>Install</c>.</summary>
    public string InstallButtonText => CanUpgradeToEmotions ? "Add emotions" : "Install";

    /// <summary>
    ///     Gets a value indicating whether the next install request should extract the emotion clips: always for an
    ///     "Add emotions" upgrade, otherwise the <see cref="IncludeEmotions"/> choice.
    /// </summary>
    public bool EffectiveIncludeEmotions => HasEmotionVariants && (CanUpgradeToEmotions || IncludeEmotions);

    /// <summary>
    ///     Gets the disk space the pack uses once installed (or would use), for example
    ///     <c>Installs about 572 MiB (with emotions)</c>; empty when the pack's installed size is unknown.
    /// </summary>
    public string InstalledSizeText
    {
        get
        {
            if (VoicesBytes <= 0)
            {
                return string.Empty;
            }

            var installed = State == SpeechModelState.Downloaded;
            var withEmotions = HasEmotionVariants && (installed ? HasEmotions : IncludeEmotions);
            var bytes = withEmotions ? VoicesBytes + EmotionsBytes : VoicesBytes;
            var label = withEmotions ? "with emotions" : "voices only";
            return string.Create(CultureInfo.InvariantCulture, $"{(installed ? "Uses" : "Installs")} about {bytes / (1024.0 * 1024.0):0} MiB ({label})");
        }
    }

    /// <summary>Gets a value indicating whether the installed pack may be removed right now.</summary>
    public bool CanRemove => State == SpeechModelState.Downloaded;

    /// <summary>Gets a value indicating whether an install is currently in flight.</summary>
    public bool IsInstalling => State == SpeechModelState.Downloading;

    /// <summary>Gets the human-readable description of the pack's current state.</summary>
    public string StatusText => State switch
    {
        SpeechModelState.NotDownloaded => "Not installed",
        SpeechModelState.Downloading => "Installing",
        SpeechModelState.Downloaded when HasEmotionVariants => HasEmotions ? "Installed (with emotions)" : "Installed (voices only)",
        SpeechModelState.Downloaded => "Installed",
        SpeechModelState.FailedOrCorrupt => "Failed or corrupt",
        _ => State.ToString(),
    };

    /// <summary>
    ///     Initializes a new instance of the <see cref="ReferenceVoicePackListItemViewModel"/> class.
    /// </summary>
    /// <param name="pack">The pack descriptor supplying this row's identity. Must not be <see langword="null"/>.</param>
    /// <param name="state">The pack's current install state.</param>
    /// <param name="hasEmotions">Whether the installed pack was extracted with its emotion clips.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pack"/> is <see langword="null"/>.</exception>
    public ReferenceVoicePackListItemViewModel(ReferenceVoicePack pack, SpeechModelState state, bool hasEmotions = false)
    {
        ArgumentNullException.ThrowIfNull(pack);

        Id = pack.Id;
        DisplayName = pack.DisplayName;
        SizeText = string.Create(CultureInfo.InvariantCulture, $"Download: {pack.SizeBytes / (1024.0 * 1024.0):0} MiB");
        VoicesBytes = pack.VoicesBytes;
        EmotionsBytes = pack.EmotionsBytes;
        HasEmotionVariants = pack.EmotionsBytes > 0;
        HasEmotions = hasEmotions;
        LicenseText = $"{pack.Attribution} - {pack.LicenseName}";
        State = state;
    }
}
