using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.ReadingSubsystem;

/// <summary>
///     One row of the reading table: the pronunciation of a reading's headword and where it came from.
/// </summary>
/// <param name="ReadingId">The reading id, such as <c>record.noun</c>.</param>
/// <param name="Pronunciation">The ARPAbet pronunciation of the reading's lemma.</param>
/// <param name="Source">The provenance: <c>cmu#N</c>, <c>rule:&lt;name&gt;</c> or <c>authored</c>.</param>
internal sealed record ReadingEntry(string ReadingId, Pronunciation Pronunciation, string Source);
