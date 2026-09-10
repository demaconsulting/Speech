// cspell:ignore Alsa ALSA portaudio
using System.Runtime.InteropServices;

namespace DemaConsulting.Speech.AudioSubsystem.PortAudio;

/// <summary>
///     Holds the current PortAudio runtime seam and caches one initialization attempt so callers
///     can compose probes and devices without risking composition-time exceptions.
/// </summary>
/// <remarks>
///     Tests construct their own instances around fake <see cref="IPortAudioApi"/> implementations.
///     Production code uses a shared instance backed by the real PortAudio runtime wrapper.
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
    ///     The PortAudio seam implementation used by this environment.
    /// </summary>
    private readonly IPortAudioApi _api;

    /// <summary>
    ///     The cached one-time initialization result.
    /// </summary>
    private readonly Lazy<PortAudioInitializationState> _initializationState;

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
