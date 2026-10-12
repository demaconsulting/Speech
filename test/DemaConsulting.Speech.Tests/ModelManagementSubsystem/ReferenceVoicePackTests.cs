using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for the <see cref="ReferenceVoicePack"/> descriptor.
/// </summary>
public sealed class ReferenceVoicePackTests
{
    /// <summary>A valid 64-character checksum.</summary>
    private static readonly string ValidChecksum = new('a', 64);

    /// <summary>Builds a pack, overriding selected arguments.</summary>
    private static ReferenceVoicePack Create(
        string id = "pack-1",
        Uri? uri = null,
        string? checksum = null,
        long size = 10,
        string archiveFileName = "pack-1.zip",
        string root = "root") => new(
            id,
            "Pack",
            uri ?? new Uri("https://example.invalid/pack-1.zip"),
            checksum ?? ValidChecksum,
            size,
            archiveFileName,
            root,
            "CC0",
            new Uri("https://example.invalid/license"),
            "Attribution",
            new Uri("https://example.invalid/source"));

    /// <summary>
    ///     Proves that the built-in voice-zero 1.0 metadata is exactly the published, verified values.
    /// </summary>
    [Fact]
    public void ReferenceVoicePack_VoiceZero10_HasPublishedMetadata()
    {
        // Act
        var pack = ReferenceVoicePack.VoiceZero10;

        // Assert
        Assert.Equal("voice-zero-1.0", pack.Id);
        Assert.Equal(
            "https://github.com/OwenTyme/voice-zero/releases/download/1.0/voice-zero-1.0.zip",
            pack.Uri.ToString());
        Assert.Equal(597_269_359L, pack.SizeBytes);
        Assert.Equal("001d5982fc4b602f7c62a764046dea1f53999a493de43955f35686d2c16d7c66", pack.Sha256Checksum);
        Assert.Equal("CC0 1.0 Universal", pack.LicenseName);
        Assert.Equal("voice-zero", pack.ArchiveRootFolder);
        Assert.Equal("voice-zero-1.0.zip", pack.ArchiveFileName);
        Assert.Contains(pack, ReferenceVoicePack.All);
    }

    /// <summary>
    ///     Proves that the descriptor has reference identity semantics (it is a class, not a value-equal record).
    /// </summary>
    [Fact]
    public void ReferenceVoicePack_Equals_SameValuesDifferentInstances_AreNotEqual()
    {
        // Arrange
        var first = Create();
        var second = Create();

        // Assert
        Assert.NotSame(first, second);
        Assert.NotEqual(first, second);
        Assert.Equal(first, first);
    }

    /// <summary>
    ///     Proves that a valid descriptor is accepted and exposes its values.
    /// </summary>
    [Fact]
    public void ReferenceVoicePack_Constructor_ValidArguments_ExposesValues()
    {
        // Act
        var pack = Create();

        // Assert
        Assert.Equal("pack-1", pack.Id);
        Assert.Equal("Pack", pack.DisplayName);
        Assert.Equal("Attribution", pack.Attribution);
        Assert.Equal(new Uri("https://example.invalid/source"), pack.SourceUrl);
        Assert.Equal(new Uri("https://example.invalid/license"), pack.LicenseUrl);
        Assert.Equal(new Uri("https://example.invalid/pack-1.zip"), pack.Uri);
        Assert.Equal(ValidChecksum, pack.Sha256Checksum);
        Assert.Equal(10, pack.SizeBytes);
        Assert.Equal("pack-1.zip", pack.ArchiveFileName);
        Assert.Equal("root", pack.ArchiveRootFolder);
        Assert.Equal("CC0", pack.LicenseName);
    }

    /// <summary>
    ///     Proves that the download descriptor holds exactly one file naming the archive, its URI and its checksum.
    /// </summary>
    [Fact]
    public void ReferenceVoicePack_DownloadDescriptor_DescribesTheArchive()
    {
        // Act
        var pack = Create();
        var file = Assert.Single(pack.DownloadDescriptor.Files);

        // Assert
        Assert.Equal(pack.Uri, file.Uri);
        Assert.Equal(pack.Sha256Checksum, file.Sha256Checksum);
        Assert.Equal("pack-1.zip", file.RelativeInstallPath);
    }

    /// <summary>
    ///     Proves that the built-in pack's display name, license and source pages are the published values.
    /// </summary>
    [Fact]
    public void ReferenceVoicePack_VoiceZero10_HasPublishedPresentationFields()
    {
        // Act
        var pack = ReferenceVoicePack.VoiceZero10;

        // Assert
        Assert.Equal("voice-zero 1.0", pack.DisplayName);
        Assert.Equal("voice-zero by OwenTyme", pack.Attribution);
        Assert.Equal(new Uri("https://creativecommons.org/publicdomain/zero/1.0/"), pack.LicenseUrl);
        Assert.Equal(new Uri("https://github.com/OwenTyme/voice-zero"), pack.SourceUrl);
        Assert.Equal(pack.ArchiveFileName, Assert.Single(pack.DownloadDescriptor.Files).RelativeInstallPath);
    }

    /// <summary>
    ///     Proves that an archive file name that is not a single safe name is rejected, naming the parameter.
    /// </summary>
    [Theory]
    [InlineData("a/b.zip")]
    [InlineData("..\\evil.zip")]
    [InlineData("..")]
    [InlineData("C:")]
    [InlineData("C:evil.zip")]
    [InlineData("C:\\evil.zip")]
    [InlineData("/evil.zip")]
    [InlineData("a:b.zip")]
    public void ReferenceVoicePack_Constructor_UnsafeArchiveFileName_Throws(string name)
    {
        // Act
        var ex = Assert.Throws<ArgumentException>(() => Create(archiveFileName: name));

        // Assert
        Assert.Equal("archiveFileName", ex.ParamName);
    }

    /// <summary>
    ///     Proves that a negative archive size is rejected.
    /// </summary>
    [Fact]
    public void ReferenceVoicePack_Constructor_NegativeSize_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(size: -1));

    /// <summary>
    ///     Proves that the built-in pack carries the sizes measured from the real archive (voices plus license/readme
    ///     files, and the emotion clips) and that the installed-size arithmetic adds them only when emotions are included.
    /// </summary>
    [Fact]
    public void ReferenceVoicePack_VoiceZero10_InstalledSizes_AreMeasuredValues()
    {
        // Act
        var pack = ReferenceVoicePack.VoiceZero10;

        // Assert
        Assert.Equal(45_632_432L, pack.VoicesBytes);
        Assert.Equal(553_873_246L, pack.EmotionsBytes);
        Assert.Equal(pack.VoicesBytes, pack.InstalledSizeBytes(includeEmotions: false));
        Assert.Equal(599_505_678L, pack.InstalledSizeBytes(includeEmotions: true));
    }

    /// <summary>
    ///     Proves that a pack constructed without installed sizes reports zero and that negative sizes are rejected.
    /// </summary>
    [Fact]
    public void ReferenceVoicePack_Constructor_InstalledSizes_DefaultZeroAndRejectNegative()
    {
        // Act
        var pack = Create();

        // Assert
        Assert.Equal(0L, pack.InstalledSizeBytes(includeEmotions: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReferenceVoicePack(
            "pack-1", "Pack", new Uri("https://example.invalid/pack-1.zip"), ValidChecksum, 10, "pack-1.zip", "root", "CC0",
            new Uri("https://example.invalid/license"), "Attribution", new Uri("https://example.invalid/source"), voicesBytes: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReferenceVoicePack(
            "pack-1", "Pack", new Uri("https://example.invalid/pack-1.zip"), ValidChecksum, 10, "pack-1.zip", "root", "CC0",
            new Uri("https://example.invalid/license"), "Attribution", new Uri("https://example.invalid/source"), emotionsBytes: -1));
    }

    /// <summary>
    ///     Proves that a non-HTTPS archive URI is rejected.
    /// </summary>
    [Fact]
    public void ReferenceVoicePack_Constructor_NonHttpsUri_Throws() =>
        Assert.Throws<ArgumentException>(() => Create(uri: new Uri("http://example.invalid/pack-1.zip")));

    /// <summary>
    ///     Proves that a malformed checksum is rejected.
    /// </summary>
    [Theory]
    [InlineData("abc")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void ReferenceVoicePack_Constructor_BadChecksum_Throws(string checksum) =>
        Assert.Throws<ArgumentException>(() => Create(checksum: checksum));

    /// <summary>
    ///     Proves that an id, archive name or root folder that is not a single safe name is rejected.
    /// </summary>
    [Theory]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("..")]
    [InlineData("  ")]
    [InlineData("C:")]
    [InlineData("C:pack")]
    [InlineData("C:\\pack")]
    [InlineData("/pack")]
    public void ReferenceVoicePack_Constructor_UnsafeId_Throws(string id) =>
        Assert.ThrowsAny<ArgumentException>(() => Create(id: id));

    /// <summary>
    ///     Proves that a rooted or drive-qualified archive root folder is rejected on every platform.
    /// </summary>
    [Theory]
    [InlineData("C:")]
    [InlineData("C:root")]
    [InlineData("/root")]
    public void ReferenceVoicePack_Constructor_RootedRootFolder_Throws(string root) =>
        Assert.Throws<ArgumentException>(() => Create(root: root));

    /// <summary>
    ///     Proves that an unsafe archive root folder is rejected.
    /// </summary>
    [Fact]
    public void ReferenceVoicePack_Constructor_UnsafeRootFolder_Throws() =>
        Assert.Throws<ArgumentException>(() => Create(root: "a/b"));

    /// <summary>
    ///     Proves that a non-positive size is rejected.
    /// </summary>
    [Fact]
    public void ReferenceVoicePack_Constructor_NonPositiveSize_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(size: 0));

    /// <summary>
    ///     Proves that a voice's mood clip is found case-insensitively after trimming the requested name, and that
    ///     null, blank, unknown names and a voice without mood clips never match.
    /// </summary>
    [Fact]
    public void ReferenceVoiceInfo_TryGetMoodClip_IsCaseInsensitiveAndTrimmed()
    {
        // Arrange
        var clips = new Dictionary<string, string> { ["sad"] = "/m/sad.flac", ["anger"] = "/m/anger.flac" };
        var voice = new ReferenceVoiceInfo("v", "V", "/v.flac", ReferenceVoiceSource.Pack, "p", "v", MoodClips: clips);
        var plain = new ReferenceVoiceInfo("v", "V", "/v.flac", ReferenceVoiceSource.User, null, "v");

        // Act and Assert
        foreach (var name in new[] { "sad", "SAD", " Sad ", "\tsAd" })
        {
            Assert.True(voice.TryGetMoodClip(name, out var path), name);
            Assert.Equal("/m/sad.flac", path);
        }

        foreach (var name in new[] { null, "", "  ", "calm", "sa d" })
        {
            Assert.False(voice.TryGetMoodClip(name, out var missing), name);
            Assert.Null(missing);
        }

        Assert.False(plain.TryGetMoodClip("sad", out _));
    }
}
