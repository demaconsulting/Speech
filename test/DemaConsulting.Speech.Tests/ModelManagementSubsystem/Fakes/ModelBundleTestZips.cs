using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;

/// <summary>
///     Builds small synthetic model bundle zips (built in memory with <see cref="System.IO.Compression"/>) and the
///     matching <see cref="SpeechModelBundle"/> declarations, so no test needs a real published bundle or any network
///     access.
/// </summary>
internal static class ModelBundleTestZips
{
    /// <summary>The bundle file name used by default.</summary>
    internal const string FileName = "test-model-v1.0-int8-b1.zip";

    /// <summary>The bundle name used by default (the file name without <c>.zip</c>).</summary>
    internal const string Name = "test-model-v1.0-int8-b1";

    /// <summary>Gets the default vendor files: a model graph in a subfolder and a voice file in another.</summary>
    internal static IReadOnlyList<(string Path, byte[] Content)> VendorFiles { get; } =
    [
        ("onnx/model.onnx", "model-graph-bytes"u8.ToArray()),
        ("voices/af.bin", "voice-embedding-bytes"u8.ToArray()),
    ];

    /// <summary>Computes the lowercase hexadecimal SHA-256 of some bytes.</summary>
    /// <param name="bytes">The bytes to hash.</param>
    /// <returns>The 64-character checksum.</returns>
    internal static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>Builds the compiled-pin <c>Contents</c> descriptor for vendor files.</summary>
    /// <param name="vendorFiles">The vendor files to pin.</param>
    /// <returns>A descriptor with one pinned entry per file at its path.</returns>
    internal static SpeechModelDownloadDescriptor ContentsFor(IEnumerable<(string Path, byte[] Content)> vendorFiles) =>
        new([.. vendorFiles.Select(file => new SpeechModelDownloadFile(
            new Uri($"https://example.invalid/upstream/{file.Path}"), Sha256(file.Content), file.Path))]);

    /// <summary>Builds <c>model.json</c> text in the layout the bundler writes.</summary>
    /// <param name="bundleName">The value of <c>"bundle"</c>.</param>
    /// <param name="files">The declared files (path, size and SHA-256).</param>
    /// <param name="schemaVersion">The value of <c>"schemaVersion"</c>.</param>
    /// <returns>The UTF-8 bytes of the JSON document.</returns>
    internal static byte[] ManifestJson(string bundleName, IEnumerable<(string Path, long Size, string Sha256)> files, int schemaVersion = 1)
    {
        var manifest = new
        {
            schemaVersion,
            bundle = bundleName,
            model = "test-model",
            licenses = new[] { new { name = "MIT", url = "https://opensource.org/license/mit", appliesTo = "everything" } },
            files = files.Select(file => new
            {
                path = file.Path,
                size = file.Size,
                sha256 = file.Sha256,
                sourceUrl = $"https://example.invalid/upstream/{file.Path}",
            }).ToArray(),
        };
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
    }

    /// <summary>Builds <c>model.json</c> that truthfully declares vendor files.</summary>
    /// <param name="bundleName">The value of <c>"bundle"</c>.</param>
    /// <param name="vendorFiles">The vendor files.</param>
    /// <returns>The UTF-8 bytes of the JSON document.</returns>
    internal static byte[] ManifestJsonFor(string bundleName, IEnumerable<(string Path, byte[] Content)> vendorFiles) =>
        ManifestJson(bundleName, vendorFiles.Select(file => (file.Path, (long)file.Content.Length, Sha256(file.Content))));

    /// <summary>
    ///     Builds the entry list of a well-formed bundle: README.txt, LICENSE, optionally NOTICE, model.json, then
    ///     the vendor files. Tests mutate the list to build malformed bundles.
    /// </summary>
    /// <param name="vendorFiles">The vendor files, or <see langword="null"/> for <see cref="VendorFiles"/>.</param>
    /// <param name="bundleName">The bundle name recorded in <c>model.json</c>.</param>
    /// <param name="includeNotice"><see langword="true"/> to include the optional NOTICE entry.</param>
    /// <returns>The mutable entry list.</returns>
    internal static List<(string Name, byte[] Content)> BundleEntries(
        IReadOnlyList<(string Path, byte[] Content)>? vendorFiles = null,
        string bundleName = Name,
        bool includeNotice = true)
    {
        var files = vendorFiles ?? VendorFiles;
        var entries = new List<(string Name, byte[] Content)>
        {
            ("README.txt", "Repackaged for tests; the vendor files are unmodified."u8.ToArray()),
            ("LICENSE", "MIT License text"u8.ToArray()),
        };
        if (includeNotice)
        {
            entries.Add(("NOTICE", "Test attribution notice"u8.ToArray()));
        }

        entries.Add(("model.json", ManifestJsonFor(bundleName, files)));
        entries.AddRange(files.Select(file => (file.Path, file.Content)));
        return entries;
    }

    /// <summary>Builds zip bytes holding the given entries in order.</summary>
    /// <param name="entries">The entry names and contents.</param>
    /// <returns>The zip bytes.</returns>
    internal static byte[] BuildZip(IEnumerable<(string Name, byte[] Content)> entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
                using var entryStream = entry.Open();
                entryStream.Write(content, 0, content.Length);
            }
        }

        return stream.ToArray();
    }

    /// <summary>Creates the bundle declaration for zip bytes, pinning the given vendor files as its contents.</summary>
    /// <param name="zipBytes">The bundle zip (its SHA-256 becomes the bundle pin).</param>
    /// <param name="vendorFiles">The vendor files to pin, or <see langword="null"/> for <see cref="VendorFiles"/>.</param>
    /// <param name="fileName">The bundle file name.</param>
    /// <returns>The bundle declaration.</returns>
    internal static SpeechModelBundle BundleFor(
        byte[] zipBytes,
        IReadOnlyList<(string Path, byte[] Content)>? vendorFiles = null,
        string fileName = FileName) =>
        new(new Uri(SpeechModelBundle.ModelsReleaseBaseUri, fileName), Sha256(zipBytes), ContentsFor(vendorFiles ?? VendorFiles));

    /// <summary>Writes zip bytes to <c>{directory}/{fileName}</c>, creating the directory.</summary>
    /// <param name="directory">The staging directory.</param>
    /// <param name="zipBytes">The zip bytes.</param>
    /// <param name="fileName">The bundle file name.</param>
    /// <returns>The written path.</returns>
    internal static string Stage(string directory, byte[] zipBytes, string fileName = FileName)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Join(directory, fileName);
        File.WriteAllBytes(path, zipBytes);
        return path;
    }

    /// <summary>Lists every file under a directory as sorted, <c>/</c>-separated relative paths.</summary>
    /// <param name="directory">The directory to list.</param>
    /// <returns>The relative paths in ordinal order.</returns>
    internal static string[] ListFiles(string directory) =>
        [.. Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(directory, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];
}
