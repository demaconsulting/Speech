namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Options controlling how <see cref="AudioFileReader"/> decodes a file.
/// </summary>
public sealed class AudioFileReadOptions
{
    /// <summary>The default value of <see cref="MaxSamples"/>: 2^28 samples (1 GiB of floats).</summary>
    public const long DefaultMaxSamples = 1L << 28;

    /// <summary>
    ///     The default value of <see cref="MaxEncodedBytes"/>: 1.25 GiB, which covers an uncompressed 32-bit file
    ///     holding <see cref="DefaultMaxSamples"/> samples (1 GiB) with headroom for headers and tags.
    /// </summary>
    public const long DefaultMaxEncodedBytes = 5L << 28;

    /// <summary>
    ///     Gets or sets a value indicating whether FLAC frame header (CRC-8) and frame (CRC-16) checksums are
    ///     verified. Defaults to <see langword="true"/>; it has no effect on WAV files.
    /// </summary>
    public bool VerifyChecksums { get; init; } = true;

    /// <summary>
    ///     Gets or sets the largest number of samples (across all channels) a file may decode to. A file that
    ///     declares or produces more is rejected with <see cref="InvalidDataException"/>, which bounds memory use
    ///     for hostile input. Defaults to <see cref="DefaultMaxSamples"/>.
    /// </summary>
    public long MaxSamples { get; init; } = DefaultMaxSamples;

    /// <summary>
    ///     Gets or sets the largest encoded input size, in bytes, that is read into memory (the file length, or
    ///     the bytes read from the stream). Larger input is rejected with <see cref="InvalidDataException"/> before
    ///     it is fully read, which bounds memory use for hostile input. This limit is independent of
    ///     <see cref="MaxSamples"/>: a valid FLAC stream has no upper bound on encoded bytes per decoded sample, so
    ///     no limit is derived from the sample count. The effective limit never exceeds the largest array length.
    ///     Defaults to <see cref="DefaultMaxEncodedBytes"/>.
    /// </summary>
    public long MaxEncodedBytes { get; init; } = DefaultMaxEncodedBytes;
}
