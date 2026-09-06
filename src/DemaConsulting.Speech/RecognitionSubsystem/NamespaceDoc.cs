namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Streaming speech-to-text: the public recognizer contract and its composition root, and
///     the capture-to-engine pipeline that feeds a real speech-inference engine.
/// </summary>
/// <remarks>
///     Contains <see cref="ISpeechRecognizer"/> and its result/event types,
///     <see cref="SpeechRecognizerFactory"/> - the composition root that returns either a
///     working recognizer or the honest <see cref="UnavailableSpeechRecognizer"/> fallback - the
///     capture-to-engine pipeline with its <see cref="AudioFrameResampler"/> audio-format
///     converter, and the internal mockable speech-inference seam backed by
///     <see cref="SherpaOnnxSpeechRecognizer"/>.
/// </remarks>
internal static class NamespaceDoc
{
}
