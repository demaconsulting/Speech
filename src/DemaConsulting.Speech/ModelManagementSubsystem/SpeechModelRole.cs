namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Identifies which speech capability a model provides.
/// </summary>
/// <remarks>
///     In this library, this is the seam <see cref="IRecognitionModel"/> and
///     <see cref="ISynthesisModel"/> let <see cref="SpeechModelCatalog"/> report and filter
///     models by role without knowing anything about a model's engine-specific configuration -
///     the real recognition/synthesis construction lives behind those role-specific
///     interfaces, so this enum never needs to change.
/// </remarks>
public enum SpeechModelRole
{
    /// <summary>The model performs streaming speech-to-text (STT) recognition.</summary>
    Recognition,

    /// <summary>The model performs text-to-speech (TTS) synthesis.</summary>
    Synthesis,
}
