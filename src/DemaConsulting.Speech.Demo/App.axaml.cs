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
using DemaConsulting.Speech.Onnx.Kokoro;
using DemaConsulting.Speech.Onnx.NemotronStt;
using DemaConsulting.Speech.Sherpa;

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
    ///     This demo's parsed <c>--models-dir</c>/<c>--mirror-*</c> launch options, set by
    ///     <see cref="Program.Main"/> before the Avalonia lifetime starts, or <see langword="null"/>
    ///     when running under Avalonia's design-time tooling (which constructs this type without
    ///     ever calling <see cref="Program.Main"/>). See <see cref="AppLaunchOptions"/>'s remarks
    ///     for how launch-time options complement the in-app mirror-settings panel.
    /// </summary>
    public static AppLaunchOptions? LaunchOptions { get; set; }

    /// <summary>
    ///     The model catalog service owned by this application, disposed when the desktop
    ///     lifetime shuts down.
    /// </summary>
    /// <remarks>
    ///     Typed as the concrete <see cref="ModelCatalogService"/> (not the
    ///     <see cref="IModelCatalogService"/> seam it implements) because disposal is this
    ///     composition root's own responsibility and <see cref="IModelCatalogService"/>
    ///     deliberately does not extend <see cref="IDisposable"/> - see that interface's remarks.
    /// </remarks>
    private ModelCatalogService? _catalogService;

    /// <summary>
    ///     Set once the deferred shutdown's async cleanup has been started, so the second,
    ///     genuine <c>ShutdownRequested</c> raised by this type's own call to
    ///     <see cref="IControlledApplicationLifetime.Shutdown"/> is allowed to proceed instead of
    ///     being deferred again.
    /// </summary>
    private bool _shuttingDown;

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
            var storeOptions = LaunchOptions?.CreateStoreOptions();
            var downloaderOptions = LaunchOptions?.CreateDownloaderOptions();

            // Captures storeOptions and the Sherpa/Kokoro/Nemotron composition so the catalog service
            // below can rebuild an equivalent catalog, pointed at a new mirror, whenever the
            // user applies new mirror settings from the model catalog panel.
            SpeechModelCatalog CatalogFactory(SpeechModelDownloaderOptions? options) =>
                new SpeechModelCatalog(storeOptions, options, diagnostics: null)
                    .AddSherpaModels()
                    .AddKokoroModels()
                    .AddNemotronSttModels();

            var initialCatalog = CatalogFactory(downloaderOptions);

            // A second store instance resolving the same root directory as the catalog's own
            // internal store (honoring the same --models-dir override, if supplied), used only
            // to locate an installed model's files for the
            // synthesis/recognition session seams below.
            var modelStore = new SpeechModelStore(storeOptions);

            // Wrap each concrete/static library entry point in the demo's own service seam so the
            // panel ViewModels depend only on interfaces this application owns.
            var deviceService = new AudioDeviceService(audioFactory);
            var catalogService = new ModelCatalogService(initialCatalog, CatalogFactory);
            _catalogService = catalogService;
            var synthesizerSessionFactory = new SynthesizerSessionFactory(modelStore);
            var recognizerSessionFactory = new RecognizerSessionFactory(modelStore);

            var deviceSelection = new DeviceSelectionViewModel(deviceService);

            var viewModel = new MainWindowViewModel(
                deviceSelection,
                new ModelCatalogViewModel(
                    catalogService,
                    LaunchOptions?.MirrorUrl,
                    LaunchOptions?.MirrorUser,
                    LaunchOptions?.MirrorPassword,
                    LaunchOptions?.MirrorBearerToken),
                new SynthesisPanelViewModel(catalogService, deviceService, deviceSelection, synthesizerSessionFactory),
                new RecognitionPanelViewModel(catalogService, deviceService, deviceSelection, recognizerSessionFactory));

            desktop.MainWindow = new MainWindow { DataContext = viewModel };

            // Release the catalog's download machinery and each panel ViewModel's own
            // subscriptions/resources when the application exits. ShutdownRequested is a
            // synchronous event with no async-aware overload, so this handler defers the
            // shutdown Avalonia is about to perform (via ShutdownRequestedEventArgs.Cancel),
            // synchronously, runs the async cleanup to genuine completion, and only then calls
            // IClassicDesktopStyleApplicationLifetime.Shutdown() to let the real shutdown
            // proceed - rather than an async void handler that would return control to Avalonia
            // at its first incomplete await and let shutdown continue while cleanup (and any
            // exception it throws) is still pending.
            desktop.ShutdownRequested += (object? _, ShutdownRequestedEventArgs e) =>
            {
                if (_shuttingDown)
                {
                    // This is the second, genuine ShutdownRequested raised by this handler's own
                    // call to Shutdown() below, once cleanup has already completed.
                    return;
                }

                // Set synchronously, before the first await inside CompleteShutdownAsync runs
                // (finding 29): a second ShutdownRequested racing in during that first await -
                // for example, the user closing the window again while cleanup is still pending -
                // must see this guard already set, rather than finding it still false and
                // starting a second concurrent cleanup task against the same ViewModels/catalog.
                _shuttingDown = true;

                e.Cancel = true;
                CompleteShutdownAsync(desktop, viewModel).ConfigureAwait(false);
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    ///     Runs this application's asynchronous teardown to genuine completion, then lets the
    ///     deferred shutdown this type requested in <see cref="OnFrameworkInitializationCompleted"/>
    ///     proceed.
    /// </summary>
    /// <remarks>
    ///     <see cref="_shuttingDown"/> is already set to <see langword="true"/> by the caller
    ///     before this method starts (finding 29), so no field assignment is needed here; this
    ///     method's own <see langword="finally"/> only has to resume shutdown once cleanup has
    ///     settled, whether it succeeded or faulted.
    /// </remarks>
    /// <param name="desktop">The desktop lifetime to resume shutdown on once cleanup has completed.</param>
    /// <param name="viewModel">The main window's view model owning the panels to dispose.</param>
    private async Task CompleteShutdownAsync(IClassicDesktopStyleApplicationLifetime desktop, MainWindowViewModel viewModel)
    {
        try
        {
            await viewModel.Synthesis.DisposeAsync();
            await viewModel.Recognition.DisposeAsync();
            _catalogService?.Dispose();
        }
        catch
        {
            // Intentionally broad: a teardown fault must not prevent the application from
            // actually exiting once shutdown has been requested.
        }
        finally
        {
            desktop.Shutdown();
        }
    }
}
