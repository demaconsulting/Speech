namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Text-to-speech: the closed Natural Language Audio Tag vocabulary, the async
///     Engine/Session synthesis API, and the internal backend-agnostic native seam a model
///     supplies its own synthesis backend through.
/// </summary>
/// <remarks>
///     Contains the model-independent Layer 1 audio-tag parser (<see cref="AudioTagParser"/>,
///     <see cref="AudioTagCatalog"/>) alongside per-model Layer 2 rendering and sentence
///     chunking, the public <see cref="ISpeechSynthesizerEngine"/>/<see cref="ISynthesisSession"/>
///     Engine/Session API with its <see cref="SpeechSynthesizerFactory"/> composition root and
///     honest <see cref="UnavailableSpeechSynthesizerEngine"/>/<see cref="UnavailableSynthesisSession"/>
///     fallbacks, and the internal mockable native seam (<see cref="ISynthesisBackend"/>,
///     <see cref="ISynthesisBackendFactory"/>) backed by <see cref="SpeechSynthesizerEngine"/>,
///     <see cref="SynthesisSession"/>, and their playback-format converter. The concrete native
///     backend implementation (for example, the sibling <c>DemaConsulting.Speech.Sherpa</c>
///     package's sherpa-onnx backend) lives outside this namespace and is supplied by a model's
///     own <c>CreateBackend</c> implementation.
/// </remarks>
internal static class NamespaceDoc
{
}
