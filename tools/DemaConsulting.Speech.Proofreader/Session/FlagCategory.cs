namespace DemaConsulting.Speech.Proofreader.Session;

/// <summary>
///     The kind of problem a listener flags in a sentence.
/// </summary>
internal enum FlagCategory
{
    /// <summary>A word with several readings was read with the wrong one.</summary>
    WrongHeteronym,

    /// <summary>A single word was pronounced incorrectly.</summary>
    MispronouncedWord,

    /// <summary>A number, date, abbreviation, or symbol was expanded incorrectly.</summary>
    Normalization,

    /// <summary>A word was skipped or an extra word was spoken.</summary>
    MissingOrExtraWord,

    /// <summary>The rhythm, stress, or pausing sounded wrong.</summary>
    ProsodyOrPause,

    /// <summary>The voice produced an artifact such as a click, noise, or slurred sound.</summary>
    VoiceGlitch,

    /// <summary>Anything else; explain it in the note.</summary>
    Other,
}

/// <summary>
///     Display and export names for <see cref="FlagCategory"/> values.
/// </summary>
internal static class FlagCategories
{
    /// <summary>Every category in the order the editor offers them.</summary>
    public static IReadOnlyList<FlagCategory> All { get; } = Enum.GetValues<FlagCategory>();

    /// <summary>
    ///     Gets the label shown to the user.
    /// </summary>
    /// <param name="category">The category.</param>
    /// <returns>A short human-readable label.</returns>
    public static string Label(FlagCategory category) => category switch
    {
        FlagCategory.WrongHeteronym => "Wrong heteronym",
        FlagCategory.MispronouncedWord => "Mispronounced word",
        FlagCategory.Normalization => "Normalization",
        FlagCategory.MissingOrExtraWord => "Missing / extra word",
        FlagCategory.ProsodyOrPause => "Prosody / pause",
        FlagCategory.VoiceGlitch => "Voice glitch",
        _ => "Other",
    };

    /// <summary>
    ///     Gets the stable machine-readable key used in the TSV export.
    /// </summary>
    /// <param name="category">The category.</param>
    /// <returns>A lower-case kebab-case key that never changes between releases of the tool.</returns>
    public static string Key(FlagCategory category) => category switch
    {
        FlagCategory.WrongHeteronym => "wrong-heteronym",
        FlagCategory.MispronouncedWord => "mispronounced-word",
        FlagCategory.Normalization => "normalization",
        FlagCategory.MissingOrExtraWord => "missing-or-extra-word",
        FlagCategory.ProsodyOrPause => "prosody-or-pause",
        FlagCategory.VoiceGlitch => "voice-glitch",
        _ => "other",
    };
}
