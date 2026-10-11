namespace DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;

/// <summary>
///     Seam over the cache-aware streaming encoder: turns one chunk of log-mel features (with its
///     pre-encode context) into encoder frames, carrying the attention/convolution caches from one
///     call to the next.
/// </summary>
/// <remarks>
///     The seam exists so the engine's chunking, endpointing, and flush logic can be unit tested
///     with a managed fake and no model files; <see cref="NemotronEncoder"/> is the ONNX Runtime
///     implementation.
/// </remarks>
internal interface INemotronEncoder : IDisposable
{
    /// <summary>Gets the number of floats in one encoder output frame.</summary>
    int HiddenSize { get; }

    /// <summary>
    ///     Encodes one chunk.
    /// </summary>
    /// <param name="features">
    ///     <paramref name="frameCount"/> log-mel frames (pre-encode cache frames followed by the new
    ///     chunk), row-major with <see cref="NemotronFeatureExtractor.MelBands"/> values per frame.
    /// </param>
    /// <param name="frameCount">The number of feature frames supplied.</param>
    /// <param name="outputFrames">The number of encoder output frames produced.</param>
    /// <returns>
    ///     The encoder output, <paramref name="outputFrames"/> frames of <see cref="HiddenSize"/>
    ///     floats, valid until the next call to <see cref="Encode"/> or <see cref="Reset"/>.
    /// </returns>
    ReadOnlySpan<float> Encode(ReadOnlySpan<float> features, int frameCount, out int outputFrames);

    /// <summary>Clears the streaming caches, returning to the start-of-stream state.</summary>
    void Reset();
}
