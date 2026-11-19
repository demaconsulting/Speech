namespace DemaConsulting.Speech.Phonemes.En.RenderingSubsystem;

/// <summary>
///     The rendering subsystem of the English pronunciation package: the renderers that turn the ARPAbet items of a
///     <c>PhonemizedText</c> into the phoneme string a particular voice model expects (the espeak-style IPA of the
///     Piper voice, the misaki-style symbols of Kokoro).
/// </summary>
/// <remarks>
///     Depends on the phonemizer subsystem's result types and on the lexicon subsystem's phone model; engines
///     depend on this one. The mapping tables are authored from phonetics and from each model's own symbol
///     inventory; the committed vocabulary-coverage tests prove that no symbol outside that inventory is emitted.
/// </remarks>
internal static class NamespaceDoc
{
}
