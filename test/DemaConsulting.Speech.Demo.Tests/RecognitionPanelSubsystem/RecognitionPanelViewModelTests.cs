using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Demo.DeviceSelectionSubsystem;
using DemaConsulting.Speech.Demo.ModelCatalogSubsystem;
using DemaConsulting.Speech.Demo.RecognitionPanelSubsystem;
using DemaConsulting.Speech.Demo.Tests.Fakes;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;
using NSubstitute;

namespace DemaConsulting.Speech.Demo.Tests.RecognitionPanelSubsystem;

/// <summary>
///     Unit tests for <see cref="RecognitionPanelViewModel"/>.
/// </summary>
public class RecognitionPanelViewModelTests
{
    /// <summary>
    ///     Builds a device-selection panel over a fake reporting no devices, for the constructor
    ///     parameter every panel requires.
    /// </summary>
    /// <returns>The composed panel.</returns>
    private static DeviceSelectionViewModel DeviceSelection()
    {
        var service = Substitute.For<IAudioDeviceService>();
        service.EnumerateCaptureDevices().Returns([]);
        service.EnumeratePlaybackDevices().Returns([]);
        return new DeviceSelectionViewModel(service);
    }

    /// <summary>
    ///     Builds a catalog service fake returning the supplied descriptors.
    /// </summary>
    /// <param name="descriptors">The descriptors to report.</param>
    /// <returns>The composed fake.</returns>
    private static IModelCatalogService Catalog(params SpeechModelDescriptor[] descriptors)
    {
        var service = Substitute.For<IModelCatalogService>();
        service.Enumerate().Returns(descriptors);
        return service;
    }

    /// <summary>
    ///     Builds an available capture device fake.
    /// </summary>
    /// <param name="isAvailable">Whether the fake reports itself as available.</param>
    /// <returns>The composed fake.</returns>
    private static IAudioCaptureDevice CaptureDevice(bool isAvailable = true)
    {
        var device = Substitute.For<IAudioCaptureDevice>();
        device.IsAvailable.Returns(isAvailable);
        return device;
    }

    /// <summary>
    ///     Builds a session-factory substitute whose <c>LoadAsync</c> returns each of the given
    ///     engines in order (the last is returned for any further call), for any model.
    /// </summary>
    /// <param name="engines">The engine(s) to return from successive calls.</param>
    /// <returns>The composed substitute.</returns>
    private static IRecognizerSessionFactory SessionFactory(params FakeSpeechRecognizerEngine[] engines)
    {
        var factory = Substitute.For<IRecognizerSessionFactory>();
        var tasks = engines.Select(engine => Task.FromResult<ISpeechRecognizerEngine>(engine)).ToArray();
        factory.LoadAsync(Arg.Any<ISpeechModel>(), Arg.Any<CancellationToken>())
            .Returns(tasks[0], tasks[1..]);
        return factory;
    }

    /// <summary>
    ///     Proves that the panel rejects any missing constructor dependency.
    /// </summary>
    [Fact]
    public void RecognitionPanelViewModel_Constructor_NullDependency_ThrowsArgumentNullException()
    {
        // Arrange: valid instances of everything, for substituting one null at a time
        var catalog = Catalog();
        var deviceService = Substitute.For<IAudioDeviceService>();
        var deviceSelection = DeviceSelection();
        var sessionFactory = Substitute.For<IRecognizerSessionFactory>();

        // Act & Assert: each missing dependency is a programming error
        Assert.Throws<ArgumentNullException>(
            () => new RecognitionPanelViewModel(null!, deviceService, deviceSelection, sessionFactory));
        Assert.Throws<ArgumentNullException>(
            () => new RecognitionPanelViewModel(catalog, null!, deviceSelection, sessionFactory));
        Assert.Throws<ArgumentNullException>(
            () => new RecognitionPanelViewModel(catalog, deviceService, null!, sessionFactory));
        Assert.Throws<ArgumentNullException>(
            () => new RecognitionPanelViewModel(catalog, deviceService, deviceSelection, null!));
    }

    /// <summary>
    ///     Proves that a machine with no installed recognition model reports the honest empty
    ///     state at construction, rather than an unexplained blank picker.
    /// </summary>
    [Fact]
    public void RecognitionPanelViewModel_Constructor_NoInstalledRecognitionModel_ReportsHonestEmptyState()
    {
        // Arrange & Act: compose the panel over an empty catalog
        var viewModel = new RecognitionPanelViewModel(
            Catalog(), Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<IRecognizerSessionFactory>());

        // Assert: the panel reports nothing to choose from
        Assert.False(viewModel.HasModels);
        Assert.Empty(viewModel.AvailableModels);
        Assert.Null(viewModel.SelectedModel);
        Assert.False(viewModel.CanStart);
    }

    /// <summary>
    ///     Proves that Refresh only offers installed models declaring the recognition role,
    ///     excluding synthesis models and not-yet-downloaded recognition models.
    /// </summary>
    [Fact]
    public void RecognitionPanelViewModel_Refresh_MixedCatalog_OffersOnlyInstalledRecognitionModels()
    {
        // Arrange: a catalog mixing roles and install states
        var catalog = Catalog(
            FakeSpeechModel.Descriptor("stt-installed", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition),
            FakeSpeechModel.Descriptor("stt-pending", SpeechModelState.NotDownloaded, role: SpeechModelRole.Recognition),
            FakeSpeechModel.Descriptor("tts-installed", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis));

        // Act: compose the panel
        var viewModel = new RecognitionPanelViewModel(
            catalog, Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<IRecognizerSessionFactory>());

        // Assert: only the installed recognition model is offered and selected
        Assert.True(viewModel.HasModels);
        Assert.Single(viewModel.AvailableModels);
        Assert.Equal("stt-installed", viewModel.SelectedModel?.Id);
    }

    /// <summary>
    ///     Proves that a user-selected model remains selected after a catalog Refresh, as long as
    ///     that model is still reported as installed, rather than silently reverting to the
    ///     catalog's first entry.
    /// </summary>
    [Fact]
    public void RecognitionPanelViewModel_Refresh_ModelStillInstalled_PreservesSelection()
    {
        // Arrange: a panel composed over a catalog reporting two installed recognition models
        var catalog = Substitute.For<IModelCatalogService>();
        var descriptors = new[]
        {
            FakeSpeechModel.Descriptor("stt-a", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition),
            FakeSpeechModel.Descriptor("stt-b", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition),
        };
        catalog.Enumerate().Returns(_ => descriptors);
        var viewModel = new RecognitionPanelViewModel(
            catalog, Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<IRecognizerSessionFactory>());

        // Act: select the second model, then trigger a catalog refresh reporting the same models
        viewModel.SelectedModel = viewModel.AvailableModels.Single(model => model.Id == "stt-b");
        viewModel.Refresh();

        // Assert: the previously selected model is still selected after the refresh
        Assert.Equal("stt-b", viewModel.SelectedModel?.Id);
    }

    /// <summary>
    ///     Proves that Start reports the honest "no model selected" outcome rather than throwing
    ///     when invoked with nothing selected.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_Start_NoModelSelected_ReportsErrorState()
    {
        // Arrange: a panel with no installed models, so nothing can be selected
        var viewModel = new RecognitionPanelViewModel(
            Catalog(), Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<IRecognizerSessionFactory>());

        // Act: invoke Start directly (bypassing the command's own CanExecute gate, which
        // already disables the button for this state) to prove the defensive guard behaves
        // honestly too
        await viewModel.StartCommand.ExecuteAsync(null);

        // Assert: an honest, explanatory error - not an exception
        Assert.Equal(RecognitionPanelViewModel.NoModelSelectedMessage, viewModel.StatusMessage);
        Assert.Equal(RecognitionStreamingState.Error, viewModel.State);
    }

    /// <summary>
    ///     Proves that Start reports the honest "no capture device" outcome when the device seam
    ///     reports none available.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_Start_NoCaptureDevice_ReportsErrorState()
    {
        // Arrange: an installed model but a machine with no usable capture device
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var unavailableDevice = CaptureDevice(false);
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(unavailableDevice);
        var viewModel = new RecognitionPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), Substitute.For<IRecognizerSessionFactory>());

        // Act: attempt to start
        await viewModel.StartCommand.ExecuteAsync(null);

        // Assert: the honest device-unavailable outcome
        Assert.Equal(RecognitionPanelViewModel.NoCaptureDeviceMessage, viewModel.StatusMessage);
        Assert.Equal(RecognitionStreamingState.Error, viewModel.State);
    }

    /// <summary>
    ///     Proves that Start reports the honest "recognizer unavailable" outcome, and disposes
    ///     the unavailable engine, when the session seam cannot compose a working one.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_Start_RecognizerUnavailable_ReportsErrorStateAndDisposes()
    {
        // Arrange: an installed model and available device, but an engine that honestly reports
        // it cannot compose a working recognizer
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = CaptureDevice();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var engine = new FakeSpeechRecognizerEngine { IsAvailable = false };
        var viewModel = new RecognitionPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), SessionFactory(engine));

        // Act: attempt to start
        await viewModel.StartCommand.ExecuteAsync(null);

        // Assert: the honest unavailable outcome, and the unusable engine was released
        Assert.Equal(RecognitionPanelViewModel.RecognizerUnavailableMessage, viewModel.StatusMessage);
        Assert.Equal(RecognitionStreamingState.Error, viewModel.State);
        Assert.Equal(1, engine.DisposeCallCount);
    }

    /// <summary>
    ///     Proves that Start reports the honest outcome, and releases the session, when the
    ///     library reports an unavailable capture device only after composition (an honest
    ///     "reported available but the device failed to start" outcome).
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_Start_RecognizerStartThrows_ReportsErrorStateAndDisposes()
    {
        // Arrange: a session that reports itself available but faults when actually started
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = CaptureDevice();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var session = new FakeRecognitionSession
        {
            StartException = new SpeechRecognizerUnavailableException("Capture device failed to start."),
        };
        var engine = new FakeSpeechRecognizerEngine { SessionFactory = _ => session };
        var viewModel = new RecognitionPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), SessionFactory(engine));

        // Act: attempt to start
        await viewModel.StartCommand.ExecuteAsync(null);

        // Assert: the fault is reported honestly, and the session released (the engine remains
        // cached, since only the session failed to start)
        Assert.Equal("Capture device failed to start.", viewModel.StatusMessage);
        Assert.Equal(RecognitionStreamingState.Error, viewModel.State);
        Assert.Equal(1, session.DisposeCallCount);
        Assert.Equal(0, engine.DisposeCallCount);
    }

    /// <summary>
    ///     Proves that a successful Start enters the Listening state and begins streaming.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_Start_SuccessfulSession_EntersListeningState()
    {
        // Arrange: an installed model, an available device, and a session that starts cleanly
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = CaptureDevice();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var session = new FakeRecognitionSession();
        var engine = new FakeSpeechRecognizerEngine { SessionFactory = _ => session };
        var viewModel = new RecognitionPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), SessionFactory(engine));

        // Act: start listening
        await viewModel.StartCommand.ExecuteAsync(null);

        // Assert: streaming began and the panel reports it
        Assert.Equal(RecognitionStreamingState.Listening, viewModel.State);
        Assert.Null(viewModel.StatusMessage);
        Assert.Equal(1, session.StartCallCount);
    }

    /// <summary>
    ///     Proves that a provisional result replaces the trailing partial line without
    ///     committing anything to the final transcript, and that a subsequent final result
    ///     commits the line and clears the partial - the exact sequencing a live captioning UI
    ///     depends on.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_ResultReceived_PartialThenFinal_UpdatesTranscriptInOrder()
    {
        // Arrange: a listening session
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = CaptureDevice();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var session = new FakeRecognitionSession();
        var engine = new FakeSpeechRecognizerEngine { SessionFactory = _ => session };
        var viewModel = new RecognitionPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), SessionFactory(engine));
        await viewModel.StartCommand.ExecuteAsync(null);

        // Act: push a provisional result, then a final one for the same utterance
        session.PushResult(new SpeechRecognitionResult("hel", false));

        // Assert: the partial line reflects the provisional result; nothing is final yet
        Assert.Equal("hel", viewModel.Partial);
        Assert.Empty(viewModel.Finals);

        // Act: the recognizer decides the utterance is complete
        session.PushResult(new SpeechRecognitionResult("hello", true));

        // Assert: the line is committed and the partial is cleared
        Assert.Equal(["hello"], viewModel.Finals);
        Assert.Equal(string.Empty, viewModel.Partial);
    }

    /// <summary>
    ///     Proves that several final results accumulate in order, and that
    ///     <see cref="RecognitionPanelViewModel.BuildTranscriptText"/> renders every committed
    ///     line followed by any in-progress partial.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_BuildTranscriptText_FinalsAndPartial_RendersInOrder()
    {
        // Arrange: a listening session with two committed lines and one in-progress partial
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = CaptureDevice();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var session = new FakeRecognitionSession();
        var engine = new FakeSpeechRecognizerEngine { SessionFactory = _ => session };
        var viewModel = new RecognitionPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), SessionFactory(engine));
        await viewModel.StartCommand.ExecuteAsync(null);

        // Act: commit two lines and leave a third in progress
        session.PushResult(new SpeechRecognitionResult("one", true));
        session.PushResult(new SpeechRecognitionResult("two", true));
        session.PushResult(new SpeechRecognitionResult("thr", false));

        // Assert: the transcript renders both committed lines then the trailing partial
        Assert.Equal($"one{Environment.NewLine}two{Environment.NewLine}thr", viewModel.BuildTranscriptText());
    }

    /// <summary>
    ///     Proves that a session transitioning to <see cref="RecognitionSessionState.Faulted"/>
    ///     (for example, the bound capture device being lost mid-session) is reported honestly
    ///     through <see cref="RecognitionPanelViewModel.State"/> and
    ///     <see cref="RecognitionPanelViewModel.StatusMessage"/>, driven entirely by the
    ///     <see cref="IRecognitionSession.StateChanged"/> mapping rather than ad hoc assignment.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_StateChanged_SessionTransitionsToFaulted_ReportsErrorState()
    {
        // Arrange: a listening session
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = CaptureDevice();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var session = new FakeRecognitionSession();
        var engine = new FakeSpeechRecognizerEngine { SessionFactory = _ => session };
        var viewModel = new RecognitionPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), SessionFactory(engine));
        await viewModel.StartCommand.ExecuteAsync(null);

        // Act: the session itself reports an unrecoverable fault (not caused by this panel
        // calling Stop or Start)
        session.RaiseStateChanged(RecognitionSessionState.Faulted);

        // Assert: the panel reports the honest error state, driven by the StateChanged mapping
        Assert.Equal(RecognitionStreamingState.Error, viewModel.State);
        Assert.Equal(RecognitionPanelViewModel.SessionFaultedMessage, viewModel.StatusMessage);
    }

    /// <summary>
    ///     Proves that clicking the shared device-selection panel's Refresh while this panel is
    ///     actively listening stops the session first - deterministically, via the registered
    ///     pre-refresh hook - letting the device refresh succeed rather than being refused with
    ///     an <see cref="AudioDeviceInUseException"/>.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_PreRefreshHook_WhileListening_StopsSessionBeforeDeviceRefreshSucceeds()
    {
        // Arrange: a device service whose RefreshDevices() refuses while a "still listening" flag
        // is true, and a session whose Stop flips that flag false - standing in for the real
        // library's "refuses a refresh while a stream is active" contract
        var stillListening = false;
        var deviceService = Substitute.For<IAudioDeviceService>();
        deviceService.EnumerateCaptureDevices().Returns([]);
        deviceService.EnumeratePlaybackDevices().Returns([]);
        deviceService.When(s => s.RefreshDevices()).Do(_ =>
        {
            if (stillListening)
            {
                throw new AudioDeviceInUseException("Cannot refresh PortAudio devices while a stream is active on: Mic A.");
            }
        });
        var deviceSelection = new DeviceSelectionViewModel(deviceService);

        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var availableDevice = CaptureDevice();
        var captureDeviceService = Substitute.For<IAudioDeviceService>();
        captureDeviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var session = new FakeRecognitionSession { OnStopRequested = () => stillListening = false };
        var engine = new FakeSpeechRecognizerEngine { SessionFactory = _ => session };
        var viewModel = new RecognitionPanelViewModel(
            Catalog(descriptor), captureDeviceService, deviceSelection, SessionFactory(engine));

        // Act: start listening, mark the session as actively streaming, then refresh the shared
        // device-selection panel (as the "Refresh devices" button would)
        await viewModel.StartCommand.ExecuteAsync(null);
        stillListening = true;
        var exception = await Record.ExceptionAsync(() => deviceSelection.Refresh());

        // Assert: the session was stopped by the hook, the refresh completed without throwing,
        // and the panel returned to idle
        Assert.Null(exception);
        Assert.Equal(1, session.StopCallCount);
        Assert.Equal(RecognitionStreamingState.Idle, viewModel.State);
        Assert.False(viewModel.CanStop);
    }

    /// <summary>
    ///     Proves that the registered pre-refresh hook is a safe no-op when no listening session
    ///     is in flight, so a "Refresh devices" click while the panel is idle never calls Stop on
    ///     a session that was never even created.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_PreRefreshHook_WhileIdle_IsNoOpAndDeviceRefreshSucceeds()
    {
        // Arrange: a panel with no listening session ever started, sharing its own
        // device-selection panel so Refresh() exercises the real registered hook
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = CaptureDevice();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var engine = new FakeSpeechRecognizerEngine();
        var deviceSelection = DeviceSelection();
        var viewModel = new RecognitionPanelViewModel(
            Catalog(descriptor), deviceService, deviceSelection, SessionFactory(engine));

        // Act: refresh the shared device-selection panel while idle, with listening never started
        var exception = await Record.ExceptionAsync(() => deviceSelection.Refresh());

        // Assert: the refresh completes without throwing, no session was ever created, and the
        // panel remains idle
        Assert.Null(exception);
        Assert.Equal(0, engine.CreateSessionCallCount);
        Assert.Equal(RecognitionStreamingState.Idle, viewModel.State);
    }

    /// <summary>
    ///     Proves that Stop ends an in-flight session deterministically without disposing the
    ///     cached engine - it is reused across Start/Stop cycles (see
    ///     <see cref="RecognitionPanelViewModel"/>'s "Engine/session reuse" remarks) - and reports
    ///     the stop rather than an error.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_Stop_DuringListening_StopsWithoutDisposingSession()
    {
        // Arrange: a listening session
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = CaptureDevice();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var session = new FakeRecognitionSession();
        var engine = new FakeSpeechRecognizerEngine { SessionFactory = _ => session };
        var viewModel = new RecognitionPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), SessionFactory(engine));
        await viewModel.StartCommand.ExecuteAsync(null);

        // Act: stop
        await viewModel.StopCommand.ExecuteAsync(null);

        // Assert: the session was stopped, is released (single-use), but the engine itself is
        // kept alive (model stays loaded), and the panel reports the stop
        Assert.Equal(1, session.StopCallCount);
        Assert.Equal(0, engine.DisposeCallCount);
        Assert.Equal(RecognitionStreamingState.Idle, viewModel.State);
        Assert.Equal(RecognitionPanelViewModel.StoppedMessage, viewModel.StatusMessage);
    }

    /// <summary>
    ///     Proves the central "Engine reuse" guarantee: starting, stopping, and starting again for
    ///     the same model/capture-device selection loads the engine only once, instead of
    ///     reloading its model on every Start click, even though - because a session is
    ///     single-use - a fresh session is created for each run.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_StartStopStart_SameSelection_ReusesEngine()
    {
        // Arrange: a panel ready to listen
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = CaptureDevice();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var engine = new FakeSpeechRecognizerEngine();
        var sessionFactory = SessionFactory(engine);
        var viewModel = new RecognitionPanelViewModel(Catalog(descriptor), deviceService, DeviceSelection(), sessionFactory);

        // Act: start, stop, start again - the same selection throughout
        await viewModel.StartCommand.ExecuteAsync(null);
        await viewModel.StopCommand.ExecuteAsync(null);
        await viewModel.StartCommand.ExecuteAsync(null);

        // Assert: the expensive engine-load step ran exactly once; a fresh single-use session
        // was created for each of the two runs
        await sessionFactory.Received(1).LoadAsync(Arg.Any<ISpeechModel>(), Arg.Any<CancellationToken>());
        Assert.Equal(2, engine.CreateSessionCallCount);
        Assert.All(engine.CreatedSessions, createdSession => Assert.Equal(1, createdSession.StartCallCount));
    }

    /// <summary>
    ///     Proves that a device refresh invalidates only the cached session (not the engine): a
    ///     Start after the refresh builds a fresh session/device pair without reloading the
    ///     engine.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_DeviceRefresh_InvalidatesCachedSessionButNotEngine()
    {
        // Arrange: a panel with a cached (idle) session from a prior Start/Stop cycle
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(_ => CaptureDevice());
        var engine = new FakeSpeechRecognizerEngine();
        var sessionFactory = SessionFactory(engine);
        var deviceSelection = DeviceSelection();
        var viewModel = new RecognitionPanelViewModel(Catalog(descriptor), deviceService, deviceSelection, sessionFactory);
        await viewModel.StartCommand.ExecuteAsync(null);
        await viewModel.StopCommand.ExecuteAsync(null);
        var firstSession = engine.CreatedSessions.Single();

        // Act: refresh the shared device-selection panel while idle, then start again
        await deviceSelection.Refresh();
        await viewModel.StartCommand.ExecuteAsync(null);

        // Assert: the stale session was released by the refresh, a second session was created
        // from the same cached engine, and the engine was never reloaded
        Assert.Equal(1, firstSession.DisposeCallCount);
        Assert.Equal(2, engine.CreateSessionCallCount);
        await sessionFactory.Received(1).LoadAsync(Arg.Any<ISpeechModel>(), Arg.Any<CancellationToken>());
        Assert.Equal(1, engine.CreatedSessions[1].StartCallCount);
    }

    /// <summary>
    ///     Proves that changing the selected recognition model invalidates a cached (idle)
    ///     engine, so the next Start loads a new engine for the newly selected model instead of
    ///     reusing one loaded for the previous model.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_SelectedModelChanged_InvalidatesCachedEngine()
    {
        // Arrange: a panel with two installed models and a cached (idle) engine for the first
        var firstDescriptor = FakeSpeechModel.Descriptor("stt-a", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var secondDescriptor = FakeSpeechModel.Descriptor("stt-b", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(_ => CaptureDevice());
        var engine = new FakeSpeechRecognizerEngine();
        var viewModel = new RecognitionPanelViewModel(
            Catalog(firstDescriptor, secondDescriptor), deviceService, DeviceSelection(), SessionFactory(engine));
        await viewModel.StartCommand.ExecuteAsync(null);
        await viewModel.StopCommand.ExecuteAsync(null);

        // Act: pick the other installed model while idle
        viewModel.SelectedModel = viewModel.AvailableModels.Single(model => model.Id == "stt-b");
        await Task.Yield();

        // Assert: the engine cached for the previous model was disposed
        Assert.Equal(1, engine.DisposeCallCount);
    }

    /// <summary>
    ///     Proves that changing the selected recognition model while a session is actively
    ///     listening stops that session and invalidates the cached engine, rather than disposing
    ///     it without stopping first - which would leave <c>State</c> stuck at <c>Listening</c>
    ///     forever, since a later Stop would see no cached session and no-op.
    ///     <c>SelectedModel</c> has a public setter and is not guarded against this at the
    ///     property level (only the view disables the model picker while listening), so the
    ///     change can arrive mid-session.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_SelectedModelChanged_WhileListening_StopsAndInvalidatesEngine()
    {
        // Arrange: a panel with two installed models and an actively listening session for the
        // first
        var firstDescriptor = FakeSpeechModel.Descriptor("stt-a", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var secondDescriptor = FakeSpeechModel.Descriptor("stt-b", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(_ => CaptureDevice());
        var session = new FakeRecognitionSession();
        var engine = new FakeSpeechRecognizerEngine { SessionFactory = _ => session };
        var viewModel = new RecognitionPanelViewModel(
            Catalog(firstDescriptor, secondDescriptor), deviceService, DeviceSelection(), SessionFactory(engine));
        await viewModel.StartCommand.ExecuteAsync(null);

        // Act: pick the other installed model while still listening (bypassing the view's
        // disabled picker, e.g. a direct property set)
        viewModel.SelectedModel = viewModel.AvailableModels.Single(model => model.Id == "stt-b");
        await Task.Yield();

        // Assert: the active session was stopped and the engine was disposed, and the panel is
        // not stuck in Listening
        Assert.Equal(1, session.StopCallCount);
        Assert.Equal(1, engine.DisposeCallCount);
        Assert.Equal(RecognitionStreamingState.Idle, viewModel.State);
    }

    /// <summary>
    ///     Proves that changing the selected capture device invalidates only a cached (idle)
    ///     session - never the cached engine - so the next Start builds a session bound to the
    ///     newly selected device while reusing the already-loaded engine.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_SelectedCaptureDeviceChanged_InvalidatesSessionButNotEngine()
    {
        // Arrange: a panel sharing a device-selection panel reporting two capture devices, with a
        // cached (idle) session bound to the first
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceA = new AudioDeviceDescription("Mic A", AudioDeviceDirection.Capture, 1, 48_000);
        var deviceB = new AudioDeviceDescription("Mic B", AudioDeviceDirection.Capture, 1, 48_000);
        var deviceService = Substitute.For<IAudioDeviceService>();
        deviceService.EnumerateCaptureDevices().Returns([deviceA, deviceB]);
        deviceService.EnumeratePlaybackDevices().Returns([]);
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(_ => CaptureDevice());
        var engine = new FakeSpeechRecognizerEngine();
        var deviceSelection = new DeviceSelectionViewModel(deviceService);
        var viewModel = new RecognitionPanelViewModel(Catalog(descriptor), deviceService, deviceSelection, SessionFactory(engine));
        await viewModel.StartCommand.ExecuteAsync(null);
        await viewModel.StopCommand.ExecuteAsync(null);
        var firstSession = engine.CreatedSessions.Single();

        // Act: pick the other capture device while idle
        deviceSelection.SelectedCaptureDevice = deviceB;
        await Task.Yield();

        // Assert: the session cached for the previous device was disposed, but the engine was
        // never reloaded
        Assert.Equal(1, firstSession.DisposeCallCount);
        Assert.Equal(0, engine.DisposeCallCount);
    }

    /// <summary>
    ///     Proves that changing the selected capture device while a session is actively listening
    ///     stops that session and invalidates the cached session (not the engine), rather than
    ///     silently leaving it bound to the now-abandoned device: the capture picker is not
    ///     disabled while listening (unlike the model picker; see
    ///     <see cref="RecognitionPanelViewModel.CanChangeModel"/>), so this change can arrive
    ///     mid-session.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_SelectedCaptureDeviceChanged_WhileListening_StopsAndInvalidatesSession()
    {
        // Arrange: a panel sharing a device-selection panel reporting two capture devices, with
        // an actively listening session bound to the first
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceA = new AudioDeviceDescription("Mic A", AudioDeviceDirection.Capture, 1, 48_000);
        var deviceB = new AudioDeviceDescription("Mic B", AudioDeviceDirection.Capture, 1, 48_000);
        var deviceService = Substitute.For<IAudioDeviceService>();
        deviceService.EnumerateCaptureDevices().Returns([deviceA, deviceB]);
        deviceService.EnumeratePlaybackDevices().Returns([]);
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(_ => CaptureDevice());
        var session = new FakeRecognitionSession();
        var engine = new FakeSpeechRecognizerEngine { SessionFactory = _ => session };
        var deviceSelection = new DeviceSelectionViewModel(deviceService);
        var viewModel = new RecognitionPanelViewModel(Catalog(descriptor), deviceService, deviceSelection, SessionFactory(engine));
        await viewModel.StartCommand.ExecuteAsync(null);

        // Act: pick the other capture device while still listening
        deviceSelection.SelectedCaptureDevice = deviceB;
        await Task.Yield();

        // Assert: the active session was stopped and released, but the engine persists
        Assert.Equal(1, session.StopCallCount);
        Assert.Equal(1, session.DisposeCallCount);
        Assert.Equal(0, engine.DisposeCallCount);
        Assert.Equal(RecognitionStreamingState.Idle, viewModel.State);
    }

    /// <summary>
    ///     Proves that Stop with nothing listening is a safe no-op, matching the library's own
    ///     "stop when not running" contract.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_Stop_NothingListening_IsSafeNoOp()
    {
        // Arrange: a freshly composed panel that never started
        var viewModel = new RecognitionPanelViewModel(
            Catalog(), Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<IRecognizerSessionFactory>());

        // Act: stop with nothing running
        var exception = await Record.ExceptionAsync(() => viewModel.StopCommand.ExecuteAsync(null));

        // Assert: no fault, and the panel remains idle with no status to report
        Assert.Null(exception);
        Assert.Equal(RecognitionStreamingState.Idle, viewModel.State);
        Assert.Null(viewModel.StatusMessage);
    }

    /// <summary>
    ///     Proves that calling Stop twice concurrently - as <see cref="CommunityToolkit.Mvvm.Input.AsyncRelayCommand"/>'s
    ///     <c>AllowConcurrentExecutions</c> permits - both complete without throwing, rather than
    ///     racing to double-dispose the same session.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_Stop_CalledTwiceConcurrently_BothCompleteWithoutThrowing()
    {
        // Arrange: a listening session
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = CaptureDevice();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var session = new FakeRecognitionSession();
        var engine = new FakeSpeechRecognizerEngine { SessionFactory = _ => session };
        var viewModel = new RecognitionPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), SessionFactory(engine));
        await viewModel.StartCommand.ExecuteAsync(null);

        // Act: invoke Stop twice without awaiting the first before starting the second
        var first = viewModel.StopCommand.ExecuteAsync(null);
        var second = viewModel.StopCommand.ExecuteAsync(null);
        var exception = await Record.ExceptionAsync(() => Task.WhenAll(first, second));

        // Assert: both complete without throwing, and the panel settles at Idle
        Assert.Null(exception);
        Assert.Equal(RecognitionStreamingState.Idle, viewModel.State);
    }

    /// <summary>
    ///     Proves that <see cref="RecognitionPanelViewModel.DisposeAsync"/> releases an active
    ///     session cleanly without throwing, idempotently.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_Dispose_ReleasesActiveSessionWithoutThrowing()
    {
        // Arrange: a listening session
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = CaptureDevice();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var session = new FakeRecognitionSession();
        var engine = new FakeSpeechRecognizerEngine { SessionFactory = _ => session };
        var viewModel = new RecognitionPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), SessionFactory(engine));
        await viewModel.StartCommand.ExecuteAsync(null);

        // Act: dispose the panel directly (as the shell would on shutdown) and again for
        // idempotency
        var exception = await Record.ExceptionAsync(async () =>
        {
            await viewModel.DisposeAsync();
            await viewModel.DisposeAsync();
        });

        // Assert: no fault, and the session/engine were each released exactly once
        Assert.Null(exception);
        Assert.Equal(1, session.DisposeCallCount);
        Assert.Equal(1, engine.DisposeCallCount);
    }

    /// <summary>
    ///     Proves that a <see cref="IModelCatalogService.ModelInstalled"/> event for a
    ///     recognition-role model causes the panel to refresh itself automatically, without a
    ///     manual Refresh click.
    /// </summary>
    [Fact]
    public void RecognitionPanelViewModel_ModelInstalled_MatchingRole_TriggersRefresh()
    {
        // Arrange: a panel composed over a catalog reporting no installed models yet
        var catalog = Substitute.For<IModelCatalogService>();
        var descriptors = Array.Empty<SpeechModelDescriptor>();
        catalog.Enumerate().Returns(_ => descriptors);
        var viewModel = new RecognitionPanelViewModel(
            catalog, Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<IRecognizerSessionFactory>());
        Assert.False(viewModel.HasModels);

        // Act: the catalog now reports a newly installed recognition model, and raises the event
        descriptors = [FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition)];
        catalog.ModelInstalled += Raise.Event<EventHandler<ModelInstalledEventArgs>>(
            catalog, new ModelInstalledEventArgs("stt", SpeechModelRole.Recognition));

        // Assert: the panel refreshed itself and now shows the newly installed model
        Assert.True(viewModel.HasModels);
        Assert.Contains(viewModel.AvailableModels, model => model.Id == "stt");
    }

    /// <summary>
    ///     Proves that a <see cref="IModelCatalogService.ModelInstalled"/> event for a
    ///     synthesis-role model is ignored by this panel, since it only cares about recognition
    ///     models.
    /// </summary>
    [Fact]
    public void RecognitionPanelViewModel_ModelInstalled_NonMatchingRole_DoesNotTriggerRefresh()
    {
        // Arrange: a panel composed over a catalog reporting no installed models yet
        var catalog = Substitute.For<IModelCatalogService>();
        var descriptors = Array.Empty<SpeechModelDescriptor>();
        catalog.Enumerate().Returns(_ => descriptors);
        var viewModel = new RecognitionPanelViewModel(
            catalog, Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<IRecognizerSessionFactory>());
        Assert.False(viewModel.HasModels);

        // Act: the catalog now has a newly installed *synthesis* model - irrelevant to this panel
        descriptors = [FakeSpeechModel.Descriptor("tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis)];
        catalog.ModelInstalled += Raise.Event<EventHandler<ModelInstalledEventArgs>>(
            catalog, new ModelInstalledEventArgs("tts", SpeechModelRole.Synthesis));

        // Assert: the panel did not refresh - still reports no models despite the catalog change
        Assert.False(viewModel.HasModels);
    }

    /// <summary>
    ///     Proves that <see cref="RecognitionPanelViewModel.DisposeAsync"/> unsubscribes from
    ///     <see cref="IModelCatalogService.ModelInstalled"/>, so a later install completing after
    ///     disposal is never applied.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_Dispose_UnsubscribesFromModelInstalled_NoRefreshAfterDispose()
    {
        // Arrange: a panel composed over a catalog reporting no installed models yet
        var catalog = Substitute.For<IModelCatalogService>();
        var descriptors = Array.Empty<SpeechModelDescriptor>();
        catalog.Enumerate().Returns(_ => descriptors);
        var viewModel = new RecognitionPanelViewModel(
            catalog, Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<IRecognizerSessionFactory>());

        // Act: dispose the panel, then simulate a later install completing
        await viewModel.DisposeAsync();
        descriptors = [FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition)];
        var exception = Record.Exception(() => catalog.ModelInstalled += Raise.Event<EventHandler<ModelInstalledEventArgs>>(
            catalog, new ModelInstalledEventArgs("stt", SpeechModelRole.Recognition)));

        // Assert: no fault, and the panel did not pick up the later install since it had already
        // unsubscribed
        Assert.Null(exception);
        Assert.False(viewModel.HasModels);
    }

    /// <summary>
    ///     Proves that <see cref="RecognitionPanelViewModel.CanChangeModel"/> is <see langword="true"/>
    ///     at <see cref="RecognitionStreamingState.Idle"/> and <see cref="RecognitionStreamingState.Error"/>,
    ///     <see langword="false"/> while <see cref="RecognitionStreamingState.Listening"/>, and
    ///     reverts to <see langword="true"/> after Stop is invoked,
    ///     so the model-selection control is disabled only while a session is actively streaming.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_CanChangeModel_TogglesAcrossStateTransitions()
    {
        // Arrange: an installed model, an available device, and a session that starts cleanly
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = CaptureDevice();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var engine = new FakeSpeechRecognizerEngine();
        var viewModel = new RecognitionPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), SessionFactory(engine));

        // Assert: true at the initial Idle state
        Assert.True(viewModel.CanChangeModel);

        // Act: start listening
        await viewModel.StartCommand.ExecuteAsync(null);

        // Assert: false while actively listening
        Assert.Equal(RecognitionStreamingState.Listening, viewModel.State);
        Assert.False(viewModel.CanChangeModel);

        // Act: stop
        await viewModel.StopCommand.ExecuteAsync(null);

        // Assert: reverts to true after Stop
        Assert.Equal(RecognitionStreamingState.Idle, viewModel.State);
        Assert.True(viewModel.CanChangeModel);
    }

    /// <summary>
    ///     Proves that <see cref="RecognitionPanelViewModel.CanChangeModel"/> is <see langword="true"/>
    ///     at the <see cref="RecognitionStreamingState.Error"/> state, so a user can pick a
    ///     different model after a failed Start.
    /// </summary>
    [Fact]
    public async Task RecognitionPanelViewModel_CanChangeModel_ErrorState_IsTrue()
    {
        // Arrange: a panel with no installed models, so Start reports the Error state
        var viewModel = new RecognitionPanelViewModel(
            Catalog(), Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<IRecognizerSessionFactory>());
        await viewModel.StartCommand.ExecuteAsync(null);
        Assert.Equal(RecognitionStreamingState.Error, viewModel.State);

        // Act & Assert: the model-selection control remains enabled at the Error state
        Assert.True(viewModel.CanChangeModel);
    }
}
