namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Declares a bounded tunable parameter, either continuous (for example speaking rate or
///     pitch, rendered by a host as a slider) or - when <see cref="IsInteger"/> is
///     <see langword="true"/> - an inherently whole-number value (for example a discrete
///     speaker index, rendered by a host as a numeric up-down so a fractional value can never
///     be selected).
/// </summary>
/// <remarks>
///     Validation happens eagerly in the constructor so an invalid descriptor - one whose
///     bounds could never be honored by any UI control - is rejected the moment it is created,
///     never silently accepted by a model's backing class.
/// </remarks>
public sealed record NumericParameter : ISpeechModelParameter
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="NumericParameter"/> record, validating
    ///     every field eagerly. See the type-level remarks for the exact rules enforced.
    /// </summary>
    /// <param name="id">The stable parameter key. Must not be null, empty, or whitespace-only.</param>
    /// <param name="displayName">The human-readable label. Must not be null.</param>
    /// <param name="description">The human-readable explanation. Must not be null.</param>
    /// <param name="bounds">
    ///     The parameter's minimum, maximum, step, and default value, grouped into a single
    ///     <see cref="NumericParameterBounds"/> to keep this constructor's own parameter count
    ///     small. See <see cref="Minimum"/>, <see cref="Maximum"/>, <see cref="Step"/>, and
    ///     <see cref="Default"/> for the individual constraints each value must satisfy.
    /// </param>
    /// <param name="unit">
    ///     An optional short unit label to display alongside the value (for example "%" or
    ///     "words/min"), or <see langword="null"/> when the parameter is dimensionless.
    /// </param>
    /// <param name="isInteger">
    ///     Whether this parameter is an inherently whole-number value (for example a discrete
    ///     index) with no fractional meaning, so a host must render it with a control that can
    ///     never select a fractional value (for example a numeric up-down) rather than a
    ///     continuous slider. Defaults to <see langword="false"/> for a continuous parameter
    ///     (for example a speaking-rate or volume ratio). When <see langword="true"/>, every
    ///     value in <paramref name="bounds"/> must be a whole number.
    /// </param>
    /// <exception cref="ArgumentException">
    ///     Thrown when any value in <paramref name="bounds"/> is not finite, when its minimum
    ///     exceeds its maximum, when its step is not strictly positive, when its default falls
    ///     outside <c>[minimum, maximum]</c>, or when <paramref name="isInteger"/> is
    ///     <see langword="true"/> and any value in <paramref name="bounds"/> is not a whole
    ///     number.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="id"/>, <paramref name="displayName"/>, or
    ///     <paramref name="description"/> is <see langword="null"/>.
    /// </exception>
    public NumericParameter(
        string id,
        string displayName,
        string description,
        NumericParameterBounds bounds,
        string? unit = null,
        bool isInteger = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(description);

        var (minimum, maximum, step, @default) = bounds;

        // Reject non-finite bounds up front - NaN/infinity could never be rendered by a slider
        // or validated meaningfully by a model's own clamping logic.
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) ||
            !double.IsFinite(step) || !double.IsFinite(@default))
        {
            throw new ArgumentException(
                "Numeric parameter minimum, maximum, step, and default must all be finite values.",
                nameof(bounds));
        }

        if (minimum > maximum)
        {
            throw new ArgumentException(
                $"Minimum ({minimum}) must not exceed maximum ({maximum}).",
                nameof(bounds));
        }

        if (step <= 0)
        {
            throw new ArgumentException(
                $"Step ({step}) must be strictly greater than zero.",
                nameof(bounds));
        }

        if (@default < minimum || @default > maximum)
        {
            throw new ArgumentException(
                $"Default ({@default}) must be within [{minimum}, {maximum}].",
                nameof(bounds));
        }

        // An integer parameter (for example a discrete speaker index) has no fractional
        // meaning - reject a bound that could never be honored by a whole-number-only control
        // the moment a model's backing class declares it, rather than discovering a fractional
        // value later at render or use time.
        if (isInteger &&
            (!double.IsInteger(minimum) ||
             !double.IsInteger(maximum) ||
             !double.IsInteger(step) ||
             !double.IsInteger(@default)))
        {
            throw new ArgumentException(
                "Minimum, maximum, step, and default must all be whole numbers when isInteger is true.",
                nameof(isInteger));
        }

        Id = id;
        DisplayName = displayName;
        Description = description;
        Minimum = minimum;
        Maximum = maximum;
        Step = step;
        Default = @default;
        Unit = unit;
        IsInteger = isInteger;
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public string DisplayName { get; }

    /// <inheritdoc/>
    public string Description { get; }

    /// <summary>Gets the smallest value a host may supply for this parameter.</summary>
    public double Minimum { get; }

    /// <summary>Gets the largest value a host may supply for this parameter.</summary>
    public double Maximum { get; }

    /// <summary>Gets the smallest meaningful increment between adjacent values.</summary>
    public double Step { get; }

    /// <summary>Gets the value used when a host supplies none.</summary>
    public double Default { get; }

    /// <summary>
    ///     Gets the optional short unit label to display alongside the value, or
    ///     <see langword="null"/> when the parameter is dimensionless.
    /// </summary>
    public string? Unit { get; }

    /// <summary>
    ///     Gets whether this parameter is an inherently whole-number value (for example a
    ///     discrete index) with no fractional meaning, so a host must render it with a control
    ///     that can never select a fractional value (for example a numeric up-down) rather than
    ///     a continuous slider. Defaults to <see langword="false"/> for a continuous parameter.
    /// </summary>
    public bool IsInteger { get; }
}
