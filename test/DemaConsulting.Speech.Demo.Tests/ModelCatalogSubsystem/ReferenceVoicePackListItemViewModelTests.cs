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
        Assert.Equal("Download: 570 MiB", row.SizeText);
        Assert.Equal("Installs about 572 MiB (with emotions)", row.InstalledSizeText);
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
    [InlineData(SpeechModelState.Downloaded, "Installed (with emotions)", false, true, false)]
    [InlineData(SpeechModelState.FailedOrCorrupt, "Failed or corrupt", true, false, false)]
    public void ReferenceVoicePackListItemViewModel_State_DrivesFlagsAndStatusText(
        SpeechModelState state, string text, bool canInstall, bool canRemove, bool isInstalling)
    {
        // Act
        var row = new ReferenceVoicePackListItemViewModel(ReferenceVoicePack.VoiceZero10, state, hasEmotions: true);

        // Assert
        Assert.Equal(text, row.StatusText);
        Assert.Equal(canInstall, row.CanInstall);
        Assert.Equal(canRemove, row.CanRemove);
        Assert.Equal(isInstalling, row.IsInstalling);
    }

    /// <summary>
    ///     Proves that the installed-size text follows the emotion option before install and the actual install after.
    /// </summary>
    [Fact]
    public void ReferenceVoicePackListItemViewModel_InstalledSizeText_FollowsOptionAndInstall()
    {
        // Arrange
        var row = new ReferenceVoicePackListItemViewModel(ReferenceVoicePack.VoiceZero10, SpeechModelState.NotDownloaded);
        Assert.True(row.IncludeEmotions);

        // Act & Assert: the option drives the pre-install estimate
        row.IncludeEmotions = false;
        Assert.Equal("Installs about 44 MiB (voices only)", row.InstalledSizeText);
        row.IncludeEmotions = true;
        Assert.Equal("Installs about 572 MiB (with emotions)", row.InstalledSizeText);

        // Act & Assert: once installed, the actual install wins over the option
        row.State = SpeechModelState.Downloaded;
        row.HasEmotions = false;
        row.IncludeEmotions = true;
        Assert.Equal("Uses about 44 MiB (voices only)", row.InstalledSizeText);
        row.HasEmotions = true;
        Assert.Equal("Uses about 572 MiB (with emotions)", row.InstalledSizeText);
    }

    /// <summary>
    ///     Proves that a voices-only install offers "Add emotions" (which always includes them), an install with emotions
    ///     does not, and a not-installed pack shows the option and passes the choice through.
    /// </summary>
    [Fact]
    public void ReferenceVoicePackListItemViewModel_AddEmotions_OnlyForVoicesOnlyInstall()
    {
        // Arrange
        var fresh = new ReferenceVoicePackListItemViewModel(ReferenceVoicePack.VoiceZero10, SpeechModelState.NotDownloaded);
        var voicesOnly = new ReferenceVoicePackListItemViewModel(ReferenceVoicePack.VoiceZero10, SpeechModelState.Downloaded, hasEmotions: false);
        var full = new ReferenceVoicePackListItemViewModel(ReferenceVoicePack.VoiceZero10, SpeechModelState.Downloaded, hasEmotions: true);

        // Assert
        Assert.Equal("Install", fresh.InstallButtonText);
        Assert.True(fresh.ShowEmotionOption);
        Assert.True(fresh.EffectiveIncludeEmotions);
        fresh.IncludeEmotions = false;
        Assert.False(fresh.EffectiveIncludeEmotions);

        Assert.Equal("Add emotions", voicesOnly.InstallButtonText);
        Assert.True(voicesOnly.CanUpgradeToEmotions);
        Assert.True(voicesOnly.CanInstall);
        Assert.False(voicesOnly.ShowEmotionOption);
        voicesOnly.IncludeEmotions = false;
        Assert.True(voicesOnly.EffectiveIncludeEmotions);
        Assert.Equal("Installed (voices only)", voicesOnly.StatusText);

        Assert.False(full.CanInstall);
        Assert.Equal("Install", full.InstallButtonText);
        Assert.Equal("Installed (with emotions)", full.StatusText);
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
