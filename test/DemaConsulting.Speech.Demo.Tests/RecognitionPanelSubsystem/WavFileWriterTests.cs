using DemaConsulting.Speech.Demo.RecognitionPanelSubsystem;

namespace DemaConsulting.Speech.Demo.Tests.RecognitionPanelSubsystem;

/// <summary>
///     Unit tests for <see cref="WavFileWriter"/>.
/// </summary>
/// <remarks>
///     TEMPORARY diagnostic instrumentation test coverage, matching the scope of
///     <see cref="WavFileWriter"/> itself: these tests exist to give the capture-debug bug
///     investigation confidence the written files are genuinely valid, playable RIFF/WAVE files
///     with correctly round-tripped sample data - not full production-feature test coverage.
/// </remarks>
public class WavFileWriterTests
{
    /// <summary>
    ///     Builds a unique temporary file path inside this test run's own output directory.
    /// </summary>
    /// <returns>A path isolated from any other test run.</returns>
    private static string TempFilePath() => Path.Combine(
        AppContext.BaseDirectory, "WavFileWriterTests", $"{Guid.NewGuid():N}.wav");

    /// <summary>
    ///     Proves that a written file has a valid, correctly-sized canonical RIFF/WAVE/fmt header
    ///     describing the declared sample rate and channel count.
    /// </summary>
    [Fact]
    public void WavFileWriter_Dispose_AfterWritingSamples_WritesValidRiffWaveHeader()
    {
        // Arrange
        var path = TempFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        float[] samples = [0f, 0.5f, -0.5f, 1f];

        // Act
        using (var writer = new WavFileWriter(path, sampleRate: 16000, channelCount: 1))
        {
            writer.WriteSamples(samples);
        }

        // Assert: the canonical 44-byte header identifies this as an uncompressed mono
        // 16-bit/16kHz PCM WAVE file, with chunk sizes patched to the real written length
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(44 + samples.Length * 2, bytes.Length);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(bytes.Length - 8, BitConverter.ToInt32(bytes, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(bytes, 8, 4));
        Assert.Equal("fmt ", System.Text.Encoding.ASCII.GetString(bytes, 12, 4));
        Assert.Equal(16, BitConverter.ToInt32(bytes, 16)); // fmt chunk size
        Assert.Equal(1, BitConverter.ToInt16(bytes, 20)); // format tag: PCM
        Assert.Equal(1, BitConverter.ToInt16(bytes, 22)); // channel count
        Assert.Equal(16000, BitConverter.ToInt32(bytes, 24)); // sample rate
        Assert.Equal(16, BitConverter.ToInt16(bytes, 34)); // bits per sample
        Assert.Equal("data", System.Text.Encoding.ASCII.GetString(bytes, 36, 4));
        Assert.Equal(samples.Length * 2, BitConverter.ToInt32(bytes, 40));
    }

    /// <summary>
    ///     Proves that written samples round-trip through 16-bit PCM within the precision loss
    ///     inherent in the float-to-int16 conversion, including at the +/-1.0 boundary.
    /// </summary>
    [Fact]
    public void WavFileWriter_WriteSamples_NormalizedFloatSamples_RoundTripAsPcm16()
    {
        // Arrange
        var path = TempFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        float[] samples = [0f, 1f, -1f, 0.25f, -0.25f];

        // Act
        using (var writer = new WavFileWriter(path, sampleRate: 8000, channelCount: 1))
        {
            writer.WriteSamples(samples);
        }

        // Assert: read the data chunk back as int16 and compare against the expected scaled value
        var bytes = File.ReadAllBytes(path);
        var dataOffset = 44;
        for (var i = 0; i < samples.Length; i++)
        {
            var expected = (short)Math.Round(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue,
                MidpointRounding.AwayFromZero);
            var actual = BitConverter.ToInt16(bytes, dataOffset + i * 2);
            Assert.Equal(expected, actual);
        }
    }

    /// <summary>
    ///     Proves that out-of-range samples are clamped rather than overflowing into an unrelated
    ///     sample value.
    /// </summary>
    [Fact]
    public void WavFileWriter_WriteSamples_OutOfRangeSample_ClampsToBoundary()
    {
        // Arrange
        var path = TempFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        float[] samples = [2f, -2f];

        // Act
        using (var writer = new WavFileWriter(path, sampleRate: 8000, channelCount: 1))
        {
            writer.WriteSamples(samples);
        }

        // Assert
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(short.MaxValue, BitConverter.ToInt16(bytes, 44));
        Assert.Equal(-short.MaxValue, BitConverter.ToInt16(bytes, 46));
    }

    /// <summary>
    ///     Proves that construction rejects an empty path rather than deferring the failure to a
    ///     later, harder-to-diagnose I/O error.
    /// </summary>
    [Fact]
    public void WavFileWriter_Constructor_EmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new WavFileWriter(string.Empty, 16000, 1));
    }

    /// <summary>
    ///     Proves that construction rejects a non-positive sample rate.
    /// </summary>
    [Fact]
    public void WavFileWriter_Constructor_NonPositiveSampleRate_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var path = TempFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new WavFileWriter(path, 0, 1));
    }

    /// <summary>
    ///     Proves that construction rejects a non-positive channel count.
    /// </summary>
    [Fact]
    public void WavFileWriter_Constructor_NonPositiveChannelCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var path = TempFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new WavFileWriter(path, 16000, 0));
    }

    /// <summary>
    ///     Proves that a second Dispose call is a safe no-op rather than throwing or corrupting
    ///     the already-finalized header.
    /// </summary>
    [Fact]
    public void WavFileWriter_Dispose_CalledTwice_IsIdempotent()
    {
        // Arrange
        var path = TempFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var writer = new WavFileWriter(path, 16000, 1);
        writer.WriteSamples([0.1f]);

        // Act
        writer.Dispose();
        var exception = Record.Exception(() => writer.Dispose());

        // Assert
        Assert.Null(exception);
    }
}
