using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo.ModelCatalogSubsystem;

/// <summary>
///     Presentation state for one row of the demo's model-catalog list.
/// </summary>
/// <remarks>
///     A model's identity is fixed once the catalog reports it, but its install state and download
///     progress change while the user watches, so those live here as observable properties rather
///     than being re-projected from an immutable descriptor on every change. Keeping this per-row
///     state in its own object is what lets the list update one row's progress bar without
///     rebuilding, and losing the scroll position of, the whole list.
///     <para>
///     Not thread-safe: expected to be mutated from the UI thread only.
///     </para>
/// </remarks>
public sealed partial class ModelListItemViewModel : ObservableObject
{
    /// <summary>
    ///     Gets the library identifier used to request this model's download.
    /// </summary>
    public string Id { get; }

    /// <summary>
    ///     Gets the human-readable model name shown in the list.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    ///     Gets whether this model performs recognition or synthesis.
    /// </summary>
    public SpeechModelRole Role { get; }

    /// <summary>
    ///     Gets or sets this model's current install state as last reported or observed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    [NotifyPropertyChangedFor(nameof(IsDownloading))]
    private SpeechModelState _state;

    /// <summary>
    ///     Gets or sets the fraction of this model's download that has completed, from
    ///     <c>0.0</c> to <c>1.0</c>, or <see langword="null"/> when no measurable progress is
    ///     available (no download in flight, or a server that did not report a content length).
    /// </summary>
    [ObservableProperty]
    private double? _progressFraction;

    /// <summary>
    ///     Gets or sets the explanation of the most recent failed download attempt, or
    ///     <see langword="null"/> when the last attempt did not fail.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailureMessage))]
    private string? _failureMessage;

    /// <summary>
    ///     Gets a value indicating whether a failure explanation is available to display.
    /// </summary>
    public bool HasFailureMessage => !string.IsNullOrEmpty(FailureMessage);

    /// <summary>
    ///     Gets a value indicating whether a download may be started for this model right now.
    /// </summary>
    /// <remarks>
    ///     A model that is already installed or already downloading must not be re-requested; a
    ///     previously failed one must be, since retrying is the natural user response to a
    ///     transient network failure.
    /// </remarks>
    public bool CanDownload =>
        State is SpeechModelState.NotDownloaded or SpeechModelState.FailedOrCorrupt;

    /// <summary>
    ///     Gets a value indicating whether a download is currently in flight for this model.
    /// </summary>
    public bool IsDownloading => State == SpeechModelState.Downloading;

    /// <summary>
    ///     Gets the human-readable description of this model's current state.
    /// </summary>
    public string StatusText => State switch
    {
        SpeechModelState.NotDownloaded => "Not downloaded",
        SpeechModelState.Downloading => "Downloading",
        SpeechModelState.Downloaded => "Installed",
        SpeechModelState.FailedOrCorrupt => "Failed or corrupt",
        _ => State.ToString(),
    };

    /// <summary>
    ///     Initializes a new instance of the <see cref="ModelListItemViewModel"/> class from a
    ///     catalog descriptor.
    /// </summary>
    /// <param name="descriptor">
    ///     The library descriptor supplying this row's identity and initial state. Must not be
    ///     <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="descriptor"/> is <see langword="null"/>.
    /// </exception>
    public ModelListItemViewModel(SpeechModelDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        Id = descriptor.Id;
        DisplayName = descriptor.DisplayName;
        Role = descriptor.Role;
        _state = descriptor.State;
    }
}
