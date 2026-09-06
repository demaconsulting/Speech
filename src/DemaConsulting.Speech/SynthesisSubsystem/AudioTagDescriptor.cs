namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     The kind and canonical display information for one <see cref="NaturalLanguageAudioTag"/>,
///     as reported by <see cref="AudioTagCatalog.Tags"/>.
/// </summary>
/// <param name="Tag">The canonical tag value.</param>
/// <param name="Kind">The kind this tag belongs to.</param>
/// <param name="Aliases">
///     Every bracket-text alias (already normalized: lowercase, single-spaced) that resolves to
///     this tag, including the canonical form itself. Never empty.
/// </param>
/// <remarks>
///     This record is a read-only reporting shape only; it carries no parsing behavior of its
///     own. It exists so a host UI (for example a demo application's "example tags" hint) or
///     user-guide generation can enumerate the full vocabulary without hard-coding it separately
///     from <see cref="AudioTagCatalog"/>'s alias table.
/// </remarks>
public sealed record AudioTagDescriptor(
    NaturalLanguageAudioTag Tag,
    NaturalLanguageAudioTagKind Kind,
    IReadOnlyList<string> Aliases);
