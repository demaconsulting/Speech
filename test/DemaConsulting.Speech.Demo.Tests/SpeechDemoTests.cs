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
///     Tests that prove composition (<c>RealLibraryEntryPoints_ComposesWorkingShell</c>,
///     <c>RealModelCatalog_ReportsKnownModels</c>) call the application's own composition root,
///     <c>App.Compose</c>, over the real library factories and catalog, so they prove it works on
///     a machine with no audio backend and no installed model. Tests of panel behavior instead
///     mirror that wiring by hand over controlled fakes, so a download, a synthesis or a
///     recognition session can be driven deterministically without network, models or hardware.
///     Only the Avalonia window itself is left out, because starting a UI toolkit is not
///     something CI can rely on and adds no evidence the ViewModels do not already provide.
///     The class joins the non-parallel process-wide-state collection because the recognition
///     scenarios start the capture-debug recorder, which reads a process-wide environment variable.
/// </remarks>
[Collection(ProcessWideStateCollection.Name)]
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
        // Arrange: the application's own composition root, isolated to a private model store
        var launchOptions = AppLaunchOptions.Parse(["--models-dir", IsolatedOptions().RootPathOverride!], _ => null);

        // Act: compose the whole demo object graph exactly as start-up does
        var viewModel = App.Compose(launchOptions, out var catalogService);
        using var disposable = catalogService;

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
    ///     The demo's composition root (<c>App.Compose</c>) registers the Sherpa, Kokoro,
    ///     Nemotron STT and Chatterbox Turbo model families, so the panel must surface the real
    ///     models rather than the empty-catalog message. The test composes through
    ///     <c>App.Compose</c> itself and asserts the literal identifiers of the documented model families
    ///     rather than comparing the panel with the catalog it was built from. Core
    ///     <c>DemaConsulting.Speech</c> itself ships no built-in models.
    /// </remarks>
    [Fact]
    public void SpeechDemo_SystemIntegration_RealModelCatalog_ReportsKnownModels()
    {
        // Arrange: the application's own composition root over an isolated store root
        var launchOptions = AppLaunchOptions.Parse(["--models-dir", IsolatedOptions().RootPathOverride!], _ => null);

        // Act: compose, which builds the model catalog panel over the real catalog
        var shell = App.Compose(launchOptions, out var catalogService);
        using var disposable = catalogService;
        var registered = catalogService.Enumerate();

        // Assert: the panel lists a model of each documented family by literal identifier,
        // covering both capabilities, not an empty state
        var listed = shell.ModelCatalog.Models.Select(m => m.Id).ToList();
        foreach (var expected in AppTests.ExpectedModelIds)
        {
            Assert.Contains(expected, listed);
        }

        Assert.Contains(listed, id => id.StartsWith(AppTests.ExpectedChatterboxIdPrefix, StringComparison.Ordinal));
        Assert.False(shell.ModelCatalog.IsCatalogEmpty);
        Assert.Contains(registered, m => m.Role == SpeechModelRole.Synthesis);
        Assert.Contains(registered, m => m.Role == SpeechModelRole.Recognition);
    }    /// <summary>
    ///     Proves that the demo's synthesis and recognition panels, composed over the real
    ///     library catalog, honestly report that no models are installed yet - the expected
    ///     state of a fresh install - rather than a silently empty or crashing panel.
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

        // Assert: the settings panel explains that no model is selected
        Assert.True(synthesis.Settings.IsEmpty);
        Assert.Equal(ModelSettingsViewModel.NoModelMessage, synthesis.Settings.EmptyMessage);
    }

    /// <summary>
    ///     Proves that a caller-supplied catalog with no models is explained by the catalog panel
    ///     rather than shown as a silent blank list.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_EmptyCatalog_ModelPanelExplainsEmptiness()
    {
        // Act: compose over a catalog with no models
        var shell = ComposeShell(FakeCatalog());

        // Assert: the panel is empty and says why
        Assert.True(shell.ModelCatalog.IsCatalogEmpty);
        Assert.False(string.IsNullOrWhiteSpace(ModelCatalogViewModel.EmptyCatalogMessage));
    }

    /// <summary>
    ///     Proves that the settings panel explains a selected model that declares no tunable
    ///     parameters.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_ModelWithoutParameters_SettingsPanelExplainsNoParameters()
    {
        // Arrange / Act: compose over an installed synthesis model with no parameters
        var shell = ComposeShell(FakeCatalog(FakeSpeechModel.Descriptor(
            "tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis)));

        // Assert: the settings panel has a model but explains it has nothing to tune
        Assert.NotNull(shell.Synthesis.Settings.Model);
        Assert.True(shell.Synthesis.Settings.IsEmpty);
        Assert.Equal(ModelSettingsViewModel.NoParametersMessage, shell.Synthesis.Settings.EmptyMessage);
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
    /// <param name="playbackAvailable">Whether the faked playback device reports itself available.</param>
    /// <param name="captureAvailable">Whether the faked capture device reports itself available.</param>
    /// <param name="captureDevice">A test-controlled capture device, or <see langword="null"/> for a plain fake.</param>
    /// <returns>The composed shell.</returns>
    private static MainWindowViewModel ComposeShell(
        IModelCatalogService catalogService,
        ISynthesizerSessionFactory? synthesizerFactory = null,
        IRecognizerSessionFactory? recognizerFactory = null,
        string? mirror = null,
        bool playbackAvailable = true,
        bool captureAvailable = true,
        IAudioCaptureDevice? captureDevice = null)
    {
        var deviceService = Substitute.For<IAudioDeviceService>();
        var speaker = new AudioDeviceDescription("Speaker", AudioDeviceDirection.Playback, 2, 44100);
        var microphone = new AudioDeviceDescription("Microphone", AudioDeviceDirection.Capture, 1, 16000);
        deviceService.EnumeratePlaybackDevices().Returns([speaker]);
        deviceService.EnumerateCaptureDevices().Returns([microphone]);
        var playbackDevice = Substitute.For<IAudioPlaybackDevice>();
        playbackDevice.IsAvailable.Returns(playbackAvailable);
        deviceService.CreatePlaybackDevice(Arg.Any<AudioDeviceSelection?>()).Returns(playbackDevice);
        var capture = captureDevice ?? Substitute.For<IAudioCaptureDevice>();
        if (captureDevice is null)
        {
            capture.IsAvailable.Returns(captureAvailable);
        }

        deviceService.CreateCaptureDevice(Arg.Any<AudioDeviceSelection?>()).Returns(capture);

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
    ///     Builds a composed demo shell over a catalog whose one listed model downloads with the
    ///     supplied results.
    /// </summary>
    /// <param name="results">The results the catalog service returns for successive downloads.</param>
    /// <returns>The composed shell.</returns>
    private static MainWindowViewModel ComposeDownloadShell(params SpeechModelDownloadResult[] results)
    {
        var service = FakeCatalog(FakeSpeechModel.Descriptor("model-a"));
        service
            .DownloadAsync("model-a", Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(results[0], results.Skip(1).ToArray());
        return ComposeShell(service);
    }

    /// <summary>
    ///     Proves that a user can download a listed model from the composed demo and see it end
    ///     installed.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_DownloadListedModel_ShowsInstalledOutcome()
    {
        // Arrange: a composed demo whose catalog installs the model
        var shell = ComposeDownloadShell(new SpeechModelDownloadResult(SpeechModelDownloadOutcome.Installed));
        var row = shell.ModelCatalog.Models[0];

        // Act: download the listed model
        await shell.ModelCatalog.DownloadAsync(row, TestContext.Current.CancellationToken);

        // Assert: the row ends installed
        Assert.Equal(SpeechModelState.Downloaded, row.State);
        Assert.False(row.CanDownload);
        Assert.False(row.HasFailureMessage);
    }

    /// <summary>
    ///     Proves that the composed demo shows a running download's progress on the model's row.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_DownloadListedModel_ReportsProgress()
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

        // Assert: progress was reported while downloading
        Assert.Contains(0.5, observed);
    }

    /// <summary>
    ///     Proves that a failed download is reported honestly in the composed demo, rather than
    ///     being hidden or crashing the application.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_DownloadListedModel_FailureIsReported()
    {
        // Arrange: a composed demo whose download fails its checksum
        var shell = ComposeDownloadShell(new SpeechModelDownloadResult(SpeechModelDownloadOutcome.ChecksumMismatch));
        var row = shell.ModelCatalog.Models[0];

        // Act: the attempt fails
        await shell.ModelCatalog.DownloadAsync(row, TestContext.Current.CancellationToken);

        // Assert: the failure is explained on the row
        Assert.Equal(SpeechModelState.FailedOrCorrupt, row.State);
        Assert.True(row.HasFailureMessage);
    }

    /// <summary>
    ///     Proves that a failed download can be retried in the composed demo and that the earlier
    ///     failure explanation is cleared once the retry succeeds.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_DownloadListedModel_FailedDownloadIsRetryable()
    {
        // Arrange: a composed demo whose first download fails and second succeeds
        var shell = ComposeDownloadShell(
            new SpeechModelDownloadResult(SpeechModelDownloadOutcome.ChecksumMismatch),
            new SpeechModelDownloadResult(SpeechModelDownloadOutcome.Installed));
        var row = shell.ModelCatalog.Models[0];
        await shell.ModelCatalog.DownloadAsync(row, TestContext.Current.CancellationToken);
        Assert.True(row.CanDownload);

        // Act: the retry succeeds
        await shell.ModelCatalog.DownloadAsync(row, TestContext.Current.CancellationToken);

        // Assert: the row is installed and the earlier failure message is cleared
        Assert.Equal(SpeechModelState.Downloaded, row.State);
        Assert.False(row.HasFailureMessage);
    }

    /// <summary>
    ///     Proves that a mirror named in launch arguments reaches the library options and
    ///     pre-populates the catalog panel's mirror field.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_MirrorRedirection_AppliesAtLaunch()
    {
        // Arrange: launch arguments naming a mirror, turned into the downloader options the
        // application's composition root hands the library
        var launch = AppLaunchOptions.Parse(["--mirror-url", "https://mirror.example.com"], _ => null);

        // Act
        var downloaderOptions = launch.CreateDownloaderOptions();
        var shell = ComposeShell(FakeCatalog(), mirror: launch.MirrorUrl);

        // Assert: the launch-time mirror reaches the library options and pre-populates the panel
        Assert.Equal(new Uri("https://mirror.example.com"), downloaderOptions?.Mirror?.BaseUri);
        Assert.Equal("https://mirror.example.com", shell.ModelCatalog.MirrorUrl);
    }

    /// <summary>
    ///     Proves that a user can redirect downloads to a mirror from the running demo's catalog
    ///     panel without a restart.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_MirrorRedirection_AppliesWhileRunning()
    {
        // Arrange: a running demo
        var service = FakeCatalog();
        var shell = ComposeShell(service);

        // Act: the user points the panel at a mirror and applies it
        shell.ModelCatalog.MirrorUrl = "https://other.example.com";
        shell.ModelCatalog.ApplyMirrorCommand.Execute(null);

        // Assert: the service was redirected to the new mirror and the panel confirms it
        service.Received(1).ApplyMirror(Arg.Is<DownloadMirror?>(
            m => m != null && m.BaseUri == new Uri("https://other.example.com")));
        Assert.True(shell.ModelCatalog.MirrorAppliedSuccessfully);
    }

    /// <summary>
    ///     Proves that clearing the mirror in the running demo reverts downloads to each model's
    ///     own public location.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_MirrorRedirection_ClearingMirrorRevertsToPublicUris()
    {
        // Arrange: a running demo whose user has typed a mirror and then cleared it
        var service = FakeCatalog();
        var shell = ComposeShell(service, mirror: "https://mirror.example.com");
        shell.ModelCatalog.MirrorUrl = null;

        // Act
        shell.ModelCatalog.ApplyMirrorCommand.Execute(null);

        // Assert: downloads are reverted to no mirror
        service.Received(1).ApplyMirror(null);
        Assert.True(shell.ModelCatalog.MirrorAppliedSuccessfully);
    }

    /// <summary>
    ///     Proves that the launch-time models-dir option relocates the model store.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_LaunchOptions_RelocateModelStore()
    {
        // Act
        var options = AppLaunchOptions.Parse(["--models-dir", "models-root"], _ => null);

        // Assert: the store is relocated
        Assert.Equal("models-root", options.CreateStoreOptions()?.RootPathOverride);
    }

    /// <summary>
    ///     Proves that the launch-time voices-dir option records the user's own reference-voice
    ///     folder.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_LaunchOptions_VoicesDirOptionRecordsVoicesFolder()
    {
        // Arrange: a real, existing voices folder under the test output directory
        var voices = Directory.CreateDirectory(Path.Join(
            AppContext.BaseDirectory, "SpeechDemoTests", Guid.NewGuid().ToString("N")));

        // Act
        var options = AppLaunchOptions.Parse(["--voices-dir", voices.FullName], _ => null);

        // Assert: the voices folder is recorded
        Assert.Equal(voices.FullName, options.VoicesDir);
    }

    /// <summary>
    ///     Proves that an unrecognized or incomplete launch option is rejected by the demo's entry
    ///     point with an explanation, a non-zero exit code and the usage text, and never starts
    ///     the application.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_LaunchOptions_RejectBadOptionsWithExplanation()
    {
        // Act: an unrecognized option, which may be a mistyped secret
        var unknown = RunEntryPoint(["--not-an-option"]);

        // Assert: it is rejected at its position without being echoed, the usage is shown, and
        // the application was not started
        Assert.Equal(1, unknown.ExitCode);
        Assert.Contains("Error:", unknown.Error, StringComparison.Ordinal);
        Assert.Contains("position 1", unknown.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("--not-an-option", unknown.Error, StringComparison.Ordinal);
        Assert.Contains("Usage:", unknown.Error, StringComparison.Ordinal);
        Assert.Equal(0, unknown.StartCount);

        // Act: an option missing its value
        var incomplete = RunEntryPoint(["--models-dir"]);

        // Assert: it is rejected with an explanation naming the option
        Assert.Equal(1, incomplete.ExitCode);
        Assert.Contains("--models-dir requires a value", incomplete.Error, StringComparison.Ordinal);
        Assert.Equal(0, incomplete.StartCount);
    }

    /// <summary>
    ///     Proves that a help request prints the usage text, succeeds, and never starts the
    ///     application, even alongside an otherwise invalid mirror configuration.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_LaunchOptions_HelpPrintsUsageAndExits()
    {
        // Act
        var help = RunEntryPoint(["--help", "--mirror-url", "not a url"]);

        // Assert: usage on standard output, success, no error text, application not started
        Assert.Equal(0, help.ExitCode);
        Assert.Contains("Usage:", help.Output, StringComparison.Ordinal);
        Assert.Contains("--models-dir <path>", help.Output, StringComparison.Ordinal);
        Assert.Empty(help.Error);
        Assert.Equal(0, help.StartCount);
    }

    /// <summary>
    ///     Proves that mirror settings supplied through the <c>DEMASPEECH_MIRROR_*</c> environment
    ///     variables, rather than command-line options, redirect downloads at launch and
    ///     pre-populate the catalog panel.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_LaunchOptions_MirrorEnvironmentAppliesAtLaunch()
    {
        // Arrange: no command-line mirror, but the environment names one with a bearer token
        var launch = AppLaunchOptions.Parse(
            ["--models-dir", IsolatedOptions().RootPathOverride!],
            name => name switch
            {
                AppLaunchOptions.MirrorUrlEnvironmentVariable => "https://env-mirror.example.com",
                AppLaunchOptions.MirrorBearerTokenEnvironmentVariable => "env-token",
                _ => null,
            });

        // Act: compose through the application's own composition root
        var shell = App.Compose(launch, out var catalogService);
        using var disposable = catalogService;

        // Assert: the environment mirror reaches the library options and the panel
        Assert.Equal(new Uri("https://env-mirror.example.com"), launch.CreateDownloaderOptions()?.Mirror?.BaseUri);
        Assert.Equal("https://env-mirror.example.com", shell.ModelCatalog.MirrorUrl);
    }

    /// <summary>
    ///     The observable outcome of running the demo's entry-point logic with captured writers.
    /// </summary>
    /// <param name="ExitCode">The process exit code the entry point returned.</param>
    /// <param name="Output">The text written to standard output.</param>
    /// <param name="Error">The text written to standard error.</param>
    /// <param name="StartCount">How many times the application would have been started.</param>
    private sealed record EntryPointOutcome(int ExitCode, string Output, string Error, int StartCount);

    /// <summary>
    ///     Runs the demo's entry-point logic over <paramref name="args"/> with an empty environment
    ///     and captured writers, without starting Avalonia.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The captured outcome.</returns>
    private static EntryPointOutcome RunEntryPoint(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var starts = 0;
        var exitCode = Program.Run(args, output, error, _ => null, _ => starts++);
        return new EntryPointOutcome(exitCode, output.ToString(), error.ToString(), starts);
    }

    /// <summary>
    ///     Proves that, with <c>DEMASPEECH_CAPTURE_DEBUG_DIR</c> naming a directory, a listening
    ///     session records the captured audio to a finalized <c>.wav</c> file there, and that
    ///     without the variable nothing is recorded.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_SpeechToText_CaptureDebugRecordsOnlyWhenEnabled()
    {
        var variable = CaptureDebugRecorder.EnvironmentVariableName;
        var prior = Environment.GetEnvironmentVariable(variable);
        var directory = Path.Join(AppContext.BaseDirectory, "SpeechDemoTests", Guid.NewGuid().ToString("N"));
        try
        {
            // Arrange: an enabled recording directory and a listening shell with a 16 kHz microphone
            Environment.SetEnvironmentVariable(variable, directory);
            var capture = Substitute.For<IAudioCaptureDevice>();
            capture.IsAvailable.Returns(true);
            capture.SampleRate.Returns(16000);
            capture.ChannelCount.Returns(1);
            var shell = ComposeListeningShell(out _, captureDevice: capture);

            // Act: listen, deliver one captured frame, and stop
            await shell.Recognition.StartCommand.ExecuteAsync(null);
            capture.FrameCaptured += Raise.Event<EventHandler<AudioCaptureFrameEventArgs>>(
                capture, new AudioCaptureFrameEventArgs([0.1f, -0.1f]));
            await shell.Recognition.StopCommand.ExecuteAsync(null);

            // Assert: one finalized WAV file holding the frame (44-byte header + 2 16-bit samples)
            var file = Assert.Single(Directory.GetFiles(directory, "*.wav"));
            Assert.Equal(44 + (2 * 2), new FileInfo(file).Length);

            // Arrange / Act: the same session with the variable cleared
            Environment.SetEnvironmentVariable(variable, null);
            var disabledShell = ComposeListeningShell(out _, captureDevice: capture);
            await disabledShell.Recognition.StartCommand.ExecuteAsync(null);
            await disabledShell.Recognition.StopCommand.ExecuteAsync(null);

            // Assert: nothing further was recorded
            Assert.Single(Directory.GetFiles(directory, "*.wav"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, prior);
        }
    }

    /// <summary>
    ///     Builds a faked catalog service with a synthesis model whose voice list grows when the
    ///     reference-voice pack is installed, and a shell composed over it.
    /// </summary>
    /// <param name="service">The faked catalog service.</param>
    /// <param name="setDescriptors">Replaces the descriptors the service reports.</param>
    /// <param name="newModel">Builds the synthesis model with the given voice count.</param>
    /// <returns>The composed shell.</returns>
    private static MainWindowViewModel ComposeVoicePackShell(
        out IModelCatalogService service,
        out Action<int> setDescriptors,
        out Func<int, ISpeechModel> newModel)
    {
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
        var catalog = Substitute.For<IModelCatalogService>();
        catalog.Enumerate().Returns(_ => descriptors);
        catalog.ReferenceVoicePacks.Returns([ReferenceVoicePack.VoiceZero10]);
        catalog.GetReferenceVoicePackState(ReferenceVoicePack.VoiceZero10.Id).Returns(SpeechModelState.NotDownloaded);
        catalog.InstallReferenceVoicePackAsync(
                ReferenceVoicePack.VoiceZero10.Id,
                Arg.Any<bool>(),
                Arg.Any<IProgress<SpeechModelDownloadProgress>?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                descriptors = [new SpeechModelDescriptor(NewModel(5), SpeechModelState.Downloaded)];
                catalog.GetReferenceVoicePackState(ReferenceVoicePack.VoiceZero10.Id).Returns(SpeechModelState.Downloaded);
                catalog.ReferenceVoicesChanged += Raise.Event<EventHandler>(catalog, EventArgs.Empty);
                return Task.FromResult(new SpeechModelDownloadResult(SpeechModelDownloadOutcome.Installed));
            });
        service = catalog;
        setDescriptors = count => descriptors = [new SpeechModelDescriptor(NewModel(count), SpeechModelState.Downloaded)];
        newModel = NewModel;
        return ComposeShell(catalog);
    }

    /// <summary>
    ///     Proves that a user can install and remove the reference-voice pack from the composed
    ///     catalog panel.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_ReferenceVoicePack_InstallAndRemoveFromCatalogPanel()
    {
        // Arrange: a composed demo offering the not-yet-installed pack
        var shell = ComposeVoicePackShell(out var service, out var setDescriptors, out _);
        var pack = Assert.Single(shell.ModelCatalog.VoicePacks);
        Assert.True(pack.CanInstall);

        // Act: install the pack from the catalog panel
        await shell.ModelCatalog.InstallVoicePackAsync(pack, TestContext.Current.CancellationToken);

        // Assert: the pack reads as installed and removable
        Assert.True(shell.ModelCatalog.VoicePacks[0].CanRemove);

        // Act: remove the pack
        setDescriptors(1);
        service.GetReferenceVoicePackState(ReferenceVoicePack.VoiceZero10.Id).Returns(SpeechModelState.NotDownloaded);
        await shell.ModelCatalog.RemoveVoicePackAsync(shell.ModelCatalog.VoicePacks[0], TestContext.Current.CancellationToken);

        // Assert: the service was asked to uninstall and the pack can be installed again
        await service.Received(1).UninstallReferenceVoicePackAsync(ReferenceVoicePack.VoiceZero10.Id, Arg.Any<CancellationToken>());
        Assert.True(shell.ModelCatalog.VoicePacks[0].CanInstall);
    }

    /// <summary>
    ///     Proves that the synthesis panel offers the newly installed pack's voices immediately
    ///     while keeping the voice the user already chose.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_ReferenceVoicePack_InstalledVoicesOfferedAndChoiceKept()
    {
        // Arrange: a composed demo whose single voice grows to five when the pack installs
        var shell = ComposeVoicePackShell(out _, out _, out _);
        var pack = Assert.Single(shell.ModelCatalog.VoicePacks);

        // Act: install the pack
        await shell.ModelCatalog.InstallVoicePackAsync(pack, TestContext.Current.CancellationToken);

        // Assert: the synthesis panel offers the new voices
        var voice = (ChoiceParameterViewModel)shell.Synthesis.Settings.Parameters[0];
        Assert.Equal(5, voice.Options.Count);

        // Act: the user chooses a voice, then the voices change again
        voice.SelectedOption = voice.Options[3];
        shell.Synthesis.RefreshCommand.Execute(null);

        // Assert: the chosen voice is kept where it is still offered
        var refreshed = (ChoiceParameterViewModel)shell.Synthesis.Settings.Parameters[0];
        Assert.Equal("v3", refreshed.SelectedOption?.Value);
    }

    /// <summary>
    ///     Proves that the consent note is shown in the catalog panel where reference-voice packs
    ///     are managed.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_ReferenceVoicePack_CatalogPanelShowsConsentNote()
    {
        // Arrange / Act: compose a demo that offers the pack
        var shell = ComposeVoicePackShell(out _, out _, out _);

        // Assert: the consent note accompanies the pack list
        Assert.Equal(ReferenceVoiceLibrary.ConsentNotice, shell.ModelCatalog.VoiceConsentNotice);
    }

    /// <summary>
    ///     Builds a composed demo whose only installed synthesis model is the real Chatterbox
    ///     Turbo model with the reference-voice pack installed.
    /// </summary>
    /// <returns>The composed shell.</returns>
    private static MainWindowViewModel ComposeChatterboxShell()
    {
        var chatterbox = new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { IncludeStoreVoices = false });
        var descriptors = new[] { new SpeechModelDescriptor(chatterbox, SpeechModelState.Downloaded) };
        var service = Substitute.For<IModelCatalogService>();
        service.Enumerate().Returns(_ => descriptors);
        service.ReferenceVoicePacks.Returns([ReferenceVoicePack.VoiceZero10]);
        service.GetReferenceVoicePackState(ReferenceVoicePack.VoiceZero10.Id).Returns(SpeechModelState.Downloaded);
        return ComposeShell(service);
    }

    /// <summary>
    ///     Proves that the consent note is shown in the synthesis panel for the voice-cloning
    ///     model.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_ChatterboxSelected_ShowsConsentNote()
    {
        // Act: compose the demo with Chatterbox Turbo selected
        var shell = ComposeChatterboxShell();

        // Assert: consent accompanies the voice choice
        Assert.True(shell.Synthesis.HasVoiceNotice);
        Assert.Contains(ReferenceVoiceLibrary.ConsentNotice, shell.Synthesis.VoiceNotice, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that the installed pack's license attribution is shown in the synthesis panel
    ///     for the voice-cloning model.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_ChatterboxSelected_ShowsPackAttribution()
    {
        // Act: compose the demo with Chatterbox Turbo selected
        var shell = ComposeChatterboxShell();

        // Assert: the pack's attribution accompanies the voice choice
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
    ///     Builds a composed demo shell with an installed synthesis model whose session the test
    ///     controls, so each speech scenario can drive the real view models end to end.
    /// </summary>
    /// <param name="session">The test-controlled synthesis session the engine hands out.</param>
    /// <param name="playbackAvailable">Whether the playback device reports itself available.</param>
    /// <param name="engineAvailable">Whether the loaded synthesizer engine reports itself available.</param>
    /// <returns>The composed shell.</returns>
    private static MainWindowViewModel ComposeSpeakingShell(
        out FakeSynthesisSession session,
        bool playbackAvailable = true,
        bool engineAvailable = true)
    {
        var service = FakeCatalog(FakeSpeechModel.Descriptor(
            "tts", SpeechModelState.Downloaded, role: SpeechModelRole.Synthesis));
        var fakeSession = new FakeSynthesisSession();
        session = fakeSession;
        var engine = new FakeSpeechSynthesizerEngine { SessionFactory = _ => fakeSession, IsAvailable = engineAvailable };
        var factory = Substitute.For<ISynthesizerSessionFactory>();
        factory.LoadAsync(
                Arg.Any<ISpeechModel>(), Arg.Any<IReadOnlyDictionary<string, object>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ISpeechSynthesizerEngine>(engine));
        return ComposeShell(service, factory, playbackAvailable: playbackAvailable);
    }

    /// <summary>
    ///     Builds a composed demo shell with an installed recognition model whose session the test
    ///     controls, so each listening scenario can drive the real view models end to end.
    /// </summary>
    /// <param name="session">The test-controlled recognition session the engine hands out.</param>
    /// <param name="captureAvailable">Whether the plain fake capture device reports itself available.</param>
    /// <param name="engineAvailable">Whether the loaded recognizer engine reports itself available.</param>
    /// <param name="captureDevice">A test-controlled capture device, or <see langword="null"/> for a plain fake.</param>
    /// <returns>The composed shell.</returns>
    private static MainWindowViewModel ComposeListeningShell(
        out FakeRecognitionSession session,
        bool captureAvailable = true,
        bool engineAvailable = true,
        IAudioCaptureDevice? captureDevice = null)
    {
        var service = FakeCatalog(FakeSpeechModel.Descriptor(
            "stt", SpeechModelState.Downloaded, role: SpeechModelRole.Recognition));
        var fakeSession = new FakeRecognitionSession();
        session = fakeSession;
        var engine = new FakeSpeechRecognizerEngine { SessionFactory = _ => fakeSession, IsAvailable = engineAvailable };
        var factory = Substitute.For<IRecognizerSessionFactory>();
        factory.LoadAsync(Arg.Any<ISpeechModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ISpeechRecognizerEngine>(engine));
        return ComposeShell(service, recognizerFactory: factory, captureAvailable: captureAvailable, captureDevice: captureDevice);
    }

    /// <summary>
    ///     Proves that entered text, including an inline audio tag, reaches the synthesis session
    ///     unchanged when the user speaks it through the composed demo.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_TextToSpeech_SpeaksEnteredText()
    {
        // Arrange: an installed synthesis model and an available playback device
        var shell = ComposeSpeakingShell(out var session);

        // Act: speak text containing an inline audio tag to completion
        shell.Synthesis.Text = "Hello [whispers] world";
        await shell.Synthesis.PlayCommand.ExecuteAsync(null);

        // Assert: the text reached the session unchanged
        Assert.Equal(["Hello [whispers] world"], session.SpeakTexts);
    }

    /// <summary>
    ///     Proves that the composed demo reports synthesizing, then playing, then idle while text
    ///     is spoken.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_TextToSpeech_ReportsLifecycleStatus()
    {
        // Arrange: an installed synthesis model, recording every state the panel reports
        var shell = ComposeSpeakingShell(out _);
        var states = new List<SynthesisPlaybackState>();
        shell.Synthesis.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SynthesisPanelViewModel.State))
            {
                states.Add(shell.Synthesis.State);
            }
        };

        // Act: speak to completion
        shell.Synthesis.Text = "Hello world";
        await shell.Synthesis.PlayCommand.ExecuteAsync(null);

        // Assert: the status walked through the whole lifecycle
        Assert.Equal(
            [SynthesisPlaybackState.Synthesizing, SynthesisPlaybackState.Playing, SynthesisPlaybackState.Idle],
            states);
    }

    /// <summary>
    ///     Proves that a user can stop an utterance in progress and that the composed demo says
    ///     it was stopped.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_TextToSpeech_StopEndsUtteranceWithExplanation()
    {
        // Arrange: an utterance that would never finish on its own
        var shell = ComposeSpeakingShell(out var session);
        session.SpeakImplementation = (_, token) => Task.Delay(Timeout.Infinite, token);
        shell.Synthesis.Text = "Hello";

        // Act: start speaking, then stop
        var playTask = shell.Synthesis.PlayCommand.ExecuteAsync(null);
        await shell.Synthesis.StopCommand.ExecuteAsync(null);
        await playTask;

        // Assert: playback stopped and the panel says so
        Assert.Equal(SynthesisPlaybackState.Idle, shell.Synthesis.State);
        Assert.Equal(SynthesisPanelViewModel.StoppedMessage, shell.Synthesis.StatusMessage);
    }

    /// <summary>
    ///     Proves that the composed demo offers example audio tags beside the text input.
    /// </summary>
    [Fact]
    public void SpeechDemo_SystemIntegration_TextToSpeech_ShowsExampleAudioTags()
    {
        // Arrange / Act: read the hints the synthesis panel shows beside the text input
        var hints = SynthesisPanelViewModel.ExampleTagHints;

        // Assert: example tags are offered
        Assert.NotEmpty(hints);
        Assert.All(hints, hint => Assert.False(string.IsNullOrWhiteSpace(hint)));
    }

    /// <summary>
    ///     Proves that speaking with no playback device available produces an explanation rather
    ///     than a crash.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_TextToSpeech_NoPlaybackDeviceIsExplained()
    {
        // Arrange: an installed model but no usable playback device
        var shell = ComposeSpeakingShell(out var session, playbackAvailable: false);
        shell.Synthesis.Text = "Hello";

        // Act
        await shell.Synthesis.PlayCommand.ExecuteAsync(null);

        // Assert: the panel explains the absence and nothing was spoken
        Assert.Equal(SynthesisPlaybackState.Error, shell.Synthesis.State);
        Assert.Equal(SynthesisPanelViewModel.NoPlaybackDeviceMessage, shell.Synthesis.StatusMessage);
        Assert.Empty(session.SpeakTexts);
    }

    /// <summary>
    ///     Proves that speaking with a synthesizer engine that reports itself unavailable produces
    ///     an explanation rather than a crash.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_TextToSpeech_UnavailableEngineIsExplained()
    {
        // Arrange: an installed model whose engine is unavailable on this machine
        var shell = ComposeSpeakingShell(out var session, engineAvailable: false);
        shell.Synthesis.Text = "Hello";

        // Act
        await shell.Synthesis.PlayCommand.ExecuteAsync(null);

        // Assert: the panel explains the unavailability and nothing was spoken
        Assert.Equal(SynthesisPlaybackState.Error, shell.Synthesis.State);
        Assert.Equal(SynthesisPanelViewModel.SynthesizerUnavailableMessage, shell.Synthesis.StatusMessage);
        Assert.Empty(session.SpeakTexts);
    }

    /// <summary>
    ///     Proves that starting listening in the composed demo begins streaming.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_SpeechToText_StartsStreaming()
    {
        // Arrange: an installed recognition model and an available capture device
        var shell = ComposeListeningShell(out _);

        // Act: start listening
        await shell.Recognition.StartCommand.ExecuteAsync(null);

        // Assert: streaming began
        Assert.Equal(RecognitionStreamingState.Listening, shell.Recognition.State);
    }

    /// <summary>
    ///     Proves that a provisional result is shown as the partial transcript and not yet
    ///     committed as a finished line.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_SpeechToText_ShowsPartialResult()
    {
        // Arrange: a listening session
        var shell = ComposeListeningShell(out var session);
        await shell.Recognition.StartCommand.ExecuteAsync(null);

        // Act: a provisional result arrives
        session.PushResult(new SpeechRecognitionResult("hel", false));

        // Assert: it is shown as the partial and nothing is committed
        Assert.Equal("hel", shell.Recognition.Partial);
        Assert.Empty(shell.Recognition.Finals);
    }

    /// <summary>
    ///     Proves that a final result is committed as a finished line and clears the partial.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_SpeechToText_CommitsFinalResult()
    {
        // Arrange: a listening session that has shown a partial
        var shell = ComposeListeningShell(out var session);
        await shell.Recognition.StartCommand.ExecuteAsync(null);
        session.PushResult(new SpeechRecognitionResult("hel", false));

        // Act: the committed result arrives
        session.PushResult(new SpeechRecognitionResult("hello", true));

        // Assert: the final line is committed and the partial cleared
        Assert.Equal(["hello"], shell.Recognition.Finals);
        Assert.Equal(string.Empty, shell.Recognition.Partial);
    }

    /// <summary>
    ///     Proves that stopping listening ends the session and returns the panel to idle.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_SpeechToText_StopEndsSession()
    {
        // Arrange: a listening session
        var shell = ComposeListeningShell(out var session);
        await shell.Recognition.StartCommand.ExecuteAsync(null);

        // Act: stop listening
        await shell.Recognition.StopCommand.ExecuteAsync(null);

        // Assert: the session was stopped and the panel is idle again
        Assert.Equal(1, session.StopCallCount);
        Assert.Equal(RecognitionStreamingState.Idle, shell.Recognition.State);
    }

    /// <summary>
    ///     Proves that starting listening with no capture device available produces an
    ///     explanation rather than a crash.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_SpeechToText_NoCaptureDeviceIsExplained()
    {
        // Arrange: an installed model but no usable capture device
        var shell = ComposeListeningShell(out _, captureAvailable: false);

        // Act
        await shell.Recognition.StartCommand.ExecuteAsync(null);

        // Assert: the panel explains the absence
        Assert.Equal(RecognitionStreamingState.Error, shell.Recognition.State);
        Assert.Equal(RecognitionPanelViewModel.NoCaptureDeviceMessage, shell.Recognition.StatusMessage);
    }

    /// <summary>
    ///     Proves that starting listening with a recognizer engine that reports itself unavailable
    ///     produces an explanation rather than a crash.
    /// </summary>
    [Fact]
    public async Task SpeechDemo_SystemIntegration_SpeechToText_UnavailableEngineIsExplained()
    {
        // Arrange: an installed model whose engine is unavailable on this machine
        var shell = ComposeListeningShell(out _, engineAvailable: false);

        // Act
        await shell.Recognition.StartCommand.ExecuteAsync(null);

        // Assert: the panel explains the unavailability
        Assert.Equal(RecognitionStreamingState.Error, shell.Recognition.State);
        Assert.Equal(RecognitionPanelViewModel.RecognizerUnavailableMessage, shell.Recognition.StatusMessage);
    }
}
