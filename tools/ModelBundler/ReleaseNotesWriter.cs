using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DemaConsulting.Speech.ModelBundler;

/// <summary>A bundle zip written by a build: its definition, location, SHA-256 and length.</summary>
/// <param name="Definition">The bundle definition.</param>
/// <param name="Path">The full path of the written zip.</param>
/// <param name="Sha256">The lowercase SHA-256 of the zip.</param>
/// <param name="Size">The zip length in bytes.</param>
internal sealed record BuiltBundle(BundleDefinition Definition, string Path, string Sha256, long Size);

/// <summary>One line of a <c>SHA256SUMS</c> file.</summary>
/// <param name="Sha256">The lowercase SHA-256.</param>
/// <param name="FileName">The zip file name.</param>
internal sealed record ChecksumLine(string Sha256, string FileName);

/// <summary>
///     Writes and reads the release-level files a build produces beside the zips: <c>SHA256SUMS</c> (the checksums
///     <c>verify</c> and <c>upload</c> work from) and <c>release-notes.md</c> (the <c>models</c> release body).
/// </summary>
/// <remarks>
///     Both files list the bundles sorted by file name (ordinal), use LF line endings, and depend only on the bundles,
///     so they are reproducible too. Stateless and thread-safe.
/// </remarks>
internal static partial class ReleaseNotesWriter
{
    /// <summary>The checksum file name.</summary>
    internal const string ChecksumsFileName = "SHA256SUMS";

    /// <summary>The release notes file name.</summary>
    internal const string ReleaseNotesFileName = "release-notes.md";

    /// <summary>
    ///     Creates <c>SHA256SUMS</c>: one <c>{sha256}  {file name}</c> line per bundle (the <c>sha256sum</c> format).
    /// </summary>
    /// <param name="bundles">The built bundles.</param>
    /// <returns>The text, with LF line endings.</returns>
    public static string CreateChecksums(IEnumerable<BuiltBundle> bundles)
    {
        var text = new StringBuilder();
        foreach (var bundle in bundles.OrderBy(bundle => bundle.Definition.FileName, StringComparer.Ordinal))
        {
            text.Append(bundle.Sha256).Append("  ").Append(bundle.Definition.FileName).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    ///     Parses <c>SHA256SUMS</c>, accepting only <c>{64 lowercase hex}  {bundle}.zip</c> lines (blank lines ignored).
    /// </summary>
    /// <param name="text">The file content.</param>
    /// <returns>The lines, in file order.</returns>
    /// <exception cref="InvalidDataException">Thrown when a line is malformed, a file repeats, or there is no line.</exception>
    public static IReadOnlyList<ChecksumLine> ParseChecksums(string text)
    {
        var lines = new List<ChecksumLine>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            var match = ChecksumLinePattern().Match(line);
            if (!match.Success)
            {
                throw new InvalidDataException($"'{line}' is not a '{{sha256}}  {{bundle}}.zip' line of {ChecksumsFileName}.");
            }

            lines.Add(new ChecksumLine(match.Groups["sha"].Value, match.Groups["file"].Value));
        }

        if (lines.Count == 0)
        {
            throw new InvalidDataException($"{ChecksumsFileName} lists no bundle.");
        }

        var duplicate = lines.GroupBy(line => line.FileName, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException($"{ChecksumsFileName} lists '{duplicate.Key}' more than once.");
        }

        return lines;
    }

    /// <summary>
    ///     Creates <c>release-notes.md</c>: for each bundle its file name, model ids, original source, licenses,
    ///     attribution, SHA-256 and size, ending with the issues link (suitable for <c>gh release create --notes-file</c>).
    /// </summary>
    /// <param name="bundles">The built bundles.</param>
    /// <returns>The Markdown text, with LF line endings.</returns>
    public static string CreateReleaseNotes(IEnumerable<BuiltBundle> bundles)
    {
        var text = new StringBuilder();
        AppendLine(text, "# Models");
        AppendLine(text);
        AppendLine(text, "Model bundles for DemaConsulting.Speech, repackaged by DEMA Consulting. Each bundle holds the");
        AppendLine(text, "unmodified upstream vendor files plus LICENSE, NOTICE, model.json and README.txt. Assets on this");
        AppendLine(text, "release are never replaced: a changed bundle is published under a new revision (bN).");
        foreach (var bundle in bundles.OrderBy(bundle => bundle.Definition.FileName, StringComparer.Ordinal))
        {
            var definition = bundle.Definition;
            AppendLine(text);
            AppendLine(text, $"## {definition.FileName}");
            AppendLine(text);
            AppendLine(text, $"- Model ids: {string.Join(", ", definition.ModelIds.Select(id => $"`{id}`"))}");
            AppendLine(text, $"- Source: <{definition.UpstreamRepository}>, {definition.UpstreamRevision}");
            foreach (var license in definition.Licenses)
            {
                AppendLine(text, $"- License: [{license.Name}]({license.Url}), applies to {license.AppliesTo}");
            }

            AppendLine(text, $"- Attribution: {definition.Attribution}");
            AppendLine(text, $"- SHA-256: `{bundle.Sha256}`");
            AppendLine(text, $"- Size: {bundle.Size.ToString("N0", CultureInfo.InvariantCulture)} bytes");
        }

        AppendLine(text);
        AppendLine(text, $"Report problems with these bundles at <{BundleMetadataWriter.IssuesUrl}>.");
        return text.ToString();
    }

    /// <summary>Appends a line terminated by LF (never the platform newline).</summary>
    /// <param name="text">The builder.</param>
    /// <param name="line">The line, or empty for a blank line.</param>
    private static void AppendLine(StringBuilder text, string line = "") => text.Append(line).Append('\n');

    /// <summary>A <c>SHA256SUMS</c> line: a lowercase SHA-256, two spaces and a bundle zip file name.</summary>
    /// <returns>The compiled, culture-invariant pattern.</returns>
    [GeneratedRegex(@"^(?<sha>[0-9a-f]{64})  (?<file>[a-z0-9]+([.-][a-z0-9]+)*-(fp32|fp16|int8|int4)-b[1-9][0-9]*\.zip)\z", RegexOptions.CultureInvariant)]
    private static partial Regex ChecksumLinePattern();
}
