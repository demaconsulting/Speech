namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     File-backed <see cref="IAudioPlaybackDevice"/> implementation that writes every
///     <see cref="Write"/> call as 16-bit PCM sample data into a RIFF/WAVE file instead of
///     rendering to real playback hardware.
/// </summary>
/// <remarks>
///     <para>
///         This is the correct layer for this capability (not a CLI-only helper) because
///         <see cref="IAudioPlaybackDevice"/> is exactly the seam
///         <c>SpeechSynthesizerFactory.Create</c> already requires, so any host application -
///         not only a future command-line tool - can capture synthesized speech to a file
///         deterministically (for example, for its own automated tests) by supplying this class
///         wherever a real playback device would otherwise be used.
///     </para>
///     <para>
///         The RIFF/WAVE byte layout and clamp-before-scale rounding behavior deliberately match
///         the informal, temporary <c>WavFileWriter</c> previously used only for diagnostic
///         instrumentation in <c>DemaConsulting.Speech.Demo</c>, promoted here to a permanent,
///         tested, public capability: a 44-byte canonical header is written immediately at
///         construction with placeholder RIFF/<c>data</c> chunk sizes (so a file truncated by a
///         crash before <see cref="Dispose"/> runs is still a recognizable, if silent, WAVE
///         file), and each incoming normalized <see langword="float"/> sample in
///         <c>[-1.0, 1.0]</c> is clamped before scaling to a 16-bit signed PCM sample so a value
///         at or beyond the clamp boundary maps to the nearest valid 16-bit value rather than
///         overflowing into an unrelated sample on the wire.
///     </para>
///     <para>
///         Because writing to a file never depends on real audio hardware, this device is always
///         available: <see cref="IsAvailable"/> is always <see langword="true"/>, and
///         <see cref="Start"/>/<see cref="Stop"/> are no-ops beyond state tracking.
///         <see cref="PendingSampleCount"/> always reports <c>0</c> because every
///         <see cref="Write"/> call is fully synchronous - there is never anything still "in
///         flight" the way a real device's callback-driven queue can have. Not thread-safe:
///         callers must not call <see cref="Write"/> from more than one thread concurrently, and
///         must not call it after <see cref="Dispose"/>.
///     </para>
/// </remarks>
public sealed class WavFileAudioPlaybackDevice : IAudioPlaybackDevice, IDisposable
{
    /// <summary>The number of bytes in one 16-bit PCM sample.</summary>
    private const int BytesPerSample = sizeof(short);

    /// <summary>The byte offset of the RIFF chunk size field, patched once the total size is known.</summary>
    private const long RiffChunkSizeOffset = 4;

    /// <summary>The byte offset of the data chunk size field, patched once the total size is known.</summary>
    private const long DataChunkSizeOffset = 40;

    /// <summary>The underlying file stream backing this device.</summary>
    private readonly FileStream _stream;

    /// <summary>The binary writer used to emit the header and sample data.</summary>
    private readonly BinaryWriter _writer;

    /// <summary>The total number of sample data bytes written so far, used to patch the header on <see cref="Dispose"/>.</summary>
    private long _dataBytesWritten;

    /// <summary>Guards against writing the finalized header more than once.</summary>
    private bool _isDisposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="WavFileAudioPlaybackDevice"/> class,
    ///     creating the file at <paramref name="path"/> and writing a placeholder RIFF/WAVE
    ///     header immediately.
    /// </summary>
    /// <param name="path">The full path of the file to create. Must not be <see langword="null"/> or empty.</param>
    /// <param name="sampleRate">The sample rate, in Hz, samples supplied to <see cref="Write"/> are rendered at. Must be greater than zero.</param>
    /// <param name="channelCount">The number of interleaved channels every <see cref="Write"/> call must supply. Must be greater than zero.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="sampleRate"/> or <paramref name="channelCount"/> is not greater than zero.
    /// </exception>
    public WavFileAudioPlaybackDevice(string path, int sampleRate, int channelCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(channelCount, 0);

        SampleRate = sampleRate;
        ChannelCount = channelCount;

        _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new BinaryWriter(_stream);

        WriteHeader(sampleRate, channelCount);
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Always <see langword="true"/>: writing to a file never depends on real audio
    ///     hardware, so this device never fails to resolve a backend the way a real device can.
    /// </remarks>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    /// <remarks>
    ///     Reports the channel count supplied at construction, which is also the interleaving
    ///     stride every <see cref="Write"/> call must honor.
    /// </remarks>
    public int ChannelCount { get; }

    /// <inheritdoc/>
    /// <remarks>
    ///     Reports the sample rate supplied at construction, which is also the rate declared in
    ///     the written file's <c>fmt </c> chunk.
    /// </remarks>
    public int SampleRate { get; }

    /// <inheritdoc/>
    /// <remarks>
    ///     A no-op beyond state tracking: there is no real playback stream to open, since every
    ///     <see cref="Write"/> call synchronously appends to the file.
    /// </remarks>
    public void Start()
    {
        // Intentionally a no-op: file writes are synchronous and require no stream to open.
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     A no-op beyond state tracking: there is no real playback stream to stop. The file
    ///     itself is only finalized when <see cref="Dispose"/> is called.
    /// </remarks>
    public void Stop()
    {
        // Intentionally a no-op: file writes are synchronous and require no stream to stop.
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Converts and appends one block of normalized samples as 16-bit PCM data. Each sample
    ///     is clamped to <c>[-1.0, 1.0]</c> before scaling to a 16-bit signed value, matching the
    ///     source diagnostic writer this device promotes to a permanent capability, so a sample
    ///     at or beyond the clamp boundary maps to the nearest valid 16-bit value rather than
    ///     overflowing into an unrelated sample on the wire.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="samples"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this device has already been disposed.</exception>
    public void Write(IReadOnlyList<float> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        // An indexed loop avoids LINQ's per-sample iterator/delegate overhead on this hot,
        // potentially large-volume write path.
        for (var index = 0; index < samples.Count; index++)
        {
            _writer.Write(ConvertSampleToPcm(samples[index]));
        }

        _dataBytesWritten += samples.Count * (long)BytesPerSample;
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Always <c>0</c>: every <see cref="Write"/> call is fully synchronous, so there is
    ///     never anything still "in flight" the way a real device's callback-driven queue can
    ///     have.
    /// </remarks>
    public long PendingSampleCount => 0;

    /// <summary>
    ///     Converts one normalized sample to a 16-bit PCM value so the write loop can express
    ///     its intent as a projection from caller-owned floats to on-disk PCM samples.
    /// </summary>
    /// <param name="sample">The normalized sample to convert.</param>
    /// <returns>
    ///     The nearest 16-bit PCM value after clamping <paramref name="sample"/> to
    ///     <c>[-1.0, 1.0]</c>.
    /// </returns>
    private static short ConvertSampleToPcm(float sample)
    {
        // Clamp before scaling: a sample at or beyond +/-1.0 must map to the nearest valid
        // 16-bit value rather than overflow into an unrelated sample on the wire.
        var clamped = Math.Clamp(sample, -1f, 1f);
        return (short)Math.Round(clamped * short.MaxValue, MidpointRounding.AwayFromZero);
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
        // known until the caller stops writing; seek back and patch both size fields now that it
        // is known.
        var riffChunkSize = (int)(36 + _dataBytesWritten);
        _stream.Seek(RiffChunkSizeOffset, SeekOrigin.Begin);
        _writer.Write(riffChunkSize);
        _stream.Seek(DataChunkSizeOffset, SeekOrigin.Begin);
        _writer.Write((int)_dataBytesWritten);

        _writer.Dispose();
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
}
