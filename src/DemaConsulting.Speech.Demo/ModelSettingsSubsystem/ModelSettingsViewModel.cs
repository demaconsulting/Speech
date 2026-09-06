using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo.ModelSettingsSubsystem;

/// <summary>
///     Presentation state for the demo's per-model tunable-parameter settings, embedded within
///     the synthesis and recognition panels for whichever model is currently selected there.
/// </summary>
/// <remarks>
///     This ViewModel proves that a host can build a completely generic parameter-settings UI
///     against <see cref="ISpeechModel.Parameters"/> alone, using only a type check against the
///     three concrete <see cref="ISpeechModelParameter"/> implementations
///     (<see cref="NumericParameter"/>, <see cref="ChoiceParameter"/>, <see cref="BooleanParameter"/>)
///     - it never needs prior knowledge of a specific model's declared parameter set.
///     <para>
///     A model with no parameters and no model at all are both honest, expected states (the
///     latter before the host has selected one, or on a machine with none installed) and are
///     rendered as an explanatory message rather than a blank panel a user would read as a bug,
///     matching the empty-state precedent set by the model catalog panel.
///     </para>
///     <para>
///     <see cref="BuildValueBag"/> assembles the untyped key-value bag this library uses
///     as the interface between a host's settings UI and per-model synthesis/recognition
///     parameter handling. As of this pass, the synthesis panel's
///     <c>SynthesisPanelViewModel.PlayAsync</c> passes this bag to
///     <c>ISynthesizerSessionFactory.Create</c>, which forwards it to the library's
///     <c>SpeechSynthesizerFactory.Create</c> and, from there, to a multi-speaker model's own
///     <c>ISynthesisModel.ResolveSpeakerId</c> hook - so a selected value (for example
///     <see cref="SherpaOnnxKokoroEnglishSynthesisModel"/>'s voice choice) now reaches a real
///     synthesis call. The recognition panel's <c>ISpeechRecognizer</c> contract still accepts
///     no such bag on any per-call member, so for recognition this bag remains built and fully
///     exercised by unit tests only, with no real consumer yet - this is stated plainly here
///     rather than silently implied.
///     </para>
///     <para>
///     Not thread-safe: expected to be used from the UI thread.
///     </para>
/// </remarks>
public sealed partial class ModelSettingsViewModel : ObservableObject
{
    /// <summary>The message shown when no model is currently selected.</summary>
    public const string NoModelMessage =
        "Select an installed model to view its tunable parameters.";

    /// <summary>The message shown when the selected model declares no tunable parameters.</summary>
    public const string NoParametersMessage =
        "This model declares no tunable parameters.";

    /// <summary>
    ///     Gets the presenters for the selected model's declared parameters, in declaration
    ///     order.
    /// </summary>
    public ObservableCollection<ParameterViewModelBase> Parameters { get; } = [];

    /// <summary>
    ///     Gets or sets the model whose parameters are presented, or <see langword="null"/> when
    ///     no model is selected.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasModel))]
    [NotifyPropertyChangedFor(nameof(HasParameters))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(EmptyMessage))]
    private ISpeechModel? _model;

    /// <summary>
    ///     Gets a value indicating whether a model is currently selected.
    /// </summary>
    public bool HasModel => Model is not null;

    /// <summary>
    ///     Gets a value indicating whether the selected model declares at least one tunable
    ///     parameter.
    /// </summary>
    public bool HasParameters => Parameters.Count > 0;

    /// <summary>
    ///     Gets a value indicating whether the explanatory empty-state message should be shown
    ///     in place of the parameter list.
    /// </summary>
    public bool IsEmpty => !HasModel || !HasParameters;

    /// <summary>
    ///     Gets the explanatory message for the current empty state: no model selected, or a
    ///     selected model with no declared parameters.
    /// </summary>
    public string EmptyMessage => HasModel ? NoParametersMessage : NoModelMessage;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ModelSettingsViewModel"/> class.
    /// </summary>
    /// <param name="model">
    ///     The initially selected model, or <see langword="null"/> to start with no model
    ///     selected - the honest starting state before a host has chosen one.
    /// </param>
    public ModelSettingsViewModel(ISpeechModel? model = null)
    {
        // Assign the backing field directly (rather than the Model property) so construction
        // performs exactly one rebuild regardless of whether a model was supplied.
        _model = model;
        Rebuild();
    }

    /// <summary>
    ///     Rebuilds the presented parameters whenever a new model is assigned, including a
    ///     <see langword="null"/> model.
    /// </summary>
    /// <param name="value">The newly assigned model.</param>
    partial void OnModelChanged(ISpeechModel? value) => Rebuild();

    /// <summary>
    ///     Assembles the current value of every presented parameter into an untyped key-value bag
    ///     keyed by each parameter's <see cref="ISpeechModelParameter.Id"/>.
    /// </summary>
    /// <returns>
    ///     A snapshot dictionary of parameter id to boxed current value. Empty when no model is
    ///     selected or the selected model declares no parameters.
    /// </returns>
    /// <remarks>
    ///     See the type-level remarks: as of this pass, the synthesis panel forwards this bag to
    ///     a real synthesis call (via <c>ISynthesizerSessionFactory.Create</c>); the recognition
    ///     panel still has no such consumer.
    /// </remarks>
    public IReadOnlyDictionary<string, object> BuildValueBag() =>
        Parameters.ToDictionary(parameter => parameter.Id, parameter => parameter.BoxedValue);

    /// <summary>
    ///     Rebuilds <see cref="Parameters"/> from <see cref="Model"/>'s currently declared
    ///     parameters, pattern-matching each one to its concrete presenter.
    /// </summary>
    /// <remarks>
    ///     Rows are rebuilt rather than merged because a newly selected model's parameter set is
    ///     unrelated to the previous one's; keeping a stale row alive across a model change would
    ///     let the panel offer a control for a parameter the new model does not declare. A
    ///     parameter kind this presenter does not recognize is silently skipped rather than
    ///     thrown, mirroring the library's own "silently ignores unrecognized" tolerance for a
    ///     parameter set built against a different model.
    /// </remarks>
    private void Rebuild()
    {
        Parameters.Clear();

        if (Model is not null)
        {
            foreach (var parameter in Model.Parameters)
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
        }

        OnPropertyChanged(nameof(HasModel));
        OnPropertyChanged(nameof(HasParameters));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyMessage));
    }
}
