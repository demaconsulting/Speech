// cspell:ignore Alsa ALSA portaudio
namespace DemaConsulting.Speech.AudioSubsystem.PortAudio;

/// <summary>
///     Abstracts the subset of PortAudio functionality the Speech library needs so audio-device
///     logic can be unit tested without loading a native PortAudio runtime or touching real audio
///     hardware.
/// </summary>
/// <remarks>
///     Public audio-device types depend on this seam rather than directly on PortAudioSharp2 or
///     raw P/Invoke entry points. Implementations may wrap the real PortAudio runtime or a test
///     double, but callers above this seam observe the same host-API resolution, device
///     enumeration, and stream-opening contracts.
/// </remarks>
internal interface IPortAudioApi
{
    /// <summary>
    ///     Initializes the underlying PortAudio runtime so device enumeration and stream opening
    ///     can proceed.
    /// </summary>
    /// <remarks>
    ///     Implementations may treat repeated calls as idempotent or may forward them to the
    ///     underlying runtime when that runtime supports reference-counted initialization.
    /// </remarks>
    /// <exception cref="Exception">
    ///     Thrown when the underlying PortAudio runtime cannot be initialized.
    /// </exception>
    void Initialize();

    /// <summary>
    ///     Terminates the underlying PortAudio runtime, releasing its cached device table so a
    ///     subsequent <see cref="Initialize"/> call re-scans available hardware.
    /// </summary>
    /// <remarks>
    ///     Mirrors <c>Pa_Terminate()</c>. Only safe to call when no stream opened against this
    ///     runtime is currently active; callers above this seam (<see cref="PortAudioEnvironment"/>)
    ///     are responsible for refusing to call this while any stream is open.
    /// </remarks>
    /// <exception cref="Exception">
    ///     Thrown when the underlying PortAudio runtime fails to terminate cleanly.
    /// </exception>
    void Terminate();

    /// <summary>
    ///     Gets the number of host APIs visible to the initialized PortAudio runtime.
    /// </summary>
    /// <remarks>
    ///     This mirrors <c>Pa_GetHostApiCount</c> so callers can distinguish an empty runtime
    ///     from a missing preferred host API.
    /// </remarks>
    int HostApiCount { get; }

    /// <summary>
    ///     Gets the number of audio devices visible to the initialized PortAudio runtime.
    /// </summary>
    int DeviceCount { get; }

    /// <summary>
    ///     Resolves a stable PortAudio host-API type identifier to the runtime's current host-API
    ///     index.
    /// </summary>
    /// <param name="hostApiType">
    ///     The stable PortAudio host-API type identifier to resolve.
    /// </param>
    /// <returns>
    ///     The current host-API index when the runtime exposes that host API; otherwise,
    ///     <see langword="null"/>.
    /// </returns>
    int? FindHostApiIndex(PortAudioHostApiType hostApiType);

    /// <summary>
    ///     Gets descriptive information for one host API exposed by the current PortAudio runtime.
    /// </summary>
    /// <param name="hostApiIndex">
    ///     The runtime-specific host-API index to inspect.
    /// </param>
    /// <returns>
    ///     A managed description of the requested host API.
    /// </returns>
    PortAudioHostApiInfo GetHostApiInfo(int hostApiIndex);

    /// <summary>
    ///     Gets descriptive information for one audio device exposed by the current PortAudio
    ///     runtime.
    /// </summary>
    /// <param name="deviceIndex">
    ///     The runtime-specific device index to inspect.
    /// </param>
    /// <returns>
    ///     A managed description of the requested device.
    /// </returns>
    PortAudioDeviceInfo GetDeviceInfo(int deviceIndex);

    /// <summary>
    ///     Determines whether the given capture channel count and sample rate can actually be
    ///     opened on the specified input device.
    /// </summary>
    /// <param name="deviceIndex">The PortAudio device index to probe.</param>
    /// <param name="channelCount">The number of input channels to probe.</param>
    /// <param name="sampleRate">The sample rate, in Hz, to probe.</param>
    /// <returns>
    ///     <see langword="true"/> when the host API confirms the exact combination can be opened;
    ///     otherwise, <see langword="false"/>, including when the probe itself fails.
    /// </returns>
    bool IsCaptureFormatSupported(int deviceIndex, int channelCount, int sampleRate);

    /// <summary>
    ///     Determines whether the given playback channel count and sample rate can actually be
    ///     opened on the specified output device.
    /// </summary>
    /// <param name="deviceIndex">The PortAudio device index to probe.</param>
    /// <param name="channelCount">The number of output channels to probe.</param>
    /// <param name="sampleRate">The sample rate, in Hz, to probe.</param>
    /// <returns>
    ///     <see langword="true"/> when the host API confirms the exact combination can be opened;
    ///     otherwise, <see langword="false"/>, including when the probe itself fails.
    /// </returns>
    bool IsPlaybackFormatSupported(int deviceIndex, int channelCount, int sampleRate);

    /// <summary>
    ///     Opens a capture-only stream for the specified input device.
    /// </summary>
    /// <param name="deviceIndex">The PortAudio device index to open.</param>
    /// <param name="channelCount">The number of input channels to request.</param>
    /// <param name="sampleRate">The sample rate, in Hz, to request.</param>
    /// <param name="framesPerBuffer">
    ///     The callback block size to request, or <c>0</c> for PortAudio's unspecified size.
    /// </param>
    /// <param name="onSamplesCaptured">
    ///     Callback invoked for each captured interleaved sample block.
    /// </param>
    /// <returns>
    ///     A stream wrapper representing the opened native stream.
    /// </returns>
    IPortAudioStream OpenCaptureStream(
        int deviceIndex,
        int channelCount,
        int sampleRate,
        uint framesPerBuffer,
        Action<IReadOnlyList<float>> onSamplesCaptured);

    /// <summary>
    ///     Opens a playback-only stream for the specified output device.
    /// </summary>
    /// <param name="deviceIndex">The PortAudio device index to open.</param>
    /// <param name="channelCount">The number of output channels to request.</param>
    /// <param name="sampleRate">The sample rate, in Hz, to request.</param>
    /// <param name="framesPerBuffer">
    ///     The callback block size to request, or <c>0</c> for PortAudio's unspecified size.
    /// </param>
    /// <param name="provideSamples">
    ///     Callback that must return a <see cref="float"/> array containing exactly the requested
    ///     number of interleaved samples. When less real audio is available, the caller is
    ///     expected to zero-fill the remainder.
    /// </param>
    /// <returns>
    ///     A stream wrapper representing the opened native stream.
    /// </returns>
    IPortAudioStream OpenPlaybackStream(
        int deviceIndex,
        int channelCount,
        int sampleRate,
        uint framesPerBuffer,
        Func<int, float[]> provideSamples);
}
