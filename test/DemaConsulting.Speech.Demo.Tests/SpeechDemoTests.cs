using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Demo.DeviceSelectionSubsystem;
using DemaConsulting.Speech.Demo.ModelCatalogSubsystem;
using DemaConsulting.Speech.Demo.RecognitionPanelSubsystem;
using DemaConsulting.Speech.Demo.ShellSubsystem;
using DemaConsulting.Speech.Demo.SynthesisPanelSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;

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

        // Assert: the shell offers this phase's four panels and opens on the first
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
    ///     lists the recognition models this phase registers rather than reporting them as an
    ///     empty catalog.
    /// </summary>
    /// <remarks>
    ///     Earlier phases documented an empty <see cref="SpeechModelCatalog.KnownModels"/> as the
    ///     expected shipped state; this phase resolves that limitation for recognition models
    ///     (a synthesis model remains a tracked Phase 7b follow-up), so this test now proves the
    ///     panel surfaces the real models instead of the empty-catalog message.
    /// </remarks>
    [Fact]
    public void SpeechDemo_SystemIntegration_RealModelCatalog_ReportsKnownModels()
    {
        // Arrange: the real library catalog over an isolated store root
        using var catalog = new SpeechModelCatalog(IsolatedOptions());

        // Act: build the model catalog panel over it
        var viewModel = new ModelCatalogViewModel(new ModelCatalogService(catalog));

        // Assert: the panel lists exactly the library's known models, not an empty state
        Assert.Equal(SpeechModelCatalog.KnownModels.Count, viewModel.Models.Count);
        Assert.False(viewModel.IsCatalogEmpty);
    }

    /// <summary>
    ///     Proves that the demo's synthesis and recognition panels, composed over the real
    ///     library catalog, honestly report that no models are installed yet - the expected
    ///     shipped state of this phase - rather than a silently empty or crashing panel.
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
}
