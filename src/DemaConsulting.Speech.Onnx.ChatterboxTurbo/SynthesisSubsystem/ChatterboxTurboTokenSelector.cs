namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;

/// <summary>
///     Picks the next speech token: repetition penalty over everything generated so far, then greedy arg-max.
/// </summary>
/// <remarks>
///     Reproduces the reference pipeline exactly: each distinct previously generated id (including the start
///     token) has its logit divided by the penalty when positive and multiplied by it when negative, which
///     always lowers the id's score; the highest score wins and ties resolve to the lowest id. Greedy
///     selection (no sampling) keeps generation deterministic. An optional blocked token is excluded from the
///     arg-max for one call (the run-length cap), but the stop token is never blocked so generation can always
///     end. NaN logits are treated as negative infinity, so all-NaN or all-negative-infinity input resolves to
///     the lowest unblocked id. Stateless and thread-safe.
/// </remarks>
internal static class ChatterboxTurboTokenSelector
{
    /// <summary>The reference repetition penalty.</summary>
    public const float RepetitionPenalty = 1.2f;

    /// <summary>The speech stop token, which is never blocked.</summary>
    private const int StopSpeechToken = OnnxChatterboxTurboSynthesisEngine.StopSpeechToken;

    /// <summary>
    ///     Selects the next token.
    /// </summary>
    /// <param name="logits">The vocabulary-sized logits of the last position.</param>
    /// <param name="generated">The distinct ids generated so far, including the start token; ids outside the vocabulary are ignored.</param>
    /// <param name="blockedToken">
    ///     A token id excluded from selection for this call, or -1 for none. The stop token is never blocked, and if
    ///     the only id of the vocabulary is blocked that id is returned.
    /// </param>
    /// <returns>The chosen token id.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="generated"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="logits"/> is empty.</exception>
    public static int Select(ReadOnlySpan<float> logits, IReadOnlySet<long> generated, int blockedToken = -1)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (logits.IsEmpty)
        {
            throw new ArgumentException("Logits must not be empty.", nameof(logits));
        }

        // The stop token can never be blocked, so generation can always end
        var blocked = blockedToken == StopSpeechToken ? -1 : blockedToken;

        // Scan once over the unblocked ids, penalizing generated ids on the fly. The first unblocked id seeds the
        // result so all-NaN or all -Infinity logits still resolve deterministically to the lowest unblocked id;
        // strict '>' keeps the lowest id on ties.
        var best = -1;
        var bestScore = float.NegativeInfinity;
        for (var id = 0; id < logits.Length; id++)
        {
            if (id == blocked)
            {
                continue;
            }

            var score = logits[id];
            if (float.IsNaN(score))
            {
                // NaN never wins against a real score
                score = float.NegativeInfinity;
            }
            else if (generated.Contains(id))
            {
                score = score < 0 ? score * RepetitionPenalty : score / RepetitionPenalty;
            }

            if (best < 0 || score > bestScore)
            {
                bestScore = score;
                best = id;
            }
        }

        // Only reachable when the single id of the vocabulary is blocked: return that id as the unblocked choice
        return best >= 0 ? best : 0;
    }
}
