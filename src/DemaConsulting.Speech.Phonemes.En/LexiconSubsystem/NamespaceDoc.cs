namespace DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

/// <summary>
///     The lexicon subsystem of the English pronunciation package: where every pronunciation the package knows
///     comes from before any rule of speech is applied.
/// </summary>
/// <remarks>
///     <para>
///     It holds the ARPAbet phone and pronunciation value types, the binary image format and the lazily loaded
///     lexicon of the embedded CMU Pronouncing Dictionary, the morphology that derives the pronunciation of an
///     inflected or derived word (and attaches contraction clitics) from the pronunciation of its stem, and the
///     frozen table that gives every heteronym reading id of the English text analysis a pronunciation.
///     </para>
///     <para>
///     Everything here is internal: the phonemizer is the only consumer, and the renderers see only what the
///     phonemizer's public result types expose. The subsystem depends on no other subsystem of the package.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
