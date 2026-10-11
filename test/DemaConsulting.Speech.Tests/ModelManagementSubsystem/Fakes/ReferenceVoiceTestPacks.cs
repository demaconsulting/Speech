using System.IO.Compression;
using System.Security.Cryptography;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;

/// <summary>
///     Builds small synthetic reference-voice archives and matching pack descriptors for tests, so no test needs the
///     real (about 570 MiB) upstream archive or any network access.
/// </summary>
internal static class ReferenceVoiceTestPacks
{
    /// <summary>The root folder used inside synthetic archives.</summary>
    internal const string Root = "voice-zero";

    /// <summary>Builds the zip bytes for the given entry name/content pairs.</summary>
    internal static byte[] BuildZip(IEnumerable<(string Name, byte[] Content)> entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var entryStream = entry.Open();
                entryStream.Write(content, 0, content.Length);
            }
        }

        return stream.ToArray();
    }

    /// <summary>Builds a well-formed archive: two voices plus license/readme files and ignorable extras.</summary>
    internal static byte[] BuildTypicalZip() => BuildZip(
    [
        ($"{Root}/LICENSE.md", "license"u8.ToArray()),
        ($"{Root}/README.md", "readme"u8.ToArray()),
        ($"{Root}/icon.svg", "<svg/>"u8.ToArray()),
        ($"{Root}/voices/README.md", "voices readme"u8.ToArray()),
        ($"{Root}/voices/alan_davis_drake2.flac", "flac-a"u8.ToArray()),
        ($"{Root}/voices/alana_jordan.flac", "flac-b"u8.ToArray()),
        ($"{Root}/voices/notes.txt", "ignored"u8.ToArray()),
        ($"{Root}/voices-emotion/README.md", "emotion readme"u8.ToArray()),
        ($"{Root}/voices-emotion/alana_jordan/happy.flac", "emotion"u8.ToArray()),
    ]);

    /// <summary>Creates a pack descriptor whose checksum matches <paramref name="zipBytes"/>.</summary>
    internal static ReferenceVoicePack PackFor(byte[] zipBytes, string id = "voice-zero-1.0") => new(
        id,
        "Test pack",
        new Uri($"https://example.invalid/{id}.zip"),
        Convert.ToHexStringLower(SHA256.HashData(zipBytes)),
        zipBytes.Length,
        $"{id}.zip",
        Root,
        "CC0 1.0 Universal",
        new Uri("https://creativecommons.org/publicdomain/zero/1.0/"),
        "Test attribution",
        new Uri("https://example.invalid/project"));

    /// <summary>Writes <paramref name="zipBytes"/> to <c>{directory}/{pack.ArchiveFileName}</c> and returns the path.</summary>
    internal static string WriteArchive(string directory, ReferenceVoicePack pack, byte[] zipBytes)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Join(directory, pack.ArchiveFileName);
        File.WriteAllBytes(path, zipBytes);
        return path;
    }
}
