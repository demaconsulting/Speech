using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for <see cref="WavFileAudioPlaybackDevice"/>.
/// </summary>
public class WavFileAudioPlaybackDeviceTests
{
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
    ///     Proves that samples written through <see cref="WavFileAudioPlaybackDevice.Write"/> and
    ///     finalized by <see cref="WavFileAudioPlaybackDevice.Dispose"/> round-trip through
    ///     <see cref="WavFileAudioCaptureDevice"/> within 16-bit quantization tolerance.
    /// </summary>
    [Fact]
    public void WavFileAudioPlaybackDevice_WriteThenDispose_RoundTripsSamplesWithinQuantizationTolerance()
    {
        // Arrange: known samples and a temporary file path
        var path = CreateTempWavPath();
        float[] samples = [0.0f, 0.5f, -0.5f, 1.0f, -1.0f, 0.25f];

        try
        {
            // Act: write the samples through the device, then read them back through the capture device
            using (var device = new WavFileAudioPlaybackDevice(path, 16000, 1))
            {
                device.Start();
                device.Write(samples);
                device.Stop();
            }

            List<float> captured = [];
            var captureDevice = new WavFileAudioCaptureDevice(path);
            captureDevice.FrameCaptured += (_, args) => captured.AddRange(args.Samples);
            captureDevice.Start();

            // Assert: every sample round-trips within 16-bit quantization tolerance
            Assert.Equal(samples.Length, captured.Count);
            for (var index = 0; index < samples.Length; index++)
            {
                Assert.Equal(samples[index], captured[index], 0.001);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that the constructor rejects a <see langword="null"/> or empty path.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void WavFileAudioPlaybackDevice_Constructor_NullOrEmptyPath_ThrowsArgumentException(string? path)
    {
        // Act & Assert: constructing with an invalid path throws (ArgumentNullException for
        // null, ArgumentException for empty/whitespace, both derived from ArgumentException)
        Assert.ThrowsAny<ArgumentException>(() => new WavFileAudioPlaybackDevice(path!, 16000, 1));
    }

    /// <summary>
    ///     Proves that the constructor rejects a non-positive sample rate.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WavFileAudioPlaybackDevice_Constructor_NonPositiveSampleRate_ThrowsArgumentOutOfRangeException(int sampleRate)
    {
        // Arrange: a temporary file path
        var path = CreateTempWavPath();

        try
        {
            // Act & Assert: constructing with a non-positive sample rate throws
            Assert.Throws<ArgumentOutOfRangeException>(() => new WavFileAudioPlaybackDevice(path, sampleRate, 1));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that the constructor rejects a non-positive channel count.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WavFileAudioPlaybackDevice_Constructor_NonPositiveChannelCount_ThrowsArgumentOutOfRangeException(int channelCount)
    {
        // Arrange: a temporary file path
        var path = CreateTempWavPath();

        try
        {
            // Act & Assert: constructing with a non-positive channel count throws
            Assert.Throws<ArgumentOutOfRangeException>(() => new WavFileAudioPlaybackDevice(path, 16000, channelCount));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that the device always reports itself as available, since writing to a file
    ///     never depends on real audio hardware.
    /// </summary>
    [Fact]
    public void WavFileAudioPlaybackDevice_IsAvailable_Always_ReturnsTrue()
    {
        // Arrange: a constructed device
        var path = CreateTempWavPath();

        try
        {
            using var device = new WavFileAudioPlaybackDevice(path, 16000, 1);

            // Act: read the availability flag
            var isAvailable = device.IsAvailable;

            // Assert: always true
            Assert.True(isAvailable);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that the device reports the sample rate and channel count supplied at
    ///     construction.
    /// </summary>
    [Fact]
    public void WavFileAudioPlaybackDevice_Format_Read_ReflectsConstructorArguments()
    {
        // Arrange: a constructed device with specific format arguments
        var path = CreateTempWavPath();

        try
        {
            using var device = new WavFileAudioPlaybackDevice(path, 22050, 2);

            // Act: read the reported format
            var sampleRate = device.SampleRate;
            var channelCount = device.ChannelCount;

            // Assert: both match the constructor arguments
            Assert.Equal(22050, sampleRate);
            Assert.Equal(2, channelCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that <see cref="WavFileAudioPlaybackDevice.Start"/> and
    ///     <see cref="WavFileAudioPlaybackDevice.Stop"/> are safe no-ops that never throw.
    /// </summary>
    [Fact]
    public void WavFileAudioPlaybackDevice_StartThenStop_Always_DoesNotThrow()
    {
        // Arrange: a constructed device
        var path = CreateTempWavPath();

        try
        {
            using var device = new WavFileAudioPlaybackDevice(path, 16000, 1);

            // Act: start then stop
            var exception = Record.Exception(() =>
            {
                device.Start();
                device.Stop();
            });

            // Assert: no exception is thrown
            Assert.Null(exception);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that <see cref="WavFileAudioPlaybackDevice.PendingSampleCount"/> always reports
    ///     zero, since every write is fully synchronous.
    /// </summary>
    [Fact]
    public void WavFileAudioPlaybackDevice_PendingSampleCount_AfterWrite_ReturnsZero()
    {
        // Arrange: a constructed device with some samples written
        var path = CreateTempWavPath();

        try
        {
            using var device = new WavFileAudioPlaybackDevice(path, 16000, 1);
            device.Write([0.1f, 0.2f, 0.3f]);

            // Act: read the pending sample count
            var pendingSampleCount = device.PendingSampleCount;

            // Assert: always zero
            Assert.Equal(0, pendingSampleCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that <see cref="WavFileAudioPlaybackDevice.Write"/> rejects a
    ///     <see langword="null"/> sample buffer.
    /// </summary>
    [Fact]
    public void WavFileAudioPlaybackDevice_Write_NullSamples_ThrowsArgumentNullException()
    {
        // Arrange: a constructed device
        var path = CreateTempWavPath();

        try
        {
            using var device = new WavFileAudioPlaybackDevice(path, 16000, 1);

            // Act & Assert: writing a null buffer throws
            Assert.Throws<ArgumentNullException>(() => device.Write(null!));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that <see cref="WavFileAudioPlaybackDevice.Write"/> throws
    ///     <see cref="ObjectDisposedException"/> once the device has been disposed.
    /// </summary>
    [Fact]
    public void WavFileAudioPlaybackDevice_Write_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange: a device that has already been disposed
        var path = CreateTempWavPath();

        try
        {
            var device = new WavFileAudioPlaybackDevice(path, 16000, 1);
            device.Dispose();

            // Act & Assert: writing after disposal throws
            Assert.Throws<ObjectDisposedException>(() => device.Write([0.1f]));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that calling <see cref="WavFileAudioPlaybackDevice.Dispose"/> more than once is
    ///     safe and does not throw.
    /// </summary>
    [Fact]
    public void WavFileAudioPlaybackDevice_Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange: a constructed device
        var path = CreateTempWavPath();

        try
        {
            var device = new WavFileAudioPlaybackDevice(path, 16000, 1);
            device.Write([0.1f]);
            device.Dispose();

            // Act & Assert: disposing a second time does not throw
            var exception = Record.Exception(device.Dispose);
            Assert.Null(exception);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that samples at or beyond the +/-1.0 clamp boundary map to the nearest valid
    ///     16-bit value rather than overflowing, by round-tripping extreme values through the
    ///     capture device and confirming they land at the expected clamped extremes.
    /// </summary>
    [Fact]
    public void WavFileAudioPlaybackDevice_Write_SamplesBeyondClampBoundary_ClampToNearestValidValue()
    {
        // Arrange: samples beyond the documented [-1.0, 1.0] range
        var path = CreateTempWavPath();
        float[] samples = [2.0f, -2.0f];

        try
        {
            // Act: write the out-of-range samples then read them back
            using (var device = new WavFileAudioPlaybackDevice(path, 16000, 1))
            {
                device.Write(samples);
            }

            List<float> captured = [];
            var captureDevice = new WavFileAudioCaptureDevice(path);
            captureDevice.FrameCaptured += (_, args) => captured.AddRange(args.Samples);
            captureDevice.Start();

            // Assert: both samples clamp to the nearest valid 16-bit extreme
            Assert.Equal(2, captured.Count);
            Assert.Equal(1.0f, captured[0], 0.001);
            Assert.Equal(-1.0f, captured[1], 0.001);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
