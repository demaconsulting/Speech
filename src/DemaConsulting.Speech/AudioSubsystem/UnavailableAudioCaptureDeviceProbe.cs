namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Honest fallback <see cref="IAudioCaptureDeviceProbe"/> used when no real capture backend
///     is available, always reporting zero devices rather than throwing.
/// </summary>
/// <remarks>
///     Per architecture.md's "nothing throws at composition" decision, a probe that cannot
///     enumerate real hardware returns an empty list instead of failing, so callers can always
///     treat "no devices" and "backend unavailable" identically. The type is stateless and holds
///     no resources, so the shared <see cref="Instance"/> is safe for concurrent use by any
///     number of callers.
/// </remarks>
public sealed class UnavailableAudioCaptureDeviceProbe : IAudioCaptureDeviceProbe
{
    /// <summary>
    ///     Prevents external construction; callers use the shared <see cref="Instance"/> instead
    ///     since the type carries no state and multiple instances would provide no value.
    /// </summary>
    private UnavailableAudioCaptureDeviceProbe()
    {
    }

    /// <summary>
    ///     Gets the single shared unavailable capture device probe.
    /// </summary>
    public static UnavailableAudioCaptureDeviceProbe Instance { get; } = new();

    /// <inheritdoc/>
    /// <remarks>Always returns an empty list; never throws.</remarks>
    public IReadOnlyList<AudioDeviceDescription> Enumerate()
    {
        return [];
    }
}
