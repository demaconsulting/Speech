using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.Speech.Demo;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo.ModelCatalogSubsystem;

/// <summary>
///     Presentation state for the demo's model catalog and download panel.
/// </summary>
/// <remarks>
///     This ViewModel proves that a host application can build a complete model-management page
///     against the library's generic catalog contract alone, with no knowledge of any specific
///     model. Every model, state, and download outcome comes from the injected
///     <see cref="IModelCatalogService"/> seam, which is what makes the panel testable against
///     controlled data, including an intentionally empty catalog.
///     <para>
///     An empty catalog (whether from a test double or a real service configured with no models)
///     renders an explicit explanation rather than a blank list a user would read as a bug. See
///     <see cref="EmptyCatalogMessage"/>.
///     </para>
///     <para>
///     Not thread-safe: expected to be used from the UI thread. Download progress is marshalled
///     back through the same synchronization context the download was started on. While a model
///     download or voice-pack install is in flight, <see cref="RefreshAsync"/>, <see cref="ApplyMirrorAsync"/>
///     and the voice-pack commands are refused, because they would rebuild the catalog or the rows
///     the transfer depends on.
///     </para>
/// </remarks>
public sealed partial class ModelCatalogViewModel : ObservableObject
{
    /// <summary>The message shown when the catalog reports no known models at all.</summary>
    public const string EmptyCatalogMessage =
        "No speech models are currently known to this catalog, so the list is empty. " +
        "This can happen if the model catalog service was configured with no models; the panel " +
        "will list models automatically once the catalog knows about any.";

    /// <summary>The explanation shown when a voice-pack action is refused because a transfer is in flight.</summary>
    public const string VoiceBusyMessage =
        "Wait for the current model or voice-pack download to finish (or cancel it) and try again: " +
        "installing or removing a voice pack rebuilds the catalog, which would abort it.";

    /// <summary>The seam supplying the model list and performing downloads.</summary>
    private readonly IModelCatalogService _catalogService;

    /// <summary>
    ///     Gets the rows currently displayed in the catalog list.
    /// </summary>
    public ObservableCollection<ModelListItemViewModel> Models { get; } = [];

    /// <summary>
    ///     Gets the rows of the "Reference voices" section: one per installable reference-voice pack.
    ///     Empty when the catalog service has no reference-voice support.
    /// </summary>
    public ObservableCollection<ReferenceVoicePackListItemViewModel> VoicePacks { get; } = [];

    /// <summary>Gets a value indicating whether the "Reference voices" section has any pack to show.</summary>
    public bool HasVoicePacks => VoicePacks.Count > 0;

    /// <summary>
    ///     Gets the user folder where the user may drop their own <c>.wav</c>/<c>.flac</c> voices, or
    ///     <see langword="null"/> when unavailable.
    /// </summary>
    public string? UserVoicesDirectory => _catalogService.UserVoicesDirectory;

    /// <summary>Gets the consent notice shown with the reference-voice section.</summary>
    public string VoiceConsentNotice { get; } = ReferenceVoiceLibrary.ConsentNotice;

    /// <summary>
    ///     Gets or sets the row the user has selected, or <see langword="null"/> when none is
    ///     selected.
    /// </summary>
    [ObservableProperty]
    public partial ModelListItemViewModel? SelectedModel { get; set; }

    /// <summary>
    ///     Gets a value indicating whether the catalog reported at least one known model.
    /// </summary>
    public bool HasModels => Models.Count > 0;

    /// <summary>
    ///     Gets a value indicating whether the explanatory empty-catalog message should be shown.
    /// </summary>
    public bool IsCatalogEmpty => Models.Count == 0;

    /// <summary>
    ///     Gets or sets the mirror base URL the user has typed, or <see langword="null"/>/empty
    ///     to revert to each model's own declared public download URI.
    /// </summary>
    [ObservableProperty]
    public partial string? MirrorUrl { get; set; }

    /// <summary>
    ///     Gets or sets the HTTP Basic username the user has typed for the mirror, or
    ///     <see langword="null"/> when the mirror needs no Basic credential.
    /// </summary>
    [ObservableProperty]
    public partial string? MirrorUser { get; set; }

    /// <summary>
    ///     Gets or sets the HTTP Basic password the user has typed for the mirror, or
    ///     <see langword="null"/> when the mirror needs no Basic credential.
    /// </summary>
    [ObservableProperty]
    public partial string? MirrorPassword { get; set; }

    /// <summary>
    ///     Gets or sets the bearer token the user has typed for the mirror, or
    ///     <see langword="null"/> when the mirror needs no bearer token.
    /// </summary>
    [ObservableProperty]
    public partial string? MirrorBearerToken { get; set; }

    /// <summary>
    ///     Gets or sets the result message from the most recent <see cref="ApplyMirrorAsync"/>
    ///     attempt, or <see langword="null"/> before one has been made.
    /// </summary>
    [ObservableProperty]
    public partial string? MirrorStatusMessage { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether <see cref="MirrorStatusMessage"/> describes a
    ///     rejected mirror configuration rather than a successfully applied one.
    /// </summary>
    [ObservableProperty]
    public partial bool MirrorHasError { get; set; }

    /// <summary>
    ///     Gets a value indicating whether <see cref="MirrorStatusMessage"/> has content to show.
    /// </summary>
    public bool HasMirrorStatusMessage => !string.IsNullOrEmpty(MirrorStatusMessage);

    /// <summary>
    ///     Gets a value indicating whether <see cref="MirrorStatusMessage"/> describes a
    ///     successfully applied mirror configuration.
    /// </summary>
    public bool MirrorAppliedSuccessfully => HasMirrorStatusMessage && !MirrorHasError;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ModelCatalogViewModel"/> class and loads
    ///     the current catalog contents.
    /// </summary>
    /// <param name="catalogService">
    ///     The catalog seam to read models from and request downloads through. Must not be
    ///     <see langword="null"/>.
    /// </param>
    /// <param name="initialMirrorUrl">
    ///     A mirror base URL to pre-populate the mirror-settings panel with (for example one
    ///     supplied via launch-time arguments), or <see langword="null"/> to start empty.
    /// </param>
    /// <param name="initialMirrorUser">The HTTP Basic username to pre-populate, or <see langword="null"/>.</param>
    /// <param name="initialMirrorPassword">The HTTP Basic password to pre-populate, or <see langword="null"/>.</param>
    /// <param name="initialMirrorBearerToken">The bearer token to pre-populate, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="catalogService"/> is <see langword="null"/>.
    /// </exception>
    public ModelCatalogViewModel(
        IModelCatalogService catalogService,
        string? initialMirrorUrl = null,
        string? initialMirrorUser = null,
        string? initialMirrorPassword = null,
        string? initialMirrorBearerToken = null)
    {
        ArgumentNullException.ThrowIfNull(catalogService);

        _catalogService = catalogService;
        MirrorUrl = initialMirrorUrl;
        MirrorUser = initialMirrorUser;
        MirrorPassword = initialMirrorPassword;
        MirrorBearerToken = initialMirrorBearerToken;
        RefreshCore();

        // A constructor cannot await: the installed packs' emotion flags are read in the background (the task never
        // faults) and a caller that needs them settled awaits InitialLoadTask
        InitialLoadTask = LoadPackEmotionsAsync(CancellationToken.None);
    }

    /// <summary>
    ///     Gets a task that completes when the emotion flags of the packs that were installed at construction
    ///     have been read. It never faults.
    /// </summary>
    public Task InitialLoadTask { get; }

    /// <summary>
    ///     Applies the mirror settings currently typed into <see cref="MirrorUrl"/>,
    ///     <see cref="MirrorUser"/>, <see cref="MirrorPassword"/>, and
    ///     <see cref="MirrorBearerToken"/>, replacing where every subsequent download fetches
    ///     models from. Leaving <see cref="MirrorUrl"/> blank and applying reverts to each
    ///     model's own declared public download URI.
    /// </summary>
    /// <remarks>
    ///     Validation failures (an invalid URL, a lone user or password, or a mirror this
    ///     service's catalog does not support reconfiguring), and any unexpected fault from the
    ///     service or from the catalog re-read that follows, are reported in
    ///     <see cref="MirrorStatusMessage"/> rather than thrown at the user, consistent with how
    ///     <see cref="DownloadAsync"/> reports every failure in-row instead of crashing the panel.
    ///     <para>
    ///     Refuses to apply (reporting an explained error, without calling the catalog service at
    ///     all) while any row is currently downloading. This is the mechanism that keeps
    ///     <see cref="IModelCatalogService.ApplyMirror"/>'s documented "caller must not reconfigure
    ///     the mirror while one of its own downloads is in flight" contract from ever being
    ///     violated through this panel.
    ///     </para>
    /// </remarks>
    [RelayCommand]
    public async Task ApplyMirrorAsync()
    {
        if (Models.Any(model => model.IsDownloading) || VoicePacks.Any(pack => pack.IsInstalling))
        {
            MirrorHasError = true;
            MirrorStatusMessage =
                "Cannot change the mirror while a download is in progress. Wait for it to " +
                "finish, or cancel it, and try again.";
            NotifyMirrorStatusChanged();
            return;
        }

        try
        {
            // Reconfigure first; any failure here (invalid input, an unsupported reconfiguration or an
            // unexpected seam fault) is reported as the mirror error and the rows are left alone.
            var mirror = MirrorOptionsFactory.Create(MirrorUrl, MirrorUser, MirrorPassword, MirrorBearerToken);
            _catalogService.ApplyMirror(mirror);

            MirrorHasError = false;
            MirrorStatusMessage = mirror is null
                ? "Mirror cleared: downloads now use each model's own public URI."
                : $"Mirror applied: downloads now use '{mirror.BaseUri}'.";
            NotifyMirrorStatusChanged();

            // The freshly rebuilt catalog may report different install states for models whose
            // files live under a models directory this session has not yet touched, so refresh
            // the row list from it rather than leaving stale rows on display. A failure of this
            // re-read is contained separately so it cannot escape the command.
            await ReloadAfterMirrorAsync().ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Presentation layer must not crash the application on any seam fault.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            MirrorHasError = true;
            MirrorStatusMessage = exception.Message;
            NotifyMirrorStatusChanged();
        }
    }

    /// <summary>
    ///     Re-reads the catalog after a successful mirror change, reporting a failed re-read as a mirror error.
    /// </summary>
    /// <returns>A task that completes when the rows are current or the failure has been reported.</returns>
    private async Task ReloadAfterMirrorAsync()
    {
        try
        {
            await ReloadAsync().ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Presentation layer must not crash the application on any seam fault.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            MirrorHasError = true;
            MirrorStatusMessage = $"Mirror applied, but the catalog could not be re-read: {exception.Message}";
            NotifyMirrorStatusChanged();
        }
    }

    /// <summary>Announces the derived mirror-status display flags.</summary>
    private void NotifyMirrorStatusChanged()
    {
        OnPropertyChanged(nameof(HasMirrorStatusMessage));
        OnPropertyChanged(nameof(MirrorAppliedSuccessfully));
    }

    /// <summary>
    ///     Re-reads the catalog, rebuilding the displayed rows from the library's current view of
    ///     which models exist and what state each is in.
    /// </summary>
    /// <remarks>
    ///     Rows are rebuilt rather than merged because the library's catalog is the single source
    ///     of truth for both membership and state; keeping stale rows alive across a refresh would
    ///     let the panel show a model the library no longer reports.
    ///     <para>
    ///     Refused (leaving the rows untouched) while any model download or voice-pack install is in
    ///     flight, because rebuilding the rows would discard the row that is showing that transfer's
    ///     progress and outcome. The same busy rule already guards <see cref="ApplyMirrorAsync"/> and the
    ///     voice-pack commands.
    ///     </para>
    /// </remarks>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsTransferInFlight())
        {
            return;
        }

        await ReloadAsync().ConfigureAwait(true);
    }

    /// <summary>
    ///     Gets a value indicating whether a model download or voice-pack install is currently in flight.
    /// </summary>
    /// <returns><see langword="true"/> when any row is downloading or installing.</returns>
    private bool IsTransferInFlight() =>
        Models.Any(model => model.IsDownloading) || VoicePacks.Any(pack => pack.IsInstalling);

    /// <summary>
    ///     Rebuilds the rows and reads the installed packs' emotion flags without the busy check; used by the
    ///     commands that have just finished (or deliberately completed) the transfer themselves.
    /// </summary>
    /// <returns>A task that completes when the rows and flags are current.</returns>
    private async Task ReloadAsync()
    {
        RefreshCore();
        await LoadPackEmotionsAsync(CancellationToken.None).ConfigureAwait(true);
    }

    /// <summary>
    ///     Rebuilds the model and voice-pack rows from the catalog service synchronously (pure in-memory reads);
    ///     the installed packs' emotion flags, which need file I/O, are filled in by
    ///     <see cref="LoadPackEmotionsAsync"/>.
    /// </summary>
    private void RefreshCore()
    {
        var previousId = SelectedModel?.Id;

        Models.Clear();
        foreach (var model in _catalogService.Enumerate()
            .Select(static descriptor => new ModelListItemViewModel(descriptor)))
        {
            Models.Add(model);
        }

        SelectedModel = previousId is null
            ? Models.FirstOrDefault()
            : Models.FirstOrDefault(model => string.Equals(model.Id, previousId, StringComparison.Ordinal))
              ?? Models.FirstOrDefault();

        VoicePacks.Clear();
        foreach (var pack in _catalogService.ReferenceVoicePacks)
        {
            var packState = _catalogService.GetReferenceVoicePackState(pack.Id);
            VoicePacks.Add(new ReferenceVoicePackListItemViewModel(pack, packState, hasEmotions: false));
        }

        OnPropertyChanged(nameof(HasModels));
        OnPropertyChanged(nameof(IsCatalogEmpty));
        OnPropertyChanged(nameof(HasVoicePacks));
        OnPropertyChanged(nameof(UserVoicesDirectory));
    }

    /// <summary>
    ///     Reads, asynchronously, whether each installed voice-pack row has its emotion clips and updates the row.
    /// </summary>
    /// <param name="cancellationToken">A token that aborts the reads.</param>
    /// <returns>A task that completes when every installed row has been updated; it never faults.</returns>
    private async Task LoadPackEmotionsAsync(CancellationToken cancellationToken)
    {
        foreach (var row in VoicePacks.Where(static pack => pack.State == SpeechModelState.Downloaded).ToList())
        {
            try
            {
                row.HasEmotions = await _catalogService
                    .ReferenceVoicePackHasEmotionsAsync(row.Id, cancellationToken)
                    .ConfigureAwait(true);
            }
#pragma warning disable CA1031 // Presentation layer must not crash the application on any seam fault.
            catch (Exception)
#pragma warning restore CA1031
            {
                // The flag stays false when the service cannot report it
            }
        }
    }

    /// <summary>
    ///     Downloads one model, reflecting its progress and final outcome in that model's row.
    /// </summary>
    /// <param name="model">
    ///     The row to download, or <see langword="null"/> when the command was invoked with no
    ///     row (which is ignored).
    /// </param>
    /// <param name="cancellationToken">
    ///     A token supplied by the command infrastructure that cancels an in-flight download.
    /// </param>
    /// <returns>A task that completes when the download attempt has finished and been reported.</returns>
    /// <remarks>
    ///     Every failure mode is surfaced in the row rather than thrown at the user: an honest
    ///     non-installed outcome, an unexpected exception from the seam, and cancellation each map
    ///     to a state and an explanatory message. That mirrors the library's own contract, where a
    ///     failed download is an ordinary outcome that must never disturb an existing install.
    /// </remarks>
    [RelayCommand]
    public async Task DownloadAsync(ModelListItemViewModel? model, CancellationToken cancellationToken)
    {
        if (model is null || !model.CanDownload)
        {
            return;
        }

        // A voice-pack install rebuilds the catalog on completion, which would abort this download.
        if (VoicePacks.Any(pack => pack.IsInstalling))
        {
            model.FailureMessage = VoiceBusyMessage;
            return;
        }

        // Enter the downloading state up front so the row's button disables and its progress bar
        // appears before the first byte arrives.
        model.FailureMessage = null;
        model.ProgressFraction = null;
        model.State = SpeechModelState.Downloading;

        var progress = new Progress<SpeechModelDownloadProgress>(
            value => model.ProgressFraction = value.FractionComplete);

        try
        {
            var result = await _catalogService
                .DownloadAsync(model.Id, progress, cancellationToken)
                .ConfigureAwait(true);

            // Trust the library's reported outcome rather than assuming success: a checksum
            // mismatch or transport failure is a normal result, not an exception.
            if (result.Outcome == SpeechModelDownloadOutcome.Installed)
            {
                model.State = SpeechModelState.Downloaded;
                model.ProgressFraction = 1.0;
            }
            else
            {
                model.State = SpeechModelState.FailedOrCorrupt;
                model.ProgressFraction = null;
                model.FailureMessage = DescribeFailure(result);
            }
        }
        catch (OperationCanceledException)
        {
            // The user asked to stop; the library guarantees nothing was installed, so report the
            // model as simply not downloaded rather than as failed.
            model.State = SpeechModelState.NotDownloaded;
            model.ProgressFraction = null;
            model.FailureMessage = "Download canceled.";
        }
#pragma warning disable CA1031 // Presentation layer must not crash the application on any seam fault.
        // Intentionally broad: this command is a UI fault-isolation boundary, so an unexpected
        // catalog seam failure must be reported in-row instead of tearing down the whole demo.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            model.State = SpeechModelState.FailedOrCorrupt;
            model.ProgressFraction = null;
            model.FailureMessage = exception.Message;
        }
    }

    /// <summary>
    ///     Downloads and installs one reference-voice pack, reflecting progress and the final outcome
    ///     in that pack's row, then refreshes the panel so the new voices are offered.
    /// </summary>
    /// <param name="pack">The row to install, or <see langword="null"/> (ignored).</param>
    /// <param name="cancellationToken">A token supplied by the command infrastructure that cancels the install.</param>
    /// <returns>A task that completes when the install attempt has finished and been reported.</returns>
    /// <remarks>
    ///     Refused (with <see cref="VoiceBusyMessage"/> on the row) while any model download is in
    ///     flight, because the catalog rebuild after a successful install would abort it. Every
    ///     failure is reported in-row rather than thrown.
    /// </remarks>
    [RelayCommand]
    public async Task InstallVoicePackAsync(ReferenceVoicePackListItemViewModel? pack, CancellationToken cancellationToken)
    {
        if (pack is null || !pack.CanInstall)
        {
            return;
        }

        if (Models.Any(model => model.IsDownloading) || VoicePacks.Any(other => other.IsInstalling))
        {
            pack.FailureMessage = VoiceBusyMessage;
            return;
        }

        // Read the choice before the state changes: an "Add emotions" upgrade always includes them
        var includeEmotions = pack.EffectiveIncludeEmotions;

        pack.FailureMessage = null;
        pack.ProgressFraction = null;
        pack.State = SpeechModelState.Downloading;

        var progress = new Progress<SpeechModelDownloadProgress>(value => pack.ProgressFraction = value.FractionComplete);

        try
        {
            var result = await _catalogService
                .InstallReferenceVoicePackAsync(pack.Id, includeEmotions, progress, cancellationToken)
                .ConfigureAwait(true);

            if (result.Outcome == SpeechModelDownloadOutcome.Installed)
            {
                // The service rebuilt the catalog (so Chatterbox re-reads its voices); re-read the rows.
                await ReloadAsync().ConfigureAwait(true);
            }
            else
            {
                await ApplyPackOutcomeAsync(pack, SpeechModelState.FailedOrCorrupt, DescribeFailure(result), cancellationToken).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            await ApplyPackOutcomeAsync(pack, SpeechModelState.NotDownloaded, "Install canceled.", CancellationToken.None).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Presentation layer must not crash the application on any seam fault.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            await ApplyPackOutcomeAsync(pack, SpeechModelState.FailedOrCorrupt, exception.Message, CancellationToken.None).ConfigureAwait(true);
        }
    }

    /// <summary>
    ///     Re-reads one pack's state from the catalog service after an install or upgrade that did not
    ///     succeed, and shows it together with the failure message.
    /// </summary>
    /// <param name="pack">The row whose attempt just ended.</param>
    /// <param name="outcomeState">
    ///     The state to show when the service reports the pack is not installed (a failed or canceled
    ///     fresh install).
    /// </param>
    /// <param name="message">The failure or cancellation explanation to keep on the row.</param>
    /// <param name="cancellationToken">A token that aborts the state re-read.</param>
    /// <returns>A task that completes when the row has been updated.</returns>
    /// <remarks>
    ///     A failed "Add emotions" upgrade is rolled back by the library to the previous voices-only
    ///     install, so the pack is still <see cref="SpeechModelState.Downloaded"/> and must stay
    ///     removable. Trusting the service's report, rather than assuming the failure left the pack
    ///     broken, keeps the row honest; the failure message is preserved either way.
    /// </remarks>
    private async Task ApplyPackOutcomeAsync(ReferenceVoicePackListItemViewModel pack, SpeechModelState outcomeState, string message, CancellationToken cancellationToken)
    {
        var state = outcomeState;
        var hasEmotions = false;

        try
        {
            if (_catalogService.GetReferenceVoicePackState(pack.Id) == SpeechModelState.Downloaded)
            {
                state = SpeechModelState.Downloaded;
                hasEmotions = await _catalogService.ReferenceVoicePackHasEmotionsAsync(pack.Id, cancellationToken).ConfigureAwait(true);
            }
        }
#pragma warning disable CA1031 // Presentation layer must not crash the application on any seam fault.
        catch (Exception)
#pragma warning restore CA1031
        {
            // The service cannot report the pack's state; fall back to the attempt's own outcome.
        }

        pack.ProgressFraction = null;
        pack.HasEmotions = hasEmotions;
        pack.State = state;
        pack.FailureMessage = message;
    }

    /// <summary>
    ///     Removes one installed reference-voice pack and refreshes the panel so its voices disappear
    ///     from the voice choice.
    /// </summary>
    /// <param name="pack">The row to remove, or <see langword="null"/> (ignored).</param>
    /// <param name="cancellationToken">A token supplied by the command infrastructure.</param>
    /// <returns>A task that completes when the removal has finished and been reported.</returns>
    /// <remarks>
    ///     The removal runs asynchronously so the UI thread is never blocked; if an install of the same pack is
    ///     in flight the library waits for it before removing (this panel also refuses to start one while an install
    ///     or download is running).
    /// </remarks>
    [RelayCommand]
    public async Task RemoveVoicePackAsync(ReferenceVoicePackListItemViewModel? pack, CancellationToken cancellationToken)
    {
        if (pack is null || !pack.CanRemove)
        {
            return;
        }

        if (Models.Any(model => model.IsDownloading) || VoicePacks.Any(other => other.IsInstalling))
        {
            pack.FailureMessage = VoiceBusyMessage;
            return;
        }

        try
        {
            await _catalogService.UninstallReferenceVoicePackAsync(pack.Id, cancellationToken).ConfigureAwait(true);
            await ReloadAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            pack.FailureMessage = "Removal canceled.";
        }
#pragma warning disable CA1031 // Presentation layer must not crash the application on any seam fault.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            pack.FailureMessage = exception.Message;
        }
    }

    /// <summary>
    ///     Builds the user-facing explanation for a download that did not install the model.
    /// </summary>
    /// <param name="result">The library-reported outcome. Must not be <see langword="null"/>.</param>
    /// <returns>A short explanation suitable for display beside the failed row.</returns>
    private static string DescribeFailure(SpeechModelDownloadResult result) => result.Outcome switch
    {
        SpeechModelDownloadOutcome.ChecksumMismatch =>
            "The downloaded files did not match their expected checksums and were discarded.",
        SpeechModelDownloadOutcome.Failed =>
            result.Error?.Message ?? "The download could not be completed.",
        _ => $"The download reported '{result.Outcome}'.",
    };
}
