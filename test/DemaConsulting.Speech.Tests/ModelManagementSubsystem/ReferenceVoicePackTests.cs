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
    }

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
    public void ReferenceVoicePack_Constructor_UnsafeId_Throws(string id) =>
        Assert.ThrowsAny<ArgumentException>(() => Create(id: id));

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
}
