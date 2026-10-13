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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="AudioFileReadOptions.MaxSamples"/> is negative (reported as <c>options</c>, after the file-exists check).</exception>
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
            options ??= new AudioFileReadOptions();
            var maxBytes = GetMaxBytes(options);

            // Reject an oversized file before reading any of it
            var length = new FileInfo(path).Length;
            if (length > maxBytes)
            {
                throw new InvalidDataException($"The file is {length} bytes, which exceeds the {maxBytes}-byte limit implied by MaxSamples.");
            }

            return Decode(File.ReadAllBytes(path), options);
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
        options ??= new AudioFileReadOptions();
        return Decode(ReadBounded(stream, GetMaxBytes(options)), options);
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

    /// <summary>
    ///     Derives the largest input size, in bytes, a file allowed to decode to <see cref="AudioFileReadOptions.MaxSamples"/>
    ///     samples can have: at most five bytes per sample (32-bit PCM/float, or incompressible 32-bit FLAC with frame overhead) plus slack
    ///     for headers, tags and frame overhead, capped at the largest array length.
    /// </summary>
    private static long GetMaxBytes(AudioFileReadOptions options)
    {
        if (options.MaxSamples < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxSamples must not be negative.");
        }

        const long slack = 1L << 20;
        var wanted = options.MaxSamples > (long.MaxValue - slack) / 5 ? long.MaxValue : (options.MaxSamples * 5) + slack;
        return Math.Min(wanted, Array.MaxLength);
    }

    /// <summary>Reads a stream to its end, failing as soon as more than <paramref name="maxBytes"/> bytes are seen.</summary>
    private static byte[] ReadBounded(Stream stream, long maxBytes)
    {
        // A seekable stream tells us its size up front, so allocate exactly once
        if (stream.CanSeek)
        {
            var remaining = stream.Length - stream.Position;
            if (remaining > maxBytes)
            {
                throw new InvalidDataException($"The stream holds {remaining} bytes, which exceeds the {maxBytes}-byte limit implied by MaxSamples.");
            }

            var exact = new byte[remaining];
            stream.ReadExactly(exact);
            return exact;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                throw new InvalidDataException($"The stream exceeds the {maxBytes}-byte limit implied by MaxSamples.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>Sniffs the container and dispatches to the matching decoder.</summary>
    private static AudioClip Decode(byte[] data, AudioFileReadOptions options)
    {
        // MaxSamples was already validated by GetMaxBytes, which every caller runs first
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
