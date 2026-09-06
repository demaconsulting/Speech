namespace DemaConsulting.Speech.AudioSubsystem.PortAudio;

/// <summary>
///     Managed description of one PortAudio device, decoupled from PortAudioSharp2's concrete
///     <c>DeviceInfo</c> struct so unit tests can supply fake devices without loading the native
///     binding.
/// </summary>
/// <param name="Name">The PortAudio-reported device name.</param>
/// <param name="HostApiIndex">The runtime-specific host-API index that owns this device.</param>
/// <param name="MaxInputChannels">The maximum input channels the device can capture.</param>
/// <param name="MaxOutputChannels">The maximum output channels the device can render.</param>
/// <param name="DefaultSampleRate">The device's default sample rate in Hz.</param>
/// <param name="DefaultLowInputLatency">
///     The PortAudio-reported preferred low-latency input setting, in seconds.
/// </param>
/// <param name="DefaultLowOutputLatency">
///     The PortAudio-reported preferred low-latency output setting, in seconds.
/// </param>
internal sealed record PortAudioDeviceInfo(
    string Name,
    int HostApiIndex,
    int MaxInputChannels,
    int MaxOutputChannels,
    int DefaultSampleRate,
    double DefaultLowInputLatency,
    double DefaultLowOutputLatency);
