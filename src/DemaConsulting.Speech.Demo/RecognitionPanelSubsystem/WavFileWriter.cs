namespace DemaConsulting.Speech.Demo.RecognitionPanelSubsystem;

/// <summary>
///     TEMPORARY diagnostic instrumentation: a minimal 16-bit PCM RIFF/WAVE file writer used by
///     <see cref="CaptureDebugRecorder"/> to capture raw microphone audio for offline analysis of
///     a reported streaming-recognition bug. This is internal investigation tooling, not a
///     permanent library capability - remove once the investigation concludes.
/// </summary>
/// <remarks>
///     Writes a standard 44-byte canonical RIFF/WAVE header with placeholder chunk sizes up
///     front (the total sample count is not known until the caller stops recording), then
///     patches the RIFF and <c>data</c> chunk sizes by seeking back into the already-written
///     header when <see cref="Dispose"/> is called. Each incoming normalized <c>float</c> sample
///     in <c>[-1.0, 1.0]</c> is converted to a 16-bit signed PCM sample, which loses some
///     precision relative to the source but keeps the file playable in the widest range of
///     ordinary media players and audio tools for a quick listen-back. Not thread-safe: callers
///     must not call <see cref="WriteSamples"/> from more than one thread concurrently, and must
///     not call it after <see cref="Dispose"/>.
/// </remarks>
internal sealed class WavFileWriter : IDisposable
{
    /// <summary>The number of bytes in one 16-bit PCM sample.</summary>
    private const int BytesPerSample = sizeof(short);

    /// <summary>The byte offset of the RIFF chunk size field, patched once the total size is known.</summary>
    private const long RiffChunkSizeOffset = 4;

    /// <summary>The byte offset of the data chunk size field, patched once the total size is known.</summary>
    private const long DataChunkSizeOffset = 40;

    /// <summary>The underlying file stream backing this writer.</summary>
    private readonly FileStream _stream;

    /// <summary>The binary writer used to emit the header and sample data.</summary>
    private readonly BinaryWriter _writer;

    /// <summary>The total number of sample data bytes written so far, used to patch the header on <see cref="Dispose"/>.</summary>
    private long _dataBytesWritten;

    /// <summary>Guards against writing the finalized header more than once.</summary>
    private bool _isDisposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="WavFileWriter"/> class, creating the file
    ///     at <paramref name="path"/> and writing a placeholder RIFF/WAVE header immediately so a
    ///     partially-written file (for example, one truncated by a crash before <see cref="Dispose"/>
    ///     runs) is still a recognizable, if silent, WAVE file rather than raw garbage.
    /// </summary>
    /// <param name="path">The full path of the file to create. Must not be <see langword="null"/> or empty.</param>
    /// <param name="sampleRate">The sample rate, in Hz, samples are captured at. Must be greater than zero.</param>
    /// <param name="channelCount">The number of interleaved channels in each write. Must be greater than zero.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="sampleRate"/> or <paramref name="channelCount"/> is not greater than zero.
    /// </exception>
    public WavFileWriter(string path, int sampleRate, int channelCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(channelCount, 0);

        _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new BinaryWriter(_stream);

        WriteHeader(sampleRate, channelCount);
    }

    /// <summary>
    ///     Writes the 44-byte canonical RIFF/WAVE header for 16-bit PCM, with the RIFF and
    ///     <c>data</c> chunk sizes left as zero placeholders to be patched by <see cref="Dispose"/>.
    /// </summary>
    /// <param name="sampleRate">The sample rate, in Hz, to declare in the <c>fmt </c> chunk.</param>
    /// <param name="channelCount">The channel count to declare in the <c>fmt </c> chunk.</param>
    private void WriteHeader(int sampleRate, int channelCount)
    {
        const short bitsPerSample = BytesPerSample * 8;
        var blockAlign = (short)(channelCount * BytesPerSample);
        var byteRate = sampleRate * blockAlign;

        _writer.Write("RIFF"u8);
        _writer.Write(0); // Placeholder RIFF chunk size, patched in Dispose once the total is known
        _writer.Write("WAVE"u8);
        _writer.Write("fmt "u8);
        _writer.Write(16); // fmt chunk size for uncompressed PCM
        _writer.Write((short)1); // Format tag 1 = uncompressed PCM
        _writer.Write((short)channelCount);
        _writer.Write(sampleRate);
        _writer.Write(byteRate);
        _writer.Write(blockAlign);
        _writer.Write(bitsPerSample);
        _writer.Write("data"u8);
        _writer.Write(0); // Placeholder data chunk size, patched in Dispose once the total is known
    }

    /// <summary>
    ///     Converts and appends one block of normalized capture samples as 16-bit PCM data.
    /// </summary>
    /// <param name="samples">
    ///     The samples to append, as normalized 32-bit floating point values in <c>[-1.0, 1.0]</c>,
    ///     interleaved by channel exactly as delivered by <see cref="AudioSubsystem.IAudioCaptureDevice.FrameCaptured"/>.
    /// </param>
    public void WriteSamples(IReadOnlyList<float> samples)
    {
        foreach (var pcmValue in samples.Select(static sample =>
        {
            // Clamp before scaling: a sample at or beyond +/-1.0 must map to the nearest valid
            // 16-bit value rather than overflow into an unrelated sample on the wire
            var clamped = Math.Clamp(sample, -1f, 1f);
            return (short)Math.Round(clamped * short.MaxValue, MidpointRounding.AwayFromZero);
        }))
        {
            _writer.Write(pcmValue);
        }

        _dataBytesWritten += samples.Count * (long)BytesPerSample;
    }

    /// <summary>
    ///     Patches the RIFF and <c>data</c> chunk sizes with the now-known total byte count and
    ///     closes the underlying file. Idempotent: a second call does nothing.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        _writer.Flush();

        // The header was written with placeholder sizes because the total sample count is not
        // known until recording stops; seek back and patch both size fields now that it is
        var riffChunkSize = (int)(36 + _dataBytesWritten);
        _stream.Seek(RiffChunkSizeOffset, SeekOrigin.Begin);
        _writer.Write(riffChunkSize);
        _stream.Seek(DataChunkSizeOffset, SeekOrigin.Begin);
        _writer.Write((int)_dataBytesWritten);

        _writer.Dispose();
    }
}
