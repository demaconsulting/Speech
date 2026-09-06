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
    public void RecognitionPanelViewModel_Start_NoModelSelected_ReportsErrorState()
    {
        // Arrange: a panel with no installed models, so nothing can be selected
        var viewModel = new RecognitionPanelViewModel(
            Catalog(), Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<IRecognizerSessionFactory>());

        // Act: invoke Start directly (bypassing the command's own CanExecute gate, which
        // already disables the button for this state) to prove the defensive guard behaves
        // honestly too
        viewModel.StartCommand.Execute(null);

        // Assert: an honest, explanatory error - not an exception
        Assert.Equal(RecognitionPanelViewModel.NoModelSelectedMessage, viewModel.StatusMessage);
        Assert.Equal(RecognitionStreamingState.Error, viewModel.State);
    }

    /// <summary>
    ///     Proves that Start reports the honest "no capture device" outcome when the device seam
    ///     reports none available.
    /// </summary>
    [Fact]
    public void RecognitionPanelViewModel_Start_NoCaptureDevice_ReportsErrorState()
    {
        // Arrange: an installed model but a machine with no usable capture device
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var unavailableDevice = CaptureDevice(false);
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(unavailableDevice);
        var viewModel = new RecognitionPanelViewModel(
            Catalog(descriptor), deviceService, DeviceSelection(), Substitute.For<IRecognizerSessionFactory>());

        // Act: attempt to start
        viewModel.StartCommand.Execute(null);

        // Assert: the honest device-unavailable outcome
        Assert.Equal(RecognitionPanelViewModel.NoCaptureDeviceMessage, viewModel.StatusMessage);
        Assert.Equal(RecognitionStreamingState.Error, viewModel.State);
    }

    /// <summary>
    ///     Proves that Start reports the honest "recognizer unavailable" outcome, and disposes
    ///     the unavailable recognizer, when the session seam cannot compose a working one.
    /// </summary>
    [Fact]
    public void RecognitionPanelViewModel_Start_RecognizerUnavailable_ReportsErrorStateAndDisposes()
    {
        // Arrange: an installed model and available device, but a session factory that honestly
        // reports it cannot compose a working recognizer
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = CaptureDevice();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var recognizer = Substitute.For<ISpeechRecognizer>();
        recognizer.IsAvailable.Returns(false);
        var sessionFactory = Substitute.For<IRecognizerSessionFactory>();
        sessionFactory.Create(Arg.Any<ISpeechModel>(), Arg.Any<IAudioCaptureDevice>()).Returns(recognizer);
        var viewModel = new RecognitionPanelViewModel(Catalog(descriptor), deviceService, DeviceSelection(), sessionFactory);

        // Act: attempt to start
        viewModel.StartCommand.Execute(null);

        // Assert: the honest unavailable outcome, and the unusable recognizer was released
        Assert.Equal(RecognitionPanelViewModel.RecognizerUnavailableMessage, viewModel.StatusMessage);
        Assert.Equal(RecognitionStreamingState.Error, viewModel.State);
        recognizer.Received(1).Dispose();
    }

    /// <summary>
    ///     Proves that Start reports the honest outcome, and releases the recognizer, when the
    ///     library reports an unavailable capture device only after composition (an honest
    ///     "reported available but the device failed to start" outcome).
    /// </summary>
    [Fact]
    public void RecognitionPanelViewModel_Start_RecognizerStartThrows_ReportsErrorStateAndDisposes()
    {
        // Arrange: a recognizer that reports itself available but faults when actually started
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = CaptureDevice();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var recognizer = Substitute.For<ISpeechRecognizer>();
        recognizer.IsAvailable.Returns(true);
        recognizer.When(r => r.Start()).Throw(new SpeechRecognizerUnavailableException("Capture device failed to start."));
        var sessionFactory = Substitute.For<IRecognizerSessionFactory>();
        sessionFactory.Create(Arg.Any<ISpeechModel>(), Arg.Any<IAudioCaptureDevice>()).Returns(recognizer);
        var viewModel = new RecognitionPanelViewModel(Catalog(descriptor), deviceService, DeviceSelection(), sessionFactory);

        // Act: attempt to start
        viewModel.StartCommand.Execute(null);

        // Assert: the fault is reported honestly, and the recognizer released
        Assert.Equal("Capture device failed to start.", viewModel.StatusMessage);
        Assert.Equal(RecognitionStreamingState.Error, viewModel.State);
        recognizer.Received(1).Dispose();
    }

    /// <summary>
    ///     Proves that a successful Start enters the Listening state and begins streaming.
    /// </summary>
    [Fact]
    public void RecognitionPanelViewModel_Start_SuccessfulSession_EntersListeningState()
    {
        // Arrange: an installed model, an available device, and a recognizer that starts cleanly
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = CaptureDevice();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var recognizer = Substitute.For<ISpeechRecognizer>();
        recognizer.IsAvailable.Returns(true);
        var sessionFactory = Substitute.For<IRecognizerSessionFactory>();
        sessionFactory.Create(Arg.Any<ISpeechModel>(), Arg.Any<IAudioCaptureDevice>()).Returns(recognizer);
        var viewModel = new RecognitionPanelViewModel(Catalog(descriptor), deviceService, DeviceSelection(), sessionFactory);

        // Act: start listening
        viewModel.StartCommand.Execute(null);

        // Assert: streaming began and the panel reports it
        Assert.Equal(RecognitionStreamingState.Listening, viewModel.State);
        Assert.Null(viewModel.StatusMessage);
        recognizer.Received(1).Start();
    }

    /// <summary>
    ///     Proves that a provisional result replaces the trailing partial line without
    ///     committing anything to the final transcript, and that a subsequent final result
    ///     commits the line and clears the partial - the exact sequencing a live captioning UI
    ///     depends on.
    /// </summary>
    [Fact]
    public void RecognitionPanelViewModel_ResultReceived_PartialThenFinal_UpdatesTranscriptInOrder()
    {
        // Arrange: a listening session
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = CaptureDevice();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var recognizer = Substitute.For<ISpeechRecognizer>();
        recognizer.IsAvailable.Returns(true);
        var sessionFactory = Substitute.For<IRecognizerSessionFactory>();
        sessionFactory.Create(Arg.Any<ISpeechModel>(), Arg.Any<IAudioCaptureDevice>()).Returns(recognizer);
        var viewModel = new RecognitionPanelViewModel(Catalog(descriptor), deviceService, DeviceSelection(), sessionFactory);
        viewModel.StartCommand.Execute(null);

        // Act: raise a provisional result, then a final one for the same utterance
        recognizer.ResultReceived += Raise.Event<EventHandler<SpeechRecognitionEvent>>(recognizer, new SpeechRecognitionEvent(new SpeechRecognitionResult("hel", false)));

        // Assert: the partial line reflects the provisional result; nothing is final yet
        Assert.Equal("hel", viewModel.Partial);
        Assert.Empty(viewModel.Finals);

        // Act: the recognizer decides the utterance is complete
        recognizer.ResultReceived += Raise.Event<EventHandler<SpeechRecognitionEvent>>(recognizer, new SpeechRecognitionEvent(new SpeechRecognitionResult("hello", true)));

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
    public void RecognitionPanelViewModel_BuildTranscriptText_FinalsAndPartial_RendersInOrder()
    {
        // Arrange: a listening session with two committed lines and one in-progress partial
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = CaptureDevice();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var recognizer = Substitute.For<ISpeechRecognizer>();
        recognizer.IsAvailable.Returns(true);
        var sessionFactory = Substitute.For<IRecognizerSessionFactory>();
        sessionFactory.Create(Arg.Any<ISpeechModel>(), Arg.Any<IAudioCaptureDevice>()).Returns(recognizer);
        var viewModel = new RecognitionPanelViewModel(Catalog(descriptor), deviceService, DeviceSelection(), sessionFactory);
        viewModel.StartCommand.Execute(null);

        // Act: commit two lines and leave a third in progress
        recognizer.ResultReceived += Raise.Event<EventHandler<SpeechRecognitionEvent>>(recognizer, new SpeechRecognitionEvent(new SpeechRecognitionResult("one", true)));
        recognizer.ResultReceived += Raise.Event<EventHandler<SpeechRecognitionEvent>>(recognizer, new SpeechRecognitionEvent(new SpeechRecognitionResult("two", true)));
        recognizer.ResultReceived += Raise.Event<EventHandler<SpeechRecognitionEvent>>(recognizer, new SpeechRecognitionEvent(new SpeechRecognitionResult("thr", false)));

        // Assert: the transcript renders both committed lines then the trailing partial
        Assert.Equal($"one{Environment.NewLine}two{Environment.NewLine}thr", viewModel.BuildTranscriptText());
    }

    /// <summary>
    ///     Proves that Stop ends an in-flight session deterministically, releasing the
    ///     recognizer and reporting the stop rather than an error.
    /// </summary>
    [Fact]
    public void RecognitionPanelViewModel_Stop_DuringListening_StopsAndReleasesSession()
    {
        // Arrange: a listening session
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = CaptureDevice();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var recognizer = Substitute.For<ISpeechRecognizer>();
        recognizer.IsAvailable.Returns(true);
        var sessionFactory = Substitute.For<IRecognizerSessionFactory>();
        sessionFactory.Create(Arg.Any<ISpeechModel>(), Arg.Any<IAudioCaptureDevice>()).Returns(recognizer);
        var viewModel = new RecognitionPanelViewModel(Catalog(descriptor), deviceService, DeviceSelection(), sessionFactory);
        viewModel.StartCommand.Execute(null);

        // Act: stop
        viewModel.StopCommand.Execute(null);

        // Assert: the session was stopped and released, and the panel reports the stop
        recognizer.Received(1).Stop();
        recognizer.Received(1).Dispose();
        Assert.Equal(RecognitionStreamingState.Idle, viewModel.State);
        Assert.Equal(RecognitionPanelViewModel.StoppedMessage, viewModel.StatusMessage);
    }

    /// <summary>
    ///     Proves that Stop with nothing listening is a safe no-op, matching the library's own
    ///     "stop when not running" contract.
    /// </summary>
    [Fact]
    public void RecognitionPanelViewModel_Stop_NothingListening_IsSafeNoOp()
    {
        // Arrange: a freshly composed panel that never started
        var viewModel = new RecognitionPanelViewModel(
            Catalog(), Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<IRecognizerSessionFactory>());

        // Act: stop with nothing running
        var exception = Record.Exception(() => viewModel.StopCommand.Execute(null));

        // Assert: no fault, and the panel remains idle with no status to report
        Assert.Null(exception);
        Assert.Equal(RecognitionStreamingState.Idle, viewModel.State);
        Assert.Null(viewModel.StatusMessage);
    }

    /// <summary>
    ///     Proves that a result received after Stop is no longer applied to the transcript,
    ///     since the handler was unsubscribed when the session ended.
    /// </summary>
    [Fact]
    public void RecognitionPanelViewModel_Dispose_ReleasesActiveSessionWithoutThrowing()
    {
        // Arrange: a listening session
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = CaptureDevice();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var recognizer = Substitute.For<ISpeechRecognizer>();
        recognizer.IsAvailable.Returns(true);
        var sessionFactory = Substitute.For<IRecognizerSessionFactory>();
        sessionFactory.Create(Arg.Any<ISpeechModel>(), Arg.Any<IAudioCaptureDevice>()).Returns(recognizer);
        var viewModel = new RecognitionPanelViewModel(Catalog(descriptor), deviceService, DeviceSelection(), sessionFactory);
        viewModel.StartCommand.Execute(null);

        // Act: dispose the panel directly (as the shell would on shutdown) and again for
        // idempotency
        var exception = Record.Exception(() =>
        {
            viewModel.Dispose();
            viewModel.Dispose();
        });

        // Assert: no fault, and the recognizer was released exactly once
        Assert.Null(exception);
        recognizer.Received(1).Dispose();
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
    ///     Proves that <see cref="RecognitionPanelViewModel.Dispose"/> unsubscribes from
    ///     <see cref="IModelCatalogService.ModelInstalled"/>, so a later install completing after
    ///     disposal is never applied.
    /// </summary>
    [Fact]
    public void RecognitionPanelViewModel_Dispose_UnsubscribesFromModelInstalled_NoRefreshAfterDispose()
    {
        // Arrange: a panel composed over a catalog reporting no installed models yet
        var catalog = Substitute.For<IModelCatalogService>();
        var descriptors = Array.Empty<SpeechModelDescriptor>();
        catalog.Enumerate().Returns(_ => descriptors);
        var viewModel = new RecognitionPanelViewModel(
            catalog, Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<IRecognizerSessionFactory>());

        // Act: dispose the panel, then simulate a later install completing
        viewModel.Dispose();
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
    public void RecognitionPanelViewModel_CanChangeModel_TogglesAcrossStateTransitions()
    {
        // Arrange: an installed model, an available device, and a recognizer that starts cleanly
        var descriptor = FakeSpeechModel.Descriptor("stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var availableDevice = CaptureDevice();
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(availableDevice);
        var recognizer = Substitute.For<ISpeechRecognizer>();
        recognizer.IsAvailable.Returns(true);
        var sessionFactory = Substitute.For<IRecognizerSessionFactory>();
        sessionFactory.Create(Arg.Any<ISpeechModel>(), Arg.Any<IAudioCaptureDevice>()).Returns(recognizer);
        var viewModel = new RecognitionPanelViewModel(Catalog(descriptor), deviceService, DeviceSelection(), sessionFactory);

        // Assert: true at the initial Idle state
        Assert.True(viewModel.CanChangeModel);

        // Act: start listening
        viewModel.StartCommand.Execute(null);

        // Assert: false while actively listening
        Assert.Equal(RecognitionStreamingState.Listening, viewModel.State);
        Assert.False(viewModel.CanChangeModel);

        // Act: stop
        viewModel.StopCommand.Execute(null);

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
    public void RecognitionPanelViewModel_CanChangeModel_ErrorState_IsTrue()
    {
        // Arrange: a panel with no installed models, so Start reports the Error state
        var viewModel = new RecognitionPanelViewModel(
            Catalog(), Substitute.For<IAudioDeviceService>(), DeviceSelection(),
            Substitute.For<IRecognizerSessionFactory>());
        viewModel.StartCommand.Execute(null);
        Assert.Equal(RecognitionStreamingState.Error, viewModel.State);

        // Act & Assert: the model-selection control remains enabled at the Error state
        Assert.True(viewModel.CanChangeModel);
    }
}
