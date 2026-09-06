using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Demo.DeviceSelectionSubsystem;

/// <summary>
///     Demo-owned seam over the library's audio-device enumeration surface.
/// </summary>
/// <remarks>
///     The library exposes device enumeration through the concrete <see cref="AudioDeviceFactory"/>
///     rather than through an injectable interface, so a ViewModel that depended on it directly
///     could not be unit tested without a real audio backend. This interface exists purely as the
///     demo application's own composition-root seam: the production implementation delegates
///     straight to <see cref="AudioDeviceFactory"/>, while tests substitute a fake. It adds no
///     public API to <c>DemaConsulting.Speech</c> and deliberately mirrors, rather than extends,
///     the library's behavior.
/// </remarks>
public interface IAudioDeviceService
{
    /// <summary>
    ///     Enumerates the audio capture (input) devices currently offered to the user.
    /// </summary>
    /// <returns>
    ///     A snapshot list of available capture devices, in the order the audio backend reports
    ///     them. Never <see langword="null"/>; empty when no capture device can be enumerated
    ///     (for example on a machine with no microphone, or where the audio backend failed to
    ///     initialize).
    /// </returns>
    /// <remarks>Never throws; an unavailable audio backend degrades to an empty list.</remarks>
    IReadOnlyList<AudioDeviceDescription> EnumerateCaptureDevices();

    /// <summary>
    ///     Enumerates the audio playback (output) devices currently offered to the user.
    /// </summary>
    /// <returns>
    ///     A snapshot list of available playback devices, in the order the audio backend reports
    ///     them. Never <see langword="null"/>; empty when no playback device can be enumerated.
    /// </returns>
    /// <remarks>Never throws; an unavailable audio backend degrades to an empty list.</remarks>
    IReadOnlyList<AudioDeviceDescription> EnumeratePlaybackDevices();

    /// <summary>
    ///     Creates a capture (input) device for the given selection, for a panel that needs to
    ///     stream real audio rather than merely list device names.
    /// </summary>
    /// <param name="selection">
    ///     The persisted device selection to honor, or <see langword="null"/> to request the
    ///     host-API-scoped default input device.
    /// </param>
    /// <returns>
    ///     A working capture device when the audio backend is available, or an honest
    ///     <c>IsAvailable == false</c> device otherwise.
    /// </returns>
    /// <remarks>Never throws; an unavailable audio backend degrades to an unavailable device.</remarks>
    IAudioCaptureDevice CreateCaptureDevice(AudioDeviceSelection? selection);

    /// <summary>
    ///     Creates a playback (output) device for the given selection, for a panel that needs to
    ///     play real audio rather than merely list device names.
    /// </summary>
    /// <param name="selection">
    ///     The persisted device selection to honor, or <see langword="null"/> to request the
    ///     host-API-scoped default output device.
    /// </param>
    /// <returns>
    ///     A working playback device when the audio backend is available, or an honest
    ///     <c>IsAvailable == false</c> device otherwise.
    /// </returns>
    /// <remarks>Never throws; an unavailable audio backend degrades to an unavailable device.</remarks>
    IAudioPlaybackDevice CreatePlaybackDevice(AudioDeviceSelection? selection);
}
