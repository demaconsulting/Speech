namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;

/// <summary>
///     How one library tag is rendered for Chatterbox Turbo.
/// </summary>
internal enum ChatterboxTurboTagKind
{
    /// <summary>Rendered as one of the model's own bracketed tokens, inline.</summary>
    Native,

    /// <summary>Approximated by real inserted silence (the model has no usable token).</summary>
    Pause,

    /// <summary>Dropped; the surrounding words are still spoken.</summary>
    Strip,
}
