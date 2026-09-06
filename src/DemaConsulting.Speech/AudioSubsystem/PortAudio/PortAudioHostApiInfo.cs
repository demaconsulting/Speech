namespace DemaConsulting.Speech.AudioSubsystem.PortAudio;

/// <summary>
///     Managed description of one PortAudio host API, decoupled from raw native structs so the
///     public device-selection logic can be unit tested in pure managed code.
/// </summary>
/// <param name="Name">The PortAudio-reported host-API display name.</param>
/// <param name="Type">The stable PortAudio host-API type identifier.</param>
/// <param name="DefaultInputDeviceIndex">
///     The host-API-scoped default input-device index, or <c>-1</c> when none exists.
/// </param>
/// <param name="DefaultOutputDeviceIndex">
///     The host-API-scoped default output-device index, or <c>-1</c> when none exists.
/// </param>
internal sealed record PortAudioHostApiInfo(
    string Name,
    PortAudioHostApiType Type,
    int DefaultInputDeviceIndex,
    int DefaultOutputDeviceIndex);
