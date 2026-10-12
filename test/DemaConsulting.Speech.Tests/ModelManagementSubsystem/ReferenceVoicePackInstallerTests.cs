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
        catch (IOException)
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
    ///     Proves that unsafe emotion entry names fail the install when emotions are requested, like any other entry.
    /// </summary>
    [Theory]
    [InlineData("voice-zero/voices-emotion/../../evil.flac")]
    [InlineData("voice-zero/voices-emotion/alana_jordan/..\\evil.flac")]
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
    ///     Proves that <c>pack.json</c> records the pack's identity, license, attribution and voice count.
    /// </summary>
    [Fact]
    public async Task ReferenceVoicePackInstaller_Install_TypicalArchive_WritesPackMetadata()
    {
        // Act
        var staging = await InstallAsync(ReferenceVoiceTestPacks.BuildTypicalZip());

        // Assert
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Join(staging, "pack.json"), TestContext.Current.CancellationToken));
        var json = document.RootElement;
        Assert.Equal("voice-zero-1.0", json.GetProperty("id").GetString());
        Assert.Equal("CC0 1.0 Universal", json.GetProperty("license").GetString());
        Assert.Equal("Test attribution", json.GetProperty("attribution").GetString());
        Assert.Equal(2, json.GetProperty("voiceCount").GetInt32());
        Assert.Equal("voices", json.GetProperty("variants")[0].GetString());
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
    public async Task ReferenceVoicePackInstaller_Install_UnsafeEntryName_ThrowsAndWritesNothingOutside(string entryName)
    {
        // Arrange
        var zip = ReferenceVoiceTestPacks.BuildZip(
        [
            ("voice-zero/voices/good.flac", "good"u8.ToArray()),
            (entryName, "evil"u8.ToArray()),
        ]);

        // Act / Assert
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

        // Act / Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => InstallAsync(zip));
        Assert.Contains("no", exception.Message, StringComparison.OrdinalIgnoreCase);
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

        // Act / Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => InstallAsync(bytes));
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

        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ReferenceVoicePackInstaller.InstallAsync(pack, includeEmotions: false, staging, source.Token));
    }
}
