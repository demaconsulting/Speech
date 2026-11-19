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

    private readonly KokoroPhonemeVocabulary _vocabulary;
    private readonly EnglishPhonemizer _phonemizer = new();
    private readonly MisakiRenderer _renderer = new(MisakiProfile.Us, UseWeakForms);

    /// <summary>
    ///     Initializes a new instance of the <see cref="KokoroPhonemePipeline"/> class with the shipped phonemizer and a
    ///     renderer that follows <see cref="UseWeakForms"/>.
    /// </summary>
    /// <param name="vocabulary">Kokoro's phoneme vocabulary. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="vocabulary"/> is <see langword="null"/>.</exception>
    public KokoroPhonemePipeline(KokoroPhonemeVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);
        _vocabulary = vocabulary;
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

        // The renderer splits a long text between words; every symbol of its output is one token or none (an
        // unknown symbol), so a part never has more tokens than symbols
        var parts = new List<IReadOnlyList<int>>();
        foreach (var phonemes in _renderer.RenderParts(_phonemizer.Phonemize(text, annotations), KokoroPhonemeVocabulary.MaxPhonemeTokens))
        {
            var ids = _vocabulary.ToTokenIds(phonemes);
            if (ids.Count > 0)
            {
                parts.Add(ids);
            }
        }

        return parts;
    }
}
