using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for the <see cref="SpeechModelDownloadProgress"/> record.
/// </summary>
public class SpeechModelDownloadProgressTests
{
    /// <summary>
    ///     Proves that <see cref="SpeechModelDownloadProgress.FractionComplete"/> computes the
    ///     expected fraction when the total size is known.
    /// </summary>
    [Fact]
    public void SpeechModelDownloadProgress_FractionComplete_KnownTotal_ComputesFraction()
    {
        // Arrange
        var progress = new SpeechModelDownloadProgress(0, 1, 50, 200);

        // Act & Assert
        Assert.Equal(0.25, progress.FractionComplete);
    }

    /// <summary>
    ///     Proves that <see cref="SpeechModelDownloadProgress.FractionComplete"/> is null when the
    ///     total size is unknown, so a GUI can distinguish "0%" from "unknown".
    /// </summary>
    [Fact]
    public void SpeechModelDownloadProgress_FractionComplete_UnknownTotal_ReturnsNull()
    {
        // Arrange
        var progress = new SpeechModelDownloadProgress(0, 1, 50, null);

        // Act & Assert
        Assert.Null(progress.FractionComplete);
    }

    /// <summary>
    ///     Proves that a zero-byte total is treated as fully complete rather than dividing by zero.
    /// </summary>
    [Fact]
    public void SpeechModelDownloadProgress_FractionComplete_ZeroTotalBytes_ReturnsOne()
    {
        // Arrange
        var progress = new SpeechModelDownloadProgress(0, 1, 0, 0);

        // Act & Assert
        Assert.Equal(1.0, progress.FractionComplete);
    }
}
