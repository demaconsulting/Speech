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
using DemaConsulting.Speech.Onnx.ChatterboxTurbo;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;
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
///     a reference application. The composition itself lives in <see cref="Compose"/>, separate
///     from the Avalonia lifetime hookup, so it can be exercised without a running desktop. A
///     machine with no audio backend and no installed models still produces a fully working
///     window that honestly reports what is unavailable; <see cref="Program.Main"/> validates
///     the launch options before this class runs, so composition does not fail on bad input.
/// </remarks>
public sealed class App : Application
{
    /// <summary>
    ///     This demo's parsed <c>--models-dir</c>/<c>--voices-dir</c>/<c>--mirror-*</c> launch options, set by
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
    ///     The in-flight deferred shutdown, retained so the task is observable rather than
    ///     discarded. It never faults: every teardown step's failure is contained.
    /// </summary>
    private Task? _shutdownTask;

    /// <summary>
    ///     Gets the in-flight deferred shutdown, or <see langword="null"/> before shutdown starts.
    /// </summary>
    internal Task? ShutdownTask => _shutdownTask;

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
            var viewModel = Compose(LaunchOptions, out var catalogService);
            _catalogService = catalogService;

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

                // Set synchronously, before CompleteShutdownAsync starts, so a second
                // ShutdownRequested that races in while cleanup is still pending (for example,
                // the user closing the window again) already sees this guard set and cannot
                // start a second concurrent cleanup against the same ViewModels and catalog.
                _shuttingDown = true;

                e.Cancel = true;
                _shutdownTask = CompleteShutdownAsync(desktop, viewModel);
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    ///     Builds the demo's complete object graph by hand over the library's real public entry
    ///     points.
    /// </summary>
    /// <remarks>
    ///     Kept separate from <see cref="OnFrameworkInitializationCompleted"/> so the composition
    ///     can be exercised without a running desktop lifetime. A missing audio backend degrades
    ///     to honest unavailable probes, and the model catalog is valid even when the library's
    ///     compiled-in known-model registry is empty, so those entry points do not throw.
    ///     <paramref name="launchOptions"/> is expected to have been validated already (as
    ///     <see cref="Program.Main"/> does), because
    ///     <see cref="AppLaunchOptions.CreateDownloaderOptions"/> throws for an invalid mirror
    ///     configuration.
    /// </remarks>
    /// <param name="launchOptions">The parsed launch options, or <see langword="null"/> for defaults.</param>
    /// <param name="catalogService">
    ///     Receives the catalog service the caller must dispose when the application exits.
    /// </param>
    /// <returns>The main window view model with every panel composed.</returns>
    internal static MainWindowViewModel Compose(AppLaunchOptions? launchOptions, out ModelCatalogService catalogService)
    {
        var audioFactory = new AudioDeviceFactory();
        var storeOptions = launchOptions?.CreateStoreOptions();
        var downloaderOptions = launchOptions?.CreateDownloaderOptions();

        // Captures storeOptions and the Sherpa/Kokoro/Nemotron/Chatterbox composition so the
        // catalog service below can rebuild an equivalent catalog, pointed at a new mirror,
        // whenever the user applies new mirror settings from the model catalog panel, or
        // installs/removes a reference-voice pack (the Chatterbox voice list is read when the
        // catalog is built). Exactly one ReferenceVoiceLibrary is created per catalog and shared
        // between the Chatterbox model (voice list) and the service (pack install/uninstall),
        // so both use the same store root and mirror - the same arrangement the CLI uses.
        ModelCatalogComposition CatalogFactory(SpeechModelDownloaderOptions? options)
        {
            var catalog = new SpeechModelCatalog(storeOptions, options, diagnostics: null)
                .AddSherpaModels()
                .AddKokoroModels()
                .AddNemotronSttModels();
            var voiceLibrary = new ReferenceVoiceLibrary(catalog.Store, options);
            catalog.AddChatterboxTurboModels(new ChatterboxTurboModelOptions
            {
                ReferenceVoiceDirectory = launchOptions?.VoicesDir,
                ReferenceVoiceLibrary = voiceLibrary,
            });
            return new ModelCatalogComposition(catalog, voiceLibrary);
        }

        var initialComposition = CatalogFactory(downloaderOptions);

        // A second store instance resolving the same root directory as the catalog's own
        // internal store (honoring the same --models-dir override, if supplied), used only
        // to locate an installed model's files for the synthesis/recognition session seams.
        var modelStore = new SpeechModelStore(storeOptions);

        // Wrap each concrete/static library entry point in the demo's own service seam so the
        // panel ViewModels depend only on interfaces this application owns.
        var deviceService = new AudioDeviceService(audioFactory);
        catalogService = new ModelCatalogService(initialComposition, downloaderOptions, CatalogFactory);
        var deviceSelection = new DeviceSelectionViewModel(deviceService);

        return new MainWindowViewModel(
            deviceSelection,
            new ModelCatalogViewModel(
                catalogService,
                launchOptions?.MirrorUrl,
                launchOptions?.MirrorUser,
                launchOptions?.MirrorPassword,
                launchOptions?.MirrorBearerToken),
            new SynthesisPanelViewModel(catalogService, deviceService, deviceSelection, new SynthesizerSessionFactory(modelStore)),
            new RecognitionPanelViewModel(catalogService, deviceService, deviceSelection, new RecognizerSessionFactory(modelStore)));
    }

    /// <summary>
    ///     Runs every teardown step in order, containing each step's failure separately so one
    ///     faulting step never prevents the remaining steps from running.
    /// </summary>
    /// <param name="steps">The teardown steps, run sequentially in the supplied order.</param>
    /// <returns>A task that completes once every step has run, whether or not any faulted.</returns>
    internal static async Task RunTeardownAsync(params Func<ValueTask>[] steps)
    {
        foreach (var step in steps)
        {
            try
            {
                await step();
            }
            catch
            {
                // Intentionally broad: a teardown fault must not prevent the remaining
                // resources from being released or the application from exiting.
            }
        }
    }

    /// <summary>
    ///     Runs this application's asynchronous teardown to genuine completion, then lets the
    ///     deferred shutdown this type requested in <see cref="OnFrameworkInitializationCompleted"/>
    ///     proceed.
    /// </summary>
    /// <remarks>
    ///     <see cref="_shuttingDown"/> is already set to <see langword="true"/> by the caller
    ///     before this method starts; this method's own <see langword="finally"/> only has to
    ///     resume shutdown once cleanup has settled, whether it succeeded or faulted.
    /// </remarks>
    /// <param name="desktop">The desktop lifetime to resume shutdown on once cleanup has completed.</param>
    /// <param name="viewModel">The main window's view model owning the panels to dispose.</param>
    private async Task CompleteShutdownAsync(IClassicDesktopStyleApplicationLifetime desktop, MainWindowViewModel viewModel)
    {
        try
        {
            await RunTeardownAsync(
                viewModel.Synthesis.DisposeAsync,
                viewModel.Recognition.DisposeAsync,
                () =>
                {
                    _catalogService?.Dispose();
                    return ValueTask.CompletedTask;
                });
        }
        finally
        {
            desktop.Shutdown();
        }
    }
}
