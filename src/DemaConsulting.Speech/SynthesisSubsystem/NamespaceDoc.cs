namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Text-to-speech: the closed Natural Language Audio Tag vocabulary, the streaming
///     synthesis/playback pipeline, and the real sherpa-onnx synthesis engine.
/// </summary>
/// <remarks>
///     Contains the model-independent Layer 1 audio-tag parser (<see cref="AudioTagParser"/>,
///     <see cref="AudioTagCatalog"/>) alongside per-model Layer 2 rendering and sentence
///     chunking, the public <see cref="ISpeechSynthesizer"/> streaming/playback pipeline with its
///     <see cref="SpeechSynthesizerFactory"/> composition root and honest
///     <see cref="UnavailableSpeechSynthesizer"/> fallback, and the internal mockable synthesis
///     seam backed by <see cref="SherpaOnnxSpeechSynthesizer"/> and its playback-format
///     converter.
/// </remarks>
internal static class NamespaceDoc
{
}
