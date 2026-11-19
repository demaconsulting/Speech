using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Proofreader.Diagnostics;
using DemaConsulting.Speech.Proofreader.Playback;
using DemaConsulting.Speech.Proofreader.Session;
using DemaConsulting.Speech.Proofreader.Settings;
using DemaConsulting.Speech.Proofreader.Synthesis;
using DemaConsulting.Speech.Proofreader.Text;

namespace DemaConsulting.Speech.Proofreader;

/// <summary>
///     The single view model of the Proofreader window: the text and its sentences, playback, the
///     two model slots, diagnostics, and (in the partial files) flags and sessions.
/// </summary>
/// <remarks>
///     It orchestrates the playback controller and is the controller's host. Everything runs on the
///     UI thread; synthesis, resampling, text analysis, and diagnostics run on the thread pool and
///     report back through awaited tasks. Navigation has one rule: while something is playing or
///     paused, jumping to a sentence restarts playback there (or stops first when the jump is only a
///     selection), so the highlighted sentence is always the one that would be heard next.
/// </remarks>
internal sealed partial class MainViewModel : ObservableObject, IPlaybackHost, IDisposable
{
    /// <summary>The model catalog.</summary>
    private readonly ProofreaderCatalog _catalog;

    /// <summary>The synthesis engine host.</summary>
    private readonly SynthesisEngineHost _engineHost;

    /// <summary>The synthesis cache.</summary>
    private readonly SentenceAudioCache _cache;

    /// <summary>The audio player.</summary>
    private readonly SentencePlayer _player;

    /// <summary>The sentence splitter.</summary>
    private readonly SentenceSplitter _splitter;

    /// <summary>The diagnostics builder.</summary>
    private readonly DiagnosticsBuilder _diagnostics;

    /// <summary>The playback controller.</summary>
    private readonly PlaybackController _controller;

    /// <summary>The sentences of the loaded text.</summary>
    private List<ProofreadSentence> _sentences = [];

    /// <summary>The plain text that was split (Markdown already stripped).</summary>
    private string _plainText = string.Empty;

    /// <summary>Set while the view model changes <see cref="CurrentIndex"/> itself, so it is not mistaken for a user jump.</summary>
    private bool _suppressJump;

    /// <summary>Incremented per diagnostics request so a slow, stale result is dropped.</summary>
    private int _diagnosticsVersion;

    /// <summary>The diagnostics currently shown, with the sentence they belong to.</summary>
    private (int Index, SentenceDiagnostics Diagnostics)? _shownDiagnostics;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    /// <param name="catalog">The model catalog.</param>
    /// <param name="engineHost">The synthesis engine host.</param>
    /// <param name="cache">The synthesis cache over <paramref name="engineHost"/>.</param>
    /// <param name="player">The audio player.</param>
    /// <param name="splitter">The sentence splitter.</param>
    /// <param name="diagnostics">The diagnostics builder.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public MainViewModel(
        ProofreaderCatalog catalog,
        SynthesisEngineHost engineHost,
        SentenceAudioCache cache,
        SentencePlayer player,
        SentenceSplitter splitter,
        DiagnosticsBuilder diagnostics)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(engineHost);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(splitter);
        ArgumentNullException.ThrowIfNull(diagnostics);

        _catalog = catalog;
        _engineHost = engineHost;
        _cache = cache;
        _player = player;
        _splitter = splitter;
        _diagnostics = diagnostics;
        _controller = new PlaybackController(this, cache, player);

        RefreshModels();
        LoadDevices();
    }

    /// <summary>Gets the sentence rows.</summary>
    public ObservableCollection<SentenceItemViewModel> Sentences { get; } = [];

    /// <summary>Gets model slot A.</summary>
    public ModelSlotViewModel SlotA { get; } = new("A");

    /// <summary>Gets model slot B.</summary>
    public ModelSlotViewModel SlotB { get; } = new("B");

    /// <summary>Gets the playback devices.</summary>
    public ObservableCollection<DeviceChoice> PlaybackDevices { get; } = [];

    /// <summary>Gets or sets the text typed or pasted into the paste box.</summary>
    [ObservableProperty]
    public partial string SourceText { get; set; } = string.Empty;

    /// <summary>Gets or sets the name of the loaded text (a file name, or "Pasted text").</summary>
    [ObservableProperty]
    public partial string SourceName { get; set; } = "No text loaded";

    /// <summary>Gets or sets the zero-based index of the current sentence, or -1.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PositionText))]
    public partial int CurrentIndex { get; set; } = -1;

    /// <summary>Gets or sets the full text of the current sentence.</summary>
    [ObservableProperty]
    public partial string CurrentSentenceText { get; set; } = string.Empty;

    /// <summary>Gets or sets the status banner message.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string StatusMessage { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the status message describes a problem.</summary>
    [ObservableProperty]
    public partial bool StatusIsError { get; set; }

    /// <summary>Gets or sets the playback state.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText))]
    [NotifyPropertyChangedFor(nameof(PlayPauseLabel))]
    public partial PlaybackState State { get; set; } = PlaybackState.Stopped;

    /// <summary>Gets or sets the slot (A or B) that is, or was last, audible.</summary>
    [ObservableProperty]
    public partial string ActiveSlotName { get; set; } = "A";

    /// <summary>Gets or sets a value indicating whether each sentence plays through slot A and then slot B.</summary>
    [ObservableProperty]
    public partial bool AbMode { get; set; }

    /// <summary>Gets or sets the selected playback device.</summary>
    [ObservableProperty]
    public partial DeviceChoice? SelectedDevice { get; set; }

    /// <summary>Gets or sets a one-line description of the playback device state.</summary>
    [ObservableProperty]
    public partial string DeviceStatus { get; set; } = string.Empty;

    /// <summary>Gets or sets the analysis pane text.</summary>
    [ObservableProperty]
    public partial string AnalysisText { get; set; } = "Select or play a sentence to see its analysis.";

    /// <summary>Gets or sets the phoneme pane text.</summary>
    [ObservableProperty]
    public partial string PhonemeText { get; set; } = "Play a sentence to see the phonemes the model consumed.";

    /// <summary>Gets or sets a value indicating whether a long operation (text analysis) is running.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Gets a value indicating whether the status banner has something to show.</summary>
    public bool HasStatus => StatusMessage.Length > 0;

    /// <summary>Gets a value indicating whether a text with at least one sentence is loaded.</summary>
    public bool HasSentences => _sentences.Count > 0;

    /// <summary>Gets the position of the current sentence as text.</summary>
    public string PositionText => _sentences.Count == 0 ? "No text loaded" : $"Sentence {CurrentIndex + 1} of {_sentences.Count}";

    /// <summary>Gets the playback state as text.</summary>
    public string StateText => State.ToString();

    /// <summary>Gets the label of the play/pause button.</summary>
    public string PlayPauseLabel => State switch
    {
        PlaybackState.Playing or PlaybackState.Loading => "Pause",
        PlaybackState.Paused => "Resume",
        _ => "Play",
    };

    /// <inheritdoc/>
    int IPlaybackHost.SentenceCount => _sentences.Count;

    /// <inheritdoc/>
    string IPlaybackHost.GetSentenceText(int index) => _sentences[index].Text;

    /// <inheritdoc/>
    IReadOnlyList<PlaybackTarget> IPlaybackHost.GetTargets() =>
        AbMode ? [SlotA.ToTarget(), SlotB.ToTarget()] : [SlotA.ToTarget()];

    /// <inheritdoc/>
    void IPlaybackHost.OnStateChanged(PlaybackState state) => State = state;

    /// <inheritdoc/>
    void IPlaybackHost.OnSentenceStarted(int index, string slotName)
    {
        ActiveSlotName = slotName;
        SetCurrent(index);
        RefreshDiagnostics(index, slotName, null);
    }

    /// <inheritdoc/>
    void IPlaybackHost.OnAudioReady(int index, PlaybackTarget target, SentenceAudio audio) =>
        RefreshDiagnostics(index, target.Name, audio);

    /// <inheritdoc/>
    void IPlaybackHost.OnMessage(string? message, bool isError) => SetMessage(message, isError);

    /// <summary>
    ///     Plays from the current sentence, or pauses or resumes.
    /// </summary>
    [RelayCommand]
    internal void PlayPause()
    {
        if (HasSentences)
        {
            _controller.TogglePlayPause(Math.Max(CurrentIndex, 0));
        }
    }

    /// <summary>
    ///     Stops playback.
    /// </summary>
    [RelayCommand]
    internal void Stop() => _controller.Stop();

    /// <summary>
    ///     Moves to the previous sentence.
    /// </summary>
    [RelayCommand]
    internal void Previous() => JumpTo(CurrentIndex - 1, play: false);

    /// <summary>
    ///     Moves to the next sentence.
    /// </summary>
    [RelayCommand]
    internal void Next() => JumpTo(CurrentIndex + 1, play: false);

    /// <summary>
    ///     Plays the current sentence again (and the ones after it).
    /// </summary>
    [RelayCommand]
    internal void Replay()
    {
        if (HasSentences)
        {
            _controller.PlayFrom(Math.Max(CurrentIndex, 0));
        }
    }

    /// <summary>
    ///     Toggles A/B mode.
    /// </summary>
    [RelayCommand]
    internal void ToggleAb() => AbMode = !AbMode;

    /// <summary>
    ///     Loads the text of the paste box.
    /// </summary>
    /// <returns>A task that completes when the text has been split into sentences.</returns>
    [RelayCommand]
    internal Task LoadPastedTextAsync() => LoadTextAsync(SourceText, "Pasted text", isMarkdown: false);

    /// <summary>
    ///     Re-reads which models are installed.
    /// </summary>
    [RelayCommand]
    internal void RefreshModels()
    {
        var descriptors = _catalog.EnumerateSynthesisModels();
        SlotA.SetModels(descriptors, 0);
        SlotB.SetModels(descriptors, 1);
        if (descriptors.All(descriptor => descriptor.State != SpeechModelState.Downloaded))
        {
            SetMessage(
                "No synthesis model is installed. Install one (Kokoro, Piper, ...) with the Demo or the ModelDownloader tool, then press Refresh models.",
                true);
        }
    }

    /// <summary>
    ///     Re-reads the audio devices.
    /// </summary>
    [RelayCommand]
    internal void RefreshDevices()
    {
        try
        {
            _controller.Stop();
            _player.RefreshDevices();
        }
        catch (AudioDeviceInUseException ex)
        {
            SetMessage($"Devices could not be refreshed while audio is active: {ex.Message}", true);
        }

        LoadDevices();
    }

    /// <summary>
    ///     Lists the audio devices the backend currently reports, keeping the selection when possible.
    /// </summary>
    private void LoadDevices()
    {
        var previous = SelectedDevice?.Name;
        PlaybackDevices.Clear();
        PlaybackDevices.Add(new DeviceChoice(null));
        foreach (var name in _player.EnumerateDevices())
        {
            PlaybackDevices.Add(new DeviceChoice(name));
        }

        SelectedDevice = PlaybackDevices.FirstOrDefault(device => device.Name == previous) ?? PlaybackDevices[0];
        DeviceStatus = PlaybackDevices.Count > 1
            ? $"{PlaybackDevices.Count - 1} output device(s) found."
            : "No playback device found; playback is unavailable until one is connected.";
    }

    /// <summary>
    ///     Loads a file given on the command line: a <c>.json</c> file is a session, anything else is text.
    /// </summary>
    /// <param name="path">The file to open.</param>
    /// <returns>A task that completes when the file is loaded (or the problem has been reported).</returns>
    internal Task OpenAsync(string path) =>
        Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase)
            ? LoadSessionAsync(path)
            : LoadFileAsync(path);

    /// <summary>
    ///     Loads a text file; Markdown (<c>.md</c>, <c>.markdown</c>) is reduced to plain prose first.
    /// </summary>
    /// <param name="path">The file to load.</param>
    /// <returns>A task that completes when the text has been split into sentences.</returns>
    internal async Task LoadFileAsync(string path)
    {
        string text;
        try
        {
            text = await File.ReadAllTextAsync(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetMessage($"Could not read '{path}': {ex.Message}", true);
            return;
        }

        var extension = Path.GetExtension(path);
        var isMarkdown = extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase);
        await LoadTextAsync(text, Path.GetFileName(path), isMarkdown);
    }

    /// <summary>
    ///     Splits text into sentences and makes it the loaded text, discarding the flags of any previous text.
    /// </summary>
    /// <param name="text">The text (or Markdown) to load.</param>
    /// <param name="name">The name to show for the source.</param>
    /// <param name="isMarkdown">Whether <paramref name="text"/> is Markdown to be stripped.</param>
    /// <returns>A task that completes when the text is loaded (or the problem has been reported).</returns>
    internal async Task LoadTextAsync(string text, string name, bool isMarkdown)
    {
        if (IsBusy)
        {
            return;
        }

        _controller.Stop();
        IsBusy = true;
        SetMessage("Analyzing the text...", false);
        try
        {
            var plain = isMarkdown ? MarkdownStripper.Strip(text) : text;
            var sentences = await Task.Run(() => _splitter.Split(plain));
            ApplyText(plain, sentences, name);
            Flags.Clear();
            SessionPath = null;
            SetMessage(
                sentences.Count == 0
                    ? "The text has no sentences to proofread."
                    : $"Loaded {sentences.Count} sentences from {name}. Press Space to play.",
                sentences.Count == 0);
        }
        catch (Exception ex)
        {
            // Intentionally broad: the analysis runs on the thread pool and any failure must be
            // shown as a message instead of leaving the window half-loaded.
            SetMessage($"The text could not be analyzed: {ex.Message}", true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    ///     Moves to a sentence.
    /// </summary>
    /// <param name="index">The target sentence; an index outside the text is ignored.</param>
    /// <param name="play">
    ///     Whether playback must start there. Without it, playback that is running restarts at the
    ///     target and playback that is paused is stopped, so the selection and the audio never disagree.
    /// </param>
    internal void JumpTo(int index, bool play)
    {
        if (index < 0 || index >= _sentences.Count)
        {
            return;
        }

        // Moving to the sentence that is already current must not disturb a pause.
        if (!play && index == CurrentIndex && State is PlaybackState.Paused or PlaybackState.Stopped)
        {
            return;
        }

        if (play || State is PlaybackState.Playing or PlaybackState.Loading)
        {
            _controller.PlayFrom(index);
            return;
        }

        if (State == PlaybackState.Paused)
        {
            _controller.Stop();
        }

        SetCurrent(index);
        RefreshDiagnostics(index, ActiveSlotName, null);
    }

    /// <summary>
    ///     Pauses playback and stops it for good when the window closes, then releases the engines and the catalog.
    /// </summary>
    public void Dispose()
    {
        _controller.Stop();

        // Engine disposal is asynchronous; the process is exiting, so wait for it with a bound
        // rather than leaving native resources to the finalizers.
        Task.Run(() => _engineHost.DisposeAsync().AsTask()).Wait(TimeSpan.FromSeconds(5));
        _catalog.Dispose();
    }

    /// <summary>
    ///     Applies a loaded text to the sentence list.
    /// </summary>
    /// <param name="plain">The plain text that was split.</param>
    /// <param name="sentences">The sentences of <paramref name="plain"/>.</param>
    /// <param name="name">The source name.</param>
    private void ApplyText(string plain, IReadOnlyList<ProofreadSentence> sentences, string name)
    {
        _plainText = plain;
        _sentences = [.. sentences];
        SourceName = name;

        _suppressJump = true;
        try
        {
            Sentences.Clear();
            foreach (var sentence in sentences)
            {
                Sentences.Add(new SentenceItemViewModel(sentence));
            }

            CurrentIndex = sentences.Count > 0 ? 0 : -1;
        }
        finally
        {
            _suppressJump = false;
        }

        CurrentSentenceText = sentences.Count > 0 ? sentences[0].Text : string.Empty;
        OnPropertyChanged(nameof(HasSentences));
        OnPropertyChanged(nameof(PositionText));
        _shownDiagnostics = null;
        if (sentences.Count > 0)
        {
            RefreshDiagnostics(0, ActiveSlotName, null);
        }
    }

    /// <summary>
    ///     Sets the current sentence without treating it as a user jump.
    /// </summary>
    /// <param name="index">The sentence index.</param>
    private void SetCurrent(int index)
    {
        _suppressJump = true;
        try
        {
            CurrentIndex = index;
        }
        finally
        {
            _suppressJump = false;
        }
    }

    /// <summary>
    ///     Reacts to a selection made by the user in the sentence list: a click jumps and plays.
    /// </summary>
    /// <param name="value">The new current index.</param>
    partial void OnCurrentIndexChanged(int value)
    {
        CurrentSentenceText = value >= 0 && value < _sentences.Count ? _sentences[value].Text : string.Empty;
        if (!_suppressJump && value >= 0)
        {
            JumpTo(value, play: true);
        }
    }

    /// <summary>
    ///     Applies the playback device the user chose.
    /// </summary>
    /// <param name="value">The chosen device.</param>
    partial void OnSelectedDeviceChanged(DeviceChoice? value)
    {
        if (value is null)
        {
            return;
        }

        _controller.Stop();
        _player.SelectDevice(value.Name);
    }

    /// <summary>
    ///     Shows or clears the status banner.
    /// </summary>
    /// <param name="message">The message, or <see langword="null"/> to clear it.</param>
    /// <param name="isError">Whether the message describes a problem.</param>
    private void SetMessage(string? message, bool isError)
    {
        StatusMessage = message ?? string.Empty;
        StatusIsError = isError;
    }

    /// <summary>
    ///     Gets a slot by name.
    /// </summary>
    /// <param name="name">The slot name.</param>
    /// <returns>Slot B for <c>B</c>, otherwise slot A.</returns>
    private ModelSlotViewModel SlotNamed(string name) => name == "B" ? SlotB : SlotA;

    /// <summary>
    ///     Rebuilds the diagnostics panes for a sentence and slot on the thread pool.
    /// </summary>
    /// <param name="index">The sentence index.</param>
    /// <param name="slotName">The slot whose model is shown.</param>
    /// <param name="audio">The sentence's audio when known; otherwise the cache is consulted.</param>
    private void RefreshDiagnostics(int index, string slotName, SentenceAudio? audio)
    {
        var slot = SlotNamed(slotName);
        if (slot.SelectedModel is not { } choice || index < 0 || index >= _sentences.Count)
        {
            return;
        }

        var text = _sentences[index].Text;
        var selection = slot.CurrentSelection;
        audio ??= selection is null ? null : _cache.TryGetCompleted(selection, text);

        var version = ++_diagnosticsVersion;
        _ = ApplyDiagnosticsAsync(choice.Model, index, text, audio, version);
    }

    /// <summary>
    ///     Builds diagnostics off the UI thread and shows them unless a newer request has superseded them.
    /// </summary>
    /// <param name="model">The real model.</param>
    /// <param name="index">The sentence index.</param>
    /// <param name="text">The sentence text.</param>
    /// <param name="audio">The sentence audio, or <see langword="null"/>.</param>
    /// <param name="version">The request number.</param>
    /// <returns>A task that never faults.</returns>
    private async Task ApplyDiagnosticsAsync(
        ISynthesisModel model,
        int index,
        string text,
        SentenceAudio? audio,
        int version)
    {
        try
        {
            var result = await Task.Run(() => _diagnostics.Build(model, text, audio));
            if (version != _diagnosticsVersion)
            {
                return;
            }

            _shownDiagnostics = (index, result);
            AnalysisText = result.FormatAnalysis();
            PhonemeText = result.FormatPhonemes();
        }
        catch (Exception ex)
        {
            // Intentionally broad: diagnostics are an aid; a failure is shown in place of the
            // panes instead of disturbing playback.
            if (version == _diagnosticsVersion)
            {
                AnalysisText = $"Diagnostics failed: {ex.Message}";
                PhonemeText = string.Empty;
            }
        }
    }
}
