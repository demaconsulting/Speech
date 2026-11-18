using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;

/// <summary>
///     TEMPORARY PLACEHOLDER for the out-of-vocabulary letter-to-sound conversion: speaks a word letter by letter,
///     by letter name.
/// </summary>
/// <remarks>
///     <para>
///     This exists only so that an unknown word is audible and reported (the phonemizer flags it as out of
///     vocabulary) instead of silent. It is not a pronunciation model and is replaced by the real letter-to-sound
///     conversion when that is built (the next work chunk); nothing may depend on the way it pronounces.
///     </para>
///     <para>Stateless and thread-safe.</para>
/// </remarks>
internal sealed class SpellingLetterToSound : ILetterToSound
{
    /// <summary>Gets the shared instance.</summary>
    public static SpellingLetterToSound Instance { get; } = new();

    /// <inheritdoc/>
    public bool TryConvert(string word, out Pronunciation pronunciation)
    {
        ArgumentNullException.ThrowIfNull(word);

        // Speak every letter by name; apostrophes carry no sound
        var result = Pronunciation.Empty;
        foreach (var letter in word)
        {
            if (LetterNames.TryGet(letter, out var name))
            {
                result = result.Append(name.Phones);
            }
        }

        pronunciation = result;
        return result.Count > 0;
    }
}
