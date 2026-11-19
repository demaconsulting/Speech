using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Proofreader.Synthesis;

/// <summary>
///     Loads synthesis engines on demand and serializes synthesis per engine.
/// </summary>
/// <remarks>
///     An engine admits one session at a time and binds its parameter values when it is loaded, so
///     the host keeps one engine per <see cref="ModelSelection.Key"/> (a settings change simply
///     loads another engine) guarded by a semaphore, and keeps at most <see cref="MaxEngines"/> of
///     them, evicting the least recently used. Failures never throw: a model that is not
///     installed, a missing native runtime, or a faulting sentence yields a failed
///     <see cref="SentenceAudio"/> with an explanation. Safe for concurrent callers.
/// </remarks>
internal sealed class SynthesisEngineHost : IAsyncDisposable
{
    /// <summary>The most engines kept loaded at once (A and B plus one recently used).</summary>
    private const int MaxEngines = 3;

    /// <summary>The catalog whose store the engines load models from.</summary>
    private readonly SpeechModelCatalog _catalog;

    /// <summary>Guards <see cref="_entries"/> and <see cref="_disposed"/>.</summary>
    private readonly object _sync = new();

    /// <summary>The loaded (or loading) engines by selection key.</summary>
    private readonly Dictionary<string, Task<Entry>> _entries = new(StringComparer.Ordinal);

    /// <summary>Set once <see cref="DisposeAsync"/> has started.</summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SynthesisEngineHost"/> class.
    /// </summary>
    /// <param name="catalog">The catalog to load models from. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is <see langword="null"/>.</exception>
    public SynthesisEngineHost(SpeechModelCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    /// <summary>
    ///     Synthesizes one sentence, loading the engine first when needed.
    /// </summary>
    /// <param name="model">The real (undecorated) model.</param>
    /// <param name="selection">The model and parameter values to synthesize with.</param>
    /// <param name="text">The sentence text.</param>
    /// <returns>The audio and the backend input, or a failed result with the reason. Never throws for synthesis problems.</returns>
    public async Task<SentenceAudio> SynthesizeAsync(ISynthesisModel model, ModelSelection selection, string text)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(text);

        var entry = await GetEntryAsync(model, selection).ConfigureAwait(false);
        entry.LastUsedTicks = Environment.TickCount64;
        if (entry.UnavailableReason is not null)
        {
            // An unusable engine is forgotten so a later request (for example after the user
            // installs the model) tries again instead of replaying the failure.
            Forget(selection.Key, entry);
            return SentenceAudio.Failed(entry.UnavailableReason);
        }

        await entry.Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (entry.Disposed)
            {
                return SentenceAudio.Failed("The engine for this model was unloaded to make room for other models. Play the sentence again.");
            }

            entry.Recorder!.BeginCapture();
            var speech = await entry.Engine!.SynthesizeAsync(text).ConfigureAwait(false);
            return new SentenceAudio(speech, entry.Recorder.EndCapture());
        }
        catch (Exception ex)
        {
            // Intentionally broad: synthesis crosses the native runtime boundary, and a fault in
            // one sentence must become a marked sentence, not a crashed proofreading session.
            entry.Recorder!.EndCapture();
            return SentenceAudio.Failed($"Synthesis failed: {ex.Message}");
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        List<Task<Entry>> entries;
        lock (_sync)
        {
            _disposed = true;
            entries = [.. _entries.Values];
            _entries.Clear();
        }

        foreach (var task in entries)
        {
            await DisposeEntryAsync(task).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Gets (or starts loading) the engine entry for a selection.
    /// </summary>
    /// <param name="model">The real model.</param>
    /// <param name="selection">The selection.</param>
    /// <returns>The entry, which carries an unavailable reason when the engine could not be loaded.</returns>
    private Task<Entry> GetEntryAsync(ISynthesisModel model, ModelSelection selection)
    {
        List<Task<Entry>> evicted = [];
        Task<Entry> task;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_entries.TryGetValue(selection.Key, out var existing))
            {
                existing = LoadEntryAsync(model, selection);
                _entries[selection.Key] = existing;
            }

            task = existing;

            // Evict the least recently used engines beyond the limit. An engine that is still
            // loading counts as the newest, so a load in flight is never evicted.
            while (_entries.Count > MaxEngines)
            {
                var oldest = _entries
                    .Where(pair => pair.Key != selection.Key)
                    .OrderBy(pair => pair.Value.IsCompletedSuccessfully ? pair.Value.Result.LastUsedTicks : long.MaxValue)
                    .First();
                _entries.Remove(oldest.Key);
                evicted.Add(oldest.Value);
            }
        }

        foreach (var old in evicted)
        {
            _ = DisposeEntryAsync(old);
        }

        return task;
    }

    /// <summary>
    ///     Removes a failed entry so the next request retries.
    /// </summary>
    /// <param name="key">The selection key.</param>
    /// <param name="entry">The entry that failed to load.</param>
    private void Forget(string key, Entry entry)
    {
        lock (_sync)
        {
            if (_entries.TryGetValue(key, out var task) && task.IsCompletedSuccessfully && ReferenceEquals(task.Result, entry))
            {
                _entries.Remove(key);
            }
        }
    }

    /// <summary>
    ///     Loads an engine, reporting every failure as an unavailable entry instead of throwing.
    /// </summary>
    /// <param name="model">The real model.</param>
    /// <param name="selection">The selection.</param>
    /// <returns>A usable entry, or one carrying the reason the engine is unavailable.</returns>
    private async Task<Entry> LoadEntryAsync(ISynthesisModel model, ModelSelection selection)
    {
        // A model that is not in the store is the most common reason for silence: say so plainly.
        var state = _catalog.GetState(model.Id);
        if (state != SpeechModelState.Downloaded)
        {
            return Entry.Unavailable(
                $"'{model.DisplayName}' is not installed (state: {state}). Install it with the Demo or the ModelDownloader tool.");
        }

        var diagnostics = new LastProblemDiagnostics();
        var recorder = new RecordingSynthesisModel(model);
        try
        {
            // Loading touches the native runtime; it is awaited off the caller's context.
            var engine = await SpeechSynthesizerFactory.LoadAsync(recorder, _catalog, diagnostics, selection.Parameters)
                .ConfigureAwait(false);
            if (!engine.IsAvailable)
            {
                await engine.DisposeAsync().ConfigureAwait(false);
                return Entry.Unavailable(
                    $"'{model.DisplayName}' could not be loaded: {diagnostics.LastProblem ?? "the engine is unavailable (is the native runtime installed?)."}");
            }

            return new Entry(engine, recorder);
        }
        catch (Exception ex)
        {
            // Intentionally broad: an invalid parameter value or a native load fault must become
            // an explained, retryable error rather than an unobserved task fault.
            return Entry.Unavailable($"'{model.DisplayName}' could not be loaded: {ex.Message}");
        }
    }

    /// <summary>
    ///     Disposes one engine once any synthesis running on it has finished.
    /// </summary>
    /// <param name="task">The entry task to dispose.</param>
    /// <returns>A task that completes when the engine has been disposed; it never faults.</returns>
    private static async Task DisposeEntryAsync(Task<Entry> task)
    {
        try
        {
            var entry = await task.ConfigureAwait(false);
            if (entry.Engine is null)
            {
                return;
            }

            await entry.Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                entry.Disposed = true;
                await entry.Engine.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                // Released so a caller already waiting on the gate sees Disposed instead of waiting forever.
                entry.Gate.Release();
            }
        }
        catch (Exception)
        {
            // Intentionally broad: disposal is best effort and must never fault the caller or
            // leave a background task unobserved.
        }
    }

    /// <summary>
    ///     A loaded engine with the gate that serializes its use, or the reason it is unavailable.
    /// </summary>
    private sealed class Entry
    {
        /// <summary>
        ///     Initializes a usable entry.
        /// </summary>
        /// <param name="engine">The loaded engine.</param>
        /// <param name="recorder">The decorated model the engine was loaded with.</param>
        public Entry(ISpeechSynthesizerEngine engine, RecordingSynthesisModel recorder)
        {
            Engine = engine;
            Recorder = recorder;
        }

        /// <summary>
        ///     Initializes an unavailable entry.
        /// </summary>
        /// <param name="reason">Why there is no engine.</param>
        private Entry(string reason)
        {
            UnavailableReason = reason;
        }

        /// <summary>Gets the engine, or <see langword="null"/> when unavailable.</summary>
        public ISpeechSynthesizerEngine? Engine { get; }

        /// <summary>Gets the recording model, or <see langword="null"/> when unavailable.</summary>
        public RecordingSynthesisModel? Recorder { get; }

        /// <summary>Gets the reason there is no engine, or <see langword="null"/> when usable.</summary>
        public string? UnavailableReason { get; }

        /// <summary>Gets the gate that admits one synthesis at a time.</summary>
        public SemaphoreSlim Gate { get; } = new(1, 1);

        /// <summary>
        ///     Gets or sets a value indicating whether the engine has been disposed. Read and written
        ///     only while holding <see cref="Gate"/>.
        /// </summary>
        public bool Disposed { get; set; }

        /// <summary>Gets or sets the tick count of the last request, for least-recently-used eviction.</summary>
        public long LastUsedTicks { get; set; } = Environment.TickCount64;

        /// <summary>
        ///     Creates an unavailable entry.
        /// </summary>
        /// <param name="reason">Why there is no engine.</param>
        /// <returns>The entry.</returns>
        public static Entry Unavailable(string reason) => new(reason);
    }

    /// <summary>
    ///     Remembers the most recent warning or error the library reported while loading, so the
    ///     reason an engine is unavailable can be shown to the user.
    /// </summary>
    private sealed class LastProblemDiagnostics : ISpeechDiagnostics
    {
        /// <summary>Gets the last warning or error message, or <see langword="null"/> when none was reported.</summary>
        public string? LastProblem { get; private set; }

        /// <inheritdoc/>
        public void Report(SpeechDiagnosticLevel level, string category, string message)
        {
            if (level >= SpeechDiagnosticLevel.Warning)
            {
                LastProblem = message;
            }
        }
    }
}
