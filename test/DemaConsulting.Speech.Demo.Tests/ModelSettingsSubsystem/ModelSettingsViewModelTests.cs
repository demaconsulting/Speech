using DemaConsulting.Speech.Demo.ModelSettingsSubsystem;
using DemaConsulting.Speech.Demo.Tests.Fakes;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo.Tests.ModelSettingsSubsystem;

/// <summary>
///     Unit tests for <see cref="ModelSettingsViewModel"/>.
/// </summary>
public class ModelSettingsViewModelTests
{
    /// <summary>A numeric parameter used across several tests.</summary>
    private static readonly NumericParameter NumericParam =
        new("speed", "Speed", "Speaking rate.", 0.5, 2.0, 0.1, 1.0, "x");

    /// <summary>An integer numeric parameter used to prove whole-number rounding behavior.</summary>
    private static readonly NumericParameter IntegerNumericParam =
        new("speaker", "Speaker", "Speaker index.", 0, 903, 1, 0, isInteger: true);

    /// <summary>A choice parameter used across several tests.</summary>
    private static readonly ChoiceParameter ChoiceParam = new(
        "voice",
        "Voice",
        "Speaker voice.",
        [new ChoiceParameterOption("alto", "Alto"), new ChoiceParameterOption("tenor", "Tenor")],
        "alto");

    /// <summary>A boolean parameter used across several tests.</summary>
    private static readonly BooleanParameter BooleanParam =
        new("denoise", "Denoise", "Removes background noise.", true);

    /// <summary>
    ///     Proves that constructing the panel with no model starts in the honest "no model"
    ///     empty state, consistent with the model catalog's own empty-state precedent.
    /// </summary>
    [Fact]
    public void ModelSettingsViewModel_Constructor_NoModel_ReportsNoModelEmptyState()
    {
        // Arrange & Act: build the panel with no model selected
        var viewModel = new ModelSettingsViewModel();

        // Assert: the panel is empty and explains why
        Assert.False(viewModel.HasModel);
        Assert.False(viewModel.HasParameters);
        Assert.True(viewModel.IsEmpty);
        Assert.Empty(viewModel.Parameters);
        Assert.Equal(ModelSettingsViewModel.NoModelMessage, viewModel.EmptyMessage);
    }

    /// <summary>
    ///     Proves that a selected model with no declared parameters is also an honest empty
    ///     state, distinct from the "no model" message.
    /// </summary>
    [Fact]
    public void ModelSettingsViewModel_Model_ModelWithNoParameters_ReportsNoParametersEmptyState()
    {
        // Arrange: a model that declares no tunable parameters
        var model = new FakeSpeechModel(parameters: []);

        // Act: select it
        var viewModel = new ModelSettingsViewModel { Model = model };

        // Assert: the panel is empty but distinguishes "model with no parameters"
        Assert.True(viewModel.HasModel);
        Assert.False(viewModel.HasParameters);
        Assert.True(viewModel.IsEmpty);
        Assert.Empty(viewModel.Parameters);
        Assert.Equal(ModelSettingsViewModel.NoParametersMessage, viewModel.EmptyMessage);
    }

    /// <summary>
    ///     Proves that the panel renders one presenter per declared parameter, of the concrete
    ///     kind matching each library parameter type.
    /// </summary>
    [Fact]
    public void ModelSettingsViewModel_Model_ModelWithAllThreeParameterKinds_RendersMatchingPresenters()
    {
        // Arrange: a model declaring one of each parameter kind
        var model = new FakeSpeechModel(parameters: [NumericParam, ChoiceParam, BooleanParam]);

        // Act: select it
        var viewModel = new ModelSettingsViewModel { Model = model };

        // Assert: three presenters, each of the correct concrete kind, in declaration order
        Assert.True(viewModel.HasModel);
        Assert.True(viewModel.HasParameters);
        Assert.False(viewModel.IsEmpty);
        Assert.Equal(3, viewModel.Parameters.Count);
        Assert.IsType<NumericParameterViewModel>(viewModel.Parameters[0]);
        Assert.IsType<ChoiceParameterViewModel>(viewModel.Parameters[1]);
        Assert.IsType<BooleanParameterViewModel>(viewModel.Parameters[2]);
    }

    /// <summary>
    ///     Proves that switching to a different model discards the previous model's presenters
    ///     rather than merging them.
    /// </summary>
    [Fact]
    public void ModelSettingsViewModel_Model_ChangedToDifferentModel_ReplacesPresenters()
    {
        // Arrange: a panel presenting the first model's single parameter
        var first = new FakeSpeechModel("first", parameters: [NumericParam]);
        var second = new FakeSpeechModel("second", parameters: [BooleanParam]);
        var viewModel = new ModelSettingsViewModel { Model = first };

        // Act: switch to the second model
        viewModel.Model = second;

        // Assert: only the second model's parameter is presented
        Assert.Single(viewModel.Parameters);
        Assert.IsType<BooleanParameterViewModel>(viewModel.Parameters[0]);
    }

    /// <summary>
    ///     Proves that <see cref="ModelSettingsViewModel.BuildValueBag"/> assembles every
    ///     presenter's current value keyed by its parameter id, round-tripping each kind's
    ///     current value.
    /// </summary>
    [Fact]
    public void ModelSettingsViewModel_BuildValueBag_AllThreeParameterKinds_RoundTripsCurrentValues()
    {
        // Arrange: a panel over all three parameter kinds, with each value changed from its default
        var model = new FakeSpeechModel(parameters: [NumericParam, ChoiceParam, BooleanParam]);
        var viewModel = new ModelSettingsViewModel { Model = model };
        ((NumericParameterViewModel)viewModel.Parameters[0]).Value = 1.5;
        ((ChoiceParameterViewModel)viewModel.Parameters[1]).SelectedOption =
            ChoiceParam.Options[1];
        ((BooleanParameterViewModel)viewModel.Parameters[2]).Value = false;

        // Act: assemble the value bag
        var bag = viewModel.BuildValueBag();

        // Assert: every changed value round-trips under its parameter id
        Assert.Equal(1.5, bag["speed"]);
        Assert.Equal("tenor", bag["voice"]);
        Assert.False((bool)bag["denoise"]);
    }

    /// <summary>
    ///     Proves that the value bag is empty when there is no model or no declared parameters,
    ///     rather than throwing.
    /// </summary>
    [Fact]
    public void ModelSettingsViewModel_BuildValueBag_NoModel_ReturnsEmptyBag()
    {
        // Arrange: a panel with no model selected
        var viewModel = new ModelSettingsViewModel();

        // Act: assemble the value bag
        var bag = viewModel.BuildValueBag();

        // Assert: it is empty, not a fault
        Assert.Empty(bag);
    }

    /// <summary>
    ///     Proves that a numeric presenter clamps a value pushed outside its declared range,
    ///     so a bound control can never push this parameter beyond what the model can honor.
    /// </summary>
    [Fact]
    public void NumericParameterViewModel_Value_SetOutsideRange_ClampsToBounds()
    {
        // Arrange: a numeric presenter over the shared parameter
        var presenter = new NumericParameterViewModel(NumericParam);

        // Act: push values outside the declared [0.5, 2.0] range
        presenter.Value = 10.0;
        var clampedHigh = presenter.Value;
        presenter.Value = -5.0;
        var clampedLow = presenter.Value;

        // Assert: both writes were clamped to the declared bounds
        Assert.Equal(2.0, clampedHigh);
        Assert.Equal(0.5, clampedLow);
    }

    /// <summary>
    ///     Proves that a numeric presenter starts at the parameter's declared default and
    ///     carries its metadata unchanged.
    /// </summary>
    [Fact]
    public void NumericParameterViewModel_Constructor_Parameter_StartsAtDeclaredDefault()
    {
        // Act: build a presenter for the shared numeric parameter
        var presenter = new NumericParameterViewModel(NumericParam);

        // Assert: metadata and starting value all came from the parameter
        Assert.Equal("speed", presenter.Id);
        Assert.Equal("Speed", presenter.DisplayName);
        Assert.Equal(0.5, presenter.Minimum);
        Assert.Equal(2.0, presenter.Maximum);
        Assert.Equal(0.1, presenter.Step);
        Assert.Equal("x", presenter.Unit);
        Assert.Equal(1.0, presenter.Value);
        Assert.Equal(1.0, presenter.BoxedValue);
        Assert.False(presenter.IsInteger);
    }

    /// <summary>
    ///     Proves that a numeric presenter over an integer parameter starts with
    ///     <see cref="NumericParameterViewModel.IsInteger"/> exposed as <see langword="true"/>,
    ///     so a host renders it with a whole-number-only control rather than a continuous
    ///     slider.
    /// </summary>
    [Fact]
    public void NumericParameterViewModel_Constructor_IntegerParameter_ExposesIsIntegerTrue()
    {
        // Act
        var presenter = new NumericParameterViewModel(IntegerNumericParam);

        // Assert
        Assert.True(presenter.IsInteger);
        Assert.Equal(0.0, presenter.Value);
    }

    /// <summary>
    ///     Proves that setting a fractional value on an integer presenter rounds it to the
    ///     nearest whole number, so no code path - test, XAML binding, or otherwise - can leave
    ///     the presenter holding a fractional value for a parameter with no fractional meaning.
    /// </summary>
    [Fact]
    public void NumericParameterViewModel_Value_SetFractionalOnIntegerParameter_RoundsToNearestWholeNumber()
    {
        // Arrange
        var presenter = new NumericParameterViewModel(IntegerNumericParam);

        // Act
        presenter.Value = 153.51;

        // Assert
        Assert.Equal(154.0, presenter.Value);
    }

    /// <summary>
    ///     Proves that setting a fractional value outside an integer presenter's declared range
    ///     is both clamped to bounds and rounded to a whole number, so the two constraints
    ///     compose correctly regardless of write order.
    /// </summary>
    [Fact]
    public void NumericParameterViewModel_Value_SetFractionalOutsideRangeOnIntegerParameter_ClampsAndRounds()
    {
        // Arrange
        var presenter = new NumericParameterViewModel(IntegerNumericParam);

        // Act
        presenter.Value = 1000.7;

        // Assert: clamped to the declared maximum of 903, which is already a whole number
        Assert.Equal(903.0, presenter.Value);
    }

    /// <summary>
    ///     Proves that a choice presenter starts at the option matching the parameter's declared
    ///     default value.
    /// </summary>
    [Fact]
    public void ChoiceParameterViewModel_Constructor_Parameter_StartsAtDeclaredDefaultOption()
    {
        // Act: build a presenter for the shared choice parameter
        var presenter = new ChoiceParameterViewModel(ChoiceParam);

        // Assert: the default-matching option is selected and exposed as the boxed value
        Assert.Equal("alto", presenter.SelectedOption.Value);
        Assert.Equal("alto", presenter.BoxedValue);
        Assert.Equal(2, presenter.Options.Count);
    }

    /// <summary>
    ///     Proves that selecting a different declared option updates the boxed value used to
    ///     assemble the value bag.
    /// </summary>
    [Fact]
    public void ChoiceParameterViewModel_SelectedOption_ChangedToDifferentOption_UpdatesBoxedValue()
    {
        // Arrange: a choice presenter starting at its default
        var presenter = new ChoiceParameterViewModel(ChoiceParam);

        // Act: select the other declared option
        presenter.SelectedOption = ChoiceParam.Options[1];

        // Assert: the boxed value reflects the newly selected option
        Assert.Equal("tenor", presenter.BoxedValue);
    }

    /// <summary>
    ///     Proves that a boolean presenter starts at the parameter's declared default and
    ///     round-trips a toggled value.
    /// </summary>
    [Fact]
    public void BooleanParameterViewModel_Constructor_Parameter_StartsAtDeclaredDefault()
    {
        // Act: build a presenter for the shared boolean parameter
        var presenter = new BooleanParameterViewModel(BooleanParam);

        // Assert: it starts at the declared default
        Assert.True(presenter.Value);
        Assert.True((bool)presenter.BoxedValue);
    }

    /// <summary>
    ///     Proves that toggling a boolean presenter's value updates the boxed value.
    /// </summary>
    [Fact]
    public void BooleanParameterViewModel_Value_Toggled_UpdatesBoxedValue()
    {
        // Arrange: a boolean presenter starting at its default (true)
        var presenter = new BooleanParameterViewModel(BooleanParam);

        // Act: toggle it off
        presenter.Value = false;

        // Assert: the boxed value reflects the toggle
        Assert.False((bool)presenter.BoxedValue);
    }

    /// <summary>
    ///     Proves that every parameter presenter rejects a missing library descriptor.
    /// </summary>
    [Fact]
    public void ParameterViewModels_Constructor_NullParameter_ThrowArgumentNullException()
    {
        // Act & Assert: building any presenter without a descriptor is a programming error
        Assert.Throws<ArgumentNullException>(() => new NumericParameterViewModel(null!));
        Assert.Throws<ArgumentNullException>(() => new ChoiceParameterViewModel(null!));
        Assert.Throws<ArgumentNullException>(() => new BooleanParameterViewModel(null!));
    }
}
