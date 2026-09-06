using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for the <see cref="ChoiceParameter"/> and <see cref="ChoiceParameterOption"/>
///     records.
/// </summary>
public class ChoiceParameterTests
{
    /// <summary>
    ///     Proves that a well-formed option list and a default matching one option construct
    ///     successfully and expose the supplied values.
    /// </summary>
    [Fact]
    public void ChoiceParameter_Constructor_ValidOptions_ExposesValues()
    {
        // Arrange
        IReadOnlyList<ChoiceParameterOption> options =
        [
            new ChoiceParameterOption("en-us", "English (US)"),
            new ChoiceParameterOption("en-gb", "English (UK)"),
        ];

        // Act
        var parameter = new ChoiceParameter("language", "Language", "Recognition language.", options, "en-us");

        // Assert
        Assert.Equal("language", parameter.Id);
        Assert.Equal(options, parameter.Options);
        Assert.Equal("en-us", parameter.Default);
    }

    /// <summary>
    ///     Proves that an empty option list is rejected, since a dropdown with no options could
    ///     never be rendered meaningfully.
    /// </summary>
    [Fact]
    public void ChoiceParameter_Constructor_EmptyOptions_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => new ChoiceParameter("bad", "Bad", "No options.", [], "anything"));
    }

    /// <summary>
    ///     Proves that two options sharing the same value are rejected, since a supplied bag
    ///     entry using that value would be ambiguous.
    /// </summary>
    [Fact]
    public void ChoiceParameter_Constructor_DuplicateOptionValues_ThrowsArgumentException()
    {
        // Arrange
        IReadOnlyList<ChoiceParameterOption> options =
        [
            new ChoiceParameterOption("a", "Option A"),
            new ChoiceParameterOption("a", "Option A Duplicate"),
        ];

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => new ChoiceParameter("bad", "Bad", "Duplicate values.", options, "a"));
    }

    /// <summary>
    ///     Proves that a default not matching any declared option is rejected.
    /// </summary>
    [Fact]
    public void ChoiceParameter_Constructor_DefaultNotAnOption_ThrowsArgumentException()
    {
        // Arrange
        IReadOnlyList<ChoiceParameterOption> options = [new ChoiceParameterOption("a", "Option A")];

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => new ChoiceParameter("bad", "Bad", "Unknown default.", options, "not-an-option"));
    }

    /// <summary>
    ///     Proves that an empty option value is rejected.
    /// </summary>
    [Fact]
    public void ChoiceParameterOption_Constructor_EmptyValue_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ChoiceParameterOption("", "Label"));
    }
}
