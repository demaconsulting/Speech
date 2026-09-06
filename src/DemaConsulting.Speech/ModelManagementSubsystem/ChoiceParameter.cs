namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Declares one selectable option of a <see cref="ChoiceParameter"/>: a stable, model-facing
///     value paired with the human-readable label a host UI displays for it.
/// </summary>
/// <remarks>
///     Validation happens eagerly in the constructor for the same reason as the containing
///     <see cref="ChoiceParameter"/>: an invalid option is rejected the moment it is created.
/// </remarks>
public sealed record ChoiceParameterOption
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ChoiceParameterOption"/> record.
    /// </summary>
    /// <param name="value">
    ///     The stable, model-facing value supplied in the untyped key-value bag when this option
    ///     is selected. Must not be null, empty, or whitespace-only.
    /// </param>
    /// <param name="label">The human-readable label a host UI displays for this option. Must not be null.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is empty or whitespace-only.</exception>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="value"/> or <paramref name="label"/> is <see langword="null"/>.
    /// </exception>
    public ChoiceParameterOption(string value, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentNullException.ThrowIfNull(label);

        Value = value;
        Label = label;
    }

    /// <summary>
    ///     Gets the stable, model-facing value supplied in the untyped key-value bag when this
    ///     option is selected.
    /// </summary>
    public string Value { get; }

    /// <summary>
    ///     Gets the human-readable label a host UI displays for this option.
    /// </summary>
    public string Label { get; }
}

/// <summary>
///     Declares a closed-set tunable parameter (for example a speaker/voice selection), rendered
///     by a host as a dropdown of the declared options.
/// </summary>
/// <remarks>
///     Validation happens eagerly in the constructor so an invalid descriptor - an empty option
///     list, duplicate option values, or a default that matches no declared option - is rejected
///     the moment it is created, never silently accepted by a model's backing class.
/// </remarks>
public sealed record ChoiceParameter : ISpeechModelParameter
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ChoiceParameter"/> record, validating
    ///     every field eagerly. See the type-level remarks for the exact rules enforced.
    /// </summary>
    /// <param name="id">The stable parameter key. Must not be null, empty, or whitespace-only.</param>
    /// <param name="displayName">The human-readable label. Must not be null.</param>
    /// <param name="description">The human-readable explanation. Must not be null.</param>
    /// <param name="options">
    ///     The ordered, selectable options for this parameter. Must contain at least one entry
    ///     with no two entries sharing the same <see cref="ChoiceParameterOption.Value"/>; order
    ///     is preserved as the order a host renders the options in.
    /// </param>
    /// <param name="default">
    ///     The value used when a host supplies none. Must match one declared option's
    ///     <see cref="ChoiceParameterOption.Value"/> exactly (ordinal comparison).
    /// </param>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="options"/> is empty, when two or more entries share the
    ///     same <see cref="ChoiceParameterOption.Value"/>, or when <paramref name="default"/>
    ///     does not match any declared option's value.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="id"/>, <paramref name="displayName"/>,
    ///     <paramref name="description"/>, <paramref name="options"/>, or
    ///     <paramref name="default"/> is <see langword="null"/>.
    /// </exception>
    public ChoiceParameter(
        string id,
        string displayName,
        string description,
        IReadOnlyList<ChoiceParameterOption> options,
        string @default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(@default);

        if (options.Count == 0)
        {
            throw new ArgumentException(
                "A choice parameter must declare at least one option.",
                nameof(options));
        }

        // Two options sharing the same value would make a supplied bag entry ambiguous - reject
        // that up front rather than letting a model's own lookup logic silently pick one.
        var distinctValueCount = options
            .Select(option => option.Value)
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (distinctValueCount != options.Count)
        {
            throw new ArgumentException(
                "A choice parameter must not declare two options with the same value.",
                nameof(options));
        }

        if (!options.Any(option => string.Equals(option.Value, @default, StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                $"Default value '{@default}' must match one of the declared options.",
                nameof(@default));
        }

        Id = id;
        DisplayName = displayName;
        Description = description;
        Options = options;
        Default = @default;
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public string DisplayName { get; }

    /// <inheritdoc/>
    public string Description { get; }

    /// <summary>Gets the ordered, selectable options for this parameter.</summary>
    public IReadOnlyList<ChoiceParameterOption> Options { get; }

    /// <summary>Gets the value used when a host supplies none.</summary>
    public string Default { get; }
}
