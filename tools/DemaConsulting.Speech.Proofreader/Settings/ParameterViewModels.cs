using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Proofreader.Settings;

/// <summary>
///     Common presentation state for one tunable parameter of a synthesis model.
/// </summary>
/// <remarks>
///     A small copy of the Demo's settings presenters (the Demo's types are not reusable from this
///     tool): the library's parameters are typed and self-describing, so a host renders a control per
///     concrete <see cref="ISpeechModelParameter"/> kind using only a type check. <see cref="BoxedValue"/>
///     lets <see cref="ModelSettingsViewModel"/> assemble the untyped value bag without knowing the kind.
/// </remarks>
public abstract partial class ParameterViewModelBase : ObservableObject
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ParameterViewModelBase"/> class.
    /// </summary>
    /// <param name="parameter">The library parameter descriptor. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parameter"/> is <see langword="null"/>.</exception>
    protected ParameterViewModelBase(ISpeechModelParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        Id = parameter.Id;
        DisplayName = parameter.DisplayName;
        Description = parameter.Description;
    }

    /// <summary>Gets the stable key of this parameter in the value bag.</summary>
    public string Id { get; }

    /// <summary>Gets the short label shown to the user.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the longer explanation shown as help text.</summary>
    public string Description { get; }

    /// <summary>Gets the current value, boxed for the untyped value bag.</summary>
    public abstract object BoxedValue { get; }
}

/// <summary>
///     Presentation state for a <see cref="NumericParameter"/>: a slider, or a numeric up-down for integers.
/// </summary>
/// <remarks>
///     The value is clamped to the declared range on every write (and rounded for integer parameters),
///     so no bound control can push the model outside what it declared it honors.
/// </remarks>
public sealed partial class NumericParameterViewModel : ParameterViewModelBase
{
    /// <summary>The backing field for <see cref="Value"/>.</summary>
    private double _value;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NumericParameterViewModel"/> class at its default.
    /// </summary>
    /// <param name="parameter">The descriptor. Must not be <see langword="null"/>.</param>
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

    /// <summary>Gets the smallest accepted value.</summary>
    public double Minimum { get; }

    /// <summary>Gets the largest accepted value.</summary>
    public double Maximum { get; }

    /// <summary>Gets the smallest meaningful increment.</summary>
    public double Step { get; }

    /// <summary>Gets the optional unit label, or <see langword="null"/>.</summary>
    public string? Unit { get; }

    /// <summary>Gets a value indicating whether the parameter is a whole number (shown with a numeric up-down).</summary>
    public bool IsInteger { get; }

    /// <summary>
    ///     Gets or sets the current value, clamped to the declared range; a non-finite write is ignored.
    /// </summary>
    public double Value
    {
        get => _value;
        set
        {
            if (!double.IsFinite(value))
            {
                return;
            }

            var clamped = Math.Clamp(value, Minimum, Maximum);
            SetProperty(ref _value, IsInteger ? Math.Round(clamped, MidpointRounding.AwayFromZero) : clamped);
        }
    }

    /// <inheritdoc/>
    public override object BoxedValue => Value;
}

/// <summary>
///     Presentation state for a <see cref="ChoiceParameter"/>: a dropdown of the declared options.
/// </summary>
public sealed partial class ChoiceParameterViewModel : ParameterViewModelBase
{
    /// <summary>The backing field for <see cref="SelectedOption"/>.</summary>
    private ChoiceParameterOption _selectedOption;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChoiceParameterViewModel"/> class at its default option.
    /// </summary>
    /// <param name="parameter">The descriptor. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parameter"/> is <see langword="null"/>.</exception>
    public ChoiceParameterViewModel(ChoiceParameter parameter)
        : base(parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        Options = parameter.Options;
        _selectedOption = parameter.Options.First(
            option => string.Equals(option.Value, parameter.Default, StringComparison.Ordinal));
    }

    /// <summary>Gets the selectable options.</summary>
    public IReadOnlyList<ChoiceParameterOption> Options { get; }

    /// <summary>
    ///     Gets or sets the chosen option; <see langword="null"/> or an unknown option is ignored.
    /// </summary>
    public ChoiceParameterOption SelectedOption
    {
        get => _selectedOption;
        set
        {
            var member = value is null
                ? null
                : Options.FirstOrDefault(option => string.Equals(option.Value, value.Value, StringComparison.Ordinal));
            if (member is not null)
            {
                SetProperty(ref _selectedOption, member);
            }
        }
    }

    /// <inheritdoc/>
    public override object BoxedValue => SelectedOption.Value;
}

/// <summary>
///     Presentation state for a <see cref="BooleanParameter"/>: a checkbox.
/// </summary>
public sealed partial class BooleanParameterViewModel : ParameterViewModelBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="BooleanParameterViewModel"/> class at its default.
    /// </summary>
    /// <param name="parameter">The descriptor. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parameter"/> is <see langword="null"/>.</exception>
    public BooleanParameterViewModel(BooleanParameter parameter)
        : base(parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        Value = parameter.Default;
    }

    /// <summary>Gets or sets the current value.</summary>
    [ObservableProperty]
    public partial bool Value { get; set; }

    /// <inheritdoc/>
    public override object BoxedValue => Value;
}
