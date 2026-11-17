using DemaConsulting.Speech.Demo.ModelSettingsSubsystem;
using DemaConsulting.Speech.Demo.Tests.Fakes;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo.Tests.ModelSettingsSubsystem;

/// <summary>
///     Subsystem tests for the ModelSettingsSubsystem, driving the settings panel's public
///     view-model from a selected model through to the value bag it hands to the engine factory.
/// </summary>
public class ModelSettingsSubsystemTests
{
    /// <summary>
    ///     Proves that selecting a model presents a whole-number-only value for its integer
    ///     parameter and a fractional value for its continuous parameter, and that the value bag
    ///     handed on carries exactly what the user could select.
    /// </summary>
    [Fact]
    public void ModelSettingsSubsystem_SelectModel_IntegerAndContinuousParameters_IntegerIsWholeNumberOnly()
    {
        // Arrange: a model declaring one whole-number parameter and one continuous parameter
        var integerParameter = new NumericParameter(
            "speaker", "Speaker", "Speaker index.", new NumericParameterBounds(0, 903, 1, 0), isInteger: true);
        var continuousParameter = new NumericParameter(
            "speed", "Speed", "Speaking rate.", new NumericParameterBounds(0.5, 2.0, 0.1, 1.0), "x");
        var model = new FakeSpeechModel(parameters: [integerParameter, continuousParameter]);

        // Act: select the model in the settings panel and try to select fractional values
        var panel = new ModelSettingsViewModel { Model = model };
        var presentedInteger = Assert.IsType<NumericParameterViewModel>(panel.Parameters[0]);
        var presentedContinuous = Assert.IsType<NumericParameterViewModel>(panel.Parameters[1]);
        presentedInteger.Value = 41.6;
        presentedContinuous.Value = 1.25;
        var bag = panel.BuildValueBag();

        // Assert: the integer parameter is flagged whole-number-only and only whole numbers were
        // selectable; the continuous parameter keeps its fraction; the bag carries both
        Assert.True(presentedInteger.IsInteger);
        Assert.False(presentedContinuous.IsInteger);
        Assert.Equal(42.0, presentedInteger.Value);
        Assert.Equal(1.25, presentedContinuous.Value);
        Assert.Equal(42.0, bag["speaker"]);
        Assert.Equal(1.25, bag["speed"]);
    }

    /// <summary>
    ///     Proves that switching to a model without an integer parameter leaves no whole-number
    ///     presentation behind, so a stale integer constraint never applies to the new model.
    /// </summary>
    [Fact]
    public void ModelSettingsSubsystem_SwitchModel_ContinuousOnlyModel_NoStaleWholeNumberConstraint()
    {
        // Arrange: a panel presenting a model with an integer parameter
        var integerParameter = new NumericParameter(
            "speaker", "Speaker", "Speaker index.", new NumericParameterBounds(0, 903, 1, 0), isInteger: true);
        var continuousParameter = new NumericParameter(
            "speed", "Speed", "Speaking rate.", new NumericParameterBounds(0.5, 2.0, 0.1, 1.0), "x");
        var panel = new ModelSettingsViewModel { Model = new FakeSpeechModel("first", parameters: [integerParameter]) };

        // Act: switch to a model that declares only a continuous parameter, and select a fraction
        panel.Model = new FakeSpeechModel("second", parameters: [continuousParameter]);
        var presented = Assert.IsType<NumericParameterViewModel>(Assert.Single(panel.Parameters));
        presented.Value = 0.75 + 0.5;

        // Assert: the new model's parameter is continuous and keeps the fraction
        Assert.False(presented.IsInteger);
        Assert.Equal(1.25, presented.Value);
        Assert.Equal(1.25, panel.BuildValueBag()["speed"]);
    }
}
