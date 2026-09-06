using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo.ModelSettingsSubsystem;

/// <summary>
///     Presentation state for one <see cref="BooleanParameter"/>, rendered by a host as a
///     checkbox or toggle.
/// </summary>
public sealed partial class BooleanParameterViewModel : ParameterViewModelBase
{
    /// <summary>
    ///     Gets or sets this parameter's current value.
    /// </summary>
    [ObservableProperty]
    private bool _value;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BooleanParameterViewModel"/> class from a
    ///     library parameter descriptor, starting at its declared default.
    /// </summary>
    /// <param name="parameter">The library descriptor to present. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parameter"/> is <see langword="null"/>.</exception>
    public BooleanParameterViewModel(BooleanParameter parameter)
        : base(parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        _value = parameter.Default;
    }

    /// <inheritdoc/>
    public override object BoxedValue => Value;
}
