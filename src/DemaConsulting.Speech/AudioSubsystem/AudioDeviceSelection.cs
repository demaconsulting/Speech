namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Immutable, persistable reference to a chosen audio device, identified by name alone.
/// </summary>
/// <remarks>
///     A host persists an <see cref="AudioDeviceSelection"/> (e.g. in application settings) and
///     resolves it against a probe's current enumeration each time a device is needed.
///     Resolution never throws: per this library's "nothing throws at composition" decision,
///     a saved selection that no longer matches any currently enumerated device silently
///     resolves to <see langword="null"/> (meaning "fall back to the system default") rather than
///     raising an error, since a previously-selected device disappearing (unplugged, renamed) is
///     an ordinary machine-state change, not a programming error.
/// </remarks>
/// <param name="DeviceName">
///     The persisted device name to resolve, or <see langword="null"/> to explicitly request the
///     system default device. See <see cref="SystemDefault"/> for the canonical instance
///     representing this case.
/// </param>
public sealed record AudioDeviceSelection(string? DeviceName)
{
    /// <summary>
    ///     Gets the canonical selection representing "use the system default device", for
    ///     callers that want to express default-device intent explicitly rather than passing
    ///     <see langword="null"/> for <see cref="DeviceName"/> directly.
    /// </summary>
    public static AudioDeviceSelection SystemDefault { get; } = new((string?)null);

    /// <summary>
    ///     Resolves this selection against a probe's current device enumeration.
    /// </summary>
    /// <param name="availableDevices">
    ///     The devices currently reported by an <see cref="IAudioCaptureDeviceProbe"/> or
    ///     <see cref="IAudioPlaybackDeviceProbe"/>. Must not be <see langword="null"/>, but may
    ///     be empty.
    /// </param>
    /// <returns>
    ///     The <see cref="AudioDeviceDescription"/> in <paramref name="availableDevices"/> whose
    ///     <see cref="AudioDeviceDescription.Name"/> exactly matches <see cref="DeviceName"/>, or
    ///     <see langword="null"/> when <see cref="DeviceName"/> is <see langword="null"/> or no
    ///     matching device is currently enumerated - both cases mean "fall back to the system
    ///     default" and are treated identically by callers.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="availableDevices"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///     This method never throws for a stale or unmatched selection; see the class remarks.
    ///     Matching is an exact, ordinal string comparison since device names are opaque
    ///     identifiers, not user-facing text requiring culture-aware comparison.
    /// </remarks>
    public AudioDeviceDescription? Resolve(IReadOnlyList<AudioDeviceDescription> availableDevices)
    {
        // A null device list would make "no match found" indistinguishable from "not asked" -
        // reject it explicitly rather than silently treating it as "no devices available".
        ArgumentNullException.ThrowIfNull(availableDevices);

        // A null DeviceName always means "use the system default" - there is nothing to match.
        if (DeviceName is null)
        {
            return null;
        }

        // Fall back to null (system default) rather than throwing when the persisted name no
        // longer matches any currently enumerated device.
        return availableDevices.FirstOrDefault(
            device => string.Equals(device.Name, DeviceName, StringComparison.Ordinal));
    }
}
