using System.Text;

namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Layer 1 of this library's "two-layer tag rendering" design: a pure, model-independent
///     scanner that recognizes closed-vocabulary Natural Language Audio Tag bracket syntax in
///     input text and produces an ordered, neutral sequence of <see cref="TaggedTextSpan"/>.
/// </summary>
/// <remarks>
///     This parser has no knowledge of any synthesis model, engine, or audio device; it is pure
///     text-in, spans-out. Per this library's "guarantees a reply is never worse than plain
///     narration" reasoning, any bracket content this parser cannot resolve against
///     <see cref="AudioTagCatalog"/> - an unknown word, an unclosed bracket, or empty brackets -
///     is emitted as literal <see cref="TaggedTextSpanKind.PlainText"/> (including the brackets
///     themselves) rather than thrown away or raised as an error. Layer 2 per-model rendering (a
///     later phase) consumes this parser's output to build a <c>SpeechPlan</c>.
/// </remarks>
public static class AudioTagParser
{
    /// <summary>
    ///     Scans <paramref name="text"/> for closed-vocabulary Natural Language Audio Tag bracket
    ///     syntax and returns the ordered sequence of literal-text and recognized-tag spans that
    ///     make it up.
    /// </summary>
    /// <param name="text">The input text to scan. Must not be <see langword="null"/>.</param>
    /// <returns>
    ///     An ordered, read-only list of spans reconstructing <paramref name="text"/>: recognized
    ///     tags become <see cref="TaggedTextSpanKind.Tag"/> spans, and everything else - including
    ///     unrecognized or malformed bracket content - becomes <see cref="TaggedTextSpanKind.PlainText"/>
    ///     spans. Empty for an empty <paramref name="text"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<TaggedTextSpan> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var spans = new List<TaggedTextSpan>();
        var plain = new StringBuilder();
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];
            if (c != '[')
            {
                // Ordinary narration character - accumulate into the running plain-text buffer
                plain.Append(c);
                i++;
                continue;
            }

            // Found a potential tag opener - look for its matching closing bracket
            var closeIndex = text.IndexOf(']', i + 1);
            if (closeIndex < 0)
            {
                // Unbalanced bracket: no closing ']' exists anywhere in the remainder of the
                // text, so nothing here can ever resolve to a tag - pass the rest through as
                // literal text rather than dropping or throwing
                plain.Append(text, i, text.Length - i);
                break;
            }

            var content = text.Substring(i + 1, closeIndex - i - 1);
            if (AudioTagCatalog.TryResolve(content, out var tag, out _))
            {
                // Recognized tag - flush any accumulated plain text as its own span first, so
                // spans stay ordered and adjacent tags never merge into one span
                FlushPlainText(spans, plain);
                spans.Add(TaggedTextSpan.TagSpan(tag));
            }
            else
            {
                // Unknown tag name or empty brackets: malformed/unrecognized bracket content
                // passes through literally, brackets included, per the "never worse than plain
                // narration" guarantee
                plain.Append(text, i, closeIndex - i + 1);
            }

            i = closeIndex + 1;
        }

        FlushPlainText(spans, plain);
        return spans;
    }

    /// <summary>
    ///     Appends the accumulated plain-text buffer as a single <see cref="TaggedTextSpan"/>
    ///     when non-empty, then clears it for reuse.
    /// </summary>
    private static void FlushPlainText(List<TaggedTextSpan> spans, StringBuilder plain)
    {
        if (plain.Length == 0)
        {
            return;
        }

        spans.Add(TaggedTextSpan.PlainText(plain.ToString()));
        plain.Clear();
    }
}
