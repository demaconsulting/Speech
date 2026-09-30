// cspell:ignore Alsa ALSA portaudio
using System.Runtime.InteropServices;

namespace DemaConsulting.Speech.AudioSubsystem.PortAudio;

/// <summary>
///     Holds the current PortAudio runtime seam and caches an initialization attempt so callers
///     can compose probes and devices without risking composition-time exceptions.
/// </summary>
/// <remarks>
///     Tests construct their own instances around fake <see cref="IPortAudioApi"/> implementations.
///     Production code uses a shared instance backed by the real PortAudio runtime wrapper. The
///     cached initialization result is re-created on demand by <see cref="Refresh"/>, which also
///     tracks currently active (started) streams via <see cref="RegisterActiveStream"/>/
///     <see cref="UnregisterActiveStream"/> so a refresh can refuse to tear down a live stream.
/// </remarks>
internal sealed class PortAudioEnvironment
{
    /// <summary>
    ///     Gets the shared production PortAudio environment backed by the real PortAudio runtime.
    /// </summary>
    internal static PortAudioEnvironment Shared { get; } = new(PortAudioApi.Instance, DetectCurrentPlatform());

    /// <summary>
    ///     Initializes a new instance of the <see cref="PortAudioEnvironment"/> class.
    /// </summary>
    /// <param name="api">The PortAudio seam implementation to use.</param>
    /// <param name="currentPlatform">
    ///     The operating-system platform that determines the preferred host API.
    /// </param>
    internal PortAudioEnvironment(IPortAudioApi api, OSPlatform currentPlatform)
    {
        ArgumentNullException.ThrowIfNull(api);

        _api = api;
        CurrentPlatform = currentPlatform;
        _initializationState = new Lazy<PortAudioInitializationState>(Initialize, true);
    }

    /// <summary>
    ///     Synchronizes the active-stream registry and <see cref="Refresh"/> so a refresh cannot
    ///     tear down the native runtime while a stream registered before it began is still active.
    /// </summary>
    private readonly object _syncRoot = new();

    /// <summary>
    ///     The set of currently active (started) streams, keyed by the owning device instance
    ///     (reference identity), with each value holding the resolved device name used to build a
    ///     refusal message. Guarded by <see cref="_syncRoot"/>.
    /// </summary>
    private readonly Dictionary<object, string> _activeStreams = new();

    /// <summary>
    ///     The PortAudio seam implementation used by this environment.
    /// </summary>
    private readonly IPortAudioApi _api;

    /// <summary>
    ///     The cached initialization result, re-created by <see cref="Refresh"/>. Reads and writes
    ///     are guarded by <see cref="_syncRoot"/> for writes; volatile so unsynchronized readers
    ///     (<see cref="IsInitialized"/>, <see cref="InitializationFailureMessage"/>,
    ///     <see cref="TryResolvePreferredHostApi"/>) observe a reassigned instance promptly.
    /// </summary>
    private volatile Lazy<PortAudioInitializationState> _initializationState;

    /// <summary>
    ///     Gets the current operating-system platform associated with this environment.
    /// </summary>
    internal OSPlatform CurrentPlatform { get; }

    /// <summary>
    ///     Gets the PortAudio seam implementation used by this environment.
    /// </summary>
    internal IPortAudioApi Api => _api;

    /// <summary>
    ///     Gets a value indicating whether the PortAudio runtime initialized successfully.
    /// </summary>
    internal bool IsInitialized => _initializationState.Value.IsInitialized;

    /// <summary>
    ///     Gets the initialization-failure message when <see cref="IsInitialized"/> is
    ///     <see langword="false"/>; otherwise, <see langword="null"/>.
    /// </summary>
    internal string? InitializationFailureMessage => _initializationState.Value.FailureMessage;

    /// <summary>
    ///     Resolves the single preferred PortAudio host API for one platform.
    /// </summary>
    /// <param name="platform">The operating-system platform to map.</param>
    /// <returns>
    ///     The preferred host API for the platform, or <see langword="null"/> when the platform
    ///     is outside this library's supported desktop set.
    /// </returns>
    internal static PortAudioHostApiType? ResolvePreferredHostApiType(OSPlatform platform)
    {
        if (platform == OSPlatform.Windows)
        {
            return PortAudioHostApiType.Wasapi;
        }

        if (platform == OSPlatform.Linux)
        {
            return PortAudioHostApiType.Alsa;
        }

        if (platform == OSPlatform.OSX)
        {
            return PortAudioHostApiType.CoreAudio;
        }

        return null;
    }

    /// <summary>
    ///     Resolves the current platform's preferred host API to the runtime's current host-API
    ///     index and metadata.
    /// </summary>
    /// <param name="hostApiIndex">
    ///     Receives the runtime-specific host-API index when resolution succeeds.
    /// </param>
    /// <param name="hostApiInfo">
    ///     Receives the metadata for the resolved host API when resolution succeeds.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when the runtime initialized successfully and exposes the
    ///     preferred host API for the current platform; otherwise, <see langword="false"/>.
    /// </returns>
    internal bool TryResolvePreferredHostApi(out int hostApiIndex, out PortAudioHostApiInfo hostApiInfo)
    {
        hostApiIndex = default;
        hostApiInfo = default!;

        if (!IsInitialized)
        {
            return false;
        }

        var preferredHostApiType = ResolvePreferredHostApiType(CurrentPlatform);
        if (!preferredHostApiType.HasValue)
        {
            return false;
        }

        var resolvedIndex = _api.FindHostApiIndex(preferredHostApiType.Value);
        if (!resolvedIndex.HasValue)
        {
            return false;
        }

        hostApiIndex = resolvedIndex.Value;
        hostApiInfo = _api.GetHostApiInfo(hostApiIndex);
        return true;
    }

    /// <summary>
    ///     Registers one device instance as currently holding an active (started) stream, so a
    ///     concurrent or later <see cref="Refresh"/> refuses to tear down the native runtime.
    /// </summary>
    /// <param name="owner">
    ///     The device instance registering itself, used only as a reference-identity key.
    /// </param>
    /// <param name="deviceName">
    ///     The resolved device name, used to build a <see cref="AudioDeviceInUseException"/>
    ///     message if a refresh is refused while this registration is active.
    /// </param>
    internal void RegisterActiveStream(object owner, string deviceName)
    {
        lock (_syncRoot)
        {
            _activeStreams[owner] = deviceName;
        }
    }

    /// <summary>
    ///     Unregisters one device instance previously registered via
    ///     <see cref="RegisterActiveStream"/>, once its stream has stopped or failed to start.
    /// </summary>
    /// <param name="owner">
    ///     The device instance unregistering itself, matching the key previously passed to
    ///     <see cref="RegisterActiveStream"/>.
    /// </param>
    internal void UnregisterActiveStream(object owner)
    {
        lock (_syncRoot)
        {
            _activeStreams.Remove(owner);
        }
    }

    /// <summary>
    ///     Forces PortAudio to re-scan its device table by terminating and reinitializing the
    ///     native runtime, so newly attached/removed hardware becomes visible to subsequent
    ///     device enumeration and creation.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Thread safety: refusal/re-initialization is performed atomically under this
    ///         environment's internal lock, so a stream registered before this call began can
    ///         never be torn down by it. This call does not, however, provide linearizability
    ///         against concurrent <c>Enumerate()</c>/device-creation calls that do not take the
    ///         same lock; those may observe a transient mix of pre- and post-refresh native state
    ///         when racing a concurrent <see cref="Refresh"/>.
    ///     </para>
    ///     <para>
    ///         If the underlying <see cref="IPortAudioApi.Terminate"/> call itself throws, this
    ///         environment's cached initialization state is left untouched (still reporting the
    ///         previous successful result) even though the native runtime may have partially torn
    ///         down - a narrow, expected-to-be-rare edge case that this method intentionally does
    ///         not attempt to paper over with speculative recovery logic.
    ///     </para>
    /// </remarks>
    /// <exception cref="AudioDeviceInUseException">
    ///     Thrown when any capture/playback device created from this environment currently has an
    ///     open/started stream. The message identifies the distinct in-use device name(s).
    /// </exception>
    /// <exception cref="Exception">
    ///     Propagated when the underlying <see cref="IPortAudioApi.Terminate"/> call itself
    ///     throws (see the remarks above for the resulting cached-state guarantee). The
    ///     subsequent re-initialization attempt made by <see cref="Initialize"/> never throws;
    ///     any native initialization fault is instead captured and reported through
    ///     <see cref="IsInitialized"/>/<see cref="InitializationFailureMessage"/>.
    /// </exception>
    internal void Refresh()
    {
        lock (_syncRoot)
        {
            if (_activeStreams.Count > 0)
            {
                var inUseDeviceNames = _activeStreams.Values.Distinct(StringComparer.Ordinal).ToArray();
                throw new AudioDeviceInUseException(
                    "Cannot refresh PortAudio devices while a stream is active on: " +
                    string.Join(", ", inUseDeviceNames) +
                    ". Stop the device(s) and retry.");
            }

            if (_initializationState.IsValueCreated && _initializationState.Value.IsInitialized)
            {
                _api.Terminate();
            }

            _initializationState = new Lazy<PortAudioInitializationState>(Initialize, true);
        }
    }

    /// <summary>
    ///     Performs the environment's one-time PortAudio initialization attempt.
    /// </summary>
    /// <returns>
    ///     A cached success/failure result that callers can inspect without handling
    ///     exceptions themselves.
    /// </returns>
    private PortAudioInitializationState Initialize()
    {
        try
        {
            _api.Initialize();
            return PortAudioInitializationState.Success;
        }
        catch (Exception ex)
        {
            // Intentionally broad: initialization is the managed boundary around native
            // PortAudio startup, so any interop fault must be cached as unavailability.
            return new PortAudioInitializationState(false, ex.Message);
        }
    }

    /// <summary>
    ///     Detects the current operating system so the shared production environment can choose
    ///     the corresponding preferred PortAudio host API.
    /// </summary>
    /// <returns>
    ///     The current desktop platform token when recognized; otherwise, an <c>UNKNOWN</c>
    ///     token that intentionally resolves to no preferred host API.
    /// </returns>
    private static OSPlatform DetectCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return OSPlatform.Windows;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return OSPlatform.Linux;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return OSPlatform.OSX;
        }

        return OSPlatform.Create("UNKNOWN");
    }

    /// <summary>
    ///     Immutable cached result of one PortAudio initialization attempt.
    /// </summary>
    /// <param name="IsInitialized">
    ///     Indicates whether initialization succeeded.
    /// </param>
    /// <param name="FailureMessage">
    ///     The failure message when initialization did not succeed; otherwise,
    ///     <see langword="null"/>.
    /// </param>
    private sealed record PortAudioInitializationState(bool IsInitialized, string? FailureMessage)
    {
        /// <summary>
        ///     Gets the canonical successful initialization result.
        /// </summary>
        internal static PortAudioInitializationState Success { get; } = new(true, null);
    }
}
