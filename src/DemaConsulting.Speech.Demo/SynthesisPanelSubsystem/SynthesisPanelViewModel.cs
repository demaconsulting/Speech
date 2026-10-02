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
///     <see cref="DisposeAsync"/> unsubscribes this handler.
///     </para>
///     <para>
///     <b>Engine/session reuse.</b> Per <see cref="ISpeechSynthesizerEngine"/>'s and
///     <see cref="ISynthesisSession"/>'s own reuse guidance, this ViewModel loads an engine at
///     most once per selected model/parameter-value combination, and creates a session at most
///     once per engine/playback-device combination, reusing both across many Play calls instead
///     of reloading the model and recreating the session on every click (the bug this redesign
///     fixes). <see cref="PlayAsync"/> detects a reason to reload lazily, on its next call,
///     rather than proactively on a property change: the cached engine is reloaded only when the
///     selected model or <see cref="Settings"/>' built parameter values differ from the ones it
///     was last loaded with; the cached session is independently recreated whenever the engine
///     was just reloaded or the selected playback device differs from the one it was last bound
///     to.
///     </para>
/// </remarks>
public sealed partial class SynthesisPanelViewModel : ObservableObject, IAsyncDisposable
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

    /// <summary>The message shown when the composed synthesizer engine honestly reports itself unavailable.</summary>
    public const string SynthesizerUnavailableMessage =
        "The selected model's speech synthesizer is unavailable on this machine.";

    /// <summary>The message shown after playback is stopped by the user.</summary>
    public const string StoppedMessage = "Playback stopped.";

    /// <summary>The message shown when the active session reports an unrecoverable fault.</summary>
    public const string SessionFaultedMessage = "The synthesis session reported an unrecoverable error.";

    /// <summary>Example Natural Language Audio Tags drawn from the library's closed, published vocabulary.</summary>
    public static IReadOnlyList<string> ExampleTagHints { get; } =
        AudioTagCatalog.Tags.Select(descriptor => $"[{descriptor.Aliases[0]}]").ToArray();

    /// <summary>The seam supplying every installed synthesis model.</summary>
    private readonly IModelCatalogService _catalogService;

    /// <summary>The seam used to create the real playback device to speak through.</summary>
    private readonly IAudioDeviceService _deviceService;

    /// <summary>The shared device-selection panel state supplying the chosen playback device.</summary>
    private readonly DeviceSelectionViewModel _deviceSelection;

    /// <summary>The seam used to load a synthesizer engine for the selected model.</summary>
    private readonly ISynthesizerSessionFactory _sessionFactory;

    /// <summary>
    ///     The UI thread context captured at construction, used to marshal
    ///     <see cref="IModelCatalogService.ModelInstalled"/> handling onto the UI thread
    ///     regardless of which thread raises it.
    /// </summary>
    private readonly SynchronizationContext? _catalogEventUiContext = SynchronizationContext.Current;

    /// <summary>
    ///     The cached synthesizer engine, reused across many Play calls for as long as
    ///     <see cref="_engineModel"/> and <see cref="_engineParameterValues"/> match the current
    ///     selection; see the "Engine/session reuse" remarks above. <see langword="null"/> before
    ///     the first Play, or after invalidation by a model/parameter change.
    /// </summary>
    private ISpeechSynthesizerEngine? _engine;

    /// <summary>The model <see cref="_engine"/> was loaded for, if any.</summary>
    private ISpeechModel? _engineModel;

    /// <summary>The parameter value bag <see cref="_engine"/> was loaded with, if any.</summary>
    private IReadOnlyDictionary<string, object>? _engineParameterValues;

    /// <summary>
    ///     The cached session created from <see cref="_engine"/>, reused across many Play calls
    ///     for as long as it is bound to the currently selected playback device.
    ///     <see langword="null"/> before the first Play, or after invalidation.
    /// </summary>
    private ISynthesisSession? _session;

    /// <summary>The playback device selection <see cref="_session"/> was created with, if any.</summary>
    private AudioDeviceSelection? _sessionPlaybackSelection;

    /// <summary>
    ///     The UI thread context captured when <see cref="_session"/> was created, used to
    ///     marshal <see cref="ISynthesisSession.StateChanged"/> handling onto the UI thread.
    /// </summary>
    private SynchronizationContext? _uiContext;

    /// <summary>
    ///     The pre-refresh hook registered with <see cref="_deviceSelection"/>, retained so the
    ///     exact same delegate instance can be unregistered in <see cref="DisposeAsync"/>.
    /// </summary>
    private readonly Func<Task> _preRefreshHook;

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
    public partial ISpeechModel? SelectedModel { get; set; }

    /// <summary>
    ///     Gets or sets the text to synthesize, which may contain inline Natural Language Audio
    ///     Tags such as <c>[whispers]</c> or <c>[short pause]</c>.
    /// </summary>
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the current playback lifecycle state.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPlay))]
    [NotifyPropertyChangedFor(nameof(CanChangeModel))]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    public partial SynthesisPlaybackState State { get; set; } = SynthesisPlaybackState.Idle;

    /// <summary>
    ///     Gets or sets the status message describing the current or most recently failed
    ///     attempt, or <see langword="null"/> when there is nothing to report.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    public partial string? StatusMessage { get; set; }

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
    /// <param name="sessionFactory">The seam used to load a synthesizer engine. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is <see langword="null"/>.</exception>
    internal SynthesisPanelViewModel(
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

        _preRefreshHook = StopBeforeDeviceRefreshAsync;
        _deviceSelection.RegisterPreRefreshHook(_preRefreshHook);

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
        foreach (var model in _catalogService.Enumerate()
            .Where(static descriptor =>
                descriptor.Role == SpeechModelRole.Synthesis &&
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
    }

    /// <summary>
    ///     Applies a newly selected model's declared parameters to the embedded settings panel.
    /// </summary>
    /// <param name="value">The newly selected model.</param>
    /// <remarks>
    ///     Does not proactively invalidate the cached engine/session: <see cref="PlayAsync"/>
    ///     detects the model change lazily on its next call (see the "Engine/session reuse"
    ///     remarks above) by comparing against <see cref="_engineModel"/>.
    /// </remarks>
    partial void OnSelectedModelChanged(ISpeechModel? value) => Settings.Model = value;

    /// <summary>
    ///     Maps a <see cref="SynthesisSessionState"/> to the corresponding
    ///     <see cref="SynthesisPlaybackState"/>, giving every transition a single, predictable
    ///     source of truth instead of ad hoc assignment at each call site.
    /// </summary>
    /// <param name="state">The session state to map.</param>
    /// <returns>The corresponding playback state.</returns>
    private static SynthesisPlaybackState MapSessionState(SynthesisSessionState state) => state switch
    {
        SynthesisSessionState.Starting => SynthesisPlaybackState.Synthesizing,
        SynthesisSessionState.Running or SynthesisSessionState.Stopping => SynthesisPlaybackState.Playing,
        SynthesisSessionState.Faulted => SynthesisPlaybackState.Error,
        _ => SynthesisPlaybackState.Idle,
    };

    /// <summary>
    ///     Marshals one <see cref="ISynthesisSession.StateChanged"/> event onto the UI thread and
    ///     applies <see cref="MapSessionState"/>.
    /// </summary>
    /// <param name="sender">The raising session. Unused.</param>
    /// <param name="e">The event carrying the previous and current session state.</param>
    private void OnSessionStateChanged(object? sender, SessionStateChangedEventArgs e)
    {
        if (_uiContext is null)
        {
            ApplySessionStateChanged(e);
        }
        else
        {
            _uiContext.Post(state => ApplySessionStateChanged((SessionStateChangedEventArgs)state!), e);
        }
    }

    /// <summary>
    ///     Applies one session state transition to <see cref="State"/>, additionally reporting
    ///     <see cref="SessionFaultedMessage"/> when the session has faulted.
    /// </summary>
    /// <param name="e">The event carrying the previous and current session state.</param>
    private void ApplySessionStateChanged(SessionStateChangedEventArgs e)
    {
        State = MapSessionState(e.Current);

        if (e.Current == SynthesisSessionState.Faulted)
        {
            StatusMessage = SessionFaultedMessage;
        }
    }

    /// <summary>
    ///     Compares two parameter value bags for equality by key and value, since
    ///     <see cref="ModelSettingsViewModel.BuildValueBag"/> returns a freshly built dictionary
    ///     on every call rather than a stable cached instance.
    /// </summary>
    /// <param name="first">The first bag to compare, or <see langword="null"/>.</param>
    /// <param name="second">The second bag to compare, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both bags contain the same keys mapped to equal values.</returns>
    private static bool ParameterValuesEqual(
        IReadOnlyDictionary<string, object>? first,
        IReadOnlyDictionary<string, object>? second)
    {
        if (ReferenceEquals(first, second))
        {
            return true;
        }

        if (first is null || second is null || first.Count != second.Count)
        {
            return false;
        }

        foreach (var pair in first)
        {
            if (!second.TryGetValue(pair.Key, out var value) || !Equals(pair.Value, value))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Synthesizes and speaks <see cref="Text"/> through the currently selected playback
    ///     device, using the currently selected model, reloading the cached engine and/or
    ///     recreating the cached session only when something they were built from has actually
    ///     changed (see the "Engine/session reuse" remarks above).
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

        var parameterValues = Settings.BuildValueBag();

        // Check the cheap, synchronous precondition (a usable playback device) before paying for
        // the comparatively expensive async engine load, so an honest "no device" outcome does
        // not depend on what an engine-loading seam happens to return for this combination.
        var desiredSelection = _deviceSelection.PlaybackSelection;
        var precheckDevice = _deviceService.CreatePlaybackDevice(desiredSelection);
        if (!precheckDevice.IsAvailable)
        {
            StatusMessage = NoPlaybackDeviceMessage;
            State = SynthesisPlaybackState.Error;
            return;
        }

        if (_engine is null ||
            !ReferenceEquals(_engineModel, selectedModel) ||
            !ParameterValuesEqual(_engineParameterValues, parameterValues))
        {
            await InvalidateEngineAsync().ConfigureAwait(true);

            var engine = await _sessionFactory.LoadAsync(selectedModel, parameterValues, cancellationToken).ConfigureAwait(true);
            if (!engine.IsAvailable)
            {
                await engine.DisposeAsync().ConfigureAwait(true);
                StatusMessage = SynthesizerUnavailableMessage;
                State = SynthesisPlaybackState.Error;
                return;
            }

            _engine = engine;
            _engineModel = selectedModel;
            _engineParameterValues = parameterValues;
        }

        if (_session is null || !Equals(_sessionPlaybackSelection, desiredSelection))
        {
            await ReleaseSessionAsync().ConfigureAwait(true);

            // Reuse the device already resolved by the precondition check above rather than
            // resolving it a second time.
            var playbackDevice = precheckDevice;

            ISynthesisSession session;
            try
            {
                session = await _engine.CreateSessionAsync(playbackDevice, cancellationToken).ConfigureAwait(true);
            }
            catch (SynthesisEngineBusyException ex)
            {
                StatusMessage = ex.Message;
                State = SynthesisPlaybackState.Error;
                return;
            }

            if (!session.IsAvailable)
            {
                await session.DisposeAsync().ConfigureAwait(true);
                StatusMessage = SynthesizerUnavailableMessage;
                State = SynthesisPlaybackState.Error;
                return;
            }

            _uiContext = SynchronizationContext.Current;
            session.StateChanged += OnSessionStateChanged;

            _session = session;
            _sessionPlaybackSelection = desiredSelection;
        }

        StatusMessage = null;

        try
        {
            await _session.SpeakAsync(Text, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // The session's own StateChanged transitions already drove State back to Idle.
            StatusMessage = StoppedMessage;
        }
        catch (SpeechSynthesizerUnavailableException ex)
        {
            State = SynthesisPlaybackState.Error;
            StatusMessage = ex.Message;
        }
        catch (SynthesisSessionFaultedException ex)
        {
            // Faulted is terminal for a session (see ISynthesisSession's remarks): release it so
            // the next Play creates a fresh one against the still-cached engine.
            await ReleaseSessionAsync().ConfigureAwait(true);
            StatusMessage = ex.Message;
        }
    }

    /// <summary>
    ///     Stops an in-flight Play session deterministically. A safe no-op when nothing is
    ///     playing.
    /// </summary>
    [RelayCommand]
    private async Task StopAsync()
    {
        var session = _session;
        if (session is not null)
        {
            await session.StopAsync().ConfigureAwait(true);
        }

        // Defense-in-depth: also requests cancellation of PlayAsync's own cancellation token, in
        // case the session itself cannot be stopped (for example an unavailable fallback).
        PlayCommand.Cancel();
    }

    /// <summary>
    ///     Stops an in-flight Play, if any, awaits its actual completion, and releases the cached
    ///     session (keeping the cached engine) so the shared device-selection panel can safely
    ///     force the audio backend to re-scan its device table - the session's bound playback
    ///     device would otherwise become stale the instant the refresh completes.
    /// </summary>
    /// <returns>
    ///     A task that completes once the in-flight <see cref="PlayCommand"/> execution (if any)
    ///     has itself completed and the cached session has been released.
    /// </returns>
    private async Task StopBeforeDeviceRefreshAsync()
    {
        if (PlayCommand.IsRunning)
        {
            await StopAsync().ConfigureAwait(true);

            if (PlayCommand.ExecutionTask is { } executionTask)
            {
                try
                {
                    await executionTask.ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    // Expected: StopAsync() cancels the in-flight PlayAsync, which surfaces as
                    // OperationCanceledException from its awaited ExecutionTask.
                }
            }
        }

        await ReleaseSessionAsync().ConfigureAwait(true);
    }

    /// <summary>
    ///     Releases and clears the cached session, if any, unsubscribing from
    ///     <see cref="ISynthesisSession.StateChanged"/> first so a subsequent disposal transition
    ///     cannot be mistaken for a fresh fault. Safe to call when nothing is cached. Does not
    ///     touch the cached engine.
    /// </summary>
    private async Task ReleaseSessionAsync()
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        session.StateChanged -= OnSessionStateChanged;

        await session.DisposeAsync().ConfigureAwait(true);

        _session = null;
        _sessionPlaybackSelection = null;
        _uiContext = null;
    }

    /// <summary>
    ///     Releases the cached session (see <see cref="ReleaseSessionAsync"/>), then disposes and
    ///     clears the cached engine, if any, so the next Play loads a fresh one. Safe to call when
    ///     nothing is cached.
    /// </summary>
    private async Task InvalidateEngineAsync()
    {
        await ReleaseSessionAsync().ConfigureAwait(true);

        if (_engine is null)
        {
            return;
        }

        await _engine.DisposeAsync().ConfigureAwait(true);
        _engine = null;
        _engineModel = null;
        _engineParameterValues = null;
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
    ///     Stops any in-flight Play, unsubscribes from <see cref="IModelCatalogService.ModelInstalled"/>
    ///     and the device-selection panel's pre-refresh hook, and releases the cached session and
    ///     engine, if any.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _catalogService.ModelInstalled -= OnModelInstalled;
        _deviceSelection.UnregisterPreRefreshHook(_preRefreshHook);

        if (PlayCommand.IsRunning)
        {
            await StopAsync().ConfigureAwait(true);

            if (PlayCommand.ExecutionTask is { } executionTask)
            {
                try
                {
                    await executionTask.ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    // Expected: StopAsync() cancels the in-flight PlayAsync.
                }
            }
        }

        await InvalidateEngineAsync().ConfigureAwait(true);
    }
}
