namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Honest fallback <see cref="IAudioCaptureDevice"/> used when no real capture backend is
///     available, reporting <see cref="IsAvailable"/> as <see langword="false"/> rather than
///     letting a caller construct against a device that cannot function.
/// </summary>
/// <remarks>
///     Per architecture.md's "nothing throws at composition" decision, obtaining and holding this
///     instance never throws. Only the operational members (<see cref="Start"/>,
///     <see cref="Stop"/>) throw <see cref="AudioDeviceUnavailableException"/>, and only when
///     actually invoked - a caller that checks <see cref="IsAvailable"/> first, as documented,
///     never triggers them. The type is stateless and holds no resources, so the shared
///     <see cref="Instance"/> is safe for concurrent use by any number of callers.
/// </remarks>
public sealed class UnavailableAudioCaptureDevice : IAudioCaptureDevice
{
    /// <summary>
    ///     Prevents external construction; callers use the shared <see cref="Instance"/> instead
    ///     since the type carries no state and multiple instances would provide no value.
    /// </summary>
    private UnavailableAudioCaptureDevice()
    {
    }

    /// <summary>
    ///     Gets the single shared unavailable capture device.
    /// </summary>
    public static UnavailableAudioCaptureDevice Instance { get; } = new();

    /// <summary>
    ///     Backing field for <see cref="FrameCaptured"/>. Never invoked, because this device can
    ///     never enter a running state; kept only so subscribe/unsubscribe are safe no-ops rather
    ///     than throwing.
    /// </summary>
    private EventHandler<AudioCaptureFrameEventArgs>? _frameCaptured;

    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    /// <remarks>
    ///     Always <c>0</c>: this device resolves no real capture source, so it has no channel
    ///     layout to report. Reporting zero rather than a plausible-looking default keeps the
    ///     fallback honest, matching <see cref="IsAvailable"/>.
    /// </remarks>
    public int ChannelCount => 0;

    /// <inheritdoc/>
    /// <remarks>
    ///     Always <c>0</c>: this device resolves no real capture source, so it has no sample
    ///     rate to report. Reporting zero rather than a plausible-looking default keeps the
    ///     fallback honest, matching <see cref="IsAvailable"/>.
    /// </remarks>
    public int SampleRate => 0;

    /// <inheritdoc/>
    /// <remarks>
    ///     Never raised, since this device can never enter a running state; subscribing and
    ///     unsubscribing are still safe no-ops.
    /// </remarks>
    public event EventHandler<AudioCaptureFrameEventArgs>? FrameCaptured
    {
        add => _frameCaptured += value;
        remove => _frameCaptured -= value;
    }

    /// <inheritdoc/>
    /// <exception cref="AudioDeviceUnavailableException">
    ///     Always thrown; this device has no real capture backend to start.
    /// </exception>
    public void Start()
    {
        throw new AudioDeviceUnavailableException(
            "Cannot start capture: no audio capture device is available.");
    }

    /// <inheritdoc/>
    /// <exception cref="AudioDeviceUnavailableException">
    ///     Always thrown; this device has no real capture backend to stop.
    /// </exception>
    public void Stop()
    {
        throw new AudioDeviceUnavailableException(
            "Cannot stop capture: no audio capture device is available.");
    }
}
