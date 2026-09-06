namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Host-configurable options controlling where <see cref="SpeechModelStore"/> keeps its
///     per-user model files on disk.
/// </summary>
/// <remarks>
///     Per this library's "simplified single-root model storage" decision, a host almost
///     never needs to supply this type - the default (per-user
///     <see cref="Environment.SpecialFolder.LocalApplicationData"/>) is correct for the vast
///     majority of desktop applications. <see cref="RootPathOverride"/> exists only for the
///     minority of hosts with their own storage-location policy (e.g. portable installs,
///     integration tests), so it stays an explicit opt-in rather than a required constructor
///     argument.
/// </remarks>
public sealed class SpeechModelStoreOptions
{
    /// <summary>Initializes a new instance of the <see cref="SpeechModelStoreOptions"/> class.</summary>
    public SpeechModelStoreOptions()
    {
    }

    /// <summary>
    ///     Gets or sets an absolute directory path to use as the model store root instead of the
    ///     default per-user <see cref="Environment.SpecialFolder.LocalApplicationData"/> location.
    /// </summary>
    /// <remarks>
    ///     When <see langword="null"/> (the default), <see cref="SpeechModelStore"/> resolves its
    ///     root to a "DemaConsulting.Speech/Models" folder under
    ///     <see cref="Environment.SpecialFolder.LocalApplicationData"/>. The override, when
    ///     supplied, is used verbatim as the store root - it is the caller's responsibility to
    ///     supply a writable, per-installation-appropriate path. The directory is created on
    ///     first use if it does not already exist; supplying a non-writable path surfaces as a
    ///     first-use I/O failure from whichever store operation needed to write, not from
    ///     constructing this options object or the store itself.
    /// </remarks>
    public string? RootPathOverride { get; init; }
}
