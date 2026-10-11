namespace DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;

/// <summary>
///     The tunable constants of <see cref="OnnxNemotronRecognitionEngine"/>. The defaults are the
///     documented production values; tests use other values to exercise edge cases.
/// </summary>
/// <param name="DitherAmplitude">The dither standard deviation (default 1e-5, about -100 dBFS); zero disables dither.</param>
/// <param name="DitherSeed">The dither random seed.</param>
/// <param name="EndpointQuietMs">
///     How long, in input audio, the signal must stay quiet (per the <see cref="SilenceRunLimiter"/>)
///     after speech before the utterance is finalized (default 1500 ms). Measured on the input
///     clock, not the limiter's output, because the limiter removes most of a long quiet run.
/// </param>
/// <param name="EndpointEmptyChunks">
///     The number of consecutive encoder chunks, after text exists, that emit no token before the
///     utterance text is finalized (default 3, about 1.7 s). This is the fallback for a
///     steady background noise that never reads as quiet, where <paramref name="EndpointQuietMs"/>
///     cannot fire.
/// </param>
/// <param name="FlushTailChunks">
///     The number of all-zero feature chunks decoded after the final real chunk at flush/endpoint
///     (default 1), letting the encoder emit the last words held back by its look-ahead.
/// </param>
/// <param name="Limiter">The silence-run limiter constants.</param>
internal sealed record NemotronEngineOptions(
    float DitherAmplitude = DitherNoise.DefaultAmplitude,
    int DitherSeed = DitherNoise.DefaultSeed,
    int EndpointQuietMs = 2500,
    int EndpointEmptyChunks = 3,
    int FlushTailChunks = 1,
    SilenceRunLimiterOptions? Limiter = null)
{
    /// <summary>The number of new feature frames per encoder chunk (about 0.56 s).</summary>
    public const int ChunkFrames = 56;

    /// <summary>The number of preceding feature frames re-supplied with each chunk as pre-encode context.</summary>
    public const int PreEncodeFrames = 9;
}
