namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;

/// <summary>
///     The reference-voice conditioning produced once per (voice, mood) by the speech encoder and reused for every
///     utterance spoken with that clip. The instance is deeply immutable: the constructor copies every supplied array
///     into private storage and the properties expose that storage only as <see cref="ReadOnlyMemory{T}"/>, so neither a
///     caller that retains a source array nor a consumer of a property can alter the conditioning, which is what makes
///     one instance safe to share across utterances.
/// </summary>
internal sealed class ChatterboxTurboConditioning
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatterboxTurboConditioning"/> class, copying every array.
    /// </summary>
    /// <param name="conditioningEmbeddings">Flattened conditioning embeddings, <paramref name="conditioningLength"/> x embedding size, prepended to the text embeddings.</param>
    /// <param name="conditioningLength">The number of conditioning positions.</param>
    /// <param name="promptTokens">The speech prompt tokens that open the decoder input.</param>
    /// <param name="speakerEmbeddings">The flattened speaker embedding vector.</param>
    /// <param name="speakerFeatures">The flattened speaker feature frames.</param>
    /// <param name="speakerFeatureFrames">The number of speaker feature frames.</param>
    /// <exception cref="ArgumentNullException">Thrown when any array argument is <see langword="null"/>.</exception>
    public ChatterboxTurboConditioning(
        float[] conditioningEmbeddings,
        int conditioningLength,
        long[] promptTokens,
        float[] speakerEmbeddings,
        float[] speakerFeatures,
        int speakerFeatureFrames)
    {
        ArgumentNullException.ThrowIfNull(conditioningEmbeddings);
        ArgumentNullException.ThrowIfNull(promptTokens);
        ArgumentNullException.ThrowIfNull(speakerEmbeddings);
        ArgumentNullException.ThrowIfNull(speakerFeatures);

        ConditioningEmbeddings = conditioningEmbeddings.ToArray();
        ConditioningLength = conditioningLength;
        PromptTokens = promptTokens.ToArray();
        SpeakerEmbeddings = speakerEmbeddings.ToArray();
        SpeakerFeatures = speakerFeatures.ToArray();
        SpeakerFeatureFrames = speakerFeatureFrames;
    }

    /// <summary>
    ///     Gets the read-only flattened conditioning embeddings, <see cref="ConditioningLength"/> x embedding size,
    ///     prepended to the text embeddings.
    /// </summary>
    public ReadOnlyMemory<float> ConditioningEmbeddings { get; }

    /// <summary>
    ///     Gets the number of conditioning positions.
    /// </summary>
    public int ConditioningLength { get; }

    /// <summary>
    ///     Gets the read-only speech prompt tokens that open the decoder input.
    /// </summary>
    public ReadOnlyMemory<long> PromptTokens { get; }

    /// <summary>
    ///     Gets the read-only flattened speaker embedding vector.
    /// </summary>
    public ReadOnlyMemory<float> SpeakerEmbeddings { get; }

    /// <summary>
    ///     Gets the read-only flattened speaker feature frames.
    /// </summary>
    public ReadOnlyMemory<float> SpeakerFeatures { get; }

    /// <summary>
    ///     Gets the number of speaker feature frames.
    /// </summary>
    public int SpeakerFeatureFrames { get; }
}
