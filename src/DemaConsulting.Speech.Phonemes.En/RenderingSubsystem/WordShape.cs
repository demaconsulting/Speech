using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.RenderingSubsystem;

/// <summary>
///     Questions about the shape of a word's phones that the phonetic rules of both renderers ask: where the stress
///     is, what stands next to a phone, and where a <c>t</c> is flapped.
/// </summary>
/// <remarks>The class is stateless and thread-safe.</remarks>
internal static class WordShape
{
    /// <summary>
    ///     Gets the ordinal (among the vowels) of the first primary-stressed vowel.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <returns>The ordinal, or <c>-1</c> when no vowel has primary stress.</returns>
    public static int TonicOrdinal(ReadOnlySpan<Phone> phones)
    {
        var ordinal = -1;
        foreach (var phone in phones)
        {
            if (!phone.IsVowel)
            {
                continue;
            }

            ordinal++;
            if (phone.Stress == 1)
            {
                return ordinal;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Counts the vowels of a word.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <returns>The number of vowel phones.</returns>
    public static int CountVowels(ReadOnlySpan<Phone> phones)
    {
        var count = 0;
        foreach (var phone in phones)
        {
            count += phone.IsVowel ? 1 : 0;
        }

        return count;
    }

    /// <summary>
    ///     Gets the base symbol of the phone at an index.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <param name="index">The index; outside the word it is the empty string.</param>
    /// <returns>The symbol without stress digit.</returns>
    public static string SymbolAt(ReadOnlySpan<Phone> phones, int index) =>
        index >= 0 && index < phones.Length ? phones[index].BaseSymbol : string.Empty;

    /// <summary>
    ///     Tests whether the phone at an index is a vowel.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <param name="index">The index.</param>
    /// <returns><see langword="true"/> when the index is inside the word and the phone is a vowel.</returns>
    public static bool IsVowelAt(ReadOnlySpan<Phone> phones, int index) =>
        index >= 0 && index < phones.Length && phones[index].IsVowel;

    /// <summary>
    ///     Tests whether a <c>t</c> is flapped: between a vowel (or the <c>r</c> after it) and an unstressed vowel, as in
    ///     <i>city</i>, <i>water</i>, <i>quality</i> and <i>party</i>.
    /// </summary>
    /// <param name="phones">The phones of the word.</param>
    /// <param name="index">The index of the consonant.</param>
    /// <returns><see langword="true"/> when the consonant is a flapped <c>t</c>.</returns>
    public static bool IsFlap(ReadOnlySpan<Phone> phones, int index)
    {
        if (SymbolAt(phones, index) != "T" || index == 0 || index + 1 >= phones.Length)
        {
            return false;
        }

        var next = phones[index + 1];

        // A secondary stress on the final -y is not realized (see EspeakWordRules), so the t before it is flapped as before an unstressed vowel (ability, identity)
        var finalY = next.Stress == 2 && next.BaseSymbol == "IY" && index + 2 == phones.Length;
        if (!next.IsVowel || (next.Stress != 0 && !finalY))
        {
            return false;
        }

        var before = index - 1;
        if (SymbolAt(phones, before) == "R" && before >= 1)
        {
            before--;
        }

        return IsVowelAt(phones, before);
    }

    /// <summary>
    ///     Tests whether the vowel at an index is the vowel of a closing inflection: <c>-ed</c> after <c>t</c> or <c>d</c>,
    ///     <c>-es</c> after a sibilant.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <param name="index">The index of the vowel.</param>
    /// <returns><see langword="true"/> when the word ends with this vowel and <c>d</c> or <c>z</c> in that environment.</returns>
    public static bool IsInflectionEnding(ReadOnlySpan<Phone> phones, int index)
    {
        if (index + 2 != phones.Length || index == 0)
        {
            return false;
        }

        var before = phones[index - 1].BaseSymbol;
        return phones[index + 1].BaseSymbol switch
        {
            "D" => before is "T" or "D",
            "Z" => before is "S" or "Z" or "SH" or "ZH" or "CH" or "JH",
            _ => false,
        };
    }
}
