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
///     <para>
///         <b>Thread safety</b>: unlike <see cref="PortAudioEnvironment.Refresh"/>, which guards
///         its own state under an internal lock, this class's <see cref="_captureProbe"/>/
///         <see cref="_playbackProbe"/> backing fields are plain (non-<see langword="volatile"/>)
///         fields reassigned by <see cref="RefreshDevices"/> without any synchronization. A
///         caller that invokes <see cref="RefreshDevices"/> concurrently with another
///         <see cref="RefreshDevices"/> call, or with a concurrent read of <see cref="CaptureProbe"/>/
///         <see cref="PlaybackProbe"/>, from multiple threads without its own external
///         synchronization may observe a stale probe reference or an unpredictable interleaving
///         of the two calls. Callers that refresh from more than one thread must provide their
///         own external synchronization (for example, routing every refresh through one thread or
///         a caller-owned lock).
///     </para>
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
        _captureProbeIsExplicit = captureProbe is not null;
        _playbackProbeIsExplicit = playbackProbe is not null;
        var useRealPortAudio = _environment.IsInitialized;

        if (!useRealPortAudio)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Warning,
                "AudioSubsystem",
                $"PortAudio initialization failed; using unavailable audio fallbacks. {_environment.InitializationFailureMessage}");
        }

        _captureProbe = captureProbe ?? SelectCaptureProbe(useRealPortAudio);
        _playbackProbe = playbackProbe ?? SelectPlaybackProbe(useRealPortAudio);
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
    ///     Indicates whether the caller explicitly injected <see cref="CaptureProbe"/> at
    ///     construction, so <see cref="RefreshDevices"/> must not overwrite it.
    /// </summary>
    private readonly bool _captureProbeIsExplicit;

    /// <summary>
    ///     Indicates whether the caller explicitly injected <see cref="PlaybackProbe"/> at
    ///     construction, so <see cref="RefreshDevices"/> must not overwrite it.
    /// </summary>
    private readonly bool _playbackProbeIsExplicit;

    /// <summary>
    ///     The backing field for <see cref="CaptureProbe"/>, re-assigned by
    ///     <see cref="RefreshDevices"/> unless <see cref="_captureProbeIsExplicit"/>.
    /// </summary>
    private IAudioCaptureDeviceProbe _captureProbe;

    /// <summary>
    ///     The backing field for <see cref="PlaybackProbe"/>, re-assigned by
    ///     <see cref="RefreshDevices"/> unless <see cref="_playbackProbeIsExplicit"/>.
    /// </summary>
    private IAudioPlaybackDeviceProbe _playbackProbe;

    /// <summary>
    ///     Gets the probe used to enumerate available capture devices.
    /// </summary>
    public IAudioCaptureDeviceProbe CaptureProbe => _captureProbe;

    /// <summary>
    ///     Gets the probe used to enumerate available playback devices.
    /// </summary>
    public IAudioPlaybackDeviceProbe PlaybackProbe => _playbackProbe;

    /// <summary>
    ///     Forces the underlying PortAudio device table to be re-scanned so newly attached or
    ///     removed hardware becomes visible to subsequent <see cref="CaptureProbe"/>/
    ///     <see cref="PlaybackProbe"/> enumeration and to <see cref="CreateCaptureDevice"/>/
    ///     <see cref="CreatePlaybackDevice"/>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This is the one deliberate exception to this library's "never throws at
    ///         composition" policy: refreshing requires tearing down and reinitializing the native
    ///         PortAudio runtime, which cannot safely happen while any device created from this
    ///         factory's environment currently has an open/started stream.
    ///     </para>
    ///     <para>
    ///         Calling <see cref="IAudioCaptureDeviceProbe.Enumerate"/>/
    ///         <see cref="IAudioPlaybackDeviceProbe.Enumerate"/> on <see cref="CaptureProbe"/>/
    ///         <see cref="PlaybackProbe"/> alone, without first calling this method, will never
    ///         reveal newly attached or removed hardware: enumeration only reads the device table
    ///         as it stood at the last successful <see cref="RefreshDevices"/> call (or at
    ///         construction). To pick up a hot-plugged device, a caller must, in order: (1) stop
    ///         any active capture/playback streams so the refresh is not refused, (2) call
    ///         <see cref="RefreshDevices"/>, (3) re-enumerate <see cref="CaptureProbe"/>/
    ///         <see cref="PlaybackProbe"/>, and (4) update any UI or persisted device selection
    ///         from that fresh enumeration.
    ///     </para>
    ///     <para>
    ///         Probes supplied explicitly at construction (rather than defaulted) are never
    ///         replaced by this call, matching this factory's general policy of never overriding
    ///         an explicitly injected dependency. The practical consequence is that an explicitly
    ///         injected probe (for example, a mock or other custom <see cref="IAudioCaptureDeviceProbe"/>/
    ///         <see cref="IAudioPlaybackDeviceProbe"/> supplied at construction) will not reflect
    ///         newly attached or removed hardware after a successful <see cref="RefreshDevices"/>
    ///         call: this method only re-evaluates the PortAudio-backed default probes it selected
    ///         itself, never a caller-supplied instance. A caller that injects its own probe owns
    ///         making that probe reflect hardware changes.
    ///     </para>
    ///     <para>
    ///         A successful call also advances <see cref="PortAudioEnvironment.Generation"/> on
    ///         this factory's environment. Any <see cref="IAudioCaptureDevice"/>/
    ///         <see cref="IAudioPlaybackDevice"/> instance created before that point (via
    ///         <see cref="CreateCaptureDevice"/>/<see cref="CreatePlaybackDevice"/>) has its own
    ///         device index invalidated as a result and throws
    ///         <see cref="AudioDeviceUnavailableException"/> if <c>Start</c> is later called on
    ///         it; such an instance must be discarded and a replacement created afterward.
    ///     </para>
    /// </remarks>
    /// <exception cref="AudioDeviceInUseException">
    ///     Thrown when any capture/playback device created from this factory's environment
    ///     currently has an open/started stream. No probe or state is modified when this is
    ///     thrown. This exception exposes no structured list of the in-use device(s); any details
    ///     are contained only in its <see cref="Exception.Message"/>. The caller can resolve this
    ///     by stopping the active stream(s) via <see cref="IAudioCaptureDevice.Stop"/>/
    ///     <see cref="IAudioPlaybackDevice.Stop"/> and retrying.
    /// </exception>
    public void RefreshDevices()
    {
        _environment.Refresh();

        var useRealPortAudio = _environment.IsInitialized;
        if (!useRealPortAudio)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Warning,
                "AudioSubsystem",
                $"PortAudio refresh completed but initialization failed; using unavailable audio fallbacks. {_environment.InitializationFailureMessage}");
        }
        else
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Info,
                "AudioSubsystem",
                "PortAudio devices refreshed successfully.");
        }

        if (!_captureProbeIsExplicit)
        {
            _captureProbe = SelectCaptureProbe(useRealPortAudio);
        }

        if (!_playbackProbeIsExplicit)
        {
            _playbackProbe = SelectPlaybackProbe(useRealPortAudio);
        }
    }

    /// <summary>
    ///     Selects the default capture probe for the given PortAudio availability.
    /// </summary>
    /// <param name="useRealPortAudio">
    ///     <see langword="true"/> when PortAudio initialized successfully.
    /// </param>
    /// <returns>
    ///     A real PortAudio-backed probe bound to <see cref="_environment"/> when
    ///     <paramref name="useRealPortAudio"/> is <see langword="true"/>; otherwise,
    ///     <see cref="UnavailableAudioCaptureDeviceProbe.Instance"/>.
    /// </returns>
    private IAudioCaptureDeviceProbe SelectCaptureProbe(bool useRealPortAudio)
    {
        return useRealPortAudio
            ? new PortAudioCaptureDeviceProbe(_environment)
            : UnavailableAudioCaptureDeviceProbe.Instance;
    }

    /// <summary>
    ///     Selects the default playback probe for the given PortAudio availability.
    /// </summary>
    /// <param name="useRealPortAudio">
    ///     <see langword="true"/> when PortAudio initialized successfully.
    /// </param>
    /// <returns>
    ///     A real PortAudio-backed probe bound to <see cref="_environment"/> when
    ///     <paramref name="useRealPortAudio"/> is <see langword="true"/>; otherwise,
    ///     <see cref="UnavailableAudioPlaybackDeviceProbe.Instance"/>.
    /// </returns>
    private IAudioPlaybackDeviceProbe SelectPlaybackProbe(bool useRealPortAudio)
    {
        return useRealPortAudio
            ? new PortAudioPlaybackDeviceProbe(_environment)
            : UnavailableAudioPlaybackDeviceProbe.Instance;
    }

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
    ///     never reported. A <see langword="null"/>-or-empty <see cref="AudioDeviceSelection.DeviceName"/>
    ///     is treated the same as "no device requested" here, matching
    ///     <see cref="AudioDeviceSelection.Resolve"/>: an empty name can never exactly match a
    ///     real device, so <c>Resolve</c> always falls back to the system default for it, and
    ///     this check must fall back to "any known device" rather than demanding an impossible
    ///     exact match against an empty name.
    /// </remarks>
    private static bool IsSelectionKnownToProbe(
        IReadOnlyList<AudioDeviceDescription> knownDevices,
        AudioDeviceSelection? selection)
    {
        if (selection?.DeviceName is { Length: > 0 } deviceName)
        {
            return knownDevices.Any(device => string.Equals(device.Name, deviceName, StringComparison.Ordinal));
        }

        return knownDevices.Count > 0;
    }
}
