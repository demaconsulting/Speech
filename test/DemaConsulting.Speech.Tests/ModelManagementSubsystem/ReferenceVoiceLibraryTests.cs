using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;
using NSubstitute;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for <see cref="ReferenceVoiceLibrary"/> using synthetic archives and a fake download client
///     (no network access).
/// </summary>
public sealed class ReferenceVoiceLibraryTests : IDisposable
{
    /// <summary>A scratch directory unique to this test instance.</summary>
    private readonly string _root = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Tests", Guid.NewGuid().ToString("N"));

    /// <summary>Deletes the scratch directory.</summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup only.
        }
    }

    /// <summary>Creates a store rooted in the scratch directory.</summary>
    private SpeechModelStore NewStore() =>
        new(new SpeechModelStoreOptions { RootPathOverride = Path.Join(_root, "models") });

    /// <summary>Creates a library over a synthetic pack and fake client.</summary>
    private ReferenceVoiceLibrary NewLibrary(
        byte[] zipBytes,
        out FakeClient client,
        SpeechModelDownloaderOptions? options = null,
        byte[]? served = null)
    {
        var pack = ReferenceVoiceTestPacks.PackFor(zipBytes);
        client = new FakeClient(served ?? zipBytes);
        return new ReferenceVoiceLibrary(NewStore(), options, diagnostics: null, client, [pack]);
    }

    /// <summary>
    ///     Proves the documented store layout: the library lives under <c>reference-voices</c> in the store root.
    /// </summary>
    [Fact]
    public void ReferenceVoiceLibrary_Paths_FollowDocumentedLayout()
    {
        // Arrange
        var store = NewStore();

        // Act
        using var library = new ReferenceVoiceLibrary(store);

        // Assert
        Assert.Equal(Path.Join(store.RootPath, "reference-voices"), library.RootPath);
        Assert.Equal(Path.Join(library.RootPath, "user"), library.UserDirectory);
        Assert.Equal(
            Path.Join(library.RootPath, "voice-zero-1.0", "current"),
            library.GetPackDirectory("voice-zero-1.0"));
        Assert.Contains(ReferenceVoicePack.VoiceZero10, ReferenceVoiceLibrary.Packs);
        Assert.False(Directory.Exists(library.RootPath));
    }

    /// <summary>
    ///     Proves that a null store is rejected and unknown pack ids are reported.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_InvalidArguments_Throw()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new ReferenceVoiceLibrary(null!));
        using var library = new ReferenceVoiceLibrary(NewStore());
        Assert.Throws<ArgumentException>(() => library.GetPackState("nope"));
        Assert.Throws<ArgumentException>(() => library.GetPackDirectory("nope"));
        await Assert.ThrowsAsync<ArgumentException>(() => library.UninstallPackAsync("nope", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => library.InstallPackAsync("nope", cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that the user folder is created on request and enumerated as user voices.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_EnsureUserDirectory_CreatesFolderAndListsUserVoices()
    {
        // Arrange
        using var library = new ReferenceVoiceLibrary(NewStore());

        // Act
        var directory = library.EnsureUserDirectory();
        await File.WriteAllTextAsync(Path.Join(directory, "my_voice.WAV"), "x", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Join(directory, "readme.txt"), "x", TestContext.Current.CancellationToken);
        var voices = library.EnumerateVoices();

        // Assert
        var voice = Assert.Single(voices);
        Assert.Equal("my_voice", voice.Id);
        Assert.Equal("My Voice", voice.DisplayName);
        Assert.Equal(ReferenceVoiceSource.User, voice.Source);
        Assert.Null(voice.PackId);
    }

    /// <summary>
    ///     Proves that enumeration with nothing installed and no folders is empty and creates nothing.
    /// </summary>
    [Fact]
    public void ReferenceVoiceLibrary_EnumerateVoices_NothingPresent_ReturnsEmptyWithoutCreatingFolders()
    {
        // Arrange
        using var library = new ReferenceVoiceLibrary(NewStore());

        // Act
        var voices = library.EnumerateVoices([Path.Join(_root, "missing")]);

        // Assert
        Assert.Empty(voices);
        Assert.False(Directory.Exists(library.RootPath));
    }

    /// <summary>
    ///     Proves that installing a pack downloads the archive through the client, extracts the voices, deletes the
    ///     archive and reports state transitions.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_InstallPack_Success_InstallsVoicesAndUpdatesState()
    {
        // Arrange
        using var library = NewLibrary(ReferenceVoiceTestPacks.BuildTypicalZip(), out var client);
        Assert.Equal(SpeechModelState.NotDownloaded, library.GetPackState("voice-zero-1.0"));

        // Act
        var result = await library.InstallPackAsync("voice-zero-1.0", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelDownloadOutcome.Installed, result.Outcome);
        Assert.Equal(SpeechModelState.Downloaded, library.GetPackState("voice-zero-1.0"));
        Assert.Equal(new Uri("https://example.invalid/voice-zero-1.0.zip"), client.Requests.Single());
        var packDirectory = library.GetPackDirectory("voice-zero-1.0");
        Assert.True(File.Exists(Path.Join(packDirectory, "pack.json")));
        Assert.False(File.Exists(Path.Join(packDirectory, "voice-zero-1.0.zip")));
        var voices = library.EnumerateVoices();
        Assert.Equal(["alan_davis_drake2", "alana_jordan"], voices.Select(voice => voice.Id).ToArray());
        Assert.All(voices, voice =>
        {
            Assert.Equal(ReferenceVoiceSource.Pack, voice.Source);
            Assert.Equal("voice-zero-1.0", voice.PackId);
            Assert.Equal("Test attribution", voice.Attribution);
            Assert.Equal("CC0 1.0 Universal", voice.LicenseName);
        });
        Assert.Equal("Alan Davis Drake 2", voices[0].DisplayName);
    }

    /// <summary>
    ///     Proves that a configured mirror is used for the archive URI.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_InstallPack_WithMirror_RequestsMirrorUri()
    {
        // Arrange
        var options = new SpeechModelDownloaderOptions { Mirror = new DownloadMirror(new Uri("https://mirror.invalid/models")) };
        using var library = NewLibrary(ReferenceVoiceTestPacks.BuildTypicalZip(), out var client, options);

        // Act
        var result = await library.InstallPackAsync("voice-zero-1.0", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelDownloadOutcome.Installed, result.Outcome);
        Assert.Equal(
            "https://mirror.invalid/models/voice-zero-1.0/voice-zero-1.0.zip",
            client.Requests.Single().ToString());
    }

    /// <summary>
    ///     Proves that a default install extracts the emotion clips, attaches them to their speaker as
    ///     <c>MoodClips</c> (lower-case mood, sorted), adds no extra voice ids, and records the variant.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_InstallPack_Default_AttachesMoodClipsWithoutExtraVoices()
    {
        // Arrange
        using var library = NewLibrary(ReferenceVoiceTestPacks.BuildTypicalZip(), out _);

        // Act
        await library.InstallPackAsync("voice-zero-1.0", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var voices = library.EnumerateVoices();
        Assert.Equal(["alan_davis_drake2", "alana_jordan"], voices.Select(voice => voice.Id).ToArray());
        Assert.True((await library.PackHasEmotionsAsync("voice-zero-1.0", TestContext.Current.CancellationToken)));
        Assert.Equal(["voices", "voices-emotion"], (await library.GetPackInstalledVariantsAsync("voice-zero-1.0", TestContext.Current.CancellationToken)).ToArray());
        var alana = voices.Single(voice => voice.Id == "alana_jordan");
        Assert.Null(alana.Mood);
        Assert.NotNull(alana.MoodClips);
        Assert.Equal(["happy", "sad"], alana.MoodClips.Keys.ToArray());
        Assert.All(alana.MoodClips.Values, path => Assert.True(File.Exists(path)));
        Assert.Equal(["anger"], voices.Single(voice => voice.Id == "alan_davis_drake2").MoodClips!.Keys.ToArray());
    }

    /// <summary>
    ///     Proves that opting out leaves no emotion clips, no <c>MoodClips</c> and no emotion variant.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_InstallPack_NoEmotions_LeavesVoicesOnly()
    {
        // Arrange
        using var library = NewLibrary(ReferenceVoiceTestPacks.BuildTypicalZip(), out _);

        // Act
        await library.InstallPackAsync("voice-zero-1.0", includeEmotions: false, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False((await library.PackHasEmotionsAsync("voice-zero-1.0", TestContext.Current.CancellationToken)));
        Assert.Equal(["voices"], (await library.GetPackInstalledVariantsAsync("voice-zero-1.0", TestContext.Current.CancellationToken)).ToArray());
        Assert.False(Directory.Exists(Path.Join(library.GetPackDirectory("voice-zero-1.0"), "voices-emotion")));
        Assert.All(library.EnumerateVoices(), voice => Assert.Null(voice.MoodClips));
    }

    /// <summary>
    ///     Proves that installing with emotions over a voices-only install upgrades it (the old install is moved aside, then replaced) and that a
    ///     later request for the base voices only never downgrades or downloads again.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_InstallPack_UpgradeFromVoicesOnly_AddsEmotions()
    {
        // Arrange
        using var library = NewLibrary(ReferenceVoiceTestPacks.BuildTypicalZip(), out var client);
        await library.InstallPackAsync("voice-zero-1.0", includeEmotions: false, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var upgrade = await library.InstallPackAsync("voice-zero-1.0", cancellationToken: TestContext.Current.CancellationToken);
        var again = await library.InstallPackAsync("voice-zero-1.0", includeEmotions: false, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelDownloadOutcome.Installed, upgrade.Outcome);
        Assert.Equal(SpeechModelDownloadOutcome.Installed, again.Outcome);
        Assert.Equal(2, client.Requests.Count);
        Assert.True((await library.PackHasEmotionsAsync("voice-zero-1.0", TestContext.Current.CancellationToken)));
        Assert.NotNull(library.EnumerateVoices().Single(voice => voice.Id == "alana_jordan").MoodClips);
        Assert.Equal(SpeechModelState.Downloaded, library.GetPackState("voice-zero-1.0"));
    }

    /// <summary>
    ///     Proves that a failed upgrade (checksum mismatch) keeps the previous voices-only install and leaves no backup.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_InstallPack_FailedUpgrade_KeepsPreviousInstall()
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildTypicalZip();
        using var library = NewLibrary(zip, out var client);
        await library.InstallPackAsync("voice-zero-1.0", includeEmotions: false, cancellationToken: TestContext.Current.CancellationToken);
        client.Payload = [1, 2, 3];

        // Act
        var upgrade = await library.InstallPackAsync("voice-zero-1.0", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(SpeechModelDownloadOutcome.Installed, upgrade.Outcome);
        Assert.Equal(SpeechModelState.Downloaded, library.GetPackState("voice-zero-1.0"));
        Assert.False((await library.PackHasEmotionsAsync("voice-zero-1.0", TestContext.Current.CancellationToken)));
        Assert.Equal(2, library.EnumerateVoices().Count);
        Assert.Empty(Directory.EnumerateDirectories(Path.GetDirectoryName(library.GetPackDirectory("voice-zero-1.0"))!, "current.upgrade-backup-*"));
    }

    /// <summary>
    ///     Proves that a canceled upgrade keeps the previous voices-only install.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_InstallPack_CanceledUpgrade_KeepsPreviousInstall()
    {
        // Arrange
        using var library = NewLibrary(ReferenceVoiceTestPacks.BuildTypicalZip(), out var client);
        await library.InstallPackAsync("voice-zero-1.0", includeEmotions: false, cancellationToken: TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        client.OnDownload = (_, _) =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        };

        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => library.InstallPackAsync("voice-zero-1.0", cancellationToken: cts.Token));
        Assert.Equal(SpeechModelState.Downloaded, library.GetPackState("voice-zero-1.0"));
        Assert.Equal(2, library.EnumerateVoices().Count);
    }

    /// <summary>
    ///     Proves that a voices-only install in flight does not let a concurrent emotions install skip the upgrade:
    ///     the second call waits, then upgrades, and the in-progress state lasts until both finish.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_InstallPack_ConcurrentCalls_AreSerialized()
    {
        // Arrange
        using var library = NewLibrary(ReferenceVoiceTestPacks.BuildTypicalZip(), out var client);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.OnDownload = async (call, _) =>
        {
            if (call == 1)
            {
                entered.SetResult();
                await release.Task;
            }
        };

        // Act
        var first = library.InstallPackAsync("voice-zero-1.0", includeEmotions: false, cancellationToken: TestContext.Current.CancellationToken);
        await entered.Task;
        var second = library.InstallPackAsync("voice-zero-1.0", cancellationToken: TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(second.IsCompleted);
        release.SetResult();
        var results = await Task.WhenAll(first, second);

        // Assert
        Assert.All(results, result => Assert.Equal(SpeechModelDownloadOutcome.Installed, result.Outcome));
        Assert.True((await library.PackHasEmotionsAsync("voice-zero-1.0", TestContext.Current.CancellationToken)));
        Assert.Equal(2, client.Requests.Count);
        Assert.Equal(SpeechModelState.Downloaded, library.GetPackState("voice-zero-1.0"));
    }

    /// <summary>
    ///     Proves that the source-compatible overload installs with emotions.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_InstallPack_ProgressOverload_IncludesEmotions()
    {
        // Arrange
        using var library = NewLibrary(ReferenceVoiceTestPacks.BuildTypicalZip(), out _);

        // Act
        var result = await library.InstallPackAsync("voice-zero-1.0", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelDownloadOutcome.Installed, result.Outcome);
        Assert.True((await library.PackHasEmotionsAsync("voice-zero-1.0", TestContext.Current.CancellationToken)));
    }

    /// <summary>
    ///     Proves that the variant queries never throw: nothing installed, unknown pack, or unreadable metadata.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_PackVariants_MissingOrCorruptMetadata_AreEmptyAndNeverThrow()
    {
        // Arrange
        using var library = NewLibrary(ReferenceVoiceTestPacks.BuildTypicalZip(), out _);
        Assert.False((await library.PackHasEmotionsAsync("voice-zero-1.0", TestContext.Current.CancellationToken)));
        await Assert.ThrowsAsync<ArgumentException>(() => library.PackHasEmotionsAsync("nope", TestContext.Current.CancellationToken));
        await library.InstallPackAsync("voice-zero-1.0", cancellationToken: TestContext.Current.CancellationToken);
        var metadata = Path.Join(library.GetPackDirectory("voice-zero-1.0"), "pack.json");

        // Act & Assert
        await File.WriteAllTextAsync(metadata, "{ not json", TestContext.Current.CancellationToken);
        Assert.Empty(await library.GetPackInstalledVariantsAsync("voice-zero-1.0", TestContext.Current.CancellationToken));
        await File.WriteAllTextAsync(metadata, "{\"variants\": 3}", TestContext.Current.CancellationToken);
        Assert.False((await library.PackHasEmotionsAsync("voice-zero-1.0", TestContext.Current.CancellationToken)));
        File.Delete(metadata);
        Assert.False((await library.PackHasEmotionsAsync("voice-zero-1.0", TestContext.Current.CancellationToken)));
    }

    /// <summary>
    ///     Proves that a second install of an installed pack does not download again.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_InstallPack_SecondCall_IsNoOp()
    {
        // Arrange
        using var library = NewLibrary(ReferenceVoiceTestPacks.BuildTypicalZip(), out var client);
        await library.InstallPackAsync("voice-zero-1.0", cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var result = await library.InstallPackAsync("voice-zero-1.0", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelDownloadOutcome.Installed, result.Outcome);
        Assert.Single(client.Requests);
    }

    /// <summary>
    ///     Proves that a checksum mismatch installs nothing and leaves the pack in the failed state.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_InstallPack_ChecksumMismatch_InstallsNothingAndReportsFailedState()
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildTypicalZip();
        var tampered = (byte[])zip.Clone();
        tampered[^1] ^= 0xFF;
        using var library = NewLibrary(zip, out _, served: tampered);

        // Act
        var result = await library.InstallPackAsync("voice-zero-1.0", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelDownloadOutcome.ChecksumMismatch, result.Outcome);
        Assert.Equal(SpeechModelState.FailedOrCorrupt, library.GetPackState("voice-zero-1.0"));
        Assert.Empty(library.EnumerateVoices());
    }

    /// <summary>
    ///     Proves that an archive with no voices fails the install and installs nothing.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_InstallPack_ArchiveWithoutVoices_FailsWithoutInstalling()
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildZip([("voice-zero/LICENSE.md", "x"u8.ToArray())]);
        using var library = NewLibrary(zip, out _);

        // Act
        var result = await library.InstallPackAsync("voice-zero-1.0", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(SpeechModelDownloadOutcome.Installed, result.Outcome);
        Assert.Equal(SpeechModelState.FailedOrCorrupt, library.GetPackState("voice-zero-1.0"));
        Assert.False(Directory.Exists(library.GetPackDirectory("voice-zero-1.0")));
    }

    /// <summary>
    ///     Proves that uninstalling removes the pack's voices but keeps the user folder, and that a repeat is harmless.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_UninstallPackAsync_RemovesPackKeepsUserFolder()
    {
        // Arrange
        using var library = NewLibrary(ReferenceVoiceTestPacks.BuildTypicalZip(), out _);
        await library.InstallPackAsync("voice-zero-1.0", cancellationToken: TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Join(library.EnsureUserDirectory(), "mine.wav"), "x", TestContext.Current.CancellationToken);

        // Act
        await library.UninstallPackAsync("voice-zero-1.0", TestContext.Current.CancellationToken);
        await library.UninstallPackAsync("voice-zero-1.0", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelState.NotDownloaded, library.GetPackState("voice-zero-1.0"));
        Assert.Equal(["mine"], library.EnumerateVoices().Select(voice => voice.Id).ToArray());
    }

    /// <summary>
    ///     Proves that uninstalling while an install of the same pack is in flight waits for the install to finish
    ///     (serialized on the same per-pack lock) and then removes the pack, instead of throwing.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_UninstallPackAsync_WhileInstalling_WaitsThenRemoves()
    {
        // Arrange
        using var library = NewLibrary(ReferenceVoiceTestPacks.BuildTypicalZip(), out var client);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.OnDownload = async (_, _) =>
        {
            entered.SetResult();
            await release.Task;
        };
        var install = library.InstallPackAsync("voice-zero-1.0", cancellationToken: TestContext.Current.CancellationToken);
        await entered.Task;

        // Act
        var uninstall = library.UninstallPackAsync("voice-zero-1.0", TestContext.Current.CancellationToken);
        var stateWhileWaiting = library.GetPackState("voice-zero-1.0");
        var completedWhileInstalling = uninstall.IsCompleted;
        release.SetResult();
        var result = await install;
        await uninstall;

        // Assert
        Assert.False(completedWhileInstalling);
        Assert.Equal(SpeechModelState.Downloading, stateWhileWaiting);
        Assert.Equal(SpeechModelDownloadOutcome.Installed, result.Outcome);
        Assert.Equal(SpeechModelState.NotDownloaded, library.GetPackState("voice-zero-1.0"));
    }

    /// <summary>
    ///     Proves that canceling an uninstall that is waiting for an in-flight install cancels only the uninstall: the
    ///     install still completes and the pack stays installed.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_UninstallPackAsync_CanceledWhileWaiting_LeavesInstallIntact()
    {
        // Arrange
        using var library = NewLibrary(ReferenceVoiceTestPacks.BuildTypicalZip(), out var client);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.OnDownload = async (_, _) =>
        {
            entered.SetResult();
            await release.Task;
        };
        var install = library.InstallPackAsync("voice-zero-1.0", cancellationToken: TestContext.Current.CancellationToken);
        await entered.Task;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var uninstall = library.UninstallPackAsync("voice-zero-1.0", cts.Token);

        // Act
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => uninstall);
        release.SetResult();
        var result = await install;

        // Assert
        Assert.Equal(SpeechModelDownloadOutcome.Installed, result.Outcome);
        Assert.Equal(SpeechModelState.Downloaded, library.GetPackState("voice-zero-1.0"));
    }

    /// <summary>
    ///     Proves that an uninstall with an already-canceled token does not remove an installed pack, and that the
    ///     per-pack lock is released so a later uninstall succeeds.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_UninstallPackAsync_AlreadyCanceled_DoesNothingAndReleasesLock()
    {
        // Arrange
        using var library = NewLibrary(ReferenceVoiceTestPacks.BuildTypicalZip(), out _);
        await library.InstallPackAsync("voice-zero-1.0", cancellationToken: TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => library.UninstallPackAsync("voice-zero-1.0", cts.Token));
        var stateAfterCancel = library.GetPackState("voice-zero-1.0");
        await library.UninstallPackAsync("voice-zero-1.0", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelState.Downloaded, stateAfterCancel);
        Assert.Equal(SpeechModelState.NotDownloaded, library.GetPackState("voice-zero-1.0"));
    }

    /// <summary>
    ///     Proves that uninstalling discards an upgrade backup folder left by an interrupted upgrade so it cannot
    ///     resurrect the pack.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_UninstallPackAsync_DiscardsUpgradeBackup()
    {
        // Arrange
        using var library = NewLibrary(ReferenceVoiceTestPacks.BuildTypicalZip(), out _);
        await library.InstallPackAsync("voice-zero-1.0", cancellationToken: TestContext.Current.CancellationToken);
        var modelDirectory = new SpeechModelStore(new SpeechModelStoreOptions { RootPathOverride = library.RootPath }).GetModelDirectory("voice-zero-1.0");
        var backup = Path.Join(modelDirectory, "current.upgrade-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backup);

        // Act
        await library.UninstallPackAsync("voice-zero-1.0", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Directory.Exists(backup));
    }

    /// <summary>
    ///     Proves that an install restores a backup left behind by an interrupted upgrade (the installed folder
    ///     missing, a backup present) instead of downloading again.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_InstallPack_AbandonedBackup_IsRestored()
    {
        // Arrange
        using var library = NewLibrary(ReferenceVoiceTestPacks.BuildTypicalZip(), out var client);
        await library.InstallPackAsync("voice-zero-1.0", cancellationToken: TestContext.Current.CancellationToken);
        var store = new SpeechModelStore(new SpeechModelStoreOptions { RootPathOverride = library.RootPath });
        var current = store.GetCurrentDirectory("voice-zero-1.0");
        var backup = Path.Join(store.GetModelDirectory("voice-zero-1.0"), "current.upgrade-backup-" + Guid.NewGuid().ToString("N"));
        Directory.Move(current, backup);

        // Act
        var result = await library.InstallPackAsync("voice-zero-1.0", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelDownloadOutcome.Installed, result.Outcome);
        Assert.True(Directory.Exists(current));
        Assert.False(Directory.Exists(backup));
        Assert.Single(client.Requests);
    }

    /// <summary>
    ///     Proves that disposing the library more than once is harmless.
    /// </summary>
    [Fact]
    public void ReferenceVoiceLibrary_Dispose_Twice_DoesNotThrow()
    {
        // Arrange
        var library = NewLibrary(ReferenceVoiceTestPacks.BuildTypicalZip(), out _);

        // Act
        library.Dispose();
        var second = Record.Exception(library.Dispose);

        // Assert
        Assert.Null(second);
    }

    /// <summary>
    ///     Proves the merge order (extra directory, then user, then pack) and that the first id wins.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_EnumerateVoices_MergesInPrecedenceOrderFirstIdWins()
    {
        // Arrange
        using var library = NewLibrary(ReferenceVoiceTestPacks.BuildTypicalZip(), out _);
        await library.InstallPackAsync("voice-zero-1.0", cancellationToken: TestContext.Current.CancellationToken);
        var extra = Path.Join(_root, "extra");
        Directory.CreateDirectory(extra);
        await File.WriteAllTextAsync(Path.Join(extra, "alana_jordan.wav"), "extra", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Join(extra, "zed.flac"), "extra", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Join(library.EnsureUserDirectory(), "alan_davis_drake2.wav"), "user", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Join(library.UserDirectory, "mine.wav"), "user", TestContext.Current.CancellationToken);

        // Act
        var voices = library.EnumerateVoices([extra]);

        // Assert
        Assert.Equal(
            [
                ("alana_jordan", ReferenceVoiceSource.Directory),
                ("zed", ReferenceVoiceSource.Directory),
                ("alan_davis_drake2", ReferenceVoiceSource.User),
                ("mine", ReferenceVoiceSource.User),
            ],
            voices.Select(voice => (voice.Id, voice.Source)).ToArray());
    }

    /// <summary>
    ///     Proves that a voice hidden by an earlier voice with the same id is reported at Info level, naming the id.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_EnumerateVoices_DuplicateId_ReportsInfoDiagnostic()
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildTypicalZip();
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        using var library = new ReferenceVoiceLibrary(NewStore(), null, diagnostics, new FakeClient(zip), [ReferenceVoiceTestPacks.PackFor(zip)]);
        var user = library.EnsureUserDirectory();
        await File.WriteAllTextAsync(Path.Join(user, "mine.wav"), "a", TestContext.Current.CancellationToken);
        var extra = Path.Join(_root, "extra");
        Directory.CreateDirectory(extra);
        await File.WriteAllTextAsync(Path.Join(extra, "mine.flac"), "b", TestContext.Current.CancellationToken);

        // Act
        var voices = library.EnumerateVoices([extra]);

        // Assert
        Assert.Equal(ReferenceVoiceSource.Directory, Assert.Single(voices).Source);
        diagnostics.Received(1).Report(
            SpeechDiagnosticLevel.Info,
            "ModelManagementSubsystem",
            Arg.Is<string>(message => message.Contains("'mine'") && message.Contains("hidden by an earlier voice")));
    }

    /// <summary>
    ///     Proves that the consent notice is non-blank and tells hosts to obtain permission before cloning a voice.
    /// </summary>
    [Fact]
    public void ReferenceVoiceLibrary_ConsentNotice_MentionsPermissionToClone() =>
        Assert.Contains("permission", ReferenceVoiceLibrary.ConsentNotice, StringComparison.Ordinal);

    /// <summary>
    ///     Proves that voice-name humanizing handles separators, case and trailing numbers.
    /// </summary>
    [Theory]
    [InlineData("alan_davis_drake2", "Alan Davis Drake 2")]
    [InlineData("MY-voice", "My Voice")]
    [InlineData("_", "_")]
    [InlineData("42", "42")]
    public void ReferenceVoiceLibrary_Humanize_FormatsName(string id, string expected) =>
        Assert.Equal(expected, ReferenceVoiceLibrary.Humanize(id));

    /// <summary>
    ///     Proves that using a disposed library for installing throws.
    /// </summary>
    [Fact]
    public async Task ReferenceVoiceLibrary_InstallPack_AfterDispose_Throws()
    {
        // Arrange
        var library = NewLibrary(ReferenceVoiceTestPacks.BuildTypicalZip(), out _);
        library.Dispose();

        // Act / Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => library.InstallPackAsync("voice-zero-1.0", cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>A download client that serves fixed bytes and records requested URIs.</summary>
    internal sealed class FakeClient(byte[] payload) : IModelDownloadClient
    {
        /// <summary>Gets or sets the bytes served by the next download.</summary>
        public byte[] Payload { get; set; } = payload;

        /// <summary>Gets or sets an optional hook awaited at the start of each download (it may block or throw).</summary>
        public Func<int, CancellationToken, Task>? OnDownload { get; set; }

        /// <summary>Gets the URIs requested so far.</summary>
        public List<Uri> Requests { get; } = [];

        /// <inheritdoc/>
        public async Task DownloadAsync(
            Uri sourceUri,
            Stream destination,
            IProgress<SpeechModelDownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            Requests.Add(sourceUri);
            if (OnDownload is not null)
            {
                await OnDownload(Requests.Count, cancellationToken);
            }

            await destination.WriteAsync(Payload, cancellationToken);
        }
    }
}
