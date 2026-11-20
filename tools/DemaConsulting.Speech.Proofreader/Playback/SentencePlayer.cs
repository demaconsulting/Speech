using System.Runtime.CompilerServices;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Proofreader.Synthesis;

namespace DemaConsulting.Speech.Proofreader.Playback;

/// <summary>
///     One sentence's audio converted to the playback device's rate and channel layout.
/// </summary>
/// <param name="Samples">The interleaved samples: pre-silence, resampled speech, and post-silence of every segment.</param>
/// <param name="SampleRate">The device sample rate the samples were prepared for.</param>
/// <param name="ChannelCount">The device channel count the samples were prepared for.</param>
internal sealed record PreparedAudio(float[] Samples, int SampleRate, int ChannelCount)
{
    /// <summary>Gets the number of frames (one sample per channel).</summary>
    public int FrameCount => Samples.Length / ChannelCount;
}

/// <summary>
///     Plays prepared sentence audio on a playback device with pause, resume, and an instant stop.
/// </summary>
/// <remarks>
///     The library's playback device is a fire-and-forget queue, so the player streams a sentence in
///     short blocks while keeping the queue shallow; that is what makes a pause or a jump take effect
///     within a fraction of a second. Pausing stops the device (which discards the queued samples)
///     and remembers how far the sentence had really been played; resuming restarts the device and
///     continues from there. All members except <see cref="Prepare"/> must be called on the UI
///     thread: playback state is deliberately unsynchronized because the playback loop itself runs
///     on the UI thread's context. <see cref="Prepare"/> is safe from any thread.
/// </remarks>
internal sealed class SentencePlayer
{
    /// <summary>The duration of one written block.</summary>
    private const int BlockMilliseconds = 50;

    /// <summary>How much audio is allowed to wait in the device queue before the loop pauses writing.</summary>
    private const int MaxQueuedMilliseconds = 250;

    /// <summary>How far a resume rewinds, because the device had already consumed (but not yet sounded) that much audio.</summary>
    private const int ResumeRewindMilliseconds = 80;

    /// <summary>The factory that enumerates devices and creates playback devices.</summary>
    private readonly AudioDeviceFactory _factory;

    /// <summary>Creates the playback device for a selection (the factory's, unless a test supplies its own).</summary>
    private readonly Func<AudioDeviceSelection, IAudioPlaybackDevice> _createDevice;

    /// <summary>Guards the device reference, which <see cref="Prepare"/> may touch from a worker thread.</summary>
    private readonly object _deviceSync = new();

    /// <summary>Prepared audio per sentence audio, so replays and A/B repeats do not resample again.</summary>
    private readonly ConditionalWeakTable<SentenceAudio, PreparedAudio> _prepared = [];

    /// <summary>The selected device name, or <see langword="null"/> for the system default.</summary>
    private string? _deviceName;

    /// <summary>The playback device, created lazily and re-created after a device change or refresh.</summary>
    private IAudioPlaybackDevice? _device;

    /// <summary>Whether the device stream is currently started.</summary>
    private bool _started;

    /// <summary>Whether a sentence is currently being played by <see cref="PlayAsync"/>.</summary>
    private bool _playing;

    /// <summary>Guards <see cref="_playing"/> and <see cref="_playGeneration"/> against overlapping runs.</summary>
    private readonly object _playSync = new();

    /// <summary>Identifies the most recent <see cref="PlayAsync"/> invocation; only it may clear <see cref="_playing"/>.</summary>
    private int _playGeneration;

    /// <summary>The next frame of the current sentence to write.</summary>
    private int _position;

    /// <summary>Completed by <see cref="Resume"/> or <see cref="Halt"/> to release a paused playback loop.</summary>
    private TaskCompletionSource _resume = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    ///     Initializes a new instance of the <see cref="SentencePlayer"/> class.
    /// </summary>
    /// <param name="factory">The device factory. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is <see langword="null"/>.</exception>
    public SentencePlayer(AudioDeviceFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _createDevice = selection => factory.CreatePlaybackDevice(selection);
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SentencePlayer"/> class that creates its playback
    ///     devices itself, so tests can play through a fake device.
    /// </summary>
    /// <param name="factory">The device factory used for enumeration and refresh. Must not be <see langword="null"/>.</param>
    /// <param name="createDevice">Creates the playback device for a selection. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    internal SentencePlayer(AudioDeviceFactory factory, Func<AudioDeviceSelection, IAudioPlaybackDevice> createDevice)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(createDevice);
        _factory = factory;
        _createDevice = createDevice;
    }

    /// <summary>Gets a value indicating whether playback is paused.</summary>
    public bool IsPaused { get; private set; }

    /// <summary>Gets a value indicating whether a sentence is being played (or is paused mid-sentence).</summary>
    public bool IsPlaying => _playing;

    /// <summary>
    ///     Lists the playback device names the audio backend reports.
    /// </summary>
    /// <returns>The names; empty when there is no audio backend or no output device (never throws).</returns>
    public IReadOnlyList<string> EnumerateDevices() =>
        _factory.PlaybackProbe.Enumerate().Select(device => device.Name).ToList();

    /// <summary>
    ///     Selects the playback device used from now on.
    /// </summary>
    /// <param name="deviceName">The device name, or <see langword="null"/> for the system default.</param>
    public void SelectDevice(string? deviceName)
    {
        Halt();
        lock (_deviceSync)
        {
            _deviceName = deviceName;
            _device = null;
        }
    }

    /// <summary>
    ///     Re-reads the audio backend's device table.
    /// </summary>
    /// <exception cref="AudioDeviceInUseException">Thrown when a stream is still active (the player halts first, so this is not expected).</exception>
    public void RefreshDevices()
    {
        Halt();
        _factory.RefreshDevices();
        lock (_deviceSync)
        {
            _device = null;
        }
    }

    /// <summary>
    ///     Converts sentence audio to the device's rate and channel layout (memoized per audio).
    /// </summary>
    /// <param name="audio">A successful sentence audio. Must not be <see langword="null"/>.</param>
    /// <returns>The prepared audio.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="audio"/> is <see langword="null"/>.</exception>
    /// <exception cref="AudioDeviceUnavailableException">Thrown when there is no usable playback device.</exception>
    public PreparedAudio Prepare(SentenceAudio audio)
    {
        ArgumentNullException.ThrowIfNull(audio);

        var device = GetDevice();
        if (_prepared.TryGetValue(audio, out var cached)
            && cached.SampleRate == device.SampleRate
            && cached.ChannelCount == device.ChannelCount)
        {
            return cached;
        }

        var rate = device.SampleRate;
        var channels = device.ChannelCount;
        var samples = new List<float>();

        // Each segment is silence, then the speech resampled to the device rate, then silence,
        // with every mono frame copied to every device channel.
        foreach (var speech in audio.Speech)
        {
            AppendSilence(samples, speech.PreSilence, rate, channels);
            if (speech.Samples.Count > 0)
            {
                var mono = speech.Samples is float[] array ? array : [.. speech.Samples];
                foreach (var value in AudioResampler.Resample(mono, speech.SampleRate, rate))
                {
                    for (var channel = 0; channel < channels; channel++)
                    {
                        samples.Add(value);
                    }
                }
            }

            AppendSilence(samples, speech.PostSilence, rate, channels);
        }

        var prepared = new PreparedAudio([.. samples], rate, channels);
        _prepared.AddOrUpdate(audio, prepared);
        return prepared;
    }

    /// <summary>
    ///     Plays prepared audio to its end, honoring pause and resume.
    /// </summary>
    /// <remarks>
    ///     Completes when the whole sentence has been heard. The device stream stays started between
    ///     sentences (so there is no start-up gap) until <see cref="Halt"/> is called.
    /// </remarks>
    /// <param name="audio">The audio from <see cref="Prepare"/>.</param>
    /// <param name="cancellationToken">Cancels playback; the caller then calls <see cref="Halt"/>.</param>
    /// <param name="onAudible">
    ///     Called once, just before the first block of samples is written to the device (after any pause
    ///     has been lifted), which is when the sentence actually starts to be heard; never called when the
    ///     audio has no samples or playback is canceled first.
    /// </param>
    /// <returns>A task that completes when playback has finished.</returns>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="AudioDeviceUnavailableException">Thrown when the device cannot be started.</exception>
    public async Task PlayAsync(PreparedAudio audio, CancellationToken cancellationToken, Action? onAudible = null)
    {
        ArgumentNullException.ThrowIfNull(audio);

        // A canceled request must not touch the position or the playing flag of the sentence now audible.
        cancellationToken.ThrowIfCancellationRequested();

        var device = GetDevice();
        var channels = audio.ChannelCount;
        var blockFrames = Math.Max(1, audio.SampleRate * BlockMilliseconds / 1000);
        var maxQueuedSamples = (long)audio.SampleRate * channels * MaxQueuedMilliseconds / 1000;
        var total = audio.FrameCount;

        // Each invocation takes a generation under the lock, so a canceled run that unwinds after a
        // newer run has started cannot clear the newer run's playing flag.
        int generation;
        lock (_playSync)
        {
            generation = ++_playGeneration;
            _position = 0;
            _playing = true;
        }

        var announce = onAudible;
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Paused: wait for Resume (or cancellation); the position was rewound by Pause.
                if (IsPaused)
                {
                    await _resume.Task.WaitAsync(cancellationToken);
                    continue;
                }

                // Everything is written: finish once the device has drained the queue.
                if (_position >= total)
                {
                    if (!_started || device.PendingSampleCount == 0)
                    {
                        break;
                    }

                    await Task.Delay(10, cancellationToken);
                    continue;
                }

                if (!_started)
                {
                    device.Start();
                    _started = true;
                }

                // Keep the queue shallow so pause and jump react quickly.
                if (device.PendingSampleCount > maxQueuedSamples)
                {
                    await Task.Delay(10, cancellationToken);
                    continue;
                }

                if (announce is not null)
                {
                    announce();
                    announce = null;
                }

                var frames = Math.Min(blockFrames, total - _position);
                device.Write(new ArraySegment<float>(audio.Samples, _position * channels, frames * channels));
                _position += frames;
            }
        }
        finally
        {
            lock (_playSync)
            {
                if (generation == _playGeneration)
                {
                    _playing = false;
                }
            }
        }
    }

    /// <summary>
    ///     Pauses playback immediately, remembering the position, or arms a pause for the next sentence.
    /// </summary>
    public void Pause()
    {
        if (IsPaused)
        {
            return;
        }

        IsPaused = true;
        _resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        if (_started && _device is { } device)
        {
            // What is still queued was not played; stopping discards it, so rewind to just before it.
            // Between sentences nothing is playing and the next PlayAsync resets the position anyway.
            if (_playing)
            {
                var pendingFrames = (int)(device.PendingSampleCount / Math.Max(1, device.ChannelCount));
                var rewind = device.SampleRate * ResumeRewindMilliseconds / 1000;
                _position = Math.Max(0, _position - pendingFrames - rewind);
            }

            StopDevice(device);
        }
    }

    /// <summary>
    ///     Completes at once when playback is not paused, otherwise when it is resumed or halted.
    /// </summary>
    /// <remarks>
    ///     Lets a run hold at a sentence boundary while paused, so the current sentence does not
    ///     advance to one that has not been heard.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when playback is no longer paused.</returns>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public async Task WaitWhilePausedAsync(CancellationToken cancellationToken)
    {
        while (IsPaused)
        {
            await _resume.Task.WaitAsync(cancellationToken);
        }
    }

    /// <summary>
    ///     Resumes a paused playback.
    /// </summary>
    public void Resume()
    {
        if (!IsPaused)
        {
            return;
        }

        IsPaused = false;
        _resume.TrySetResult();
    }

    /// <summary>
    ///     Stops playback at once, discarding queued audio and clearing a pending pause.
    /// </summary>
    /// <remarks>
    ///     The caller cancels the playback loop first; this only silences the device and resets the
    ///     pause state so the next playback starts clean. It never throws for a device that is
    ///     already gone.
    /// </remarks>
    public void Halt()
    {
        IsPaused = false;
        _resume.TrySetResult();
        if (_started && _device is { } device)
        {
            StopDevice(device);
        }

        _started = false;
    }

    /// <summary>
    ///     Gets the playback device, creating it on first use.
    /// </summary>
    /// <returns>The available device.</returns>
    /// <exception cref="AudioDeviceUnavailableException">Thrown when no playback device is available.</exception>
    private IAudioPlaybackDevice GetDevice()
    {
        lock (_deviceSync)
        {
            if (_device is { } existing)
            {
                return existing;
            }

            var selection = _deviceName is null ? AudioDeviceSelection.SystemDefault : new AudioDeviceSelection(_deviceName);
            var created = _createDevice(selection);
            if (!created.IsAvailable)
            {
                throw new AudioDeviceUnavailableException(
                    _deviceName is null
                        ? "No playback device is available. Connect an output device and press Refresh devices."
                        : $"The playback device '{_deviceName}' is not available. Choose another device or press Refresh devices.");
            }

            _device = created;
            return created;
        }
    }

    /// <summary>
    ///     Stops the device stream, treating a device that cannot be stopped as already stopped.
    /// </summary>
    /// <param name="device">The device to stop.</param>
    private void StopDevice(IAudioPlaybackDevice device)
    {
        _started = false;
        try
        {
            device.Stop();
        }
        catch (AudioDeviceUnavailableException)
        {
            // The device vanished; there is nothing left to stop, and the next play re-creates it.
            lock (_deviceSync)
            {
                _device = null;
            }
        }
    }

    /// <summary>
    ///     Appends interleaved silence.
    /// </summary>
    /// <param name="samples">The list to append to.</param>
    /// <param name="duration">The silence duration.</param>
    /// <param name="rate">The device sample rate.</param>
    /// <param name="channels">The device channel count.</param>
    private static void AppendSilence(List<float> samples, TimeSpan duration, int rate, int channels)
    {
        var count = (int)(duration.TotalSeconds * rate) * channels;
        for (var i = 0; i < count; i++)
        {
            samples.Add(0f);
        }
    }
}
