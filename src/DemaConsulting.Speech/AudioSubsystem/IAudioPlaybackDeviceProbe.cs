namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Enumerates the audio playback (output) devices currently available on the host machine.
/// </summary>
/// <remarks>
///     Implementations restrict enumeration to one preferred host API per platform (e.g. WASAPI
///     on Windows) per this library's decision to avoid the same physical device appearing
///     multiple times under different host APIs. Enumeration never throws; a backend that cannot
///     enumerate devices returns an empty list, see <see cref="UnavailableAudioPlaybackDeviceProbe"/>.
/// </remarks>
public interface IAudioPlaybackDeviceProbe
{
    /// <summary>
    ///     Returns the audio playback devices currently available on the host machine.
    /// </summary>
    /// <returns>
    ///     A read-only list of <see cref="AudioDeviceDescription"/>, one per available playback
    ///     device. Never <see langword="null"/>; an empty list means no playback devices could be
    ///     enumerated.
    /// </returns>
    IReadOnlyList<AudioDeviceDescription> Enumerate();
}
