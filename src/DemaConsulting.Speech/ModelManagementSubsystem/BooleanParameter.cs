namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Declares a two-state tunable parameter (for example "denoise input" or "enable auto
///     punctuation"), rendered by a host as a checkbox.
/// </summary>
/// <remarks>
///     Unlike <see cref="NumericParameter"/> and <see cref="ChoiceParameter"/>, a boolean value
///     has no invalid range to reject - construction only validates the common
///     <see cref="ISpeechModelParameter"/> fields.
/// </remarks>
public sealed record BooleanParameter : ISpeechModelParameter
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="BooleanParameter"/> record.
    /// </summary>
    /// <param name="id">The stable parameter key. Must not be null, empty, or whitespace-only.</param>
    /// <param name="displayName">The human-readable label. Must not be null.</param>
    /// <param name="description">The human-readable explanation. Must not be null.</param>
    /// <param name="default">The value used when a host supplies none.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is empty or whitespace-only.</exception>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="id"/>, <paramref name="displayName"/>, or
    ///     <paramref name="description"/> is <see langword="null"/>.
    /// </exception>
    public BooleanParameter(string id, string displayName, string description, bool @default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(description);

        Id = id;
        DisplayName = displayName;
        Description = description;
        Default = @default;
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public string DisplayName { get; }

    /// <inheritdoc/>
    public string Description { get; }

    /// <summary>Gets the value used when a host supplies none.</summary>
    public bool Default { get; }
}
