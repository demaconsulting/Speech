namespace DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;

/// <summary>
///     The tunable constants of <see cref="OnnxNemotronRecognitionEngine"/>. The defaults are the
///     documented production values; tests and the evaluation tool use other values to exercise
///     edge cases and to measure alternatives.
/// </summary>
/// <param name="DitherAmplitude">The dither standard deviation (default 1e-5, about -100 dBFS); zero disables dither.</param>
/// <param name="DitherSeed">The dither random seed.</param>
/// <param name="FlushTailChunks">
///     The number of whole chunks of silence features decoded after the last real audio when the
///     stream is flushed (default 1), letting the encoder emit the last words held back by its
///     look-ahead. The silence is dithered digital silence run through the feature extractor -
///     never all-zero log-mel rows, which the model hears as loud sound. Must not be negative.
/// </param>
/// <param name="BlankPenalty">
///     The score subtracted from the RNN-T blank token before greedy selection (default
///     <see cref="NemotronRnntNetwork.DefaultBlankPenalty"/>); see
///     <see cref="NemotronRnntNetwork"/>. Must be finite and not negative; zero is plain greedy
///     decoding.
/// </param>
/// <param name="Endpoint">The endpoint rules, or <see langword="null"/> for the Sherpa-equivalent defaults (2.4 s, 1.2 s, 20 s).</param>
/// <param name="Limiter">The silence-run limiter constants, or <see langword="null"/> for the defaults.</param>
internal sealed record NemotronEngineOptions(
    float DitherAmplitude = DitherNoise.DefaultAmplitude,
    int DitherSeed = DitherNoise.DefaultSeed,
    int FlushTailChunks = 1,
    float BlankPenalty = NemotronRnntNetwork.DefaultBlankPenalty,
    NemotronEndpointRules? Endpoint = null,
    SilenceRunLimiterOptions? Limiter = null)
{
    /// <summary>The number of new feature frames per encoder chunk (about 0.56 s).</summary>
    public const int ChunkFrames = 56;

    /// <summary>The number of preceding feature frames re-supplied with each chunk as pre-encode context.</summary>
    public const int PreEncodeFrames = 9;
}
