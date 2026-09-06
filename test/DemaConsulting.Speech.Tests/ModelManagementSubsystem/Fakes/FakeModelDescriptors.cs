using System.IO.Compression;
using System.Security.Cryptography;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;

/// <summary>
///     Builds genuinely valid <see cref="SpeechModelDownloadDescriptor"/> instances for the fake
///     model classes, using a fixed local placeholder URI (never actually dereferenced by tests
///     that use a fake <see cref="IModelDownloadClient"/>) paired with a checksum that truly
///     matches a small, deterministic in-memory payload.
/// </summary>
internal static class FakeModelDescriptors
{
    /// <summary>
    ///     Builds a single-file download descriptor for the given model id, whose declared
    ///     checksum genuinely matches <see cref="Payload"/>'s SHA-256 hash.
    /// </summary>
    /// <param name="modelId">The model id, used only to vary the placeholder source URI.</param>
    public static SpeechModelDownloadDescriptor SingleFileDescriptor(string modelId)
    {
        var checksum = Convert.ToHexStringLower(SHA256.HashData(Payload));
        return new SpeechModelDownloadDescriptor(
        [
            new SpeechModelDownloadFile(new Uri($"https://example.test/{modelId}.bin"), checksum, "model.bin"),
        ]);
    }

    /// <summary>Gets the fixed in-memory payload whose SHA-256 hash matches every descriptor built here.</summary>
    public static byte[] Payload { get; } = "fake-model-payload"u8.ToArray();

    /// <summary>The relative install path a zip-archive descriptor declares for its downloaded archive.</summary>
    public const string ZipArchiveRelativeInstallPath = "model.zip";

    /// <summary>The name of the single entry packed into <see cref="ZipArchiveBytes"/>.</summary>
    public const string ZipArchiveEntryName = "model.txt";

    /// <summary>The fixed in-memory content of <see cref="ZipArchiveEntryName"/> within <see cref="ZipArchiveBytes"/>.</summary>
    public static byte[] ZipArchiveEntryContent { get; } = "fake-model-zip-entry-payload"u8.ToArray();

    /// <summary>
    ///     Builds a single-file download descriptor for the given model id whose declared file is
    ///     a small, deterministic in-memory zip archive (see <see cref="ZipArchiveBytes"/>),
    ///     whose declared checksum genuinely matches that archive's own SHA-256 hash.
    /// </summary>
    /// <param name="modelId">The model id, used only to vary the placeholder source URI.</param>
    public static SpeechModelDownloadDescriptor ZipArchiveDescriptor(string modelId)
    {
        var checksum = Convert.ToHexStringLower(SHA256.HashData(ZipArchiveBytes));
        return new SpeechModelDownloadDescriptor(
        [
            new SpeechModelDownloadFile(
                new Uri($"https://example.test/{modelId}.zip"),
                checksum,
                ZipArchiveRelativeInstallPath),
        ]);
    }

    /// <summary>
    ///     Gets a small, deterministic in-memory zip archive (built with
    ///     <see cref="System.IO.Compression.ZipArchive"/>, no new package required) containing one
    ///     entry (<see cref="ZipArchiveEntryName"/>, whose content is <see cref="ZipArchiveEntryContent"/>).
    /// </summary>
    public static byte[] ZipArchiveBytes { get; } = BuildZipArchiveBytes();

    /// <summary>
    ///     Builds <see cref="ZipArchiveBytes"/>'s backing bytes: an in-memory zip archive
    ///     containing a single deterministic entry.
    /// </summary>
    private static byte[] BuildZipArchiveBytes()
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(ZipArchiveEntryName);
            using var entryStream = entry.Open();
            entryStream.Write(ZipArchiveEntryContent);
        }

        return memoryStream.ToArray();
    }
}
