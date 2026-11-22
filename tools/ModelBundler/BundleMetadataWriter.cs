using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DemaConsulting.Speech.ModelBundler;

/// <summary>
///     Creates the four metadata entries every bundle carries beside its vendor files: <c>README.txt</c>,
///     <c>LICENSE</c>, <c>NOTICE</c> and <c>model.json</c>.
/// </summary>
/// <remarks>
///     Every entry is a pure function of the bundle definition and the verified vendor files, encoded as UTF-8 without a
///     byte order mark and with LF line endings, so a rebuild produces identical bytes on any machine. The vendor files
///     must already be sorted by entry name (ordinal), which <see cref="BundleBuilder"/> guarantees. Stateless and
///     thread-safe.
/// </remarks>
internal static class BundleMetadataWriter
{
    /// <summary>The <c>model.json</c> schema version the runtime installer accepts.</summary>
    internal const int SchemaVersion = 1;

    /// <summary>Who repackaged the bundle, as stated in <c>model.json</c> and <c>README.txt</c>.</summary>
    internal const string RepackagedBy = "DEMA Consulting for DemaConsulting.Speech";

    /// <summary>Where to report problems with a bundle.</summary>
#pragma warning disable S1075 // The project's issue tracker is a deliberate, reviewed literal.
    internal const string IssuesUrl = "https://github.com/demaconsulting/Speech/issues";
#pragma warning restore S1075

    /// <summary>The metadata entry names, in their fixed order at the start of every zip.</summary>
    internal static readonly IReadOnlyList<string> EntryOrder = ["README.txt", "LICENSE", "NOTICE", "model.json"];

    /// <summary>The UTF-8 encoding without a byte order mark used for every metadata entry.</summary>
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    ///     Creates every metadata entry of a bundle, keyed by entry name.
    /// </summary>
    /// <param name="definition">The bundle definition.</param>
    /// <param name="files">The verified vendor files, sorted by entry name (ordinal).</param>
    /// <returns>The content of <c>README.txt</c>, <c>LICENSE</c>, <c>NOTICE</c> and <c>model.json</c>.</returns>
    public static IReadOnlyDictionary<string, byte[]> CreateEntries(BundleDefinition definition, IReadOnlyList<ResolvedVendorFile> files) =>
        new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["README.txt"] = Utf8.GetBytes(CreateReadme(definition, files)),
            ["LICENSE"] = Utf8.GetBytes(CreateLicense(definition)),
            ["NOTICE"] = Utf8.GetBytes(CreateNotice(definition)),
            ["model.json"] = CreateModelJson(definition, files),
        };

    /// <summary>
    ///     Creates <c>model.json</c>: the machine-readable description the runtime installer checks.
    /// </summary>
    /// <remarks>
    ///     Keys are written in a fixed order with a two-space indent and LF line endings. <c>files</c> lists exactly the
    ///     vendor files, sorted by path (ordinal), each with its size, lowercase SHA-256 and original source URL.
    /// </remarks>
    /// <param name="definition">The bundle definition.</param>
    /// <param name="files">The verified vendor files, sorted by entry name (ordinal).</param>
    /// <returns>The UTF-8 bytes of the document, ending with a newline.</returns>
    public static byte[] CreateModelJson(BundleDefinition definition, IReadOnlyList<ResolvedVendorFile> files)
    {
        using var buffer = new MemoryStream();
        var options = new JsonWriterOptions
        {
            Indented = true,
            IndentSize = 2,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        using (var writer = new Utf8JsonWriter(buffer, options))
        {
            // Identity of the bundle and its name parts
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WriteString("bundle", definition.Name);
            writer.WriteString("model", definition.Model);
            writer.WriteString("upstreamRelease", definition.UpstreamRelease);
            writer.WriteString("quant", definition.Quant);
            writer.WriteNumber("bundleRevision", definition.BundleRevision);
            writer.WriteString("date", FormatDate(definition.Date));
            writer.WriteString("repackagedBy", RepackagedBy);

            // Provenance and licenses
            writer.WriteStartObject("upstream");
            writer.WriteString("repository", definition.UpstreamRepository);
            writer.WriteString("revision", definition.UpstreamRevision);
            writer.WriteEndObject();
            writer.WriteStartArray("licenses");
            foreach (var license in definition.Licenses)
            {
                writer.WriteStartObject();
                writer.WriteString("name", license.Name);
                writer.WriteString("url", license.Url);
                writer.WriteString("appliesTo", license.AppliesTo);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("modelIds");
            foreach (var modelId in definition.ModelIds)
            {
                writer.WriteStringValue(modelId);
            }

            writer.WriteEndArray();

            // The vendor files the installer verifies, with their original source URLs
            writer.WriteStartArray("files");
            foreach (var file in files)
            {
                writer.WriteStartObject();
                writer.WriteString("path", file.EntryName);
                writer.WriteNumber("size", file.Size);
                writer.WriteString("sha256", file.File.Sha256Checksum.ToLowerInvariant());
                writer.WriteString("sourceUrl", file.File.Uri.AbsoluteUri);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        buffer.WriteByte((byte)'\n');
        return buffer.ToArray();
    }

    /// <summary>
    ///     Creates <c>README.txt</c>: the human note on what the bundle is, that its vendor files are unmodified, what
    ///     was added, which licenses apply and where to report issues.
    /// </summary>
    /// <param name="definition">The bundle definition.</param>
    /// <param name="files">The verified vendor files, sorted by entry name (ordinal).</param>
    /// <returns>The text, with LF line endings.</returns>
    public static string CreateReadme(BundleDefinition definition, IReadOnlyList<ResolvedVendorFile> files)
    {
        var text = new StringBuilder();

        // Who repackaged what, and when
        AppendLine(text, definition.FileName);
        AppendLine(text, new string('=', definition.FileName.Length));
        AppendLine(text);
        AppendLine(text, $"This bundle was repackaged by {RepackagedBy}.");
        AppendLine(text);
        AppendLine(text, $"Bundle:            {definition.Name}");
        AppendLine(text, $"Bundle revision:   b{definition.BundleRevision.ToString(CultureInfo.InvariantCulture)}");
        AppendLine(text, $"Date:              {FormatDate(definition.Date)}");
        AppendLine(text, $"Model ids:         {string.Join(", ", definition.ModelIds)}");
        AppendLine(text, $"Upstream:          {definition.UpstreamRepository}");
        AppendLine(text, $"Upstream revision: {definition.UpstreamRevision}");
        AppendLine(text);

        // The unmodified vendor files and their provenance
        AppendLine(text, "Vendor files");
        AppendLine(text, "------------");
        AppendLine(text);
        AppendLine(text, "The vendor files in this bundle are UNMODIFIED copies of the original upstream files. Each file is");
        AppendLine(text, "listed with its original URL and SHA-256; model.json records the same facts.");
        AppendLine(text);
        foreach (var file in files)
        {
            AppendLine(text, $"  {file.EntryName}");
            AppendLine(text, $"    Original URL: {file.File.Uri.AbsoluteUri}");
            AppendLine(text, $"    SHA-256:      {file.File.Sha256Checksum.ToLowerInvariant()}");
            AppendLine(text, $"    Size:         {file.Size.ToString(CultureInfo.InvariantCulture)} bytes");
        }

        AppendLine(text);

        // What DEMA Consulting added
        AppendLine(text, "Added files");
        AppendLine(text, "-----------");
        AppendLine(text);
        AppendLine(text, "DEMA Consulting added only these files to the original files:");
        AppendLine(text);
        AppendLine(text, "  LICENSE     the license text(s) of the original files");
        AppendLine(text, "  NOTICE      the attribution and notices of origin of the original files");
        AppendLine(text, "  model.json  the machine-readable bundle description (files, sizes, SHA-256, original URLs)");
        AppendLine(text, "  README.txt  this file");
        AppendLine(text);

        // The original licenses and the required attribution
        AppendLine(text, "License");
        AppendLine(text, "-------");
        AppendLine(text);
        foreach (var license in definition.Licenses)
        {
            AppendLine(text, $"  {license.Name} ({license.Url})");
            AppendLine(text, $"    applies to: {license.AppliesTo}");
        }

        AppendLine(text);
        AppendLine(text, "Attribution:");
        AppendLine(text, definition.Attribution);
        AppendLine(text);

        // Where to report issues
        AppendLine(text, "Issues");
        AppendLine(text, "------");
        AppendLine(text);
        AppendLine(text, $"Report problems with this bundle at {IssuesUrl}.");
        return text.ToString();
    }

    /// <summary>
    ///     Creates <c>LICENSE</c>: the full text of each license, verbatim. A bundle with several licenses gets each text
    ///     under a header naming the license and what it applies to.
    /// </summary>
    /// <param name="definition">The bundle definition.</param>
    /// <returns>The text, with LF line endings.</returns>
    public static string CreateLicense(BundleDefinition definition)
    {
        // One license: its text exactly as published
        if (definition.Licenses.Count == 1)
        {
            return definition.Licenses[0].Text;
        }

        // Several licenses: each text under a separator and a header
        var text = new StringBuilder();
        foreach (var license in definition.Licenses)
        {
            if (text.Length > 0)
            {
                AppendLine(text);
            }

            AppendLine(text, new string('=', 78));
            AppendLine(text, $"{license.Name} - applies to: {license.AppliesTo}");
            AppendLine(text, license.Url);
            AppendLine(text, new string('=', 78));
            AppendLine(text);
            text.Append(license.Text);
        }

        return text.ToString();
    }

    /// <summary>
    ///     Creates <c>NOTICE</c>: the attribution, copyright and notices of origin from the definition, followed by the
    ///     fixed statement that the files are unmodified.
    /// </summary>
    /// <param name="definition">The bundle definition.</param>
    /// <returns>The text, with LF line endings.</returns>
    public static string CreateNotice(BundleDefinition definition)
    {
        var text = new StringBuilder();
        foreach (var line in definition.NoticeLines)
        {
            AppendLine(text, line);
        }

        AppendLine(text);
        AppendLine(text, "The files listed in model.json are unmodified copies of the original upstream files.");
        AppendLine(text, $"They were repackaged by {RepackagedBy} ({definition.Name}, {FormatDate(definition.Date)}),");
        AppendLine(text, "which added only LICENSE, NOTICE, model.json and README.txt.");
        return text.ToString();
    }

    /// <summary>Formats a bundle date as <c>yyyy-MM-dd</c>.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The invariant ISO date.</returns>
    internal static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Appends a line terminated by LF (never the platform newline).</summary>
    /// <param name="text">The builder.</param>
    /// <param name="line">The line, or empty for a blank line.</param>
    private static void AppendLine(StringBuilder text, string line = "") => text.Append(line).Append('\n');
}
