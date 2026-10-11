namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;

/// <summary>
///     Chooses how a paralinguistic tag that would end a sentence (with no speech after it) is rendered.
/// </summary>
/// <remarks>
///     The model produces its non-verbal sounds reliably only when speech follows the tag; a tag at the very
///     end of a sentence is frequently dropped or triggers runaway generation. This policy selects the
///     workaround.
/// </remarks>
public enum ChatterboxTurboEndOfSentenceTagPolicy
{
    /// <summary>
    ///     Move the tag in front of the sentence's final word so speech follows it; when the sentence is too short,
    ///     fall back to the per-tag suffix and then to stripping (default).
    /// </summary>
    MoveBeforeFinalWord,

    /// <summary>Append a per-tag trailing suffix where one is known to work (laugh only); otherwise strip.</summary>
    Suffix,

    /// <summary>Drop the tag when no speech follows it.</summary>
    Strip,
}
