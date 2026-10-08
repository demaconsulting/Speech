namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Streaming speech-to-text: the public Engine/Session contract and its composition root, and
///     the capture-to-backend pipeline that feeds a real speech-inference backend.
/// </summary>
/// <remarks>
///     Contains <see cref="ISpeechRecognizerEngine"/> (the loaded, expensive, model-bound
///     composition unit) and <see cref="IRecognitionSession"/> (the cheap, single-use,
///     device-bound streaming unit) with their result/event/state types,
///     <see cref="SpeechRecognizerFactory"/> - the composition root that returns either a
///     working engine or the honest <see cref="UnavailableSpeechRecognizerEngine"/> fallback (with
///     <see cref="UnavailableRecognitionSession"/> as the matching session-layer fallback) - the
///     capture-to-backend pipeline with its <see cref="AudioFrameResampler"/> audio-format
///     converter, and the internal mockable speech-inference seam backed by
///     <see cref="RecognitionSession"/>.
/// </remarks>
internal static class NamespaceDoc
{
}
