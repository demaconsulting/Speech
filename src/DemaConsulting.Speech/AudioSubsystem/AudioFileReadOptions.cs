namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Options controlling how <see cref="AudioFileReader"/> decodes a file.
/// </summary>
public sealed class AudioFileReadOptions
{
    /// <summary>The default value of <see cref="MaxSamples"/>: 2^28 samples (1 GiB of floats).</summary>
    public const long DefaultMaxSamples = 1L << 28;

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
}
