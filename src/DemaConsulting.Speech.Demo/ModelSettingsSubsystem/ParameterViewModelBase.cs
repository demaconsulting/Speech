using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo.ModelSettingsSubsystem;

/// <summary>
///     Common presentation state for one tunable parameter of a selected speech model, shared by
///     the numeric, choice, and boolean concrete presenters.
/// </summary>
/// <remarks>
///     Per architecture.md's typed, self-describing parameter design, a host renders an
///     appropriate control per concrete <see cref="ISpeechModelParameter"/> implementation using
///     only a type check - it never needs prior knowledge of a specific model's parameter set.
///     This base class carries the three fields every kind shares (<see cref="Id"/>,
///     <see cref="DisplayName"/>, <see cref="Description"/>) plus <see cref="BoxedValue"/>, the
///     hook that lets <see cref="ModelSettingsViewModel"/> assemble an untyped key-value bag from
///     whatever mix of parameter kinds a model declares, without needing to know which concrete
///     presenter produced each entry.
/// </remarks>
public abstract partial class ParameterViewModelBase : ObservableObject
{
    /// <summary>
    ///     Gets the stable key used to reference this parameter in the untyped key-value bag.
    /// </summary>
    public string Id { get; }

    /// <summary>
    ///     Gets the short, human-readable label a host UI shows for this parameter.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    ///     Gets the longer, human-readable explanation shown as a tooltip or help text.
    /// </summary>
    public string Description { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ParameterViewModelBase"/> class from the
    ///     common parameter contract.
    /// </summary>
    /// <param name="parameter">
    ///     The library parameter descriptor supplying <see cref="Id"/>, <see cref="DisplayName"/>,
    ///     and <see cref="Description"/>. Must not be <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="parameter"/> is <see langword="null"/>.
    /// </exception>
    protected ParameterViewModelBase(ISpeechModelParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        Id = parameter.Id;
        DisplayName = parameter.DisplayName;
        Description = parameter.Description;
    }

    /// <summary>
    ///     Gets this parameter's current value, boxed for inclusion in the untyped key-value bag
    ///     <see cref="ModelSettingsViewModel.BuildValueBag"/> assembles.
    /// </summary>
    public abstract object BoxedValue { get; }
}
