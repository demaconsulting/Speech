namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Push-to-talk <see cref="IAudioCaptureDevice"/> decorator that keeps its underlying capture
///     device running between <see cref="Stop"/> and the next <see cref="Start"/>, remembers the
///     most recent <see cref="PreRoll"/> of audio captured while stopped, and delivers that audio
///     as the first <see cref="FrameCaptured"/> block of the next run.
/// </summary>
/// <remarks>
///     <para>
///         Push-to-talk hosts start recognition when a button is pressed, but people start
///         speaking as they press it. Opening a capture stream at that moment clips the first
///         syllable - often the whole of a short answer such as "yes". This decorator closes that
///         gap without any change to the single-use recognition session contract: a host keeps
///         one instance for as long as the user may talk, and creates a new recognition session
///         over it for every press. Each session's <see cref="Start"/> receives the remembered
///         pre-roll first, followed by live audio, in capture order; each session's
///         <see cref="Stop"/> pauses delivery while the underlying device keeps capturing into the
///         pre-roll buffer.
///     </para>
///     <para>
///         The pre-roll buffer only ever holds audio that no subscriber received: it is filled
///         while this device is stopped (or started with no subscriber), and emptied when it is
///         delivered on <see cref="Start"/>. Audio already delivered to one session therefore
///         never reaches the next session a second time.
///     </para>
///     <para>
///         Because the microphone stays open while the host is idle, the host owns this instance
///         and must call <see cref="Dispose"/> to release the underlying device (for example when
///         the user picks a different microphone, or before a device refresh). A
///         <see cref="PreRoll"/> of <see cref="TimeSpan.Zero"/> disables the feature: <see cref="Stop"/>
///         then stops the underlying device immediately and this decorator is a plain
///         pass-through.
///     </para>
///     <para>
///         Thread safety: <see cref="Start"/>, <see cref="Stop"/>, and <see cref="Dispose"/> are
///         serialized internally and may be called from any thread. <see cref="FrameCaptured"/> is
///         raised on the underlying device's capture thread for live audio, and on the thread
///         calling <see cref="Start"/> for the pre-roll block; the two are ordered so the pre-roll
///         block is always raised before any live block of the same run. Handlers must return
///         quickly, exactly as for the underlying device.
///     </para>
/// </remarks>
public sealed class PreRollAudioCaptureDevice : IAudioCaptureDevice, IDisposable
{
    /// <summary>
    ///     The default pre-roll: 300 ms, long enough to cover the delay between a person starting
    ///     to speak and a push-to-talk button press registering, short enough not to pull in
    ///     unrelated speech from before the press.
    /// </summary>
    public static readonly TimeSpan DefaultPreRoll = TimeSpan.FromMilliseconds(300);

    /// <summary>
    ///     The longest supported pre-roll: 10 seconds. Bounds the buffer's memory (about 4 MB of
    ///     48 kHz stereo audio) against a configuration mistake.
    /// </summary>
    public static readonly TimeSpan MaxPreRoll = TimeSpan.FromSeconds(10);

    /// <summary>The wrapped capture device that actually captures audio.</summary>
    private readonly IAudioCaptureDevice _inner;

    /// <summary>Serializes <see cref="Start"/>, <see cref="Stop"/>, and <see cref="Dispose"/>.</summary>
    private readonly object _lifecycleLock = new();

    /// <summary>Guards changes to <see cref="_frameCaptured"/>.</summary>
    private readonly object _handlersLock = new();

    /// <summary>The subscribers of <see cref="FrameCaptured"/>; read with <see cref="Volatile"/> when raising.</summary>
    private EventHandler<AudioCaptureFrameEventArgs>? _frameCaptured;

    /// <summary>
    ///     Guards the delivery state and the pre-roll buffer, which the underlying device's capture
    ///     callback and <see cref="Start"/> both touch. Held while raising
    ///     <see cref="FrameCaptured"/> so the pre-roll block and live blocks cannot interleave.
    /// </summary>
    private readonly object _deliveryLock = new();

    /// <summary>
    ///     The circular pre-roll buffer of interleaved samples, sized to <see cref="PreRoll"/> at
    ///     the underlying device's format once that format is known; empty while unknown or when
    ///     the pre-roll is disabled.
    /// </summary>
    private float[] _ring = [];

    /// <summary>The index in <see cref="_ring"/> of the oldest buffered sample.</summary>
    private int _ringStart;

    /// <summary>The number of samples currently buffered in <see cref="_ring"/>.</summary>
    private int _ringCount;

    /// <summary>Whether this decorator is subscribed to, and has started, the underlying device.</summary>
    private bool _innerRunning;

    /// <summary>Whether captured audio is currently forwarded to subscribers (between Start and Stop).</summary>
    private bool _delivering;

    /// <summary>Whether <see cref="Dispose"/> has run.</summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PreRollAudioCaptureDevice"/> class over an
    ///     existing capture device.
    /// </summary>
    /// <param name="inner">
    ///     The capture device to wrap. Must not be <see langword="null"/>, and must be a live
    ///     device: a finite <see cref="WavFileAudioCaptureDevice"/> has no audio "before the
    ///     press" and must reach a recognition session unwrapped so the whole file is recognized
    ///     with backpressure. This decorator takes over the device's Start/Stop lifecycle; callers
    ///     must not start or stop it directly afterwards.
    /// </param>
    /// <param name="preRoll">
    ///     How much of the most recent audio captured while stopped to deliver on the next
    ///     <see cref="Start"/>, or <see langword="null"/> for <see cref="DefaultPreRoll"/>. Must be
    ///     between <see cref="TimeSpan.Zero"/> (disabled) and <see cref="MaxPreRoll"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="inner"/> is a <see cref="WavFileAudioCaptureDevice"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="preRoll"/> is negative or greater than <see cref="MaxPreRoll"/>.
    /// </exception>
    public PreRollAudioCaptureDevice(IAudioCaptureDevice inner, TimeSpan? preRoll = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        if (inner is WavFileAudioCaptureDevice)
        {
            throw new ArgumentException(
                "A finite WAV file source cannot be wrapped for pre-roll; pass it to the recognition session directly.",
                nameof(inner));
        }
        var duration = preRoll ?? DefaultPreRoll;
        if (duration < TimeSpan.Zero || duration > MaxPreRoll)
        {
            throw new ArgumentOutOfRangeException(
                nameof(preRoll),
                duration,
                $"The pre-roll must be between zero and {MaxPreRoll.TotalSeconds} seconds.");
        }

        _inner = inner;
        PreRoll = duration;
    }

    /// <summary>
    ///     Gets how much of the most recent audio captured while stopped is delivered on the next
    ///     <see cref="Start"/>; <see cref="TimeSpan.Zero"/> when the feature is disabled.
    /// </summary>
    public TimeSpan PreRoll { get; }

    /// <inheritdoc/>
    /// <remarks>Reflects the underlying device; always <see langword="false"/> once disposed.</remarks>
    public bool IsAvailable => !Volatile.Read(ref _disposed) && _inner.IsAvailable;

    /// <inheritdoc/>
    /// <remarks>Reflects the underlying device, whose format every delivered block (pre-roll included) uses.</remarks>
    public int ChannelCount => _inner.ChannelCount;

    /// <inheritdoc/>
    /// <remarks>Reflects the underlying device, whose format every delivered block (pre-roll included) uses.</remarks>
    public int SampleRate => _inner.SampleRate;

    /// <inheritdoc/>
    /// <remarks>
    ///     Raised on the underlying device's capture thread for live audio, and synchronously from
    ///     <see cref="Start"/> for the one pre-roll block (when any audio was buffered).
    ///     Unsubscribing waits for a block that is already being raised to finish, so once the
    ///     <c>remove</c> returns the handler receives nothing more, and a block raised to it before
    ///     that has been fully delivered - a block is never half-delivered at the handoff between a
    ///     stopping subscriber and the pre-roll buffer. Do not unsubscribe while holding a lock that
    ///     a handler needs.
    /// </remarks>
    public event EventHandler<AudioCaptureFrameEventArgs>? FrameCaptured
    {
        add
        {
            lock (_handlersLock)
            {
                _frameCaptured += value;
            }
        }

        remove
        {
            lock (_handlersLock)
            {
                _frameCaptured -= value;
            }

            lock (_deliveryLock)
            {
                // Barrier: blocks are raised while holding the delivery lock, so acquiring it
                // here waits for a block already being raised (possibly to this handler) to
                // finish. Every block raised after this point sees the updated handler list and,
                // if nobody is left to receive it, goes to the pre-roll buffer instead.
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     The first call starts the underlying device, delivering anything it raises from then
    ///     on (there is no pre-roll yet); later calls (after a <see cref="Stop"/> that kept it
    ///     running) only resume delivery. Before resuming, the buffered pre-roll is
    ///     raised as one <see cref="FrameCaptured"/> block and the buffer is emptied. Calling
    ///     this while already started does nothing.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when this device has been disposed.</exception>
    /// <exception cref="AudioDeviceUnavailableException">
    ///     Thrown when the underlying device is unavailable, or propagated from the underlying
    ///     device's own <see cref="IAudioCaptureDevice.Start"/> when its stream fails to start.
    /// </exception>
    public void Start()
    {
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_inner.IsAvailable)
            {
                throw new AudioDeviceUnavailableException(
                    "Cannot start capture: the underlying capture device is unavailable.");
            }

            // Start the underlying device on the first run (or every run when pre-roll is
            // disabled). Nothing can have been buffered yet in that case, so delivery resumes
            // before the underlying device starts: a device that raises blocks synchronously from
            // its own Start(), or reports its format only once started, therefore loses nothing.
            if (!_innerRunning)
            {
                lock (_deliveryLock)
                {
                    EnsureRingAllocatedLocked();
                    _delivering = true;
                }

                _inner.FrameCaptured += OnInnerFrameCaptured;
                try
                {
                    _inner.Start();
                }
                catch
                {
                    _inner.FrameCaptured -= OnInnerFrameCaptured;
                    lock (_deliveryLock)
                    {
                        _delivering = false;
                    }

                    throw;
                }

                _innerRunning = true;
                lock (_deliveryLock)
                {
                    // The format may only have become known once the device started.
                    EnsureRingAllocatedLocked();
                }

                return;
            }

            // Hand over the pre-roll and resume live delivery atomically with respect to the
            // capture callback, so the pre-roll block always precedes every live block.
            lock (_deliveryLock)
            {
                if (_delivering)
                {
                    return;
                }

                EnsureRingAllocatedLocked();
                var preRoll = DrainRingLocked();
                _delivering = true;
                if (preRoll.Length > 0)
                {
                    Volatile.Read(ref _frameCaptured)?.Invoke(this, new AudioCaptureFrameEventArgs(preRoll));
                }
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Stops delivering audio to subscribers. With a non-zero <see cref="PreRoll"/> the
    ///     underlying device keeps capturing into the pre-roll buffer until the next
    ///     <see cref="Start"/> or <see cref="Dispose"/>; with a zero pre-roll the underlying device
    ///     is stopped immediately. Calling this when not started, or after disposal, does nothing.
    /// </remarks>
    /// <exception cref="AudioDeviceUnavailableException">
    ///     Propagated from the underlying device's own <see cref="IAudioCaptureDevice.Stop"/> when
    ///     the pre-roll is disabled and its stream fails to stop cleanly.
    /// </exception>
    public void Stop()
    {
        lock (_lifecycleLock)
        {
            if (_disposed || !_innerRunning)
            {
                return;
            }

            lock (_deliveryLock)
            {
                _delivering = false;
            }

            if (PreRoll == TimeSpan.Zero)
            {
                StopInnerLocked();
            }
        }
    }

    /// <summary>
    ///     Stops and releases the underlying device and discards the pre-roll buffer. Idempotent.
    /// </summary>
    /// <remarks>
    ///     Never throws: an underlying device that fails to stop cleanly is already being released,
    ///     and a disposer has no way to recover from that, so the failure is deliberately
    ///     swallowed. After disposal <see cref="IsAvailable"/> reports <see langword="false"/> and
    ///     <see cref="Start"/> throws <see cref="ObjectDisposedException"/>.
    /// </remarks>
    public void Dispose()
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            Volatile.Write(ref _disposed, true);
            lock (_deliveryLock)
            {
                _delivering = false;
                _ringCount = 0;
            }

            if (_innerRunning)
            {
                try
                {
                    StopInnerLocked();
                }
                catch (AudioDeviceUnavailableException)
                {
                    // Already being released; nothing further a disposer can do (see remarks).
                }
            }
        }
    }

    /// <summary>
    ///     Unsubscribes from and stops the underlying device. Must be called with
    ///     <see cref="_lifecycleLock"/> held and only while <see cref="_innerRunning"/> is set.
    /// </summary>
    private void StopInnerLocked()
    {
        _inner.FrameCaptured -= OnInnerFrameCaptured;
        _innerRunning = false;
        _inner.Stop();
    }

    /// <summary>
    ///     Routes one block from the underlying device: forwarded to subscribers while delivering,
    ///     otherwise remembered in the pre-roll buffer.
    /// </summary>
    /// <param name="sender">The underlying device. Unused.</param>
    /// <param name="e">The captured block.</param>
    private void OnInnerFrameCaptured(object? sender, AudioCaptureFrameEventArgs e)
    {
        lock (_deliveryLock)
        {
            var handlers = Volatile.Read(ref _frameCaptured);
            if (_delivering && handlers is not null)
            {
                handlers.Invoke(this, e);
                return;
            }

            // Not delivering, or delivering to nobody (for example while a stopping session has
            // already unsubscribed): this audio reached no one, so it is pre-roll for the next run.
            AppendToRingLocked(e.Samples);
        }
    }

    /// <summary>
    ///     Sizes the pre-roll buffer once the underlying device reports a usable format. Must be
    ///     called with <see cref="_deliveryLock"/> held.
    /// </summary>
    /// <remarks>
    ///     The capacity is a whole number of interleaved frames, so the buffer always holds whole
    ///     frames and the pre-roll block keeps the underlying device's channel alignment.
    /// </remarks>
    private void EnsureRingAllocatedLocked()
    {
        if (_ring.Length > 0 || PreRoll == TimeSpan.Zero)
        {
            return;
        }

        var sampleRate = _inner.SampleRate;
        var channelCount = _inner.ChannelCount;
        if (sampleRate <= 0 || channelCount <= 0)
        {
            return;
        }

        var frameCount = (int)Math.Round(PreRoll.TotalSeconds * sampleRate);
        if (frameCount > 0)
        {
            _ring = new float[frameCount * channelCount];
        }
    }

    /// <summary>
    ///     Appends captured samples to the circular pre-roll buffer, overwriting the oldest
    ///     samples once it is full. Must be called with <see cref="_deliveryLock"/> held.
    /// </summary>
    /// <param name="samples">The interleaved samples to remember.</param>
    private void AppendToRingLocked(IReadOnlyList<float> samples)
    {
        var capacity = _ring.Length;
        if (capacity == 0)
        {
            return;
        }

        // Only the newest `capacity` samples of an oversized block can survive anyway.
        var first = Math.Max(0, samples.Count - capacity);
        for (var i = first; i < samples.Count; i++)
        {
            _ring[(_ringStart + _ringCount) % capacity] = samples[i];
            if (_ringCount < capacity)
            {
                _ringCount++;
            }
            else
            {
                _ringStart = (_ringStart + 1) % capacity;
            }
        }
    }

    /// <summary>
    ///     Copies the buffered pre-roll out in capture order and empties the buffer. Must be called
    ///     with <see cref="_deliveryLock"/> held.
    /// </summary>
    /// <returns>The buffered samples, oldest first; empty when nothing was buffered.</returns>
    private float[] DrainRingLocked()
    {
        var samples = new float[_ringCount];
        for (var i = 0; i < _ringCount; i++)
        {
            samples[i] = _ring[(_ringStart + i) % _ring.Length];
        }

        _ringStart = 0;
        _ringCount = 0;
        return samples;
    }
}
