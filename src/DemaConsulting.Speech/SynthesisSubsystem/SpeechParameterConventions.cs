namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     The conservative, built-in naming conventions <see cref="DefaultModelCapabilityProfile"/>
///     and <see cref="SherpaOnnxSpeechSynthesizer"/> both use to recognize which of a model's own
///     declared numeric parameters (if any) conventionally controls speaking rate or output
///     volume.
/// </summary>
/// <remarks>
///     Kept as its own tiny, shared lookup so both sides of the convention - "which declared
///     parameter should a pace/volume tag adjust" and "how should an adjusted value actually be
///     applied when generating audio" - stay in agreement without duplicating the keyword lists.
///     No model backing class ships in this phase to validate the convention against; it is
///     deliberately conservative and silently no-ops for any model whose declared parameter ids
///     do not match, per the approved plan's risk mitigation.
/// </remarks>
internal static class SpeechParameterConventions
{
    /// <summary>
    ///     Parameter id substrings (matched case-insensitively) conventionally understood to
    ///     control speaking rate, used to map <see cref="NaturalLanguageAudioTagKind.Pace"/> tags.
    /// </summary>
    internal static readonly IReadOnlyList<string> SpeedKeywords = ["tempo", "rate", "speed"];

    /// <summary>
    ///     Parameter id substrings (matched case-insensitively) conventionally understood to
    ///     control output volume, used to map
    ///     <see cref="NaturalLanguageAudioTagKind.DeliveryVolume"/> tags.
    /// </summary>
    internal static readonly IReadOnlyList<string> VolumeKeywords = ["volume", "loudness", "gain"];

    /// <summary>
    ///     Determines whether a declared parameter id conventionally controls speaking rate.
    /// </summary>
    /// <param name="parameterId">The declared parameter id to test.</param>
    /// <returns><see langword="true"/> when the id matches the speed convention.</returns>
    internal static bool IsSpeedParameter(string parameterId) =>
        SpeedKeywords.Any(keyword => parameterId.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     Determines whether a declared parameter id conventionally controls output volume.
    /// </summary>
    /// <param name="parameterId">The declared parameter id to test.</param>
    /// <returns><see langword="true"/> when the id matches the volume convention.</returns>
    internal static bool IsVolumeParameter(string parameterId) =>
        VolumeKeywords.Any(keyword => parameterId.Contains(keyword, StringComparison.OrdinalIgnoreCase));
}
