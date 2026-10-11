using System.Text;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;

/// <summary>
///     Renders library audio tags for Chatterbox Turbo: native paralinguistic tokens inline where they are
///     reliable, real silence where the model has no usable token, and stripping elsewhere.
/// </summary>
/// <remarks>
///     <b>Why a bespoke profile</b>: the default native-tag profile would emit the library's canonical text
///     (for example <c>[laughs]</c>, which is not a model token) and may split a tag onto its own synthesis
///     unit, leaving it without speech after it - the model only produces its sounds reliably when speech
///     follows the tag. This profile therefore maps each tag through <see cref="Treatments"/> to the model's
///     exact token (<c>[laugh]</c>, <c>[clear throat]</c>, <c>[gasp]</c>), keeps a tagged sentence in ONE
///     synthesis unit, merges a tag-only unit into the next unit as a leading tag, and relocates a
///     sentence-final tag according to <see cref="ChatterboxTurboEndOfSentenceTagPolicy"/>.
///     <para>
///     No library tag ever maps to the ten emotion/style tokens (<c>[angry]</c> ...): they have no audible
///     effect on this model. <c>Sighs</c> becomes silence because <c>[sigh]</c> is unsupported and
///     <c>[sigh] ...</c> triggers runaway generation; a bare dash between sentences is never produced for the
///     same reason. Sentence splitting is local to this package because the core chunker is internal. Stateless
///     apart from the configured policy and thread-safe.
///     </para>
/// </remarks>
internal sealed class ChatterboxTurboCapabilityProfile : IModelCapabilityProfile
{
    /// <summary>The silence length of a short pause, matching the default profile.</summary>
    private const int ShortPauseMilliseconds = 300;

    /// <summary>The silence length of a long pause, matching the default profile.</summary>
    private const int LongPauseMilliseconds = 900;

    /// <summary>The silence after a unit that ends in an ellipsis, matching the default profile.</summary>
    private const int EllipsisPauseMilliseconds = 500;

    /// <summary>The silence standing in for a sigh.</summary>
    private const int SighMilliseconds = 300;

    /// <summary>The longest synthesis unit before it is split at secondary boundaries or word budget.</summary>
    private const int MaxUnitLength = 200;

    /// <summary>The first private-use code point used as a tag placeholder.</summary>
    private const char PlaceholderBase = '\uE000';

    /// <summary>The per-tag table: every catalog tag has exactly one row.</summary>
    internal static readonly IReadOnlyDictionary<NaturalLanguageAudioTag, ChatterboxTurboTagTreatment> Treatments = BuildTreatments();

    /// <summary>Distinct native tokens, index = placeholder offset.</summary>
    private static readonly string[] NativeTokens =
        [.. Treatments.Values.Where(t => t.Kind == ChatterboxTurboTagKind.Native).Select(t => t.Token!).Distinct()];

    /// <summary>The configured sentence-final tag policy.</summary>
    private readonly ChatterboxTurboEndOfSentenceTagPolicy _policy;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatterboxTurboCapabilityProfile"/> class.
    /// </summary>
    /// <param name="policy">How sentence-final tags are rendered.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="policy"/> is not a defined value.</exception>
    public ChatterboxTurboCapabilityProfile(ChatterboxTurboEndOfSentenceTagPolicy policy)
    {
        if (!Enum.IsDefined(policy))
        {
            throw new ArgumentOutOfRangeException(nameof(policy));
        }

        _policy = policy;
    }

    /// <inheritdoc/>
    public SpeechPlan Render(IReadOnlyList<TaggedTextSpan> spans, ISpeechModel model)
    {
        ArgumentNullException.ThrowIfNull(spans);
        ArgumentNullException.ThrowIfNull(model);

        var segments = new List<SpeechSegment>();
        var buffer = new StringBuilder();

        void Flush()
        {
            if (buffer.Length == 0)
            {
                return;
            }

            foreach (var (text, endsWithEllipsis) in BuildUnits(buffer.ToString()))
            {
                segments.Add(new SpeechSegment(text, 0, endsWithEllipsis ? EllipsisPauseMilliseconds : 0, null));
            }

            buffer.Clear();
        }

        foreach (var span in spans)
        {
            // Plain text passes through, minus any private-use characters that would collide with placeholders
            if (span.Kind == TaggedTextSpanKind.PlainText)
            {
                foreach (var c in span.Text.Where(c => !IsPlaceholder(c)))
                {
                    buffer.Append(c);
                }

                continue;
            }

            var treatment = Treatments[span.Tag!.Value];
            switch (treatment.Kind)
            {
                case ChatterboxTurboTagKind.Native:
                    buffer.Append(PlaceholderFor(treatment.Token!));
                    break;

                case ChatterboxTurboTagKind.Pause:
                    Flush();
                    segments.Add(new SpeechSegment(string.Empty, 0, treatment.PauseMilliseconds, null));
                    break;

                case ChatterboxTurboTagKind.Strip:
                default:
                    // Stripped: the surrounding words are still spoken
                    break;
            }
        }

        Flush();
        return new SpeechPlan(segments);
    }

    /// <summary>Builds the per-tag table.</summary>
    private static Dictionary<NaturalLanguageAudioTag, ChatterboxTurboTagTreatment> BuildTreatments()
    {
        // Default every tag to Strip, then override the supported ones
        var table = Enum.GetValues<NaturalLanguageAudioTag>()
            .ToDictionary(tag => tag, _ => new ChatterboxTurboTagTreatment(ChatterboxTurboTagKind.Strip, null, 0, null));
        table[NaturalLanguageAudioTag.Laughs] = new(ChatterboxTurboTagKind.Native, "[laugh]", 0, " ...");
        table[NaturalLanguageAudioTag.ClearsThroat] = new(ChatterboxTurboTagKind.Native, "[clear throat]", 0, null);
        table[NaturalLanguageAudioTag.Gasp] = new(ChatterboxTurboTagKind.Native, "[gasp]", 0, null);
        table[NaturalLanguageAudioTag.Sighs] = new(ChatterboxTurboTagKind.Pause, null, SighMilliseconds, null);
        table[NaturalLanguageAudioTag.ShortPause] = new(ChatterboxTurboTagKind.Pause, null, ShortPauseMilliseconds, null);
        table[NaturalLanguageAudioTag.LongPause] = new(ChatterboxTurboTagKind.Pause, null, LongPauseMilliseconds, null);
        return table;
    }

    /// <summary>Returns the placeholder character standing for a native token.</summary>
    private static char PlaceholderFor(string token) => (char)(PlaceholderBase + Array.IndexOf(NativeTokens, token));

    /// <summary>Returns true for a placeholder character.</summary>
    private static bool IsPlaceholder(char c) => c >= PlaceholderBase && c < PlaceholderBase + NativeTokens.Length;

    /// <summary>Returns the native token for a placeholder character.</summary>
    private static string TokenFor(char placeholder) => NativeTokens[placeholder - PlaceholderBase];

    /// <summary>Replaces every placeholder with its token text.</summary>
    private static string Substitute(string text)
    {
        var builder = new StringBuilder(text.Length + 16);
        foreach (var c in text)
        {
            builder.Append(IsPlaceholder(c) ? TokenFor(c) : c.ToString());
        }

        return builder.ToString();
    }

    /// <summary>Removes every placeholder.</summary>
    private static string RemovePlaceholders(string text) => new([.. text.Where(c => !IsPlaceholder(c))]);

    /// <summary>Returns true when the text holds a letter or digit.</summary>
    private static bool HasWord(string text) => text.Any(char.IsLetterOrDigit);

    /// <summary>Splits tagged text into synthesis units with tags placed per policy.</summary>
    private IEnumerable<(string Text, bool EndsWithEllipsis)> BuildUnits(string text)
    {
        // Merge tag-only chunks into the next chunk as leading tags (or the previous as trailing)
        var units = new List<(string Text, bool Ellipsis)>();
        var carry = string.Empty;
        foreach (var (chunk, ellipsis) in Chunk(text))
        {
            if (!HasWord(chunk))
            {
                carry += new string([.. chunk.Where(IsPlaceholder)]);
                continue;
            }

            units.Add((carry.Length > 0 ? carry + " " + chunk : chunk, ellipsis));
            carry = string.Empty;
        }

        if (carry.Length > 0 && units.Count > 0)
        {
            var last = units[^1];
            units[^1] = (last.Text + carry, last.Ellipsis);
        }

        foreach (var (unit, ellipsis) in units)
        {
            var rendered = ApplyEndOfSentencePolicy(unit).Trim();
            if (rendered.Length > 0)
            {
                yield return (rendered, ellipsis);
            }
        }
    }

    /// <summary>Places tags that have no speech after them according to the configured policy.</summary>
    private string ApplyEndOfSentencePolicy(string unit)
    {
        // Find the last word; placeholders after it are sentence-final tags
        var lastWord = -1;
        for (var i = unit.Length - 1; i >= 0; i--)
        {
            if (char.IsLetterOrDigit(unit[i]))
            {
                lastWord = i;
                break;
            }
        }

        var tail = unit[(lastWord + 1)..];
        var trailingTags = tail.Where(IsPlaceholder).ToArray();
        if (trailingTags.Length == 0)
        {
            return Substitute(unit);
        }

        var head = unit[..(lastWord + 1)];
        var body = head + RemovePlaceholders(tail);

        // Preferred: move the tags before the final word so speech follows them
        if (_policy == ChatterboxTurboEndOfSentenceTagPolicy.MoveBeforeFinalWord)
        {
            var wordStart = lastWord;
            while (wordStart > 0 && !char.IsWhiteSpace(unit[wordStart - 1]) && !IsPlaceholder(unit[wordStart - 1]))
            {
                wordStart--;
            }

            // Needs a word before the final one, otherwise the tag would lead the whole sentence anyway
            if (HasWord(unit[..wordStart]))
            {
                var prefix = unit[..wordStart];
                var tokens = string.Join(' ', trailingTags.Select(TokenFor));
                var separator = prefix.Length > 0 && !char.IsWhiteSpace(prefix[^1]) ? " " : string.Empty;
                return Substitute(prefix) + separator + tokens + " " + Substitute(body[wordStart..]);
            }
        }

        // Fallback: a per-tag suffix where one is known to work; everything else is stripped
        if (_policy != ChatterboxTurboEndOfSentenceTagPolicy.Strip)
        {
            var suffixed = new StringBuilder(Substitute(body));
            foreach (var tag in trailingTags)
            {
                var treatment = Treatments.Values.First(t => t.Token == TokenFor(tag));
                if (treatment.EndOfSentenceSuffix is not null)
                {
                    suffixed.Append(' ').Append(treatment.Token).Append(treatment.EndOfSentenceSuffix);
                }
            }

            return suffixed.ToString();
        }

        return Substitute(body);
    }

    /// <summary>Splits text into sentence-like units, reporting which end in an ellipsis.</summary>
    private static List<(string Text, bool Ellipsis)> Chunk(string text)
    {
        var trimmed = text.Trim();
        var pieces = new List<string>();
        if (trimmed.Length > 0)
        {
            foreach (var sentence in SplitOnBoundaries(trimmed, ".!?", mergeConsecutive: true))
            {
                pieces.AddRange(sentence.Length <= MaxUnitLength ? [sentence] : SplitLong(sentence));
            }
        }

        // Pieces with no word and no tag (bare punctuation) join the previous piece or are dropped
        var merged = new List<string>();
        foreach (var piece in pieces)
        {
            if (HasWord(piece) || piece.Any(IsPlaceholder))
            {
                merged.Add(piece);
            }
            else if (merged.Count > 0)
            {
                merged[^1] = merged[^1] + " " + piece;
            }
        }

        return [.. merged.Select(piece => (piece, EndsWithEllipsis(piece)))];
    }

    /// <summary>Splits an over-long sentence at secondary boundaries, then by word budget.</summary>
    private static List<string> SplitLong(string sentence)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        foreach (var clause in SplitOnBoundaries(sentence, ",;:", mergeConsecutive: false))
        {
            if (current.Length > 0 && current.Length + 1 + clause.Length > MaxUnitLength)
            {
                result.Add(current.ToString());
                current.Clear();
            }

            current.Append(current.Length > 0 ? " " : string.Empty).Append(clause);
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        // A single clause can still exceed the limit: fall back to a whitespace word budget
        return [.. result.SelectMany(piece => piece.Length <= MaxUnitLength ? [piece] : SplitOnWordBudget(piece))];
    }

    /// <summary>Greedily packs whole words into pieces no longer than the unit limit.</summary>
    private static List<string> SplitOnWordBudget(string text)
    {
        var pieces = new List<string>();
        var current = new StringBuilder();
        foreach (var word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.Length > 0 && current.Length + 1 + word.Length > MaxUnitLength)
            {
                pieces.Add(current.ToString());
                current.Clear();
            }

            current.Append(current.Length > 0 ? " " : string.Empty).Append(word);
        }

        if (current.Length > 0)
        {
            pieces.Add(current.ToString());
        }

        return pieces;
    }

    /// <summary>Splits after each boundary character, keeping the boundary with the preceding piece.</summary>
    private static List<string> SplitOnBoundaries(string text, string boundaries, bool mergeConsecutive)
    {
        var pieces = new List<string>();
        var start = 0;
        var i = 0;
        while (i < text.Length)
        {
            // A boundary flanked by digits (12:30, 1,000, 3.14) belongs to the numeral
            if (!boundaries.Contains(text[i]) || IsDigitFlanked(text, i))
            {
                i++;
                continue;
            }

            var end = i;
            while (mergeConsecutive && end + 1 < text.Length && boundaries.Contains(text[end + 1]))
            {
                end++;
            }

            var piece = text[start..(end + 1)].Trim();
            if (piece.Length > 0)
            {
                pieces.Add(piece);
            }

            start = end + 1;
            i = end + 1;
        }

        var remainder = text[start..].Trim();
        if (remainder.Length > 0)
        {
            pieces.Add(remainder);
        }

        return pieces;
    }

    /// <summary>Returns true when the character at <paramref name="index"/> sits between two digits.</summary>
    private static bool IsDigitFlanked(string text, int index) =>
        index > 0 && index + 1 < text.Length && char.IsDigit(text[index - 1]) && char.IsDigit(text[index + 1]);

    /// <summary>Returns true when the text ends in three or more dots (ignoring trailing whitespace).</summary>
    private static bool EndsWithEllipsis(string text)
    {
        var dots = 0;
        for (var i = text.Length - 1; i >= 0; i--)
        {
            if (text[i] == '.')
            {
                dots++;
            }
            else if (!char.IsWhiteSpace(text[i]))
            {
                break;
            }
        }

        return dots >= 3;
    }
}
