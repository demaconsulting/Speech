using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo.ModelSettingsSubsystem;

/// <summary>
///     Presentation state for one <see cref="ChoiceParameter"/>, rendered by a host as a dropdown
///     or radio group of the declared options.
/// </summary>
/// <remarks>
///     <see cref="SelectedOption"/> always starts at the option matching the parameter's declared
///     default, resolved by value (ordinal comparison), exactly as the library itself resolves a
///     missing bag entry.
/// </remarks>
public sealed partial class ChoiceParameterViewModel : ParameterViewModelBase
{
    /// <summary>Gets the ordered, selectable options for this parameter.</summary>
    public IReadOnlyList<ChoiceParameterOption> Options { get; }

    /// <summary>The backing field for <see cref="SelectedOption"/>.</summary>
    private ChoiceParameterOption _selectedOption;

    /// <summary>
    ///     Gets or sets the option the user has chosen. A <see langword="null"/> value, or an option that is
    ///     not one of <see cref="Options"/> (compared by value, ordinal), is ignored and the previous selection
    ///     is kept, so <see cref="BoxedValue"/> is never <see langword="null"/> and never a value the
    ///     model did not declare.
    /// </summary>
    public ChoiceParameterOption SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (value is null)
            {
                return;
            }

            var member = Options.FirstOrDefault(option => string.Equals(option.Value, value.Value, StringComparison.Ordinal));
            if (member is null)
            {
                return;
            }

            SetProperty(ref _selectedOption, member);
        }
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChoiceParameterViewModel"/> class from a
    ///     library parameter descriptor, starting at the option matching its declared default.
    /// </summary>
    /// <param name="parameter">The library descriptor to present. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parameter"/> is <see langword="null"/>.</exception>
    public ChoiceParameterViewModel(ChoiceParameter parameter)
        : base(parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        Options = parameter.Options;
        _selectedOption = parameter.Options.First(
            option => string.Equals(option.Value, parameter.Default, StringComparison.Ordinal));
    }

    /// <inheritdoc/>
    public override object BoxedValue => SelectedOption.Value;
}
