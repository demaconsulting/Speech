namespace DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

/// <summary>
///     One word of the lexicon with all of its pronunciation variants.
/// </summary>
/// <param name="Word">The lower-case headword (letters a to z and apostrophes).</param>
/// <param name="Pronunciations">The variants in dictionary order: the first is the default, later ones are alternatives. Never empty.</param>
internal readonly record struct LexiconEntry(string Word, IReadOnlyList<Pronunciation> Pronunciations);
