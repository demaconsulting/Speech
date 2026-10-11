namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Reads WAV and FLAC audio files into <see cref="AudioClip"/> values using fully managed decoders.
/// </summary>
/// <remarks>
///     The container is detected from the file content (<c>RIFF....WAVE</c>, or <c>fLaC</c> optionally behind an
///     ID3v2 tag), never from the file extension, so a mislabeled file still loads. WAV supports PCM 8/16/24/32-bit
///     and 32-bit float; FLAC supports every RFC 9639 native-framed stream (1-8 channels, 4-32 bits). Ogg-FLAC is
///     not supported. The whole file is read into memory, and decoded size is bounded by
///     <see cref="AudioFileReadOptions.MaxSamples"/>. Stateless and thread-safe.
/// </remarks>
public static class AudioFileReader
{
    /// <summary>Gets the lower-case file extensions (with the leading dot) this reader handles, for directory scans.</summary>
    public static IReadOnlyList<string> SupportedExtensions { get; } = [".wav", ".flac"];

    /// <summary>
    ///     Reads and decodes an audio file.
    /// </summary>
    /// <param name="path">The file path. Must not be null or blank.</param>
    /// <param name="options">Decode options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The decoded clip.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or blank.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is not a well-formed WAV or FLAC file, or is too large.</exception>
    /// <exception cref="NotSupportedException">Thrown when a WAV file uses an unsupported encoding.</exception>
    public static AudioClip Read(string path, AudioFileReadOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Audio file '{path}' was not found.", path);
        }

        try
        {
            return Decode(File.ReadAllBytes(path), options ?? new AudioFileReadOptions());
        }
        catch (InvalidDataException ex)
        {
            // Name the file so a failure among many voices is attributable
            throw new InvalidDataException($"'{path}': {ex.Message}", ex);
        }
        catch (NotSupportedException ex)
        {
            throw new NotSupportedException($"'{path}': {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Reads and decodes audio from a stream, reading it to its end.
    /// </summary>
    /// <param name="stream">A readable stream positioned at the start of the file. Must not be null.</param>
    /// <param name="options">Decode options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The decoded clip.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidDataException">Thrown when the data is not a well-formed WAV or FLAC file, or is too large.</exception>
    /// <exception cref="NotSupportedException">Thrown when a WAV file uses an unsupported encoding.</exception>
    public static AudioClip Read(Stream stream, AudioFileReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Decode(buffer.ToArray(), options ?? new AudioFileReadOptions());
    }

    /// <summary>
    ///     Reads an audio file and down-mixes it to mono.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="options">Decode options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The mono samples and their sample rate.</returns>
    /// <inheritdoc cref="Read(string, AudioFileReadOptions?)" path="/exception"/>
    public static (float[] Samples, int SampleRate) ReadMono(string path, AudioFileReadOptions? options = null)
    {
        var clip = Read(path, options);
        return (clip.ToMono(), clip.SampleRate);
    }

    /// <summary>
    ///     Reads audio from a stream and down-mixes it to mono.
    /// </summary>
    /// <param name="stream">A readable stream positioned at the start of the file.</param>
    /// <param name="options">Decode options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The mono samples and their sample rate.</returns>
    /// <inheritdoc cref="Read(Stream, AudioFileReadOptions?)" path="/exception"/>
    public static (float[] Samples, int SampleRate) ReadMono(Stream stream, AudioFileReadOptions? options = null)
    {
        var clip = Read(stream, options);
        return (clip.ToMono(), clip.SampleRate);
    }

    /// <summary>Sniffs the container and dispatches to the matching decoder.</summary>
    private static AudioClip Decode(byte[] data, AudioFileReadOptions options)
    {
        if (options.MaxSamples < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxSamples must not be negative.");
        }

        if (WavDecoder.IsWav(data))
        {
            return WavDecoder.Decode(data, options.MaxSamples);
        }

        if (FlacDecoder.IsFlac(data))
        {
            return FlacDecoder.Decode(data, options.VerifyChecksums, options.MaxSamples);
        }

        throw new InvalidDataException("The data is neither a RIFF/WAVE nor a FLAC file.");
    }
}
