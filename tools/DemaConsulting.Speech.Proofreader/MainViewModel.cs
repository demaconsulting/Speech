using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;
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
///     paused, jumping to a sentence restarts playback there only while it is actually playing; while
///     paused or stopped a jump (click or key) only selects, and Space plays from the selection.
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

    /// <summary>Gets the most recently started diagnostics request (it never faults); tests await it.</summary>
    internal Task DiagnosticsTask { get; private set; } = Task.CompletedTask;

    /// <summary>
    ///     The audio that was recently heard, oldest first, or empty before any (and after a new text is loaded).
    /// </summary>
    /// <remarks>
    ///     Captured from the <see cref="PlaybackTarget"/> the audio was synthesized for, never from the
    ///     slot's current user-interface state, so a flag always describes what the listener heard. Several
    ///     are kept (not only the last), so a flag for the sentence just before the current one still
    ///     describes the audio that was heard for it.
    /// </remarks>
    private readonly List<HeardSnapshot> _heard = [];

    /// <summary>How many heard snapshots are kept.</summary>
    private const int HeardHistoryLimit = 8;

    /// <summary>The settings file, or <see langword="null"/> when nothing is remembered between launches.</summary>
    private readonly UserSettingsStore? _settingsStore;

    /// <summary>The clock used to tell a flag keypress that follows a sentence closely.</summary>
    private readonly TimeProvider _time;

    /// <summary>The sentence (and slot) that is sounding now, or <see langword="null"/>; stays set while paused mid-sentence.</summary>
    private (int Index, string Slot, DateTimeOffset Since)? _audible;

    /// <summary>The sentence (and slot) that last finished sounding, or <see langword="null"/>.</summary>
    private (int Index, string Slot, DateTimeOffset At)? _finished;

    /// <summary>The category of the last flag saved (also remembered across launches), or <see langword="null"/>.</summary>
    private FlagCategory? _lastCategory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    /// <param name="catalog">The model catalog.</param>
    /// <param name="engineHost">The synthesis engine host.</param>
    /// <param name="cache">The synthesis cache over <paramref name="engineHost"/>.</param>
    /// <param name="player">The audio player.</param>
    /// <param name="splitter">The sentence splitter.</param>
    /// <param name="diagnostics">The diagnostics builder.</param>
    /// <param name="settingsStore">
    ///     The per-user settings file that remembers the models, their settings, and the last flag
    ///     category across launches, or <see langword="null"/> to remember nothing.
    /// </param>
    /// <param name="timeProvider">The clock, or <see langword="null"/> for the system clock.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
    public MainViewModel(
        ProofreaderCatalog catalog,
        SynthesisEngineHost engineHost,
        SentenceAudioCache cache,
        SentencePlayer player,
        SentenceSplitter splitter,
        DiagnosticsBuilder diagnostics,
        UserSettingsStore? settingsStore = null,
        TimeProvider? timeProvider = null)
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
        _settingsStore = settingsStore;
        _time = timeProvider ?? TimeProvider.System;
        _controller = new PlaybackController(this, cache, player);

        // Defaults first (Piper ONNX for A, Kokoro ONNX for B when installed), then what was used last.
        RefreshModels();
        if (_settingsStore is not null)
        {
            var saved = _settingsStore.Load();
            // A remembered model that is no longer installed is skipped (the defaults stand), unless
            // nothing at all is installed, when the remembered choice is the most useful thing to show.
            var requireInstalled = SlotA.Models.Any(choice => choice.IsInstalled);
            SlotA.Restore(saved.SlotA, requireInstalled);
            SlotB.Restore(saved.SlotB, requireInstalled);
            AbMode = saved.AbMode;
            _lastCategory = saved.LastCategory;
        }

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
    void IPlaybackHost.OnStateChanged(PlaybackState state)
    {
        State = state;

        // Nothing sounds once playback has stopped; a pause keeps the sentence that was interrupted.
        if (state == PlaybackState.Stopped)
        {
            _audible = null;
        }
    }

    /// <inheritdoc/>
    void IPlaybackHost.OnSentenceAudible(int index, string slotName) => _audible = (index, slotName, _time.GetUtcNow());

    /// <inheritdoc/>
    void IPlaybackHost.OnSentenceFinished(int index, string slotName)
    {
        _finished = (index, slotName, _time.GetUtcNow());
        _audible = null;
    }

    /// <inheritdoc/>
    void IPlaybackHost.OnSentenceStarted(int index, string slotName)
    {
        ActiveSlotName = slotName;
        SetCurrent(index);
        RefreshDiagnostics(index, slotName, null);
    }

    /// <inheritdoc/>
    void IPlaybackHost.OnAudioReady(int index, PlaybackTarget target, SentenceAudio audio)
    {
        if (target.Model is null || target.Selection is null || index < 0 || index >= _sentences.Count)
        {
            return;
        }

        // The model, settings, and slot of the target are what will be heard; later changes to the
        // slot's controls must not leak into a flag raised for this audio.
        var text = _sentences[index].Text;
        var heard = new HeardSnapshot(index, text, target.Name, target.Selection, null);
        _heard.Add(heard);
        if (_heard.Count > HeardHistoryLimit)
        {
            _heard.RemoveAt(0);
        }
        var version = ++_diagnosticsVersion;
        DiagnosticsTask = ApplyDiagnosticsAsync(target.Model, text, audio, version, heard);
    }

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
            SaveSettings();
            _controller.TogglePlayPause(Math.Max(CurrentIndex, 0));
        }
    }

    /// <summary>
    ///     Stops playback.
    /// </summary>
    [RelayCommand]
    internal void Stop()
    {
        _controller.Stop();
        ClearPlaybackMarks();
    }

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
            SaveSettings();
            ClearPlaybackMarks();
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
        SlotA.SetModels(descriptors, 0, OnnxPiperLibriTtsEnSynthesisModel.ModelId);
        SlotB.SetModels(descriptors, 1, OnnxKokoroEnglishSynthesisModel.ModelId);
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
        await LoadTextAsync(text, Path.GetFileName(path), isMarkdown, Path.GetDirectoryName(Path.GetFullPath(path)));
    }

    /// <summary>
    ///     Splits text into sentences and makes it the loaded text, discarding the flags of any previous text.
    /// </summary>
    /// <param name="text">The text (or Markdown) to load.</param>
    /// <param name="name">The name to show for the source.</param>
    /// <param name="isMarkdown">Whether <paramref name="text"/> is Markdown to be stripped.</param>
    /// <param name="sourceFolder">The folder the text came from, or <see langword="null"/> for pasted text.</param>
    /// <returns>A task that completes when the text is loaded (or the problem has been reported).</returns>
    internal async Task LoadTextAsync(string text, string name, bool isMarkdown, string? sourceFolder = null)
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
            SourceFolder = sourceFolder;
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
    ///     Whether playback must start there regardless of the state. Without it, playback that is
    ///     running (or loading) continues from the target, while playback that is paused or stopped
    ///     only selects: a paused run is stopped so Space plays from the selection, and nothing starts
    ///     by itself. Clicks and keyboard navigation both use this.
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

        // An explicit move overrides what was just heard: the next flag is for what the user chose.
        ClearPlaybackMarks();
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
        SaveSettings();
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
        // An editor left open would otherwise keep a draft of text that is no longer loaded.
        CancelFlag();

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
        _heard.Clear();
        ClearPlaybackMarks();
        if (sentences.Count > 0)
        {
            RefreshDiagnostics(0, ActiveSlotName, null);
        }
    }

    /// <summary>
    ///     Forgets which sentence was sounding or had just finished.
    /// </summary>
    private void ClearPlaybackMarks()
    {
        _audible = null;
        _finished = null;
    }

    /// <summary>
    ///     Remembers the models, their settings, A/B mode, and the last flag category for the next launch.
    /// </summary>
    private void SaveSettings() =>
        _settingsStore?.Save(new UserSettings
        {
            SlotA = SlotA.CurrentSelection is { } a ? SessionModel.From(a) : null,
            SlotB = SlotB.CurrentSelection is { } b ? SessionModel.From(b) : null,
            AbMode = AbMode,
            LastCategory = _lastCategory,
        });

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
    ///     Reacts to a selection made by the user in the sentence list: a click selects, and
    ///     continues playing from there only when playback is running.
    /// </summary>
    /// <param name="value">The new current index.</param>
    partial void OnCurrentIndexChanged(int value)
    {
        CurrentSentenceText = value >= 0 && value < _sentences.Count ? _sentences[value].Text : string.Empty;
        if (!_suppressJump && value >= 0)
        {
            JumpTo(value, play: false);
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
        DiagnosticsTask = ApplyDiagnosticsAsync(choice.Model, text, audio, version, null);
    }

    /// <summary>
    ///     Builds diagnostics off the UI thread and shows them unless a newer request has superseded them.
    /// </summary>
    /// <param name="model">The real model.</param>
    /// <param name="text">The sentence text.</param>
    /// <param name="audio">The sentence audio, or <see langword="null"/>.</param>
    /// <param name="version">The request number.</param>
    /// <param name="heard">
    ///     The heard snapshot these diagnostics complete, or <see langword="null"/> when they are only shown.
    /// </param>
    /// <returns>A task that never faults.</returns>
    private async Task ApplyDiagnosticsAsync(
        ISynthesisModel model,
        string text,
        SentenceAudio? audio,
        int version,
        HeardSnapshot? heard)
    {
        try
        {
            var result = await Task.Run(() => _diagnostics.Build(model, text, audio));

            // The heard snapshot is completed even when the panes have since moved on, but only
            // while it is still remembered.
            if (heard is not null)
            {
                var position = _heard.FindIndex(item => ReferenceEquals(item, heard));
                if (position >= 0)
                {
                    _heard[position] = heard with { Diagnostics = result };
                }
            }

            if (version != _diagnosticsVersion)
            {
                return;
            }

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
