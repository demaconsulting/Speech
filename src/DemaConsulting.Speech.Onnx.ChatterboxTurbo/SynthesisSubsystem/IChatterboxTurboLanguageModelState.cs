namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;

/// <summary>
///     One autoregressive generation: the language model plus its growing key/value cache.
/// </summary>
internal interface IChatterboxTurboLanguageModelState : IDisposable
{
    /// <summary>
    ///     Runs one language-model step and advances the key/value cache.
    /// </summary>
    /// <param name="inputEmbeddings">The flattened input embeddings for the new positions.</param>
    /// <param name="sequenceLength">The number of new positions in <paramref name="inputEmbeddings"/>.</param>
    /// <param name="attentionLength">The attention-mask length: all positions so far including the new ones.</param>
    /// <param name="firstPosition">The position id of the first new position (later positions increment by one).</param>
    /// <returns>The logits of the last new position (vocabulary-sized).</returns>
    float[] Step(float[] inputEmbeddings, int sequenceLength, int attentionLength, int firstPosition);
}
