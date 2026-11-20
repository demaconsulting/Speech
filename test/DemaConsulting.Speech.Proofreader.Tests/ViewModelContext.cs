using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Proofreader.Diagnostics;
using DemaConsulting.Speech.Proofreader.Playback;
using DemaConsulting.Speech.Proofreader.Session;
using DemaConsulting.Speech.Proofreader.Synthesis;
using DemaConsulting.Speech.Proofreader.Text;

namespace DemaConsulting.Speech.Proofreader.Tests;

/// <summary>
///     A view model over a real (possibly empty) catalog in a temporary directory.
/// </summary>
internal sealed class ViewModelContext : IDisposable
{
    /// <summary>The synthesis engine host.</summary>
    private readonly SynthesisEngineHost _host;

    /// <summary>The catalog.</summary>
    private readonly ProofreaderCatalog _catalog;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ViewModelContext"/> class.
    /// </summary>
    /// <param name="settingsPath">The user settings file to remember things in, or <see langword="null"/> for none.</param>
    /// <param name="timeProvider">The clock the view model uses, or <see langword="null"/> for the system clock.</param>
    /// <param name="synthesize">
    ///     Synthesizes a sentence in place of the real engines, or <see langword="null"/> to use them. Together
    ///     with <paramref name="device"/> it lets the real playback controller run without audio hardware or models.
    /// </param>
    /// <param name="device">The playback device to play through, or <see langword="null"/> for the real one.</param>
    public ViewModelContext(
        string? settingsPath = null,
        TimeProvider? timeProvider = null,
        Func<ISynthesisModel, ModelSelection, string, Task<SentenceAudio>>? synthesize = null,
        IAudioPlaybackDevice? device = null)
    {
        Directory = Path.Combine(Path.GetTempPath(), "proofreader-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        _catalog = ProofreaderCatalog.Create(Path.Combine(Directory, "models"), null);
        _host = new SynthesisEngineHost(_catalog.Catalog);
        Diagnostics = new DiagnosticsBuilder(SentenceSplitter.CreateAnalyzer());
        Player = device is null ? new SentencePlayer(new AudioDeviceFactory()) : new SentencePlayer(new AudioDeviceFactory(), _ => device);
        ViewModel = new MainViewModel(
            _catalog,
            _host,
            synthesize is null ? new SentenceAudioCache(_host) : new SentenceAudioCache(synthesize),
            Player,
            new SentenceSplitter(),
            Diagnostics,
            settingsPath is null ? null : new UserSettingsStore(settingsPath),
            timeProvider);
    }

    /// <summary>Gets the temporary directory.</summary>
    public string Directory { get; }

    /// <summary>Gets the diagnostics builder shared with the view model.</summary>
    public DiagnosticsBuilder Diagnostics { get; }

    /// <summary>Gets the view model.</summary>
    public MainViewModel ViewModel { get; }

    /// <summary>Gets the audio player the view model plays through.</summary>
    public SentencePlayer Player { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Disposing the view model releases the host and the catalog.
        ViewModel.Dispose();
        System.IO.Directory.Delete(Directory, recursive: true);
    }
}
