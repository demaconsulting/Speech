using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for <see cref="WavFileAudioCaptureDevice"/>.
/// </summary>
public class WavFileAudioCaptureDeviceTests
{
    /// <summary>The existing 16 kHz mono test fixture, reused per Risk 3 instead of authoring new binary fixtures.</summary>
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "crossing-the-bar-16k-mono.wav");

    /// <summary>
    ///     Creates a unique temporary file path with a <c>.wav</c> extension for a single test,
    ///     removing any leftover file if one already exists at that path.
    /// </summary>
    private static string CreateTempWavPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.wav");
        File.Delete(path);
        return path;
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
        var device = new WavFileAudioCaptureDevice(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

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
