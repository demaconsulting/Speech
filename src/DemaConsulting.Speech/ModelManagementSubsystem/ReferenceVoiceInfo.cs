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
/// <param name="Mood">The mood of a speaker-and-mood variant clip; <see langword="null"/> for a plain voice. A pack voice keeps this <see langword="null"/>; its mood variants are in <paramref name="MoodClips"/>.</param>
/// <param name="LicenseName">The license of a pack voice; otherwise <see langword="null"/>.</param>
/// <param name="Attribution">The attribution line of a pack voice; otherwise <see langword="null"/>.</param>
/// <param name="MoodClips">
///     The emotion variants of this voice as mood name (lower case, for example <c>sad</c>, see
///     <see cref="ReferenceVoiceInfo.KnownMoods"/>) to absolute audio file path, sorted by mood name; <see langword="null"/>
///     when the voice has no mood clips (every user and directory voice, and a pack installed without emotions). The
///     moods are variants of the same speaker, not separate voices, so they never appear as voices of their own.
/// </param>
public sealed record ReferenceVoiceInfo(
    string Id,
    string DisplayName,
    string FilePath,
    ReferenceVoiceSource Source,
    string? PackId,
    string Speaker,
    string? Mood = null,
    string? LicenseName = null,
    string? Attribution = null,
    IReadOnlyDictionary<string, string>? MoodClips = null)
{
    /// <summary>
    ///     Gets the mood names the built-in voice-zero pack provides for every speaker, in ordinal order:
    ///     <c>anger</c>, <c>calm</c>, <c>confused</c>, <c>enthused</c>, <c>excited</c>, <c>frustrated</c>, <c>happy</c>,
    ///     <c>neutral</c>, <c>sad</c>, <c>shout</c>, <c>surprised</c>, <c>tired</c>, <c>worried</c>.
    /// </summary>
    public static IReadOnlyList<string> KnownMoods { get; } =
    [
        "anger",
        "calm",
        "confused",
        "enthused",
        "excited",
        "frustrated",
        "happy",
        "neutral",
        "sad",
        "shout",
        "surprised",
        "tired",
        "worried",
    ];
}
