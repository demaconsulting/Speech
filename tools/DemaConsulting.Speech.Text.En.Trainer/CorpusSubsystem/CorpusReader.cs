using System.Runtime.ExceptionServices;
using System.Text;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.CorpusSubsystem;

/// <summary>One paragraph of a tokenized book: its text and the token spans of each of its sentences.</summary>
/// <param name="Text">The paragraph text: the paragraph's lines joined by single spaces.</param>
/// <param name="Sentences">Per sentence, the token spans as flat <c>start, length</c> pairs indexing <paramref name="Text"/>.</param>
internal sealed record CorpusParagraph(string Text, IReadOnlyList<int[]> Sentences)
{
    /// <summary>
    ///     Gets the text of a sentence: from its first token's start to its last token's end.
    /// </summary>
    /// <param name="sentence">The 0-based sentence index.</param>
    /// <returns>The sentence text.</returns>
    public string SentenceText(int sentence)
    {
        var spans = Sentences[sentence];
        var start = spans[0];
        var end = spans[^2] + spans[^1];
        return Text[start..end];
    }
}

/// <summary>One tokenized book of the corpus.</summary>
/// <param name="SourceId">The source id.</param>
/// <param name="Paragraphs">The paragraphs in body order.</param>
internal sealed record CorpusBook(string SourceId, IReadOnlyList<CorpusParagraph> Paragraphs);

/// <summary>The tokenized corpus: every used book split into paragraphs, sentences and tokens.</summary>
/// <param name="Books">The books in ordinal source-id order.</param>
internal sealed record TokenizedCorpus(IReadOnlyList<CorpusBook> Books)
{
    /// <summary>Gets the total number of tokens.</summary>
    public long Tokens => Books.Sum(static book => book.Paragraphs.Sum(static paragraph => paragraph.Sentences.Sum(static sentence => (long)sentence.Length / 2)));
}

/// <summary>
///     Reads the normalized corpus into books, paragraphs, sentences and tokens with the runtime analyzer's own tokenizer,
///     so the corpus is compared with evaluation sentences, and later used, on the analyzer's terms.
/// </summary>
/// <remarks>
///     <para>
///     Each body is split into paragraphs on blank lines (lines joined by single spaces), and each paragraph is split into
///     sentences and tokens by the analyzer's own model-free tokenizer. <see cref="Normalize"/> applies the runtime's rule
///     to a token (curly single quotes become the apostrophe, ASCII capitals become lower case, a token with a digit and no
///     letter becomes <c>&lt;num&gt;</c>), and <see cref="NormalizeEligible"/> also requires a form of at most 40
///     characters, all in U+0021 to U+007E, U+00A1 to U+00FF or U+2010 to U+2027.
///     </para>
///     <para>
///     Tokenization is deterministic for any thread count: books are tokenized in parallel into separate slots and
///     returned in the order of the input. Stateless and thread-safe.
///     </para>
/// </remarks>
internal static class CorpusReader
{
    /// <summary>The longest eligible normalized form, in UTF-16 code units.</summary>
    public const int MaximumFormLength = 40;

    /// <summary>The normalized form of a token with a digit and no letter (the runtime's rule).</summary>
    public const string NumberForm = "<num>";

    /// <summary>
    ///     Tokenizes the bodies of the corpus.
    /// </summary>
    /// <param name="bodies">The verified bodies in ordinal source-id order.</param>
    /// <param name="threads">The number of worker threads; at least 1. The result does not depend on it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tokenized corpus in the order of <paramref name="bodies"/>.</returns>
    public static TokenizedCorpus Tokenize(IReadOnlyList<(string SourceId, string Text)> bodies, int threads, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentOutOfRangeException.ThrowIfLessThan(threads, 1);
        var books = new CorpusBook[bodies.Count];
        ForEachIndex(bodies.Count, threads, cancellationToken, index => books[index] = TokenizeBook(bodies[index].SourceId, bodies[index].Text, cancellationToken));
        return new TokenizedCorpus(books);
    }

    /// <summary>
    ///     Splits a body into paragraphs.
    /// </summary>
    /// <param name="body">The normalized body (LF line endings).</param>
    /// <returns>The paragraph texts in body order, each with its lines joined by single spaces.</returns>
    public static IReadOnlyList<string> Paragraphs(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var result = new List<string>();
        var lines = new List<string>();
        foreach (var line in body.Split('\n'))
        {
            if (line.Length == 0)
            {
                if (lines.Count > 0)
                {
                    result.Add(string.Join(' ', lines));
                    lines.Clear();
                }

                continue;
            }

            lines.Add(line);
        }

        if (lines.Count > 0)
        {
            result.Add(string.Join(' ', lines));
        }

        return result;
    }

    /// <summary>
    ///     Normalizes one token with the runtime's rule and decides whether it may enter the vocabulary.
    /// </summary>
    /// <param name="token">The token text.</param>
    /// <returns>The normalized form, or null when the form is not eligible.</returns>
    public static string? NormalizeEligible(ReadOnlySpan<char> token)
    {
        var form = Normalize(token);
        return form.Length <= MaximumFormLength && form.All(IsEligible) ? form : null;
    }

    /// <summary>
    ///     Normalizes one token with the runtime's rule.
    /// </summary>
    /// <param name="token">The token text.</param>
    /// <returns><c>&lt;num&gt;</c> for a token with a digit and no letter; otherwise the token with curly single quotes as apostrophes and ASCII capitals lower-cased.</returns>
    public static string Normalize(ReadOnlySpan<char> token)
    {
        var hasLetter = false;
        var hasDigit = false;
        foreach (var rune in token.EnumerateRunes())
        {
            hasLetter |= Rune.IsLetter(rune);
            hasDigit |= Rune.IsDigit(rune);
        }

        if (hasDigit && !hasLetter)
        {
            return NumberForm;
        }

        return string.Create(token.Length, token, static (destination, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var unit = source[i];
                destination[i] = unit is '\u2018' or '\u2019' ? '\'' : FeatureHashing.FoldAscii(unit);
            }
        });
    }

    /// <summary>
    ///     Determines whether a character may appear in a table word.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for U+0021 to U+007E, U+00A1 to U+00FF and U+2010 to U+2027.</returns>
    public static bool IsEligible(char c) => c is (>= '\u0021' and <= '\u007e') or (>= '\u00a1' and <= '\u00ff') or (>= '\u2010' and <= '\u2027');

    /// <summary>
    ///     Runs a loop body for every index on up to the given number of worker threads.
    /// </summary>
    /// <remarks>
    ///     Every body writes only its own slot, so the result does not depend on the thread count. A failure inside the
    ///     loop is rethrown as itself rather than wrapped, so callers see the documented exception types.
    /// </remarks>
    /// <param name="count">The number of indexes.</param>
    /// <param name="threads">The number of worker threads; at least 1.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="body">The body.</param>
    internal static void ForEachIndex(int count, int threads, CancellationToken cancellationToken, Action<int> body)
    {
        try
        {
            Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = threads, CancellationToken = cancellationToken }, body);
        }
        catch (AggregateException exception) when (exception.InnerExceptions.Count > 0)
        {
            ExceptionDispatchInfo.Capture(exception.InnerExceptions[0]).Throw();
        }
    }

    /// <summary>
    ///     Tokenizes one book.
    /// </summary>
    /// <param name="sourceId">The source id.</param>
    /// <param name="body">The body.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tokenized book.</returns>
    private static CorpusBook TokenizeBook(string sourceId, string body, CancellationToken cancellationToken)
    {
        var paragraphs = new List<CorpusParagraph>();
        foreach (var text in Paragraphs(body))
        {
            var sentences = PartOfSpeechAdapter.Shared.Analyze(text, false, cancellationToken);
            var spans = new List<int[]>(sentences.Count);
            foreach (var sentence in sentences)
            {
                var flat = new int[sentence.Count * 2];
                for (var i = 0; i < sentence.Count; i++)
                {
                    flat[2 * i] = sentence[i].Start;
                    flat[(2 * i) + 1] = sentence[i].Length;
                }

                spans.Add(flat);
            }

            paragraphs.Add(new CorpusParagraph(text, spans));
        }

        return new CorpusBook(sourceId, paragraphs);
    }
}
