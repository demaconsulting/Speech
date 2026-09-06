using DemaConsulting.Speech.AudioSubsystem.PortAudio;

namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     PortAudio-backed implementation of <see cref="IAudioPlaybackDeviceProbe"/> that enumerates
///     output-capable devices from exactly one preferred host API per platform.
/// </summary>
/// <remarks>
///     Enumeration never throws. When PortAudio cannot initialize, the preferred host API is not
///     present, or no output-capable devices exist on that host API, callers receive an empty list.
/// </remarks>
internal sealed class PortAudioPlaybackDeviceProbe : IAudioPlaybackDeviceProbe
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PortAudioPlaybackDeviceProbe"/> class.
    /// </summary>
    /// <param name="environment">
    ///     The PortAudio environment whose preferred host API and device catalog should be used.
    /// </param>
    internal PortAudioPlaybackDeviceProbe(PortAudioEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    /// <summary>
    ///     The PortAudio environment whose preferred host API and device catalog should be used.
    /// </summary>
    private readonly PortAudioEnvironment _environment;

    /// <inheritdoc/>
    public IReadOnlyList<AudioDeviceDescription> Enumerate()
    {
        if (!_environment.TryResolvePreferredHostApi(out var hostApiIndex, out _))
        {
            return [];
        }

        var devices = new List<AudioDeviceDescription>();
        for (var deviceIndex = 0; deviceIndex < _environment.Api.DeviceCount; deviceIndex++)
        {
            var deviceInfo = _environment.Api.GetDeviceInfo(deviceIndex);
            if (deviceInfo.HostApiIndex != hostApiIndex || deviceInfo.MaxOutputChannels <= 0)
            {
                continue;
            }

            devices.Add(
                new AudioDeviceDescription(
                    deviceInfo.Name,
                    AudioDeviceDirection.Playback,
                    deviceInfo.MaxOutputChannels,
                    deviceInfo.DefaultSampleRate));
        }

        return devices;
    }
}
