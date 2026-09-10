using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for <see cref="WavFileAudioCaptureDevice"/>.
/// </summary>
public class WavFileAudioCaptureDeviceTests
{
    /// <summary>The existing 16 kHz mono test fixture, reused per Risk 3 instead of authoring new binary fixtures.</summary>
    private static readonly string FixturePath =
        Path.Join(AppContext.BaseDirectory, "TestData", "crossing-the-bar-16k-mono.wav");

    /// <summary>
    ///     Creates a unique temporary file path with a <c>.wav</c> extension for a single test,
    ///     removing any leftover file if one already exists at that path.
    /// </summary>
    private static string CreateTempWavPath()
    {
        var path = Path.Join(Path.GetTempPath(), $"{Path.GetRandomFileName()}.wav");
        File.Delete(path);
        return path;
    }

    /// <summary>
    ///     Writes a minimal mono RIFF/WAVE file at <paramref name="path"/> with an explicit
    ///     format tag and bit depth, since <see cref="WavFileAudioPlaybackDevice"/> only ever
    ///     writes 16-bit PCM (format tag 1) and therefore cannot produce the non-PCM/non-16-bit
    ///     fixtures the format-rejection tests below require.
    /// </summary>
    /// <param name="path">The full path of the file to create.</param>
    /// <param name="formatTag">The WAV format tag to declare in the <c>fmt </c> chunk.</param>
    /// <param name="bitsPerSample">The bit depth to declare in the <c>fmt </c> chunk.</param>
    private static void WriteMinimalWavFile(string path, short formatTag, short bitsPerSample)
    {
        const int sampleRate = 16000;
        const short channelCount = 1;
        var blockAlign = (short)(channelCount * (bitsPerSample / 8));
        var byteRate = sampleRate * blockAlign;
        var data = new byte[blockAlign * 4]; // A handful of silent sample bytes is sufficient

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8);
        writer.Write(36 + data.Length);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write(formatTag);
        writer.Write(channelCount);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(data.Length);
        writer.Write(data);
    }

    /// <summary>
    ///     Proves that starting capture against the existing mono fixture delivers the expected
    ///     total sample count across all raised <see cref="IAudioCaptureDevice.FrameCaptured"/>
    ///     events.
    /// </summary>
    [Fact]
    public void WavFileAudioCaptureDevice_Start_FixtureFile_DeliversExpectedTotalSampleCount()
    {
        // Arrange: the fixture file is 16-bit PCM mono at 16 kHz with a known data byte count
        var expectedSampleCount = (int)(new FileInfo(FixturePath).Length - 44) / sizeof(short);
        var device = new WavFileAudioCaptureDevice(FixturePath);
        var totalSamples = 0;
        device.FrameCaptured += (_, args) => totalSamples += args.Samples.Count;

        // Act: start capture, which synchronously delivers every frame in the file
        device.Start();

        // Assert: the total sample count across all frames matches the file's declared data size
        Assert.Equal(expectedSampleCount, totalSamples);
    }

    /// <summary>
    ///     Proves that <see cref="WavFileAudioCaptureDevice.EndOfFileReached"/> fires exactly
    ///     once, after the last <see cref="IAudioCaptureDevice.FrameCaptured"/> event, when the
    ///     fixture file is fully consumed.
    /// </summary>
    [Fact]
    public void WavFileAudioCaptureDevice_Start_FixtureFile_RaisesEndOfFileReachedExactlyOnceAfterLastFrame()
    {
        // Arrange: a device over the fixture file, tracking event order
        var device = new WavFileAudioCaptureDevice(FixturePath);
        var endOfFileReachedCount = 0;
        var lastEventWasFrameCaptured = false;
        device.FrameCaptured += (_, _) => lastEventWasFrameCaptured = true;
        device.EndOfFileReached += (_, _) =>
        {
            endOfFileReachedCount++;
            lastEventWasFrameCaptured = false;
        };

        // Act: start capture
        device.Start();

        // Assert: EndOfFileReached fired exactly once, and it fired after the last frame
        Assert.Equal(1, endOfFileReachedCount);
        Assert.False(lastEventWasFrameCaptured);
    }

    /// <summary>
    ///     Proves that the device reports the fixture's mono channel count and 16 kHz sample rate
    ///     once <see cref="WavFileAudioCaptureDevice.Start"/> has parsed the header.
    /// </summary>
    [Fact]
    public void WavFileAudioCaptureDevice_Start_FixtureFile_ReportsMonoSixteenKilohertzFormat()
    {
        // Arrange: a device over the fixture file
        var device = new WavFileAudioCaptureDevice(FixturePath);

        // Act: start capture, which parses the header before delivering any frames
        device.Start();

        // Assert: the reported format matches the known fixture format
        Assert.Equal(1, device.ChannelCount);
        Assert.Equal(16000, device.SampleRate);
    }

    /// <summary>
    ///     Proves that the device reports zero for both channel count and sample rate before
    ///     <see cref="WavFileAudioCaptureDevice.Start"/> has been called.
    /// </summary>
    [Fact]
    public void WavFileAudioCaptureDevice_Format_BeforeStart_ReturnsZero()
    {
        // Arrange: a constructed but not-yet-started device
        var device = new WavFileAudioCaptureDevice(FixturePath);

        // Act: read the format before starting
        var channelCount = device.ChannelCount;
        var sampleRate = device.SampleRate;

        // Assert: both are zero
        Assert.Equal(0, channelCount);
        Assert.Equal(0, sampleRate);
    }

    /// <summary>
    ///     Proves that starting capture against a non-existent file throws
    ///     <see cref="InvalidOperationException"/> rather than an unhandled I/O exception.
    /// </summary>
    [Fact]
    public void WavFileAudioCaptureDevice_Start_MissingFile_ThrowsInvalidOperationException()
    {
        // Arrange: a device pointed at a file that does not exist
        var device = new WavFileAudioCaptureDevice(Path.Join(Path.GetTempPath(), Path.GetRandomFileName()));

        // Act & Assert: starting capture throws the documented, handled exception
        Assert.Throws<InvalidOperationException>(device.Start);
    }

    /// <summary>
    ///     Proves that starting capture against a file that is not a valid RIFF/WAVE file throws
    ///     <see cref="InvalidOperationException"/>.
    /// </summary>
    [Fact]
    public void WavFileAudioCaptureDevice_Start_NotARiffFile_ThrowsInvalidOperationException()
    {
        // Arrange: a file containing arbitrary non-RIFF bytes
        var path = CreateTempWavPath();
        File.WriteAllBytes(path, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]);

        try
        {
            var device = new WavFileAudioCaptureDevice(path);

            // Act & Assert: starting capture throws the documented, handled exception
            Assert.Throws<InvalidOperationException>(device.Start);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that starting capture against a file whose <c>fmt </c> chunk is truncated (the
    ///     header declares more bytes than the file actually contains) throws the documented
    ///     <see cref="InvalidOperationException"/> rather than letting a <see cref="BinaryReader"/>
    ///     <see cref="EndOfStreamException"/> escape unhandled.
    /// </summary>
    [Fact]
    public void WavFileAudioCaptureDevice_Start_TruncatedFmtChunk_ThrowsInvalidOperationException()
    {
        // Arrange: a RIFF/WAVE file whose "fmt " chunk declares 16 bytes but the file ends
        // partway through the chunk's fields.
        var path = CreateTempWavPath();
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write("RIFF"u8);
            writer.Write(0);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16); // Declares a full 16-byte fmt chunk...
            writer.Write((short)1); // ...but the file ends after only the format-tag field.
        }

        try
        {
            var device = new WavFileAudioCaptureDevice(path);

            // Act & Assert: starting capture throws the documented, handled exception, not a
            // raw EndOfStreamException.
            Assert.Throws<InvalidOperationException>(device.Start);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that starting capture against a stereo WAV file throws
    ///     <see cref="InvalidOperationException"/>, since only mono files are supported.
    /// </summary>
    [Fact]
    public void WavFileAudioCaptureDevice_Start_StereoFile_ThrowsInvalidOperationException()
    {
        // Arrange: a stereo WAV file written through the playback device
        var path = CreateTempWavPath();

        try
        {
            using (var writer = new WavFileAudioPlaybackDevice(path, 16000, 2))
            {
                writer.Write([0.1f, 0.2f, 0.3f, 0.4f]);
            }

            var device = new WavFileAudioCaptureDevice(path);

            // Act & Assert: starting capture throws, since only mono files are supported
            Assert.Throws<InvalidOperationException>(device.Start);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that starting capture against a non-PCM WAV file (IEEE float, format tag 3)
    ///     throws <see cref="InvalidOperationException"/>, since only uncompressed PCM is
    ///     supported.
    /// </summary>
    [Fact]
    public void WavFileAudioCaptureDevice_Start_NonPcmFormatTag_ThrowsInvalidOperationException()
    {
        // Arrange: a mono, 32-bit "PCM" file that actually declares format tag 3 (IEEE float)
        var path = CreateTempWavPath();

        try
        {
            WriteMinimalWavFile(path, formatTag: 3, bitsPerSample: 32);

            var device = new WavFileAudioCaptureDevice(path);

            // Act & Assert: starting capture throws, since only uncompressed PCM is supported
            Assert.Throws<InvalidOperationException>(device.Start);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that starting capture against a non-16-bit WAV file (8-bit PCM) throws
    ///     <see cref="InvalidOperationException"/>, since only 16-bit PCM is supported.
    /// </summary>
    [Fact]
    public void WavFileAudioCaptureDevice_Start_EightBitDepth_ThrowsInvalidOperationException()
    {
        // Arrange: a mono, 8-bit PCM file
        var path = CreateTempWavPath();

        try
        {
            WriteMinimalWavFile(path, formatTag: 1, bitsPerSample: 8);

            var device = new WavFileAudioCaptureDevice(path);

            // Act & Assert: starting capture throws, since only 16-bit PCM is supported
            Assert.Throws<InvalidOperationException>(device.Start);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that starting capture against a non-16-bit WAV file (24-bit PCM) throws
    ///     <see cref="InvalidOperationException"/>, since only 16-bit PCM is supported.
    /// </summary>
    [Fact]
    public void WavFileAudioCaptureDevice_Start_TwentyFourBitDepth_ThrowsInvalidOperationException()
    {
        // Arrange: a mono, 24-bit PCM file
        var path = CreateTempWavPath();

        try
        {
            WriteMinimalWavFile(path, formatTag: 1, bitsPerSample: 24);

            var device = new WavFileAudioCaptureDevice(path);

            // Act & Assert: starting capture throws, since only 16-bit PCM is supported
            Assert.Throws<InvalidOperationException>(device.Start);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that calling <see cref="WavFileAudioCaptureDevice.Stop"/> from within a
    ///     <see cref="IAudioCaptureDevice.FrameCaptured"/> handler interrupts delivery before the
    ///     file is fully consumed and does not raise <see cref="WavFileAudioCaptureDevice.EndOfFileReached"/>.
    /// </summary>
    [Fact]
    public void WavFileAudioCaptureDevice_Stop_CalledDuringFrameCaptured_InterruptsDeliveryWithoutEndOfFileReached()
    {
        // Arrange: a device over the fixture file, stopped after the first frame
        var device = new WavFileAudioCaptureDevice(FixturePath, frameSampleCount: 100);
        var frameCount = 0;
        device.FrameCaptured += (_, _) =>
        {
            frameCount++;
            device.Stop();
        };
        var endOfFileReached = false;
        device.EndOfFileReached += (_, _) => endOfFileReached = true;

        // Act: start capture
        device.Start();

        // Assert: delivery stopped after exactly one frame, and EndOfFileReached was not raised
        Assert.Equal(1, frameCount);
        Assert.False(endOfFileReached);
    }

    /// <summary>
    ///     Proves that the device always reports itself as available, since reading from a file
    ///     never depends on real audio hardware.
    /// </summary>
    [Fact]
    public void WavFileAudioCaptureDevice_IsAvailable_Always_ReturnsTrue()
    {
        // Arrange: a constructed device
        var device = new WavFileAudioCaptureDevice(FixturePath);

        // Act: read the availability flag
        var isAvailable = device.IsAvailable;

        // Assert: always true
        Assert.True(isAvailable);
    }

    /// <summary>
    ///     Proves that the constructor rejects a <see langword="null"/> or empty path.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void WavFileAudioCaptureDevice_Constructor_NullOrEmptyPath_ThrowsArgumentException(string? path)
    {
        // Act & Assert: constructing with an invalid path throws (ArgumentNullException for
        // null, ArgumentException for empty/whitespace, both derived from ArgumentException)
        Assert.ThrowsAny<ArgumentException>(() => new WavFileAudioCaptureDevice(path!));
    }

    /// <summary>
    ///     Proves that the constructor rejects a non-positive frame sample count.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WavFileAudioCaptureDevice_Constructor_NonPositiveFrameSampleCount_ThrowsArgumentOutOfRangeException(int frameSampleCount)
    {
        // Act & Assert: constructing with a non-positive frame sample count throws
        Assert.Throws<ArgumentOutOfRangeException>(() => new WavFileAudioCaptureDevice(FixturePath, frameSampleCount));
    }
}
