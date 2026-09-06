namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Describes a model's current install lifecycle state, as reported by
///     <see cref="SpeechModelCatalog"/>.
/// </summary>
/// <remarks>
///     Per this library's "model states are a fixed, small set" decision, there is no
///     "update available" state in this iteration - model versioning is an explicit non-goal.
///     <see cref="SpeechModelStore"/> itself only distinguishes installed/not-installed (via
///     <see cref="SpeechModelStore.IsInstalled"/>); <see cref="SpeechModelCatalog"/> layers
///     <see cref="Downloading"/> and <see cref="FailedOrCorrupt"/> on top by also tracking
///     in-flight and most-recently-failed download attempts, since a bare on-disk store has no
///     durable concept of "currently downloading" or "last attempt failed".
/// </remarks>
public enum SpeechModelState
{
    /// <summary>The model has never been successfully installed, and no download is in progress.</summary>
    NotDownloaded,

    /// <summary>A download for this model is currently in progress.</summary>
    Downloading,

    /// <summary>The model is installed and verified, ready to use.</summary>
    Downloaded,

    /// <summary>
    ///     The most recent download/install attempt failed (for example a checksum mismatch or
    ///     transport failure), or a prior install was left in a corrupt, incomplete state.
    /// </summary>
    FailedOrCorrupt,
}
