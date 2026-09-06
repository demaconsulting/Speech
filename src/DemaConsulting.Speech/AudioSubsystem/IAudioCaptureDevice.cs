namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Event data carrying one block of audio samples captured by an <see cref="IAudioCaptureDevice"/>.
/// </summary>
/// <param name="Samples">
///     The captured audio samples, as normalized 32-bit floating point values in the range
///     <c>[-1.0, 1.0]</c>, interleaved by channel when the device is multi-channel.
/// </param>
public sealed record AudioCaptureFrameEventArgs(IReadOnlyList<float> Samples);

/// <summary>
///     Mockable capture (audio input) device contract backed by the library's real PortAudio
///     implementation or an honest unavailable fallback, allowing hosts and tests to depend on a
///     stable abstraction without touching native PortAudio types directly.
/// </summary>
/// <remarks>
///     Implementations own the full lifecycle of a single physical or virtual capture device.
///     A device that cannot function honestly reports <see cref="IsAvailable"/> as
///     <see langword="false"/> rather than throwing at construction time, per architecture.md's
///     "nothing throws at composition" decision; see <see cref="UnavailableAudioCaptureDevice"/>
///     for the canonical fallback.
/// </remarks>
public interface IAudioCaptureDevice
{
    /// <summary>
    ///     Gets a value indicating whether this device is backed by a real, usable capture
    ///     source. When <see langword="false"/>, <see cref="Start"/> and <see cref="Stop"/> throw
    ///     <see cref="AudioDeviceUnavailableException"/> rather than silently doing nothing,
    ///     because a caller that ignores this flag has made a programming error that should
    ///     surface immediately rather than silently capture no audio.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    ///     Gets the number of interleaved channels present in every <see cref="FrameCaptured"/>
    ///     payload, or <c>0</c> when <see cref="IsAvailable"/> is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    ///     This is the channel count the device actually resolved, not a requested or probed
    ///     value. Consumers that must convert captured audio into a different format - notably
    ///     speech recognition, which requires mono audio at a model-declared rate - cannot
    ///     downmix correctly without it, because <see cref="AudioCaptureFrameEventArgs.Samples"/>
    ///     carries no format metadata of its own. Reading this property never throws, so it is
    ///     safe to read before or after <see cref="Start"/>.
    /// </remarks>
    int ChannelCount { get; }

    /// <summary>
    ///     Gets the sample rate, in Hz, at which every <see cref="FrameCaptured"/> payload was
    ///     captured, or <c>0</c> when <see cref="IsAvailable"/> is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    ///     This is the rate the device actually resolved (typically the resolved hardware
    ///     device's own native default), not a requested or probed value. Consumers that must
    ///     resample captured audio to a fixed rate - notably speech recognition, whose models
    ///     declare the rate they require - cannot resample correctly without it. Reading this
    ///     property never throws, so it is safe to read before or after <see cref="Start"/>.
    /// </remarks>
    int SampleRate { get; }

    /// <summary>
    ///     Raised once for each block of audio captured while the device is running.
    /// </summary>
    /// <remarks>
    ///     Real PortAudio-backed implementations may raise this event from a high-priority audio
    ///     callback thread. Handlers should therefore return quickly and must avoid blocking work.
    /// </remarks>
    event EventHandler<AudioCaptureFrameEventArgs>? FrameCaptured;

    /// <summary>
    ///     Begins capturing audio, after which <see cref="FrameCaptured"/> is raised for each
    ///     captured block until <see cref="Stop"/> is called.
    /// </summary>
    /// <exception cref="AudioDeviceUnavailableException">
    ///     Thrown when <see cref="IsAvailable"/> is <see langword="false"/>, or when the device
    ///     reported itself as available but the underlying native capture stream later failed to
    ///     open or start.
    /// </exception>
    void Start();

    /// <summary>
    ///     Stops capturing audio. No further <see cref="FrameCaptured"/> events are raised until
    ///     <see cref="Start"/> is called again.
    /// </summary>
    /// <exception cref="AudioDeviceUnavailableException">
    ///     Thrown when <see cref="IsAvailable"/> is <see langword="false"/>, or when the device
    ///     reported itself as available but the underlying native capture stream later failed to
    ///     stop cleanly.
    /// </exception>
    void Stop();
}
