using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    ///     Initializes a new instance of the <see cref="ModelCatalogViewModel"/> class and loads
    ///     the current catalog contents.
    /// </summary>
    /// <param name="catalogService">
    ///     The catalog seam to read models from and request downloads through. Must not be
    ///     <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="catalogService"/> is <see langword="null"/>.
    /// </exception>
    public ModelCatalogViewModel(IModelCatalogService catalogService)
    {
        ArgumentNullException.ThrowIfNull(catalogService);

        _catalogService = catalogService;
        Refresh();
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
