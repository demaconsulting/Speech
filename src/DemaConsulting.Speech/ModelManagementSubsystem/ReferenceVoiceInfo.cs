namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Metadata about one reference-voice clip found by <see cref="ReferenceVoiceLibrary"/>; the audio itself is not
///     loaded.
/// </summary>
/// <param name="Id">The stable identifier: the file name without its extension.</param>
/// <param name="DisplayName">A human-readable name derived from <paramref name="Id"/> (for example <c>Alan Davis Drake 2</c>).</param>
/// <param name="FilePath">The absolute path of the audio file.</param>
/// <param name="Source">Where the clip came from.</param>
/// <param name="PackId">The owning pack's id for <see cref="ReferenceVoiceSource.Pack"/> voices; otherwise <see langword="null"/>.</param>
/// <param name="Speaker">The speaker this clip belongs to (the id for a plain voice).</param>
/// <param name="Mood">The mood of a speaker-and-mood variant clip; <see langword="null"/> for a plain voice (reserved for future packs).</param>
/// <param name="LicenseName">The license of a pack voice; otherwise <see langword="null"/>.</param>
/// <param name="Attribution">The attribution line of a pack voice; otherwise <see langword="null"/>.</param>
public sealed record ReferenceVoiceInfo(
    string Id,
    string DisplayName,
    string FilePath,
    ReferenceVoiceSource Source,
    string? PackId,
    string Speaker,
    string? Mood = null,
    string? LicenseName = null,
    string? Attribution = null);
