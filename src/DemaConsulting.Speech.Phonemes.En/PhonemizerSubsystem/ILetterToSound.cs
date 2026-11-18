using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;

/// <summary>
///     Pronounces a word that is in neither the lexicon nor reachable by the morphology rules.
/// </summary>
/// <remarks>
///     The seam between the phonemizer and the out-of-vocabulary strategy: the phonemizer never knows how an
///     unknown word is pronounced, only that it must be, so an unknown word is never silent. Implementations are
///     stateless and thread-safe.
/// </remarks>
internal interface ILetterToSound
{
    /// <summary>
    ///     Pronounces a word.
    /// </summary>
    /// <param name="word">The lower-case word; letters <c>a</c> to <c>z</c> and apostrophes.</param>
    /// <param name="pronunciation">Receives the pronunciation when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the word could be pronounced.</returns>
    bool TryConvert(string word, out Pronunciation pronunciation);
}
