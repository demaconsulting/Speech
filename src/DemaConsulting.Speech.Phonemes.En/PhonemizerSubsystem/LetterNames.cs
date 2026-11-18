using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;

/// <summary>
///     The names of the 26 letters of the English alphabet as ARPAbet pronunciations.
/// </summary>
/// <remarks>
///     Used to speak an initialism the text analysis marked for spelling (<c>F B I</c>) and by the temporary
///     letter-to-sound placeholder. The names are written out here rather than read from the lexicon because the
///     lexicon's entry for a single letter is a word (<c>a</c> is the article first), and a spelled letter must
///     never be read as a word.
/// </remarks>
internal static class LetterNames
{
    /// <summary>The names, indexed by the letter's position in the alphabet.</summary>
    private static readonly Pronunciation[] Names =
    [
        Pronunciation.Parse("EY1"),
        Pronunciation.Parse("B IY1"),
        Pronunciation.Parse("S IY1"),
        Pronunciation.Parse("D IY1"),
        Pronunciation.Parse("IY1"),
        Pronunciation.Parse("EH1 F"),
        Pronunciation.Parse("JH IY1"),
        Pronunciation.Parse("EY1 CH"),
        Pronunciation.Parse("AY1"),
        Pronunciation.Parse("JH EY1"),
        Pronunciation.Parse("K EY1"),
        Pronunciation.Parse("EH1 L"),
        Pronunciation.Parse("EH1 M"),
        Pronunciation.Parse("EH1 N"),
        Pronunciation.Parse("OW1"),
        Pronunciation.Parse("P IY1"),
        Pronunciation.Parse("K Y UW1"),
        Pronunciation.Parse("AA1 R"),
        Pronunciation.Parse("EH1 S"),
        Pronunciation.Parse("T IY1"),
        Pronunciation.Parse("Y UW1"),
        Pronunciation.Parse("V IY1"),
        Pronunciation.Parse("D AH1 B AH0 L Y UW0"),
        Pronunciation.Parse("EH1 K S"),
        Pronunciation.Parse("W AY1"),
        Pronunciation.Parse("Z IY1"),
    ];

    /// <summary>
    ///     Gets the name of a letter.
    /// </summary>
    /// <param name="letter">An ASCII letter, either case.</param>
    /// <param name="name">Receives the name when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> for an ASCII letter.</returns>
    public static bool TryGet(char letter, out Pronunciation name)
    {
        var index = char.ToLowerInvariant(letter) - 'a';
        if (index is >= 0 and < 26)
        {
            name = Names[index];
            return true;
        }

        name = Pronunciation.Empty;
        return false;
    }
}
