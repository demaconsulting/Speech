namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;

/// <summary>
///     Selects the precision of the three auxiliary graphs (token embedding, speech encoder, speech decoder).
/// </summary>
public enum ChatterboxTurboAuxiliaryModelVariant
{
    /// <summary>4-bit weight-quantized graphs (default; smallest download, quality verified acceptable).</summary>
    Q4,

    /// <summary>Half-precision graphs.</summary>
    Fp16,

    /// <summary>Full-precision graphs (largest download).</summary>
    Fp32,
}
