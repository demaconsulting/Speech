namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;

/// <summary>
///     The reference-voice conditioning produced once per (voice, mood) by the speech encoder and reused for every
///     utterance spoken with that clip. The record is shallowly immutable: its properties cannot be reassigned, but the
///     arrays they reference are shared by reference and are treated as read-only by every consumer, which is what makes
///     one instance safe to share across utterances.
/// </summary>
/// <param name="ConditioningEmbeddings">Flattened conditioning embeddings, <paramref name="ConditioningLength"/> x embedding size, prepended to the text embeddings.</param>
/// <param name="ConditioningLength">The number of conditioning positions.</param>
/// <param name="PromptTokens">The speech prompt tokens that open the decoder input.</param>
/// <param name="SpeakerEmbeddings">The flattened speaker embedding vector.</param>
/// <param name="SpeakerFeatures">The flattened speaker feature frames.</param>
/// <param name="SpeakerFeatureFrames">The number of speaker feature frames.</param>
internal sealed record ChatterboxTurboConditioning(
    float[] ConditioningEmbeddings,
    int ConditioningLength,
    long[] PromptTokens,
    float[] SpeakerEmbeddings,
    float[] SpeakerFeatures,
    int SpeakerFeatureFrames);
