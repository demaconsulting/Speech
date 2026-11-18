using System.Text;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;
using DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.RenderingSubsystem;

/// <summary>
///     Renders a <see cref="PhonemizedText"/> as the espeak-style IPA phoneme string the Piper voices expect:
///     stress marks before the vowel, one symbol per sound (<c>tʃ</c>, <c>oʊ</c>, <c>ɜː</c>), a flap for American
///     <c>t</c>, words separated by a space and the clause punctuation passed through.
/// </summary>
/// <remarks>
///     <para>
///     The mapping from ARPAbet is authored from phonetics and from the symbol inventory (<c>phoneme_id_map</c>) of
///     the Piper <c>en_US</c> voice; the committed coverage tests prove that every pronunciation of the dictionary
///     renders to symbols of that inventory. The measured agreement with the espeak-ng en-us voice the model was
///     trained on is reported in <c>docs/evaluation/phonemes-en/renderers.md</c>.
///     </para>
///     <para>
///     <b>Weak forms.</b> In running speech the small function words (articles, prepositions, auxiliaries,
///     pronouns) are unstressed and often have a reduced vowel; the model was trained on text where they are, so by
///     default the renderer replaces the full form of about sixty such words with their weak form. Turn this off
///     with <c>useWeakForms: false</c> to render every word in its dictionary form.
///     </para>
///     <para>
///     A word the host pronounced with an IPA override passes through unchanged (the host owns its symbols).
///     Instances are immutable and thread-safe.
///     </para>
/// </remarks>
public sealed class EspeakIpaRenderer
{
    /// <summary>The longest run of one clause mark that is rendered (an ellipsis is three dots).</summary>
    private const int MaxMarkRun = 3;

    /// <summary>The clause marks that pass through to the model.</summary>
    private const string ClauseMarks = ",;:.!?";

    /// <summary>The marks that become a comma (a pause) because the model has no symbol for them.</summary>
    private const string PauseMarks = "—–-()[]{}…";

    /// <summary>Whether function words are rendered in their weak form.</summary>
    private readonly bool _useWeakForms;

    /// <summary>
    ///     Initializes a renderer for the default profile (<see cref="EspeakIpaProfile.EnUs"/>) with weak forms.
    /// </summary>
    public EspeakIpaRenderer()
        : this(EspeakIpaProfile.EnUs)
    {
    }

    /// <summary>
    ///     Initializes a renderer.
    /// </summary>
    /// <param name="profile">The dialect profile.</param>
    /// <param name="useWeakForms">Whether the small function words are rendered in their weak (unstressed, reduced) form.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="profile"/> is not a defined profile.</exception>
    public EspeakIpaRenderer(EspeakIpaProfile profile, bool useWeakForms = true)
    {
        if (!Enum.IsDefined(profile))
        {
            throw new ArgumentOutOfRangeException(nameof(profile), profile, "The profile is not defined.");
        }

        Profile = profile;
        _useWeakForms = useWeakForms;
    }

    /// <summary>Gets the dialect profile.</summary>
    public EspeakIpaProfile Profile { get; }

    /// <summary>Gets a value indicating whether the small function words are rendered in their weak form.</summary>
    public bool UsesWeakForms => _useWeakForms;

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
    ///     Renders a sequence of items.
    /// </summary>
    /// <param name="items">The items, for example a slice of <see cref="PhonemizedText.Items"/> (a chunk of a long text).</param>
    /// <returns>The phoneme string.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
    public string Render(IReadOnlyList<PhonemizedItem> items) =>
        PhonemeStream.Join(RenderItems(items), items, string.Empty, string.Empty, _useWeakForms ? WeakForms.GroupedWithPrevious(items) : null);

    /// <summary>
    ///     Renders every item on its own, so a caller can split a long text between items.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <returns>
    ///     One string per item: the phonemes of a word, the mark a punctuation item becomes, or an empty string for an
    ///     item that has no sound in this voice (a quotation mark, an unsupported token).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
    public IReadOnlyList<string> RenderItems(IReadOnlyList<PhonemizedItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var pieces = new string[items.Count];
        var previousMark = false;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            switch (item.Kind)
            {
                case PhonemizedItemKind.Word:
                    pieces[i] = RenderWord(item, PhonemeStream.StartsWithVowel(items, i + 1), PhonemeStream.NextWordText(items, i + 1));
                    previousMark = false;
                    break;
                case PhonemizedItemKind.Punctuation:
                    pieces[i] = RenderMark(item.Text, previousMark);
                    previousMark = pieces[i].Length > 0 || previousMark;
                    break;
                default:
                    pieces[i] = string.Empty;
                    break;
            }
        }

        return pieces;
    }

    /// <summary>
    ///     Renders one pronunciation on its own (the dictionary form, without the weak-form table).
    /// </summary>
    /// <param name="pronunciation">The pronunciation.</param>
    /// <returns>The IPA symbols.</returns>
    internal static string RenderPronunciation(Pronunciation pronunciation)
    {
        ArgumentNullException.ThrowIfNull(pronunciation);
        return EspeakWordRules.Render(pronunciation.Phones);
    }

    /// <summary>
    ///     Renders a word item: an IPA override as given, a function word in its weak form, any other word from its phones.
    /// </summary>
    /// <param name="item">The word item.</param>
    /// <param name="nextStartsWithVowel">Whether the next word starts with a vowel.</param>
    /// <param name="nextWord">The text of the next word, or <see langword="null"/>.</param>
    /// <returns>The IPA symbols.</returns>
    private string RenderWord(PhonemizedItem item, bool nextStartsWithVowel, string? nextWord)
    {
        if (item.IpaOverride is not null)
        {
            return item.IpaOverride;
        }

        var demoted = false;
        var weak = string.Empty;
        var known = _useWeakForms && item.Source == PronunciationSource.Lexicon && WeakForms.TryGet(item.Text, nextStartsWithVowel, nextWord, espeakStyle: true, out weak, out demoted);
        var text = known && weak.Length > 0 ? weak : EspeakWordRules.Render(item.Pronunciation.Phones, demoted);

        // An r-colored vowel at the end of a word is joined to a vowel-initial word by a linking r
        return nextStartsWithVowel && (text.EndsWith('ɚ') || text.EndsWith("ɜː", StringComparison.Ordinal)) ? text + "ɹ" : text;
    }

    /// <summary>
    ///     Renders a punctuation item.
    /// </summary>
    /// <param name="text">The punctuation text (one mark, or a run of one repeated mark).</param>
    /// <param name="previousMark">Whether the previous rendered item was already a pause mark.</param>
    /// <returns>The mark, or an empty string when the voice has no sound for it.</returns>
    private static string RenderMark(string text, bool previousMark)
    {
        var mark = text[0];
        if (ClauseMarks.Contains(mark, StringComparison.Ordinal))
        {
            return new string(mark, Math.Min(text.Length, MaxMarkRun));
        }

        // Dashes, brackets and the ellipsis only pause; after a mark that already pauses they add nothing
        return PauseMarks.Contains(mark, StringComparison.Ordinal) && !previousMark ? "," : string.Empty;
    }
}
