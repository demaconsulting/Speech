using System.Collections.ObjectModel;
using System.ComponentModel;
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
///     capture device, or an engine/session fault) rather than a silent failure or a crash.
///     <para>
///     Every library entry point this panel reaches is reached through an injected seam
///     (<see cref="IModelCatalogService"/>, <see cref="IAudioDeviceService"/>,
///     <see cref="IRecognizerSessionFactory"/>), which is what makes Start/Stop lifecycle,
///     partial-then-final transcript sequencing, and every error path verifiable with no
///     downloaded model, no native runtime, and no real microphone.
///     </para>
///     <para>
///     <see cref="IRecognitionSession.StateChanged"/> and the results from
///     <see cref="IRecognitionSession.GetResultsAsync"/> are documented to raise/resume from the
///     session's own background thread, so <see cref="StartAsync"/> captures the UI thread's
///     <see cref="SynchronizationContext"/> before the session starts and this ViewModel marshals
///     every <see cref="IRecognitionSession.StateChanged"/> event through it before touching any
///     observable property - the result pump below instead relies on each
///     <c>await foreach</c> continuation naturally resuming on that same captured context.
///     Otherwise not thread-safe: expected to be used from the UI thread.
///     </para>
///     <para>
///     This ViewModel also subscribes to <see cref="IModelCatalogService.ModelInstalled"/> in its
///     constructor so a newly downloaded recognition model installed from the Model Catalog panel
///     is picked up automatically, without a manual Refresh click or app restart. The handler
///     ignores events for any other <see cref="SpeechModelRole"/> and marshals onto the UI thread
///     (captured at construction) before calling <see cref="Refresh"/>. <see cref="DisposeAsync"/>
///     unsubscribes this handler.
///     </para>
///     <para>
///     <b>Engine/session reuse.</b> Per <see cref="ISpeechRecognizerEngine"/>'s own reuse
///     guidance, this ViewModel loads an engine at most once per selected model and reuses it
///     across many Start/Stop cycles, instead of reloading its model on every click. Because an
///     <see cref="IRecognitionSession"/> is single-use (see its own remarks), a fresh session is
///     created from the cached engine for every Start and released before the next one is
///     created. The cached engine is invalidated (disposed and reloaded on the next Start) only
///     when the selected model changes; the cached session is invalidated (released, keeping the
///     engine) whenever the selected capture device changes or the shared device-selection panel
///     forces a device refresh - a session, not an engine, is bound to a capture device.
///     </para>
/// </remarks>
public sealed partial class RecognitionPanelViewModel : ObservableObject, IAsyncDisposable
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

    /// <summary>The message shown when the composed recognizer engine honestly reports itself unavailable.</summary>
    public const string RecognizerUnavailableMessage =
        "The selected model's speech recognizer is unavailable on this machine.";

    /// <summary>The message shown after listening is stopped by the user.</summary>
    public const string StoppedMessage = "Listening stopped.";

    /// <summary>The message shown when the active session reports an unrecoverable fault.</summary>
    public const string SessionFaultedMessage = "The recognition session reported an unrecoverable error.";

    /// <summary>The seam supplying every installed recognition model.</summary>
    private readonly IModelCatalogService _catalogService;

    /// <summary>The seam used to create the real capture device to listen through.</summary>
    private readonly IAudioDeviceService _deviceService;

    /// <summary>The shared device-selection panel state supplying the chosen capture device.</summary>
    private readonly DeviceSelectionViewModel _deviceSelection;

    /// <summary>The seam used to load a recognizer engine for the selected model.</summary>
    private readonly IRecognizerSessionFactory _sessionFactory;

    /// <summary>
    ///     The UI thread context captured at construction, used to marshal
    ///     <see cref="IModelCatalogService.ModelInstalled"/> handling onto the UI thread
    ///     regardless of which thread raises it.
    /// </summary>
    private readonly SynchronizationContext? _catalogEventUiContext = SynchronizationContext.Current;

    /// <summary>
    ///     The cached recognizer engine, reused across many Start/Stop cycles for as long as
    ///     <see cref="_engineModel"/> matches <see cref="SelectedModel"/>; see the
    ///     "Engine/session reuse" remarks above. <see langword="null"/> before the first Start, or
    ///     after invalidation by a model change.
    /// </summary>
    private ISpeechRecognizerEngine? _engine;

    /// <summary>The model <see cref="_engine"/> was loaded for, if any.</summary>
    private ISpeechModel? _engineModel;

    /// <summary>
    ///     The active per-run session created from <see cref="_engine"/>, if any. Single-use:
    ///     released via <see cref="InvalidateSessionAsync"/> before the next Start can lease a new
    ///     one from the same engine.
    /// </summary>
    private IRecognitionSession? _session;

    /// <summary>
    ///     The background task pumping <see cref="IRecognitionSession.GetResultsAsync"/> for
    ///     <see cref="_session"/>, stored so it can be awaited on Stop and canceled/awaited on
    ///     invalidation or disposal.
    /// </summary>
    private Task? _pumpTask;

    /// <summary>
    ///     Cancels only the result-pump enumeration for <see cref="_session"/> (never the session
    ///     itself) when that session is being released while still producing results.
    /// </summary>
    private CancellationTokenSource? _pumpCancellation;

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
    ///     The generation of the session whose state changes are currently honored. Bumped
    ///     whenever a session starts, stops, is released or the panel is disposed, so a
    ///     state-change callback already queued for an earlier session (or raised after disposal)
    ///     is recognized as stale and ignored instead of mutating the panel.
    /// </summary>
    private int _sessionGeneration;

    /// <summary>
    ///     The state-change handler subscribed to <see cref="_session"/> (it captures that
    ///     session's generation), retained so exactly the same delegate can be unsubscribed.
    /// </summary>
    private EventHandler<SessionStateChangedEventArgs>? _sessionStateHandler;

    /// <summary>
    ///     Set once <see cref="DisposeAsync"/> begins; makes already-posted catalog refreshes and
    ///     queued starts do nothing against a disposed panel.
    /// </summary>
    private volatile bool _disposed;

    /// <summary>
    ///     Set from the moment a Start is requested until it completes, so the model cannot be
    ///     switched while the engine and session are still being prepared.
    /// </summary>
    private bool _startPending;

    /// <summary>
    ///     The pre-refresh hook registered with <see cref="_deviceSelection"/>, retained so the
    ///     exact same delegate instance can be unregistered in <see cref="DisposeAsync"/>.
    /// </summary>
    private readonly Func<Task> _preRefreshHook;

    /// <summary>
    ///     Gets the installed recognition models available to choose from.
    /// </summary>
    public ObservableCollection<ISpeechModel> AvailableModels { get; } = [];

    /// <summary>The backing field for <see cref="SelectedModel"/>.</summary>
    private ISpeechModel? _selectedModel;

    /// <summary>
    ///     Serializes every start, stop and invalidation so a device change, model change, Stop or
    ///     disposal arriving while another is in flight waits for it instead of racing it over the
    ///     cached session, result pump and engine.
    /// </summary>
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);

    /// <summary>
    ///     Gets or sets the model chosen to listen with, or <see langword="null"/> when none is
    ///     installed or none has been chosen. A change is ignored (the previous selection is kept)
    ///     while a session is <see cref="RecognitionStreamingState.Listening"/> or a Start is in
    ///     flight, so the model can never be switched mid-session or between a Start reading it and
    ///     the engine it loads whether or not the view disables the picker (see
    ///     <see cref="CanChangeModel"/>).
    /// </summary>
    public ISpeechModel? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (State == RecognitionStreamingState.Listening || _startPending)
            {
                return;
            }

            if (SetProperty(ref _selectedModel, value))
            {
                OnPropertyChanged(nameof(CanStart));
                StartCommand.NotifyCanExecuteChanged();
                _ = HandleSelectedModelChangedAsync();
            }
        }
    }

    /// <summary>
    ///     Gets or sets the current streaming lifecycle state.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [NotifyPropertyChangedFor(nameof(CanChangeModel))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    public partial RecognitionStreamingState State { get; set; } = RecognitionStreamingState.Idle;

    /// <summary>
    ///     Gets or sets the status message describing the current or most recently failed
    ///     attempt, or <see langword="null"/> when there is nothing to report.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    public partial string? StatusMessage { get; set; }

    /// <summary>
    ///     Gets or sets the trailing provisional (not-yet-final) transcript text, or an empty
    ///     string when there is no in-progress utterance.
    /// </summary>
    [ObservableProperty]
    public partial string Partial { get; set; } = string.Empty;

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
    ///     <see langword="false"/> only while a session is actively <see cref="RecognitionStreamingState.Listening"/>
    ///     or a Start is still preparing its engine and session, so a user cannot switch the
    ///     recognition model mid-session or mid-start; <see langword="true"/> at
    ///     <see cref="RecognitionStreamingState.Idle"/> and <see cref="RecognitionStreamingState.Error"/>.
    /// </remarks>
    public bool CanChangeModel => State != RecognitionStreamingState.Listening && !_startPending;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecognitionPanelViewModel"/> class and
    ///     loads the currently installed recognition models.
    /// </summary>
    /// <param name="catalogService">The catalog seam to read installed models from. Must not be <see langword="null"/>.</param>
    /// <param name="deviceService">The device seam used to create the capture device. Must not be <see langword="null"/>.</param>
    /// <param name="deviceSelection">The shared device-selection panel state. Must not be <see langword="null"/>.</param>
    /// <param name="sessionFactory">The seam used to load a recognizer engine. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is <see langword="null"/>.</exception>
    internal RecognitionPanelViewModel(
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
        _deviceSelection.PropertyChanged += OnDeviceSelectionChanged;

        _preRefreshHook = StopBeforeDeviceRefreshAsync;
        _deviceSelection.RegisterPreRefreshHook(_preRefreshHook);

        Refresh();
    }

    /// <summary>
    ///     Stops an actively listening session, if any, and invalidates the cached session (not
    ///     the cached engine) when the user picks a different capture device - the session was
    ///     bound to the previously selected device and must not be reused against a new one. The
    ///     capture picker is not disabled while <see cref="State"/> is
    ///     <see cref="RecognitionStreamingState.Listening"/> (unlike the model picker; see
    ///     <see cref="CanChangeModel"/>), so a change can arrive mid-session and must stop that
    ///     session rather than leaving it silently bound to the old device.
    /// </summary>
    /// <param name="sender">The raising device-selection panel. Unused.</param>
    /// <param name="e">The event naming the property that changed.</param>
    /// <remarks>
    ///     This handler's signature is fixed by <see cref="INotifyPropertyChanged.PropertyChanged"/>
    ///     and cannot return a <see cref="Task"/>, so it fires the async work without awaiting it
    ///     - the same accepted fire-and-forget pattern used throughout this class for event
    ///     handlers that must perform async cleanup.
    /// </remarks>
    private void OnDeviceSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DeviceSelectionViewModel.CaptureSelection))
        {
            return;
        }

        _ = HandleCaptureDeviceChangedAsync();
    }

    /// <summary>
    ///     Performs the async stop-then-invalidate-session work for <see cref="OnDeviceSelectionChanged"/>.
    /// </summary>
    private async Task HandleCaptureDeviceChangedAsync()
    {
        await _lifecycleGate.WaitAsync(CancellationToken.None).ConfigureAwait(true);
        try
        {
            if (CanStop)
            {
                await StopCoreAsync().ConfigureAwait(true);
            }

            await InvalidateSessionAsync().ConfigureAwait(true);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>
    ///     Invalidates the cached engine (and its session) after the user picks a different
    ///     recognition model - it was loaded for the previously selected model and must not be
    ///     reused against a new one. <see cref="SelectedModel"/> ignores changes while listening,
    ///     so no session is active here.
    /// </summary>
    /// <remarks>
    ///     The property setter cannot await, so this fires the async work without awaiting it -
    ///     the same accepted fire-and-forget pattern used throughout this class for handlers that
    ///     must perform async cleanup. It takes the lifecycle gate, so it cannot interleave with
    ///     a Start or Stop in flight.
    /// </remarks>
    private async Task HandleSelectedModelChangedAsync()
    {
        await _lifecycleGate.WaitAsync(CancellationToken.None).ConfigureAwait(true);
        try
        {
            // The engine already matches the current selection (for example a selection that was
            // reverted before this ran): nothing to invalidate, and a live session must survive.
            if (_disposed || ReferenceEquals(_engineModel, _selectedModel))
            {
                return;
            }

            await InvalidateEngineAsync().ConfigureAwait(true);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>
    ///     Completes once every start, stop or invalidation requested before this call (including
    ///     the fire-and-forget ones raised by a model or capture-device change) has finished.
    ///     Lets tests wait deterministically instead of yielding or sleeping.
    /// </summary>
    /// <returns>A task that completes when the lifecycle gate has been acquired and released.</returns>
    internal async Task WaitForPendingLifecycleAsync()
    {
        await _lifecycleGate.WaitAsync(CancellationToken.None).ConfigureAwait(true);
        _lifecycleGate.Release();
    }

    /// <summary>
    ///     Maps a <see cref="RecognitionSessionState"/> to the corresponding
    ///     <see cref="RecognitionStreamingState"/>, giving every transition a single, predictable
    ///     source of truth instead of ad hoc assignment at each call site.
    /// </summary>
    /// <param name="state">The session state to map.</param>
    /// <returns>The corresponding streaming state.</returns>
    private static RecognitionStreamingState MapSessionState(RecognitionSessionState state) => state switch
    {
        RecognitionSessionState.Starting or RecognitionSessionState.Running or RecognitionSessionState.Stopping =>
            RecognitionStreamingState.Listening,
        RecognitionSessionState.Faulted => RecognitionStreamingState.Error,
        _ => RecognitionStreamingState.Idle,
    };

    /// <summary>
    ///     Marshals one <see cref="IRecognitionSession.StateChanged"/> event onto the UI thread and
    ///     applies <see cref="MapSessionState"/>.
    /// </summary>
    /// <param name="generation">The generation of the session that raised the event.</param>
    /// <param name="e">The event carrying the previous and current session state.</param>
    private void OnSessionStateChanged(int generation, SessionStateChangedEventArgs e)
    {
        var uiContext = _uiContext;
        if (uiContext is null)
        {
            ApplySessionStateChanged(generation, e);
        }
        else
        {
            uiContext.Post(state => ApplySessionStateChanged(generation, (SessionStateChangedEventArgs)state!), e);
        }
    }

    /// <summary>
    ///     Applies one session state transition to <see cref="State"/>, additionally reporting
    ///     <see cref="SessionFaultedMessage"/> when the session has faulted.
    /// </summary>
    /// <param name="generation">The generation of the session that raised the event.</param>
    /// <param name="e">The event carrying the previous and current session state.</param>
    /// <remarks>
    ///     Ignored when the panel is disposed or when the session that raised the event is no
    ///     longer the current one (it was stopped, released or replaced after the event was
    ///     queued), so a late callback can never resurrect a stale Listening/Error state.
    /// </remarks>
    private void ApplySessionStateChanged(int generation, SessionStateChangedEventArgs e)
    {
        if (_disposed || generation != Volatile.Read(ref _sessionGeneration))
        {
            return;
        }

        ApplySessionState(e.Current);
    }

    /// <summary>
    ///     Applies one session state to <see cref="State"/>, additionally reporting
    ///     <see cref="SessionFaultedMessage"/> when the session has faulted.
    /// </summary>
    /// <param name="sessionState">The session state to apply.</param>
    private void ApplySessionState(RecognitionSessionState sessionState)
    {
        State = MapSessionState(sessionState);

        if (sessionState == RecognitionSessionState.Faulted)
        {
            StatusMessage = SessionFaultedMessage;
        }
    }

    /// <summary>
    ///     Unsubscribes the state-change handler from <paramref name="session"/> and invalidates
    ///     its generation, so any callback already queued for it is ignored.
    /// </summary>
    /// <param name="session">The session whose handler to detach.</param>
    private void DetachSessionStateHandler(IRecognitionSession session)
    {
        if (_sessionStateHandler is not null)
        {
            session.StateChanged -= _sessionStateHandler;
            _sessionStateHandler = null;
        }

        Interlocked.Increment(ref _sessionGeneration);
    }

    /// <summary>
    ///     Releases and clears the cached session, if any, first canceling its result-pump
    ///     enumeration and awaiting that pump task, then disposing the session itself - which
    ///     releases the engine's exclusivity lease so a new session can be created. Safe to call
    ///     when nothing is cached. Does not touch the cached engine.
    /// </summary>
    private async Task InvalidateSessionAsync()
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        DetachSessionStateHandler(session);

        if (_pumpCancellation is not null)
        {
            await _pumpCancellation.CancelAsync().ConfigureAwait(true);
        }

        var pumpTask = _pumpTask;
        if (pumpTask is not null)
        {
            try
            {
                await pumpTask.ConfigureAwait(true);
            }
            catch
            {
                // Already reported through the pump's own catch blocks; this await only drains it.
            }
        }

        await session.DisposeAsync().ConfigureAwait(true);

        _pumpCancellation?.Dispose();
        _pumpCancellation = null;
        _pumpTask = null;
        _session = null;
    }

    /// <summary>
    ///     Releases and clears the cached session (see <see cref="InvalidateSessionAsync"/>), then
    ///     disposes and clears the cached engine, if any, so the next Start loads a fresh one.
    ///     Safe to call when nothing is cached.
    /// </summary>
    private async Task InvalidateEngineAsync()
    {
        await InvalidateSessionAsync().ConfigureAwait(true);

        if (_engine is null)
        {
            return;
        }

        await _engine.DisposeAsync().ConfigureAwait(true);
        _engine = null;
        _engineModel = null;
    }

    /// <summary>
    ///     Re-reads the catalog for installed recognition models, preserving the current
    ///     selection where it is still installed.
    /// </summary>
    [RelayCommand]
    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        var previousId = SelectedModel?.Id;

        AvailableModels.Clear();
        foreach (var model in _catalogService.Enumerate()
            .Where(static descriptor =>
                descriptor.Role == SpeechModelRole.Recognition &&
                descriptor.State == SpeechModelState.Downloaded)
            .Select(static descriptor => descriptor.Model))
        {
            AvailableModels.Add(model);
        }

        SelectedModel = previousId is null
            ? AvailableModels.FirstOrDefault()
            : AvailableModels.FirstOrDefault(model => string.Equals(model.Id, previousId, StringComparison.Ordinal))
              ?? AvailableModels.FirstOrDefault();

        OnPropertyChanged(nameof(HasModels));

        if (AvailableModels.Count == 0)
        {
            StatusMessage = NoModelsMessage;
        }
        else if (string.Equals(StatusMessage, NoModelsMessage, StringComparison.Ordinal))
        {
            StatusMessage = null;
        }
    }

    /// <summary>
    ///     Starts a streaming transcription session for the currently selected model through the
    ///     currently selected capture device, loading a new engine only if none is already cached
    ///     for this model (see the "Engine/session reuse" remarks above), and always creating a
    ///     fresh session since a session is single-use.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        SetStartPending(true);
        try
        {
            await _lifecycleGate.WaitAsync(CancellationToken.None).ConfigureAwait(true);
            try
            {
                await StartCoreAsync().ConfigureAwait(true);
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }
        finally
        {
            SetStartPending(false);
        }
    }

    /// <summary>Updates <see cref="_startPending"/> and notifies the model-picker availability.</summary>
    /// <param name="value">The new value.</param>
    private void SetStartPending(bool value)
    {
        _startPending = value;
        OnPropertyChanged(nameof(CanChangeModel));
    }

    /// <summary>The body of <see cref="StartAsync"/>; runs with <see cref="_lifecycleGate"/> held.</summary>
    private async Task StartCoreAsync()
    {
        if (_disposed)
        {
            return;
        }

        var selectedModel = SelectedModel;
        if (selectedModel is null)
        {
            StatusMessage = NoModelSelectedMessage;
            State = RecognitionStreamingState.Error;
            return;
        }

        // Check the cheap, synchronous precondition (a usable capture device) before paying for
        // the comparatively expensive async engine load, so an honest "no device" outcome does
        // not depend on what an engine-loading seam happens to return for this combination.
        var captureDevice = _deviceService.CreateCaptureDevice(_deviceSelection.CaptureSelection);
        if (!captureDevice.IsAvailable)
        {
            StatusMessage = NoCaptureDeviceMessage;
            State = RecognitionStreamingState.Error;
            return;
        }

        if (_engine is null || !ReferenceEquals(_engineModel, selectedModel))
        {
            await InvalidateEngineAsync().ConfigureAwait(true);

            var engine = await _sessionFactory.LoadAsync(selectedModel, CancellationToken.None).ConfigureAwait(true);
            if (!engine.IsAvailable)
            {
                await engine.DisposeAsync().ConfigureAwait(true);
                StatusMessage = RecognizerUnavailableMessage;
                State = RecognitionStreamingState.Error;
                return;
            }

            _engine = engine;
            _engineModel = selectedModel;
        }

        // A previous run's session is single-use and must be released before a new one can be
        // leased from the engine (see IRecognitionSession's single-use/engine-exclusivity remarks).
        await InvalidateSessionAsync().ConfigureAwait(true);

        IRecognitionSession session;
        try
        {
            session = await _engine.CreateSessionAsync(captureDevice, CancellationToken.None).ConfigureAwait(true);
        }
        catch (RecognitionEngineBusyException ex)
        {
            StatusMessage = ex.Message;
            State = RecognitionStreamingState.Error;
            return;
        }

        Finals.Clear();
        Partial = string.Empty;
        StatusMessage = null;

        _uiContext = SynchronizationContext.Current;
        var generation = Interlocked.Increment(ref _sessionGeneration);
        _sessionStateHandler = (_, e) => OnSessionStateChanged(generation, e);
        session.StateChanged += _sessionStateHandler;

        // TEMPORARY diagnostic instrumentation: an independent tap on the same capture device,
        // used only when DEMASPEECH_CAPTURE_DEBUG_DIR is set (see CaptureDebugRecorder), for
        // investigating a reported dropped-word bug after a mid-sentence pause. Subscribing here
        // does not affect the session above, which owns the device's Start/Stop lifecycle.
        _captureDebugRecorder = CaptureDebugRecorder.TryStart(captureDevice);

        try
        {
            await session.StartAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (SpeechRecognizerUnavailableException ex)
        {
            _captureDebugRecorder?.Dispose();
            _captureDebugRecorder = null;

            DetachSessionStateHandler(session);
            await session.DisposeAsync().ConfigureAwait(true);
            _uiContext = null;

            StatusMessage = ex.Message;
            State = RecognitionStreamingState.Error;
            return;
        }

        _pumpCancellation = new CancellationTokenSource();
        _session = session;
        _pumpTask = PumpResultsAsync(session, _pumpCancellation.Token);
    }

    /// <summary>
    ///     Stops the in-flight streaming transcription session, if any, awaiting its result pump
    ///     to fully drain before returning, without discarding the cached engine - it remains
    ///     cached, with its model still loaded, so the next Start is cheap. A safe no-op when
    ///     nothing is listening, and safe to call concurrently with itself.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStop), AllowConcurrentExecutions = true)]
    private async Task StopAsync()
    {
        await _lifecycleGate.WaitAsync(CancellationToken.None).ConfigureAwait(true);
        try
        {
            await StopCoreAsync().ConfigureAwait(true);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>The body of <see cref="StopAsync"/>; runs with <see cref="_lifecycleGate"/> held.</summary>
    private async Task StopCoreAsync()
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        await session.StopAsync(CancellationToken.None).ConfigureAwait(true);

        var pumpTask = _pumpTask;
        if (pumpTask is not null)
        {
            try
            {
                await pumpTask.ConfigureAwait(true);
            }
            catch
            {
                // Already reported through the pump's own catch blocks; this await only drains it.
            }
        }

        // TEMPORARY diagnostic instrumentation: finalize the independent raw-capture recording
        // (if one was started) so its .wav header is patched and the file is closed
        _captureDebugRecorder?.Dispose();
        _captureDebugRecorder = null;

        // Apply the session's final state directly, then invalidate its generation so any state
        // callback still queued for it cannot overwrite the settled state afterwards.
        ApplySessionState(session.State);
        Interlocked.Increment(ref _sessionGeneration);

        _uiContext = null;

        if (State != RecognitionStreamingState.Error)
        {
            StatusMessage = StoppedMessage;
        }
    }

    /// <summary>
    ///     Stops an actively listening session, if any, and releases the cached session so the
    ///     shared device-selection panel can safely force the audio backend to re-scan its device
    ///     table: the session's bound capture device would otherwise become stale the instant the
    ///     refresh completes, so it must not be reused once one is pending. The cached engine is
    ///     left intact, since a device refresh does not affect the loaded model.
    /// </summary>
    /// <returns>A task that completes once the in-flight session, if any, is fully released.</returns>
    private Task StopBeforeDeviceRefreshAsync() => HandleCaptureDeviceChangedAsync();

    /// <summary>
    ///     Pumps <see cref="IRecognitionSession.GetResultsAsync"/> for one session, applying every
    ///     result to the transcript as it arrives. Runs as a background task stored on
    ///     <see cref="_pumpTask"/>; relies on each <c>await foreach</c> continuation resuming on
    ///     the UI thread context captured by its caller (<see cref="StartAsync"/>) rather than
    ///     explicit marshaling.
    /// </summary>
    /// <param name="session">The session to pump results from.</param>
    /// <param name="cancellationToken">
    ///     A token that ends only this enumeration (not the session) when the session is being
    ///     released while still producing results.
    /// </param>
    private async Task PumpResultsAsync(IRecognitionSession session, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var recognitionEvent in session.GetResultsAsync(cancellationToken).ConfigureAwait(true))
            {
                ApplyResult(recognitionEvent.Result);
            }
        }
        catch (OperationCanceledException)
        {
            // Enumeration was ended by invalidation (model/device change or disposal), not a
            // session fault.
        }
        catch (RecognitionSessionFaultedException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (SpeechRecognizerUnavailableException ex)
        {
            StatusMessage = ex.Message;
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
            _catalogEventUiContext.Post(_ => Refresh(), null); // Refresh ignores a disposed panel.
        }
    }

    /// <summary>
    ///     Stops any in-flight session and releases the cached session and engine it holds, and
    ///     unsubscribes from <see cref="IModelCatalogService.ModelInstalled"/> and the
    ///     device-selection panel's change notifications.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _catalogService.ModelInstalled -= OnModelInstalled;
        _deviceSelection.PropertyChanged -= OnDeviceSelectionChanged;
        _deviceSelection.UnregisterPreRefreshHook(_preRefreshHook);

        await _lifecycleGate.WaitAsync(CancellationToken.None).ConfigureAwait(true);
        try
        {
            if (CanStop)
            {
                await StopCoreAsync().ConfigureAwait(true);
            }

            _captureDebugRecorder?.Dispose();
            _captureDebugRecorder = null;

            await InvalidateEngineAsync().ConfigureAwait(true);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }
}
