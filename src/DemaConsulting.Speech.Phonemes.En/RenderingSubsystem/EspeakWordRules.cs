using System.Text;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;
using static DemaConsulting.Speech.Phonemes.En.RenderingSubsystem.WordShape;

namespace DemaConsulting.Speech.Phonemes.En.RenderingSubsystem;

/// <summary>
///     The phonetic rules that turn the ARPAbet phones of one word into espeak-style IPA for the Piper voice.
/// </summary>
/// <remarks>
///     <para>
///     The basic mapping is the textbook correspondence between ARPAbet and the IPA of General American English. The
///     rules on top of it are the ones the model's training text shows: stress marks sit before the vowel; a
///     secondary stress next to the primary stress is dropped (stress clash); an unstressed <c>t</c> between vowels is
///     a flap; an unstressed short <c>i</c> or schwa is a different vowel depending on its neighbors; an
///     <c>r</c>-colored vowel before another vowel gets a linking <c>ɹ</c>; and a few vowels are shorter before certain
///     consonants. Every rule is a statement about phone classes and positions, not about particular words.
///     </para>
///     <para>The class is stateless and thread-safe.</para>
/// </remarks>
internal static class EspeakWordRules
{
    /// <summary>The primary stress mark.</summary>
    private const string Primary = "ˈ";

    /// <summary>The secondary stress mark.</summary>
    private const string Secondary = "ˌ";

    /// <summary>The consonants after which a reduced <c>i</c> in a prefix is the lax <c>ᵻ</c>.</summary>
    private static readonly string[] LaxPrefixFollowers = ["F", "L", "M", "P", "T", "JH", "B"];

    /// <summary>
    ///     Renders the phones of a word.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <param name="demotePrimary">Whether the primary stress is rendered as a secondary stress (a prominent function word).</param>
    /// <returns>The IPA symbols with stress marks.</returns>
    public static string Render(ReadOnlySpan<Phone> phones, bool demotePrimary = false)
    {
        var tonic = TonicOrdinal(phones);
        var vowelCount = CountVowels(phones);
        var teen = IsTeenNumber(phones, vowelCount);
        var builder = new StringBuilder(phones.Length * 2);
        var ordinal = -1;
        for (var i = 0; i < phones.Length; i++)
        {
            var phone = phones[i];
            if (!phone.IsVowel)
            {
                builder.Append(Consonant(phones, i));
                continue;
            }

            ordinal++;
            var last = ordinal == vowelCount - 1;

            // A secondary stress on the final -y (nobody, thirty) is not realized
            var mark = phone.Stress == 2 && last && phone.BaseSymbol == "IY" ? string.Empty : StressMark(phone.Stress, ordinal, tonic, demotePrimary);
            if (teen)
            {
                mark = TeenMark(ordinal, last, vowelCount) ?? mark;
            }

            builder.Append(mark);
            builder.Append(Nucleus(phones, i, ordinal, tonic, last, mark.Length > 0));
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Gets the stress mark of a vowel of one of the numbers thirteen to nineteen: they are stressed on their first
    ///     syllable, and a long one keeps a secondary stress on <c>-teen</c>.
    /// </summary>
    /// <param name="ordinal">The ordinal of the vowel.</param>
    /// <param name="last">Whether this is the last vowel.</param>
    /// <param name="vowelCount">The number of vowels of the word.</param>
    /// <returns>The mark, or <see langword="null"/> when the ordinary stress rules apply to this vowel.</returns>
    private static string? TeenMark(int ordinal, bool last, int vowelCount)
    {
        if (ordinal == 0)
        {
            return Primary;
        }

        if (!last)
        {
            return null;
        }

        return vowelCount > 2 ? Secondary : string.Empty;
    }

    /// <summary>
    ///     Tests whether a word is one of the numbers thirteen to nineteen: it ends in <c>t</c> and a stressed <c>IY</c> and <c>n</c>.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <param name="vowelCount">The number of vowels of the word.</param>
    /// <returns><see langword="true"/> for a word of two or more syllables that ends in <c>T IY1 N</c>.</returns>
    private static bool IsTeenNumber(ReadOnlySpan<Phone> phones, int vowelCount) =>
        vowelCount >= 2 && phones.Length >= 4
        && SymbolAt(phones, phones.Length - 3) == "T"
        && SymbolAt(phones, phones.Length - 2) == "IY" && phones[^2].Stress == 1
        && SymbolAt(phones, phones.Length - 1) == "N";

    /// <summary>
    ///     Gets the stress mark of a vowel.
    /// </summary>
    /// <param name="stress">The stress digit of the vowel.</param>
    /// <param name="ordinal">The ordinal of the vowel among the vowels of the word.</param>
    /// <param name="tonic">The ordinal of the primary-stressed vowel, or <c>-1</c>.</param>
    /// <param name="demotePrimary">Whether a primary stress is rendered as a secondary stress.</param>
    /// <returns>The mark, or an empty string.</returns>
    private static string StressMark(int stress, int ordinal, int tonic, bool demotePrimary)
    {
        switch (stress)
        {
            case 1:
                return demotePrimary ? Secondary : Primary;
            case 2:
                // A secondary stress right next to the primary one is not realized (stress clash)
                return tonic >= 0 && Math.Abs(ordinal - tonic) < 2 ? string.Empty : Secondary;
            default:
                return string.Empty;
        }
    }

    /// <summary>
    ///     Gets the IPA of a consonant in its context.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <param name="index">The index of the consonant.</param>
    /// <returns>The IPA symbols.</returns>
    private static string Consonant(ReadOnlySpan<Phone> phones, int index)
    {
        var symbol = phones[index].BaseSymbol;
        if (symbol == "T" && IsFlap(phones, index))
        {
            return "ɾ";
        }

        // n before a velar stop is the velar nasal
        if (symbol == "N" && SymbolAt(phones, index + 1) is "K" or "G")
        {
            return "ŋ";
        }

        return symbol switch
        {
            "B" => "b",
            "CH" => "tʃ",
            "D" => "d",
            "DH" => "ð",
            "F" => "f",
            "G" => "ɡ",
            "HH" => "h",
            "JH" => "dʒ",
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
    }

    /// <summary>
    ///     Gets the IPA of a vowel in its context.
    /// </summary>
    /// <param name="phones">The phones of the word.</param>
    /// <param name="index">The index of the vowel.</param>
    /// <param name="ordinal">The ordinal of the vowel among the vowels of the word.</param>
    /// <param name="tonic">The ordinal of the primary-stressed vowel, or <c>-1</c>.</param>
    /// <param name="last">Whether this is the last vowel of the word.</param>
    /// <param name="marked">Whether the vowel carries a stress mark.</param>
    /// <returns>The IPA symbols of the vowel.</returns>
    private static string Nucleus(ReadOnlySpan<Phone> phones, int index, int ordinal, int tonic, bool last, bool marked)
    {
        var phone = phones[index];
        var stress = phone.Stress;
        var nextIsVowel = IsVowelAt(phones, index + 1);
        switch (phone.BaseSymbol)
        {
            case "AA":
                return "ɑː";
            case "AE":
                return stress == 0 && index == 0 ? "ɐ" : "æ";
            case "AH":
                return stress > 0 ? "ʌ" : UnstressedAh(phones, index, ordinal, tonic, last);
            case "AO":
                return SymbolAt(phones, index + 1) is "NG" or "F" or "TH" or "S" ? "ɔ" : "ɔː";
            case "AW":
                return "aʊ";
            case "AY":
                return "aɪ";
            case "EH":
                return "ɛ";
            case "ER":
                if (nextIsVowel && stress == 0 && SymbolAt(phones, index - 1) is "F" or "V")
                {
                    // As in different and every: the vowel before r is lost
                    return "ɹ";
                }

                return (stress > 0 ? "ɜː" : "ɚ") + (nextIsVowel ? "ɹ" : string.Empty);
            case "EY":
                return "eɪ";
            case "IH":
                return stress > 0 ? "ɪ" : UnstressedIh(phones, index, ordinal, tonic);
            case "IY":
                return Iy(phones, index, ordinal, tonic, marked || stress == 1);
            case "OW":
                return "oʊ";
            case "OY":
                return "ɔɪ";
            case "UH":
                // ur before a vowel is doubled (as in "during")
                return "ʊ" + (SymbolAt(phones, index + 1) == "R" && IsVowelAt(phones, index + 2) ? "ɹ" : string.Empty);
            default:
                return "uː";
        }
    }

    /// <summary>
    ///     Gets the IPA of <c>IY</c>.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <param name="index">The index of the vowel.</param>
    /// <param name="ordinal">The vowel ordinal.</param>
    /// <param name="tonic">The ordinal of the primary-stressed vowel.</param>
    /// <param name="stressed">Whether the vowel is stressed (marked).</param>
    /// <returns>The IPA symbols.</returns>
    private static string Iy(ReadOnlySpan<Phone> phones, int index, int ordinal, int tonic, bool stressed)
    {
        if (stressed)
        {
            return "iː";
        }

        // Before another vowel (hiatus) the unstressed vowel is lax; in a prefix after r it is the lax i
        if (IsVowelAt(phones, index + 1))
        {
            return "ɪ";
        }

        var prefixAfterR = SymbolAt(phones, index - 1) == "R" && tonic > ordinal && index + 1 < phones.Length;
        return prefixAfterR ? "ᵻ" : "i";
    }

    /// <summary>
    ///     Gets the IPA of an unstressed <c>AH</c>.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <param name="index">The index of the vowel.</param>
    /// <param name="ordinal">The vowel ordinal.</param>
    /// <param name="tonic">The ordinal of the primary-stressed vowel.</param>
    /// <param name="last">Whether this is the last vowel.</param>
    /// <returns>The IPA symbols.</returns>
    private static string UnstressedAh(ReadOnlySpan<Phone> phones, int index, int ordinal, int tonic, bool last)
    {
        var next = SymbolAt(phones, index + 1);
        var nextIsFinal = index + 2 == phones.Length;
        var afterTonic = tonic >= 0 && ordinal > tonic;

        // The ending of a plural or a past tense is the lax i; so is that of -age
        if (IsInflectionEnding(phones, index))
        {
            return "ᵻ";
        }

        if (nextIsFinal && next == "JH")
        {
            return "ɪ";
        }

        // A word-initial reduced vowel before a consonant is the open schwa; before n and another consonant it is the u of un-
        if (index == 0)
        {
            return next == "N" && index + 2 < phones.Length && !phones[index + 2].IsVowel ? "ʌ" : "ɐ";
        }

        // A closing -d after another consonant (hundred, invalid) has the plain lax i
        if (nextIsFinal && next == "D")
        {
            return "ɪ";
        }

        // After the stress, before a stop or a fricative that does not close a syllable-final cluster, a lax i is heard
        if (afterTonic && !last && next is "K" or "M" or "S" or "G" or "JH" or "Z")
        {
            return "ɪ";
        }

        if (afterTonic && !last && next == "T")
        {
            return "ᵻ";
        }

        // The endings -est/-ets close the word with a lax i
        if (last && afterTonic && IsClosingEstOrEts(phones, index))
        {
            return "ɪ";
        }

        return "ə";
    }

    /// <summary>
    ///     Tests whether the vowel at an index is followed by <c>s t</c> or <c>t s</c> and then the end of the word.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <param name="index">The index of the vowel.</param>
    /// <returns><see langword="true"/> for the endings <c>-est</c> and <c>-ets</c>.</returns>
    private static bool IsClosingEstOrEts(ReadOnlySpan<Phone> phones, int index)
    {
        if (index + 3 != phones.Length)
        {
            return false;
        }

        var first = SymbolAt(phones, index + 1);
        var second = SymbolAt(phones, index + 2);
        return (first == "S" && second == "T") || (first == "T" && second == "S");
    }

    /// <summary>
    ///     Gets the IPA of an unstressed <c>IH</c>.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <param name="index">The index of the vowel.</param>
    /// <param name="ordinal">The vowel ordinal.</param>
    /// <param name="tonic">The ordinal of the primary-stressed vowel.</param>
    /// <returns>The IPA symbols.</returns>
    private static string UnstressedIh(ReadOnlySpan<Phone> phones, int index, int ordinal, int tonic)
    {
        var next = SymbolAt(phones, index + 1);
        var nextIsFinal = index + 2 == phones.Length;
        var beforeTonic = tonic > ordinal;

        // -ing and -ic keep the plain lax i; the ending of a plural or a past tense is the reduced lax i
        if (nextIsFinal && next is "NG" or "K")
        {
            return "ɪ";
        }

        if (IsInflectionEnding(phones, index))
        {
            return "ᵻ";
        }

        // A word-initial i before a velar or nasal cluster is the e of ex-/en-; before anything else it is the reduced lax i
        if (index == 0)
        {
            return next is "K" or "G" or "N" ? "ɛ" : "ᵻ";
        }

        // In a prefix: after r, or before a labial or a liquid, the reduced lax i
        if (beforeTonic && (SymbolAt(phones, index - 1) == "R" || Array.IndexOf(LaxPrefixFollowers, next) >= 0))
        {
            return "ᵻ";
        }

        return "ɪ";
    }
}
