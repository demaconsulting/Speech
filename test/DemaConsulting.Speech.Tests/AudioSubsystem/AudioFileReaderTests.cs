using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for <see cref="AudioFileReader"/> and <see cref="AudioFileReadOptions"/>.
/// </summary>
public sealed class AudioFileReaderTests
{
    /// <summary>The fixture directory.</summary>
    private static readonly string FlacDirectory = Path.Combine(AppContext.BaseDirectory, "TestData", "flac");

    /// <summary>Proves the supported extensions are .wav and .flac.</summary>
    [Fact]
    public void AudioFileReader_SupportedExtensions_ListsWavAndFlac() =>
        Assert.Equal([".wav", ".flac"], AudioFileReader.SupportedExtensions);

    /// <summary>Proves a file is read by content, whatever its extension.</summary>
    [Fact]
    public void AudioFileReader_Read_DetectsContainerByContent()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"speech-{Guid.NewGuid():N}.dat");
        File.Copy(Path.Combine(FlacDirectory, "mono-16-44100.flac"), path);

        try
        {
            // Act
            var clip = AudioFileReader.Read(path);

            // Assert
            Assert.Equal(44100, clip.SampleRate);
            Assert.Equal(1, clip.ChannelCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Proves a WAV file is also read by content when its extension is unrelated.</summary>
    [Fact]
    public void AudioFileReader_Read_WavUnderUnrelatedExtension_DetectsWavByContent()
    {
        // Arrange: a minimal 16-bit mono WAV holding one sample
        byte[] image =
        [
            .. "RIFF"u8.ToArray(), 38, 0, 0, 0, .. "WAVE"u8.ToArray(),
            .. "fmt "u8.ToArray(), 16, 0, 0, 0, 1, 0, 1, 0, 0x40, 0x1F, 0, 0, 0x80, 0x3E, 0, 0, 2, 0, 16, 0,
            .. "data"u8.ToArray(), 2, 0, 0, 0, 0, 0x40,
        ];
        var path = Path.Combine(Path.GetTempPath(), $"speech-{Guid.NewGuid():N}.dat");
        File.WriteAllBytes(path, image);

        try
        {
            // Act
            var clip = AudioFileReader.Read(path);

            // Assert
            Assert.Equal(8000, clip.SampleRate);
            Assert.Equal([0.5f], clip.Samples);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Proves a stream that is neither WAV nor FLAC is reported with <see cref="InvalidDataException"/> and no path prefix.</summary>
    [Fact]
    public void AudioFileReader_Read_UnrecognizedStream_ThrowsInvalidDataWithoutPath()
    {
        // Act
        var error = Assert.Throws<InvalidDataException>(() => AudioFileReader.Read(new MemoryStream([1, 2, 3])));

        // Assert: the reader's own message, with no path prefix because a stream has no path
        Assert.Contains("neither a RIFF/WAVE nor a FLAC", error.Message);
        Assert.DoesNotContain("'", error.Message);
    }

    /// <summary>Proves a WAV image is decoded from a stream and the same errors apply to <c>ReadMono</c>.</summary>
    [Fact]
    public void AudioFileReader_Read_WavStream_DecodesAndReadMonoRepeatsErrors()
    {
        // Arrange
        var wav = File.ReadAllBytes(Path.Combine(FlacDirectory, "mono-16-44100.wav"));

        // Act
        var clip = AudioFileReader.Read(new MemoryStream(wav));

        // Assert
        Assert.Equal(44100, clip.SampleRate);
        Assert.Equal(1, clip.ChannelCount);
        Assert.Throws<InvalidDataException>(() => AudioFileReader.ReadMono(new MemoryStream([1, 2, 3])));
        Assert.Throws<FileNotFoundException>(() => AudioFileReader.ReadMono(Path.Combine(FlacDirectory, "missing.flac")));
    }

    /// <summary>Proves the VerifyChecksums option is passed to the FLAC decoder: a damaged frame fails by default and decodes when disabled.</summary>
    [Fact]
    public void AudioFileReader_Read_VerifyChecksums_AppliesToFlacOnly()
    {
        // Arrange: the last byte belongs to the final frame's CRC-16 footer
        var flac = File.ReadAllBytes(Path.Combine(FlacDirectory, "mono-16-44100.flac"));
        flac[^1] ^= 0xFF;

        // Act
        var lenient = AudioFileReader.Read(new MemoryStream(flac), new AudioFileReadOptions { VerifyChecksums = false });

        // Assert
        Assert.Throws<InvalidDataException>(() => AudioFileReader.Read(new MemoryStream(flac)));
        Assert.Throws<InvalidDataException>(() => AudioFileReader.Read(new MemoryStream(flac), new AudioFileReadOptions { VerifyChecksums = true }));
        Assert.Equal(44100, lenient.SampleRate);
    }

    /// <summary>Proves the MaxSamples limit applies to the decoded sample count, not only the byte cap.</summary>
    [Fact]
    public void AudioFileReader_Read_MaxSamples_LimitsDecodedSamples()
    {
        // Arrange: the fixture is far below the byte cap implied by MaxSamples = 100 (1 MiB of slack) but holds more than 100 samples
        var flac = File.ReadAllBytes(Path.Combine(FlacDirectory, "mono-16-44100.flac"));
        var wav = File.ReadAllBytes(Path.Combine(FlacDirectory, "mono-16-44100.wav"));
        var options = new AudioFileReadOptions { MaxSamples = 100 };

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => AudioFileReader.Read(new MemoryStream(flac), options));
        Assert.Throws<InvalidDataException>(() => AudioFileReader.Read(new MemoryStream(wav), options));
    }

    /// <summary>Proves a missing file is reported with <see cref="FileNotFoundException"/>.</summary>
    [Fact]
    public void AudioFileReader_Read_MissingFile_ThrowsFileNotFound() =>
        Assert.Throws<FileNotFoundException>(() =>
            AudioFileReader.Read(Path.Combine(Path.GetTempPath(), $"speech-missing-{Guid.NewGuid():N}.wav")));

    /// <summary>Proves an unrecognized file is reported with <see cref="InvalidDataException"/> naming the path.</summary>
    [Fact]
    public void AudioFileReader_Read_UnrecognizedFile_ThrowsInvalidDataWithPath()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"speech-{Guid.NewGuid():N}.wav");
        File.WriteAllBytes(path, "not audio at all, just text"u8.ToArray());

        try
        {
            // Act
            var error = Assert.Throws<InvalidDataException>(() => AudioFileReader.Read(path));

            // Assert
            Assert.Contains(path, error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Proves invalid arguments are reported with the matching argument exception.</summary>
    [Fact]
    public void AudioFileReader_Read_InvalidArguments_ThrowArgumentExceptions()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => AudioFileReader.Read((Stream)null!));
        Assert.Throws<ArgumentException>(() => AudioFileReader.Read(string.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AudioFileReader.Read(new MemoryStream([1]), new AudioFileReadOptions { MaxSamples = -1 }));
    }

    /// <summary>Proves a negative MaxSamples is reported against the options parameter, and for a path only after the file is found.</summary>
    [Fact]
    public void AudioFileReader_Read_NegativeMaxSamples_ReportsOptionsParameter()
    {
        // Arrange
        var negative = new AudioFileReadOptions { MaxSamples = -1 };
        var missing = Path.Combine(FlacDirectory, "missing.flac");

        // Act
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => AudioFileReader.Read(new MemoryStream([1]), negative));

        // Assert: the existence check comes first for path-based reads
        Assert.Equal("options", error.ParamName);
        Assert.Throws<FileNotFoundException>(() => AudioFileReader.Read(missing, negative));
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioFileReader.Read(Path.Combine(FlacDirectory, "mono-16-44100.flac"), negative));
    }

    /// <summary>Proves an unsupported WAV encoding is reported with the path.</summary>
    [Fact]
    public void AudioFileReader_Read_UnsupportedWav_ThrowsNotSupportedWithPath()
    {
        // Arrange: format tag 2 (ADPCM)
        var bytes = File.ReadAllBytes(Path.Combine(FlacDirectory, "mono-16-44100.wav"));
        bytes[20] = 2;
        var path = Path.Combine(Path.GetTempPath(), $"speech-{Guid.NewGuid():N}.wav");
        File.WriteAllBytes(path, bytes);

        try
        {
            // Act / Assert
            var error = Assert.Throws<NotSupportedException>(() => AudioFileReader.Read(path));
            Assert.Contains(path, error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Proves ReadMono down-mixes and reports the native rate.</summary>
    [Fact]
    public void AudioFileReader_ReadMono_Stereo_DownMixesAtNativeRate()
    {
        // Arrange
        var path = Path.Combine(FlacDirectory, "stereo-16-48000.flac");

        // Act
        var (samples, rate) = AudioFileReader.ReadMono(path);
        var (streamSamples, streamRate) = AudioFileReader.ReadMono(new MemoryStream(File.ReadAllBytes(path)));
        var clip = AudioFileReader.Read(path);

        // Assert
        Assert.Equal(48000, rate);
        Assert.Equal(rate, streamRate);
        Assert.Equal(clip.FrameCount, samples.Length);
        Assert.Equal(samples, streamSamples);
    }

    /// <summary>Proves an oversized file is rejected from its length alone, naming the file.</summary>
    [Fact]
    public void AudioFileReader_Read_FileLargerThanByteCap_ThrowsBeforeDecoding()
    {
        // Arrange - with MaxSamples 0 the cap is the 1 MiB slack, so 2 MiB of zeros is over it
        var path = Path.Combine(Path.GetTempPath(), $"speech-{Guid.NewGuid():N}.wav");
        File.WriteAllBytes(path, new byte[2 << 20]);

        try
        {
            // Act
            var ex = Assert.Throws<InvalidDataException>(() => AudioFileReader.Read(path, new AudioFileReadOptions { MaxSamples = 0 }));

            // Assert
            Assert.Contains("limit", ex.Message);
            Assert.Contains(path, ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Proves a seekable stream over the byte cap is rejected from its length.</summary>
    [Fact]
    public void AudioFileReader_Read_SeekableStreamLargerThanByteCap_Throws() =>
        Assert.Throws<InvalidDataException>(() =>
            AudioFileReader.Read(new MemoryStream(new byte[2 << 20]), new AudioFileReadOptions { MaxSamples = 0 }));

    /// <summary>Proves a non-seekable stream is read in bounded fashion and rejected once it passes the cap.</summary>
    [Fact]
    public void AudioFileReader_Read_NonSeekableStreamLargerThanByteCap_Throws() =>
        Assert.Throws<InvalidDataException>(() =>
            AudioFileReader.Read(new NonSeekableStream(new byte[2 << 20]), new AudioFileReadOptions { MaxSamples = 0 }));

    /// <summary>Proves a non-seekable stream within the cap still decodes.</summary>
    [Fact]
    public void AudioFileReader_Read_NonSeekableStream_Decodes()
    {
        // Arrange
        var bytes = File.ReadAllBytes(Path.Combine(FlacDirectory, "mono-16-44100.flac"));

        // Act
        var clip = AudioFileReader.Read(new NonSeekableStream(bytes));

        // Assert
        Assert.Equal(44100, clip.SampleRate);
    }

    /// <summary>
    ///     Proves the byte cap is clamped to the largest array length even when MaxSamples is unbounded: a stream that
    ///     only reports a length above <see cref="Array.MaxLength"/> is rejected from its length, without allocating.
    /// </summary>
    [Fact]
    public void AudioFileReader_Read_UnboundedMaxSamples_ClampsByteCapToArrayMaxLength()
    {
        // Arrange - the stream claims one byte more than the largest array but holds no data
        using var stream = new LengthOnlyStream((long)Array.MaxLength + 1);

        // Act
        var ex = Assert.Throws<InvalidDataException>(() =>
            AudioFileReader.Read(stream, new AudioFileReadOptions { MaxSamples = long.MaxValue }));

        // Assert - the reported limit is the array clamp rather than the unbounded sample-derived value
        Assert.Contains($"{Array.MaxLength}-byte limit", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, stream.BytesRead);
    }

    /// <summary>A seekable stream that reports a length but holds no data, to test size limits without allocating.</summary>
    private sealed class LengthOnlyStream(long length) : Stream
    {
        /// <summary>Gets the number of bytes read from the stream.</summary>
        public int BytesRead { get; private set; }

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanSeek => true;

        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override long Length => length;

        /// <inheritdoc/>
        public override long Position
        {
            get => 0;
            set => throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override void Flush()
        {
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            BytesRead += count;
            return 0;
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>A read-only stream that reports it cannot seek.</summary>
    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data, writable: false)
    {
        /// <inheritdoc/>
        public override bool CanSeek => false;
    }
}
