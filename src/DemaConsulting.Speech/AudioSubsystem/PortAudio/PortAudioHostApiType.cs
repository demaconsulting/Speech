// cspell:ignore Alsa ALSA portaudio
namespace DemaConsulting.Speech.AudioSubsystem.PortAudio;

/// <summary>
///     Stable PortAudio host-API type identifiers used by the Speech library's single-host-API-
///     per-platform selection policy.
/// </summary>
/// <remarks>
///     The numeric values mirror PortAudio's native <c>PaHostApiTypeId</c> constants from
///     <c>portaudio.h</c>. They are treated as an OTS wire contract rather than application
///     magic numbers.
/// </remarks>
internal enum PortAudioHostApiType
{
    /// <summary>
    ///     The native <c>paCoreAudio</c> host API (<c>5</c>) used on macOS.
    /// </summary>
    CoreAudio = 5,

    /// <summary>
    ///     The native <c>paALSA</c> host API (<c>8</c>) preferred on Linux.
    /// </summary>
    Alsa = 8,

    /// <summary>
    ///     The native <c>paWASAPI</c> host API (<c>13</c>) preferred on Windows.
    /// </summary>
    Wasapi = 13,
}
