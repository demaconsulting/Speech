using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Proofreader.Diagnostics;
using DemaConsulting.Speech.Proofreader.Playback;
using DemaConsulting.Speech.Proofreader.Session;
using DemaConsulting.Speech.Proofreader.Synthesis;
using DemaConsulting.Speech.Proofreader.Text;

namespace DemaConsulting.Speech.Proofreader;

/// <summary>
///     The Avalonia application object and the tool's composition root.
/// </summary>
/// <remarks>
///     Objects are composed by hand, with no container, as in the Demo. A machine with no audio
///     backend and no installed models still gets a working window that says what is missing.
/// </remarks>
public sealed class App : Application
{
    /// <summary>
    ///     Gets or sets the parsed launch options, set by <see cref="Program.Main"/> before Avalonia
    ///     starts; <see langword="null"/> under design-time tooling.
    /// </summary>
    internal static LaunchOptions? Options { get; set; }

    /// <summary>
    ///     Loads the application's XAML-declared resources and styles.
    /// </summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    ///     Builds the object graph and shows the main window once Avalonia has initialized.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = Compose(Options);
            desktop.MainWindow = new MainWindow { DataContext = viewModel };

            // Release the engines and the catalog when the application exits, and open the file given on the command line.
            desktop.Exit += (_, _) => viewModel.Dispose();
            if (Options?.File is { } file)
            {
                desktop.MainWindow.Opened += (_, _) => _ = viewModel.OpenAsync(file);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    ///     Composes the view model and everything it needs.
    /// </summary>
    /// <param name="options">The launch options, or <see langword="null"/> for defaults.</param>
    /// <param name="settingsPath">
    ///     The user settings file, or <see langword="null"/> for the per-user default
    ///     (<see cref="UserSettingsStore.DefaultPath"/>); tests pass a temporary file.
    /// </param>
    /// <returns>The main view model; the caller disposes it.</returns>
    internal static MainViewModel Compose(LaunchOptions? options, string? settingsPath = null)
    {
        var catalog = ProofreaderCatalog.Create(options?.ModelsDir, options?.VoicesDir);
        var engineHost = new SynthesisEngineHost(catalog.Catalog);
        var cache = new SentenceAudioCache(engineHost);
        var player = new SentencePlayer(new AudioDeviceFactory());
        var splitter = new SentenceSplitter();
        var diagnostics = new DiagnosticsBuilder(SentenceSplitter.CreateAnalyzer());
        var settings = new UserSettingsStore(settingsPath ?? UserSettingsStore.DefaultPath());
        return new MainViewModel(catalog, engineHost, cache, player, splitter, diagnostics, settings);
    }
}
