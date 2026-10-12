using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for <see cref="AudioFileReader"/>, <see cref="AudioFileReadOptions"/> and <see cref="AudioClip"/>.
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

    /// <summary>Proves error cases: missing file, unknown format, bad options and path-qualified messages.</summary>
    [Fact]
    public void AudioFileReader_Read_Errors_AreReported()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"speech-{Guid.NewGuid():N}.wav");
        File.WriteAllBytes(path, "not audio at all, just text"u8.ToArray());

        try
        {
            // Act / Assert
            Assert.Throws<FileNotFoundException>(() => AudioFileReader.Read(Path.Combine(FlacDirectory, "missing.flac")));
            var error = Assert.Throws<InvalidDataException>(() => AudioFileReader.Read(path));
            Assert.Contains(path, error.Message);
            Assert.Throws<InvalidDataException>(() => AudioFileReader.Read(new MemoryStream([1, 2, 3])));
            Assert.Throws<ArgumentNullException>(() => AudioFileReader.Read((Stream)null!));
            Assert.Throws<ArgumentException>(() => AudioFileReader.Read(string.Empty));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AudioFileReader.Read(new MemoryStream([1]), new AudioFileReadOptions { MaxSamples = -1 }));
        }
        finally
        {
            File.Delete(path);
        }
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

    /// <summary>Proves the clip's derived members.</summary>
    [Fact]
    public void AudioClip_DerivedMembers_AreConsistent()
    {
        // Arrange
        var clip = new AudioClip([1f, -1f, 0.5f, 0.5f, 2f, -2f, 0f, float.NaN], 4, 2, 16);

        // Act
        var mono = clip.ToMono();
        var pcm = clip.ToPcm16();

        // Assert
        Assert.Equal(4, clip.FrameCount);
        Assert.Equal(1.0, clip.Duration);
        Assert.Equal([0f, 0.5f, 0f, float.NaN], mono);
        Assert.Equal(8, pcm.Length);
        Assert.Equal(32767, pcm[0]);
        Assert.Equal(-32768, pcm[1]);
        Assert.Equal(32767, pcm[4]);
        Assert.Equal(-32768, pcm[5]);
        Assert.Equal(0, pcm[7]);
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

    /// <summary>A read-only stream that reports it cannot seek.</summary>
    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data, writable: false)
    {
        /// <inheritdoc/>
        public override bool CanSeek => false;
    }
}
