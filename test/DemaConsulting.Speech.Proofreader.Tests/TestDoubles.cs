using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Proofreader.Tests;

/// <summary>
///     A clock that only moves when told to.
/// </summary>
internal sealed class ManualTime : TimeProvider
{
    /// <summary>The current time.</summary>
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>
    ///     Moves the clock forward.
    /// </summary>
    /// <param name="by">How far.</param>
    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>
///     A playback device that renders nothing and never drains by itself: queued audio stays pending, so a
///     sentence keeps "playing" until <see cref="Drain"/> is called. It records what was written.
/// </summary>
internal sealed class FakePlaybackDevice : IAudioPlaybackDevice
{
    /// <summary>The samples queued and not yet drained.</summary>
    private long _pending;

    /// <summary>The number of blocks written.</summary>
    private int _writes;

    /// <summary>The first sample of the last block written.</summary>
    private float _last;

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    public int ChannelCount => 1;

    /// <inheritdoc/>
    public int SampleRate => 16000;

    /// <inheritdoc/>
    public long PendingSampleCount => Interlocked.Read(ref _pending);

    /// <summary>Gets the number of blocks written so far.</summary>
    public int WriteCount => Volatile.Read(ref _writes);

    /// <summary>Gets the first sample of the last block written (identifies the sentence that was played).</summary>
    public float LastWritten => Volatile.Read(ref _last);

    /// <inheritdoc/>
    public void Start()
    {
    }

    /// <inheritdoc/>
    public void Stop() => Interlocked.Exchange(ref _pending, 0);

    /// <inheritdoc/>
    public void Write(IReadOnlyList<float> samples)
    {
        Volatile.Write(ref _last, samples[0]);
        Interlocked.Add(ref _pending, samples.Count);
        Interlocked.Increment(ref _writes);
    }

    /// <summary>
    ///     Lets everything queued so far finish sounding.
    /// </summary>
    public void Drain() => Interlocked.Exchange(ref _pending, 0);
}
