using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Proofreader.Settings;

/// <summary>
///     Presentation state for the tunable parameters of the model a slot has selected.
/// </summary>
/// <remarks>
///     A generic settings panel built against <see cref="ISpeechModel.Parameters"/> alone: each
///     declared parameter is presented by the matching presenter (numeric, choice, boolean), so the
///     tool needs no knowledge of any specific model - Kokoro and Sherpa expose a voice choice, Piper
///     an integer speaker number, Chatterbox a voice choice. <see cref="BuildValueBag"/> yields the
///     untyped value bag the library loads an engine with. Not thread-safe; use from the UI thread.
/// </remarks>
public sealed partial class ModelSettingsViewModel : ObservableObject
{
    /// <summary>The message shown when no model is selected.</summary>
    public const string NoModelMessage = "Select a model to see its parameters.";

    /// <summary>The message shown when the selected model declares no parameters.</summary>
    public const string NoParametersMessage = "This model declares no tunable parameters.";

    /// <summary>Gets the presenters of the selected model's parameters, in declaration order.</summary>
    public ObservableCollection<ParameterViewModelBase> Parameters { get; } = [];

    /// <summary>Gets or sets the model whose parameters are presented, or <see langword="null"/>.</summary>
    [ObservableProperty]
    public partial ISpeechModel? Model { get; set; }

    /// <summary>Gets a value indicating whether at least one parameter is presented.</summary>
    public bool HasParameters => Parameters.Count > 0;

    /// <summary>Gets a value indicating whether the empty-state message should be shown.</summary>
    public bool IsEmpty => !HasParameters;

    /// <summary>Gets the explanation for the empty state.</summary>
    public string EmptyMessage => Model is null ? NoModelMessage : NoParametersMessage;

    /// <summary>
    ///     Collects the current value of every parameter into an untyped bag keyed by parameter id.
    /// </summary>
    /// <returns>A snapshot; values are string, double, or bool.</returns>
    public IReadOnlyDictionary<string, object> BuildValueBag() =>
        Parameters.ToDictionary(parameter => parameter.Id, parameter => parameter.BoxedValue, StringComparer.Ordinal);

    /// <summary>
    ///     Re-applies previously captured values to the presented parameters.
    /// </summary>
    /// <param name="values">The values by parameter id; unknown ids, mismatched types, and unknown options are ignored.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is <see langword="null"/>.</exception>
    public void RestoreValues(IReadOnlyDictionary<string, object> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        foreach (var parameter in Parameters)
        {
            if (!values.TryGetValue(parameter.Id, out var value))
            {
                continue;
            }

            switch (parameter)
            {
                case ChoiceParameterViewModel choice when value is string text:
                    choice.SelectedOption = choice.Options.FirstOrDefault(
                        option => string.Equals(option.Value, text, StringComparison.Ordinal))!;
                    break;
                case NumericParameterViewModel numeric when value is double number:
                    numeric.Value = number;
                    break;
                case BooleanParameterViewModel boolean when value is bool flag:
                    boolean.Value = flag;
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>
    ///     Rebuilds the presented parameters whenever a new model is assigned.
    /// </summary>
    /// <param name="value">The newly assigned model.</param>
    partial void OnModelChanged(ISpeechModel? value)
    {
        // Rows are rebuilt, not merged: a new model's parameter set is unrelated to the previous one's.
        Parameters.Clear();
        foreach (var parameter in value?.Parameters ?? [])
        {
            ParameterViewModelBase? presenter = parameter switch
            {
                NumericParameter numeric => new NumericParameterViewModel(numeric),
                ChoiceParameter choice => new ChoiceParameterViewModel(choice),
                BooleanParameter boolean => new BooleanParameterViewModel(boolean),
                _ => null,
            };

            if (presenter is not null)
            {
                Parameters.Add(presenter);
            }
        }

        OnPropertyChanged(nameof(HasParameters));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyMessage));
    }
}
