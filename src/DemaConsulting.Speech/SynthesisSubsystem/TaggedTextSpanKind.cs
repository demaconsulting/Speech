namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Discriminates the two kinds of <see cref="TaggedTextSpan"/> produced by
///     <see cref="AudioTagParser.Parse"/>.
/// </summary>
public enum TaggedTextSpanKind
{
    /// <summary>
    ///     A run of ordinary narration text - including any unrecognized or malformed bracket
    ///     content, passed through literally per architecture.md's "never worse than plain
    ///     narration" guarantee.
    /// </summary>
    PlainText,

    /// <summary>A single recognized Natural Language Audio Tag.</summary>
    Tag,
}
