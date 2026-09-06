using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Demo.DeviceSelectionSubsystem;
using DemaConsulting.Speech.Demo.ModelCatalogSubsystem;
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
            () => new SynthesisPanelViewModel(
                null!, deviceService, deviceSelection, sessionFactory));
        Assert.Throws<ArgumentNullException>(
            () => new SynthesisPanelViewModel(
                catalog, null!, deviceSelection, sessionFactory));
        Assert.Throws<ArgumentNullException>(
            () => new SynthesisPanelViewModel(
                catalog, deviceService, null!, sessionFactory));
        Assert.Throws<ArgumentNullException>(
            () => new SynthesisPanelViewModel(
                catalog, deviceService, deviceSelection, null!));
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
    ///     the unavailable synthesizer, when the session seam cannot compose a working one.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_Play_SynthesizerUnavailable_ReportsErrorStateAndDisposes()
    {
        // Arrange: an installed model and available device, but a session factory that honestly
        // reports it cannot compose a working synthesizer
        var descriptor = FakeSpeechModel.Descriptor("tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = PlaybackDevice();
        deviceService.CreatePlaybackDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var synthesizer = Substitute.For<ISpeechSynthesizer>();
        synthesizer.IsAvailable.Returns(false);
        var sessionFactory = Substitute.For<ISynthesizerSessionFactory>();
        sessionFactory.Create(Arg.Any<ISpeechModel>(), Arg.Any<IAudioPlaybackDevice>(), Arg.Any<IReadOnlyDictionary<string, object>>()).Returns(synthesizer);
        var viewModel = new SynthesisPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), sessionFactory);

        // Act: attempt to play
        await viewModel.PlayCommand.ExecuteAsync(null);

        // Assert: the honest unavailable outcome, and the unusable synthesizer was released
        Assert.Equal(SynthesisPanelViewModel.SynthesizerUnavailableMessage, viewModel.StatusMessage);
        Assert.Equal(SynthesisPlaybackState.Error, viewModel.State);
        synthesizer.Received(1).Dispose();
    }

    /// <summary>
    ///     Proves that the embedded <c>ModelSettingsViewModel.BuildValueBag</c>
    ///     content genuinely reaches the injected <see cref="ISynthesizerSessionFactory.Create"/>
    ///     call during Play, closing the "settings bag has no real consumer" gap the demo
    ///     previously documented.
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
        var synthesizer = Substitute.For<ISpeechSynthesizer>();
        synthesizer.IsAvailable.Returns(true);
        synthesizer.SpeakAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var sessionFactory = Substitute.For<ISynthesizerSessionFactory>();
        sessionFactory.Create(Arg.Any<ISpeechModel>(), Arg.Any<IAudioPlaybackDevice>(), Arg.Any<IReadOnlyDictionary<string, object>>()).Returns(synthesizer);
        var viewModel = new SynthesisPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), sessionFactory)
        {
            Text = "Hello world",
        };

        // Act
        await viewModel.PlayCommand.ExecuteAsync(null);

        // Assert: the value bag built from the embedded settings panel reached Create
        sessionFactory.Received(1).Create(
            Arg.Any<ISpeechModel>(),
            Arg.Any<IAudioPlaybackDevice>(),
            Arg.Is<IReadOnlyDictionary<string, object>>(bag => IsBellaVoiceBag(bag)));
    }

    /// <summary>Matches a value bag containing exactly the expected "voice" -&gt; "af_bella" entry.</summary>
    private static bool IsBellaVoiceBag(IReadOnlyDictionary<string, object> bag) =>
        bag.Count == 1 && bag.TryGetValue("voice", out var value) && Equals(value, "af_bella");

    /// <summary>
    ///     Proves that a full, successful Play lifecycle transitions through Synthesizing then
    ///     Playing before settling on Idle, and disposes the synthesizer afterward.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_Play_SuccessfulSession_TransitionsThroughLifecycleToIdle()
    {
        // Arrange: an installed model, an available device, and a synthesizer that speaks
        // successfully
        var descriptor = FakeSpeechModel.Descriptor("tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = PlaybackDevice();
        deviceService.CreatePlaybackDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var synthesizer = Substitute.For<ISpeechSynthesizer>();
        synthesizer.IsAvailable.Returns(true);
        synthesizer.SpeakAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var sessionFactory = Substitute.For<ISynthesizerSessionFactory>();
        sessionFactory.Create(Arg.Any<ISpeechModel>(), Arg.Any<IAudioPlaybackDevice>(), Arg.Any<IReadOnlyDictionary<string, object>>()).Returns(synthesizer);
        var viewModel = new SynthesisPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), sessionFactory)
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
        // the exact text was forwarded, and the synthesizer was released
        Assert.Equal(
            [SynthesisPlaybackState.Synthesizing, SynthesisPlaybackState.Playing, SynthesisPlaybackState.Idle],
            states);
        Assert.Null(viewModel.StatusMessage);
        await synthesizer.Received(1).SpeakAsync("Hello [whispers] world", Arg.Any<CancellationToken>());
        synthesizer.Received(1).Dispose();
    }

    /// <summary>
    ///     Proves that <see cref="SynthesisPanelViewModel.CanChangeModel"/> is <see langword="true"/>
    ///     at <see cref="SynthesisPlaybackState.Idle"/>, <see langword="false"/> during both
    ///     <see cref="SynthesisPlaybackState.Synthesizing"/> and <see cref="SynthesisPlaybackState.Playing"/>,
    ///     and reverts to <see langword="true"/> once playback settles back to Idle - so the
    ///     model-selection control and its embedded settings panel are disabled for the entire
    ///     active session, not just while audio is actually playing.
    /// </summary>
    [Fact]
    public async Task SynthesisPanelViewModel_Play_SuccessfulSession_CanChangeModelTogglesAcrossLifecycle()
    {
        // Arrange: an installed model, an available device, and a synthesizer that speaks
        // successfully
        var descriptor = FakeSpeechModel.Descriptor("tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = PlaybackDevice();
        deviceService.CreatePlaybackDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var synthesizer = Substitute.For<ISpeechSynthesizer>();
        synthesizer.IsAvailable.Returns(true);
        synthesizer.SpeakAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var sessionFactory = Substitute.For<ISynthesizerSessionFactory>();
        sessionFactory.Create(Arg.Any<ISpeechModel>(), Arg.Any<IAudioPlaybackDevice>(), Arg.Any<IReadOnlyDictionary<string, object>>()).Returns(synthesizer);
        var viewModel = new SynthesisPanelViewModel(Catalog(descriptor), deviceService, DeviceSelection(), sessionFactory)
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
        // Arrange: a synthesizer whose SpeakAsync only completes when its token is canceled,
        // simulating an in-flight, indefinitely long utterance
        var descriptor = FakeSpeechModel.Descriptor("tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = PlaybackDevice();
        deviceService.CreatePlaybackDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var synthesizer = Substitute.For<ISpeechSynthesizer>();
        synthesizer.IsAvailable.Returns(true);
        synthesizer.SpeakAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.Delay(Timeout.Infinite, callInfo.ArgAt<CancellationToken>(1)));
        var sessionFactory = Substitute.For<ISynthesizerSessionFactory>();
        sessionFactory.Create(Arg.Any<ISpeechModel>(), Arg.Any<IAudioPlaybackDevice>(), Arg.Any<IReadOnlyDictionary<string, object>>()).Returns(synthesizer);
        var viewModel = new SynthesisPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), sessionFactory);

        // Act: start playback, then stop it before it would ever complete on its own
        var playTask = viewModel.PlayCommand.ExecuteAsync(null);
        viewModel.StopCommand.Execute(null);
        await playTask;

        // Assert: the session was stopped on the synthesizer itself, cancellation unwound the
        // task cleanly, and the panel reports the stop rather than an error
        synthesizer.Received(1).Stop();
        Assert.Equal(SynthesisPlaybackState.Idle, viewModel.State);
        Assert.Equal(SynthesisPanelViewModel.StoppedMessage, viewModel.StatusMessage);
        synthesizer.Received(1).Dispose();
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
    ///     Proves that <see cref="SynthesisPanelViewModel.Dispose"/> unsubscribes from
    ///     <see cref="IModelCatalogService.ModelInstalled"/>, so a later install completing after
    ///     disposal is never applied.
    /// </summary>
    [Fact]
    public void SynthesisPanelViewModel_Dispose_UnsubscribesFromModelInstalled_NoRefreshAfterDispose()
    {
        // Arrange: a panel composed over a catalog reporting no installed models yet
        var catalog = Substitute.For<IModelCatalogService>();
        var descriptors = Array.Empty<SpeechModelDescriptor>();
        catalog.Enumerate().Returns(_ => descriptors);
        var viewModel = new SynthesisPanelViewModel(
            catalog, Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<ISynthesizerSessionFactory>());

        // Act: dispose the panel, then simulate a later install completing
        viewModel.Dispose();
        descriptors = [FakeSpeechModel.Descriptor("tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis)];
        var exception = Record.Exception(() => catalog.ModelInstalled += Raise.Event<EventHandler<ModelInstalledEventArgs>>(
            catalog, new ModelInstalledEventArgs("tts", SpeechModelRole.Synthesis)));

        // Assert: no fault, and the panel did not pick up the later install since it had already
        // unsubscribed
        Assert.Null(exception);
        Assert.False(viewModel.HasModels);
    }

    /// <summary>
    ///     Proves that <see cref="SynthesisPanelViewModel.Dispose"/> is safe to call with no
    ///     active synthesizer, and idempotent when called more than once - this is a new
    ///     capability on this class, so unlike <c>RecognitionPanelViewModel</c> it has no prior
    ///     coverage to rely on.
    /// </summary>
    [Fact]
    public void SynthesisPanelViewModel_Dispose_NoActiveSynthesizer_IsSafeAndIdempotent()
    {
        // Arrange: a freshly composed panel that never played anything
        var viewModel = new SynthesisPanelViewModel(
            Catalog(), Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<ISynthesizerSessionFactory>());

        // Act: dispose twice
        var exception = Record.Exception(() =>
        {
            viewModel.Dispose();
            viewModel.Dispose();
        });

        // Assert: no fault
        Assert.Null(exception);
    }
}
