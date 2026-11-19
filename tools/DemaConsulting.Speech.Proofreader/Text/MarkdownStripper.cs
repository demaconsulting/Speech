using System.Text;
using System.Text.RegularExpressions;

namespace DemaConsulting.Speech.Proofreader.Text;

/// <summary>
///     Reduces simple Markdown to the plain prose a listener would expect to hear.
/// </summary>
/// <remarks>
///     The Proofreader is a listening aid, not a Markdown renderer, so only the constructs that
///     would otherwise be read aloud as noise are handled: heading, list, and quote markers,
///     emphasis markers, code spans, link and image targets, HTML tags, rules, and fenced code
///     blocks (dropped entirely because code is not prose). Every heading and list item becomes
///     its own paragraph (surrounded by blank lines) because such lines usually carry no terminal
///     punctuation and must never be glued to the next block by the sentence splitter. The type
///     is stateless and thread-safe.
/// </remarks>
internal static partial class MarkdownStripper
{
    /// <summary>The block-level markers removed from the start of a line, in application order.</summary>
    private static readonly Regex[] BlockMarkers = [HeadingRegex(), ListMarkerRegex(), QuoteRegex()];

    /// <summary>
    ///     Converts Markdown text to plain text, one paragraph per block, separated by blank lines.
    /// </summary>
    /// <param name="markdown">The Markdown source. Must not be <see langword="null"/>.</param>
    /// <returns>The plain text with LF line endings and no leading or trailing whitespace.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="markdown"/> is <see langword="null"/>.</exception>
    public static string Strip(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var output = new StringBuilder(markdown.Length);
        var inFence = false;
        var needsBlankLine = false;

        foreach (var rawLine in markdown.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();

            // Fenced code blocks are dropped as a whole: the fence lines toggle the state and
            // every line in between is skipped.
            if (FenceRegex().IsMatch(line))
            {
                inFence = !inFence;
                needsBlankLine = true;
                continue;
            }

            if (inFence)
            {
                continue;
            }

            // A blank line or a horizontal rule ends the current paragraph.
            if (line.Trim().Length == 0 || RuleRegex().IsMatch(line))
            {
                needsBlankLine = true;
                continue;
            }

            // Block markers are removed; headings, list items, and quotes start their own paragraph.
            var startsBlock = false;
            var content = line;
            foreach (var markerRegex in BlockMarkers)
            {
                var stripped = markerRegex.Replace(content, string.Empty, 1);
                if (stripped.Length != content.Length)
                {
                    content = stripped;
                    startsBlock = true;
                }
            }

            content = StripInline(content).Trim();
            if (content.Length == 0)
            {
                needsBlankLine = true;
                continue;
            }

            // Separate this block from the previous one with a blank line when required.
            if (output.Length > 0)
            {
                output.Append(needsBlankLine || startsBlock ? "\n\n" : "\n");
            }

            output.Append(content);
            needsBlankLine = startsBlock;
        }

        return output.ToString().Trim();
    }

    /// <summary>
    ///     Removes inline Markdown syntax from one line, keeping the visible text.
    /// </summary>
    /// <param name="text">One line of Markdown content with its block marker already removed.</param>
    /// <returns>The line without images, link targets, code-span backticks, emphasis markers, or HTML tags.</returns>
    private static string StripInline(string text)
    {
        // Images and links keep their visible text and drop the target.
        text = ImageRegex().Replace(text, "$1");
        text = LinkRegex().Replace(text, "$1");

        // HTML tags (including automatic links) are markup, not speech.
        text = HtmlTagRegex().Replace(text, string.Empty);

        // Code spans keep their content; emphasis markers are removed at word edges only, so
        // identifiers such as snake_case_name stay intact.
        text = CodeSpanRegex().Replace(text, "$1");
        text = StrongRegex().Replace(text, "$2");
        text = EmphasisStarRegex().Replace(text, "$1");
        text = EmphasisUnderscoreRegex().Replace(text, "$1");
        return StrikethroughRegex().Replace(text, "$1");
    }

    /// <summary>Matches a fenced code block delimiter line.</summary>
    [GeneratedRegex(@"^\s{0,3}(```|~~~)")]
    private static partial Regex FenceRegex();

    /// <summary>Matches a horizontal rule line.</summary>
    [GeneratedRegex(@"^\s{0,3}([-*_])(\s*\1){2,}\s*$")]
    private static partial Regex RuleRegex();

    /// <summary>Matches an ATX heading marker.</summary>
    [GeneratedRegex(@"^\s{0,3}#{1,6}\s+")]
    private static partial Regex HeadingRegex();

    /// <summary>Matches a bullet or numbered list marker.</summary>
    [GeneratedRegex(@"^\s*([-*+]|\d{1,9}[.)])\s+")]
    private static partial Regex ListMarkerRegex();

    /// <summary>Matches a block quote marker.</summary>
    [GeneratedRegex(@"^\s{0,3}>\s?")]
    private static partial Regex QuoteRegex();

    /// <summary>Matches an inline image, capturing the alternative text.</summary>
    [GeneratedRegex(@"!\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex ImageRegex();

    /// <summary>Matches an inline link, capturing the link text.</summary>
    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex LinkRegex();

    /// <summary>Matches an HTML tag or autolink.</summary>
    [GeneratedRegex(@"</?[A-Za-z][^>]*>")]
    private static partial Regex HtmlTagRegex();

    /// <summary>Matches an inline code span, capturing its content.</summary>
    [GeneratedRegex(@"`([^`]*)`")]
    private static partial Regex CodeSpanRegex();

    /// <summary>Matches strong emphasis, capturing its content in group 2.</summary>
    [GeneratedRegex(@"(\*\*|__)(?=\S)(.+?)(?<=\S)\1")]
    private static partial Regex StrongRegex();

    /// <summary>Matches single-asterisk emphasis, capturing its content.</summary>
    [GeneratedRegex(@"\*(?=\S)([^*]+?)(?<=\S)\*")]
    private static partial Regex EmphasisStarRegex();

    /// <summary>Matches single-underscore emphasis at word edges, capturing its content.</summary>
    [GeneratedRegex(@"(?<!\w)_(?=\S)([^_]+?)(?<=\S)_(?!\w)")]
    private static partial Regex EmphasisUnderscoreRegex();

    /// <summary>Matches strikethrough, capturing its content.</summary>
    [GeneratedRegex(@"~~(?=\S)(.+?)(?<=\S)~~")]
    private static partial Regex StrikethroughRegex();
}
