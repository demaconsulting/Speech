using System.Buffers.Binary;
using System.Numerics;

namespace DemaConsulting.Speech.Text.En.HeteronymSubsystem;

/// <summary>
///     An immutable, thread-safe sparse averaged-perceptron model: the integer weights of the hashed
///     features of the heteronym classifier, loaded from a strictly validated <c>HETW</c> file.
/// </summary>
/// <remarks>
///     <para>
///     Only the non-zero weights are stored. The runtime keeps an occupancy bitmap of
///     2^<see cref="HashBits"/> buckets, a per-word rank base and the packed weights, so a lookup is
///     one bit test plus one population count, with no hashing and no allocation. The bitmap costs at
///     most 2 MiB at 24 bits. A real score is an integer score divided by <see cref="Scale"/>;
///     <see cref="Temperature"/> calibrates the confidence.
///     </para>
///     <para>
///     <b>Loading</b> never trusts the file. See <see cref="ModelBinaryFormat"/> for the layout and the
///     checks; the loader additionally decodes the payload strictly: the bucket gaps are positive and
///     the buckets stay inside the hash table, a weight is never zero and never -32768, and the declared
///     payload length matches the structure exactly. Allocation is bounded by the validated hash bits and
///     row count, never by an unchecked count. Every defect is an <see cref="InvalidDataException"/>
///     whose message names the defect and never the data.
///     </para>
/// </remarks>
internal sealed class SparsePerceptronModel
{
    /// <summary>The occupancy bitmap: bit b of word w says bucket 64 w + b has a weight.</summary>
    private readonly ulong[] _occupancy;

    /// <summary>For each bitmap word, the number of weights stored before it.</summary>
    private readonly int[] _rankBase;

    /// <summary>The non-zero weights in bucket order.</summary>
    private readonly short[] _weights;

    /// <summary>The informational data-manifest hash.</summary>
    private readonly byte[] _manifest;

    /// <summary>
    ///     Initializes a new instance from decoded, validated parts.
    /// </summary>
    /// <param name="header">The validated header.</param>
    /// <param name="occupancy">The occupancy bitmap; owned by the new instance.</param>
    /// <param name="rankBase">The rank bases; owned by the new instance.</param>
    /// <param name="weights">The packed weights; owned by the new instance.</param>
    private SparsePerceptronModel(ModelHeader header, ulong[] occupancy, int[] rankBase, short[] weights)
    {
        HashBits = header.HashBits;
        Temperature = header.Temperature;
        Scale = header.Scale;
        InventoryVersion = header.InventoryVersion;
        FeatureSetVersion = header.FeatureSetVersion;
        TrainerVersion = header.TrainerVersion;
        RowCount = header.RowCount;
        _manifest = header.DataManifestSha256;
        _occupancy = occupancy;
        _rankBase = rankBase;
        _weights = weights;
    }

    /// <summary>Gets the number of hash bits; the model has 2^<see cref="HashBits"/> buckets.</summary>
    internal int HashBits { get; }

    /// <summary>Gets the calibration temperature: finite and positive.</summary>
    internal float Temperature { get; }

    /// <summary>Gets the fixed-point scale: a real score is an integer score divided by this value.</summary>
    internal float Scale { get; }

    /// <summary>Gets the revision of the reading inventory the model was trained for.</summary>
    internal int InventoryVersion { get; }

    /// <summary>Gets the feature-set version the model was trained with; at least 1, but not necessarily the version this build extracts (the classifier checks that).</summary>
    internal int FeatureSetVersion { get; }

    /// <summary>Gets the informational version of the trainer that wrote the file; 0 when unknown.</summary>
    internal uint TrainerVersion { get; }

    /// <summary>Gets the number of stored (non-zero) weights.</summary>
    internal int RowCount { get; }

    /// <summary>Gets the bucket mask, <c>2^HashBits - 1</c>.</summary>
    internal uint Mask => (uint)((1L << HashBits) - 1);

    /// <summary>
    ///     Gets a copy of the informational SHA-256 of the training data manifest recorded in the file.
    /// </summary>
    /// <returns>A new 32-byte array; all zero when the trainer recorded none.</returns>
    internal byte[] GetDataManifestSha256() => (byte[])_manifest.Clone();

    /// <summary>
    ///     Loads a model from a stream.
    /// </summary>
    /// <param name="stream">The stream, read to its end. Must not be null; neither disposed nor retained.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidDataException">The data is not a valid heteronym model; see <see cref="Load(byte[])"/>.</exception>
    /// <remarks>At most <see cref="ModelBinaryFormat.MaxFileLength"/> bytes are read, so an endless stream is rejected.</remarks>
    internal static SparsePerceptronModel Load(Stream stream) => Load(ModelBinaryFormat.ReadAll(stream));

    /// <summary>
    ///     Loads a model from the bytes of a file.
    /// </summary>
    /// <param name="image">The whole file. Must not be null; not retained.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     The file has the wrong magic, format version, flags, feature-set version (zero), inventory version
    ///     (zero), hash bits, reserved bytes, temperature, scale, row count or lengths; is truncated or has
    ///     trailing bytes; fails its checksum; has a Deflate stream that is invalid, cut off, trailing or of
    ///     the wrong size; or has a payload that is not exactly the declared gaps and weights. A well-formed
    ///     file of another feature-set or inventory version loads; compatibility is decided by the classifier.
    /// </exception>
    internal static SparsePerceptronModel Load(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);

        // Header, then the strictly inflated payload (checksum verified first), then the structural decode.
        var header = ModelBinaryFormat.ParseHeader(image);
        var payload = ModelBinaryFormat.ReadPayload(image, header);

        var rows = header.RowCount;
        var buckets = 1L << header.HashBits;
        var occupancy = new ulong[(int)(buckets / 64)];

        // The weights are the last two bytes per row; the gaps fill what precedes them. The gaps are
        // decoded first, and the weight array is only allocated once they are known to be sound, so a
        // hostile file never costs more than the inflated payload and the bitmap before it is rejected.
        var gapEnd = payload.Length - (2 * rows);
        var position = 0;
        long previous = -1;
        for (var row = 0; row < rows; row++)
        {
            var gap = ModelBinaryFormat.ReadVarint(payload, ref position, gapEnd);
            if (gap < 1)
            {
                throw new InvalidDataException("The model payload has a bucket gap of zero (buckets must strictly increase).");
            }

            var bucket = previous + gap;
            if (bucket >= buckets)
            {
                throw new InvalidDataException("The model payload has a bucket outside the hash table.");
            }

            occupancy[bucket >> 6] |= 1UL << (int)(bucket & 63);
            previous = bucket;
        }

        if (position != gapEnd)
        {
            throw new InvalidDataException("The model payload length does not match its gaps and weights.");
        }

        var weights = new short[rows];
        for (var row = 0; row < rows; row++)
        {
            var weight = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(gapEnd + (2 * row)));
            if (weight is 0 or short.MinValue)
            {
                throw new InvalidDataException("The model payload has a weight that is zero or -32768.");
            }

            weights[row] = weight;
        }

        // The rank base of each word: how many weights lie before it.
        var rankBase = new int[occupancy.Length];
        var running = 0;
        for (var word = 0; word < occupancy.Length; word++)
        {
            rankBase[word] = running;
            running += BitOperations.PopCount(occupancy[word]);
        }

        return new SparsePerceptronModel(header, occupancy, rankBase, weights);
    }

    /// <summary>
    ///     Gets the weight of a bucket.
    /// </summary>
    /// <param name="bucket">The bucket, already masked to <see cref="HashBits"/> bits.</param>
    /// <returns>The integer weight; 0 for a bucket with no stored weight or one outside the table.</returns>
    /// <remarks>Allocation-free and thread-safe (the model never changes after loading).</remarks>
    internal int GetWeight(uint bucket)
    {
        if (bucket > Mask)
        {
            return 0;
        }

        var word = (int)(bucket >> 6);
        var bit = (int)(bucket & 63);
        var occupied = _occupancy[word];
        if (((occupied >> bit) & 1UL) == 0)
        {
            return 0;
        }

        // The weight's index is the number of stored weights before this bucket.
        var rank = _rankBase[word] + BitOperations.PopCount(occupied & ((1UL << bit) - 1));
        return _weights[rank];
    }
}
