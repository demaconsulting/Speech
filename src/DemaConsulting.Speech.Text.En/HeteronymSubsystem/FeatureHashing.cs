namespace DemaConsulting.Speech.Text.En.HeteronymSubsystem;

/// <summary>
///     The deterministic hash functions of the heteronym feature set: FNV-1a over UTF-16 code
///     units, a Murmur3-style combiner, and the salts that bind a hashed feature to a reading.
/// </summary>
/// <remarks>
///     Every function is a pure integer computation over code units, so a result never depends on
///     the operating system, the runtime, the current culture or the process (nothing here calls
///     <see cref="string.GetHashCode()"/>, a culture-aware comparison or a case-mapping API). The
///     case folding is ASCII only: <c>A</c> to <c>Z</c> become <c>a</c> to <c>z</c> and no other
///     character changes, so the Turkish dotted capital I and the Kelvin sign never fold into
///     ASCII letters. The trainer and the runtime call these same functions, which is what keeps
///     a model trained offline valid for the analyzer. The type is static and thread-safe.
/// </remarks>
internal static class FeatureHashing
{
    /// <summary>The FNV-1a 32-bit offset basis.</summary>
    internal const uint FnvOffsetBasis = 2166136261u;

    /// <summary>The FNV-1a 32-bit prime.</summary>
    internal const uint FnvPrime = 16777619u;

    /// <summary>The seed every feature hash starts from; part of the feature-set definition.</summary>
    private const uint FeatureSeed = 0x5EEDu;

    /// <summary>
    ///     Computes the FNV-1a hash of UTF-16 code units, unchanged.
    /// </summary>
    /// <param name="text">The code units.</param>
    /// <returns>The hash; <see cref="FnvOffsetBasis"/> for an empty span.</returns>
    internal static uint Fnv1a(ReadOnlySpan<char> text)
    {
        var hash = FnvOffsetBasis;
        foreach (var unit in text)
        {
            hash = Update(hash, unit);
        }

        return hash;
    }

    /// <summary>
    ///     Computes the FNV-1a hash of UTF-16 code units after folding ASCII capitals to lower case.
    /// </summary>
    /// <param name="text">The code units.</param>
    /// <returns>The hash of the ASCII-folded text.</returns>
    internal static uint Fnv1aFolded(ReadOnlySpan<char> text)
    {
        var hash = FnvOffsetBasis;
        foreach (var unit in text)
        {
            hash = Update(hash, FoldAscii(unit));
        }

        return hash;
    }

    /// <summary>
    ///     Folds one ASCII capital letter to lower case and leaves every other code unit unchanged.
    /// </summary>
    /// <param name="unit">The code unit.</param>
    /// <returns>The folded code unit.</returns>
    internal static char FoldAscii(char unit) => unit is >= 'A' and <= 'Z' ? (char)(unit + ('a' - 'A')) : unit;

    /// <summary>
    ///     Feeds one code unit to a running FNV-1a hash.
    /// </summary>
    /// <param name="hash">The running hash.</param>
    /// <param name="unit">The code unit; its 16 bits are XORed in whole.</param>
    /// <returns>The new running hash.</returns>
    internal static uint Update(uint hash, char unit) => (hash ^ unit) * FnvPrime;

    /// <summary>
    ///     Combines a running hash with a value (Murmur3 key mix, round and finalizer).
    /// </summary>
    /// <param name="hash">The running hash.</param>
    /// <param name="value">The value to fold in.</param>
    /// <returns>The mixed hash; a good spread of every input bit over the output.</returns>
    internal static uint Mix(uint hash, uint value)
    {
        value *= 0xcc9e2d51u;
        value = (value << 15) | (value >> 17);
        value *= 0x1b873593u;
        hash ^= value;
        hash = (hash << 13) | (hash >> 19);
        hash = (hash * 5) + 0xe6546b64u;
        hash ^= hash >> 16;
        hash *= 2246822507u; // the first Murmur3 finalizer multiplier
        hash ^= hash >> 13;
        hash *= 0xc2b2ae35u;
        hash ^= hash >> 16;
        return hash;
    }

    /// <summary>
    ///     Computes the hash of one feature from its template number and up to two arguments.
    /// </summary>
    /// <param name="template">The feature template number.</param>
    /// <param name="first">The first argument, or 0.</param>
    /// <param name="second">The second argument, or 0.</param>
    /// <returns>The feature hash, before it is bound to a reading.</returns>
    internal static uint Feature(uint template, uint first, uint second) =>
        Mix(Mix(Mix(FeatureSeed, template), first), second);

    /// <summary>
    ///     Computes the table bucket a feature hash selects for one salt.
    /// </summary>
    /// <param name="featureHash">The feature hash, from <see cref="Feature"/> or the feature extractor.</param>
    /// <param name="salt">The label salt (conjoined features) or role salt (shared features) of a reading.</param>
    /// <param name="mask">The bucket mask of the model, <c>2^hashBits - 1</c>.</param>
    /// <returns>The bucket, from 0 to <paramref name="mask"/>.</returns>
    /// <remarks>
    ///     The one definition of the bucket of a feature for a reading: the runtime scorer and the trainer both call
    ///     it, so a model trained offline selects exactly the buckets the analyzer scores.
    /// </remarks>
    internal static uint Bucket(uint featureHash, uint salt, uint mask) => Mix(featureHash, salt) & mask;

    /// <summary>
    ///     Computes the salt that binds a hashed feature to one specific reading.
    /// </summary>
    /// <param name="readingId">The reading id, for example <c>record.noun</c>.</param>
    /// <returns>The FNV-1a hash of <c>L:</c> followed by the id.</returns>
    internal static uint LabelSalt(string readingId) => PrefixedFnv('L', readingId);

    /// <summary>
    ///     Computes the salt that binds a hashed feature to a reading role shared across lemmas.
    /// </summary>
    /// <param name="role">The role, the part of the reading id after its first dot (for example <c>noun</c>).</param>
    /// <returns>The FNV-1a hash of <c>R:</c> followed by the role.</returns>
    internal static uint RoleSalt(string role) => PrefixedFnv('R', role);

    /// <summary>
    ///     Hashes a one-letter prefix, a colon and a text, without building the joined string.
    /// </summary>
    /// <param name="prefix">The prefix letter.</param>
    /// <param name="text">The text.</param>
    /// <returns>The FNV-1a hash of <c>prefix:text</c>.</returns>
    private static uint PrefixedFnv(char prefix, string text)
    {
        var hash = Update(FnvOffsetBasis, prefix);
        hash = Update(hash, ':');
        foreach (var unit in text)
        {
            hash = Update(hash, unit);
        }

        return hash;
    }
}
