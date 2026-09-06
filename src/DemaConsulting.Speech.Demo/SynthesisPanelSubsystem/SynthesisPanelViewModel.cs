using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Demo.DeviceSelectionSubsystem;
using DemaConsulting.Speech.Demo.ModelCatalogSubsystem;
using DemaConsulting.Speech.Demo.ModelSettingsSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Demo.SynthesisPanelSubsystem;

/// <summary>
///     Presentation state for the demo's text-to-speech panel.
/// </summary>
/// <remarks>
///     This ViewModel proves that a host can build a complete text-to-speech page against the
///     library's public synthesis contract alone: choosing an installed synthesis model, viewing
///     and adjusting its declared tunable parameters through the embedded
///     <see cref="Settings"/>, and speaking arbitrary text - including inline Natural Language
///     Audio Tags drawn from the library's closed, published vocabulary - with honest reporting
///     of every unavailable state (no installed synthesis model, no playback device, or an engine
///     fault) rather than a silent failure or a crash.
///     <para>
///     Every library entry point this panel reaches is reached through an injected seam
///     (<see cref="IModelCatalogService"/>, <see cref="IAudioDeviceService"/>,
///     <see cref="ISynthesizerSessionFactory"/>), which is what makes Play/Stop lifecycle and
///     every error path verifiable with no downloaded model, no native runtime, and no real
///     speakers.
///     </para>
///     <para>
///     Not thread-safe: expected to be used from the UI thread.
///     </para>
///     <para>
///     This ViewModel subscribes to <see cref="IModelCatalogService.ModelInstalled"/> in its
///     constructor so a newly downloaded synthesis model installed from the Model Catalog panel
///     is picked up automatically, without a manual Refresh click or app restart. The handler
///     ignores events for any other <see cref="SpeechModelRole"/> and marshals onto the UI thread
///     (captured at construction) before calling <see cref="Refresh"/>, mirroring
///     <see cref="RecognitionPanelSubsystem.RecognitionPanelViewModel"/>'s identical pattern.
///     <see cref="Dispose"/> unsubscribes this handler.
///     </para>
/// </remarks>
public sealed partial class SynthesisPanelViewModel : ObservableObject, IDisposable
{
    /// <summary>The message shown when no installed synthesis model is available to choose.</summary>
    public const string NoModelsMessage =
        "No installed synthesis models were found. Install one from the Model Catalog panel to " +
        "use text-to-speech.";

    /// <summary>The message shown when Play is attempted with no model selected.</summary>
    public const string NoModelSelectedMessage = "Select an installed synthesis model to speak text.";

    /// <summary>The message shown when Play is attempted with no playback device available.</summary>
    public const string NoPlaybackDeviceMessage =
        "No audio playback device is available. Choose one on the Audio Devices panel.";

    /// <summary>The message shown when the composed synthesizer honestly reports itself unavailable.</summary>
    public const string SynthesizerUnavailableMessage =
        "The selected model's speech synthesizer is unavailable on this machine.";

    /// <summary>The message shown after playback is stopped by the user.</summary>
    public const string StoppedMessage = "Playback stopped.";

    /// <summary>Example Natural Language Audio Tags drawn from the library's closed, published vocabulary.</summary>
    public static IReadOnlyList<string> ExampleTagHints { get; } =
        AudioTagCatalog.Tags.Select(descriptor => $"[{descriptor.Aliases[0]}]").ToArray();

    /// <summary>The seam supplying every installed synthesis model.</summary>
    private readonly IModelCatalogService _catalogService;

    /// <summary>The seam used to create the real playback device to speak through.</summary>
    private readonly IAudioDeviceService _deviceService;

    /// <summary>The shared device-selection panel state supplying the chosen playback device.</summary>
    private readonly DeviceSelectionViewModel _deviceSelection;

    /// <summary>The seam used to compose a synthesizer for the selected model and device.</summary>
    private readonly ISynthesizerSessionFactory _sessionFactory;

    /// <summary>
    ///     The UI thread context captured at construction, used to marshal
    ///     <see cref="IModelCatalogService.ModelInstalled"/> handling onto the UI thread
    ///     regardless of which thread raises it.
    /// </summary>
    private readonly SynchronizationContext? _catalogEventUiContext = SynchronizationContext.Current;

    /// <summary>The synthesizer currently in use by an in-flight Play, if any.</summary>
    private ISpeechSynthesizer? _activeSynthesizer;

    /// <summary>
    ///     Gets the installed synthesis models available to choose from.
    /// </summary>
    public ObservableCollection<ISpeechModel> AvailableModels { get; } = [];

    /// <summary>
    ///     Gets or sets the model chosen to speak text with, or <see langword="null"/> when none
    ///     is installed or none has been chosen.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPlay))]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    private ISpeechModel? _selectedModel;

    /// <summary>
    ///     Gets or sets the text to synthesize, which may contain inline Natural Language Audio
    ///     Tags such as <c>[whispers]</c> or <c>[short pause]</c>.
    /// </summary>
    [ObservableProperty]
    private string _text = string.Empty;

    /// <summary>
    ///     Gets or sets the current playback lifecycle state.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPlay))]
    [NotifyPropertyChangedFor(nameof(CanChangeModel))]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    private SynthesisPlaybackState _state = SynthesisPlaybackState.Idle;

    /// <summary>
    ///     Gets or sets the status message describing the current or most recently failed
    ///     attempt, or <see langword="null"/> when there is nothing to report.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string? _statusMessage;

    /// <summary>
    ///     Gets a value indicating whether <see cref="StatusMessage"/> currently has content to
    ///     display.
    /// </summary>
    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    /// <summary>
    ///     Gets the embedded settings panel for the selected model's declared tunable
    ///     parameters.
    /// </summary>
    public ModelSettingsViewModel Settings { get; } = new();

    /// <summary>
    ///     Gets a value indicating whether at least one installed synthesis model was found.
    /// </summary>
    public bool HasModels => AvailableModels.Count > 0;

    /// <summary>
    ///     Gets a value indicating whether Play may be started right now.
    /// </summary>
    public bool CanPlay => SelectedModel is not null && State is SynthesisPlaybackState.Idle or SynthesisPlaybackState.Error;

    /// <summary>
    ///     Gets a value indicating whether the model-selection control (and its embedded settings
    ///     panel) may be changed right now.
    /// </summary>
    /// <remarks>
    ///     Mirrors <see cref="CanPlay"/>'s own state test: <see langword="false"/> while
    ///     <see cref="SynthesisPlaybackState.Synthesizing"/> or <see cref="SynthesisPlaybackState.Playing"/>
    ///     (both are treated as one "busy" band, since <c>Synthesizing</c> is the ramp-up phase of
    ///     the same active session), so a user cannot switch the selected voice/model or its
    ///     tunable parameters mid-playback; <see langword="true"/> at
    ///     <see cref="SynthesisPlaybackState.Idle"/> and <see cref="SynthesisPlaybackState.Error"/>.
    /// </remarks>
    public bool CanChangeModel => State is SynthesisPlaybackState.Idle or SynthesisPlaybackState.Error;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SynthesisPanelViewModel"/> class and loads
    ///     the currently installed synthesis models.
    /// </summary>
    /// <param name="catalogService">The catalog seam to read installed models from. Must not be <see langword="null"/>.</param>
    /// <param name="deviceService">The device seam used to create the playback device. Must not be <see langword="null"/>.</param>
    /// <param name="deviceSelection">The shared device-selection panel state. Must not be <see langword="null"/>.</param>
    /// <param name="sessionFactory">The seam used to compose a synthesizer. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is <see langword="null"/>.</exception>
    public SynthesisPanelViewModel(
        IModelCatalogService catalogService,
        IAudioDeviceService deviceService,
        DeviceSelectionViewModel deviceSelection,
        ISynthesizerSessionFactory sessionFactory)
    {
        ArgumentNullException.ThrowIfNull(catalogService);
        ArgumentNullException.ThrowIfNull(deviceService);
        ArgumentNullException.ThrowIfNull(deviceSelection);
        ArgumentNullException.ThrowIfNull(sessionFactory);

        _catalogService = catalogService;
        _deviceService = deviceService;
        _deviceSelection = deviceSelection;
        _sessionFactory = sessionFactory;

        _catalogService.ModelInstalled += OnModelInstalled;

        Refresh();
    }

    /// <summary>
    ///     Re-reads the catalog for installed synthesis models, preserving the current selection
    ///     where it is still installed.
    /// </summary>
    [RelayCommand]
    public void Refresh()
    {
        var previousId = SelectedModel?.Id;

        AvailableModels.Clear();
        foreach (var descriptor in _catalogService.Enumerate())
        {
            if (descriptor.Role == SpeechModelRole.Synthesis && descriptor.State == SpeechModelState.Downloaded)
            {
                AvailableModels.Add(descriptor.Model);
            }
        }

        SelectedModel = previousId is null
            ? AvailableModels.FirstOrDefault()
            : AvailableModels.FirstOrDefault(model => string.Equals(model.Id, previousId, StringComparison.Ordinal))
              ?? AvailableModels.FirstOrDefault();

        OnPropertyChanged(nameof(HasModels));
    }

    /// <summary>
    ///     Applies a newly selected model's declared parameters to the embedded settings panel.
    /// </summary>
    /// <param name="value">The newly selected model.</param>
    partial void OnSelectedModelChanged(ISpeechModel? value) => Settings.Model = value;

    /// <summary>
    ///     Synthesizes and speaks <see cref="Text"/> through the currently selected playback
    ///     device, using the currently selected model.
    /// </summary>
    /// <param name="cancellationToken">
    ///     The token the generated command supplies; canceling it (via <see cref="StopCommand"/>)
    ///     stops playback deterministically.
    /// </param>
    /// <returns>A task that completes once playback finishes, is stopped, or faults.</returns>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    private async Task PlayAsync(CancellationToken cancellationToken)
    {
        var selectedModel = SelectedModel;
        if (selectedModel is null)
        {
            StatusMessage = NoModelSelectedMessage;
            State = SynthesisPlaybackState.Error;
            return;
        }

        var playbackDevice = _deviceService.CreatePlaybackDevice(_deviceSelection.PlaybackSelection);
        if (!playbackDevice.IsAvailable)
        {
            StatusMessage = NoPlaybackDeviceMessage;
            State = SynthesisPlaybackState.Error;
            return;
        }

        var synthesizer = _sessionFactory.Create(selectedModel, playbackDevice, Settings.BuildValueBag());
        if (!synthesizer.IsAvailable)
        {
            synthesizer.Dispose();
            StatusMessage = SynthesizerUnavailableMessage;
            State = SynthesisPlaybackState.Error;
            return;
        }

        _activeSynthesizer = synthesizer;
        try
        {
            StatusMessage = null;
            State = SynthesisPlaybackState.Synthesizing;
            State = SynthesisPlaybackState.Playing;
            await synthesizer.SpeakAsync(Text, cancellationToken).ConfigureAwait(true);
            State = SynthesisPlaybackState.Idle;
        }
        catch (OperationCanceledException)
        {
            State = SynthesisPlaybackState.Idle;
            StatusMessage = StoppedMessage;
        }
        catch (SpeechSynthesizerUnavailableException ex)
        {
            State = SynthesisPlaybackState.Error;
            StatusMessage = ex.Message;
        }
        finally
        {
            _activeSynthesizer = null;
            synthesizer.Dispose();
        }
    }

    /// <summary>
    ///     Stops an in-flight Play session deterministically. A safe no-op when nothing is
    ///     playing.
    /// </summary>
    [RelayCommand]
    private void Stop()
    {
        _activeSynthesizer?.Stop();
        PlayCommand.Cancel();
    }

    /// <summary>
    ///     Marshals a matching-role <see cref="IModelCatalogService.ModelInstalled"/> event onto
    ///     the UI thread and calls <see cref="Refresh"/>; ignored when the installed model's role
    ///     is not <see cref="SpeechModelRole.Synthesis"/>.
    /// </summary>
    /// <param name="sender">The raising catalog service. Unused.</param>
    /// <param name="e">The event carrying the installed model's id and role.</param>
    private void OnModelInstalled(object? sender, ModelInstalledEventArgs e)
    {
        if (e.Role != SpeechModelRole.Synthesis)
        {
            return;
        }

        if (_catalogEventUiContext is null)
        {
            Refresh();
        }
        else
        {
            _catalogEventUiContext.Post(_ => Refresh(), null);
        }
    }

    /// <summary>
    ///     Unsubscribes from <see cref="IModelCatalogService.ModelInstalled"/> and, defensively,
    ///     disposes an active synthesizer if one is still held (ordinarily released by
    ///     <see cref="PlayAsync"/>'s own <c>finally</c> block, but no longer guaranteed once this
    ///     ViewModel can be disposed independently of any in-flight Play).
    /// </summary>
    public void Dispose()
    {
        _catalogService.ModelInstalled -= OnModelInstalled;
        _activeSynthesizer?.Dispose();
        _activeSynthesizer = null;
    }
}
