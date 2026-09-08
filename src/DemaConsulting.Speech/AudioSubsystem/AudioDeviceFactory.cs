using DemaConsulting.Speech.AudioSubsystem.PortAudio;
using DemaConsulting.Speech.Diagnostics;

namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Composition entry point for obtaining audio capture/playback devices and their
///     enumeration probes.
/// </summary>
/// <remarks>
///     Callers construct one <see cref="AudioDeviceFactory"/> and use it as the sole source of
///     <see cref="IAudioCaptureDevice"/>/<see cref="IAudioPlaybackDevice"/> instances, rather than
///     constructing device implementations directly, so device resolution and fallback logic lives
///     in one place. When PortAudio initializes successfully, the factory exposes real
///     PortAudio-backed probes and devices behind the public interfaces. When PortAudio itself
///     cannot initialize, the factory degrades to the honest <c>Unavailable*</c> fallbacks without
///     ever throwing at composition time.
/// </remarks>
public sealed class AudioDeviceFactory
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AudioDeviceFactory"/> class.
    /// </summary>
    /// <param name="captureProbe">
    ///     The capture device probe to expose via <see cref="CaptureProbe"/>, or
    ///     <see langword="null"/> to use the PortAudio-backed default probe when PortAudio
    ///     initializes successfully, otherwise <see cref="UnavailableAudioCaptureDeviceProbe.Instance"/>.
    /// </param>
    /// <param name="playbackProbe">
    ///     The playback device probe to expose via <see cref="PlaybackProbe"/>, or
    ///     <see langword="null"/> to use the PortAudio-backed default probe when PortAudio
    ///     initializes successfully, otherwise <see cref="UnavailableAudioPlaybackDeviceProbe.Instance"/>.
    /// </param>
    /// <param name="diagnostics">
    ///     The sink to report structural selection and fallback events to, or
    ///     <see langword="null"/> to use <see cref="NullSpeechDiagnostics.Instance"/>.
    /// </param>
    public AudioDeviceFactory(
        IAudioCaptureDeviceProbe? captureProbe = null,
        IAudioPlaybackDeviceProbe? playbackProbe = null,
        ISpeechDiagnostics? diagnostics = null)
        : this(captureProbe, playbackProbe, diagnostics, PortAudioEnvironment.Shared)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AudioDeviceFactory"/> class for tests that
    ///     need a deterministic PortAudio environment.
    /// </summary>
    internal AudioDeviceFactory(
        IAudioCaptureDeviceProbe? captureProbe,
        IAudioPlaybackDeviceProbe? playbackProbe,
        ISpeechDiagnostics? diagnostics,
        PortAudioEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        _diagnostics = diagnostics ?? NullSpeechDiagnostics.Instance;
        _environment = environment;
        var useRealPortAudio = _environment.IsInitialized;

        if (!useRealPortAudio)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Warning,
                "AudioSubsystem",
                $"PortAudio initialization failed; using unavailable audio fallbacks. {_environment.InitializationFailureMessage}");
        }

        CaptureProbe = captureProbe ??
            (useRealPortAudio
                ? new PortAudioCaptureDeviceProbe(_environment)
                : UnavailableAudioCaptureDeviceProbe.Instance);
        PlaybackProbe = playbackProbe ??
            (useRealPortAudio
                ? new PortAudioPlaybackDeviceProbe(_environment)
                : UnavailableAudioPlaybackDeviceProbe.Instance);
    }

    /// <summary>
    ///     The diagnostics sink used to report structural fallback and selection events.
    /// </summary>
    private readonly ISpeechDiagnostics _diagnostics;

    /// <summary>
    ///     The PortAudio environment whose initialization result controls real-backend availability.
    /// </summary>
    private readonly PortAudioEnvironment _environment;

    /// <summary>
    ///     Gets the probe used to enumerate available capture devices.
    /// </summary>
    public IAudioCaptureDeviceProbe CaptureProbe { get; }

    /// <summary>
    ///     Gets the probe used to enumerate available playback devices.
    /// </summary>
    public IAudioPlaybackDeviceProbe PlaybackProbe { get; }

    /// <summary>
    ///     Creates a capture device for the given selection.
    /// </summary>
    /// <param name="selection">
    ///     The persisted device selection to honor, or <see langword="null"/> to request the
    ///     host-API-scoped default input device.
    /// </param>
    /// <param name="preferredFormat">
    ///     An optional preferred capture format to request when the underlying device is opened.
    ///     When omitted, the resolved device's own default sample rate and full input-channel
    ///     capacity are requested exactly as before.
    /// </param>
    /// <returns>
    ///     A real PortAudio-backed capture device when PortAudio initialized successfully and the
    ///     requested <paramref name="selection"/> (or, when omitted, at least one device) is known
    ///     to <see cref="CaptureProbe"/>; otherwise, <see cref="UnavailableAudioCaptureDevice.Instance"/>.
    /// </returns>
    /// <remarks>
    ///     Never throws; reports backend initialization fallback via diagnostics. When
    ///     <paramref name="preferredFormat"/> is supplied and the backend honors it, the returned
    ///     device may report a <c>SampleRate</c> and <c>ChannelCount</c> different from the
    ///     hardware default. Consulting <see cref="CaptureProbe"/> before constructing the device
    ///     keeps device creation consistent with an injected probe (for example, a test double
    ///     with no known devices), rather than resolving the selection independently against the
    ///     real environment and silently ignoring the probe the caller supplied.
    /// </remarks>
    public IAudioCaptureDevice CreateCaptureDevice(
        AudioDeviceSelection? selection = null,
        AudioFormat? preferredFormat = null)
    {
        if (!_environment.IsInitialized)
        {
            return UnavailableAudioCaptureDevice.Instance;
        }

        if (!IsSelectionKnownToProbe(CaptureProbe.Enumerate(), selection))
        {
            return UnavailableAudioCaptureDevice.Instance;
        }

        return new PortAudioCaptureDevice(_environment, selection, _diagnostics, preferredFormat);
    }

    /// <summary>
    ///     Creates a playback device for the given selection.
    /// </summary>
    /// <param name="selection">
    ///     The persisted device selection to honor, or <see langword="null"/> to request the
    ///     host-API-scoped default output device.
    /// </param>
    /// <param name="preferredFormat">
    ///     An optional preferred playback format to request when the underlying device is opened.
    ///     When omitted, the resolved device's own default sample rate and full output-channel
    ///     capacity are requested exactly as before.
    /// </param>
    /// <returns>
    ///     A real PortAudio-backed playback device when PortAudio initialized successfully and the
    ///     requested <paramref name="selection"/> (or, when omitted, at least one device) is known
    ///     to <see cref="PlaybackProbe"/>; otherwise, <see cref="UnavailableAudioPlaybackDevice.Instance"/>.
    /// </returns>
    /// <remarks>
    ///     Never throws; reports backend initialization fallback via diagnostics. When
    ///     <paramref name="preferredFormat"/> is supplied and the backend honors it, the returned
    ///     device may report a <c>SampleRate</c> and <c>ChannelCount</c> different from the
    ///     hardware default. Consulting <see cref="PlaybackProbe"/> before constructing the device
    ///     keeps device creation consistent with an injected probe (for example, a test double
    ///     with no known devices), rather than resolving the selection independently against the
    ///     real environment and silently ignoring the probe the caller supplied.
    /// </remarks>
    public IAudioPlaybackDevice CreatePlaybackDevice(
        AudioDeviceSelection? selection = null,
        AudioFormat? preferredFormat = null)
    {
        if (!_environment.IsInitialized)
        {
            return UnavailableAudioPlaybackDevice.Instance;
        }

        if (!IsSelectionKnownToProbe(PlaybackProbe.Enumerate(), selection))
        {
            return UnavailableAudioPlaybackDevice.Instance;
        }

        return new PortAudioPlaybackDevice(_environment, selection, _diagnostics, preferredFormat);
    }

    /// <summary>
    ///     Determines whether a requested device selection (or, for the default selection, at
    ///     least one known device) is present in a probe's enumeration result.
    /// </summary>
    /// <param name="knownDevices">
    ///     The devices enumerated by <see cref="CaptureProbe"/> or <see cref="PlaybackProbe"/>.
    /// </param>
    /// <param name="selection">
    ///     The requested device selection, or <see langword="null"/> to request the default
    ///     device.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="selection"/> names a device present in
    ///     <paramref name="knownDevices"/>, or when <paramref name="selection"/> is
    ///     <see langword="null"/> (or names no device) and <paramref name="knownDevices"/> is not
    ///     empty; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///     This keeps <see cref="CreateCaptureDevice"/>/<see cref="CreatePlaybackDevice"/>
    ///     consistent with whichever probe was actually injected into this factory, instead of
    ///     resolving the selection independently against the real PortAudio environment: an
    ///     injected probe that reports zero devices (or does not know a named device) now yields
    ///     the honest unavailable fallback rather than a device resolved from hardware the probe
    ///     never reported.
    /// </remarks>
    private static bool IsSelectionKnownToProbe(
        IReadOnlyList<AudioDeviceDescription> knownDevices,
        AudioDeviceSelection? selection)
    {
        if (selection?.DeviceName is { } deviceName)
        {
            return knownDevices.Any(device => string.Equals(device.Name, deviceName, StringComparison.Ordinal));
        }

        return knownDevices.Count > 0;
    }
}
