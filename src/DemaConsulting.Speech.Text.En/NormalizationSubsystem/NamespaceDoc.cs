namespace DemaConsulting.Speech.Text.En.NormalizationSubsystem;

/// <summary>
///     English text normalization for DemaConsulting.Speech: replacement-span descriptions that
///     say how a span of the original text is spoken (an abbreviation in full, a symbol as
///     words, an initialism letter by letter) without ever rewriting the text itself.
/// </summary>
/// <remarks>
///     The subsystem is internal apart from two small vocabularies,
///     <see cref="NormalizationKind"/> and <see cref="CliticKind"/>, which the public analysis
///     result reports. <c>EnglishTextAnalyzer</c> (in the analysis subsystem) drives it: it hands
///     the normalizer the tokens of the text and turns the aligned result into <c>en.norm</c>
///     annotations and reconciled sentence breaks. Numbers, ordinals, years, currency, percent,
///     units, times and dates (en-US) are normalized by this release; URLs, markup, Roman
///     numerals arrive in later releases (heteronym readings are produced by the heteronym
///     subsystem, not by normalization). Host-supplied pronunciation
///     overrides are resolved to per-token directives by the analysis subsystem and applied here
///     before every built-in rule; letter-name replacements (initialisms, initials, meridiems)
///     are marked spelled and become <c>en.spell</c> annotations.
/// </remarks>
internal static class NamespaceDoc
{
}
