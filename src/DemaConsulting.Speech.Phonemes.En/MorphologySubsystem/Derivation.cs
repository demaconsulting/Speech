using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.MorphologySubsystem;

/// <summary>
///     A pronunciation derived for a word from the pronunciation of its stem.
/// </summary>
/// <param name="Stem">The stem word the derivation started from (for example <c>walk</c> for <c>walked</c>).</param>
/// <param name="Kind">The ending that was peeled off.</param>
/// <param name="StemPronunciations">The stem's pronunciation variants that were used.</param>
/// <param name="Pronunciations">The derived pronunciations, aligned by index with <paramref name="StemPronunciations"/>; the first is the preferred one.</param>
internal sealed record Derivation(
    string Stem,
    SuffixKind Kind,
    IReadOnlyList<Pronunciation> StemPronunciations,
    IReadOnlyList<Pronunciation> Pronunciations);
