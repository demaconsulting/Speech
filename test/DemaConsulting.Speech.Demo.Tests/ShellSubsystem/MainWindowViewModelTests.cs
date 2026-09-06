using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Demo.DeviceSelectionSubsystem;
using DemaConsulting.Speech.Demo.ModelCatalogSubsystem;
using DemaConsulting.Speech.Demo.RecognitionPanelSubsystem;
using DemaConsulting.Speech.Demo.ShellSubsystem;
using DemaConsulting.Speech.Demo.SynthesisPanelSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using NSubstitute;

namespace DemaConsulting.Speech.Demo.Tests.ShellSubsystem;

/// <summary>
///     Unit tests for <see cref="MainWindowViewModel"/>.
/// </summary>
public class MainWindowViewModelTests
{
    /// <summary>
    ///     Builds a device-selection panel over a fake reporting no devices.
    /// </summary>
    /// <returns>The composed panel.</returns>
    private static DeviceSelectionViewModel DevicePanel()
    {
        var service = Substitute.For<IAudioDeviceService>();
        service.EnumerateCaptureDevices().Returns([]);
        service.EnumeratePlaybackDevices().Returns([]);
        return new DeviceSelectionViewModel(service);
    }

    /// <summary>
    ///     Builds a model-catalog panel over a fake reporting no models.
    /// </summary>
    /// <returns>The composed panel.</returns>
    private static ModelCatalogViewModel CatalogPanel()
    {
        var service = Substitute.For<IModelCatalogService>();
        service.Enumerate().Returns([]);
        return new ModelCatalogViewModel(service);
    }

    /// <summary>
    ///     Builds a text-to-speech panel over fakes reporting no installed models.
    /// </summary>
    /// <param name="deviceSelection">The device-selection panel state to reuse.</param>
    /// <returns>The composed panel.</returns>
    private static SynthesisPanelViewModel SynthesisPanel(DeviceSelectionViewModel deviceSelection)
    {
        var catalogService = Substitute.For<IModelCatalogService>();
        catalogService.Enumerate().Returns([]);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var sessionFactory = Substitute.For<ISynthesizerSessionFactory>();
        return new SynthesisPanelViewModel(catalogService, deviceService, deviceSelection, sessionFactory);
    }

    /// <summary>
    ///     Builds a speech-to-text panel over fakes reporting no installed models.
    /// </summary>
    /// <param name="deviceSelection">The device-selection panel state to reuse.</param>
    /// <returns>The composed panel.</returns>
    private static RecognitionPanelViewModel RecognitionPanel(DeviceSelectionViewModel deviceSelection)
    {
        var catalogService = Substitute.For<IModelCatalogService>();
        catalogService.Enumerate().Returns([]);
        var deviceService = Substitute.For<IAudioDeviceService>();
        var sessionFactory = Substitute.For<IRecognizerSessionFactory>();
        return new RecognitionPanelViewModel(catalogService, deviceService, deviceSelection, sessionFactory);
    }

    /// <summary>
    ///     Composes a full shell over empty fakes, for tests that only care about shell behavior.
    /// </summary>
    /// <returns>The composed shell.</returns>
    private static MainWindowViewModel FullShell()
    {
        var devices = DevicePanel();
        return new MainWindowViewModel(devices, CatalogPanel(), SynthesisPanel(devices), RecognitionPanel(devices));
    }

    /// <summary>
    ///     Proves that the shell rejects a missing device panel rather than opening a window with
    ///     a broken tab.
    /// </summary>
    [Fact]
    public void MainWindowViewModel_Constructor_NullDeviceSelection_ThrowsArgumentNullException()
    {
        // Arrange: a valid device panel, needed to compose the synthesis/recognition panels
        var devices = DevicePanel();

        // Act & Assert: composing the shell without a panel is a programming error
        Assert.Throws<ArgumentNullException>(() =>
            new MainWindowViewModel(null!, CatalogPanel(), SynthesisPanel(devices), RecognitionPanel(devices)));
    }

    /// <summary>
    ///     Proves that the shell rejects a missing catalog panel.
    /// </summary>
    [Fact]
    public void MainWindowViewModel_Constructor_NullModelCatalog_ThrowsArgumentNullException()
    {
        // Arrange: a valid device panel, needed to compose the synthesis/recognition panels
        var devices = DevicePanel();

        // Act & Assert: composing the shell without a panel is a programming error
        Assert.Throws<ArgumentNullException>(() =>
            new MainWindowViewModel(devices, null!, SynthesisPanel(devices), RecognitionPanel(devices)));
    }

    /// <summary>
    ///     Proves that the shell rejects a missing synthesis panel.
    /// </summary>
    [Fact]
    public void MainWindowViewModel_Constructor_NullSynthesis_ThrowsArgumentNullException()
    {
        // Arrange: a valid device panel, needed to compose the recognition panel
        var devices = DevicePanel();

        // Act & Assert: composing the shell without a panel is a programming error
        Assert.Throws<ArgumentNullException>(() =>
            new MainWindowViewModel(devices, CatalogPanel(), null!, RecognitionPanel(devices)));
    }

    /// <summary>
    ///     Proves that the shell rejects a missing recognition panel.
    /// </summary>
    [Fact]
    public void MainWindowViewModel_Constructor_NullRecognition_ThrowsArgumentNullException()
    {
        // Arrange: a valid device panel, needed to compose the synthesis panel
        var devices = DevicePanel();

        // Act & Assert: composing the shell without a panel is a programming error
        Assert.Throws<ArgumentNullException>(() =>
            new MainWindowViewModel(devices, CatalogPanel(), SynthesisPanel(devices), null!));
    }

    /// <summary>
    ///     Proves that this phase's shell offers exactly the four implemented panels, in the
    ///     order a user would work through them.
    /// </summary>
    [Fact]
    public void MainWindowViewModel_Constructor_ValidPanels_OffersAllFourPanels()
    {
        // Arrange & Act: compose the shell over every panel
        var viewModel = FullShell();

        // Assert: every panel is offered, captioned, and ordered
        Assert.Equal(4, viewModel.Panels.Count);
        Assert.Equal("Audio Devices", viewModel.Panels[0].Title);
        Assert.Equal("Model Catalog", viewModel.Panels[1].Title);
        Assert.Equal("Text to Speech", viewModel.Panels[2].Title);
        Assert.Equal("Speech to Text", viewModel.Panels[3].Title);
    }

    /// <summary>
    ///     Proves that each navigation entry hosts the exact panel it was composed with, so the
    ///     window shows real state rather than a freshly constructed copy.
    /// </summary>
    [Fact]
    public void MainWindowViewModel_Constructor_ValidPanels_HostsTheInjectedPanelInstances()
    {
        // Arrange: distinct panel instances
        var devices = DevicePanel();
        var catalog = CatalogPanel();
        var synthesis = SynthesisPanel(devices);
        var recognition = RecognitionPanel(devices);

        // Act: compose the shell over them
        var viewModel = new MainWindowViewModel(devices, catalog, synthesis, recognition);

        // Assert: the shell hosts and exposes those exact instances
        Assert.Same(devices, viewModel.Panels[0].Content);
        Assert.Same(catalog, viewModel.Panels[1].Content);
        Assert.Same(synthesis, viewModel.Panels[2].Content);
        Assert.Same(recognition, viewModel.Panels[3].Content);
        Assert.Same(devices, viewModel.DeviceSelection);
        Assert.Same(catalog, viewModel.ModelCatalog);
        Assert.Same(synthesis, viewModel.Synthesis);
        Assert.Same(recognition, viewModel.Recognition);
    }

    /// <summary>
    ///     Proves that the window opens on a panel rather than a blank content area.
    /// </summary>
    [Fact]
    public void MainWindowViewModel_Constructor_ValidPanels_SelectsFirstPanel()
    {
        // Arrange & Act: compose the shell
        var viewModel = FullShell();

        // Assert: the first panel is shown at start-up
        Assert.Same(viewModel.Panels[0], viewModel.SelectedPanel);
    }

    /// <summary>
    ///     Proves that navigating between panels raises a change notification, which is what
    ///     drives the window's content area.
    /// </summary>
    [Fact]
    public void MainWindowViewModel_SelectedPanel_Changed_NotifiesSelectedPanel()
    {
        // Arrange: a composed shell with a recorded notification list
        var viewModel = FullShell();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        // Act: navigate to the model catalog panel
        viewModel.SelectedPanel = viewModel.Panels[1];

        // Assert: the change was announced to the bound window
        Assert.Contains(nameof(MainWindowViewModel.SelectedPanel), changed);
        Assert.Same(viewModel.Panels[1], viewModel.SelectedPanel);
    }

    /// <summary>
    ///     Proves that the window caption states what this build of the demo actually
    ///     demonstrates.
    /// </summary>
    [Fact]
    public void MainWindowViewModel_Title_Read_NamesTheImplementedCapabilities()
    {
        // Act: read the window caption
        var title = MainWindowViewModel.Title;

        // Assert: it names the demo and every implemented capability
        Assert.Contains("DemaConsulting.Speech Demo", title, StringComparison.Ordinal);
        Assert.Contains("Devices", title, StringComparison.Ordinal);
        Assert.Contains("Model Catalog", title, StringComparison.Ordinal);
        Assert.Contains("Text to Speech", title, StringComparison.Ordinal);
        Assert.Contains("Speech to Text", title, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that the shell surfaces whatever the panels report, including a machine with no
    ///     audio devices and a library with no known models.
    /// </summary>
    [Fact]
    public void MainWindowViewModel_Constructor_EmptyEnvironment_PanelsReportHonestEmptyState()
    {
        // Arrange & Act: compose the shell over a machine reporting nothing
        var viewModel = FullShell();

        // Assert: every panel reports its honest empty state without faulting
        Assert.False(viewModel.DeviceSelection.HasCaptureDevices);
        Assert.False(viewModel.DeviceSelection.HasPlaybackDevices);
        Assert.True(viewModel.ModelCatalog.IsCatalogEmpty);
        Assert.False(viewModel.Synthesis.HasModels);
        Assert.False(viewModel.Recognition.HasModels);
    }
}
