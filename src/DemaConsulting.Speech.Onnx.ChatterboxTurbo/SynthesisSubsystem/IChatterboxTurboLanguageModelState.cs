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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inputEmbeddings"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="sequenceLength"/> is not positive or <paramref name="attentionLength"/> is smaller than it.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="inputEmbeddings"/> has the wrong length.</exception>
    /// <exception cref="ObjectDisposedException">Thrown after this state or its owner was disposed.</exception>
    float[] Step(float[] inputEmbeddings, int sequenceLength, int attentionLength, int firstPosition);
}
