namespace DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

/// <summary>
///     A small deterministic pseudo-random generator (SplitMix64) with unbiased bounded draws and a Fisher-Yates
///     shuffle, so the order in which training rows are visited and the clusters a bootstrap resample draws depend
///     only on the seed, never on the runtime, the operating system or the framework version.
/// </summary>
/// <remarks>
///     The framework's own generator is deliberately not used: its algorithm is not part of its contract and may
///     change between runtime versions, which would silently change a trained model. SplitMix64 is a fixed integer
///     recurrence, so the same seed yields the same sequence everywhere. Not thread-safe; one instance belongs to
///     one sequence of draws.
/// </remarks>
internal sealed class DeterministicRandom
{
    /// <summary>The increment of the SplitMix64 state (the 64-bit golden ratio, 0x9E3779B97F4A7C15).</summary>
    private const ulong Increment = 11400714819323198485UL;

    /// <summary>The first multiplier of the SplitMix64 output mix (0xBF58476D1CE4E5B9).</summary>
    private const ulong MixOne = 13787848793156543929UL;

    /// <summary>The second multiplier of the SplitMix64 output mix (0x94D049BB133111EB).</summary>
    private const ulong MixTwo = 10723151780598845931UL;

    /// <summary>The generator state.</summary>
    private ulong _state;

    /// <summary>
    ///     Initializes a generator.
    /// </summary>
    /// <param name="seed">The seed; every value is valid, including zero.</param>
    public DeterministicRandom(ulong seed) => _state = seed;

    /// <summary>
    ///     Draws the next 64-bit value.
    /// </summary>
    /// <returns>The next value of the SplitMix64 sequence.</returns>
    public ulong NextUInt64()
    {
        unchecked
        {
            _state += Increment;
            var z = _state;
            z = (z ^ (z >> 30)) * MixOne;
            z = (z ^ (z >> 27)) * MixTwo;
            return z ^ (z >> 31);
        }
    }

    /// <summary>
    ///     Draws a value from <c>0</c> up to, not including, a bound without modulo bias.
    /// </summary>
    /// <param name="bound">The exclusive upper bound; positive.</param>
    /// <returns>A value in <c>[0, bound)</c>, each equally likely.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bound"/> is not positive.</exception>
    /// <remarks>
    ///     Draws that fall in the short final interval of the 64-bit range (the part that would make some residues
    ///     more likely than others) are rejected and redrawn, so the result is exactly uniform.
    /// </remarks>
    public int NextInt(int bound)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bound);

        var range = (ulong)bound;

        // The number of values at the bottom of the 64-bit range that must be rejected: 2^64 mod bound.
        var threshold = unchecked(0UL - range) % range;
        while (true)
        {
            var draw = NextUInt64();
            if (draw >= threshold)
            {
                return (int)(draw % range);
            }
        }
    }

    /// <summary>
    ///     Shuffles items in place with the Fisher-Yates algorithm.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items to permute.</param>
    /// <remarks>Every permutation is equally likely; the same seed and length always give the same permutation.</remarks>
    public void Shuffle<T>(Span<T> items)
    {
        for (var i = items.Length - 1; i > 0; i--)
        {
            var j = NextInt(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
