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
///     back through the same synchronization context the download was started on.
///     </para>
/// </remarks>
public sealed partial class ModelCatalogViewModel : ObservableObject
{
    /// <summary>The message shown when the catalog reports no known models at all.</summary>
    public const string EmptyCatalogMessage =
        "No speech models are currently known to this catalog, so the list is empty. " +
        "This can happen if the model catalog service was configured with no models; the panel " +
        "will list models automatically once the catalog knows about any.";

    /// <summary>The seam supplying the model list and performing downloads.</summary>
    private readonly IModelCatalogService _catalogService;

    /// <summary>
    ///     Gets the rows currently displayed in the catalog list.
    /// </summary>
    public ObservableCollection<ModelListItemViewModel> Models { get; } = [];

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
    ///     Gets or sets the result message from the most recent <see cref="ApplyMirror"/>
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
        Refresh();
    }

    /// <summary>
    ///     Applies the mirror settings currently typed into <see cref="MirrorUrl"/>,
    ///     <see cref="MirrorUser"/>, <see cref="MirrorPassword"/>, and
    ///     <see cref="MirrorBearerToken"/>, replacing where every subsequent download fetches
    ///     models from. Leaving <see cref="MirrorUrl"/> blank and applying reverts to each
    ///     model's own declared public download URI.
    /// </summary>
    /// <remarks>
    ///     Validation failures (an invalid URL, a lone user or password, or a mirror this
    ///     service's catalog does not support reconfiguring) are reported in
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
    public void ApplyMirror()
    {
        if (Models.Any(model => model.IsDownloading))
        {
            MirrorHasError = true;
            MirrorStatusMessage =
                "Cannot change the mirror while a download is in progress. Wait for it to " +
                "finish, or cancel it, and try again.";
            OnPropertyChanged(nameof(HasMirrorStatusMessage));
            OnPropertyChanged(nameof(MirrorAppliedSuccessfully));
            return;
        }

        try
        {
            var mirror = MirrorOptionsFactory.Create(MirrorUrl, MirrorUser, MirrorPassword, MirrorBearerToken);
            _catalogService.ApplyMirror(mirror);

            MirrorHasError = false;
            MirrorStatusMessage = mirror is null
                ? "Mirror cleared: downloads now use each model's own public URI."
                : $"Mirror applied: downloads now use '{mirror.BaseUri}'.";

            // The freshly rebuilt catalog may report different install states for models whose
            // files live under a models directory this session has not yet touched, so refresh
            // the row list from it rather than leaving stale rows on display.
            Refresh();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            MirrorHasError = true;
            MirrorStatusMessage = exception.Message;
        }
        finally
        {
            OnPropertyChanged(nameof(HasMirrorStatusMessage));
            OnPropertyChanged(nameof(MirrorAppliedSuccessfully));
        }
    }

    /// <summary>
    ///     Re-reads the catalog, rebuilding the displayed rows from the library's current view of
    ///     which models exist and what state each is in.
    /// </summary>
    /// <remarks>
    ///     Rows are rebuilt rather than merged because the library's catalog is the single source
    ///     of truth for both membership and state; keeping stale rows alive across a refresh would
    ///     let the panel show a model the library no longer reports.
    /// </remarks>
    [RelayCommand]
    public void Refresh()
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

        OnPropertyChanged(nameof(HasModels));
        OnPropertyChanged(nameof(IsCatalogEmpty));
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
