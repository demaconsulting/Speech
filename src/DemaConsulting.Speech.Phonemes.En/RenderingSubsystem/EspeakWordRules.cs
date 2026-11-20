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

    /// <summary>The base symbols of the stems of the numbers thirteen to nineteen (before <c>T IY N</c>).</summary>
    private static readonly string[][] TeenStems =
    [
        ["TH", "ER"], ["F", "AO", "R"], ["F", "IH", "F"], ["S", "IH", "K", "S"], ["S", "EH", "V", "AH", "N"], ["EY"], ["N", "AY", "N"],
    ];

    /// <summary>The consonants after which a reduced <c>i</c> in a prefix is the lax <c>ᵻ</c>.</summary>
    private static readonly string[] LaxPrefixFollowers = ["F", "L", "M", "P", "T", "JH", "B"];

    /// <summary>
    ///     Renders the phones of a word.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <param name="demotePrimary">Whether the primary stress is rendered as a secondary stress (a prominent function word).</param>
    /// <param name="elementStarts">
    ///     The index of the first phone of each element after the first of a compound pronounced part by part, or
    ///     <see langword="null"/>. A secondary stress from the first of them on is the lesser stress of a later element
    ///     of the compound, not a clash with the primary stress, so it is always written (<c>wˈɛlnˌoʊn</c>), and the
    ///     vowel-before-<c>r</c> rules look only at the phones of the element a vowel is in (<c>tea-room</c> is
    ///     <c>tˈiːɹˌuːm</c>, not an r-colored <c>ɪɹ</c>).
    /// </param>
    /// <returns>The IPA symbols with stress marks.</returns>
    public static string Render(ReadOnlySpan<Phone> phones, bool demotePrimary = false, IReadOnlyList<int>? elementStarts = null)
    {
        var tonic = TonicOrdinal(phones);
        var vowelCount = CountVowels(phones);
        var teen = IsTeenNumber(phones);
        var compoundBreak = elementStarts is { Count: > 0 } ? elementStarts[0] : -1;
        var builder = new StringBuilder(phones.Length * 2);
        var ordinal = -1;
        for (var i = 0; i < phones.Length; i++)
        {
            var phone = phones[i];
            var (low, high) = ElementBounds(elementStarts, i, phones.Length);
            var element = phones[low..high];
            if (!phone.IsVowel)
            {
                builder.Append(IsSchwaCarriedR(element, i - low) ? string.Empty : Consonant(phones, i));
                continue;
            }

            ordinal++;
            var last = ordinal == vowelCount - 1;

            // A secondary stress on the final -y (nobody, thirty) is not realized
            var mark = phone.Stress == 2 && last && phone.BaseSymbol == "IY" ? string.Empty : StressMark(phone.Stress, ordinal, tonic, demotePrimary);

            // The lesser stress of an element of a compound (the prefix of ex-wife, the later parts of well-known) is deliberate and stands next to the primary stress
            if (phone.Stress == 2 && compoundBreak >= 0 && !(i == phones.Length - 1 && phone.BaseSymbol == "IY"))
            {
                mark = Secondary;
            }

            if (teen)
            {
                mark = TeenMark(ordinal, last, vowelCount) ?? mark;
            }

            builder.Append(mark);
            builder.Append(RColoredVowel(element, i - low) ?? Nucleus(phones, i, ordinal, tonic, last, mark.Length > 0));
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Gets the first and the end index of the element of a compound that contains a phone.
    /// </summary>
    /// <param name="elementStarts">The index of the first phone of each element after the first, or <see langword="null"/>.</param>
    /// <param name="index">The index of the phone.</param>
    /// <param name="length">The number of phones of the word.</param>
    /// <returns>The index of the first phone of the element and the index after its last phone.</returns>
    private static (int Low, int High) ElementBounds(IReadOnlyList<int>? elementStarts, int index, int length)
    {
        var low = 0;
        var high = length;
        if (elementStarts is null)
        {
            return (low, high);
        }

        foreach (var start in elementStarts)
        {
            if (start <= index)
            {
                low = start;
            }
            else
            {
                high = start;
                break;
            }
        }

        return (low, high);
    }

    /// <summary>
    ///     Gets the IPA of a vowel that stands before an <c>r</c> of the same element, in the way the espeak-ng en-us voice
    ///     writes it.
    /// </summary>
    /// <param name="element">The phones of the element (the word, or one part of a compound) the vowel is in.</param>
    /// <param name="index">The index of the vowel in the element.</param>
    /// <returns>The IPA symbols, or <see langword="null"/> when the vowel has no special form before <c>r</c>.</returns>
    /// <remarks>
    ///     <c>IY</c> is the lax <c>ɪ</c> (<i>here</i>: <c>hˈɪɹ</c>); <c>AY</c> and <c>AW</c> are followed by a schwa that carries the
    ///     <c>r</c> (see <see cref="DiphthongBeforeR"/>); <c>UW</c> is the vowel of <i>cure</i> (<i>poor</i>: <c>pˈʊɹ</c>, doubled
    ///     before a vowel); <c>EY</c> before <c>r</c> and a consonant or the end is the vowel of <i>square</i> (<i>share</i>).
    /// </remarks>
    private static string? RColoredVowel(ReadOnlySpan<Phone> element, int index)
    {
        if (SymbolAt(element, index + 1) != "R")
        {
            return null;
        }

        switch (element[index].BaseSymbol)
        {
            case "IY":
                return "ɪ";
            case "AW":
                return DiphthongBeforeR(element, index, "aʊ");
            case "AY":
                return DiphthongBeforeR(element, index, "aɪ");
            case "EY":
                return IsVowelAt(element, index + 2) ? null : "ɛ";
            case "UW":
                return "ʊ" + (IsVowelAt(element, index + 2) ? "ɹ" : string.Empty);
            default:
                return null;
        }
    }

    /// <summary>
    ///     Tests whether an <c>r</c> is part of the schwa that follows the diphthong before it (<i>fire</i>: <c>faɪɚ</c>), so it
    ///     is not written on its own.
    /// </summary>
    /// <param name="element">The phones of the element the <c>r</c> is in.</param>
    /// <param name="index">The index of the phone in the element.</param>
    /// <returns><see langword="true"/> for an <c>r</c> after <c>AY</c> or <c>AW</c> of the same element that no vowel follows.</returns>
    private static bool IsSchwaCarriedR(ReadOnlySpan<Phone> element, int index) =>
        element[index].BaseSymbol == "R" && SymbolAt(element, index - 1) is "AY" or "AW" && !IsVowelAt(element, index + 1);

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
    ///     Tests whether a word is one of the numbers thirteen to nineteen: the stem of one of the seven number names
    ///     followed by <c>t</c>, a stressed <c>IY</c> and <c>n</c>.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <returns><see langword="true"/> for one of the seven numbers; other words that end in <c>T IY1 N</c> (<c>routine</c>, <c>canteen</c>) are not.</returns>
    /// <remarks>The stress digit of the stem's vowel is not compared: the dictionary lists several (<c>EY0</c> and <c>EY1</c> of <c>eighteen</c>).</remarks>
    private static bool IsTeenNumber(ReadOnlySpan<Phone> phones)
    {
        var length = phones.Length;
        if (length < 4
            || SymbolAt(phones, length - 3) != "T"
            || SymbolAt(phones, length - 2) != "IY" || phones[^2].Stress != 1
            || SymbolAt(phones, length - 1) != "N")
        {
            return false;
        }

        foreach (var stem in TeenStems)
        {
            if (stem.Length == length - 3 && StemMatches(phones, stem))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Tests whether the leading phones of a word are a stem, ignoring stress digits.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <param name="stem">The base symbols of the stem.</param>
    /// <returns><see langword="true"/> when the first phones have exactly these base symbols.</returns>
    private static bool StemMatches(ReadOnlySpan<Phone> phones, string[] stem)
    {
        for (var i = 0; i < stem.Length; i++)
        {
            if (SymbolAt(phones, i) != stem[i])
            {
                return false;
            }
        }

        return true;
    }
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
    ///     Gets the IPA of the diphthong <c>ay</c> or <c>aw</c>: before <c>r</c> a schwa follows it that carries the
    ///     <c>r</c> (<i>inspire</i>: <c>ɪnspˈaɪɚ</c>, <i>hour</i>: <c>ˈaʊɚ</c>), and a high front vowel after the <c>r</c> brings the
    ///     <c>r</c> back (<i>firing</i>: <c>fˈaɪɚɹɪŋ</c>).
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <param name="index">The index of the diphthong.</param>
    /// <param name="diphthong">The IPA of the diphthong itself.</param>
    /// <returns>The IPA symbols.</returns>
    /// <remarks>Before any other vowel (<i>cairo</i>, <i>cyrus</i>, <i>environment</i>) the <c>r</c> stays a plain <c>ɹ</c> after the diphthong.</remarks>
    private static string DiphthongBeforeR(ReadOnlySpan<Phone> phones, int index, string diphthong)
    {
        if (SymbolAt(phones, index + 1) != "R")
        {
            return diphthong;
        }

        var highFrontOrNone = !IsVowelAt(phones, index + 2) || SymbolAt(phones, index + 2) is "IH" or "IY";
        return highFrontOrNone ? diphthong + "ɚ" : diphthong;
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
    /// <remarks>
    ///     An unstressed <c>IY</c> is the tense <c>i</c> at the end of a word, after the primary stress
    ///     (<i>happy</i>, <i>happiness</i>, <i>everybody</i>) and at the start of a word (<i>email</i>). Before the
    ///     primary stress, where it ends a prefix or the first element of a compound after a consonant
    ///     (<i>semicircle</i>, <i>multicolored</i>, <i>anticlimax</i>), it is lax as espeak-ng writes it: the voice
    ///     reads a tense <c>i</c> followed by a consonant and a stressed syllable as the end of a word, lengthens it
    ///     and pauses after it, which splits the word in two. A vowel that lost its secondary stress to a stress clash
    ///     (the letter names of <i>http</i>) keeps its tense vowel.
    /// </remarks>
    private static string Iy(ReadOnlySpan<Phone> phones, int index, int ordinal, int tonic, bool stressed)
    {
        if (stressed)
        {
            return "iː";
        }

        // Before another vowel (hiatus) the unstressed vowel is lax
        if (IsVowelAt(phones, index + 1))
        {
            return "ɪ";
        }

        // At the end of the word and after the primary stress the vowel is the tense i of happy
        var pretonic = tonic > ordinal && index + 1 < phones.Length;
        if (!pretonic)
        {
            return "i";
        }

        // Before the primary stress and a consonant: the lax i after r (rebuild), and ɪ after any other consonant when
        // the vowel is a reduced one (semicircle), not a secondary stress lost to a clash or the first sound of the word
        if (SymbolAt(phones, index - 1) == "R")
        {
            return "ᵻ";
        }

        return phones[index].Stress == 0 && index > 0 && !IsVowelAt(phones, index - 1) ? "ɪ" : "i";
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
