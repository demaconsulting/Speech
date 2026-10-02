using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Demo.DeviceSelectionSubsystem;
using DemaConsulting.Speech.Demo.ModelCatalogSubsystem;
using DemaConsulting.Speech.Demo.ModelSettingsSubsystem;
using DemaConsulting.Speech.Demo.SynthesisPanelSubsystem;
using DemaConsulting.Speech.Demo.Tests.Fakes;
using DemaConsulting.Speech.ModelManagementSubsystem;
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
    ///     Play, closing the "settings bag has no real consumer" gap the demo previously
    ///     documented.
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
    ///     Proves the central bugfix this redesign exists for: calling Play twice in a row with
    ///     the selected model and settings parameter values unchanged reuses the exact same
    ///     cached engine and session, rather than reloading the model and recreating the session
    ///     on every click.
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
    ///     Audio Tag vocabulary and include the tags this phase's task explicitly calls out.
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
}
