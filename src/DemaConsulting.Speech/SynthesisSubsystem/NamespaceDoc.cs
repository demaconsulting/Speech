namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Text-to-speech: the closed Natural Language Audio Tag vocabulary, the async
///     Engine/Session synthesis API, and the real sherpa-onnx synthesis backend.
/// </summary>
/// <remarks>
///     Contains the model-independent Layer 1 audio-tag parser (<see cref="AudioTagParser"/>,
///     <see cref="AudioTagCatalog"/>) alongside per-model Layer 2 rendering and sentence
///     chunking, the public <see cref="ISpeechSynthesizerEngine"/>/<see cref="ISynthesisSession"/>
///     Engine/Session API with its <see cref="SpeechSynthesizerFactory"/> composition root and
///     honest <see cref="UnavailableSpeechSynthesizerEngine"/>/<see cref="UnavailableSynthesisSession"/>
///     fallbacks, and the internal mockable native seam (<see cref="ISynthesisBackend"/>,
///     <see cref="ISynthesisBackendFactory"/>) backed by <see cref="SherpaOnnxSpeechSynthesizerEngine"/>,
///     <see cref="SherpaOnnxSynthesisSession"/>, and their playback-format converter.
/// </remarks>
internal static class NamespaceDoc
{
}
