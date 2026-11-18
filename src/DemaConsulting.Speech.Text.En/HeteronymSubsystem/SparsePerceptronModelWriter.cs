using System.Buffers.Binary;

namespace DemaConsulting.Speech.Text.En.HeteronymSubsystem;

/// <summary>
///     Builds heteronym model files (<c>HETW</c> version 1) from a list of weights: the writer
///     counterpart of <see cref="SparsePerceptronModel.Load(byte[])"/>.
/// </summary>
/// <remarks>
///     Internal because the only writers are tests (which build hand-made models through the
///     real format) and the offline trainer tool. The writer refuses anything the
///     loader would refuse, so a model it produces always loads. It is stateless and thread-safe.
/// </remarks>
internal static class SparsePerceptronModelWriter
{
    /// <summary>
    ///     Compiles weights into the bytes of a model file.
    /// </summary>
    /// <param name="hashBits">The number of hash bits, 16 to 24.</param>
    /// <param name="inventoryVersion">The inventory revision the weights were trained for, 1 to 65535.</param>
    /// <param name="temperature">The calibration temperature: finite, greater than zero and at most 1e6.</param>
    /// <param name="scale">The fixed-point scale: finite, greater than zero and at most 1e6.</param>
    /// <param name="weights">The (bucket, weight) pairs in any order. Must not be null. Zero weights are dropped.</param>
    /// <param name="compress">Whether to store the payload as raw Deflate (the default) or uncompressed.</param>
    /// <param name="trainerVersion">The informational trainer version; 0 for none.</param>
    /// <param name="dataManifestSha256">The informational 32-byte manifest hash, or null for all zeros.</param>
    /// <returns>The file bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="weights"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A header field is outside its range, or a bucket is outside the hash table.</exception>
    /// <exception cref="ArgumentException">A bucket appears twice, a weight is -32768, <paramref name="dataManifestSha256"/> is not 32 bytes, or there are more rows than a file may hold.</exception>
    internal static byte[] Compile(
        int hashBits,
        int inventoryVersion,
        float temperature,
        float scale,
        IEnumerable<(uint Bucket, short Weight)> weights,
        bool compress = true,
        uint trainerVersion = 0,
        byte[]? dataManifestSha256 = null)
    {
        ArgumentNullException.ThrowIfNull(weights);

        // Validate the header fields with the same limits the loader enforces.
        if (hashBits is < ModelBinaryFormat.MinHashBits or > ModelBinaryFormat.MaxHashBits)
        {
            throw new ArgumentOutOfRangeException(nameof(hashBits), hashBits, "The hash bits are outside 16 to 24.");
        }

        if (inventoryVersion is < 1 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(inventoryVersion), inventoryVersion, "The inventory version is outside 1 to 65535.");
        }

        if (!ModelBinaryFormat.IsValidFloatField(temperature))
        {
            throw new ArgumentOutOfRangeException(nameof(temperature), temperature, "The temperature must be finite, greater than zero and at most 1e6.");
        }

        if (!ModelBinaryFormat.IsValidFloatField(scale))
        {
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "The scale must be finite, greater than zero and at most 1e6.");
        }

        if (dataManifestSha256 is { Length: not ModelBinaryFormat.ManifestLength })
        {
            throw new ArgumentException("The data manifest hash must be 32 bytes.", nameof(dataManifestSha256));
        }

        // Order by bucket, reject duplicates and out-of-range buckets, and drop zero weights.
        var buckets = 1L << hashBits;
        var ordered = new List<(uint Bucket, short Weight)>(weights);
        ordered.Sort(static (a, b) => a.Bucket.CompareTo(b.Bucket));
        var rows = new List<(uint Bucket, short Weight)>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var (bucket, weight) = ordered[i];
            if (bucket >= buckets)
            {
                throw new ArgumentOutOfRangeException(nameof(weights), "A bucket is outside the hash table.");
            }

            if (i > 0 && ordered[i - 1].Bucket == bucket)
            {
                throw new ArgumentException("A bucket appears more than once.", nameof(weights));
            }

            if (weight == short.MinValue)
            {
                throw new ArgumentException("A weight of -32768 cannot be stored.", nameof(weights));
            }

            if (weight != 0)
            {
                rows.Add((bucket, weight));
            }
        }

        if (rows.Count > ModelBinaryFormat.MaxPayloadLength / 3)
        {
            throw new ArgumentException("There are more weights than a model file may hold.", nameof(weights));
        }

        // Payload: the bucket gaps as varints (the first gap is the bucket plus one), then the int16 weights.
        using var payload = new MemoryStream();
        long previous = -1;
        foreach (var (bucket, _) in rows)
        {
            ModelBinaryFormat.WriteVarint(payload, (uint)(bucket - previous));
            previous = bucket;
        }

        var weightBytes = new byte[2];
        foreach (var (_, weight) in rows)
        {
            BinaryPrimitives.WriteInt16LittleEndian(weightBytes, weight);
            payload.Write(weightBytes, 0, weightBytes.Length);
        }

        var header = new ModelHeader(
            compress ? ModelBinaryFormat.FlagDeflate : 0,
            HeteronymFeatureExtractor.FeatureSetVersion,
            inventoryVersion,
            hashBits,
            temperature,
            scale,
            rows.Count,
            (int)payload.Length,
            0,
            0,
            trainerVersion,
            dataManifestSha256 is null ? new byte[ModelBinaryFormat.ManifestLength] : (byte[])dataManifestSha256.Clone());
        return ModelBinaryFormat.WriteImage(header, payload.ToArray(), compress);
    }

    /// <summary>
    ///     Rewrites a valid model file with an uncompressed payload and every other field unchanged.
    /// </summary>
    /// <param name="image">The bytes of a valid model file, compressed or not. Must not be null.</param>
    /// <returns>The equivalent file with the Deflate flag cleared and the checksum recomputed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is null.</exception>
    /// <exception cref="InvalidDataException"><paramref name="image"/> is not a valid model file.</exception>
    /// <remarks>
    ///     Gives a representation that does not depend on the Deflate implementation of a runtime, so a
    ///     later staleness check can compare two models byte for byte. The compressed and the raw form
    ///     of one model always rewrite to identical bytes.
    /// </remarks>
    internal static byte[] ToUncompressedImage(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var header = ModelBinaryFormat.ParseHeader(image);
        var payload = ModelBinaryFormat.ReadPayload(image, header);
        return ModelBinaryFormat.WriteImage(header, payload, compress: false);
    }
}
