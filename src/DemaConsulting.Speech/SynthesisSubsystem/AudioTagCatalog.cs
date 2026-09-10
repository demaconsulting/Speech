using System.Text;

namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     The single source-of-truth lookup table mapping literal Natural Language Audio Tag bracket
///     text - including every documented synonym - onto its canonical
///     <see cref="NaturalLanguageAudioTag"/> and <see cref="NaturalLanguageAudioTagKind"/>.
/// </summary>
/// <remarks>
///     <see cref="AudioTagParser"/> is the only production consumer of
///     <see cref="TryResolve"/>/<see cref="Normalize"/>; this class contains no scanning logic of
///     its own so the alias table can be reasoned about, tested, and (in a later phase) rendered
///     into a user guide independently of how bracket text is located in a larger string.
/// </remarks>
public static class AudioTagCatalog
{
    /// <summary>
    ///     Normalized alias text (see <see cref="Normalize"/>) mapped to the canonical tag and
    ///     kind it resolves to.
    /// </summary>
    private static readonly Dictionary<string, (NaturalLanguageAudioTag Tag, NaturalLanguageAudioTagKind Kind)> AliasLookup =
        BuildAliasLookup();

    /// <summary>
    ///     Every canonical tag, its kind, and its full alias list, in <see cref="NaturalLanguageAudioTag"/>
    ///     declaration order.
    /// </summary>
    public static IReadOnlyList<AudioTagDescriptor> Tags { get; } = BuildDescriptors();

    /// <summary>
    ///     Normalizes raw bracket-interior text for alias lookup: trims leading/trailing
    ///     whitespace, collapses runs of internal whitespace to a single space, and lower-cases
    ///     the result using the invariant culture.
    /// </summary>
    /// <param name="rawBracketContent">The raw text found between <c>[</c> and <c>]</c>.</param>
    /// <returns>The normalized text, or an empty string when <paramref name="rawBracketContent"/> is empty or all whitespace.</returns>
    public static string Normalize(string rawBracketContent)
    {
        var trimmed = rawBracketContent.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        // Collapse any run of whitespace (spaces, tabs, newlines) down to a single space so
        // "very   slow" and "very slow" resolve to the same alias
        var builder = new StringBuilder(trimmed.Length);
        var lastWasSpace = false;
        foreach (var c in trimmed)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    builder.Append(' ');
                }

                lastWasSpace = true;
            }
            else
            {
                builder.Append(char.ToLowerInvariant(c));
                lastWasSpace = false;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Attempts to resolve raw bracket-interior text to a canonical tag and kind.
    /// </summary>
    /// <param name="rawBracketContent">The raw text found between <c>[</c> and <c>]</c>.</param>
    /// <param name="tag">The resolved canonical tag, when this method returns <see langword="true"/>.</param>
    /// <param name="kind">The resolved tag's kind, when this method returns <see langword="true"/>.</param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="rawBracketContent"/> normalizes to a known
    ///     alias; otherwise <see langword="false"/>.
    /// </returns>
    public static bool TryResolve(string rawBracketContent, out NaturalLanguageAudioTag tag, out NaturalLanguageAudioTagKind kind)
    {
        var normalized = Normalize(rawBracketContent);
        if (normalized.Length > 0 && AliasLookup.TryGetValue(normalized, out var entry))
        {
            tag = entry.Tag;
            kind = entry.Kind;
            return true;
        }

        tag = default;
        kind = default;
        return false;
    }

    /// <summary>
    ///     Builds the immutable alias-to-(tag, kind) lookup table once, at type initialization.
    /// </summary>
    private static Dictionary<string, (NaturalLanguageAudioTag, NaturalLanguageAudioTagKind)> BuildAliasLookup()
    {
        var lookup = new Dictionary<string, (NaturalLanguageAudioTag, NaturalLanguageAudioTagKind)>();
        foreach (var descriptor in BuildDescriptors())
        {
            foreach (var alias in descriptor.Aliases)
            {
                lookup[alias] = (descriptor.Tag, descriptor.Kind);
            }
        }

        return lookup;
    }

    /// <summary>
    ///     Builds the canonical tag/kind/alias descriptor list, the single source of truth this
    ///     class exposes both for lookup and for enumeration.
    /// </summary>
    private static List<AudioTagDescriptor> BuildDescriptors() =>
    [
        Describe(NaturalLanguageAudioTag.Excited, NaturalLanguageAudioTagKind.Emotion, "excited", "excitedly"),
        Describe(NaturalLanguageAudioTag.Serious, NaturalLanguageAudioTagKind.Emotion, "serious"),
        Describe(NaturalLanguageAudioTag.Sarcastic, NaturalLanguageAudioTagKind.Emotion, "sarcastic"),
        Describe(NaturalLanguageAudioTag.Panicked, NaturalLanguageAudioTagKind.Emotion, "panicked", "shocked"),
        Describe(NaturalLanguageAudioTag.Bored, NaturalLanguageAudioTagKind.Emotion, "bored", "tired"),
        Describe(NaturalLanguageAudioTag.Sad, NaturalLanguageAudioTagKind.Emotion, "sad", "crying"),
        Describe(NaturalLanguageAudioTag.Slow, NaturalLanguageAudioTagKind.Pace, "slow"),
        Describe(NaturalLanguageAudioTag.VerySlow, NaturalLanguageAudioTagKind.Pace, "very slow"),
        Describe(NaturalLanguageAudioTag.Fast, NaturalLanguageAudioTagKind.Pace, "fast"),
        Describe(NaturalLanguageAudioTag.VeryFast, NaturalLanguageAudioTagKind.Pace, "very fast"),
        Describe(NaturalLanguageAudioTag.ShortPause, NaturalLanguageAudioTagKind.Pause, "short pause"),
        Describe(NaturalLanguageAudioTag.LongPause, NaturalLanguageAudioTagKind.Pause, "long pause"),
        Describe(NaturalLanguageAudioTag.Emphasis, NaturalLanguageAudioTagKind.Emphasis, "emphasis"),
        Describe(NaturalLanguageAudioTag.Whispers, NaturalLanguageAudioTagKind.DeliveryVolume, "whispers", "whispering"),
        Describe(NaturalLanguageAudioTag.Soft, NaturalLanguageAudioTagKind.DeliveryVolume, "soft"),
        Describe(NaturalLanguageAudioTag.Loud, NaturalLanguageAudioTagKind.DeliveryVolume, "loud", "shouting", "screams"),
        Describe(NaturalLanguageAudioTag.Breathy, NaturalLanguageAudioTagKind.DeliveryVolume, "breathy"),
        Describe(NaturalLanguageAudioTag.Laughs, NaturalLanguageAudioTagKind.NonVerbal, "laughs", "laughing", "giggles"),
        Describe(NaturalLanguageAudioTag.Sighs, NaturalLanguageAudioTagKind.NonVerbal, "sighs", "sigh"),
        Describe(NaturalLanguageAudioTag.Gasp, NaturalLanguageAudioTagKind.NonVerbal, "gasp", "inhale"),
        Describe(NaturalLanguageAudioTag.ClearsThroat, NaturalLanguageAudioTagKind.NonVerbal, "clears throat", "cough"),
        Describe(NaturalLanguageAudioTag.Snorts, NaturalLanguageAudioTagKind.NonVerbal, "snorts"),
    ];

    /// <summary>
    ///     Builds one <see cref="AudioTagDescriptor"/>. Aliases are supplied already normalized
    ///     (lowercase, single-spaced) as literal strings in <see cref="BuildDescriptors"/>.
    /// </summary>
    private static AudioTagDescriptor Describe(NaturalLanguageAudioTag tag, NaturalLanguageAudioTagKind kind, params string[] aliases) =>
        new(tag, kind, aliases);
}
