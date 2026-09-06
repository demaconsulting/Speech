namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Immutable audio-format value declaring a sample rate and channel count together.
/// </summary>
/// <remarks>
///     This value type exists so callers can pass audio-format intent through the library
///     without leaking any native-backend type into the public API. At model call sites it
///     expresses the audio format a model requires or prefers. At audio-device call sites it
///     expresses the format a caller would like the backend to request when opening a device.
///     Those two uses have deliberately different authority: a recognition model's
///     <see cref="SampleRate"/> and <see cref="ChannelCount"/> are authoritative requirements
///     for the engine it configures, while a synthesis model's or audio device's
///     <see cref="AudioDeviceFactory.CreateCaptureDevice(AudioDeviceSelection?, AudioFormat?)"/>
///     and
///     <see cref="AudioDeviceFactory.CreatePlaybackDevice(AudioDeviceSelection?, AudioFormat?)"/>
///     call-site value is only a requested or preferred format. The actual resolved device
///     format remains the device instance's own reported <c>SampleRate</c> and
///     <c>ChannelCount</c> after construction.
/// </remarks>
public sealed record AudioFormat
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AudioFormat"/> class.
    /// </summary>
    /// <param name="sampleRate">
    ///     The audio sample rate, in Hz. Must be greater than zero.
    /// </param>
    /// <param name="channelCount">
    ///     The audio channel count. Must be greater than zero.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="sampleRate"/> or <paramref name="channelCount"/> is less
    ///     than or equal to zero, because no meaningful audio format exists at a zero or negative
    ///     rate or channel count.
    /// </exception>
    public AudioFormat(int sampleRate, int channelCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(channelCount, 0);

        SampleRate = sampleRate;
        ChannelCount = channelCount;
    }

    /// <summary>
    ///     Gets the audio sample rate, in Hz.
    /// </summary>
    public int SampleRate { get; }

    /// <summary>
    ///     Gets the number of audio channels.
    /// </summary>
    public int ChannelCount { get; }

    /// <summary>
    ///     Creates a mono audio format at the supplied sample rate.
    /// </summary>
    /// <param name="sampleRate">
    ///     The mono sample rate, in Hz. Must be greater than zero.
    /// </param>
    /// <returns>
    ///     A new <see cref="AudioFormat"/> whose <see cref="ChannelCount"/> is <c>1</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="sampleRate"/> is less than or equal to zero.
    /// </exception>
    /// <remarks>
    ///     Recognition and synthesis models in this library currently declare mono formats, so
    ///     this helper keeps those call sites concise while preserving the same validation rules
    ///     as the main constructor.
    /// </remarks>
    public static AudioFormat Mono(int sampleRate)
    {
        return new AudioFormat(sampleRate, channelCount: 1);
    }
}
