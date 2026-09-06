namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Honest fallback <see cref="IAudioPlaybackDevice"/> used when no real playback backend is
///     available, reporting <see cref="IsAvailable"/> as <see langword="false"/> rather than
///     letting a caller construct against a device that cannot function.
/// </summary>
/// <remarks>
///     Per architecture.md's "nothing throws at composition" decision, obtaining and holding this
///     instance never throws. Only the operational members (<see cref="Start"/>,
///     <see cref="Stop"/>, <see cref="Write"/>) throw <see cref="AudioDeviceUnavailableException"/>,
///     and only when actually invoked - a caller that checks <see cref="IsAvailable"/> first, as
///     documented, never triggers them. The type is stateless and holds no resources, so the
///     shared <see cref="Instance"/> is safe for concurrent use by any number of callers.
/// </remarks>
public sealed class UnavailableAudioPlaybackDevice : IAudioPlaybackDevice
{
    /// <summary>
    ///     Prevents external construction; callers use the shared <see cref="Instance"/> instead
    ///     since the type carries no state and multiple instances would provide no value.
    /// </summary>
    private UnavailableAudioPlaybackDevice()
    {
    }

    /// <summary>
    ///     Gets the single shared unavailable playback device.
    /// </summary>
    public static UnavailableAudioPlaybackDevice Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    /// <remarks>
    ///     Always <c>0</c>: this device resolves no real playback sink, so it has no channel
    ///     layout to report. Reporting zero rather than a plausible-looking default keeps the
    ///     fallback honest, matching <see cref="IsAvailable"/>.
    /// </remarks>
    public int ChannelCount => 0;

    /// <inheritdoc/>
    /// <remarks>
    ///     Always <c>0</c>: this device resolves no real playback sink, so it has no sample rate
    ///     to report. Reporting zero rather than a plausible-looking default keeps the fallback
    ///     honest, matching <see cref="IsAvailable"/>.
    /// </remarks>
    public int SampleRate => 0;

    /// <inheritdoc/>
    /// <exception cref="AudioDeviceUnavailableException">
    ///     Always thrown; this device has no real playback backend to start.
    /// </exception>
    public void Start()
    {
        throw new AudioDeviceUnavailableException(
            "Cannot start playback: no audio playback device is available.");
    }

    /// <inheritdoc/>
    /// <exception cref="AudioDeviceUnavailableException">
    ///     Always thrown; this device has no real playback backend to stop.
    /// </exception>
    public void Stop()
    {
        throw new AudioDeviceUnavailableException(
            "Cannot stop playback: no audio playback device is available.");
    }

    /// <inheritdoc/>
    /// <exception cref="AudioDeviceUnavailableException">
    ///     Always thrown; this device has no real playback backend to write to.
    /// </exception>
    public void Write(IReadOnlyList<float> samples)
    {
        throw new AudioDeviceUnavailableException(
            "Cannot write audio: no audio playback device is available.");
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Always <c>0</c>: this device never accepts any samples via <see cref="Write"/> (it
    ///     always throws instead), so there is never anything pending to report, matching
    ///     <see cref="IsAvailable"/>.
    /// </remarks>
    public long PendingSampleCount => 0;
}
