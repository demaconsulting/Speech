using System.Text;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;
using static DemaConsulting.Speech.Phonemes.En.RenderingSubsystem.WordShape;

namespace DemaConsulting.Speech.Phonemes.En.RenderingSubsystem;

/// <summary>
///     The phonetic rules that turn the ARPAbet phones of one word into the misaki-style symbols of Kokoro.
/// </summary>
/// <remarks>
///     <para>
///     The basic mapping is the textbook correspondence between ARPAbet and the IPA of General American English, with
///     Kokoro's single-letter diphthongs (<c>A I O W Y</c>) and affricates (<c>ʧ ʤ</c>) and its <c>ɜɹ</c>/<c>əɹ</c> for
///     <c>ER</c>. The rules on top of it are the ones the model's training text shows: a flap for an unstressed
///     <c>t</c> between vowels, the lax <c>ᵻ</c> of the endings <c>-ed</c> and <c>-es</c>, the superscript schwa
///     <c>ᵊ</c> of a syllabic <c>l</c> or <c>n</c>, a plain schwa for a reduced <c>i</c> in a prefix, and the dropping
///     of a secondary stress right before the primary one. Every rule is a statement about phone classes and
///     positions, not about particular words.
///     </para>
///     <para>The class is stateless and thread-safe.</para>
/// </remarks>
internal static class MisakiWordRules
{
    /// <summary>The primary stress mark.</summary>
    private const string Primary = "ˈ";

    /// <summary>The secondary stress mark.</summary>
    private const string Secondary = "ˌ";

    /// <summary>
    ///     Renders the phones of a word.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <param name="demotePrimary">Whether the primary stress is rendered as a secondary stress (a prominent function word).</param>
    /// <returns>The symbols with stress marks.</returns>
    public static string Render(ReadOnlySpan<Phone> phones, bool demotePrimary = false)
    {
        var tonic = TonicOrdinal(phones);
        var vowelCount = CountVowels(phones);
        var builder = new StringBuilder(phones.Length * 2);
        var ordinal = -1;
        for (var i = 0; i < phones.Length; i++)
        {
            var phone = phones[i];
            if (!phone.IsVowel)
            {
                builder.Append(IsFlap(phones, i) ? "ɾ" : Consonant(phone.BaseSymbol));
                continue;
            }

            ordinal++;
            var last = ordinal == vowelCount - 1;
            builder.Append(StressMark(phone, ordinal, tonic, last, demotePrimary));
            builder.Append(Nucleus(phones, i, ordinal, tonic));
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Gets the stress mark of a vowel.
    /// </summary>
    /// <param name="phone">The vowel.</param>
    /// <param name="ordinal">The ordinal of the vowel among the vowels of the word.</param>
    /// <param name="tonic">The ordinal of the primary-stressed vowel, or <c>-1</c>.</param>
    /// <param name="last">Whether this is the last vowel.</param>
    /// <param name="demotePrimary">Whether a primary stress is rendered as a secondary stress.</param>
    /// <returns>The mark, or an empty string.</returns>
    private static string StressMark(Phone phone, int ordinal, int tonic, bool last, bool demotePrimary)
    {
        switch (phone.Stress)
        {
            case 1:
                return demotePrimary ? Secondary : Primary;
            case 2:
                // Right before the primary stress, and on a final -y, a secondary stress is not realized
                return (tonic == ordinal + 1) || (last && phone.BaseSymbol == "IY") ? string.Empty : Secondary;
            default:
                return string.Empty;
        }
    }

    /// <summary>
    ///     Gets the symbols of a consonant.
    /// </summary>
    /// <param name="symbol">The ARPAbet consonant.</param>
    /// <returns>The symbols.</returns>
    private static string Consonant(string symbol) => symbol switch
    {
        "B" => "b",
        "CH" => "ʧ",
        "D" => "d",
        "DH" => "ð",
        "F" => "f",
        "G" => "ɡ",
        "HH" => "h",
        "JH" => "ʤ",
        "K" => "k",
        "L" => "l",
        "M" => "m",
        "N" => "n",
        "NG" => "ŋ",
        "P" => "p",
        "R" => "ɹ",
        "S" => "s",
        "SH" => "ʃ",
        "T" => "t",
        "TH" => "θ",
        "V" => "v",
        "W" => "w",
        "Y" => "j",
        "Z" => "z",
        _ => "ʒ",
    };

    /// <summary>
    ///     Gets the symbols of a vowel in its context.
    /// </summary>
    /// <param name="phones">The phones of the word.</param>
    /// <param name="index">The index of the vowel.</param>
    /// <param name="ordinal">The ordinal of the vowel among the vowels of the word.</param>
    /// <param name="tonic">The ordinal of the primary-stressed vowel, or <c>-1</c>.</param>
    /// <returns>The symbols of the vowel.</returns>
    private static string Nucleus(ReadOnlySpan<Phone> phones, int index, int ordinal, int tonic)
    {
        var stress = phones[index].Stress;
        return phones[index].BaseSymbol switch
        {
            "AA" => "ɑ",
            "AE" => "æ",
            "AH" => stress > 0 ? "ʌ" : UnstressedAh(phones, index),
            "AO" => "ɔ",
            "AW" => "W",
            "AY" => "I",
            "EH" => "ɛ",
            "ER" => stress > 0 ? "ɜɹ" : "əɹ",
            "EY" => "A",
            "IH" => stress > 0 ? "ɪ" : UnstressedIh(phones, index),
            "IY" => IyBeforeStress(ordinal, tonic, stress) ? "ə" : "i",
            "OW" => "O",
            "OY" => "Y",
            "UH" => "ʊ",
            _ => "u",
        };
    }

    /// <summary>
    ///     Tests whether an unstressed <c>IY</c> stands in a prefix before the stress (<i>re-</i>, <i>de-</i>), where it is a schwa.
    /// </summary>
    /// <param name="ordinal">The vowel ordinal.</param>
    /// <param name="tonic">The ordinal of the primary-stressed vowel.</param>
    /// <param name="stress">The stress digit.</param>
    /// <returns><see langword="true"/> for a prefix vowel.</returns>
    private static bool IyBeforeStress(int ordinal, int tonic, int stress) => stress == 0 && tonic > ordinal;

    /// <summary>
    ///     Gets the symbols of an unstressed <c>AH</c>.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <param name="index">The index of the vowel.</param>
    /// <returns>The symbols.</returns>
    private static string UnstressedAh(ReadOnlySpan<Phone> phones, int index)
    {
        var next = SymbolAt(phones, index + 1);
        var previous = SymbolAt(phones, index - 1);

        // The ending of a plural or a past tense is the lax i; so is that of -age
        if (IsInflectionEnding(phones, index))
        {
            return "ᵻ";
        }

        if (index + 2 == phones.Length && next == "JH")
        {
            return "ɪ";
        }

        // A syllabic l at the end of a word after a consonant (little, table) or after m (family), or n after t d s z p f (students, season)
        if (next == "L" && index > 0 && ((index + 2 == phones.Length && !IsGlideOrR(phones, index - 1)) || previous == "M"))
        {
            return "ᵊ";
        }

        if (next == "N" && previous is "T" or "D" or "S" or "Z" or "P" or "F" && ClosesWithNasalCluster(phones, index + 1))
        {
            return "ᵊ";
        }

        return "ə";
    }

    /// <summary>
    ///     Tests whether the phone at an index is <c>r</c>, <c>w</c>, <c>y</c> or an <c>r</c>-colored vowel, which keep a plain schwa before <c>l</c>.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <param name="index">The index.</param>
    /// <returns><see langword="true"/> for such a phone.</returns>
    private static bool IsGlideOrR(ReadOnlySpan<Phone> phones, int index) =>
        SymbolAt(phones, index) is "R" or "W" or "Y" or "ER";

    /// <summary>
    ///     Tests whether the <c>n</c> at an index ends the word or is followed by <c>t</c>, <c>s</c>, <c>d</c> or <c>z</c> and then the end of the word.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <param name="nIndex">The index of the <c>n</c>.</param>
    /// <returns><see langword="true"/> when the nasal closes the word, alone or with one more consonant.</returns>
    private static bool ClosesWithNasalCluster(ReadOnlySpan<Phone> phones, int nIndex)
    {
        if (nIndex + 1 == phones.Length)
        {
            return true;
        }

        return nIndex + 2 <= phones.Length && SymbolAt(phones, nIndex + 1) is "T" or "S" or "D" or "Z";
    }

    /// <summary>
    ///     Gets the symbols of an unstressed <c>IH</c>.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <param name="index">The index of the vowel.</param>
    /// <returns>The symbols.</returns>
    private static string UnstressedIh(ReadOnlySpan<Phone> phones, int index)
    {
        var next = SymbolAt(phones, index + 1);
        var nextIsFinal = index + 2 == phones.Length;

        // -ing, -ic, -ive, -age and -ish keep the plain lax i, and so does an i before a velar nasal inside the word
        if (next == "NG" || (nextIsFinal && next is "K" or "V" or "JH" or "SH"))
        {
            return "ɪ";
        }

        if (IsInflectionEnding(phones, index))
        {
            return "ᵻ";
        }

        // A word-initial i before a velar or nasal (ex-, en-) stays an i; anywhere else the reduced i is a schwa
        return index == 0 && next is "K" or "G" or "N" ? "ɪ" : "ə";
    }
}
