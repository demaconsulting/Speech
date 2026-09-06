using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Demo.DeviceSelectionSubsystem;
using DemaConsulting.Speech.Demo.ModelCatalogSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Demo.RecognitionPanelSubsystem;

/// <summary>
///     Presentation state for the demo's speech-to-text panel.
/// </summary>
/// <remarks>
///     This ViewModel proves that a host can build a complete streaming speech-to-text page
///     against the library's public recognition contract alone: choosing an installed
///     recognition model, starting and stopping a live transcription session, and rendering the
///     progressive provisional ("partial") and final results the library raises while streaming
///     - with honest reporting of every unavailable state (no installed recognition model, no
///     capture device, or an engine fault) rather than a silent failure or a crash.
///     <para>
///     Every library entry point this panel reaches is reached through an injected seam
///     (<see cref="IModelCatalogService"/>, <see cref="IAudioDeviceService"/>,
///     <see cref="IRecognizerSessionFactory"/>), which is what makes Start/Stop lifecycle,
///     partial-then-final transcript sequencing, and every error path verifiable with no
///     downloaded model, no native runtime, and no real microphone.
///     </para>
///     <para>
///     <see cref="ISpeechRecognizer.ResultReceived"/> is documented to raise from the
///     recognizer's own background decoding thread, so this ViewModel captures the UI thread's
///     <see cref="SynchronizationContext"/> when a session starts and posts every event through
///     it before touching any observable property, mirroring the library's own
///     <c>ConfigureAwait(true)</c> marshaling used elsewhere in this demo. Otherwise not
///     thread-safe: expected to be used from the UI thread.
///     </para>
///     <para>
///     This ViewModel also subscribes to <see cref="IModelCatalogService.ModelInstalled"/> in its
///     constructor so a newly downloaded recognition model installed from the Model Catalog panel
///     is picked up automatically, without a manual Refresh click or app restart. The handler
///     ignores events for any other <see cref="SpeechModelRole"/> and marshals onto the UI thread
///     (captured at construction) before calling <see cref="Refresh"/>, using the same
///     <see cref="SynchronizationContext"/> pattern as <see cref="ISpeechRecognizer.ResultReceived"/>
///     handling above. <see cref="Dispose"/> unsubscribes this handler.
///     </para>
/// </remarks>
public sealed partial class RecognitionPanelViewModel : ObservableObject, IDisposable
{
    /// <summary>The message shown when no installed recognition model is available to choose.</summary>
    public const string NoModelsMessage =
        "No installed recognition models were found. Install one from the Model Catalog panel to " +
        "use speech-to-text.";

    /// <summary>The message shown when Start is attempted with no model selected.</summary>
    public const string NoModelSelectedMessage = "Select an installed recognition model to start listening.";

    /// <summary>The message shown when Start is attempted with no capture device available.</summary>
    public const string NoCaptureDeviceMessage =
        "No audio capture device is available. Choose one on the Audio Devices panel.";

    /// <summary>The message shown when the composed recognizer honestly reports itself unavailable.</summary>
    public const string RecognizerUnavailableMessage =
        "The selected model's speech recognizer is unavailable on this machine.";

    /// <summary>The message shown after listening is stopped by the user.</summary>
    public const string StoppedMessage = "Listening stopped.";

    /// <summary>The seam supplying every installed recognition model.</summary>
    private readonly IModelCatalogService _catalogService;

    /// <summary>The seam used to create the real capture device to listen through.</summary>
    private readonly IAudioDeviceService _deviceService;

    /// <summary>The shared device-selection panel state supplying the chosen capture device.</summary>
    private readonly DeviceSelectionViewModel _deviceSelection;

    /// <summary>The seam used to compose a recognizer for the selected model and device.</summary>
    private readonly IRecognizerSessionFactory _sessionFactory;

    /// <summary>
    ///     The UI thread context captured at construction, used to marshal
    ///     <see cref="IModelCatalogService.ModelInstalled"/> handling onto the UI thread
    ///     regardless of which thread raises it.
    /// </summary>
    private readonly SynchronizationContext? _catalogEventUiContext = SynchronizationContext.Current;

    /// <summary>The recognizer currently streaming, if any.</summary>
    private ISpeechRecognizer? _activeRecognizer;

    /// <summary>
    ///     TEMPORARY diagnostic instrumentation for investigating a reported dropped-word bug
    ///     after a mid-sentence pause during streaming recognition (see
    ///     <see cref="CaptureDebugRecorder"/>): the active raw-capture recording session, if the
    ///     <see cref="CaptureDebugRecorder.EnvironmentVariableName"/> environment variable enabled
    ///     one for the current session, or <see langword="null"/> otherwise.
    /// </summary>
    private CaptureDebugRecorder? _captureDebugRecorder;

    /// <summary>The UI thread context captured when the active session started.</summary>
    private SynchronizationContext? _uiContext;

    /// <summary>
    ///     Gets the installed recognition models available to choose from.
    /// </summary>
    public ObservableCollection<ISpeechModel> AvailableModels { get; } = [];

    /// <summary>
    ///     Gets or sets the model chosen to listen with, or <see langword="null"/> when none is
    ///     installed or none has been chosen.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private ISpeechModel? _selectedModel;

    /// <summary>
    ///     Gets or sets the current streaming lifecycle state.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [NotifyPropertyChangedFor(nameof(CanChangeModel))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private RecognitionStreamingState _state = RecognitionStreamingState.Idle;

    /// <summary>
    ///     Gets or sets the status message describing the current or most recently failed
    ///     attempt, or <see langword="null"/> when there is nothing to report.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string? _statusMessage;

    /// <summary>
    ///     Gets or sets the trailing provisional (not-yet-final) transcript text, or an empty
    ///     string when there is no in-progress utterance.
    /// </summary>
    [ObservableProperty]
    private string _partial = string.Empty;

    /// <summary>
    ///     Gets the ordered list of committed final transcript lines.
    /// </summary>
    public ObservableCollection<string> Finals { get; } = [];

    /// <summary>
    ///     Gets a value indicating whether at least one installed recognition model was found.
    /// </summary>
    public bool HasModels => AvailableModels.Count > 0;

    /// <summary>
    ///     Gets a value indicating whether <see cref="StatusMessage"/> currently has content to
    ///     display.
    /// </summary>
    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    /// <summary>
    ///     Gets a value indicating whether Start may be invoked right now.
    /// </summary>
    public bool CanStart => SelectedModel is not null && State != RecognitionStreamingState.Listening;

    /// <summary>
    ///     Gets a value indicating whether Stop may be invoked right now.
    /// </summary>
    public bool CanStop => State == RecognitionStreamingState.Listening;

    /// <summary>
    ///     Gets a value indicating whether the model-selection control may be changed right now.
    /// </summary>
    /// <remarks>
    ///     <see langword="false"/> only while a session is actively <see cref="RecognitionStreamingState.Listening"/>,
    ///     so a user cannot switch the recognition model mid-session; <see langword="true"/> at
    ///     <see cref="RecognitionStreamingState.Idle"/> and <see cref="RecognitionStreamingState.Error"/>.
    /// </remarks>
    public bool CanChangeModel => State != RecognitionStreamingState.Listening;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecognitionPanelViewModel"/> class and
    ///     loads the currently installed recognition models.
    /// </summary>
    /// <param name="catalogService">The catalog seam to read installed models from. Must not be <see langword="null"/>.</param>
    /// <param name="deviceService">The device seam used to create the capture device. Must not be <see langword="null"/>.</param>
    /// <param name="deviceSelection">The shared device-selection panel state. Must not be <see langword="null"/>.</param>
    /// <param name="sessionFactory">The seam used to compose a recognizer. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is <see langword="null"/>.</exception>
    public RecognitionPanelViewModel(
        IModelCatalogService catalogService,
        IAudioDeviceService deviceService,
        DeviceSelectionViewModel deviceSelection,
        IRecognizerSessionFactory sessionFactory)
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
    ///     Re-reads the catalog for installed recognition models, preserving the current
    ///     selection where it is still installed.
    /// </summary>
    [RelayCommand]
    public void Refresh()
    {
        var previousId = SelectedModel?.Id;

        AvailableModels.Clear();
        foreach (var descriptor in _catalogService.Enumerate())
        {
            if (descriptor.Role == SpeechModelRole.Recognition && descriptor.State == SpeechModelState.Downloaded)
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
    ///     Starts a streaming transcription session for the currently selected model through the
    ///     currently selected capture device.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        var selectedModel = SelectedModel;
        if (selectedModel is null)
        {
            StatusMessage = NoModelSelectedMessage;
            State = RecognitionStreamingState.Error;
            return;
        }

        var captureDevice = _deviceService.CreateCaptureDevice(_deviceSelection.CaptureSelection);
        if (!captureDevice.IsAvailable)
        {
            StatusMessage = NoCaptureDeviceMessage;
            State = RecognitionStreamingState.Error;
            return;
        }

        var recognizer = _sessionFactory.Create(selectedModel, captureDevice);
        if (!recognizer.IsAvailable)
        {
            recognizer.Dispose();
            StatusMessage = RecognizerUnavailableMessage;
            State = RecognitionStreamingState.Error;
            return;
        }

        Finals.Clear();
        Partial = string.Empty;
        StatusMessage = null;

        _uiContext = SynchronizationContext.Current;
        _activeRecognizer = recognizer;
        recognizer.ResultReceived += OnResultReceived;

        // TEMPORARY diagnostic instrumentation: an independent tap on the same capture device,
        // used only when DEMASPEECH_CAPTURE_DEBUG_DIR is set (see CaptureDebugRecorder), for
        // investigating a reported dropped-word bug after a mid-sentence pause. Subscribing here
        // does not affect the recognizer above, which owns the device's Start/Stop lifecycle.
        _captureDebugRecorder = CaptureDebugRecorder.TryStart(captureDevice);

        try
        {
            recognizer.Start();
            State = RecognitionStreamingState.Listening;
        }
        catch (SpeechRecognizerUnavailableException ex)
        {
            _captureDebugRecorder?.Dispose();
            _captureDebugRecorder = null;
            recognizer.ResultReceived -= OnResultReceived;
            _activeRecognizer = null;
            recognizer.Dispose();
            StatusMessage = ex.Message;
            State = RecognitionStreamingState.Error;
        }
    }

    /// <summary>
    ///     Stops the in-flight streaming transcription session, if any. A safe no-op when nothing
    ///     is listening.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        var recognizer = _activeRecognizer;
        if (recognizer is null)
        {
            return;
        }

        recognizer.Stop();
        recognizer.ResultReceived -= OnResultReceived;
        recognizer.Dispose();
        _activeRecognizer = null;
        _uiContext = null;

        // TEMPORARY diagnostic instrumentation: finalize the independent raw-capture recording
        // (if one was started) so its .wav header is patched and the file is closed
        _captureDebugRecorder?.Dispose();
        _captureDebugRecorder = null;

        State = RecognitionStreamingState.Idle;
        StatusMessage = StoppedMessage;
    }

    /// <summary>
    ///     Marshals one recognition result onto the UI thread and applies it to the transcript.
    /// </summary>
    /// <param name="sender">The raising recognizer. Unused.</param>
    /// <param name="e">The recognition event carrying the result to apply.</param>
    private void OnResultReceived(object? sender, SpeechRecognitionEvent e)
    {
        if (_uiContext is null)
        {
            ApplyResult(e.Result);
        }
        else
        {
            _uiContext.Post(state => ApplyResult((SpeechRecognitionResult)state!), e.Result);
        }
    }

    /// <summary>
    ///     Applies one recognition result to the transcript: committing a final result to
    ///     <see cref="Finals"/>, or replacing <see cref="Partial"/> with a provisional one.
    /// </summary>
    /// <param name="result">The result to apply.</param>
    private void ApplyResult(SpeechRecognitionResult result)
    {
        if (result.IsFinal)
        {
            Finals.Add(result.Text);
            Partial = string.Empty;
        }
        else
        {
            Partial = result.Text;
        }
    }

    /// <summary>
    ///     Gets the full transcript so far: every committed final line, followed by the
    ///     in-progress partial line when one exists.
    /// </summary>
    /// <returns>The concatenated transcript text.</returns>
    public string BuildTranscriptText()
    {
        var builder = new StringBuilder();
        foreach (var line in Finals)
        {
            builder.AppendLine(line);
        }

        if (!string.IsNullOrEmpty(Partial))
        {
            builder.Append(Partial);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Marshals a matching-role <see cref="IModelCatalogService.ModelInstalled"/> event onto
    ///     the UI thread and calls <see cref="Refresh"/>; ignored when the installed model's role
    ///     is not <see cref="SpeechModelRole.Recognition"/>.
    /// </summary>
    /// <param name="sender">The raising catalog service. Unused.</param>
    /// <param name="e">The event carrying the installed model's id and role.</param>
    private void OnModelInstalled(object? sender, ModelInstalledEventArgs e)
    {
        if (e.Role != SpeechModelRole.Recognition)
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
    ///     Stops any in-flight session and releases the recognizer and capture device it holds,
    ///     and unsubscribes from <see cref="IModelCatalogService.ModelInstalled"/>.
    /// </summary>
    public void Dispose()
    {
        _catalogService.ModelInstalled -= OnModelInstalled;

        _captureDebugRecorder?.Dispose();
        _captureDebugRecorder = null;

        if (_activeRecognizer is null)
        {
            return;
        }

        _activeRecognizer.ResultReceived -= OnResultReceived;
        _activeRecognizer.Dispose();
        _activeRecognizer = null;
    }
}
