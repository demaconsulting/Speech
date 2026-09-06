namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Enumerates the audio capture (input) devices currently available on the host machine.
/// </summary>
/// <remarks>
///     Implementations restrict enumeration to one preferred host API per platform (e.g. WASAPI
///     on Windows) per architecture.md's decision to avoid the same physical device appearing
///     multiple times under different host APIs. Enumeration never throws; a backend that cannot
///     enumerate devices returns an empty list, see <see cref="UnavailableAudioCaptureDeviceProbe"/>.
/// </remarks>
public interface IAudioCaptureDeviceProbe
{
    /// <summary>
    ///     Returns the audio capture devices currently available on the host machine.
    /// </summary>
    /// <returns>
    ///     A read-only list of <see cref="AudioDeviceDescription"/>, one per available capture
    ///     device. Never <see langword="null"/>; an empty list means no capture devices could be
    ///     enumerated.
    /// </returns>
    IReadOnlyList<AudioDeviceDescription> Enumerate();
}
