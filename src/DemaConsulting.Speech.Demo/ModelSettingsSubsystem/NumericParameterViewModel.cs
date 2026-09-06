using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo.ModelSettingsSubsystem;

/// <summary>
///     Presentation state for one <see cref="NumericParameter"/>, rendered by a host as a slider
///     or numeric input.
/// </summary>
/// <remarks>
///     The current value starts at the parameter's declared default and is clamped to
///     <see cref="Minimum"/>/<see cref="Maximum"/> on every write, so a control bound to
///     <see cref="Value"/> can never push this parameter outside the range the model declared it
///     could honor - even if the bound control itself allows a wider range (for example, a text
///     entry) or a test sets the value directly.
/// </remarks>
public sealed partial class NumericParameterViewModel : ParameterViewModelBase
{
    /// <summary>Gets the smallest value this parameter accepts.</summary>
    public double Minimum { get; }

    /// <summary>Gets the largest value this parameter accepts.</summary>
    public double Maximum { get; }

    /// <summary>Gets the smallest meaningful increment between adjacent values.</summary>
    public double Step { get; }

    /// <summary>
    ///     Gets the optional short unit label to display alongside the value, or
    ///     <see langword="null"/> when the parameter is dimensionless.
    /// </summary>
    public string? Unit { get; }

    /// <summary>
    ///     Gets whether this parameter is an inherently whole-number value (for example a
    ///     discrete index) with no fractional meaning. When <see langword="true"/>, a host must
    ///     render this parameter with a control that can never select a fractional value (for
    ///     example a numeric up-down) rather than a continuous slider, and <see cref="Value"/>
    ///     rounds every written value to the nearest whole number.
    /// </summary>
    public bool IsInteger { get; }

    /// <summary>The backing field for <see cref="Value"/>.</summary>
    private double _value;

    /// <summary>
    ///     Gets or sets this parameter's current value, clamped to
    ///     <c>[<see cref="Minimum"/>, <see cref="Maximum"/>]</c> on every write so no bound
    ///     control can push this parameter outside the range the model declared it could honor.
    ///     When <see cref="IsInteger"/> is <see langword="true"/>, the value is additionally
    ///     rounded to the nearest whole number, so no code path - test, XAML binding, or
    ///     otherwise - can leave this presenter holding a fractional value for an integer
    ///     parameter.
    /// </summary>
    public double Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, Minimum, Maximum);
            var resolved = IsInteger ? Math.Round(clamped, MidpointRounding.AwayFromZero) : clamped;
            SetProperty(ref _value, resolved);
        }
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="NumericParameterViewModel"/> class from a
    ///     library parameter descriptor, starting at its declared default.
    /// </summary>
    /// <param name="parameter">The library descriptor to present. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parameter"/> is <see langword="null"/>.</exception>
    public NumericParameterViewModel(NumericParameter parameter)
        : base(parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        Minimum = parameter.Minimum;
        Maximum = parameter.Maximum;
        Step = parameter.Step;
        Unit = parameter.Unit;
        IsInteger = parameter.IsInteger;
        _value = parameter.Default;
    }

    /// <inheritdoc/>
    public override object BoxedValue => Value;
}
