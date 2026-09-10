// cspell:ignore Alsa ALSA portaudio Cdecl Conv Convs
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DemaConsulting.Speech.AudioSubsystem.PortAudio;

/// <summary>
///     Declares supplementary native PortAudio entry points that PortAudioSharp2 does not bind
///     but that the Speech library needs for preferred-host-API selection.
/// </summary>
/// <remarks>
///     These declarations intentionally target the same <c>portaudio</c> shared-library name that
///     PortAudioSharp2 itself uses, so no additional native dependency is introduced.
/// </remarks>
internal static partial class PortAudioNativeMethods
{
    /// <summary>
    ///     The shared-library name used by both PortAudioSharp2 and the supplementary bindings in
    ///     this repository.
    /// </summary>
    internal const string PortAudioLibraryName = "portaudio";

    /// <summary>
    ///     Returns the number of host APIs exposed by the initialized PortAudio runtime.
    /// </summary>
    [LibraryImport(PortAudioLibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int Pa_GetHostApiCount();

    /// <summary>
    ///     Returns a pointer to immutable information describing one host API.
    /// </summary>
    /// <param name="hostApiIndex">The runtime-specific host-API index to inspect.</param>
    [LibraryImport(PortAudioLibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint Pa_GetHostApiInfo(int hostApiIndex);

    /// <summary>
    ///     Resolves a stable PortAudio host-API type identifier to the runtime's current host-
    ///     API index.
    /// </summary>
    /// <param name="type">The stable host-API type identifier to resolve.</param>
    [LibraryImport(PortAudioLibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int Pa_HostApiTypeIdToHostApiIndex(PortAudioHostApiType type);

    /// <summary>
    ///     Determines whether the host API can actually open a stream for the given input and/or
    ///     output parameters at the given sample rate, returning <c>paNoError</c> (<c>0</c>) only
    ///     when the exact combination is supported.
    /// </summary>
    /// <param name="inputParameters">
    ///     Pointer to a native <c>PaStreamParameters</c> structure describing the requested input
    ///     side, or <see cref="nint.Zero"/> when the probe is output-only.
    /// </param>
    /// <param name="outputParameters">
    ///     Pointer to a native <c>PaStreamParameters</c> structure describing the requested output
    ///     side, or <see cref="nint.Zero"/> when the probe is input-only.
    /// </param>
    /// <param name="sampleRate">The sample rate, in Hz, to probe.</param>
    [LibraryImport(PortAudioLibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int Pa_IsFormatSupported(nint inputParameters, nint outputParameters, double sampleRate);

    /// <summary>
    ///     Managed layout for PortAudio's native <c>PaHostApiInfo</c> structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct HostApiInfoNative
    {
        /// <summary>
        ///     The PortAudio structure version.
        /// </summary>
        internal int StructVersion;

        /// <summary>
        ///     The stable PortAudio host-API type identifier.
        /// </summary>
        internal PortAudioHostApiType Type;

        /// <summary>
        ///     Pointer to the UTF-8 or ANSI host-API name owned by PortAudio.
        /// </summary>
        internal nint Name;

        /// <summary>
        ///     The number of devices exposed through this host API.
        /// </summary>
        internal int DeviceCount;

        /// <summary>
        ///     The host-API-scoped default input-device index, or <c>-1</c>.
        /// </summary>
        internal int DefaultInputDevice;

        /// <summary>
        ///     The host-API-scoped default output-device index, or <c>-1</c>.
        /// </summary>
        internal int DefaultOutputDevice;
    }
}
