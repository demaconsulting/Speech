using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Demo.DeviceSelectionSubsystem;
using DemaConsulting.Speech.Demo.ModelCatalogSubsystem;
using DemaConsulting.Speech.Demo.ModelSettingsSubsystem;
using DemaConsulting.Speech.Demo.RecognitionPanelSubsystem;
using DemaConsulting.Speech.Demo.ShellSubsystem;
using DemaConsulting.Speech.Demo.SynthesisPanelSubsystem;
using DemaConsulting.Speech.Demo.Tests.Fakes;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Kokoro;
using DemaConsulting.Speech.Onnx.NemotronStt;
using DemaConsulting.Speech.RecognitionSubsystem;
using DemaConsulting.Speech.Sherpa;
using DemaConsulting.Speech.SynthesisSubsystem;
using NSubstitute;

namespace DemaConsulting.Speech.Demo.Tests;

/// <summary>
///     System-level integration tests for the SpeechDemo system.
/// </summary>
/// <remarks>
///     These tests compose the demo exactly as its application entry point does - the real
///     library factories and catalog, the real service adapters, and the real ViewModels - so
///     they prove the demo's composition root genuinely works on a machine with no audio backend
///     and no installed model. Only the Avalonia window itself is left out, because starting a UI
///     toolkit is not something CI can rely on and adds no evidence the ViewModels do not already
///     provide.
/// </remarks>
public class SpeechDemoTests
{
    /// <summary>
    ///     Builds catalog options rooted inside this test run's own output directory, so no test
    ///     ever reads or writes a developer's real installed-model store.
    /// </summary>
    /// <returns>Options whose store root is a fresh directory under the test output folder.</returns>
    private static SpeechModelStoreOptions IsolatedOptions() => new()
    {
        RootPathOverride = Path.Join(
            AppContext.BaseDirectory, "SpeechDemoTests", Guid.NewGuid().ToString("N"))
    };

    /// <summary>
    ///     Proves that the demo composes end to end over the real library - audio factory, model
    ///     catalog, service adapters, and shell - without throwing, on whatever machine it runs.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_RealLibraryEntryPoints_ComposesWorkingShell()
    {
        // Arrange: the real library entry points, exactly as the application's composition root
        // constructs them
        var options = IsolatedOptions();
        using var catalog = new SpeechModelCatalog(options);
        var store = new SpeechModelStore(options);
        var deviceService = new AudioDeviceService(new AudioDeviceFactory());
        var catalogService = new ModelCatalogService(catalog);
        var deviceSelection = new DeviceSelectionViewModel(deviceService);

        // Act: compose the whole demo object graph
        var viewModel = new MainWindowViewModel(
            deviceSelection,
            new ModelCatalogViewModel(catalogService),
            new SynthesisPanelViewModel(
                catalogService, deviceService, deviceSelection, new SynthesizerSessionFactory(store)),
            new RecognitionPanelViewModel(
                catalogService, deviceService, deviceSelection, new RecognizerSessionFactory(store)));

        // Assert: the shell offers its four panels and opens on the first
        Assert.Equal(4, viewModel.Panels.Count);
        Assert.Same(viewModel.Panels[0], viewModel.SelectedPanel);
        Assert.Same(viewModel.DeviceSelection, viewModel.Panels[0].Content);
        Assert.Same(viewModel.ModelCatalog, viewModel.Panels[1].Content);
        Assert.Same(viewModel.Synthesis, viewModel.Panels[2].Content);
        Assert.Same(viewModel.Recognition, viewModel.Panels[3].Content);
    }

    /// <summary>
    ///     Proves that enumerating audio devices through the demo over the real library never
    ///     throws, regardless of whether the machine running the tests has any audio hardware or
    ///     a working PortAudio runtime.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_RealAudioBackend_DeviceEnumerationNeverThrows()
    {
        // Arrange: the real audio factory, whatever this machine's backend reports
        var deviceService = new AudioDeviceService(new AudioDeviceFactory());

        // Act: build the device panel, which enumerates both directions during construction
        var exception = Record.Exception(() => new DeviceSelectionViewModel(deviceService));

        // Assert: composition succeeded on this machine, whatever its audio state
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that a machine whose audio backend is unavailable still produces a usable demo
    ///     panel that explains the absence, rather than an empty picker a user would read as a bug.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_UnavailableAudioBackend_DevicePanelExplainsAbsence()
    {
        // Arrange: the library's own honest unavailable probes, which is what the audio factory
        // itself falls back to when the PortAudio runtime cannot initialize
        var factory = new AudioDeviceFactory(
            UnavailableAudioCaptureDeviceProbe.Instance,
            UnavailableAudioPlaybackDeviceProbe.Instance);

        // Act: build the device panel over that machine state
        var viewModel = new DeviceSelectionViewModel(new AudioDeviceService(factory));

        // Assert: the panel reports the absence honestly and offers the library's default
        // selection so a caller never has to special-case the no-device machine
        Assert.False(viewModel.HasCaptureDevices);
        Assert.False(viewModel.HasPlaybackDevices);
        Assert.Contains("audio backend may be unavailable", viewModel.CaptureStatus, StringComparison.Ordinal);
        Assert.Same(AudioDeviceSelection.SystemDefault, viewModel.CaptureSelection);
        Assert.Same(AudioDeviceSelection.SystemDefault, viewModel.PlaybackSelection);
    }

    /// <summary>
    ///     Proves that the demo's model catalog panel, composed over the real library catalog,
    ///     lists the recognition and synthesis models the application registers rather than reporting
    ///     them as an empty catalog.
    /// </summary>
    /// <remarks>
    ///     Earlier versions documented an empty <c>SpeechModelCatalog.KnownModels</c> as the
    ///     expected shipped state; core <c>DemaConsulting.Speech</c> still ships zero built-in
    ///     models, but the sibling <c>DemaConsulting.Speech.Sherpa</c> package's
    ///     <c>AddSherpaModels()</c> registers four real models (two recognition, two synthesis)
    ///     and the sibling <c>DemaConsulting.Speech.Onnx.Kokoro</c> package's
    ///     <c>AddKokoroModels()</c> registers one further real synthesis model, and the sibling
    ///     <c>DemaConsulting.Speech.Onnx.NemotronStt</c> package's <c>AddNemotronSttModels()</c> registers
    ///     one further real recognition model, and the sibling <c>DemaConsulting.Speech.Onnx.ChatterboxTurbo</c>
    ///     package's <c>AddChatterboxTurboModels()</c> registers one further voice-cloning synthesis model - the
    ///     same four
    ///     extension methods the demo's own composition root (<c>App.OnFrameworkInitializationCompleted</c>)
    ///     calls in combination - into the catalog the demo composes, so this test now proves the
    ///     panel surfaces all seven real models instead of the empty-catalog message or only the
    ///     Sherpa subset.
    /// </remarks>
    [Fact]
    public void SpeechDemo_SystemIntegration_RealModelCatalog_ReportsKnownModels()
    {
        // Arrange: the real library catalog over an isolated store root, seeded with the same
        // models the application's own composition root registers (AddSherpaModels() followed
        // by AddKokoroModels(), AddNemotronSttModels() and AddChatterboxTurboModels(), exactly as App.OnFrameworkInitializationCompleted composes them)
        using var catalog = new SpeechModelCatalog(IsolatedOptions()).AddSherpaModels().AddKokoroModels().AddNemotronSttModels().AddChatterboxTurboModels();
        using var referenceCatalog =
            new SpeechModelCatalog(IsolatedOptions()).AddSherpaModels().AddKokoroModels().AddNemotronSttModels().AddChatterboxTurboModels();

        // Act: build the model catalog panel over it
        var viewModel = new ModelCatalogViewModel(new ModelCatalogService(catalog));

        // Assert: the panel lists exactly the registered models (seven: three recognition, four
        // synthesis), not an empty state and not only the four Sherpa models
        Assert.Equal(7, referenceCatalog.Enumerate().Count);
        Assert.Equal(referenceCatalog.Enumerate().Count, viewModel.Models.Count);
        Assert.False(viewModel.IsCatalogEmpty);
    }

    /// <summary>
    ///     Proves that the demo's synthesis and recognition panels, composed over the real
    ///     library catalog, honestly report that no models are installed yet - the expected
    ///     expected state of a fresh install - rather than a silently empty or crashing panel.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_RealCatalog_TtsAndSttPanelsReportEmptyStateHonestly()
    {
        // Arrange: the real library entry points over an isolated, empty store root
        var options = IsolatedOptions();
        using var catalog = new SpeechModelCatalog(options);
        var store = new SpeechModelStore(options);
        var deviceService = new AudioDeviceService(new AudioDeviceFactory());
        var catalogService = new ModelCatalogService(catalog);
        var deviceSelection = new DeviceSelectionViewModel(deviceService);

        // Act: build the two new panels over that machine state
        var synthesis = new SynthesisPanelViewModel(
            catalogService, deviceService, deviceSelection, new SynthesizerSessionFactory(store));
        var recognition = new RecognitionPanelViewModel(
            catalogService, deviceService, deviceSelection, new RecognizerSessionFactory(store));

        // Assert: both honestly report having nothing installed to choose from
        Assert.False(synthesis.HasModels);
        Assert.Null(synthesis.SelectedModel);
        Assert.False(recognition.HasModels);
        Assert.Null(recognition.SelectedModel);
    }

    /// <summary>
    ///     Proves that refreshing every panel of a fully composed demo over the real library never
    ///     throws, which is what a user clicking the refresh buttons actually exercises.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_RefreshAllPanels_NeverThrows()
    {
        // Arrange: a fully composed demo over the real library
        var options = IsolatedOptions();
        using var catalog = new SpeechModelCatalog(options);
        var store = new SpeechModelStore(options);
        var deviceService = new AudioDeviceService(new AudioDeviceFactory());
        var catalogService = new ModelCatalogService(catalog);
        var deviceSelection = new DeviceSelectionViewModel(deviceService);
        var viewModel = new MainWindowViewModel(
            deviceSelection,
            new ModelCatalogViewModel(catalogService),
            new SynthesisPanelViewModel(
                catalogService, deviceService, deviceSelection, new SynthesizerSessionFactory(store)),
            new RecognitionPanelViewModel(
                catalogService, deviceService, deviceSelection, new RecognizerSessionFactory(store)));

        // Act: refresh every panel the way their buttons do
        var exception = Record.Exception(() =>
        {
            viewModel.DeviceSelection.RefreshCommand.Execute(null);
            viewModel.ModelCatalog.RefreshCommand.Execute(null);
            viewModel.Synthesis.RefreshCommand.Execute(null);
            viewModel.Recognition.RefreshCommand.Execute(null);
        });

        // Assert: no refresh faulted on this machine
        Assert.Null(exception);
    }

    /// <summary>
    ///     Builds a fully composed demo shell over a faked model-catalog service, a faked audio
    ///     device service offering one available capture and one available playback device, and
    ///     the supplied session factories, so system-level scenarios can drive the real view
    ///     models end to end without a network, audio hardware or installed model.
    /// </summary>
    /// <param name="catalogService">The faked model-catalog service.</param>
    /// <param name="synthesizerFactory">The synthesizer session factory, or <see langword="null"/> for an unused substitute.</param>
    /// <param name="recognizerFactory">The recognizer session factory, or <see langword="null"/> for an unused substitute.</param>
    /// <param name="mirror">The launch-time mirror values the catalog panel starts with.</param>
    /// <returns>The composed shell.</returns>
    private static MainWindowViewModel ComposeShell(
        IModelCatalogService catalogService,
        ISynthesizerSessionFactory? synthesizerFactory = null,
        IRecognizerSessionFactory? recognizerFactory = null,
        string? mirror = null)
    {
        var deviceService = Substitute.For<IAudioDeviceService>();
        var speaker = new AudioDeviceDescription("Speaker", AudioDeviceDirection.Playback, 2, 44100);
        var microphone = new AudioDeviceDescription("Microphone", AudioDeviceDirection.Capture, 1, 16000);
        deviceService.EnumeratePlaybackDevices().Returns([speaker]);
        deviceService.EnumerateCaptureDevices().Returns([microphone]);
        var playbackDevice = Substitute.For<IAudioPlaybackDevice>();
        playbackDevice.IsAvailable.Returns(true);
        deviceService.CreatePlaybackDevice(Arg.Any<AudioDeviceSelection?>()).Returns(playbackDevice);
        var captureDevice = Substitute.For<IAudioCaptureDevice>();
        captureDevice.IsAvailable.Returns(true);
        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(captureDevice);

        var deviceSelection = new DeviceSelectionViewModel(deviceService);
        return new MainWindowViewModel(
            deviceSelection,
            new ModelCatalogViewModel(catalogService, mirror),
            new SynthesisPanelViewModel(
                catalogService,
                deviceService,
                deviceSelection,
                synthesizerFactory ?? Substitute.For<ISynthesizerSessionFactory>()),
            new RecognitionPanelViewModel(
                catalogService,
                deviceService,
                deviceSelection,
                recognizerFactory ?? Substitute.For<IRecognizerSessionFactory>()));
    }

    /// <summary>
    ///     Builds a faked catalog service reporting the supplied descriptors.
    /// </summary>
    /// <param name="descriptors">The descriptors to report.</param>
    /// <returns>The faked service.</returns>
    private static IModelCatalogService FakeCatalog(params SpeechModelDescriptor[] descriptors)
    {
        var service = Substitute.For<IModelCatalogService>();
        service.Enumerate().Returns(descriptors);
        return service;
    }

    /// <summary>
    ///     Proves that a user can download a listed model from the composed demo and see the
    ///     download's progress and its final installed outcome.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_DownloadListedModel_ReportsProgressAndInstalledOutcome()
    {
        // Arrange: a composed demo whose catalog reports partial progress and then installs
        using var scope = InlineSynchronizationContext.Install();
        var service = FakeCatalog(FakeSpeechModel.Descriptor("model-a"));
        service
            .DownloadAsync("model-a", Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callInfo.Arg<IProgress<SpeechModelDownloadProgress>?>()
                    ?.Report(new SpeechModelDownloadProgress(0, 1, 50, 100));
                return Task.FromResult(new SpeechModelDownloadResult(SpeechModelDownloadOutcome.Installed));
            });
        var shell = ComposeShell(service);
        var row = shell.ModelCatalog.Models[0];
        var observed = new List<double?>();
        row.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ModelListItemViewModel.ProgressFraction))
            {
                observed.Add(row.ProgressFraction);
            }
        };

        // Act: download the listed model
        await shell.ModelCatalog.DownloadAsync(row, TestContext.Current.CancellationToken);

        // Assert: progress was reported while downloading, and the row ends installed
        Assert.Contains(0.5, observed);
        Assert.Equal(SpeechModelState.Downloaded, row.State);
        Assert.False(row.CanDownload);
        Assert.False(row.HasFailureMessage);
    }

    /// <summary>
    ///     Proves that a failed download is reported honestly in the composed demo and can be
    ///     retried, rather than being hidden or crashing the application.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_DownloadListedModel_FailureIsReportedAndRetryable()
    {
        // Arrange: a composed demo whose first download fails its checksum and second succeeds
        var service = FakeCatalog(FakeSpeechModel.Descriptor("model-a"));
        service
            .DownloadAsync("model-a", Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(
                new SpeechModelDownloadResult(SpeechModelDownloadOutcome.ChecksumMismatch),
                new SpeechModelDownloadResult(SpeechModelDownloadOutcome.Installed));
        var shell = ComposeShell(service);
        var row = shell.ModelCatalog.Models[0];

        // Act: the first attempt fails
        await shell.ModelCatalog.DownloadAsync(row, TestContext.Current.CancellationToken);

        // Assert: the failure is explained and a retry is offered
        Assert.Equal(SpeechModelState.FailedOrCorrupt, row.State);
        Assert.True(row.HasFailureMessage);
        Assert.True(row.CanDownload);

        // Act: the retry succeeds
        await shell.ModelCatalog.DownloadAsync(row, TestContext.Current.CancellationToken);

        // Assert: the earlier failure message is cleared
        Assert.Equal(SpeechModelState.Downloaded, row.State);
        Assert.False(row.HasFailureMessage);
    }

    /// <summary>
    ///     Proves that a user can redirect downloads to an internal mirror both at launch and while
    ///     the demo is running, and can revert to each model's own public location.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_MirrorRedirection_AppliesAtLaunchAndWhileRunning()
    {
        // Arrange: launch arguments naming a mirror, turned into the downloader options the
        // application's composition root hands the library
        var launch = AppLaunchOptions.Parse(["--mirror-url", "https://mirror.example.com"]);
        var downloaderOptions = launch.CreateDownloaderOptions();
        var service = FakeCatalog();
        var shell = ComposeShell(service, mirror: launch.MirrorUrl);

        // Assert: the launch-time mirror reaches the library options and pre-populates the panel
        Assert.Equal(new Uri("https://mirror.example.com"), downloaderOptions?.Mirror?.BaseUri);
        Assert.Equal("https://mirror.example.com", shell.ModelCatalog.MirrorUrl);

        // Act: while running, the user points the panel at a different mirror and applies it
        shell.ModelCatalog.MirrorUrl = "https://other.example.com";
        shell.ModelCatalog.ApplyMirrorCommand.Execute(null);

        // Assert: the service was redirected to the new mirror
        service.Received(1).ApplyMirror(Arg.Is<DownloadMirror?>(
            m => m != null && m.BaseUri == new Uri("https://other.example.com")));
        Assert.True(shell.ModelCatalog.MirrorAppliedSuccessfully);

        // Act: the user clears the mirror to return to each model's own public location
        shell.ModelCatalog.MirrorUrl = null;
        shell.ModelCatalog.ApplyMirrorCommand.Execute(null);

        // Assert: downloads are reverted to no mirror
        service.Received(1).ApplyMirror(null);
        Assert.True(shell.ModelCatalog.MirrorAppliedSuccessfully);
    }

    /// <summary>
    ///     Proves that the launch-time options relocate the model store and add a reference-voice
    ///     folder, and that an unrecognized or incomplete option is rejected with an explanation.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_LaunchOptions_ConfigureStoreAndVoicesAndRejectBadOptions()
    {
        // Arrange: a real, existing voices folder under the test output directory
        var voices = Directory.CreateDirectory(Path.Join(
            AppContext.BaseDirectory, "SpeechDemoTests", Guid.NewGuid().ToString("N")));

        // Act
        var options = AppLaunchOptions.Parse(["--models-dir", "models-root", "--voices-dir", voices.FullName]);

        // Assert: the store is relocated and the voices folder is recorded
        Assert.Equal("models-root", options.CreateStoreOptions()?.RootPathOverride);
        Assert.Equal(voices.FullName, options.VoicesDir);

        // Assert: bad options are rejected with a message naming the problem
        var unknown = Assert.Throws<ArgumentException>(() => AppLaunchOptions.Parse(["--not-an-option"]));
        Assert.Contains("--not-an-option", unknown.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => AppLaunchOptions.Parse(["--models-dir"]));
    }

    /// <summary>
    ///     Proves that a user can install and remove the reference-voice pack from the composed
    ///     catalog panel, that the synthesis panel offers the new voices immediately while keeping
    ///     the voice already chosen, and that the consent note is shown where packs are managed.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_ReferenceVoicePack_InstallRemoveAndChooseVoice()
    {
        // Arrange: a synthesis model whose voice list grows when the pack is installed
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
        var descriptors = new[] { new SpeechModelDescriptor(NewModel(1), SpeechModelState.Downloaded) };
        var service = Substitute.For<IModelCatalogService>();
        service.Enumerate().Returns(_ => descriptors);
        service.ReferenceVoicePacks.Returns([ReferenceVoicePack.VoiceZero10]);
        service.GetReferenceVoicePackState(ReferenceVoicePack.VoiceZero10.Id).Returns(SpeechModelState.NotDownloaded);
        service.InstallReferenceVoicePackAsync(
                ReferenceVoicePack.VoiceZero10.Id,
                Arg.Any<bool>(),
                Arg.Any<IProgress<SpeechModelDownloadProgress>?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                descriptors = [new SpeechModelDescriptor(NewModel(5), SpeechModelState.Downloaded)];
                service.GetReferenceVoicePackState(ReferenceVoicePack.VoiceZero10.Id).Returns(SpeechModelState.Downloaded);
                service.ReferenceVoicesChanged += Raise.Event<EventHandler>(service, EventArgs.Empty);
                return Task.FromResult(new SpeechModelDownloadResult(SpeechModelDownloadOutcome.Installed));
            });
        var shell = ComposeShell(service);
        var pack = Assert.Single(shell.ModelCatalog.VoicePacks);
        Assert.True(pack.CanInstall);
        Assert.Equal(ReferenceVoiceLibrary.ConsentNotice, shell.ModelCatalog.VoiceConsentNotice);

        // Act: install the pack from the catalog panel
        await shell.ModelCatalog.InstallVoicePackAsync(pack, TestContext.Current.CancellationToken);

        // Assert: the pack reads as installed and the synthesis panel offers the new voices
        Assert.True(shell.ModelCatalog.VoicePacks[0].CanRemove);
        var voice = (ChoiceParameterViewModel)shell.Synthesis.Settings.Parameters[0];
        Assert.Equal(5, voice.Options.Count);

        // Act: the user chooses a voice, then removes the pack
        voice.SelectedOption = voice.Options[3];
        descriptors = [new SpeechModelDescriptor(NewModel(1), SpeechModelState.Downloaded)];
        service.GetReferenceVoicePackState(ReferenceVoicePack.VoiceZero10.Id).Returns(SpeechModelState.NotDownloaded);
        await shell.ModelCatalog.RemoveVoicePack(shell.ModelCatalog.VoicePacks[0], TestContext.Current.CancellationToken);

        // Assert: the service was asked to uninstall and the pack can be installed again
        await service.Received(1).UninstallReferenceVoicePackAsync(ReferenceVoicePack.VoiceZero10.Id, Arg.Any<CancellationToken>());
        Assert.True(shell.ModelCatalog.VoicePacks[0].CanInstall);
    }

    /// <summary>
    ///     Proves that the consent note and the installed pack's attribution are shown in the
    ///     synthesis panel for the voice-cloning model, and not for other models.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_ChatterboxSelected_ShowsConsentAndAttribution()
    {
        // Arrange: the real Chatterbox Turbo model with the pack installed
        var chatterbox = new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { IncludeStoreVoices = false });
        var descriptors = new[] { new SpeechModelDescriptor(chatterbox, SpeechModelState.Downloaded) };
        var service = Substitute.For<IModelCatalogService>();
        service.Enumerate().Returns(_ => descriptors);
        service.ReferenceVoicePacks.Returns([ReferenceVoicePack.VoiceZero10]);
        service.GetReferenceVoicePackState(ReferenceVoicePack.VoiceZero10.Id).Returns(SpeechModelState.Downloaded);

        // Act: compose the demo
        var shell = ComposeShell(service);

        // Assert: consent and the pack's attribution accompany the voice choice
        Assert.True(shell.Synthesis.HasVoiceNotice);
        Assert.Contains(ReferenceVoiceLibrary.ConsentNotice, shell.Synthesis.VoiceNotice, StringComparison.Ordinal);
        Assert.Contains("OwenTyme", shell.Synthesis.VoiceNotice, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that the demo renders a control for each of a selected model's declared
    ///     parameter kinds and that the user's adjusted values reach the engine when speaking.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_ModelParameters_RenderedAndForwardedToSynthesis()
    {
        // Arrange: an installed synthesis model declaring a numeric, a choice and a boolean parameter
        IReadOnlyList<ISpeechModelParameter> parameters =
        [
            new NumericParameter("speed", "Speed", "Speaking rate.", new NumericParameterBounds(0.5, 2.0, 0.1, 1.0), "x"),
            new ChoiceParameter(
                "voice", "Voice", "Voice.", [new ChoiceParameterOption("a", "A"), new ChoiceParameterOption("b", "B")], "a"),
            new BooleanParameter("denoise", "Denoise", "Removes noise.", true),
        ];
        var service = FakeCatalog(FakeSpeechModel.Descriptor(
            "tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis, parameters: parameters));
        var engine = new FakeSpeechSynthesizerEngine();
        IReadOnlyDictionary<string, object>? loadedWith = null;
        var factory = Substitute.For<ISynthesizerSessionFactory>();
        factory.LoadAsync(
                Arg.Any<ISpeechModel>(), Arg.Any<IReadOnlyDictionary<string, object>?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                loadedWith = callInfo.Arg<IReadOnlyDictionary<string, object>?>();
                return Task.FromResult<ISpeechSynthesizerEngine>(engine);
            });
        var shell = ComposeShell(service, factory);

        // Assert: one control of the matching kind per declared parameter, in declaration order
        Assert.Collection(
            shell.Synthesis.Settings.Parameters,
            p => Assert.IsType<NumericParameterViewModel>(p),
            p => Assert.IsType<ChoiceParameterViewModel>(p),
            p => Assert.IsType<BooleanParameterViewModel>(p));

        // Act: the user adjusts each control and speaks
        ((NumericParameterViewModel)shell.Synthesis.Settings.Parameters[0]).Value = 1.5;
        var voice = (ChoiceParameterViewModel)shell.Synthesis.Settings.Parameters[1];
        voice.SelectedOption = voice.Options[1];
        ((BooleanParameterViewModel)shell.Synthesis.Settings.Parameters[2]).Value = false;
        shell.Synthesis.Text = "Hello";
        await shell.Synthesis.PlayCommand.ExecuteAsync(null);

        // Assert: the adjusted values reached the engine load
        Assert.NotNull(loadedWith);
        Assert.Equal(1.5, loadedWith["speed"]);
        Assert.Equal("b", loadedWith["voice"]);
        Assert.False((bool)loadedWith["denoise"]);
    }

    /// <summary>
    ///     Proves the speak lifecycle of the composed demo: entered text is spoken through the
    ///     playback device, status passes through synthesizing and playing to idle, and a stop
    ///     request ends an in-flight utterance with an explanation.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_TextToSpeech_SpeaksAndStops()
    {
        // Arrange: an installed synthesis model whose session can be made to never end on its own
        var service = FakeCatalog(FakeSpeechModel.Descriptor(
            "tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis));
        var session = new FakeSynthesisSession();
        var engine = new FakeSpeechSynthesizerEngine { SessionFactory = _ => session };
        var factory = Substitute.For<ISynthesizerSessionFactory>();
        factory.LoadAsync(
                Arg.Any<ISpeechModel>(), Arg.Any<IReadOnlyDictionary<string, object>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ISpeechSynthesizerEngine>(engine));
        var shell = ComposeShell(service, factory);
        var states = new List<SynthesisPlaybackState>();
        shell.Synthesis.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SynthesisPanelViewModel.State))
            {
                states.Add(shell.Synthesis.State);
            }
        };

        // Act: speak text containing an inline audio tag to completion
        shell.Synthesis.Text = "Hello [whispers] world";
        await shell.Synthesis.PlayCommand.ExecuteAsync(null);

        // Assert: the text reached the session unchanged and the status walked through the lifecycle
        Assert.Equal(["Hello [whispers] world"], session.SpeakTexts);
        Assert.Equal(
            [SynthesisPlaybackState.Synthesizing, SynthesisPlaybackState.Playing, SynthesisPlaybackState.Idle],
            states);
        Assert.NotEmpty(SynthesisPanelViewModel.ExampleTagHints);

        // Act: start a second utterance that would never finish, then stop it
        session.SpeakImplementation = (_, token) => Task.Delay(Timeout.Infinite, token);
        var playTask = shell.Synthesis.PlayCommand.ExecuteAsync(null);
        await shell.Synthesis.StopCommand.ExecuteAsync(null);
        await playTask;

        // Assert: playback stopped and the panel says so
        Assert.Equal(SynthesisPlaybackState.Idle, shell.Synthesis.State);
        Assert.Equal(SynthesisPanelViewModel.StoppedMessage, shell.Synthesis.StatusMessage);
    }

    /// <summary>
    ///     Proves the listen lifecycle of the composed demo: starting begins streaming, partial
    ///     results are replaced by the committed final line as they arrive, and stopping ends the
    ///     session.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_SpeechToText_StreamsPartialAndFinalThenStops()
    {
        // Arrange: an installed recognition model and a session the test can push results into
        var service = FakeCatalog(FakeSpeechModel.Descriptor(
            "stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition));
        var session = new FakeRecognitionSession();
        var engine = new FakeSpeechRecognizerEngine { SessionFactory = _ => session };
        var factory = Substitute.For<IRecognizerSessionFactory>();
        factory.LoadAsync(Arg.Any<ISpeechModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ISpeechRecognizerEngine>(engine));
        var shell = ComposeShell(service, recognizerFactory: factory);

        // Act: start listening
        await shell.Recognition.StartCommand.ExecuteAsync(null);

        // Assert: streaming began
        Assert.Equal(RecognitionStreamingState.Listening, shell.Recognition.State);

        // Act: a provisional result arrives, then the committed one
        session.PushResult(new SpeechRecognitionResult("hel", false));
        Assert.Equal("hel", shell.Recognition.Partial);
        Assert.Empty(shell.Recognition.Finals);
        session.PushResult(new SpeechRecognitionResult("hello", true));

        // Assert: the final line is committed and the partial cleared
        Assert.Equal(["hello"], shell.Recognition.Finals);
        Assert.Equal(string.Empty, shell.Recognition.Partial);

        // Act: stop listening
        await shell.Recognition.StopCommand.ExecuteAsync(null);

        // Assert: the session was stopped and the panel is idle again
        Assert.Equal(1, session.StopCallCount);
        Assert.Equal(RecognitionStreamingState.Idle, shell.Recognition.State);
    }
}
