using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Demo.DeviceSelectionSubsystem;
using DemaConsulting.Speech.Demo.ModelCatalogSubsystem;
using DemaConsulting.Speech.Demo.ModelSettingsSubsystem;
using DemaConsulting.Speech.Demo.SynthesisPanelSubsystem;
using DemaConsulting.Speech.Demo.Tests.Fakes;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using NSubstitute;

namespace DemaConsulting.Speech.Demo.Tests.SynthesisPanelSubsystem;

/// <summary>
///     Unit tests for <see cref="SynthesisPanelViewModel"/>.
/// </summary>
public class SynthesisPanelViewModelTests
{
    /// <summary>A reusable fake playback device description.</summary>
    private static readonly AudioDeviceDescription SpeakerA = new("Speaker A", AudioDeviceDirection.Playback, 2, 44100);

    /// <summary>A second, distinct fake playback device description.</summary>
    private static readonly AudioDeviceDescription SpeakerB = new("Speaker B", AudioDeviceDirection.Playback, 2, 48000);

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
    ///     Builds a device-selection panel over the supplied playback devices, with the first
    ///     selected.
    /// </summary>
    /// <param name="devices">The playback devices to offer.</param>
    /// <returns>The composed panel.</returns>
    private static DeviceSelectionViewModel DeviceSelection(params AudioDeviceDescription[] devices)
    {
        var service = Substitute.For<IAudioDeviceService>();
        service.EnumerateCaptureDevices().Returns([]);
        service.EnumeratePlaybackDevices().Returns(devices);
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
    ///     Builds an available playback device fake.
    /// </summary>
    /// <param name="isAvailable">Whether the fake reports itself as available.</param>
    /// <returns>The composed fake.</returns>
    private static IAudioPlaybackDevice PlaybackDevice(bool isAvailable = true)
    {
        var device = Substitute.For<IAudioPlaybackDevice>();
        device.IsAvailable.Returns(isAvailable);
        return device;
    }

    /// <summary>
    ///     Builds a session-factory substitute whose <c>LoadAsync</c> returns each of the given
    ///     engines in order (the last is returned for any further call), for any model/parameter
    ///     combination.
    /// </summary>
    /// <param name="engines">The engine(s) to return from successive calls.</param>
    /// <returns>The composed substitute.</returns>
    private static ISynthesizerSessionFactory SessionFactory(params FakeSpeechSynthesizerEngine[] engines)
    {
        var factory = Substitute.For<ISynthesizerSessionFactory>();
        var tasks = engines.Select(engine => Task.FromResult<ISpeechSynthesizerEngine>(engine)).ToArray();
        factory.LoadAsync(
                Arg.Any<ISpeechModel>(),
                Arg.Any<IReadOnlyDictionary<string, object>?>(),
                Arg.Any<CancellationToken>())
            .Returns(tasks[0], tasks[1..]);
        return factory;
    }

    /// <summary>
    ///     Proves that the panel rejects any missing constructor dependency.
    /// </summary>
    [Fact]
    public void SynthesisPanelViewModel_Constructor_NullDependency_ThrowsArgumentNullException()
    {
        // Arrange: valid instances of everything, for substituting one null at a time
        var catalog = Catalog();
        var deviceService = Substitute.For<IAudioDeviceService>();
        var deviceSelection = DeviceSelection();
        var sessionFactory = Substitute.For<ISynthesizerSessionFactory>();

        // Act & Assert: each missing dependency is a programming error
        Assert.Throws<ArgumentNullException>(
            () => new SynthesisPanelViewModel(null!, deviceService, deviceSelection, sessionFactory));
        Assert.Throws<ArgumentNullException>(
            () => new SynthesisPanelViewModel(catalog, null!, deviceSelection, sessionFactory));
        Assert.Throws<ArgumentNullException>(
            () => new SynthesisPanelViewModel(catalog, deviceService, null!, sessionFactory));
        Assert.Throws<ArgumentNullException>(
            () => new SynthesisPanelViewModel(catalog, deviceService, deviceSelection, null!));
    }

    /// <summary>
    ///     Proves that a machine with no installed synthesis model reports the honest empty
    ///     state at construction, rather than an unexplained blank picker.
    /// </summary>
    [Fact]
    public void SynthesisPanelViewModel_Constructor_NoInstalledSynthesisModel_ReportsHonestEmptyState()
    {
        // Arrange & Act: compose the panel over an empty catalog
        var viewModel = new SynthesisPanelViewModel(
            Catalog(), Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<ISynthesizerSessionFactory>());

        // Assert: the panel reports nothing to choose from
        Assert.False(viewModel.HasModels);
        Assert.Empty(viewModel.AvailableModels);
        Assert.Null(viewModel.SelectedModel);
        Assert.False(viewModel.CanPlay);
    }

    /// <summary>
    ///     Proves that Refresh only offers installed models declaring the synthesis role,
    ///     excluding recognition models and not-yet-downloaded synthesis models.
    /// </summary>
    [Fact]
    public void SynthesisPanelViewModel_Refresh_MixedCatalog_OffersOnlyInstalledSynthesisModels()
    {
        // Arrange: a catalog mixing roles and install states
        var catalog = Catalog(
            FakeSpeechModel.Descriptor("tts-installed", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis),
            FakeSpeechModel.Descriptor("tts-pending", SpeechModelState.NotDownloaded, role: SpeechModelRole.Synthesis),
            FakeSpeechModel.Descriptor("stt-installed", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition));

        // Act: compose the panel
        var viewModel = new SynthesisPanelViewModel(
            catalog, Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<ISynthesizerSessionFactory>());

        // Assert: only the installed synthesis model is offered and selected
        Assert.True(viewModel.HasModels);
        Assert.Single(viewModel.AvailableModels);
        Assert.Equal("tts-installed", viewModel.SelectedModel?.Id);
    }

    /// <summary>
    ///     Proves that selecting a model applies its declared parameters to the embedded
    ///     settings panel.
    /// </summary>
    [Fact]
    public void SynthesisPanelViewModel_SelectedModel_Changed_UpdatesEmbeddedSettings()
    {
        // Arrange: two installed synthesis models, one with a declared parameter
        var parameter = new BooleanParameter("denoise", "Denoise", "Removes noise.", true);
        var withParameter = FakeSpeechModel.Descriptor(
            "with-parameter", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis, parameters: [parameter]);
        var withoutParameter = FakeSpeechModel.Descriptor(
            "without-parameter", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis);
        var viewModel = new SynthesisPanelViewModel(
            Catalog(withoutParameter, withParameter), Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<ISynthesizerSessionFactory>());

        // Act: select the model declaring a parameter
        viewModel.SelectedModel = withParameter.Model;

        // Assert: the embedded settings panel now presents that parameter
        Assert.Same(withParameter.Model, viewModel.Settings.Model);
        Assert.Single(viewModel.Settings.Parameters);
    }

    /// <summary>
    ///     Proves that Play reports the honest "no model selected" outcome rather than throwing
    ///     when invoked with nothing selected.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_Play_NoModelSelected_ReportsErrorState()
    {
        // Arrange: a panel with no installed models, so nothing can be selected
        var viewModel = new SynthesisPanelViewModel(
            Catalog(), Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<ISynthesizerSessionFactory>());

        // Act: invoke Play directly (bypassing the command's own CanExecute gate, which already
        // disables the button for this state) to prove the defensive guard behaves honestly too
        await viewModel.PlayCommand.ExecuteAsync(null);

        // Assert: an honest, explanatory error - not an exception
        Assert.Equal(SynthesisPanelViewModel.NoModelSelectedMessage, viewModel.StatusMessage);
        Assert.Equal(SynthesisPlaybackState.Error, viewModel.State);
    }

    /// <summary>
    ///     Proves that Play reports the honest "no playback device" outcome when the device seam
    ///     reports none available.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_Play_NoPlaybackDevice_ReportsErrorState()
    {
        // Arrange: an installed model but a machine with no usable playback device
        var descriptor = FakeSpeechModel.Descriptor("tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var unavailableDevice = PlaybackDevice(false);
        deviceService.CreatePlaybackDevice(Arg.Any<AudioDeviceSelection?>()).Returns(unavailableDevice);
        var viewModel = new SynthesisPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), Substitute.For<ISynthesizerSessionFactory>());

        // Act: attempt to play
        await viewModel.PlayCommand.ExecuteAsync(null);

        // Assert: the honest device-unavailable outcome
        Assert.Equal(SynthesisPanelViewModel.NoPlaybackDeviceMessage, viewModel.StatusMessage);
        Assert.Equal(SynthesisPlaybackState.Error, viewModel.State);
    }

    /// <summary>
    ///     Proves that Play reports the honest "synthesizer unavailable" outcome, and disposes
    ///     the unavailable engine, when the session seam cannot compose a working one.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_Play_SynthesizerUnavailable_ReportsErrorStateAndDisposes()
    {
        // Arrange: an installed model and available device, but an engine that honestly reports
        // it cannot compose a working synthesizer
        var descriptor = FakeSpeechModel.Descriptor("tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = PlaybackDevice();
        deviceService.CreatePlaybackDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var engine = new FakeSpeechSynthesizerEngine { IsAvailable = false };
        var viewModel = new SynthesisPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), SessionFactory(engine));

        // Act: attempt to play
        await viewModel.PlayCommand.ExecuteAsync(null);

        // Assert: the honest unavailable outcome, and the unusable engine was released
        Assert.Equal(SynthesisPanelViewModel.SynthesizerUnavailableMessage, viewModel.StatusMessage);
        Assert.Equal(SynthesisPlaybackState.Error, viewModel.State);
        Assert.Equal(1, engine.DisposeCallCount);
    }

    /// <summary>
    ///     Proves that the embedded <c>ModelSettingsViewModel.BuildValueBag</c> content genuinely
    ///     reaches the injected <see cref="ISynthesizerSessionFactory.LoadAsync"/> call during
    ///     Play.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_Play_ModelDeclaresChoiceParameter_ForwardsValueBagToSessionFactory()
    {
        // Arrange: a synthesis model declaring a voice choice parameter
        IReadOnlyList<ISpeechModelParameter> parameters =
        [
            new ChoiceParameter(
                "voice",
                "Voice",
                "Voice selection.",
                [new ChoiceParameterOption("af_bella", "Bella")],
                "af_bella"),
        ];
        var descriptor = FakeSpeechModel.Descriptor(
            "tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis, parameters: parameters);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = PlaybackDevice();
        deviceService.CreatePlaybackDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var engine = new FakeSpeechSynthesizerEngine();
        var sessionFactory = SessionFactory(engine);
        var viewModel = new SynthesisPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), sessionFactory)
        {
            Text = "Hello world",
        };

        // Act
        await viewModel.PlayCommand.ExecuteAsync(null);

        // Assert: the value bag built from the embedded settings panel reached LoadAsync
        await sessionFactory.Received(1).LoadAsync(
            Arg.Any<ISpeechModel>(),
            Arg.Is<IReadOnlyDictionary<string, object>?>(bag => IsBellaVoiceBag(bag)),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Matches a value bag containing exactly the expected "voice" -&gt; "af_bella" entry.</summary>
    private static bool IsBellaVoiceBag(IReadOnlyDictionary<string, object>? bag) =>
        bag is not null && bag.Count == 1 && bag.TryGetValue("voice", out var value) && Equals(value, "af_bella");

    /// <summary>
    ///     Proves that a full, successful Play lifecycle transitions through Synthesizing then
    ///     Playing before settling on Idle, and disposes the session afterward via the
    ///     <see cref="ISynthesisSession.StateChanged"/> mapping.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_Play_SuccessfulSession_TransitionsThroughLifecycleToIdle()
    {
        // Arrange: an installed model, an available device, and a session that speaks
        // successfully
        var descriptor = FakeSpeechModel.Descriptor("tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = PlaybackDevice();
        deviceService.CreatePlaybackDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var engine = new FakeSpeechSynthesizerEngine();
        var viewModel = new SynthesisPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), SessionFactory(engine))
        {
            Text = "Hello [whispers] world",
        };
        var states = new List<SynthesisPlaybackState>();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SynthesisPanelViewModel.State))
            {
                states.Add(viewModel.State);
            }
        };

        // Act: play to completion
        await viewModel.PlayCommand.ExecuteAsync(null);

        // Assert: the lifecycle passed through synthesizing and playing before settling idle,
        // and the exact text was forwarded
        Assert.Equal(
            [SynthesisPlaybackState.Synthesizing, SynthesisPlaybackState.Playing, SynthesisPlaybackState.Idle],
            states);
        Assert.Null(viewModel.StatusMessage);
        var session = engine.CreatedSessions.Single();
        Assert.Equal(["Hello [whispers] world"], session.SpeakTexts);
    }

    /// <summary>
    ///     Proves that <see cref="SynthesisPanelViewModel.CanChangeModel"/> is <see langword="true"/>
    ///     at <see cref="SynthesisPlaybackState.Idle"/>, <see langword="false"/> during both
    ///     <see cref="SynthesisPlaybackState.Synthesizing"/> and <see cref="SynthesisPlaybackState.Playing"/>,
    ///     and reverts to <see langword="true"/> once playback settles back to Idle.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_Play_SuccessfulSession_CanChangeModelTogglesAcrossLifecycle()
    {
        // Arrange: an installed model, an available device, and a session that speaks
        // successfully
        var descriptor = FakeSpeechModel.Descriptor("tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = PlaybackDevice();
        deviceService.CreatePlaybackDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var engine = new FakeSpeechSynthesizerEngine();
        var viewModel = new SynthesisPanelViewModel(Catalog(descriptor), deviceService, DeviceSelection(), SessionFactory(engine))
        {
            Text = "Hello world",
        };
        var canChangeModelByState = new Dictionary<SynthesisPlaybackState, bool>();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SynthesisPanelViewModel.State))
            {
                canChangeModelByState[viewModel.State] = viewModel.CanChangeModel;
            }
        };

        // Assert: true at the initial Idle state
        Assert.True(viewModel.CanChangeModel);

        // Act: play to completion
        await viewModel.PlayCommand.ExecuteAsync(null);

        // Assert: false throughout both Synthesizing and Playing, and true again once Idle
        Assert.False(canChangeModelByState[SynthesisPlaybackState.Synthesizing]);
        Assert.False(canChangeModelByState[SynthesisPlaybackState.Playing]);
        Assert.True(canChangeModelByState[SynthesisPlaybackState.Idle]);
        Assert.True(viewModel.CanChangeModel);
    }

    /// <summary>
    ///     Proves that <see cref="SynthesisPanelViewModel.CanChangeModel"/> is <see langword="true"/>
    ///     at the <see cref="SynthesisPlaybackState.Error"/> state, so a user can pick a different
    ///     model after a failed Play.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_CanChangeModel_ErrorState_IsTrue()
    {
        // Arrange: a panel with no installed models, so Play reports the Error state
        var viewModel = new SynthesisPanelViewModel(
            Catalog(), Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<ISynthesizerSessionFactory>());
        await viewModel.PlayCommand.ExecuteAsync(null);
        Assert.Equal(SynthesisPlaybackState.Error, viewModel.State);

        // Act & Assert: the model-selection control remains enabled at the Error state
        Assert.True(viewModel.CanChangeModel);
    }

    /// <summary>
    ///     Proves that Stop cancels an in-flight Play session deterministically, leaving the
    ///     panel idle with an explanatory message rather than an unexplained abrupt stop.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_Stop_DuringPlayback_CancelsSessionAndReportsStopped()
    {
        // Arrange: a session whose SpeakAsync only completes when its token is canceled,
        // simulating an in-flight, indefinitely long utterance
        var descriptor = FakeSpeechModel.Descriptor("tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = PlaybackDevice();
        deviceService.CreatePlaybackDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var session = new FakeSynthesisSession
        {
            SpeakImplementation = (_, token) => Task.Delay(Timeout.Infinite, token),
        };
        var engine = new FakeSpeechSynthesizerEngine { SessionFactory = _ => session };
        var viewModel = new SynthesisPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), SessionFactory(engine));

        // Act: start playback, then stop it before it would ever complete on its own
        var playTask = viewModel.PlayCommand.ExecuteAsync(null);
        await viewModel.StopCommand.ExecuteAsync(null);
        await playTask;

        // Assert: the session was stopped, cancellation unwound the task cleanly, and the panel
        // reports the stop rather than an error
        Assert.Equal(1, session.StopCallCount);
        Assert.Equal(SynthesisPlaybackState.Idle, viewModel.State);
        Assert.Equal(SynthesisPanelViewModel.StoppedMessage, viewModel.StatusMessage);
    }

    /// <summary>
    ///     Proves that clicking the shared device-selection panel's Refresh while this panel's
    ///     Play is in flight requests Stop and awaits its actual completion before releasing the
    ///     cached session, letting the refresh succeed deterministically rather than racing the
    ///     still-open playback device.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_PreRefreshHook_WhilePlaying_StopsAndAwaitsExecutionTaskBeforeDeviceRefreshSucceeds()
    {
        // Arrange: a session whose SpeakAsync only completes when its token is canceled,
        // simulating an in-flight, indefinitely long utterance, sharing the panel's own
        // device-selection panel so Refresh() exercises the real registered hook
        var descriptor = FakeSpeechModel.Descriptor("tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = PlaybackDevice();
        deviceService.CreatePlaybackDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var session = new FakeSynthesisSession
        {
            SpeakImplementation = (_, token) => Task.Delay(Timeout.Infinite, token),
        };
        var engine = new FakeSpeechSynthesizerEngine { SessionFactory = _ => session };
        var deviceSelection = DeviceSelection();
        var viewModel = new SynthesisPanelViewModel(
            Catalog(descriptor), deviceService, deviceSelection, SessionFactory(engine));

        // Act: start playback without awaiting it, then refresh the shared device-selection
        // panel (as the "Refresh devices" button would)
        var playTask = viewModel.PlayCommand.ExecuteAsync(null);
        var exception = await Record.ExceptionAsync(() => deviceSelection.Refresh());

        // Assert: Stop was requested on the session, the device refresh completed without
        // throwing (proving the hook genuinely awaited PlayAsync's own completion rather than
        // just requesting cancellation and returning immediately), the session was released, and
        // the panel is idle
        Assert.Null(exception);
        Assert.Equal(1, session.StopCallCount);
        Assert.Equal(1, session.DisposeCallCount);
        Assert.Equal(SynthesisPlaybackState.Idle, viewModel.State);

        // Cleanup: the in-flight Play task has already completed by the time Refresh() returned
        // (the hook awaited it), but await it anyway for deterministic test teardown
        await playTask;
    }

    /// <summary>
    ///     Proves that the registered pre-refresh hook is a safe no-op when no Play session is in
    ///     flight, so a "Refresh devices" click while the panel is idle never calls Stop or awaits
    ///     anything it doesn't need to.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_PreRefreshHook_WhileIdle_IsNoOpAndDeviceRefreshSucceeds()
    {
        // Arrange: a panel with no Play session ever started, sharing its own device-selection
        // panel so Refresh() exercises the real registered hook
        var descriptor = FakeSpeechModel.Descriptor("tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = PlaybackDevice();
        deviceService.CreatePlaybackDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var engine = new FakeSpeechSynthesizerEngine();
        var deviceSelection = DeviceSelection();
        var viewModel = new SynthesisPanelViewModel(
            Catalog(descriptor), deviceService, deviceSelection, SessionFactory(engine));

        // Act: refresh the shared device-selection panel while idle, with no Play ever started
        var exception = await Record.ExceptionAsync(() => deviceSelection.Refresh());

        // Assert: the refresh completes without throwing, no session was ever created, and the
        // panel remains idle
        Assert.Null(exception);
        Assert.Equal(0, engine.CreateSessionCallCount);
        Assert.Equal(SynthesisPlaybackState.Idle, viewModel.State);
    }

    /// <summary>
    ///     Proves that calling Play twice in a row with the selected model and settings
    ///     parameter values unchanged reuses the exact same cached engine and session, rather
    ///     than reloading the model and recreating the session on every click.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_Play_CalledTwiceWithUnchangedModelAndParameters_ReusesSameSessionWithoutReload()
    {
        // Arrange: an installed model, an available device, and a session that speaks
        // successfully
        var descriptor = FakeSpeechModel.Descriptor("tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = PlaybackDevice();
        deviceService.CreatePlaybackDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var engine = new FakeSpeechSynthesizerEngine();
        var sessionFactory = SessionFactory(engine);
        var viewModel = new SynthesisPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), sessionFactory)
        {
            Text = "Hello world",
        };

        // Act: play twice in a row with nothing changed
        await viewModel.PlayCommand.ExecuteAsync(null);
        await viewModel.PlayCommand.ExecuteAsync(null);

        // Assert: the engine was loaded exactly once, exactly one session was created, and both
        // Play calls spoke through that same session
        await sessionFactory.Received(1).LoadAsync(
            Arg.Any<ISpeechModel>(), Arg.Any<IReadOnlyDictionary<string, object>?>(), Arg.Any<CancellationToken>());
        Assert.Equal(1, engine.CreateSessionCallCount);
        var session = engine.CreatedSessions.Single();
        Assert.Equal(["Hello world", "Hello world"], session.SpeakTexts);
    }

    /// <summary>
    ///     Proves that changing a settings parameter value between two Play calls reloads the
    ///     cached engine (since the engine was composed with the stale parameter value) and, as a
    ///     direct consequence, recreates the session built from it.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_Play_ParameterValueChanged_ReloadsEngineAndRecreatesSession()
    {
        // Arrange: a model declaring one boolean parameter, two engines to be loaded in sequence
        var parameter = new BooleanParameter("denoise", "Denoise", "Removes noise.", true);
        var descriptor = FakeSpeechModel.Descriptor(
            "tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis, parameters: [parameter]);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = PlaybackDevice();
        deviceService.CreatePlaybackDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var firstEngine = new FakeSpeechSynthesizerEngine();
        var secondEngine = new FakeSpeechSynthesizerEngine();
        var sessionFactory = SessionFactory(firstEngine, secondEngine);
        var viewModel = new SynthesisPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), sessionFactory)
        {
            Text = "Hello world",
        };

        // Act: play once, change the declared parameter's value, then play again
        await viewModel.PlayCommand.ExecuteAsync(null);
        var denoiseSetting = viewModel.Settings.Parameters.OfType<BooleanParameterViewModel>().Single();
        denoiseSetting.Value = !denoiseSetting.Value;
        await viewModel.PlayCommand.ExecuteAsync(null);

        // Assert: the engine was reloaded for the second Play (the stale first engine was
        // disposed), and a fresh session was created from the new engine
        await sessionFactory.Received(2).LoadAsync(
            Arg.Any<ISpeechModel>(), Arg.Any<IReadOnlyDictionary<string, object>?>(), Arg.Any<CancellationToken>());
        Assert.Equal(1, firstEngine.DisposeCallCount);
        Assert.Equal(1, firstEngine.CreateSessionCallCount);
        Assert.Equal(1, secondEngine.CreateSessionCallCount);
    }

    /// <summary>
    ///     Proves that changing the selected playback device between two Play calls recreates
    ///     only the cached session - rebound to the newly selected device - without reloading the
    ///     unrelated cached engine.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_Play_PlaybackDeviceChanged_RecreatesSessionButNotEngine()
    {
        // Arrange: an installed model, two distinct playback devices, and an engine shared across
        // both Play calls
        var descriptor = FakeSpeechModel.Descriptor("tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis);
        var deviceService = Substitute.For<IAudioDeviceService>();
        deviceService.CreatePlaybackDevice(Arg.Any<AudioDeviceSelection?>()).Returns(_ => PlaybackDevice());
        var engine = new FakeSpeechSynthesizerEngine();
        var sessionFactory = SessionFactory(engine);
        var deviceSelection = DeviceSelection(SpeakerA, SpeakerB);
        deviceSelection.SelectedPlaybackDevice = SpeakerA;
        var viewModel = new SynthesisPanelViewModel(
            Catalog(descriptor), deviceService, deviceSelection, sessionFactory)
        {
            Text = "Hello world",
        };

        // Act: play once, change the selected playback device, then play again
        await viewModel.PlayCommand.ExecuteAsync(null);
        var firstSession = engine.CreatedSessions.Single();
        deviceSelection.SelectedPlaybackDevice = SpeakerB;
        await viewModel.PlayCommand.ExecuteAsync(null);

        // Assert: the engine was loaded only once, but a second, distinct session was created
        await sessionFactory.Received(1).LoadAsync(
            Arg.Any<ISpeechModel>(), Arg.Any<IReadOnlyDictionary<string, object>?>(), Arg.Any<CancellationToken>());
        Assert.Equal(2, engine.CreateSessionCallCount);
        Assert.Equal(1, firstSession.DisposeCallCount);
        Assert.NotSame(firstSession, engine.CreatedSessions[1]);
    }

    /// <summary>
    ///     Proves that the example tag hints are drawn from the library's closed Natural Language
    ///     Audio Tag vocabulary and include the commonly used tags.
    /// </summary>
    [Fact]
    public void SynthesisPanelViewModel_ExampleTagHints_ContainsExpectedTags()
    {
        // Assert: the closed-vocabulary examples the task calls out are present
        Assert.Contains("[whispers]", SynthesisPanelViewModel.ExampleTagHints);
        Assert.Contains("[short pause]", SynthesisPanelViewModel.ExampleTagHints);
        Assert.Contains("[excited]", SynthesisPanelViewModel.ExampleTagHints);
    }

    /// <summary>
    ///     Proves that a <see cref="IModelCatalogService.ModelInstalled"/> event for a
    ///     synthesis-role model causes the panel to refresh itself automatically, without a
    ///     manual Refresh click.
    /// </summary>
    [Fact]
    public void SynthesisPanelViewModel_ModelInstalled_MatchingRole_TriggersRefresh()
    {
        // Arrange: a panel composed over a catalog reporting no installed models yet
        var catalog = Substitute.For<IModelCatalogService>();
        var descriptors = Array.Empty<SpeechModelDescriptor>();
        catalog.Enumerate().Returns(_ => descriptors);
        var viewModel = new SynthesisPanelViewModel(
            catalog, Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<ISynthesizerSessionFactory>());
        Assert.False(viewModel.HasModels);

        // Act: the catalog now reports a newly installed synthesis model, and raises the event
        descriptors = [FakeSpeechModel.Descriptor("tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis)];
        catalog.ModelInstalled += Raise.Event<EventHandler<ModelInstalledEventArgs>>(
            catalog, new ModelInstalledEventArgs("tts", SpeechModelRole.Synthesis));

        // Assert: the panel refreshed itself and now shows the newly installed model
        Assert.True(viewModel.HasModels);
        Assert.Contains(viewModel.AvailableModels, model => model.Id == "tts");
    }

    /// <summary>
    ///     Proves that a <see cref="IModelCatalogService.ModelInstalled"/> event for a
    ///     recognition-role model is ignored by this panel, since it only cares about synthesis
    ///     models.
    /// </summary>
    [Fact]
    public void SynthesisPanelViewModel_ModelInstalled_NonMatchingRole_DoesNotTriggerRefresh()
    {
        // Arrange: a panel composed over a catalog reporting no installed models yet
        var catalog = Substitute.For<IModelCatalogService>();
        var descriptors = Array.Empty<SpeechModelDescriptor>();
        catalog.Enumerate().Returns(_ => descriptors);
        var viewModel = new SynthesisPanelViewModel(
            catalog, Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<ISynthesizerSessionFactory>());
        Assert.False(viewModel.HasModels);

        // Act: the catalog now has a newly installed *recognition* model - irrelevant to this panel
        descriptors = [FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition)];
        catalog.ModelInstalled += Raise.Event<EventHandler<ModelInstalledEventArgs>>(
            catalog, new ModelInstalledEventArgs("stt", SpeechModelRole.Recognition));

        // Assert: the panel did not refresh - still reports no models despite the catalog change
        Assert.False(viewModel.HasModels);
    }

    /// <summary>
    ///     Proves that <see cref="SynthesisPanelViewModel.DisposeAsync"/> unsubscribes from
    ///     <see cref="IModelCatalogService.ModelInstalled"/>, so a later install completing after
    ///     disposal is never applied.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_DisposeAsync_UnsubscribesFromModelInstalled_NoRefreshAfterDispose()
    {
        // Arrange: a panel composed over a catalog reporting no installed models yet
        var catalog = Substitute.For<IModelCatalogService>();
        var descriptors = Array.Empty<SpeechModelDescriptor>();
        catalog.Enumerate().Returns(_ => descriptors);
        var viewModel = new SynthesisPanelViewModel(
            catalog, Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<ISynthesizerSessionFactory>());

        // Act: dispose the panel, then simulate a later install completing
        await viewModel.DisposeAsync();
        descriptors = [FakeSpeechModel.Descriptor("tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis)];
        var exception = Record.Exception(() => catalog.ModelInstalled += Raise.Event<EventHandler<ModelInstalledEventArgs>>(
            catalog, new ModelInstalledEventArgs("tts", SpeechModelRole.Synthesis)));

        // Assert: no fault, and the panel did not pick up the later install since it had already
        // unsubscribed
        Assert.Null(exception);
        Assert.False(viewModel.HasModels);
    }

    /// <summary>
    ///     Proves that <see cref="SynthesisPanelViewModel.DisposeAsync"/> is safe to call with no
    ///     active session, and idempotent when called more than once.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_DisposeAsync_NoActiveSession_IsSafeAndIdempotent()
    {
        // Arrange: a freshly composed panel that never played anything
        var viewModel = new SynthesisPanelViewModel(
            Catalog(), Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<ISynthesizerSessionFactory>());

        // Act: dispose twice
        var exception = await Record.ExceptionAsync(async () =>
        {
            await viewModel.DisposeAsync();
            await viewModel.DisposeAsync();
        });

        // Assert: no fault
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that a <see cref="IModelCatalogService.ReferenceVoicesChanged"/> event makes the
    ///     panel re-read the catalog (so a rebuilt model's new voice list is offered) and keeps the
    ///     voice the user had chosen where it is still available.
    /// </summary>
    [Fact]
    public void SynthesisPanelViewModel_ReferenceVoicesChanged_RefreshesAndKeepsChosenVoice()
    {
        // Arrange: a synthesis model with a 150-voice list; the user picks voice 120
        ISpeechModel NewModel(int count) => new FakeSpeechModel(
            "tts",
            "TTS",
            SpeechModelRole.Synthesis,
            [new ChoiceParameter(
                "voice",
                "Voice",
                "Reference voice.",
                [.. Enumerable.Range(0, count).Select(i => new ChoiceParameterOption($"v{i}", $"Voice {i}"))],
                "v0")]);
        var catalog = Substitute.For<IModelCatalogService>();
        var descriptors = new[] { new SpeechModelDescriptor(NewModel(10), SpeechModelState.Downloaded) };
        catalog.Enumerate().Returns(_ => descriptors);
        var viewModel = new SynthesisPanelViewModel(
            catalog, Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<ISynthesizerSessionFactory>());
        var first = viewModel.SelectedModel;

        // Act: a pack install grows the voice list on the rebuilt model; the event fires
        descriptors = [new SpeechModelDescriptor(NewModel(150), SpeechModelState.Downloaded)];
        ((ChoiceParameterViewModel)viewModel.Settings.Parameters[0]).SelectedOption =
            ((ChoiceParameterViewModel)viewModel.Settings.Parameters[0]).Options[5];
        catalog.ReferenceVoicesChanged += Raise.Event<EventHandler>(catalog, EventArgs.Empty);

        // Assert: a new model instance with 150 options, still on the previously chosen voice
        Assert.NotSame(first, viewModel.SelectedModel);
        var voice = (ChoiceParameterViewModel)viewModel.Settings.Parameters[0];
        Assert.Equal(150, voice.Options.Count);
        Assert.Equal("v5", voice.SelectedOption.Value);
    }

    /// <summary>
    ///     Proves that <see cref="SynthesisPanelViewModel.DisposeAsync"/> unsubscribes from
    ///     <see cref="IModelCatalogService.ReferenceVoicesChanged"/>.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_DisposeAsync_UnsubscribesFromReferenceVoicesChanged()
    {
        // Arrange
        var catalog = Substitute.For<IModelCatalogService>();
        var descriptors = Array.Empty<SpeechModelDescriptor>();
        catalog.Enumerate().Returns(_ => descriptors);
        var viewModel = new SynthesisPanelViewModel(
            catalog, Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<ISynthesizerSessionFactory>());

        // Act: dispose, then the catalog gains a model and raises the event
        await viewModel.DisposeAsync();
        descriptors = [FakeSpeechModel.Descriptor("tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis)];
        catalog.ReferenceVoicesChanged += Raise.Event<EventHandler>(catalog, EventArgs.Empty);

        // Assert: no refresh happened after disposal
        Assert.False(viewModel.HasModels);
    }

    /// <summary>
    ///     Proves that the consent note and the installed pack's attribution are shown for the
    ///     Chatterbox voice-cloning model, and nothing is shown for other models.
    /// </summary>
    [Fact]
    public void SynthesisPanelViewModel_VoiceNotice_ChatterboxShowsConsentAndAttribution_OthersShowNothing()
    {
        // Arrange: the real Chatterbox model (no store voices, so nothing is read from disk) and a pack that is installed
        var chatterbox = new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { IncludeStoreVoices = false });
        var catalog = Substitute.For<IModelCatalogService>();
        catalog.ReferenceVoicePacks.Returns([ReferenceVoicePack.VoiceZero10]);
        catalog.GetReferenceVoicePackState(ReferenceVoicePack.VoiceZero10.Id).Returns(SpeechModelState.Downloaded);
        var descriptors = new[] { new SpeechModelDescriptor(chatterbox, SpeechModelState.Downloaded) };
        catalog.Enumerate().Returns(_ => descriptors);

        // Act
        var viewModel = new SynthesisPanelViewModel(
            catalog, Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<ISynthesizerSessionFactory>());

        // Assert: Chatterbox shows consent + attribution
        Assert.True(viewModel.HasVoiceNotice);
        Assert.Contains(ReferenceVoiceLibrary.ConsentNotice, viewModel.VoiceNotice, StringComparison.Ordinal);
        Assert.Contains("OwenTyme", viewModel.VoiceNotice, StringComparison.Ordinal);

        // Act: a different synthesis model is selected instead
        descriptors = [FakeSpeechModel.Descriptor("other", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis)];
        viewModel.Refresh();

        // Assert: no voice notice
        Assert.False(viewModel.HasVoiceNotice);
        Assert.Null(viewModel.VoiceNotice);
    }

    /// <summary>
    ///     Builds a panel with one installed synthesis model, an available playback device and
    ///     the supplied engine, ready to Play "Hello world".
    /// </summary>
    /// <param name="engine">The engine the session seam returns.</param>
    /// <returns>The composed panel.</returns>
    private static SynthesisPanelViewModel PlayablePanel(FakeSpeechSynthesizerEngine engine)
    {
        var descriptor = FakeSpeechModel.Descriptor("tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var playbackDevice = PlaybackDevice();
        deviceService.CreatePlaybackDevice(Arg.Any<AudioDeviceSelection?>()).Returns(playbackDevice);
        return new SynthesisPanelViewModel(Catalog(descriptor), deviceService, DeviceSelection(), SessionFactory(engine))
        {
            Text = "Hello world",
        };
    }

    /// <summary>
    ///     Proves that Play speaks the entered text through a session bound to the playback
    ///     device the device service resolved for the current selection.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_Play_EnteredText_SpeaksTextThroughSelectedPlaybackDevice()
    {
        // Arrange: an installed model and an engine recording the device each session is bound to
        var descriptor = FakeSpeechModel.Descriptor("tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = PlaybackDevice();
        deviceService.CreatePlaybackDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        IAudioPlaybackDevice? boundDevice = null;
        var engine = new FakeSpeechSynthesizerEngine
        {
            SessionFactory = device =>
            {
                boundDevice = device;
                return new FakeSynthesisSession();
            },
        };
        var viewModel = new SynthesisPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), SessionFactory(engine))
        {
            Text = "Speak this",
        };

        // Act
        await viewModel.PlayCommand.ExecuteAsync(null);

        // Assert: the session is bound to the resolved device and speaks exactly the entered text
        Assert.Same(availableDevice, boundDevice);
        Assert.Equal(["Speak this"], engine.CreatedSessions.Single().SpeakTexts);
    }

    /// <summary>
    ///     Proves that a refresh keeps the user's selected model when it is still installed.
    /// </summary>
    [Fact]
    public void SynthesisPanelViewModel_Refresh_SelectedModelStillInstalled_PreservesSelection()
    {
        // Arrange: two installed synthesis models with the second selected
        var first = FakeSpeechModel.Descriptor("tts-a", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis);
        var second = FakeSpeechModel.Descriptor("tts-b", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis);
        var viewModel = new SynthesisPanelViewModel(
            Catalog(first, second), Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<ISynthesizerSessionFactory>());
        viewModel.SelectedModel = second.Model;

        // Act
        viewModel.Refresh();

        // Assert: the selection survived the refresh
        Assert.Equal("tts-b", viewModel.SelectedModel?.Id);
    }

    /// <summary>
    ///     Proves that while a Play is in flight the panel refuses to start another one, because
    ///     the Play command cannot execute outside the Idle and Error states.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_Play_WhilePlaying_IsRefused()
    {
        // Arrange: a session whose speech only ends when stopped
        var session = new FakeSynthesisSession
        {
            SpeakImplementation = (_, token) => Task.Delay(Timeout.Infinite, token),
        };
        var viewModel = PlayablePanel(new FakeSpeechSynthesizerEngine { SessionFactory = _ => session });

        // Act: start playback without awaiting it
        var playTask = viewModel.PlayCommand.ExecuteAsync(null);

        // Assert: the panel is busy, so Play cannot be started again
        Assert.Equal(SynthesisPlaybackState.Playing, viewModel.State);
        Assert.False(viewModel.CanPlay);
        Assert.False(viewModel.PlayCommand.CanExecute(null));

        // Cleanup
        await viewModel.StopCommand.ExecuteAsync(null);
        await playTask;
    }

    /// <summary>
    ///     Proves that a session the engine creates but reports unavailable is released and
    ///     reported honestly as an unavailable synthesizer.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_Play_UnavailableSession_ReportsErrorStateAndDisposesSession()
    {
        // Arrange: an available engine whose created session is unavailable
        var session = new FakeSynthesisSession { IsAvailable = false };
        var viewModel = PlayablePanel(new FakeSpeechSynthesizerEngine { SessionFactory = _ => session });

        // Act
        await viewModel.PlayCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(SynthesisPanelViewModel.SynthesizerUnavailableMessage, viewModel.StatusMessage);
        Assert.Equal(SynthesisPlaybackState.Error, viewModel.State);
        Assert.Equal(1, session.DisposeCallCount);
        Assert.Equal(0, session.SpeakCallCount);
    }

    /// <summary>
    ///     Proves that an engine reporting it is busy while creating the session surfaces the
    ///     engine's explanation as an error state rather than an exception.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_Play_EngineBusy_ReportsErrorStateWithEngineMessage()
    {
        // Arrange
        var engine = new FakeSpeechSynthesizerEngine
        {
            CreateSessionException = new SynthesisEngineBusyException("The engine is busy."),
        };
        var viewModel = PlayablePanel(engine);

        // Act
        await viewModel.PlayCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("The engine is busy.", viewModel.StatusMessage);
        Assert.Equal(SynthesisPlaybackState.Error, viewModel.State);
    }

    /// <summary>
    ///     Proves that a session fault is reported as an error, the faulted session is released
    ///     and the next Play creates a fresh session against the still-cached engine.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_Play_SessionFaulted_ReportsErrorReleasesSessionAndNextPlayRecovers()
    {
        // Arrange: the first session faults while speaking; later sessions work
        var createdCount = 0;
        var engine = new FakeSpeechSynthesizerEngine
        {
            SessionFactory = _ => new FakeSynthesisSession
            {
                SpeakException = createdCount++ == 0 ? new SynthesisSessionFaultedException("Session faulted.") : null,
            },
        };
        var viewModel = PlayablePanel(engine);

        // Act: the first Play faults
        await viewModel.PlayCommand.ExecuteAsync(null);

        // Assert: error reported and the faulted session released
        Assert.Equal(SynthesisPlaybackState.Error, viewModel.State);
        Assert.Equal("Session faulted.", viewModel.StatusMessage);
        Assert.Equal(1, engine.CreatedSessions[0].DisposeCallCount);

        // Act: the next Play
        await viewModel.PlayCommand.ExecuteAsync(null);

        // Assert: a fresh session from the same engine spoke successfully
        Assert.Equal(2, engine.CreateSessionCallCount);
        Assert.Equal(SynthesisPlaybackState.Idle, viewModel.State);
        Assert.Null(viewModel.StatusMessage);
    }

    /// <summary>
    ///     Proves that a <see cref="SpeechSynthesizerUnavailableException"/> raised while speaking
    ///     is reported as an error state carrying the exception's message.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_Play_SpeakThrowsSynthesizerUnavailable_ReportsErrorStateWithMessage()
    {
        // Arrange
        var session = new FakeSynthesisSession
        {
            SpeakException = new SpeechSynthesizerUnavailableException("Runtime missing."),
        };
        var viewModel = PlayablePanel(new FakeSpeechSynthesizerEngine { SessionFactory = _ => session });

        // Act
        await viewModel.PlayCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(SynthesisPlaybackState.Error, viewModel.State);
        Assert.Equal("Runtime missing.", viewModel.StatusMessage);
    }

    /// <summary>
    ///     Proves the mapping from session states to playback states: Stopping is Playing,
    ///     Created is Idle, and Faulted is Error with the faulted message.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_SessionStateChanged_StoppingCreatedFaulted_MapsToPlayingIdleError()
    {
        // Arrange: a session that raises extra states while speaking
        var session = new FakeSynthesisSession();
        var viewModel = PlayablePanel(new FakeSpeechSynthesizerEngine { SessionFactory = _ => session });
        var observed = new List<(SynthesisPlaybackState State, string? Message)>();
        session.SpeakImplementation = (_, _) =>
        {
            session.RaiseStateChanged(SynthesisSessionState.Stopping);
            observed.Add((viewModel.State, viewModel.StatusMessage));
            session.RaiseStateChanged(SynthesisSessionState.Created);
            observed.Add((viewModel.State, viewModel.StatusMessage));
            session.RaiseStateChanged(SynthesisSessionState.Faulted);
            observed.Add((viewModel.State, viewModel.StatusMessage));
            return Task.CompletedTask;
        };

        // Act
        await viewModel.PlayCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(SynthesisPlaybackState.Playing, observed[0].State);
        Assert.Equal(SynthesisPlaybackState.Idle, observed[1].State);
        Assert.Equal(SynthesisPlaybackState.Error, observed[2].State);
        Assert.Equal(SynthesisPanelViewModel.SessionFaultedMessage, observed[2].Message);
    }

    /// <summary>
    ///     Proves that disposing the panel while a Play is in flight stops the session, awaits the
    ///     Play and releases the cached session and engine.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_DisposeAsync_WhilePlaying_StopsPlayAndReleasesSessionAndEngine()
    {
        // Arrange: a session whose speech only ends when stopped
        var session = new FakeSynthesisSession
        {
            SpeakImplementation = (_, token) => Task.Delay(Timeout.Infinite, token),
        };
        var engine = new FakeSpeechSynthesizerEngine { SessionFactory = _ => session };
        var viewModel = PlayablePanel(engine);
        var playTask = viewModel.PlayCommand.ExecuteAsync(null);

        // Act
        await viewModel.DisposeAsync();
        await playTask;

        // Assert
        Assert.Equal(1, session.StopCallCount);
        Assert.Equal(1, session.DisposeCallCount);
        Assert.Equal(1, engine.DisposeCallCount);
    }

    /// <summary>
    ///     Proves that disposing after a completed Play releases the cached session and engine
    ///     exactly once, even when disposed twice.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_DisposeAsync_AfterPlay_ReleasesSessionAndEngineOnce()
    {
        // Arrange
        var engine = new FakeSpeechSynthesizerEngine();
        var viewModel = PlayablePanel(engine);
        await viewModel.PlayCommand.ExecuteAsync(null);

        // Act
        await viewModel.DisposeAsync();
        await viewModel.DisposeAsync();

        // Assert
        Assert.Equal(1, engine.CreatedSessions.Single().DisposeCallCount);
        Assert.Equal(1, engine.DisposeCallCount);
    }
}
