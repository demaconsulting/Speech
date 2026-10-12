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
    ///     Proves that the action flags and status text follow each install state when the
    ///     installed pack lacks its emotion clips, including the "Add emotions" upgrade.
    /// </summary>
    [Theory]
    [InlineData(SpeechModelState.NotDownloaded, "Not installed", true, false, false)]
    [InlineData(SpeechModelState.Downloading, "Installing", false, false, true)]
    [InlineData(SpeechModelState.Downloaded, "Installed (voices only)", true, true, false)]
    [InlineData(SpeechModelState.FailedOrCorrupt, "Failed or corrupt", true, false, false)]
    public void ReferenceVoicePackListItemViewModel_State_VoicesOnly_DrivesFlagsAndStatusText(
        SpeechModelState state, string text, bool canInstall, bool canRemove, bool isInstalling)
    {
        // Act
        var row = new ReferenceVoicePackListItemViewModel(ReferenceVoicePack.VoiceZero10, state, hasEmotions: false);

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

    /// <summary>
    ///     Proves that changing <c>State</c> raises change notifications for every dependent property.
    /// </summary>
    [Fact]
    public void ReferenceVoicePackListItemViewModel_State_Changed_RaisesDependentPropertyChanged()
    {
        // Arrange
        var row = new ReferenceVoicePackListItemViewModel(ReferenceVoicePack.VoiceZero10, SpeechModelState.NotDownloaded);
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // Act
        row.State = SpeechModelState.Downloading;

        // Assert
        foreach (var name in new[]
        {
            nameof(row.State), nameof(row.StatusText), nameof(row.CanInstall), nameof(row.CanRemove),
            nameof(row.IsInstalling), nameof(row.InstalledSizeText), nameof(row.CanUpgradeToEmotions),
            nameof(row.ShowEmotionOption), nameof(row.InstallButtonText),
        })
        {
            Assert.Contains(name, raised);
        }
    }

    /// <summary>
    ///     Proves that changing <c>HasEmotions</c> raises change notifications for its dependent properties.
    /// </summary>
    [Fact]
    public void ReferenceVoicePackListItemViewModel_HasEmotions_Changed_RaisesDependentPropertyChanged()
    {
        // Arrange
        var row = new ReferenceVoicePackListItemViewModel(ReferenceVoicePack.VoiceZero10, SpeechModelState.Downloaded);
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // Act
        row.HasEmotions = true;

        // Assert
        foreach (var name in new[]
        {
            nameof(row.HasEmotions), nameof(row.StatusText), nameof(row.InstalledSizeText), nameof(row.CanInstall),
            nameof(row.CanUpgradeToEmotions), nameof(row.InstallButtonText),
        })
        {
            Assert.Contains(name, raised);
        }
    }

    /// <summary>
    ///     Proves that changing <c>IncludeEmotions</c> raises a change notification for <c>InstalledSizeText</c>.
    /// </summary>
    [Fact]
    public void ReferenceVoicePackListItemViewModel_IncludeEmotions_Changed_RaisesInstalledSizeTextChanged()
    {
        // Arrange
        var row = new ReferenceVoicePackListItemViewModel(ReferenceVoicePack.VoiceZero10, SpeechModelState.NotDownloaded);
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // Act
        row.IncludeEmotions = false;

        // Assert
        Assert.Contains(nameof(row.InstalledSizeText), raised);
        Assert.StartsWith("Installs about", row.InstalledSizeText, StringComparison.Ordinal);
        Assert.EndsWith("(voices only)", row.InstalledSizeText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that changing <c>FailureMessage</c> raises a change notification for <c>HasFailureMessage</c>.
    /// </summary>
    [Fact]
    public void ReferenceVoicePackListItemViewModel_FailureMessage_Changed_RaisesHasFailureMessageChanged()
    {
        // Arrange
        var row = new ReferenceVoicePackListItemViewModel(ReferenceVoicePack.VoiceZero10, SpeechModelState.NotDownloaded);
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // Act
        row.FailureMessage = "oops";

        // Assert
        Assert.Contains(nameof(row.HasFailureMessage), raised);
    }

    /// <summary>
    ///     Proves that a pack without emotion clips or a known installed size offers no emotion option, never
    ///     requests emotions, and shows no installed-size text.
    /// </summary>
    [Fact]
    public void ReferenceVoicePackListItemViewModel_PackWithoutEmotionsOrSize_HidesEmotionOptionAndSize()
    {
        // Arrange
        var pack = new ReferenceVoicePack(
            "plain-pack",
            "Plain pack",
            new Uri("https://example.com/plain.zip"),
            new string('a', 64),
            1024 * 1024,
            "plain.zip",
            "plain",
            "CC0",
            new Uri("https://example.com/license"),
            "Someone",
            new Uri("https://example.com/source"));

        // Act
        var row = new ReferenceVoicePackListItemViewModel(pack, SpeechModelState.NotDownloaded);

        // Assert
        Assert.False(row.HasEmotionVariants);
        Assert.False(row.ShowEmotionOption);
        Assert.False(row.EffectiveIncludeEmotions);
        Assert.False(row.CanUpgradeToEmotions);
        Assert.Equal(string.Empty, row.InstalledSizeText);

        row.State = SpeechModelState.Downloaded;
        Assert.Equal("Installed", row.StatusText);
        Assert.False(row.CanUpgradeToEmotions);
    }

    /// <summary>
    ///     Proves that an undefined state value falls back to its name for the status text.
    /// </summary>
    [Fact]
    public void ReferenceVoicePackListItemViewModel_UnknownState_StatusTextFallsBackToStateName()
    {
        // Act
        var row = new ReferenceVoicePackListItemViewModel(ReferenceVoicePack.VoiceZero10, (SpeechModelState)999);

        // Assert
        Assert.Equal("999", row.StatusText);
    }
}
