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
    ///     Proves that a null element in the option list is rejected with
    ///     <see cref="ArgumentException"/> rather than a <see cref="NullReferenceException"/>.
    /// </summary>
    [Fact]
    public void ChoiceParameter_Constructor_NullOptionElement_ThrowsArgumentException()
    {
        // Arrange
        IReadOnlyList<ChoiceParameterOption> options = [new ChoiceParameterOption("a", "A"), null!];

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => new ChoiceParameter("bad", "Bad", "Null option.", options, "a"));
    }

    /// <summary>
    ///     Proves that a null option list or null default is rejected.
    /// </summary>
    [Fact]
    public void ChoiceParameter_Constructor_NullOptionsOrDefault_ThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<ChoiceParameterOption> options = [new ChoiceParameterOption("a", "A")];

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new ChoiceParameter("bad", "Bad", "Null options.", null!, "a"));
        Assert.Throws<ArgumentNullException>(
            () => new ChoiceParameter("bad", "Bad", "Null default.", options, null!));
    }

    /// <summary>
    ///     Proves that an empty or whitespace id and a null display name or description are
    ///     rejected.
    /// </summary>
    [Fact]
    public void ChoiceParameter_Constructor_InvalidCommonFields_Throw()
    {
        // Arrange
        IReadOnlyList<ChoiceParameterOption> options = [new ChoiceParameterOption("a", "A")];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ChoiceParameter(" ", "Name", "Desc.", options, "a"));
        Assert.Throws<ArgumentNullException>(() => new ChoiceParameter("id", null!, "Desc.", options, "a"));
        Assert.Throws<ArgumentNullException>(() => new ChoiceParameter("id", "Name", null!, options, "a"));
    }

    /// <summary>
    ///     Proves that mutating the caller's option list after construction does not change the
    ///     descriptor's exposed options.
    /// </summary>
    [Fact]
    public void ChoiceParameter_Constructor_CallerListMutatedAfterwards_OptionsUnchanged()
    {
        // Arrange
        var options = new List<ChoiceParameterOption> { new("a", "A") };
        var parameter = new ChoiceParameter("id", "Name", "Desc.", options, "a");

        // Act
        options.Add(new ChoiceParameterOption("b", "B"));
        options.Clear();

        // Assert
        Assert.Single(parameter.Options);
        Assert.Equal("a", parameter.Options[0].Value);
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
