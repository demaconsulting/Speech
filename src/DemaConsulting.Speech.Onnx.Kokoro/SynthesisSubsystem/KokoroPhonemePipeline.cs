using DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;
using DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;
using DemaConsulting.Speech.Phonemes.En.RenderingSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.Kokoro.SynthesisSubsystem;

/// <summary>
///     Turns the text of one speech segment, with the annotations of the English text analysis when there are any,
///     into the phoneme token ids Kokoro v1.0 expects: phonemization (<see cref="EnglishPhonemizer"/>), rendering as
///     Kokoro's phoneme symbols (<see cref="MisakiRenderer"/>) and conversion through Kokoro's own vocabulary.
/// </summary>
/// <remarks>
///     <para>
///     <b>Long text.</b> Kokoro's graph accepts at most <see cref="KokoroPhonemeVocabulary.MaxPhonemeTokens"/> phoneme
///     tokens per call. The session already splits a request into sentence-sized segments, but a segment can still
///     phonemize to more tokens than that (a long number written out, a long run of words without a break), so this
///     class splits such a segment between words, preferably after a clause mark, and returns one token sequence per
///     part instead of dropping the rest of the text.
///     </para>
///     <para>
///     <b>Function words.</b> <see cref="UseWeakForms"/> chooses whether the small function words are rendered as in
///     running speech (unstressed, some with a reduced vowel) or in their full dictionary form. Kokoro was trained on
///     phonemes in which function words are weak, and rendering them that way measured closer to those phonemes (see
///     <c>docs/evaluation/phonemes-en/kokoro-adoption.md</c>).
///     </para>
///     Instances are immutable and thread-safe.
/// </remarks>
internal sealed class KokoroPhonemePipeline
{
    /// <summary>
    ///     Whether the small function words (the, of, to, and, ...) are rendered in their weak form. This is the single
    ///     switch for that choice; see the type remarks for the reason it is on.
    /// </summary>
    internal const bool UseWeakForms = true;

    /// <summary>
    ///     The token budget a part is planned to, a little under <see cref="KokoroPhonemeVocabulary.MaxPhonemeTokens"/>
    ///     so that the spaces and marks the renderer adds between the planned pieces can never push a part over the limit.
    /// </summary>
    private const int PlannedPartTokens = KokoroPhonemeVocabulary.MaxPhonemeTokens - 10;

    /// <summary>The marks after which a long text is preferably split.</summary>
    private const string ClauseMarks = ",;:.!?—…";

    private readonly KokoroPhonemeVocabulary _vocabulary;
    private readonly EnglishPhonemizer _phonemizer;
    private readonly MisakiRenderer _renderer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="KokoroPhonemePipeline"/> class with the shipped phonemizer and a
    ///     renderer that follows <see cref="UseWeakForms"/>.
    /// </summary>
    /// <param name="vocabulary">Kokoro's phoneme vocabulary. Must not be null.</param>
    public KokoroPhonemePipeline(KokoroPhonemeVocabulary vocabulary)
        : this(vocabulary, new EnglishPhonemizer(), new MisakiRenderer(MisakiProfile.Us, UseWeakForms))
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="KokoroPhonemePipeline"/> class.
    /// </summary>
    /// <param name="vocabulary">Kokoro's phoneme vocabulary. Must not be null.</param>
    /// <param name="phonemizer">The English phonemizer. Must not be null.</param>
    /// <param name="renderer">The renderer for Kokoro's phoneme symbols. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is <see langword="null"/>.</exception>
    public KokoroPhonemePipeline(KokoroPhonemeVocabulary vocabulary, EnglishPhonemizer phonemizer, MisakiRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);
        ArgumentNullException.ThrowIfNull(phonemizer);
        ArgumentNullException.ThrowIfNull(renderer);
        _vocabulary = vocabulary;
        _phonemizer = phonemizer;
        _renderer = renderer;
    }

    /// <summary>
    ///     Converts the text of one segment into token id sequences, one per part of at most
    ///     <see cref="KokoroPhonemeVocabulary.MaxPhonemeTokens"/> ids (never padded; the caller adds the pad tokens).
    /// </summary>
    /// <param name="text">The segment text. Must not be null.</param>
    /// <param name="annotations">
    ///     The segment's annotations (offsets relative to <paramref name="text"/>), or <see langword="null"/> for none:
    ///     the text is then spoken as written, with default readings and no normalization.
    /// </param>
    /// <returns>The token id sequences in speaking order; empty when the text has nothing to say.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<IReadOnlyList<int>> ToTokenIds(string text, IReadOnlyList<SegmentAnnotation>? annotations)
    {
        ArgumentNullException.ThrowIfNull(text);

        var items = _phonemizer.Phonemize(text, annotations).Items;
        var pieces = _renderer.RenderItems(items);
        var parts = new List<IReadOnlyList<int>>();
        var start = 0;
        while (start < items.Count)
        {
            var end = PlanPart(items, pieces, start);
            var ids = _vocabulary.ToTokenIds(_renderer.Render(Slice(items, start, end)));
            if (ids.Count > 0)
            {
                parts.Add(ids);
            }

            start = end;
        }

        return parts;
    }

    /// <summary>
    ///     Chooses where the part that starts at <paramref name="start"/> ends.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="pieces">The rendered piece of each item.</param>
    /// <param name="start">The index of the first item of the part.</param>
    /// <returns>The index after the last item of the part; always greater than <paramref name="start"/>.</returns>
    /// <remarks>
    ///     Takes as many items as fit the planned budget (a space and the piece's tokens each). When that is not all the
    ///     remaining items, the part ends after the last clause mark in its second half if there is one, otherwise after
    ///     the last word that fits.
    /// </remarks>
    private int PlanPart(IReadOnlyList<PhonemizedItem> items, IReadOnlyList<string> pieces, int start)
    {
        var tokens = 0;
        var end = start;
        var lastClauseEnd = -1;
        while (end < items.Count)
        {
            var cost = _vocabulary.ToTokenIds(pieces[end]).Count + 1;
            if (tokens + cost > PlannedPartTokens && end > start)
            {
                break;
            }

            tokens += cost;
            end++;
            if (pieces[end - 1].Length > 0 && ClauseMarks.Contains(pieces[end - 1][^1], StringComparison.Ordinal))
            {
                lastClauseEnd = end;
            }
        }

        if (end == items.Count)
        {
            return end;
        }

        // Prefer a clause boundary when it leaves a reasonably full part
        return lastClauseEnd > start + ((end - start) / 2) ? lastClauseEnd : end;
    }

    /// <summary>
    ///     Returns a slice of the items.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="start">The index of the first item.</param>
    /// <param name="end">The index after the last item.</param>
    /// <returns>The items in <c>[start, end)</c>.</returns>
    private static IReadOnlyList<PhonemizedItem> Slice(IReadOnlyList<PhonemizedItem> items, int start, int end) =>
        start == 0 && end == items.Count ? items : items.Skip(start).Take(end - start).ToList();
}
