namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;

/// <summary>
///     The reference-voice conditioning produced once per voice by the speech encoder and reused for every
///     utterance spoken in that voice.
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
