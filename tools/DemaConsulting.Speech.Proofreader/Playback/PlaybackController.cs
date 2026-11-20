using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Proofreader.Synthesis;

namespace DemaConsulting.Speech.Proofreader.Playback;

/// <summary>
///     What the playback controller is doing.
/// </summary>
internal enum PlaybackState
{
    /// <summary>Nothing is playing.</summary>
    Stopped,

    /// <summary>A sentence is being synthesized (or prepared) before it can be heard.</summary>
    Loading,

    /// <summary>A sentence is being heard.</summary>
    Playing,

    /// <summary>Playback is paused; resuming continues mid-sentence.</summary>
    Paused,
}

/// <summary>
///     One audible configuration of a sentence: slot A or slot B.
/// </summary>
/// <param name="Name">The slot name, <c>A</c> or <c>B</c>.</param>
/// <param name="Model">The real model, or <see langword="null"/> when the slot has no usable model.</param>
/// <param name="Selection">The model and settings, or <see langword="null"/> when the slot has no usable model.</param>
/// <param name="Problem">Why the slot cannot play (no model chosen, model not installed), or <see langword="null"/>.</param>
internal sealed record PlaybackTarget(string Name, ISynthesisModel? Model, ModelSelection? Selection, string? Problem);

/// <summary>
///     What the playback controller needs from, and reports to, the application.
/// </summary>
/// <remarks>
///     A narrow seam so the controller is independent of the view model and Avalonia. All members
///     are called on the UI thread.
/// </remarks>
internal interface IPlaybackHost
{
    /// <summary>Gets the number of sentences.</summary>
    int SentenceCount { get; }

    /// <summary>
    ///     Gets the text of a sentence.
    /// </summary>
    /// <param name="index">The zero-based sentence index.</param>
    /// <returns>The sentence text.</returns>
    string GetSentenceText(int index);

    /// <summary>
    ///     Gets the slots a sentence is played through now: A, or A then B in A/B mode.
    /// </summary>
    /// <returns>The targets, read afresh for every sentence so a settings change applies immediately.</returns>
    IReadOnlyList<PlaybackTarget> GetTargets();

    /// <summary>
    ///     Reports a change of <see cref="PlaybackState"/>.
    /// </summary>
    /// <param name="state">The new state.</param>
    void OnStateChanged(PlaybackState state);

    /// <summary>
    ///     Reports that a sentence (through a slot) has become the current one.
    /// </summary>
    /// <param name="index">The sentence index.</param>
    /// <param name="slotName">The slot name.</param>
    void OnSentenceStarted(int index, string slotName);

    /// <summary>
    ///     Reports that a sentence's audio is ready, with what the backend consumed.
    /// </summary>
    /// <param name="index">The sentence index.</param>
    /// <param name="target">The slot the audio belongs to.</param>
    /// <param name="audio">The audio and recorded backend input.</param>
    void OnAudioReady(int index, PlaybackTarget target, SentenceAudio audio);

    /// <summary>
    ///     Reports that a sentence has started to be heard: its samples are being played now, never
    ///     merely because its audio is ready or a run reached it.
    /// </summary>
    /// <param name="index">The sentence index.</param>
    /// <param name="slotName">The slot name.</param>
    void OnSentenceAudible(int index, string slotName);

    /// <summary>
    ///     Reports that a sentence's audio has been heard to its end (never for one that was canceled).
    /// </summary>
    /// <param name="index">The sentence index.</param>
    /// <param name="slotName">The slot name.</param>
    void OnSentenceFinished(int index, string slotName);

    /// <summary>
    ///     Reports a message for the status banner.
    /// </summary>
    /// <param name="message">The message, or <see langword="null"/> to clear it.</param>
    /// <param name="isError">Whether the message describes a problem.</param>
    void OnMessage(string? message, bool isError);
}

/// <summary>
///     Plays sentences in order from a starting point, with lookahead synthesis, A/B sequencing,
///     pause, resume, jump, and stop.
/// </summary>
/// <remarks>
///     One run plays consecutive sentences; starting a new run cancels the previous one, which is
///     how jump, replay, previous, and next work. Everything runs on the UI thread's context (only
///     synthesis and resampling use the thread pool), so the state needs no locking. A synthesis or
///     device problem ends the run with an explanatory message rather than skipping silently.
/// </remarks>
internal sealed class PlaybackController
{
    /// <summary>How many sentences ahead of the current one are synthesized.</summary>
    private const int LookAhead = 2;

    /// <summary>The silence between the A and B renditions of a sentence.</summary>
    private static readonly TimeSpan AbGap = TimeSpan.FromMilliseconds(350);

    /// <summary>Time given to the last block to sound before the device is stopped at the end of a run.</summary>
    private static readonly TimeSpan TailDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>The application seam.</summary>
    private readonly IPlaybackHost _host;

    /// <summary>The synthesis cache (and the prefetch mechanism).</summary>
    private readonly SentenceAudioCache _cache;

    /// <summary>The audio player.</summary>
    private readonly SentencePlayer _player;

    /// <summary>The cancellation source of the current run, or <see langword="null"/> before the first.</summary>
    private CancellationTokenSource? _runCancellation;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PlaybackController"/> class.
    /// </summary>
    /// <param name="host">The application seam. Must not be <see langword="null"/>.</param>
    /// <param name="cache">The synthesis cache. Must not be <see langword="null"/>.</param>
    /// <param name="player">The audio player. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public PlaybackController(IPlaybackHost host, SentenceAudioCache cache, SentencePlayer player)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(player);

        _host = host;
        _cache = cache;
        _player = player;
    }

    /// <summary>Gets the current state.</summary>
    public PlaybackState State { get; private set; } = PlaybackState.Stopped;

    /// <summary>
    ///     Starts (or restarts) playing from a sentence, canceling any run in progress.
    /// </summary>
    /// <param name="index">The sentence to start at.</param>
    public void PlayFrom(int index)
    {
        _runCancellation?.Cancel();
        _player.Halt();

        var cancellation = new CancellationTokenSource();
        _runCancellation = cancellation;

        // The run never faults (it reports every problem through the host), so it is not retained.
        _ = RunAsync(index, cancellation);
    }

    /// <summary>
    ///     Plays, pauses, or resumes depending on the state.
    /// </summary>
    /// <param name="currentIndex">The sentence to start at when nothing is playing.</param>
    public void TogglePlayPause(int currentIndex)
    {
        switch (State)
        {
            case PlaybackState.Playing:
            case PlaybackState.Loading:
                Pause();
                break;
            case PlaybackState.Paused:
                _player.Resume();
                SetState(_player.IsPlaying ? PlaybackState.Playing : PlaybackState.Loading);
                break;
            default:
                PlayFrom(currentIndex);
                break;
        }
    }

    /// <summary>
    ///     Pauses when something is playing or loading; does nothing otherwise.
    /// </summary>
    public void Pause()
    {
        if (State is PlaybackState.Playing or PlaybackState.Loading)
        {
            _player.Pause();
            SetState(PlaybackState.Paused);
        }
    }

    /// <summary>
    ///     Stops playback and silences the device.
    /// </summary>
    public void Stop()
    {
        _runCancellation?.Cancel();
        _player.Halt();
        SetState(PlaybackState.Stopped);
    }

    /// <summary>
    ///     Plays the sentences of one run.
    /// </summary>
    /// <param name="start">The first sentence.</param>
    /// <param name="cancellation">The run's cancellation source; the run is the latest while it is still the controller's.</param>
    /// <returns>A task that completes when the run ends. It never faults.</returns>
    private async Task RunAsync(int start, CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;
        SetState(PlaybackState.Loading);
        _host.OnMessage(null, false);
        try
        {
            for (var index = start; index < _host.SentenceCount; index++)
            {
                if (!await PlaySentenceAsync(index, token))
                {
                    return;
                }
            }

            // Let the last block sound before the stream is stopped in the finally block.
            await Task.Delay(TailDelay, token);
        }
        catch (OperationCanceledException)
        {
            // A newer run or Stop replaced this one; the replacement owns the player and the state.
        }
        catch (AudioDeviceUnavailableException ex)
        {
            _host.OnMessage($"Playback device problem: {ex.Message}", true);
        }
        catch (Exception ex)
        {
            // Intentionally broad: the run is a fire-and-forget task, so any fault must surface as
            // a message instead of an unobserved exception that would silently end playback.
            _host.OnMessage($"Playback stopped: {ex.Message}", true);
        }
        finally
        {
            // Only the latest run may reset the player and the state; a superseded run must not
            // silence the run that replaced it.
            if (ReferenceEquals(_runCancellation, cancellation))
            {
                _player.Halt();
                SetState(PlaybackState.Stopped);
            }
        }
    }

    /// <summary>
    ///     Plays one sentence through every active slot.
    /// </summary>
    /// <param name="index">The sentence index.</param>
    /// <param name="token">Cancels the run.</param>
    /// <returns><see langword="true"/> to continue with the next sentence; <see langword="false"/> when the run must end.</returns>
    private async Task<bool> PlaySentenceAsync(int index, CancellationToken token)
    {
        var text = _host.GetSentenceText(index);
        var targets = _host.GetTargets();

        // Request every slot's audio first, so in A/B mode both engines synthesize in parallel.
        var requests = targets
            .Select(target => target.Model is null || target.Selection is null
                ? null
                : _cache.GetAsync(target.Model, target.Selection, text))
            .ToList();

        for (var slot = 0; slot < targets.Count; slot++)
        {
            var target = targets[slot];

            // A pause that lands between sentences (or between the A and B renditions) holds here,
            // so the current sentence stays the one that was just heard until playback resumes.
            await _player.WaitWhilePausedAsync(token);
            _host.OnSentenceStarted(index, target.Name);

            // A slot without a usable model ends the run with the reason.
            if (requests[slot] is not { } request)
            {
                _host.OnMessage(target.Problem ?? $"Slot {target.Name} has no model.", true);
                return false;
            }

            var audio = await request.WaitAsync(token);
            if (!audio.IsSuccess)
            {
                _host.OnMessage($"Sentence {index + 1} (slot {target.Name}): {audio.Error}", true);
                return false;
            }

            _host.OnAudioReady(index, target, audio);
            PrefetchAhead(index, targets);

            // Resampling is CPU work, so it runs off the UI thread; the player then streams the
            // prepared audio from the UI thread's context.
            var prepared = await Task.Run(() => _player.Prepare(audio), token);
            if (State == PlaybackState.Loading)
            {
                SetState(PlaybackState.Playing);
            }

            // The sentence is audible only when its samples actually start playing (after any pause),
            // not when its audio is merely ready. The player raises the callback just before the first
            // block is written, for every buffer that has samples; a buffer with none (silence only, or
            // nothing) is never heard, so it is neither audible nor finished.
            var announced = false;
            await _player.PlayAsync(
                prepared,
                token,
                () =>
                {
                    announced = true;
                    _host.OnSentenceAudible(index, target.Name);
                });
            if (announced)
            {
                _host.OnSentenceFinished(index, target.Name);
            }

            if (slot < targets.Count - 1)
            {
                await Task.Delay(AbGap, token);
            }
        }

        return true;
    }

    /// <summary>
    ///     Starts synthesizing (and preparing) the sentences after <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The sentence now playing.</param>
    /// <param name="targets">The active slots.</param>
    private void PrefetchAhead(int index, IReadOnlyList<PlaybackTarget> targets)
    {
        for (var ahead = 1; ahead <= LookAhead; ahead++)
        {
            if (index + ahead >= _host.SentenceCount)
            {
                break;
            }

            var text = _host.GetSentenceText(index + ahead);
            foreach (var target in targets.Where(t => t.Model is not null && t.Selection is not null))
            {
                _ = PrepareWhenReadyAsync(_cache.GetAsync(target.Model!, target.Selection!, text));
            }
        }
    }

    /// <summary>
    ///     Resamples a prefetched sentence as soon as its synthesis completes, so playing it later is instant.
    /// </summary>
    /// <param name="request">The synthesis request.</param>
    /// <returns>A task that never faults.</returns>
    private async Task PrepareWhenReadyAsync(Task<SentenceAudio> request)
    {
        try
        {
            var audio = await request.ConfigureAwait(false);
            if (audio.IsSuccess)
            {
                _player.Prepare(audio);
            }
        }
        catch (Exception)
        {
            // Intentionally broad: this is an optimization only. Any failure (no device yet, a
            // disposed host at shutdown) resurfaces, with a message, when the sentence is played.
        }
    }

    /// <summary>
    ///     Records and reports a state change.
    /// </summary>
    /// <param name="state">The new state.</param>
    private void SetState(PlaybackState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        _host.OnStateChanged(state);
    }
}
