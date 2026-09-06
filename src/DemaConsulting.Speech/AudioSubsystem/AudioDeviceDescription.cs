namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Identifies whether an audio device description represents a capture (input) or
///     playback (output) device.
/// </summary>
public enum AudioDeviceDirection
{
    /// <summary>
    ///     The device captures (records) audio.
    /// </summary>
    Capture,

    /// <summary>
    ///     The device plays back (renders) audio.
    /// </summary>
    Playback
}

/// <summary>
///     Immutable description of an enumerable audio device, identified by name alone.
/// </summary>
/// <remarks>
///     Per this library's "single preferred host API per platform, name-only device
///     identity" decision, <see cref="Name"/> is the device's sole stable identity: PortAudio
///     device indices are not stable across reboots or hot-plug events, so this library never
///     persists or compares devices by index. The trade-off - two identically-named devices are
///     indistinguishable - is accepted for the desktop use case in exchange for removing index
///     instability and host-API ambiguity.
/// </remarks>
/// <param name="Name">
///     The device's display name, as reported by the underlying audio backend. This is the
///     device's only stable identity; see the remarks on <see cref="AudioDeviceDescription"/>.
/// </param>
/// <param name="Direction">Whether the device captures or plays back audio.</param>
/// <param name="ChannelCount">
///     The number of audio channels the device supports (e.g. <c>1</c> for mono, <c>2</c> for
///     stereo). Must be a positive number.
/// </param>
/// <param name="SampleRate">
///     The device's operating sample rate in Hz (e.g. <c>16000</c>, <c>44100</c>). Must be a
///     positive number.
/// </param>
public sealed record AudioDeviceDescription(
    string Name,
    AudioDeviceDirection Direction,
    int ChannelCount,
    int SampleRate);
