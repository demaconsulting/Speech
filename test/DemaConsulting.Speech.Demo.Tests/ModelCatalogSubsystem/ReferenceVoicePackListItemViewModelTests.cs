using DemaConsulting.Speech.Demo.ModelCatalogSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo.Tests.ModelCatalogSubsystem;

/// <summary>
///     Unit tests for <see cref="ReferenceVoicePackListItemViewModel"/>.
/// </summary>
public class ReferenceVoicePackListItemViewModelTests
{
    /// <summary>
    ///     Proves that the row rejects a missing pack descriptor.
    /// </summary>
    [Fact]
    public void ReferenceVoicePackListItemViewModel_Constructor_NullPack_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ReferenceVoicePackListItemViewModel(null!, SpeechModelState.NotDownloaded));
    }

    /// <summary>
    ///     Proves that the row projects the pack's identity, size, license, attribution and the
    ///     library's consent notice.
    /// </summary>
    [Fact]
    public void ReferenceVoicePackListItemViewModel_Constructor_ProjectsPackDetails()
    {
        // Act
        var row = new ReferenceVoicePackListItemViewModel(ReferenceVoicePack.VoiceZero10, SpeechModelState.NotDownloaded);

        // Assert
        Assert.Equal("voice-zero-1.0", row.Id);
        Assert.Equal(ReferenceVoicePack.VoiceZero10.DisplayName, row.DisplayName);
        Assert.Equal("570 MiB download", row.SizeText);
        Assert.Contains("CC0 1.0 Universal", row.LicenseText, StringComparison.Ordinal);
        Assert.Contains("OwenTyme", row.LicenseText, StringComparison.Ordinal);
        Assert.Equal(ReferenceVoiceLibrary.ConsentNotice, row.ConsentNotice);
    }

    /// <summary>
    ///     Proves that the action flags and status text follow each install state.
    /// </summary>
    [Theory]
    [InlineData(SpeechModelState.NotDownloaded, "Not installed", true, false, false)]
    [InlineData(SpeechModelState.Downloading, "Installing", false, false, true)]
    [InlineData(SpeechModelState.Downloaded, "Installed", false, true, false)]
    [InlineData(SpeechModelState.FailedOrCorrupt, "Failed or corrupt", true, false, false)]
    public void ReferenceVoicePackListItemViewModel_State_DrivesFlagsAndStatusText(
        SpeechModelState state, string text, bool canInstall, bool canRemove, bool isInstalling)
    {
        // Act
        var row = new ReferenceVoicePackListItemViewModel(ReferenceVoicePack.VoiceZero10, state);

        // Assert
        Assert.Equal(text, row.StatusText);
        Assert.Equal(canInstall, row.CanInstall);
        Assert.Equal(canRemove, row.CanRemove);
        Assert.Equal(isInstalling, row.IsInstalling);
    }

    /// <summary>
    ///     Proves that setting a failure message raises <c>HasFailureMessage</c>.
    /// </summary>
    [Fact]
    public void ReferenceVoicePackListItemViewModel_FailureMessage_UpdatesHasFailureMessage()
    {
        // Arrange
        var row = new ReferenceVoicePackListItemViewModel(ReferenceVoicePack.VoiceZero10, SpeechModelState.NotDownloaded);
        Assert.False(row.HasFailureMessage);

        // Act
        row.FailureMessage = "oops";

        // Assert
        Assert.True(row.HasFailureMessage);
    }
}
