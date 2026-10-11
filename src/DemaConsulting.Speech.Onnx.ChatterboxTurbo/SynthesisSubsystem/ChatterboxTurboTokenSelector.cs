namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;

/// <summary>
///     Picks the next speech token: repetition penalty over everything generated so far, then greedy arg-max.
/// </summary>
/// <remarks>
///     Reproduces the reference pipeline exactly: each distinct previously generated id (including the start
///     token) has its logit divided by the penalty when positive and multiplied by it when negative, which
///     always lowers the id's score; the highest score wins and ties resolve to the lowest id. Greedy
///     selection (no sampling) keeps generation deterministic. Stateless and thread-safe.
/// </remarks>
internal static class ChatterboxTurboTokenSelector
{
    /// <summary>The reference repetition penalty.</summary>
    public const float RepetitionPenalty = 1.2f;

    /// <summary>
    ///     Selects the next token.
    /// </summary>
    /// <param name="logits">The vocabulary-sized logits of the last position.</param>
    /// <param name="generated">The distinct ids generated so far, including the start token; ids outside the vocabulary are ignored.</param>
    /// <returns>The chosen token id.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="generated"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="logits"/> is empty.</exception>
    public static int Select(ReadOnlySpan<float> logits, IReadOnlySet<long> generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (logits.IsEmpty)
        {
            throw new ArgumentException("Logits must not be empty.", nameof(logits));
        }

        // Scan once, penalizing generated ids on the fly; strict '>' keeps the lowest id on ties
        var best = 0;
        var bestScore = float.NegativeInfinity;
        for (var id = 0; id < logits.Length; id++)
        {
            var score = logits[id];
            if (generated.Contains(id))
            {
                score = score < 0 ? score * RepetitionPenalty : score / RepetitionPenalty;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = id;
            }
        }

        return best;
    }
}
