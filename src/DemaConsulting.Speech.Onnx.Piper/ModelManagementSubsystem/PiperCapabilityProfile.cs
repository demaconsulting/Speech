using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;

/// <summary>
///     The capability profile of the Piper voice: the library's default rendering, with the clause pieces of one sentence
///     put back together so the voice speaks a whole sentence in one utterance, as it was trained to.
/// </summary>
/// <remarks>
///     <para>
///     <b>Why.</b> The core chunker splits every sentence at each comma, semicolon and colon, so every clause would be
///     a separate utterance of the voice. The voice was trained on whole sentences with the commas in the text (the
///     phoneme string keeps the <c>,</c> as a symbol), and it does badly with the very short utterances the clause split
///     makes: a clause of one or two words (<i>coffee,</i>, <i>clocks,</i>) often loses its first sound or its last
///     vowel, and a clause that begins with an <i>r</i> or a weak function word is swallowed more often than the same
///     word inside a sentence (the recognizer proxy hears the first word of a clause 90.6 percent of the time when the
///     clause is its own utterance and 97.5 percent of the time inside its sentence; see
///     <c>docs/evaluation/phonemes-en/renderers.md</c>).
///     </para>
///     <para>
///     <b>How.</b> The default profile renders the plan as always (tags, pauses, parameter overrides, ellipsis pauses,
///     annotations). Then every segment that does not end a sentence (it ends in a clause mark, <c>,</c>, <c>;</c> or <c>:</c>,
///     in a dash, or in a letter or digit because the core chunker had to cut a long clause) is joined to the
///     segment after it, with one space between them and the annotations of the second shifted to their place in the
///     joined text, as long as nothing sets the two apart: no silence between them, the same parameter overrides, and a
///     joined text no longer than <see cref="MaxJoinedLength"/> characters. The core chunker cuts a long clause at a dash,
///     before a conjunction or after a word that is not a function word, and the joining puts such pieces together again
///     up to the budget, so a long sentence is spoken in a few whole-phrase utterances and not in one per clause. The
///     budget is 300 characters, not the core chunker's 200: over eight renderings of the final sentence of the Gettysburg
///     Address (four speakers, two draws each) the recognizer proxy made fewer word errors the longer the utterances were
///     allowed to be (see <c>docs/evaluation/phonemes-en/renderers.md</c>, "Listening Fixes 3"). The engine itself splits what
///     still phonemizes to more than 384 symbols, at the best phrase boundary. Sentence ends, ellipses, tags and
///     pauses stay boundaries, and so does every sentence boundary an analyzer declared: the spans are cut into one
///     group per authoritative sentence first, so a sentence that ends in a clause mark (<c>Consider this:</c>)
///     is never joined to the next sentence. The profile needs nothing of the core library but its public chunking and plan types.
///     </para>
///     <para>The class is stateless and thread-safe.</para>
/// </remarks>
internal sealed class PiperCapabilityProfile : IModelCapabilityProfile
{
    /// <summary>
    ///     The longest joined segment, in characters: the voice speaks a whole sentence of this length in one utterance with
    ///     fewer errors than the same text in shorter ones (see the remarks of the class).
    /// </summary>
    internal const int MaxJoinedLength = 300;

    /// <summary>The marks that end a clause piece the core chunker split at, or a piece cut at a dash.</summary>
    private const string PauseMarks = ",;:\u2012\u2013\u2014\u2015";

    /// <summary>
    ///     Gets the shared instance.
    /// </summary>
    public static PiperCapabilityProfile Instance { get; } = new();

    /// <summary>
    ///     Prevents external construction; the shared <see cref="Instance"/> is used.
    /// </summary>
    private PiperCapabilityProfile()
    {
    }

    /// <inheritdoc/>
    public SpeechPlan Render(IReadOnlyList<TaggedTextSpan> spans, ISpeechModel model)
    {
        ArgumentNullException.ThrowIfNull(spans);
        ArgumentNullException.ThrowIfNull(model);

        // Analyzer sentence breaks are authoritative: clause pieces are joined only within one sentence, so the
        // spans are cut into one group per sentence, each rendered by the default profile on its own.
        var segments = new List<SpeechSegment>();
        foreach (var group in SplitAtSentenceBreaks(spans, model))
        {
            segments.AddRange(JoinClauses(DefaultModelCapabilityProfile.Instance.Render(group, model).Segments));
        }

        return new SpeechPlan(segments);
    }

    /// <summary>
    ///     Cuts the spans into groups at every authoritative analyzer sentence break, so no group holds two sentences.
    /// </summary>
    /// <param name="spans">The tagged spans, with the analysis the session attached.</param>
    /// <param name="model">The model, whose tag support decides whether a tag contributes text.</param>
    /// <returns>
    ///     The groups in order; a single group holding the spans as given when no span carries a sentence break list.
    ///     A group never ends before it holds text, so tags that only set parameters stay with the text they apply to.
    /// </returns>
    private static List<List<TaggedTextSpan>> SplitAtSentenceBreaks(IReadOnlyList<TaggedTextSpan> spans, ISpeechModel model)
    {
        var groups = new List<List<TaggedTextSpan>>();
        var current = new List<TaggedTextSpan>();
        var hasText = false;

        void Close()
        {
            if (hasText)
            {
                groups.Add(current);
                current = [];
                hasText = false;
            }
        }

        foreach (var span in spans)
        {
            if (span.Kind != TaggedTextSpanKind.PlainText)
            {
                var breaks = span.SentenceBreaks;
                if (breaks is not null && breaks.Contains(0))
                {
                    Close();
                }

                current.Add(span with { SentenceBreaks = null });
                hasText |= model.AudioTagSupport == SpeechModelAudioTagSupport.Native;
                if (breaks is not null && breaks.Contains(1))
                {
                    Close();
                }

                continue;
            }

            if (span.Text.Length == 0 || span.SentenceBreaks is null)
            {
                current.Add(span);
                hasText |= span.Text.Length > 0;
                continue;
            }

            var position = 0;
            foreach (var cut in span.SentenceBreaks.Where(b => b <= span.Text.Length).Distinct().OrderBy(b => b))
            {
                if (cut == 0)
                {
                    Close();
                    continue;
                }

                current.Add(Slice(span, position, cut));
                hasText = true;
                position = cut;
                Close();
            }

            if (position < span.Text.Length)
            {
                current.Add(Slice(span, position, span.Text.Length));
                hasText = true;
            }
        }

        if (current.Count > 0)
        {
            groups.Add(current);
        }

        return groups;
    }

    /// <summary>
    ///     Takes the part of a plain-text span from <paramref name="start"/> to <paramref name="end"/>, keeping the
    ///     annotations wholly inside it, re-based, and keeping the span authoritative with no further breaks.
    /// </summary>
    /// <param name="span">The plain-text span, which carries a sentence break list.</param>
    /// <param name="start">The offset the part starts at.</param>
    /// <param name="end">The offset the part ends at.</param>
    /// <returns>The part as its own span.</returns>
    private static TaggedTextSpan Slice(TaggedTextSpan span, int start, int end) =>
        TaggedTextSpan.PlainText(
            span.Text[start..end],
            [.. span.Annotations.Where(a => a.Start >= start && a.End <= end).Select(a => a with { Start = a.Start - start })],
            []);

    /// <summary>
    ///     Joins every segment that ends in a clause mark to the segment after it, where nothing sets the two apart.
    /// </summary>
    /// <param name="segments">The segments of the default plan.</param>
    /// <returns>The segments with the clause pieces of each sentence joined.</returns>
    internal static IReadOnlyList<SpeechSegment> JoinClauses(IReadOnlyList<SpeechSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var joined = new List<SpeechSegment>(segments.Count);
        SpeechSegment? open = null;
        foreach (var segment in segments)
        {
            if (open is not null && CanJoin(open, segment))
            {
                open = Join(open, segment);
                continue;
            }

            if (open is not null)
            {
                joined.Add(open);
            }

            open = segment;
        }

        if (open is not null)
        {
            joined.Add(open);
        }

        return joined;
    }

    /// <summary>
    ///     Tests whether a text ends in a hyphen dash the core chunker cuts at: <c>--</c>, or a single hyphen standing alone
    ///     (a spaced dash) or attached to the word before it.
    /// </summary>
    /// <param name="text">The text of a segment.</param>
    /// <returns><see langword="true"/> when the text ends in a hyphen (the other dashes are among the pause marks).</returns>
    private static bool EndsWithDash(string text) =>
        text.EndsWith('-');

    /// <summary>
    ///     Tests whether a segment that does not end a sentence is joined to the segment after it.
    /// </summary>
    /// <param name="first">The earlier segment.</param>
    /// <param name="second">The segment after it.</param>
    /// <returns><see langword="true"/> when both have text, the first ends in a clause mark, a dash or a word (a cut inside a clause), nothing sets them apart and the joined text fits <see cref="MaxJoinedLength"/>.</returns>
    private static bool CanJoin(SpeechSegment first, SpeechSegment second) =>
        first.Text.Length > 0
        && second.Text.Length > 0
        && (PauseMarks.Contains(first.Text[^1], StringComparison.Ordinal) || EndsWithDash(first.Text) || char.IsLetterOrDigit(first.Text[^1]))
        && first.PostSilenceMs == 0
        && second.PreSilenceMs == 0
        && SameOverrides(first.ParameterOverrides, second.ParameterOverrides)
        && first.Text.Length + 1 + second.Text.Length <= MaxJoinedLength;

    /// <summary>
    ///     Joins two segments: the text with one space between, the annotations of the second moved to their place.
    /// </summary>
    /// <param name="first">The earlier segment.</param>
    /// <param name="second">The segment after it.</param>
    /// <returns>The joined segment, with the silences and overrides of the pair.</returns>
    private static SpeechSegment Join(SpeechSegment first, SpeechSegment second)
    {
        var offset = first.Text.Length + 1;
        List<SegmentAnnotation> annotations = [.. first.Annotations, .. second.Annotations.Select(a => a with { Start = a.Start + offset })];
        return new SpeechSegment(first.Text + " " + second.Text, first.PreSilenceMs, second.PostSilenceMs, first.ParameterOverrides)
        {
            Annotations = annotations,
        };
    }

    /// <summary>
    ///     Tests whether two segments carry the same parameter overrides.
    /// </summary>
    /// <param name="first">The overrides of the earlier segment, or <see langword="null"/>.</param>
    /// <param name="second">The overrides of the later segment, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both are absent, or both have the same keys and values.</returns>
    private static bool SameOverrides(IReadOnlyDictionary<string, object>? first, IReadOnlyDictionary<string, object>? second)
    {
        if (ReferenceEquals(first, second))
        {
            return true;
        }

        if (first is null || second is null || first.Count != second.Count)
        {
            return false;
        }

        return first.All(pair => second.TryGetValue(pair.Key, out var value) && Equals(pair.Value, value));
    }
}
