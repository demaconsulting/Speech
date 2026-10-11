namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;

/// <summary>
///     Selects which precision of the autoregressive language-model graph is downloaded and run.
/// </summary>
/// <remarks>
///     Only the two variants that preserve the model's paralinguistic tags are representable. The 4-bit
///     (<c>q4</c>) and 8-bit (<c>quantized</c>) language-model graphs are deliberately absent: the 4-bit
///     graph destroys the inline tags and the 8-bit graph is slower than the half-precision graph on CPU
///     without a quality benefit, so offering them would only invite a worse configuration.
/// </remarks>
public enum ChatterboxTurboLanguageModelVariant
{
    /// <summary>Half-precision weights (default; about 635 MB, float16 key/value cache).</summary>
    Fp16,

    /// <summary>Full-precision weights (about 1.27 GB, float32 key/value cache).</summary>
    Fp32,
}
