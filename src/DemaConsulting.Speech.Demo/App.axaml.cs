using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Demo.DeviceSelectionSubsystem;
using DemaConsulting.Speech.Demo.ModelCatalogSubsystem;
using DemaConsulting.Speech.Demo.RecognitionPanelSubsystem;
using DemaConsulting.Speech.Demo.ShellSubsystem;
using DemaConsulting.Speech.Demo.SynthesisPanelSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo;

/// <summary>
///     The Avalonia application object and the demo's single composition root.
/// </summary>
/// <remarks>
///     All object construction happens here, by hand, with no dependency-injection container. That
///     mirrors the library itself, which composes through explicit factories rather than a
///     container, and it keeps the demo's object graph readable in one place - the whole point of
///     a reference application. Because every library entry point used here is contractually
///     incapable of throwing at composition, this method needs no error handling: a machine with
///     no audio backend and no installed models still produces a fully working window that
///     honestly reports what is unavailable.
/// </remarks>
public sealed class App : Application
{
    /// <summary>
    ///     The library model catalog owned by this application, disposed when the desktop
    ///     lifetime shuts down.
    /// </summary>
    private SpeechModelCatalog? _catalog;

    /// <summary>
    ///     Loads the application's XAML-declared resources and styles.
    /// </summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    ///     Builds the demo's object graph and shows the main window once Avalonia's framework
    ///     initialization has completed.
    /// </summary>
    /// <remarks>
    ///     Composition is deliberately deferred to this point rather than done in the constructor
    ///     because the desktop lifetime - and therefore the ability to attach a main window and a
    ///     shutdown hook - does not exist until Avalonia calls this method.
    /// </remarks>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Compose the library entry points. Neither of these throws: a missing audio backend
            // degrades to honest unavailable probes, and the model catalog is valid even when the
            // library's compiled-in known-model registry is empty.
            var audioFactory = new AudioDeviceFactory();
            _catalog = new SpeechModelCatalog();

            // A second store instance resolving the same default root directory as the catalog's
            // own internal store, used only to locate an installed model's files for the
            // synthesis/recognition session seams below.
            var modelStore = new SpeechModelStore();

            // Wrap each concrete/static library entry point in the demo's own service seam so the
            // panel ViewModels depend only on interfaces this application owns.
            var deviceService = new AudioDeviceService(audioFactory);
            var catalogService = new ModelCatalogService(_catalog);
            var synthesizerSessionFactory = new SynthesizerSessionFactory(modelStore);
            var recognizerSessionFactory = new RecognizerSessionFactory(modelStore);

            var deviceSelection = new DeviceSelectionViewModel(deviceService);

            var viewModel = new MainWindowViewModel(
                deviceSelection,
                new ModelCatalogViewModel(catalogService),
                new SynthesisPanelViewModel(catalogService, deviceService, deviceSelection, synthesizerSessionFactory),
                new RecognitionPanelViewModel(catalogService, deviceService, deviceSelection, recognizerSessionFactory));

            desktop.MainWindow = new MainWindow { DataContext = viewModel };

            // Release the catalog's download machinery and each panel ViewModel's own
            // subscriptions/resources when the application exits.
            desktop.ShutdownRequested += (_, _) =>
            {
                viewModel.Synthesis.Dispose();
                viewModel.Recognition.Dispose();
                _catalog?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
