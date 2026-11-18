using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;
using DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.RenderingSubsystem;

/// <summary>
///     Renders a <see cref="PhonemizedText"/> as the phoneme string the Kokoro model expects: its misaki-style
///     symbols, in which the common diphthongs are single capital letters (<c>A</c> for <c>eɪ</c>, <c>I</c> for
///     <c>aɪ</c>, <c>O</c> for <c>oʊ</c>, <c>W</c> for <c>aʊ</c>, <c>Y</c> for <c>ɔɪ</c>) and the affricates are
///     <c>ʧ</c> and <c>ʤ</c>.
/// </summary>
/// <remarks>
///     <para>
///     The symbol table is authored from phonetics and from the character vocabulary of Kokoro's own tokenizer; the
///     committed coverage tests prove that every pronunciation of the dictionary renders to characters of that
///     vocabulary. The measured agreement with the reference misaki G2P is reported in
///     <c>docs/evaluation/phonemes-en/renderers.md</c>.
///     </para>
///     <para>
///     By default words are rendered in their full dictionary form, with the stress the dictionary gives them, as
///     the owner decided for Kokoro. With <c>useWeakForms: true</c> the small function words are rendered as in running
///     speech (unstressed, some with a reduced vowel; see <c>WeakForms</c>), which is closer to the text the reference
///     phonemizer produced for the model. A word the host pronounced with an IPA override is translated into
///     Kokoro's symbols (see <c>IpaSymbols</c>); an override that cannot be represented completely in the model's
///     vocabulary is not approximated, the word's normal pronunciation is spoken instead. Instances are immutable and
///     thread-safe.
///     </para>
/// </remarks>
public sealed class MisakiRenderer
{
    /// <summary>The longest run of one clause mark that is rendered.</summary>
    private const int MaxMarkRun = 3;

    /// <summary>The clause marks that pass through to the model.</summary>
    private const string ClauseMarks = ",;:.!?";

    /// <summary>The marks that open a phrase (no space after them).</summary>
    private const string OpeningMarks = "“(";

    /// <summary>The marks that stand between spaces (a dash).</summary>
    private const string StandaloneMarks = "—";

    /// <summary>Whether function words are rendered in their weak form.</summary>
    private readonly bool _useWeakForms;

    /// <summary>
    ///     Initializes a renderer for the default profile (<see cref="MisakiProfile.Us"/>) with full word forms.
    /// </summary>
    public MisakiRenderer()
        : this(MisakiProfile.Us)
    {
    }

    /// <summary>
    ///     Initializes a renderer.
    /// </summary>
    /// <param name="profile">The dialect profile.</param>
    /// <param name="useWeakForms">Whether the small function words are rendered in their weak (unstressed, reduced) form; the default is the full dictionary form.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="profile"/> is not a defined profile.</exception>
    public MisakiRenderer(MisakiProfile profile, bool useWeakForms = false)
    {
        if (!Enum.IsDefined(profile))
        {
            throw new ArgumentOutOfRangeException(nameof(profile), profile, "The profile is not defined.");
        }

        _useWeakForms = useWeakForms;
    }

    /// <summary>
    ///     Renders a phonemized text.
    /// </summary>
    /// <param name="text">The phonemized text.</param>
    /// <returns>The phoneme string: words separated by a space, punctuation attached to the word before it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public string Render(PhonemizedText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Render(text.Items);
    }

    /// <summary>
    ///     Renders a phonemized text as parts that each stay within a symbol limit, for a voice model that accepts only
    ///     so much per call.
    /// </summary>
    /// <param name="text">The phonemized text.</param>
    /// <param name="maxSymbols">The most symbols (Unicode scalar values, spaces included) a part may have; at least 16.</param>
    /// <returns>
    ///     The phoneme strings of the parts in speaking order, each rendered exactly as <see cref="Render(PhonemizedText)"/>
    ///     would render that stretch of text; empty when the text has no sound. The text is split between words,
    ///     preferably after a clause mark, so no text is dropped; only a single word longer than the limit exceeds it.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxSymbols"/> is less than 16.</exception>
    public IReadOnlyList<string> RenderParts(PhonemizedText text, int maxSymbols)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSymbols, PhonemeStream.MinPartSymbols);
        return PhonemeStream.Split(text.Items, RenderItems(text.Items), maxSymbols, Render);
    }

    /// <summary>
    ///     Renders a sequence of items.
    /// </summary>
    /// <param name="items">The items, for example a slice of <see cref="PhonemizedText.Items"/> (a chunk of a long text).</param>
    /// <returns>The phoneme string.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
    internal string Render(IReadOnlyList<PhonemizedItem> items) => PhonemeStream.Join(RenderItems(items), items, OpeningMarks, StandaloneMarks);

    /// <summary>
    ///     Renders every item on its own, so a long text can be split between items.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <returns>
    ///     One string per item: the phonemes of a word, the mark a punctuation item becomes, or an empty string for an
    ///     item that has no sound in this voice (an apostrophe, an unsupported token).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
    internal IReadOnlyList<string> RenderItems(IReadOnlyList<PhonemizedItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var pieces = new string[items.Count];
        var quoteOpen = false;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            switch (item.Kind)
            {
                case PhonemizedItemKind.Word:
                    pieces[i] = RenderWord(item, PhonemeStream.StartsWithVowel(items, i + 1));
                    break;
                case PhonemizedItemKind.Punctuation:
                    pieces[i] = RenderMark(item.Text, ref quoteOpen);
                    break;
                default:
                    pieces[i] = string.Empty;
                    break;
            }
        }

        return pieces;
    }

    /// <summary>
    ///     Renders one pronunciation on its own.
    /// </summary>
    /// <param name="pronunciation">The pronunciation.</param>
    /// <returns>The symbols.</returns>
    internal static string RenderPronunciation(Pronunciation pronunciation)
    {
        ArgumentNullException.ThrowIfNull(pronunciation);
        return MisakiWordRules.Render(pronunciation.Phones);
    }

    /// <summary>
    ///     Renders a word item: an IPA override translated to Kokoro's symbols (or the normal pronunciation when it cannot be), a function word in its weak form when that is enabled, any other word from its phones.
    /// </summary>
    /// <param name="item">The word item.</param>
    /// <param name="nextStartsWithVowel">Whether the next word starts with a vowel.</param>
    /// <returns>The symbols.</returns>
    private string RenderWord(PhonemizedItem item, bool nextStartsWithVowel)
    {
        if (item.IpaOverride is not null)
        {
            // An override the voice cannot represent completely is not approximated: the word's normal pronunciation is spoken instead
            var translated = IpaSymbols.ToKokoro(item.IpaOverride);
            if (translated is not null)
            {
                return translated;
            }

            // A word with no normal pronunciation either keeps what can be represented of the override
            if (item.Pronunciation.Count == 0)
            {
                return IpaSymbols.ToKokoroLossy(item.IpaOverride);
            }
        }

        var demoted = false;
        var weak = string.Empty;
        var known = _useWeakForms && item.Source == PronunciationSource.Lexicon && WeakForms.TryGet(item.Text, nextStartsWithVowel, nextWord: null, espeakStyle: false, out weak, out demoted);
        if (known && weak.Length > 0)
        {
            return ToMisaki(weak);
        }

        // Misaki stresses only the first part of a hyphenated compound: the primary stresses of the later parts become secondary
        var phones = item.Pronunciation.Phones;
        if (item.CompoundBreak > 0)
        {
            phones = DemoteFrom(phones, item.CompoundBreak);
        }

        return MisakiWordRules.Render(phones, demoted);
    }

    /// <summary>
    ///     Lowers every primary stress from a phone on to secondary stress.
    /// </summary>
    /// <param name="phones">The phones of a word.</param>
    /// <param name="start">The index of the first phone to change.</param>
    /// <returns>The phones with no primary stress from <paramref name="start"/> on.</returns>
    private static ReadOnlySpan<Phone> DemoteFrom(ReadOnlySpan<Phone> phones, int start)
    {
        var changed = phones.ToArray();
        for (var i = start; i < changed.Length; i++)
        {
            if (changed[i].Stress == 1)
            {
                changed[i] = changed[i].WithStress(2);
            }
        }

        return changed;
    }

    /// <summary>
    ///     Converts an espeak-style IPA form of the weak-form table to misaki symbols.
    /// </summary>
    /// <param name="ipa">The IPA form.</param>
    /// <returns>The symbols.</returns>
    private static string ToMisaki(string ipa) => (ipa == "ðɪ" ? "ði" : ipa)
        .Replace("tʃ", "ʧ", StringComparison.Ordinal)
        .Replace("dʒ", "ʤ", StringComparison.Ordinal)
        .Replace("aɪ", "I", StringComparison.Ordinal)
        .Replace("eɪ", "A", StringComparison.Ordinal)
        .Replace("oʊ", "O", StringComparison.Ordinal)
        .Replace("aʊ", "W", StringComparison.Ordinal)
        .Replace("ɔɪ", "Y", StringComparison.Ordinal)
        .Replace("ɜː", "ɜɹ", StringComparison.Ordinal)
        .Replace("ɚ", "əɹ", StringComparison.Ordinal)
        .Replace("ː", string.Empty, StringComparison.Ordinal);

    /// <summary>
    ///     Renders a punctuation item.
    /// </summary>
    /// <param name="text">The punctuation text (one mark, or a run of one repeated mark).</param>
    /// <param name="quoteOpen">Whether a straight double quote is open (toggled by a straight quote).</param>
    /// <returns>The mark, or an empty string when the voice has no symbol for it.</returns>
    private static string RenderMark(string text, ref bool quoteOpen)
    {
        var mark = text[0];
        if (mark == '.' && text.Length >= MaxMarkRun)
        {
            return "…";
        }

        if (ClauseMarks.Contains(mark, StringComparison.Ordinal))
        {
            return new string(mark, Math.Min(text.Length, MaxMarkRun));
        }

        switch (mark)
        {
            case '…':
            case '—':
            case ')':
            case '(':
            case '“':
            case '”':
                return mark.ToString();
            case '–':
            case '-':
                return "—";
            case '"':
                quoteOpen = !quoteOpen;
                return quoteOpen ? "“" : "”";
            case '«':
                return "“";
            case '»':
                return "”";
            default:
                return string.Empty;
        }
    }
}

/// <summary>
///     The dialect profile of a <see cref="MisakiRenderer"/>.
/// </summary>
/// <remarks>Single-valued today; it is the seam through which another dialect (British English) can be added without changing the constructor.</remarks>
public enum MisakiProfile
{
    /// <summary>General American English phonemes. Kokoro's British voices speak these too (the voice supplies the accent).</summary>
    Us = 0,
}
