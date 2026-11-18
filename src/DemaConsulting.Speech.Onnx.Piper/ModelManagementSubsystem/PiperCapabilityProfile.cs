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
///     annotations). Then every segment that ends in a clause mark (<c>,</c>, <c>;</c> or <c>:</c>) is joined to the
///     segment after it, with one space between them and the annotations of the second shifted to their place in the
///     joined text, as long as nothing sets the two apart: no silence between them, the same parameter overrides, and a
///     joined text no longer than <see cref="SentenceChunker.DefaultMaxChunkLength"/> characters (the budget of the core
///     chunker), so a long sentence still reaches the engine in playback-sized segments. The engine itself splits what
///     still phonemizes to more than 256 symbols, preferably after a clause mark. Sentence ends, ellipses, tags and
///     pauses stay boundaries. The profile needs nothing of the core library but its public chunking and plan types.
///     </para>
///     <para>The class is stateless and thread-safe.</para>
/// </remarks>
internal sealed class PiperCapabilityProfile : IModelCapabilityProfile
{
    /// <summary>The marks that end a clause piece the core chunker split at.</summary>
    private const string ClauseMarks = ",;:";

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

        return new SpeechPlan(JoinClauses(DefaultModelCapabilityProfile.Instance.Render(spans, model).Segments));
    }

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
    ///     Tests whether a segment that ends in a clause mark is joined to the segment after it.
    /// </summary>
    /// <param name="first">The earlier segment.</param>
    /// <param name="second">The segment after it.</param>
    /// <returns><see langword="true"/> when both have text, nothing sets them apart and the joined text fits the chunk budget.</returns>
    private static bool CanJoin(SpeechSegment first, SpeechSegment second) =>
        first.Text.Length > 0
        && second.Text.Length > 0
        && ClauseMarks.Contains(first.Text[^1], StringComparison.Ordinal)
        && first.PostSilenceMs == 0
        && second.PreSilenceMs == 0
        && SameOverrides(first.ParameterOverrides, second.ParameterOverrides)
        && first.Text.Length + 1 + second.Text.Length <= SentenceChunker.DefaultMaxChunkLength;

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
