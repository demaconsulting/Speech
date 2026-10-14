using System.Text.Json;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for the internal <c>ReferenceVoicePackInstaller</c>, using small synthetic zip archives.
/// </summary>
public sealed class ReferenceVoicePackInstallerTests : IDisposable
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup only.
        }
    }

    /// <summary>Stages an archive and runs the installer over it.</summary>
    private async Task<string> InstallAsync(byte[] zipBytes, bool includeEmotions = false)
    {
        var pack = ReferenceVoiceTestPacks.PackFor(zipBytes);
        var staging = Path.Join(_root, "staging");
        ReferenceVoiceTestPacks.WriteArchive(staging, pack, zipBytes);
        await ReferenceVoicePackInstaller.InstallAsync(pack, includeEmotions, staging, TestContext.Current.CancellationToken);
        return staging;
    }

    /// <summary>
    ///     Proves that only voice clips and the license/readme files are extracted, with the root folder stripped,
    ///     and that the archive is deleted.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_TypicalArchive_ExtractsOnlyAllowListedEntries()
    {
        // Act
        var staging = await InstallAsync(ReferenceVoiceTestPacks.BuildTypicalZip());

        // Assert
        var files = Directory.GetFiles(staging, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(staging, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            [
                "LICENSE.md",
                "README.md",
                "pack.json",
                "voices/README.md",
                "voices/alan_davis_drake2.flac",
                "voices/alana_jordan.flac",
            ],
            files);
        Assert.Equal("flac-a", await File.ReadAllTextAsync(Path.Join(staging, "voices", "alan_davis_drake2.flac"), TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that, with emotions requested, <c>voices-emotion/{speaker}/{mood}.flac</c> and its readme are extracted
    ///     beside the base voices and <c>pack.json</c> records the variant and clip count.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_WithEmotions_ExtractsMoodClipsAndRecordsVariant()
    {
        // Act
        var staging = await InstallAsync(ReferenceVoiceTestPacks.BuildTypicalZip(), includeEmotions: true);

        // Assert
        var files = Directory.GetFiles(staging, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(staging, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Contains("voices-emotion/README.md", files);
        Assert.Contains("voices-emotion/alana_jordan/happy.flac", files);
        Assert.Contains("voices-emotion/alana_jordan/Sad.flac", files);
        Assert.Contains("voices-emotion/alan_davis_drake2/anger.flac", files);
        Assert.Equal(3, files.Count(file => file.StartsWith("voices-emotion/", StringComparison.Ordinal) && file.EndsWith(".flac", StringComparison.Ordinal)));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Join(staging, "pack.json"), TestContext.Current.CancellationToken));
        var json = document.RootElement;
        Assert.Equal(2, json.GetProperty("voiceCount").GetInt32());
        Assert.Equal(3, json.GetProperty("emotionClipCount").GetInt32());
        Assert.Equal(["voices", "voices-emotion"], json.GetProperty("variants").EnumerateArray().Select(item => item.GetString()).ToArray());
    }

    /// <summary>
    ///     Proves that without emotions nothing under <c>voices-emotion</c> is extracted and the metadata says so.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_WithoutEmotions_SkipsMoodClips()
    {
        // Act
        var staging = await InstallAsync(ReferenceVoiceTestPacks.BuildTypicalZip(), includeEmotions: false);

        // Assert
        Assert.False(Directory.Exists(Path.Join(staging, "voices-emotion")));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Join(staging, "pack.json"), TestContext.Current.CancellationToken));
        Assert.Equal(0, document.RootElement.GetProperty("emotionClipCount").GetInt32());
        Assert.Equal(["voices"], document.RootElement.GetProperty("variants").EnumerateArray().Select(item => item.GetString()).ToArray());
    }

    /// <summary>
    ///     Proves that emotion entries outside the exact <c>voices-emotion/{speaker}/{mood}.flac</c> shape (too deep, loose,
    ///     or not FLAC) are ignored even when emotions are requested.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_WithEmotions_IgnoresMisshapenEmotionEntries()
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildZip(
        [
            ($"{ReferenceVoiceTestPacks.Root}/voices/alana_jordan.flac", "flac"u8.ToArray()),
            ($"{ReferenceVoiceTestPacks.Root}/voices-emotion/happy.flac", "loose"u8.ToArray()),
            ($"{ReferenceVoiceTestPacks.Root}/voices-emotion/alana_jordan/deep/happy.flac", "deep"u8.ToArray()),
            ($"{ReferenceVoiceTestPacks.Root}/voices-emotion/alana_jordan/notes.txt", "text"u8.ToArray()),
            ($"{ReferenceVoiceTestPacks.Root}/voices-emotion/alana_jordan/.flac", "empty"u8.ToArray()),
            ($"{ReferenceVoiceTestPacks.Root}/voices-emotion/alana_jordan/calm.flac", "ok"u8.ToArray()),
        ]);

        // Act
        var staging = await InstallAsync(zip, includeEmotions: true);

        // Assert
        var emotionFiles = Directory.GetFiles(Path.Join(staging, "voices-emotion"), "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(staging, file).Replace('\\', '/'))
            .ToArray();
        Assert.Equal(["voices-emotion/alana_jordan/calm.flac"], emotionFiles);
    }

    /// <summary>
    ///     Proves that unsafe or non-portable emotion entry names (traversal, backslash, colon, trailing dot or space, control
    ///     character) fail the install when emotions are requested, like any other entry.
    /// </summary>
    [Theory]
    [InlineData("voice-zero/voices-emotion/../../evil.flac")]
    [InlineData("voice-zero/voices-emotion/alana_jordan/..\\evil.flac")]
    [InlineData("voice-zero/voices-emotion/alana:jordan/happy.flac")]
    [InlineData("voice-zero/voices-emotion/alana./happy.flac")]
    [InlineData("voice-zero/voices-emotion/alana /happy.flac")]
    [InlineData("voice-zero/voices-emotion/ala\u0001na/happy.flac")]
    public async Task ReferenceVoicePackInstaller_Install_WithEmotions_UnsafeEmotionEntry_Throws(string entryName)
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildZip(
        [
            ("voice-zero/voices/a.flac", "flac"u8.ToArray()),
            (entryName, "evil"u8.ToArray()),
        ]);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => InstallAsync(zip, includeEmotions: true));
    }

    /// <summary>
    ///     Proves that entries with names invalid on Windows (wildcards, quotes, reserved device names, trailing dot)
    ///     fail with <see cref="InvalidDataException"/> rather than a platform-specific exception.
    /// </summary>
    [Theory]
    [InlineData("voice-zero/voices/bad*name.flac")]
    [InlineData("voice-zero/voices/what?.flac")]
    [InlineData("voice-zero/voices/a<b>.flac")]
    [InlineData("voice-zero/voices/pipe|d.flac")]
    [InlineData("voice-zero/voices/quo\"te.flac")]
    [InlineData("voice-zero/voices/CON.flac")]
    [InlineData("voice-zero/voices/nul.flac")]
    [InlineData("voice-zero/voices/lpt1.flac")]
    public async Task ReferenceVoicePackInstaller_Install_PlatformInvalidEntryName_ThrowsInvalidData(string entryName)
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildZip(
        [
            ("voice-zero/voices/a.flac", "flac"u8.ToArray()),
            (entryName, "evil"u8.ToArray()),
        ]);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => InstallAsync(zip));
    }

    /// <summary>
    ///     Proves that emotion clips alone never satisfy the "at least one voice" requirement.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_OnlyEmotionClips_Throws()
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildZip([($"{ReferenceVoiceTestPacks.Root}/voices-emotion/alana_jordan/happy.flac", "x"u8.ToArray())]);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => InstallAsync(zip, includeEmotions: true));
    }

    /// <summary>
    ///     Proves that <c>pack.json</c> records every pack metadata field: identity, license, attribution, source, archive and counts.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_TypicalArchive_WritesPackMetadata()
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildTypicalZip();

        // Act
        var staging = await InstallAsync(zip);

        // Assert
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Join(staging, "pack.json"), TestContext.Current.CancellationToken));
        var json = document.RootElement;
        Assert.Equal("voice-zero-1.0", json.GetProperty("id").GetString());
        Assert.Equal("Test pack", json.GetProperty("name").GetString());
        Assert.Equal("CC0 1.0 Universal", json.GetProperty("license").GetString());
        Assert.Equal("https://creativecommons.org/publicdomain/zero/1.0/", json.GetProperty("licenseUrl").GetString());
        Assert.Equal("Test attribution", json.GetProperty("attribution").GetString());
        Assert.Equal("https://example.invalid/project", json.GetProperty("sourceUrl").GetString());
        Assert.Equal("https://example.invalid/voice-zero-1.0.zip", json.GetProperty("archiveUrl").GetString());
        Assert.Equal(ReferenceVoiceTestPacks.PackFor(zip).Sha256Checksum, json.GetProperty("archiveSha256").GetString());
        Assert.Equal(2, json.GetProperty("voiceCount").GetInt32());
        Assert.Equal(0, json.GetProperty("emotionClipCount").GetInt32());
        Assert.Equal(["voices"], json.GetProperty("variants").EnumerateArray().Select(item => item.GetString()).ToArray());
    }

    /// <summary>
    ///     Proves that the archive file itself is removed after extraction.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_TypicalArchive_DeletesArchive()
    {
        // Act
        var staging = await InstallAsync(ReferenceVoiceTestPacks.BuildTypicalZip());

        // Assert
        Assert.False(File.Exists(Path.Join(staging, "voice-zero-1.0.zip")));
    }

    /// <summary>
    ///     Proves that entries with path-traversal, backslash, drive or rooted names fail the install and nothing
    ///     is written outside the staging directory.
    /// </summary>
    [Theory]
    [InlineData("voice-zero/voices/../../evil.flac")]
    [InlineData("voice-zero/voices/..\\..\\evil.flac")]
    [InlineData("/voice-zero/voices/evil.flac")]
    [InlineData("C:/evil.flac")]
    [InlineData("voice-zero/../evil.flac")]
    [InlineData("voice-zero/./evil.flac")]
    [InlineData("voice-zero/voices/./evil.flac")]
    [InlineData("voice-zero/voices/ev\0il.flac")]
    public async Task ReferenceVoicePackInstaller_Install_UnsafeEntryName_ThrowsAndWritesNothingOutside(string entryName)
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildZip(
        [
            ("voice-zero/voices/good.flac", "good"u8.ToArray()),
            (entryName, "evil"u8.ToArray()),
        ]);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => InstallAsync(zip));
        Assert.False(File.Exists(Path.Join(_root, "evil.flac")));
        Assert.False(File.Exists(Path.Join(_root, "staging", "evil.flac")));
    }

    /// <summary>
    ///     Proves that an archive with no voice clips fails with a clear message.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_NoVoices_Throws()
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildZip(
        [
            ("voice-zero/LICENSE.md", "license"u8.ToArray()),
            ("voice-zero/voices/notes.txt", "x"u8.ToArray()),
        ]);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => InstallAsync(zip));
        Assert.Contains("voice-zero/voices/*.flac", exception.Message, StringComparison.Ordinal);
        Assert.Contains("voice clips", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that an archive containing the same destination twice (here differing only in case) is rejected.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_DuplicateEntry_Throws()
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildZip(
        [
            ("voice-zero/voices/a.flac", "first"u8.ToArray()),
            ("voice-zero/voices/A.flac", "second"u8.ToArray()),
        ]);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => InstallAsync(zip));

        // Assert
        Assert.Contains("more than once", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that an entry larger than the per-entry cap is rejected using the bytes actually read.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_EntryExceedsEntryCap_Throws()
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildZip(
        [
            ("voice-zero/voices/a.flac", new byte[100]),
            ("voice-zero/voices/b.flac", new byte[101]),
        ]);
        var pack = ReferenceVoiceTestPacks.PackFor(zip);
        var staging = Path.Join(_root, "staging");
        ReferenceVoiceTestPacks.WriteArchive(staging, pack, zip);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => ReferenceVoicePackInstaller.InstallAsync(
            pack, false, staging, maximumEntryBytes: 100, maximumTotalBytes: 10_000, TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("b.flac", exception.Message, StringComparison.Ordinal);
        Assert.Contains("allowed extraction size", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that entries each within the per-entry cap are rejected once their running total passes the total cap.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_TotalExceedsTotalCap_Throws()
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildZip(
        [
            ("voice-zero/voices/a.flac", new byte[60]),
            ("voice-zero/voices/b.flac", new byte[60]),
        ]);
        var pack = ReferenceVoiceTestPacks.PackFor(zip);
        var staging = Path.Join(_root, "staging");
        ReferenceVoiceTestPacks.WriteArchive(staging, pack, zip);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => ReferenceVoicePackInstaller.InstallAsync(
            pack, false, staging, maximumEntryBytes: 100, maximumTotalBytes: 100, TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("b.flac", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that content exactly at both caps is accepted (the caps are inclusive) and the defaults are unchanged.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_AtCaps_Succeeds()
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildZip(
        [
            ("voice-zero/voices/a.flac", new byte[50]),
            ("voice-zero/voices/b.flac", new byte[50]),
        ]);
        var pack = ReferenceVoiceTestPacks.PackFor(zip);
        var staging = Path.Join(_root, "staging");
        ReferenceVoiceTestPacks.WriteArchive(staging, pack, zip);

        // Act
        await ReferenceVoicePackInstaller.InstallAsync(
            pack, false, staging, maximumEntryBytes: 50, maximumTotalBytes: 100, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, Directory.GetFiles(Path.Join(staging, "voices"), "*.flac").Length);
        Assert.Equal(128L * 1024 * 1024, ReferenceVoicePackInstaller.MaximumEntryBytes);
        Assert.Equal(2L * 1024 * 1024 * 1024, ReferenceVoicePackInstaller.MaximumTotalBytes);
    }

    /// <summary>
    ///     Proves that entries outside the pack's root folder are ignored.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_EntriesOutsideRoot_AreIgnored()
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildZip(
        [
            ("other/voices/x.flac", "x"u8.ToArray()),
            ("voice-zero/voices/a.flac", "a"u8.ToArray()),
        ]);

        // Act
        var staging = await InstallAsync(zip);

        // Assert
        Assert.Equal(["a.flac"], Directory.GetFiles(Path.Join(staging, "voices")).Select(Path.GetFileName).ToArray());
    }

    /// <summary>
    ///     Proves that a staged file that is not a zip archive fails with <see cref="InvalidDataException"/>.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_NotAZip_Throws()
    {
        // Arrange
        var bytes = "this is not a zip file"u8.ToArray();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => InstallAsync(bytes));
    }

    /// <summary>
    ///     Proves that a staging directory passed with a trailing separator still extracts correctly.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_StagingDirectoryWithTrailingSeparator_Extracts()
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildTypicalZip();
        var pack = ReferenceVoiceTestPacks.PackFor(zip);
        var staging = Path.Join(_root, "staging");
        ReferenceVoiceTestPacks.WriteArchive(staging, pack, zip);

        // Act
        await ReferenceVoicePackInstaller.InstallAsync(pack, includeEmotions: false, staging + Path.DirectorySeparatorChar, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(File.Exists(Path.Join(staging, "voices", "alana_jordan.flac")));
    }

    /// <summary>
    ///     Proves that a canceled token aborts the extraction.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_CanceledToken_Throws()
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildTypicalZip();
        var pack = ReferenceVoiceTestPacks.PackFor(zip);
        var staging = Path.Join(_root, "staging");
        ReferenceVoiceTestPacks.WriteArchive(staging, pack, zip);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ReferenceVoicePackInstaller.InstallAsync(pack, includeEmotions: false, staging, source.Token));
    }

    /// <summary>
    ///     Proves that a canceled token takes precedence over a missing staged archive, so the caller sees the
    ///     cancellation rather than a file error.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_CanceledTokenAndMissingArchive_ThrowsOperationCanceled()
    {
        // Arrange
        var pack = ReferenceVoiceTestPacks.PackFor(ReferenceVoiceTestPacks.BuildTypicalZip());
        var staging = Path.Join(_root, "staging");
        Directory.CreateDirectory(staging);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // Act
        var act = () => ReferenceVoicePackInstaller.InstallAsync(pack, includeEmotions: false, staging, source.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
    }

    /// <summary>
    ///     Proves that a canceled token takes precedence over the no-voices failure of an archive with no entries.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_CanceledTokenAndEmptyArchive_ThrowsOperationCanceled()
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildZip([]);
        var pack = ReferenceVoiceTestPacks.PackFor(zip);
        var staging = Path.Join(_root, "staging");
        ReferenceVoiceTestPacks.WriteArchive(staging, pack, zip);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // Act
        var act = () => ReferenceVoicePackInstaller.InstallAsync(pack, includeEmotions: false, staging, source.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
    }

    /// <summary>
    ///     Proves that an entry the allow-list would ignore (outside the root folder, not allow-listed, or an
    ///     emotion entry when emotions are not requested) still fails the install when it has a name segment that
    ///     is invalid on some platform.
    /// </summary>
    [Theory]
    [InlineData("other-root/notes/bad*name.txt")]
    [InlineData("voice-zero/docs/CON.txt")]
    [InlineData("voice-zero/docs/trailing.")]
    [InlineData("voice-zero/voices-emotion/spk./happy.flac")]
    public async Task ReferenceVoicePackInstaller_Install_IgnoredEntryWithInvalidSegment_ThrowsInvalidData(string entryName)
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildZip(
        [
            ("voice-zero/voices/a.flac", "flac"u8.ToArray()),
            (entryName, "ignored"u8.ToArray()),
        ]);

        // Act
        var act = () => InstallAsync(zip, includeEmotions: false);

        // Assert
        await Assert.ThrowsAsync<InvalidDataException>(act);
    }

    /// <summary>
    ///     Proves that an entry whose central-directory header understates its uncompressed size can never leave more
    ///     than the per-entry cap on disk: the install either fails or extracts at most the cap.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_ForgedHeaderSize_NeverExtractsMoreThanCap()
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildZip([("voice-zero/voices/a.flac", new byte[5000])]);
        const uint centralDirectorySignature = 0x02014B50;
        for (var offset = 0; offset <= zip.Length - 28; offset++)
        {
            if (BitConverter.ToUInt32(zip, offset) == centralDirectorySignature)
            {
                // Uncompressed size field of the central-directory header
                BitConverter.GetBytes(1u).CopyTo(zip, offset + 24);
            }
        }

        var pack = ReferenceVoiceTestPacks.PackFor(zip);
        var staging = Path.Join(_root, "staging");
        ReferenceVoiceTestPacks.WriteArchive(staging, pack, zip);

        // Act
        var exception = await Record.ExceptionAsync(() => ReferenceVoicePackInstaller.InstallAsync(
            pack, false, staging, maximumEntryBytes: 100, maximumTotalBytes: 10_000, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(exception is null or InvalidDataException);
        Assert.All(
            Directory.GetFiles(staging, "*.flac", SearchOption.AllDirectories),
            file => Assert.True(new FileInfo(file).Length <= 100));
    }

    /// <summary>
    ///     Proves that a failed install leaves the staged archive in place (only a successful install deletes it).
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_NoVoices_KeepsArchive()
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildZip([("voice-zero/LICENSE.md", "license"u8.ToArray())]);
        var pack = ReferenceVoiceTestPacks.PackFor(zip);
        var staging = Path.Join(_root, "staging");
        var archivePath = ReferenceVoiceTestPacks.WriteArchive(staging, pack, zip);

        // Act
        var exception = await Record.ExceptionAsync(() => ReferenceVoicePackInstaller.InstallAsync(pack, false, staging, TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<InvalidDataException>(exception);
        Assert.True(File.Exists(archivePath));
    }

    /// <summary>
    ///     Proves that a failure while writing the pack metadata keeps the staged archive (the archive is deleted only
    ///     after the metadata was written).
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_MetadataWriteFails_KeepsArchive()
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildTypicalZip();
        var pack = ReferenceVoiceTestPacks.PackFor(zip);
        var staging = Path.Join(_root, "staging");
        var archivePath = ReferenceVoiceTestPacks.WriteArchive(staging, pack, zip);
        Directory.CreateDirectory(Path.Join(staging, "pack.json"));

        // Act
        var exception = await Record.ExceptionAsync(() => ReferenceVoicePackInstaller.InstallAsync(pack, false, staging, TestContext.Current.CancellationToken));

        // Assert
        Assert.NotNull(exception);
        Assert.IsNotType<OperationCanceledException>(exception);
        Assert.True(File.Exists(archivePath));
    }

    /// <summary>
    ///     Proves that a canceled install keeps the staged archive.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_CanceledToken_KeepsArchive()
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildTypicalZip();
        var pack = ReferenceVoiceTestPacks.PackFor(zip);
        var staging = Path.Join(_root, "staging");
        var archivePath = ReferenceVoiceTestPacks.WriteArchive(staging, pack, zip);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        var exception = await Record.ExceptionAsync(() => ReferenceVoicePackInstaller.InstallAsync(pack, false, staging, cancellation.Token));

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.True(File.Exists(archivePath));
    }

    /// <summary>
    ///     Proves that a staged archive that is missing surfaces as an <see cref="IOException"/> (not a data error).
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_MissingArchive_ThrowsIOException()
    {
        // Arrange
        var pack = ReferenceVoiceTestPacks.PackFor(ReferenceVoiceTestPacks.BuildTypicalZip());
        var staging = Path.Join(_root, "staging");
        Directory.CreateDirectory(staging);

        // Act & Assert
        await Assert.ThrowsAnyAsync<IOException>(() => ReferenceVoicePackInstaller.InstallAsync(pack, false, staging, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that a null pack or an empty staging directory is rejected up front.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_NullPackOrEmptyStaging_Throws()
    {
        // Arrange
        var pack = ReferenceVoiceTestPacks.PackFor(ReferenceVoiceTestPacks.BuildTypicalZip());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => ReferenceVoicePackInstaller.InstallAsync(null!, false, _root, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => ReferenceVoicePackInstaller.InstallAsync(pack, false, string.Empty, TestContext.Current.CancellationToken));
    }
}
