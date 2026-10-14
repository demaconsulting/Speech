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

        // Act
        var fraction = progress.FractionComplete;

        // Assert
        Assert.Equal(0.25, fraction);
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

        // Act
        var fraction = progress.FractionComplete;

        // Assert
        Assert.Null(fraction);
    }

    /// <summary>
    ///     Proves that a zero-byte total is treated as fully complete rather than dividing by zero.
    /// </summary>
    [Fact]
    public void SpeechModelDownloadProgress_FractionComplete_ZeroTotalBytes_ReturnsOne()
    {
        // Arrange
        var progress = new SpeechModelDownloadProgress(0, 1, 0, 0);

        // Act
        var fraction = progress.FractionComplete;

        // Assert
        Assert.Equal(1.0, fraction);
    }

    /// <summary>
    ///     Proves that more bytes than the declared total is reported as exactly complete, never above one.
    /// </summary>
    [Fact]
    public void SpeechModelDownloadProgress_FractionComplete_BytesOverTotal_ClampsToOne()
    {
        // Arrange
        var progress = new SpeechModelDownloadProgress(0, 1, 250, 200);

        // Act
        var fraction = progress.FractionComplete;

        // Assert
        Assert.Equal(1.0, fraction);
    }

    /// <summary>
    ///     Proves that a negative transferred byte count is reported as no progress, never below zero.
    /// </summary>
    [Fact]
    public void SpeechModelDownloadProgress_FractionComplete_NegativeBytes_ClampsToZero()
    {
        // Arrange
        var progress = new SpeechModelDownloadProgress(0, 1, -10, 200);

        // Act
        var fraction = progress.FractionComplete;

        // Assert
        Assert.Equal(0.0, fraction);
    }
}
