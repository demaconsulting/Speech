using DemaConsulting.Speech.Demo.ModelSettingsSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo.Tests.ModelSettingsSubsystem;

/// <summary>
///     Unit tests for the parameter presenters: <see cref="ParameterViewModelBase"/>,
///     <see cref="NumericParameterViewModel"/>, <see cref="ChoiceParameterViewModel"/> and
///     <see cref="BooleanParameterViewModel"/>.
/// </summary>
/// <remarks>
///     The presenters' default-value, clamping, rounding, selection, toggling and null-descriptor
///     tests live in <see cref="ModelSettingsViewModelTests"/>; this class holds the additional
///     presenter tests.
/// </remarks>
public class ParameterViewModelsTests
{
    /// <summary>A continuous numeric parameter whose default is not on a whole number.</summary>
    private static readonly NumericParameter ContinuousParam =
        new("speed", "Speed", "Speaking rate.", new NumericParameterBounds(0.5, 2.0, 0.1, 1.0), "x");

    /// <summary>A choice parameter whose declared default is not its first option.</summary>
    private static readonly ChoiceParameter NonFirstDefaultParam = new(
        "voice",
        "Voice",
        "Speaker voice.",
        [
            new ChoiceParameterOption("alto", "Alto"),
            new ChoiceParameterOption("tenor", "Tenor"),
            new ChoiceParameterOption("bass", "Bass"),
        ],
        "tenor");

    /// <summary>A boolean parameter that defaults to <see langword="false"/>.</summary>
    private static readonly BooleanParameter FalseDefaultParam =
        new("denoise", "Denoise", "Removes background noise.", false);

    /// <summary>
    ///     Proves that a choice presenter selects the option whose value matches the declared
    ///     default even when that option is not the first one declared.
    /// </summary>
    [Fact]
    public void ChoiceParameterViewModel_Constructor_NonFirstDefault_SelectsOptionMatchingDefaultByValue()
    {
        // Act: build a presenter whose default is the second of three options
        var presenter = new ChoiceParameterViewModel(NonFirstDefaultParam);

        // Assert: the default-matching option, not the first, is selected
        Assert.Equal("tenor", presenter.SelectedOption.Value);
        Assert.Equal("tenor", presenter.BoxedValue);
        Assert.Equal(3, presenter.Options.Count);
    }

    /// <summary>
    ///     Proves that a continuous numeric presenter keeps a fractional value unrounded.
    /// </summary>
    [Fact]
    public void NumericParameterViewModel_Value_SetFractionalOnContinuousParameter_KeepsFraction()
    {
        // Arrange: a presenter over a parameter not declared as integer
        var presenter = new NumericParameterViewModel(ContinuousParam);

        // Act: write a fractional value inside the declared range
        presenter.Value = 1.37;

        // Assert: the value is kept exactly, with no whole-number rounding
        Assert.Equal(1.37, presenter.Value);
        Assert.Equal(1.37, presenter.BoxedValue);
    }

    /// <summary>
    ///     Proves that a boolean presenter starts at a declared default of
    ///     <see langword="false"/> and carries the descriptor's identity and description
    ///     unchanged.
    /// </summary>
    [Fact]
    public void BooleanParameterViewModel_Constructor_FalseDefault_StartsFalseAndCarriesIdentity()
    {
        // Act: build a presenter over a parameter defaulting to false
        var presenter = new BooleanParameterViewModel(FalseDefaultParam);

        // Assert: default and identity all come from the descriptor
        Assert.False(presenter.Value);
        Assert.False((bool)presenter.BoxedValue);
        Assert.Equal("denoise", presenter.Id);
        Assert.Equal("Denoise", presenter.DisplayName);
        Assert.Equal("Removes background noise.", presenter.Description);
    }

    /// <summary>
    ///     Proves that changing a choice presenter's selection announces the change, so a bound
    ///     control stays in step.
    /// </summary>
    [Fact]
    public void ChoiceParameterViewModel_SelectedOption_Changed_RaisesPropertyChanged()
    {
        // Arrange: a presenter recording every property-changed notification
        var presenter = new ChoiceParameterViewModel(NonFirstDefaultParam);
        var raised = new List<string?>();
        presenter.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // Act: select a different option
        presenter.SelectedOption = NonFirstDefaultParam.Options[2];

        // Assert: the change was announced
        Assert.Contains(nameof(ChoiceParameterViewModel.SelectedOption), raised);
        Assert.Equal("bass", presenter.BoxedValue);
    }
}
