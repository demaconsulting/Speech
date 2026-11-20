using DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;
using DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;
using DemaConsulting.Speech.Phonemes.En.RenderingSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.Piper.SynthesisSubsystem;

/// <summary>
///     Turns the text of one speech segment, with the annotations of the English text analysis when there are any,
///     into the phoneme-id sequences the Piper graph takes: phonemization (<see cref="EnglishPhonemizer"/>),
///     rendering as espeak-style IPA (<see cref="EspeakIpaRenderer"/>) and conversion through the voice's own symbol
///     table (<see cref="PiperVoiceConfig"/>).
/// </summary>
/// <remarks>
///     <para>
///     <b>Framing.</b> Piper's own encoding: the begin symbol <c>^</c>, then each phoneme symbol followed by the pad
///     symbol <c>_</c>, then the end symbol <c>$</c>.
///     </para>
///     <para>
///     <b>Unknown symbols.</b> A symbol that is not in the voice's table cannot be encoded. It is skipped (as Piper
///     itself does) but never silently: each <see cref="PiperUtterance"/> lists the distinct symbols it lost, and the
///     engine counts them. The renderer is tested to emit only symbols of this voice's table, so a non-empty list is a
///     defect to investigate, for example a host IPA override that uses a symbol the voice does not know.
///     </para>
///     <para>
///     <b>Long text.</b> VITS is trained on sentence-sized input, and the session already splits a request into
///     sentence-sized segments. A segment that still phonemizes to more than <see cref="MaxPartSymbols"/> symbols (a
///     long number written out, a long run of words without a break) is split between words, preferably after a clause
///     mark, and returned as several utterances instead of one very long one; no text is dropped.
///     </para>
///     Instances are immutable and thread-safe.
/// </remarks>
internal sealed class PiperPhonemePipeline
{
    /// <summary>The largest number of phoneme symbols (before the pad symbols are added) in one utterance.</summary>
    internal const int MaxPartSymbols = 384;

    private readonly PiperVoiceConfig _config;
    private readonly EnglishPhonemizer _phonemizer = new();
    private readonly EspeakIpaRenderer _renderer = new(EspeakIpaProfile.EnUs);

    /// <summary>
    ///     Initializes a new instance of the <see cref="PiperPhonemePipeline"/> class with the shipped phonemizer and the
    ///     renderer for the voice's US English phoneme inventory.
    /// </summary>
    /// <param name="config">The voice configuration. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is <see langword="null"/>.</exception>
    public PiperPhonemePipeline(PiperVoiceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    /// <summary>
    ///     Converts the text of one segment into utterances, one per part of at most <see cref="MaxPartSymbols"/> symbols.
    /// </summary>
    /// <param name="text">The segment text. Must not be null.</param>
    /// <param name="annotations">
    ///     The segment's annotations (offsets relative to <paramref name="text"/>), or <see langword="null"/> for none:
    ///     the text is then spoken as written, with default readings and no normalization.
    /// </param>
    /// <returns>The utterances in speaking order; empty when the text has nothing to say.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<PiperUtterance> ToUtterances(string text, IReadOnlyList<SegmentAnnotation>? annotations)
    {
        ArgumentNullException.ThrowIfNull(text);

        // The renderer splits a long text between words into parts of at most MaxPartSymbols symbols
        var utterances = new List<PiperUtterance>();
        foreach (var phonemes in _renderer.RenderParts(_phonemizer.Phonemize(text, annotations), MaxPartSymbols))
        {
            var utterance = Encode(phonemes);
            if (utterance.SymbolCount > 0)
            {
                utterances.Add(utterance);
            }
        }

        return utterances;
    }

    /// <summary>
    ///     Encodes a phoneme string with Piper's framing.
    /// </summary>
    /// <remarks>
    ///     The framing is exactly Piper's own (<c>phonemes_to_ids</c>): the begin symbol and a pad, then each symbol
    ///     followed by a pad, then the end symbol. The pad after the begin symbol matters: without it the first sound
    ///     of every utterance follows the begin symbol directly, an input the voice never saw in training, and a
    ///     sentence-initial stop such as the t of <i>Top</i> or <i>Testing</i> loses most of its burst.
    /// </remarks>
    /// <param name="phonemes">The IPA phoneme string. Must not be null.</param>
    /// <returns>The utterance: the framed ids and the distinct symbols the voice's table does not contain.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="phonemes"/> is <see langword="null"/>.</exception>
    public PiperUtterance Encode(string phonemes)
    {
        ArgumentNullException.ThrowIfNull(phonemes);

        List<long> ids = [.. _config.BeginIds, .. _config.PadIds];
        List<string> unknown = [];
        var symbolCount = 0;
        foreach (var rune in phonemes.EnumerateRunes())
        {
            var symbol = rune.ToString();
            if (_config.TryGetIds(symbol, out var symbolIds))
            {
                ids.AddRange(symbolIds);
                ids.AddRange(_config.PadIds);
                symbolCount++;
            }
            else if (!unknown.Contains(symbol))
            {
                unknown.Add(symbol);
            }
        }

        ids.AddRange(_config.EndIds);
        return new PiperUtterance(phonemes, ids, symbolCount, unknown);
    }
}

/// <summary>
///     One utterance for the Piper graph: the phoneme string and its framed ids.
/// </summary>
/// <param name="Phonemes">The IPA phoneme string the ids were made from.</param>
/// <param name="Ids">The framed model input ids: begin and pad, each known symbol followed by pad, end.</param>
/// <param name="SymbolCount">The number of symbols that were encoded (the symbols that are not pad or framing).</param>
/// <param name="UnknownSymbols">The distinct symbols the voice's table does not contain, in first-seen order; they are not in <paramref name="Ids"/>.</param>
internal sealed record PiperUtterance(
    string Phonemes,
    IReadOnlyList<long> Ids,
    int SymbolCount,
    IReadOnlyList<string> UnknownSymbols);
