namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Mockable playback (audio output) device contract backed by the library's real PortAudio
///     implementation or an honest unavailable fallback, allowing hosts and tests to depend on a
///     stable abstraction without touching native PortAudio types directly.
/// </summary>
/// <remarks>
///     Implementations own the full lifecycle of a single physical or virtual playback device.
///     A device that cannot function honestly reports <see cref="IsAvailable"/> as
///     <see langword="false"/> rather than throwing at construction time, per this library's
///     "nothing throws at composition" decision; see <see cref="UnavailableAudioPlaybackDevice"/>
///     for the canonical fallback.
/// </remarks>
public interface IAudioPlaybackDevice
{
    /// <summary>
    ///     Gets a value indicating whether this device is backed by a real, usable playback
    ///     sink. When <see langword="false"/>, <see cref="Start"/>, <see cref="Stop"/>, and
    ///     <see cref="Write"/> throw <see cref="AudioDeviceUnavailableException"/> rather than
    ///     silently discarding audio, because a caller that ignores this flag has made a
    ///     programming error that should surface immediately.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    ///     Gets the number of interleaved channels every <see cref="Write"/> call must supply, or
    ///     <c>0</c> when <see cref="IsAvailable"/> is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    ///     This is the channel count the device actually resolved, not a requested or probed
    ///     value. Consumers that must convert audio into a different format - notably speech
    ///     synthesis, which produces mono audio and must upmix it to whatever channel layout the
    ///     device resolved - cannot do so correctly without it. Reading this property never
    ///     throws, so it is safe to read before or after <see cref="Start"/>. Mirrors
    ///     <see cref="IAudioCaptureDevice.ChannelCount"/> for the playback direction.
    /// </remarks>
    int ChannelCount { get; }

    /// <summary>
    ///     Gets the sample rate, in Hz, at which <see cref="Write"/> renders audio, or <c>0</c>
    ///     when <see cref="IsAvailable"/> is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    ///     This is the rate the device actually resolved (typically the resolved hardware
    ///     device's own native default), not a requested or probed value. Consumers that must
    ///     resample audio to a fixed rate - notably speech synthesis, whose engines produce audio
    ///     at their own declared rate - cannot resample correctly without it. Reading this
    ///     property never throws, so it is safe to read before or after <see cref="Start"/>.
    ///     Mirrors <see cref="IAudioCaptureDevice.SampleRate"/> for the playback direction.
    /// </remarks>
    int SampleRate { get; }

    /// <summary>
    ///     Prepares the device to accept audio via <see cref="Write"/>.
    /// </summary>
    /// <exception cref="AudioDeviceUnavailableException">
    ///     Thrown when <see cref="IsAvailable"/> is <see langword="false"/>, or when the device
    ///     reported itself as available but the underlying native playback stream later failed to
    ///     open or start.
    /// </exception>
    void Start();

    /// <summary>
    ///     Stops playback. No further <see cref="Write"/> calls are valid until <see cref="Start"/>
    ///     is called again.
    /// </summary>
    /// <exception cref="AudioDeviceUnavailableException">
    ///     Thrown when <see cref="IsAvailable"/> is <see langword="false"/>, or when the device
    ///     reported itself as available but the underlying native playback stream later failed to
    ///     stop cleanly.
    /// </exception>
    void Stop();

    /// <summary>
    ///     Writes one block of audio samples to the device for playback.
    /// </summary>
    /// <param name="samples">
    ///     Normalized 32-bit floating point samples in the range <c>[-1.0, 1.0]</c>, interleaved
    ///     by channel when the device is multi-channel. Must not be <see langword="null"/>.
    /// </param>
    /// <remarks>
    ///     Real PortAudio-backed implementations may buffer the supplied samples for later
    ///     callback-driven playback rather than synchronously waiting for the hardware to finish
    ///     rendering them.
    /// </remarks>
    /// <exception cref="AudioDeviceUnavailableException">
    ///     Thrown when <see cref="IsAvailable"/> is <see langword="false"/>, or when the device
    ///     reported itself as available but the underlying native playback path failed before the
    ///     queued samples could be rendered.
    /// </exception>
    void Write(IReadOnlyList<float> samples);

    /// <summary>
    ///     Gets the number of samples previously passed to <see cref="Write"/> that have not yet
    ///     actually been rendered by the playback hardware, or <c>0</c> when
    ///     <see cref="IsAvailable"/> is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    ///     <see cref="Write"/> is fire-and-forget: it enqueues samples for later, callback-driven
    ///     rendering and returns immediately regardless of how much audio is still pending. This
    ///     property is the only honest way to observe how much of that queued audio the hardware
    ///     has actually consumed. A caller that must know playback has genuinely finished -
    ///     rather than merely having been handed off - should poll this property until it reaches
    ///     <c>0</c> (with a small additional safety margin for any residual buffering not visible
    ///     here) before treating playback as complete. Reading this property never throws and
    ///     never blocks, so it is safe to read at any time, including before <see cref="Start"/>
    ///     or after <see cref="Stop"/>, when it reads <c>0</c>.
    /// </remarks>
    long PendingSampleCount { get; }
}
