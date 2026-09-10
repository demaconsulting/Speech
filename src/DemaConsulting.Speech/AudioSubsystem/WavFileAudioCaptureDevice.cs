using System.Numerics.Tensors;
using System.Runtime.InteropServices;

namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     File-backed <see cref="IAudioCaptureDevice"/> implementation that reads normalized
///     samples from a mono, 16-bit PCM RIFF/WAVE file and raises <see cref="FrameCaptured"/> in
///     fixed-size chunks, instead of capturing from real microphone hardware.
/// </summary>
/// <remarks>
///     <para>
///         This is the correct layer for this capability (not a CLI-only helper) because
///         <see cref="IAudioCaptureDevice"/> is exactly the seam
///         <c>SpeechRecognizerFactory.Create</c> already requires, so any host application - not
///         only a future command-line tool - can drive speech recognition from a pre-recorded
///         file deterministically by supplying this class wherever a real capture device would
///         otherwise be used.
///     </para>
///     <para>
///         Because the public <see cref="IAudioCaptureDevice"/> contract has no "end of stream"
///         concept, this concrete class exposes one additional public member beyond the
///         interface: <see cref="EndOfFileReached"/>, raised exactly once after every frame in
///         the file has been delivered through <see cref="FrameCaptured"/>. This is additive, not
///         a breaking interface change: a real live-mic capture device has no equivalent "end"
///         and relies on its own stop conditions instead, so no existing consumer of
///         <see cref="IAudioCaptureDevice"/> is affected.
///     </para>
///     <para>
///         Unlike a real device, <see cref="Start"/> is a fully synchronous, blocking call: it
///         validates the file's RIFF/WAVE header, then reads and delivers every frame in the file
///         before returning, running at full CPU speed rather than throttled to real time - a
///         deliberate design choice, since no live hardware backs this device and there is no
///         wall-clock rate to honor. <see cref="Stop"/> may be called in a reentrant manner from
///         within a <see cref="FrameCaptured"/> or <see cref="EndOfFileReached"/> handler (or
///         from another thread while <see cref="Start"/> is running on a background thread) to
///         interrupt delivery early; doing so causes <see cref="Start"/> to stop delivering
///         further frames and return without raising <see cref="EndOfFileReached"/>, since the
///         file was not fully consumed.
///     </para>
///     <para>
///         Only mono, 16-bit PCM RIFF/WAVE files are supported, matching the mono format speech
///         recognition models require. <see cref="Start"/> rejects any other format - a
///         non-existent file, a malformed RIFF/WAVE header, a missing <c>fmt </c> or
///         <c>data</c> chunk, a non-PCM format tag, a bit depth other than 16, or a channel count
///         other than 1 - by throwing <see cref="InvalidOperationException"/>. This is a
///         deliberate design choice distinct from <see cref="AudioDeviceUnavailableException"/>:
///         an unsupported or malformed input file is an expected, handled caller-configuration
///         error to surface clearly, not a backend-availability failure, so
///         <see cref="IsAvailable"/> always reports <see langword="true"/> for this device
///         rather than modeling file-format problems as "no backend resolved".
///     </para>
/// </remarks>
public sealed class WavFileAudioCaptureDevice : IAudioCaptureDevice
{
    /// <summary>The number of bytes in one 16-bit PCM sample.</summary>
    private const int BytesPerSample = sizeof(short);

    /// <summary>The default number of samples delivered per <see cref="FrameCaptured"/> event.</summary>
    public const int DefaultFrameSampleCount = 1600;

    /// <summary>The full path of the WAV file to read.</summary>
    private readonly string _path;

    /// <summary>The number of samples delivered per <see cref="FrameCaptured"/> event.</summary>
    private readonly int _frameSampleCount;

    /// <summary>Set by <see cref="Stop"/> to interrupt an in-progress <see cref="Start"/> call.</summary>
    /// <remarks>
    ///     Must remain a field, not a local variable: <see cref="Stop"/> writes it and
    ///     <see cref="Start"/> reads it, and the two may run on different threads (a real
    ///     reentrant call from within a <see cref="FrameCaptured"/> handler on the same thread,
    ///     or a genuinely concurrent call from another thread while <see cref="Start"/> runs on a
    ///     background thread).
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell",
        "S1450:Private fields only used as local variables in methods should become local variables",
        Justification = "Shared across Start() and Stop(), which may be invoked from different call stacks/threads.")]
    private volatile bool _stopRequested;

    /// <summary>
    ///     Initializes a new instance of the <see cref="WavFileAudioCaptureDevice"/> class.
    /// </summary>
    /// <param name="path">
    ///     The full path of the mono, 16-bit PCM RIFF/WAVE file to read. Must not be
    ///     <see langword="null"/> or empty. The file is not opened or validated until
    ///     <see cref="Start"/> is called.
    /// </param>
    /// <param name="frameSampleCount">
    ///     The number of samples delivered per <see cref="FrameCaptured"/> event. Must be
    ///     greater than zero. Defaults to <see cref="DefaultFrameSampleCount"/>.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="frameSampleCount"/> is not greater than zero.</exception>
    public WavFileAudioCaptureDevice(string path, int frameSampleCount = DefaultFrameSampleCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(frameSampleCount, 0);

        _path = path;
        _frameSampleCount = frameSampleCount;
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Always <see langword="true"/>: reading from a file never depends on real audio
    ///     hardware, so this device never fails to resolve a backend the way a real device can.
    ///     An unsupported or malformed file is instead reported as an
    ///     <see cref="InvalidOperationException"/> from <see cref="Start"/>, since that is a
    ///     caller-configuration problem, not a backend-availability one.
    /// </remarks>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    /// <remarks>
    ///     Always <c>1</c>: only mono files are supported. Reads as <c>0</c> before
    ///     <see cref="Start"/> has successfully parsed the file's header.
    /// </remarks>
    public int ChannelCount { get; private set; }

    /// <inheritdoc/>
    /// <remarks>
    ///     Reports the sample rate declared in the file's <c>fmt </c> chunk once
    ///     <see cref="Start"/> has successfully parsed it; reads as <c>0</c> beforehand.
    /// </remarks>
    public int SampleRate { get; private set; }

    /// <inheritdoc/>
    public event EventHandler<AudioCaptureFrameEventArgs>? FrameCaptured;

    /// <summary>
    ///     Raised exactly once, after every frame in the file has been delivered through
    ///     <see cref="FrameCaptured"/>, so a caller (typically a command-line
    ///     <c>recognize --input</c> command) knows the file has been fully consumed and can call
    ///     <see cref="Stop"/> and stop waiting for further recognition results. Not raised when
    ///     <see cref="Stop"/> interrupts delivery before the file is fully consumed.
    /// </summary>
    /// <remarks>
    ///     This is an additive member beyond the <see cref="IAudioCaptureDevice"/> contract: the
    ///     interface has no "end of stream" concept because a real live-mic capture device has no
    ///     equivalent "end" and relies on its own stop conditions instead.
    /// </remarks>
    public event EventHandler? EndOfFileReached;

    /// <inheritdoc/>
    /// <remarks>
    ///     Validates the file's RIFF/WAVE header, then synchronously reads and delivers every
    ///     frame in the file through <see cref="FrameCaptured"/> before returning, raising
    ///     <see cref="EndOfFileReached"/> once delivery completes without interruption.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the file does not exist, cannot be opened, or is not a mono, 16-bit PCM
    ///     RIFF/WAVE file.
    /// </exception>
    public void Start()
    {
        _stopRequested = false;

        using var reader = OpenValidatedReader(out var dataByteCount);

        var remainingSamples = dataByteCount / BytesPerSample;

        // Read each block's raw PCM bytes in one bulk call rather than one BinaryReader.ReadInt16()
        // call per sample (each of which pays full BinaryReader/stream call overhead), then
        // reinterpret and convert the whole block to float in two vectorized passes. WAV PCM data
        // is always little-endian, matching the little-endian layout .NET always uses for in-memory
        // short values on every platform this library targets, so the byte-to-short reinterpret
        // cast below is safe without an explicit byte swap.
        var byteBuffer = new byte[_frameSampleCount * BytesPerSample];
        while (remainingSamples > 0 && !_stopRequested)
        {
            var samplesToRead = Math.Min(_frameSampleCount, remainingSamples);
            var byteSpan = byteBuffer.AsSpan(0, samplesToRead * BytesPerSample);
            reader.BaseStream.ReadExactly(byteSpan);

            var samples = new float[samplesToRead];
            var pcmSamples = MemoryMarshal.Cast<byte, short>(byteSpan);
            TensorPrimitives.ConvertChecked<short, float>(pcmSamples, samples);
            TensorPrimitives.Divide(samples, (float)short.MaxValue, samples);

            remainingSamples -= samplesToRead;
            FrameCaptured?.Invoke(this, new AudioCaptureFrameEventArgs(samples));
        }

        if (!_stopRequested)
        {
            EndOfFileReached?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Requests that an in-progress <see cref="Start"/> call stop delivering further frames
    ///     as soon as it observes this request, whether called in a reentrant manner from within
    ///     a
    ///     <see cref="FrameCaptured"/>/<see cref="EndOfFileReached"/> handler or from another
    ///     thread. Safe to call at any time, including when <see cref="Start"/> is not currently
    ///     running, in which case it has no effect beyond arming the flag for the next call.
    /// </remarks>
    public void Stop()
    {
        _stopRequested = true;
    }

    /// <summary>
    ///     Opens the file and validates its RIFF/WAVE header, leaving the returned reader
    ///     positioned at the start of the <c>data</c> chunk's sample bytes.
    /// </summary>
    /// <param name="dataByteCount">The number of sample data bytes declared by the <c>data</c> chunk.</param>
    /// <returns>A <see cref="BinaryReader"/> positioned at the start of the sample data.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the file does not exist, cannot be opened, or is not a mono, 16-bit PCM
    ///     RIFF/WAVE file.
    /// </exception>
    private BinaryReader OpenValidatedReader(out int dataByteCount)
    {
        var reader = new BinaryReader(OpenInputStream());
        try
        {
            ValidateRiffHeader(reader);
            ReadFormatAndLocateDataChunk(
                reader,
                out var audioFormat,
                out var channelCount,
                out var sampleRate,
                out var bitsPerSample,
                out dataByteCount);
            ValidateSupportedFormat(audioFormat, bitsPerSample, channelCount);
            ChannelCount = channelCount;
            SampleRate = sampleRate;
            return reader;
        }
        catch
        {
            // Intentionally broad: any validation fault after the file opens must still dispose
            // the reader so malformed input never leaks an open file handle.
            reader.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Opens the configured WAV file for shared read access so header validation can inspect
    ///     its bytes without blocking other readers.
    /// </summary>
    /// <returns>An open read-only file stream for <see cref="_path"/>.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the file cannot be opened because it is missing, unreadable, or access is
    ///     denied.
    /// </exception>
    private FileStream OpenInputStream()
    {
        try
        {
            return new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Cannot open WAV file '{_path}': {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException($"Cannot open WAV file '{_path}': {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Validates the outer RIFF/WAVE container header before any chunk-level parsing occurs.
    /// </summary>
    /// <param name="reader">
    ///     The reader positioned at the start of the file.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the file does not begin with a valid RIFF/WAVE header.
    /// </exception>
    private void ValidateRiffHeader(BinaryReader reader)
    {
        if (!TagEquals(reader.ReadBytes(4), "RIFF"u8) || reader.BaseStream.Length < 12)
        {
            throw new InvalidOperationException($"'{_path}' is not a valid RIFF file.");
        }

        reader.ReadInt32(); // RIFF chunk size (unused: the data chunk size below is authoritative)
        if (!TagEquals(reader.ReadBytes(4), "WAVE"u8))
        {
            throw new InvalidOperationException($"'{_path}' is not a valid WAVE file.");
        }
    }

    /// <summary>
    ///     Reads chunks until both the required <c>fmt </c> metadata and the <c>data</c> chunk
    ///     location have been discovered.
    /// </summary>
    /// <param name="reader">
    ///     The reader positioned immediately after the RIFF/WAVE container header.
    /// </param>
    /// <param name="audioFormat">Receives the WAV format tag declared by the <c>fmt </c> chunk.</param>
    /// <param name="channelCount">Receives the channel count declared by the <c>fmt </c> chunk.</param>
    /// <param name="sampleRate">Receives the sample rate declared by the <c>fmt </c> chunk.</param>
    /// <param name="bitsPerSample">Receives the bit depth declared by the <c>fmt </c> chunk.</param>
    /// <param name="dataByteCount">Receives the number of audio bytes declared by the <c>data</c> chunk.</param>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the file omits the required chunks or presents them in an unsupported
    ///     order.
    /// </exception>
    private void ReadFormatAndLocateDataChunk(
        BinaryReader reader,
        out short audioFormat,
        out short channelCount,
        out int sampleRate,
        out short bitsPerSample,
        out int dataByteCount)
    {
        var formatFound = false;
        audioFormat = 0;
        channelCount = 0;
        sampleRate = 0;
        bitsPerSample = 0;

        while (true)
        {
            var chunkIdBytes = reader.ReadBytes(4);
            if (chunkIdBytes.Length < 4)
            {
                throw new InvalidOperationException($"'{_path}' has no 'data' chunk.");
            }

            var chunkSize = reader.ReadInt32();
            if (TagEquals(chunkIdBytes, "fmt "u8))
            {
                ReadFormatChunk(reader, chunkSize, out audioFormat, out channelCount, out sampleRate, out bitsPerSample);
                formatFound = true;
                continue;
            }

            if (TagEquals(chunkIdBytes, "data"u8))
            {
                if (!formatFound)
                {
                    throw new InvalidOperationException($"'{_path}' has a 'data' chunk before its 'fmt ' chunk.");
                }

                dataByteCount = chunkSize;
                return;
            }

            SkipRemainder(reader, chunkSize, 0);
        }
    }

    /// <summary>
    ///     Reads the required fields from one <c>fmt </c> chunk and skips any optional trailing
    ///     bytes the chunk may contain.
    /// </summary>
    /// <param name="reader">
    ///     The reader positioned immediately after the <c>fmt </c> chunk size field.
    /// </param>
    /// <param name="chunkSize">The declared size of the <c>fmt </c> chunk.</param>
    /// <param name="audioFormat">Receives the WAV format tag declared by the chunk.</param>
    /// <param name="channelCount">Receives the channel count declared by the chunk.</param>
    /// <param name="sampleRate">Receives the sample rate declared by the chunk.</param>
    /// <param name="bitsPerSample">Receives the bit depth declared by the chunk.</param>
    private static void ReadFormatChunk(
        BinaryReader reader,
        int chunkSize,
        out short audioFormat,
        out short channelCount,
        out int sampleRate,
        out short bitsPerSample)
    {
        audioFormat = reader.ReadInt16();
        channelCount = reader.ReadInt16();
        sampleRate = reader.ReadInt32();
        reader.ReadInt32(); // byte rate (derivable from the fields already read)
        reader.ReadInt16(); // block align (derivable from the fields already read)
        bitsPerSample = reader.ReadInt16();
        SkipRemainder(reader, chunkSize, 16);
    }

    /// <summary>
    ///     Rejects WAV encodings the recognizer pipeline cannot consume, keeping file-format
    ///     policy separate from chunk-parsing mechanics.
    /// </summary>
    /// <param name="audioFormat">The WAV format tag declared by the file.</param>
    /// <param name="bitsPerSample">The WAV bit depth declared by the file.</param>
    /// <param name="channelCount">The WAV channel count declared by the file.</param>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the file is not mono, 16-bit uncompressed PCM.
    /// </exception>
    private void ValidateSupportedFormat(short audioFormat, short bitsPerSample, short channelCount)
    {
        if (audioFormat != 1)
        {
            throw new InvalidOperationException(
                $"'{_path}' uses an unsupported WAV format tag {audioFormat}; only uncompressed PCM (1) is supported.");
        }

        if (bitsPerSample != 16)
        {
            throw new InvalidOperationException(
                $"'{_path}' uses an unsupported bit depth of {bitsPerSample}; only 16-bit PCM is supported.");
        }

        if (channelCount != 1)
        {
            throw new InvalidOperationException(
                $"'{_path}' has {channelCount} channels; only mono WAV files are supported.");
        }
    }

    /// <summary>
    ///     Skips the remaining, already-read portion of a chunk (plus the RIFF odd-length pad
    ///     byte, when applicable), leaving the stream positioned at the start of the next chunk.
    /// </summary>
    /// <param name="reader">The reader positioned immediately after the bytes already consumed from the chunk.</param>
    /// <param name="chunkSize">The declared total size, in bytes, of the chunk.</param>
    /// <param name="bytesAlreadyRead">The number of chunk bytes already consumed by the caller.</param>
    private static void SkipRemainder(BinaryReader reader, int chunkSize, int bytesAlreadyRead)
    {
        var remaining = chunkSize - bytesAlreadyRead;
        var padding = chunkSize % 2 == 1 ? 1 : 0; // RIFF chunks are word-aligned
        if (remaining + padding > 0)
        {
            reader.BaseStream.Seek(remaining + padding, SeekOrigin.Current);
        }
    }

    /// <summary>
    ///     Compares four bytes read from the file against a known four-character RIFF tag.
    /// </summary>
    /// <param name="actual">The four bytes read from the file.</param>
    /// <param name="expected">The expected ASCII tag bytes.</param>
    /// <returns><see langword="true"/> when they match exactly; otherwise, <see langword="false"/>.</returns>
    private static bool TagEquals(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected) =>
        actual.SequenceEqual(expected);
}
