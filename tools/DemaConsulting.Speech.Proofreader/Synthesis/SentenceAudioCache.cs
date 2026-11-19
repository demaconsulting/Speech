using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Proofreader.Synthesis;

/// <summary>
///     Remembers synthesized sentences per sentence text, model, and settings, and doubles as the
///     prefetch mechanism.
/// </summary>
/// <remarks>
///     The cache stores the <see cref="Task"/> of each synthesis, so a request made while a
///     synthesis is already in flight (for example a prefetch the player catches up with) joins it
///     instead of starting a second one, and a request made ahead of time (a prefetch) is simply a
///     <see cref="GetAsync"/> whose result is not awaited yet. Failed results are not kept so a later
///     request retries. The cache is bounded and drops the oldest entry first; replaying a sentence
///     within the bound, or switching between the same two A/B configurations, is instant. Safe for
///     concurrent callers.
/// </remarks>
internal sealed class SentenceAudioCache
{
    /// <summary>Produces the audio of a cache miss (the engine host's synthesis).</summary>
    private readonly Func<ISynthesisModel, ModelSelection, string, Task<SentenceAudio>> _synthesize;

    /// <summary>The most entries kept; the oldest is dropped first.</summary>
    private readonly int _capacity;

    /// <summary>Guards <see cref="_entries"/> and <see cref="_order"/>.</summary>
    private readonly object _sync = new();

    /// <summary>The cached (or in-flight) syntheses by key.</summary>
    private readonly Dictionary<string, Task<SentenceAudio>> _entries = new(StringComparer.Ordinal);

    /// <summary>The entries (key and task) in insertion order, for eviction.</summary>
    private Queue<(string Key, Task<SentenceAudio> Task)> _order = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="SentenceAudioCache"/> class.
    /// </summary>
    /// <param name="host">The engine host that synthesizes cache misses. Must not be <see langword="null"/>.</param>
    /// <param name="capacity">The most entries to keep. Must be positive.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="host"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is not positive.</exception>
    public SentenceAudioCache(SynthesisEngineHost host, int capacity = 64)
        : this(CreateSynthesizer(host), capacity)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SentenceAudioCache"/> class over any synthesis
    ///     function, so cache behavior can be exercised without loading a real engine.
    /// </summary>
    /// <param name="synthesize">Produces the audio of a cache miss. Must not be <see langword="null"/>.</param>
    /// <param name="capacity">The most entries to keep. Must be positive.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="synthesize"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is not positive.</exception>
    internal SentenceAudioCache(Func<ISynthesisModel, ModelSelection, string, Task<SentenceAudio>> synthesize, int capacity = 64)
    {
        ArgumentNullException.ThrowIfNull(synthesize);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        _synthesize = synthesize;
        _capacity = capacity;
    }

    /// <summary>
    ///     Gets the audio for a sentence only when it is already synthesized, without starting anything.
    /// </summary>
    /// <param name="selection">The model and settings.</param>
    /// <param name="text">The sentence text.</param>
    /// <returns>The successful audio, or <see langword="null"/> when it is not (yet) available.</returns>
    public SentenceAudio? TryGetCompleted(ModelSelection selection, string text)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(text);

        lock (_sync)
        {
            return _entries.TryGetValue(selection.Key + '\n' + text, out var task)
                && task.IsCompletedSuccessfully
                && task.Result.IsSuccess
                    ? task.Result
                    : null;
        }
    }

    /// <summary>
    ///     Gets the audio for a sentence, starting its synthesis when it is neither cached nor in flight.
    /// </summary>
    /// <param name="model">The real model.</param>
    /// <param name="selection">The model and settings to synthesize with.</param>
    /// <param name="text">The sentence text.</param>
    /// <returns>
    ///     A task for the audio. It never faults for synthesis problems (they become a failed
    ///     result) and is not canceled by the caller: abandoning a wait leaves the synthesis running
    ///     so its result is cached for the next request.
    /// </returns>
    public Task<SentenceAudio> GetAsync(ISynthesisModel model, ModelSelection selection, string text)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(text);

        var key = selection.Key + '\n' + text;
        lock (_sync)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                return existing;
            }

            // Started on the thread pool so a cache miss never blocks the caller (the UI thread).
            var created = Task.Run(() => _synthesize(model, selection, text));
            _entries[key] = created;
            _order.Enqueue((key, created));

            // Evict the oldest entries beyond the bound. The queue holds exactly one occurrence per
            // entry (a failed entry is removed from both), and the task identity check keeps a stale
            // occurrence from evicting a newer entry stored under the same key.
            while (_order.Count > _capacity)
            {
                var (oldKey, oldTask) = _order.Dequeue();
                if (_entries.TryGetValue(oldKey, out var stored) && ReferenceEquals(stored, oldTask))
                {
                    _entries.Remove(oldKey);
                }
            }

            // A failed synthesis is forgotten so the next request retries it.
            _ = created.ContinueWith(
                task =>
                {
                    if (task.IsFaulted || (task.IsCompletedSuccessfully && !task.Result.IsSuccess))
                    {
                        lock (_sync)
                        {
                            if (_entries.TryGetValue(key, out var current) && ReferenceEquals(current, task))
                            {
                                _entries.Remove(key);
                            }

                            // Drop the queue occurrence too, so it neither counts against the bound
                            // nor evicts the retry's entry later.
                            _order = new Queue<(string Key, Task<SentenceAudio> Task)>(
                                _order.Where(item => !ReferenceEquals(item.Task, task)));
                        }
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            return created;
        }
    }

    /// <summary>
    ///     Adapts an engine host to the synthesis function.
    /// </summary>
    /// <param name="host">The engine host. Must not be <see langword="null"/>.</param>
    /// <returns>The function that synthesizes through <paramref name="host"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="host"/> is <see langword="null"/>.</exception>
    private static Func<ISynthesisModel, ModelSelection, string, Task<SentenceAudio>> CreateSynthesizer(SynthesisEngineHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return host.SynthesizeAsync;
    }
}
