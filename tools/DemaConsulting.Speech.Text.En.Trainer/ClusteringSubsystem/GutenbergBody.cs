using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;

/// <summary>
///     The normalized body of one downloaded public-domain e-book: its text, its UTF-8 bytes, the SHA-256 that pins it
///     and how many paragraphs were stripped.
/// </summary>
/// <param name="Text">The normalized body text; LF line endings, ends with exactly one LF.</param>
/// <param name="Bytes">The UTF-8 bytes of <paramref name="Text"/> (no byte order mark).</param>
/// <param name="Sha256">The lowercase hexadecimal SHA-256 of <paramref name="Bytes"/>; the <c>body_sha256</c> pin.</param>
/// <param name="CreditParagraphs">The number of producer-credit and transcriber-note paragraphs dropped near the start.</param>
/// <param name="ArchiveNameParagraphs">The number of paragraphs dropped because they name the e-book archive.</param>
internal sealed record GutenbergBodyText(string Text, byte[] Bytes, string Sha256, int CreditParagraphs, int ArchiveNameParagraphs);

/// <summary>
///     Extracts the pinned body of a downloaded plain-text e-book: the text between the archive's start and end markers,
///     without its header, footer, license, credit lines or any paragraph that names the archive.
/// </summary>
/// <remarks>
///     <para>
///     The body is what the word-cluster corpus pins (by SHA-256), what the snapshot archive stores and what the reader
///     tokenizes, so the extraction must give the same bytes for the same download on every platform and in every
///     culture. It is therefore a pure function of the downloaded bytes built only from ordinal, culture-free string
///     operations: the two marker patterns are matched case-insensitively with the invariant culture, line endings are
///     unified to LF, and no Unicode normalization is applied (normalization tables differ between ICU and NLS).
///     </para>
///     <para>
///     Every paragraph that contains the archive's name is dropped so the normalized corpus carries none of the
///     archive's trademark text and may be redistributed as plain public-domain text. The type is static and
///     thread-safe.
///     </para>
/// </remarks>
internal static partial class GutenbergBody
{
    /// <summary>The number of body lines at the start in which producer-credit paragraphs are dropped.</summary>
    internal const int CreditWindowLines = 40;

    /// <summary>The archive name whose paragraphs are dropped, compared case-insensitively.</summary>
    private const string ArchiveName = "Project Gutenberg";

    /// <summary>The paragraph openings, compared case-insensitively, that mark a producer credit or transcriber note.</summary>
    private static readonly string[] CreditOpenings = ["Produced by", "E-text prepared by", "Transcriber's Note", "Transcriber\u2019s Note"];

    /// <summary>The strict UTF-8 decoder: invalid bytes are an error, never replacement characters.</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>
    ///     Extracts and normalizes the body of a downloaded e-book.
    /// </summary>
    /// <param name="downloaded">The downloaded bytes, UTF-8 with or without a byte order mark. Must not be null.</param>
    /// <returns>The normalized body with its bytes, pin and stripped-paragraph counts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="downloaded"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     The bytes are not valid UTF-8, the start or end marker is missing, or nothing remains between them; the book
    ///     must then be rejected rather than repaired.
    /// </exception>
    public static GutenbergBodyText Extract(byte[] downloaded)
    {
        ArgumentNullException.ThrowIfNull(downloaded);

        // Decode strictly, without a byte order mark, and unify the line endings to LF
        string text;
        try
        {
            var span = downloaded.AsSpan();
            if (span.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
            {
                span = span[3..];
            }

            text = StrictUtf8.GetString(span);
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidDataException("the download is not valid UTF-8");
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

        // The body is strictly between the first start marker and the first end marker after it
        var start = Array.FindIndex(lines, static line => StartMarker().IsMatch(line));
        if (start < 0)
        {
            throw new InvalidDataException("the start marker of the e-book body was not found");
        }

        var end = Array.FindIndex(lines, start + 1, static line => EndMarker().IsMatch(line));
        if (end < 0)
        {
            throw new InvalidDataException("the end marker of the e-book body was not found");
        }

        var body = lines[(start + 1)..end];
        var kept = DropParagraphs(body, out var credits, out var archiveNames);
        var normalized = Normalize(kept);
        if (normalized.Length == 0)
        {
            throw new InvalidDataException("the e-book body is empty");
        }

        var bytes = Encoding.UTF8.GetBytes(normalized);
        return new GutenbergBodyText(normalized, bytes, Sha256Hex(bytes), credits, archiveNames);
    }

    /// <summary>
    ///     Computes the lowercase hexadecimal SHA-256 of a byte array.
    /// </summary>
    /// <param name="bytes">The bytes. Must not be null.</param>
    /// <returns>The 64-character lowercase hexadecimal hash.</returns>
    internal static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>
    ///     Drops the producer-credit paragraphs near the start and every paragraph that names the archive.
    /// </summary>
    /// <param name="body">The body lines.</param>
    /// <param name="credits">Receives the number of credit paragraphs dropped.</param>
    /// <param name="archiveNames">Receives the number of paragraphs dropped for naming the archive.</param>
    /// <returns>The kept lines; a dropped paragraph leaves its surrounding blank lines.</returns>
    private static List<string> DropParagraphs(string[] body, out int credits, out int archiveNames)
    {
        credits = 0;
        archiveNames = 0;
        var kept = new List<string>(body.Length);
        var index = 0;
        while (index < body.Length)
        {
            // Blank lines separate paragraphs and are always kept here (they are collapsed later)
            if (IsBlank(body[index]))
            {
                kept.Add(body[index]);
                index++;
                continue;
            }

            // A paragraph is the maximal run of non-blank lines; judge it on its lines joined by spaces
            var first = index;
            while (index < body.Length && !IsBlank(body[index]))
            {
                index++;
            }

            var joined = string.Join(' ', body, first, index - first);
            var trimmed = joined.TrimStart(' ', '\t');
            if (first < CreditWindowLines && CreditOpenings.Any(opening => trimmed.StartsWith(opening, StringComparison.OrdinalIgnoreCase)))
            {
                credits++;
            }
            else if (joined.Contains(ArchiveName, StringComparison.OrdinalIgnoreCase))
            {
                archiveNames++;
            }
            else
            {
                kept.AddRange(body.AsSpan(first, index - first));
            }
        }

        return kept;
    }

    /// <summary>
    ///     Removes italic underscores, trims trailing spaces and tabs, collapses blank-line runs to one and trims blank
    ///     lines at both ends.
    /// </summary>
    /// <param name="lines">The kept body lines.</param>
    /// <returns>The normalized text ending with one LF, or empty when no text remains.</returns>
    private static string Normalize(List<string> lines)
    {
        var builder = new StringBuilder();
        var pendingBlank = false;
        foreach (var raw in lines)
        {
            var line = raw.Replace("_", string.Empty, StringComparison.Ordinal).TrimEnd(' ', '\t');
            if (line.Length == 0)
            {
                // A blank line is written only between two text lines, so runs collapse and both ends are trimmed
                pendingBlank = builder.Length > 0;
                continue;
            }

            if (pendingBlank)
            {
                builder.Append('\n');
                pendingBlank = false;
            }

            builder.Append(line).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Determines whether a raw line separates paragraphs.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns><see langword="true"/> when the line holds only spaces and tabs.</returns>
    private static bool IsBlank(string line) => line.AsSpan().TrimStart(" \t").IsEmpty;

    /// <summary>The start marker of the e-book body.</summary>
    /// <returns>The compiled, culture-invariant, case-insensitive pattern.</returns>
    [GeneratedRegex(@"^\*\*\* ?START OF (THE|THIS) PROJECT GUTENBERG E-?BOOK", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
    private static partial Regex StartMarker();

    /// <summary>The end marker of the e-book body.</summary>
    /// <returns>The compiled, culture-invariant, case-insensitive pattern.</returns>
    [GeneratedRegex(@"^\*\*\* ?END OF (THE|THIS) PROJECT GUTENBERG E-?BOOK", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
    private static partial Regex EndMarker();
}
