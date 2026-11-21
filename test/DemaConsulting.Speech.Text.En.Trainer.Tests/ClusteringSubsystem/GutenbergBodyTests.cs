using System.Text;
using DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.ClusteringSubsystem;

/// <summary>
///     Unit tests for <see cref="GutenbergBody"/>: the body between the markers, the stripped paragraphs, the text
///     normalization, the pin and the rejected downloads.
/// </summary>
public sealed class GutenbergBodyTests
{
    /// <summary>
    ///     Wraps a body in a header and a footer with CRLF line endings.
    /// </summary>
    /// <param name="body">The body lines, LF-separated.</param>
    /// <param name="bom">Whether to start with a byte order mark.</param>
    /// <returns>The download bytes.</returns>
    private static byte[] Download(string body, bool bom = false)
    {
        var text = ("The Project Gutenberg eBook of Tests\nRelease date: 2000\n\n*** START OF THE PROJECT GUTENBERG EBOOK TESTS ***\n"
            + body + "\n*** END OF THE PROJECT GUTENBERG EBOOK TESTS ***\nSection 1. License terms.\n").Replace("\n", "\r\n", StringComparison.Ordinal);
        var bytes = Encoding.UTF8.GetBytes(text);
        return bom ? [0xEF, 0xBB, 0xBF, .. bytes] : bytes;
    }

    /// <summary>Proves only the text between the markers is kept, with LF endings and one final LF.</summary>
    [Fact]
    public void GutenbergBody_Extract_HeaderAndFooter_KeepsOnlyTheBody()
    {
        // Arrange
        var download = Download("\n\nFirst line of the book.\nSecond line.\n\n\n\nNext paragraph.  \n\n", bom: true);

        // Act
        var body = GutenbergBody.Extract(download);

        // Assert
        Assert.Equal("First line of the book.\nSecond line.\n\nNext paragraph.\n", body.Text);
        Assert.Equal(Encoding.UTF8.GetBytes(body.Text), body.Bytes);
        Assert.Equal(GutenbergBody.Sha256Hex(body.Bytes), body.Sha256);
    }

    /// <summary>Proves a credit paragraph near the start and every paragraph that names the archive are dropped and counted.</summary>
    [Fact]
    public void GutenbergBody_Extract_CreditsAndArchiveNames_AreDroppedAndCounted()
    {
        // Arrange
        var download = Download("Produced by A. Volunteer and the Online\nDistributed Proofreading Team\n\nTHE STORY\n\n"
            + "This file came from project\ngutenberg volunteers.\n\nThe end.");

        // Act
        var body = GutenbergBody.Extract(download);

        // Assert
        Assert.Equal("THE STORY\n\nThe end.\n", body.Text);
        Assert.Equal(1, body.CreditParagraphs);
        Assert.Equal(1, body.ArchiveNameParagraphs);
    }

    /// <summary>Proves a credit-like paragraph beyond the first 40 body lines is kept.</summary>
    [Fact]
    public void GutenbergBody_Extract_CreditBeyondWindow_IsKept()
    {
        // Arrange: 41 lines of text before the paragraph
        var lines = string.Join('\n', Enumerable.Range(0, GutenbergBody.CreditWindowLines + 1).Select(static i => $"Line {i}."));
        var download = Download(lines + "\n\nProduced by the author himself.");

        // Act
        var body = GutenbergBody.Extract(download);

        // Assert
        Assert.EndsWith("\n\nProduced by the author himself.\n", body.Text, StringComparison.Ordinal);
        Assert.Equal(0, body.CreditParagraphs);
    }

    /// <summary>Proves italic underscores are removed and trailing spaces and tabs trimmed.</summary>
    [Fact]
    public void GutenbergBody_Extract_UnderscoresAndTrailingBlanks_AreRemoved()
    {
        // Arrange
        var download = Download("It was _very_ minute.\t \n_\n\nDone.");

        // Act
        var body = GutenbergBody.Extract(download);

        // Assert: the underscore-only line becomes blank and collapses with the paragraph break
        Assert.Equal("It was very minute.\n\nDone.\n", body.Text);
    }

    /// <summary>Proves the same download always gives the same bytes and pin, and the lowercase marker form is accepted.</summary>
    [Fact]
    public void GutenbergBody_Extract_SameDownload_SamePin()
    {
        // Arrange
        var download = Encoding.UTF8.GetBytes("header\n*** Start of this Project Gutenberg E-Book x ***\nText.\n***END OF THE PROJECT GUTENBERG EBOOK x ***\n");

        // Act
        var first = GutenbergBody.Extract(download);
        var second = GutenbergBody.Extract((byte[])download.Clone());

        // Assert
        Assert.Equal("Text.\n", first.Text);
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Matches("^[0-9a-f]{64}$", first.Sha256);
    }

    /// <summary>Proves a download without a start or end marker, with an empty body or with invalid UTF-8 is rejected.</summary>
    [Fact]
    public void GutenbergBody_Extract_InvalidDownloads_Throw()
    {
        // Arrange
        var noStart = Encoding.UTF8.GetBytes("Text.\n*** END OF THE PROJECT GUTENBERG EBOOK x ***\n");
        var noEnd = Encoding.UTF8.GetBytes("*** START OF THE PROJECT GUTENBERG EBOOK x ***\nText.\n");
        var empty = Download("\n\nThis came from Project Gutenberg.\n");
        byte[] invalid = [.. Download("Text."), 0xC3];

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => GutenbergBody.Extract(noStart));
        Assert.Throws<InvalidDataException>(() => GutenbergBody.Extract(noEnd));
        Assert.Throws<InvalidDataException>(() => GutenbergBody.Extract(empty));
        Assert.Throws<InvalidDataException>(() => GutenbergBody.Extract(invalid));
    }
}
