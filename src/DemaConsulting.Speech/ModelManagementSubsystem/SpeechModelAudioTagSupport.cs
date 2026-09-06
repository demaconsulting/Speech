namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Declares how (if at all) a model can honor inline Natural Language Audio Tags
///     (for example <c>[whispers]</c> or <c>[short pause]</c>) embedded in synthesis input text.
/// </summary>
/// <remarks>
///     This enum is the declaration shape only: it lets a model state what it supports so a
///     host can introspect a model's capability before use, and so
///     <see cref="SpeechModelCatalog"/>/UI code can reason about it generically. The actual
///     Layer 2 rendering logic that turns tagged spans into a per-model <c>SpeechPlan</c>
///     (passthrough, parameter-mapping, or stripping, per architecture.md's "two-layer tag
///     rendering" decision) is implemented per-model in Phase 4, not here.
/// </remarks>
public enum SpeechModelAudioTagSupport
{
    /// <summary>
    ///     The model has no inline-tag understanding; tags are stripped and only the plain
    ///     words are spoken.
    /// </summary>
    None,

    /// <summary>
    ///     The model does not understand inline tags natively, but some tags can be approximated
    ///     by mapping them onto one of the model's own declared tunable parameters for a segment
    ///     (for example <c>[fast]</c> raising a tempo parameter).
    /// </summary>
    ParameterMapped,

    /// <summary>
    ///     The model was trained to understand inline Natural Language Audio Tags directly and
    ///     they can be passed through verbatim.
    /// </summary>
    Native,
}
