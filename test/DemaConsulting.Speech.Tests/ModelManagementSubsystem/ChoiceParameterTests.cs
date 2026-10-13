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
    ///     Proves that an empty option list is rejected, since a choice with no options could
    ///     never be rendered meaningfully.
    /// </summary>
    [Fact]
    public void ChoiceParameter_Constructor_EmptyOptions_ThrowsArgumentException()
    {
        // Arrange
        IReadOnlyList<ChoiceParameterOption> options = [];

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => new ChoiceParameter("bad", "Bad", "No options.", options, "anything"));

        // Assert
        Assert.NotNull(exception.Message);
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

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => new ChoiceParameter("bad", "Bad", "Duplicate values.", options, "a"));

        // Assert
        Assert.NotNull(exception.Message);
    }

    /// <summary>
    ///     Proves that a default not matching any declared option is rejected.
    /// </summary>
    [Fact]
    public void ChoiceParameter_Constructor_DefaultNotAnOption_ThrowsArgumentException()
    {
        // Arrange
        IReadOnlyList<ChoiceParameterOption> options = [new ChoiceParameterOption("a", "Option A")];

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => new ChoiceParameter("bad", "Bad", "Unknown default.", options, "not-an-option"));

        // Assert
        Assert.NotNull(exception.Message);
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

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => new ChoiceParameter("bad", "Bad", "Null option.", options, "a"));

        // Assert
        Assert.IsNotType<NullReferenceException>(exception);
    }

    /// <summary>
    ///     Proves that a null option list is rejected.
    /// </summary>
    [Fact]
    public void ChoiceParameter_Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<ChoiceParameterOption>? options = null;

        // Act
        var exception = Assert.Throws<ArgumentNullException>(
            () => new ChoiceParameter("bad", "Bad", "Null options.", options!, "a"));

        // Assert
        Assert.Equal("options", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a null default is rejected.
    /// </summary>
    [Fact]
    public void ChoiceParameter_Constructor_NullDefault_ThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<ChoiceParameterOption> options = [new ChoiceParameterOption("a", "A")];
        string? defaultValue = null;

        // Act
        var exception = Assert.Throws<ArgumentNullException>(
            () => new ChoiceParameter("bad", "Bad", "Null default.", options, defaultValue!));

        // Assert
        Assert.Equal("@default", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a whitespace-only id is rejected.
    /// </summary>
    [Fact]
    public void ChoiceParameter_Constructor_WhitespaceId_ThrowsArgumentException()
    {
        // Arrange
        IReadOnlyList<ChoiceParameterOption> options = [new ChoiceParameterOption("a", "A")];

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => new ChoiceParameter(" ", "Name", "Desc.", options, "a"));

        // Assert
        Assert.Equal("id", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a null display name is rejected.
    /// </summary>
    [Fact]
    public void ChoiceParameter_Constructor_NullDisplayName_ThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<ChoiceParameterOption> options = [new ChoiceParameterOption("a", "A")];
        string? displayName = null;

        // Act
        var exception = Assert.Throws<ArgumentNullException>(
            () => new ChoiceParameter("id", displayName!, "Desc.", options, "a"));

        // Assert
        Assert.Equal("displayName", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a null description is rejected.
    /// </summary>
    [Fact]
    public void ChoiceParameter_Constructor_NullDescription_ThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<ChoiceParameterOption> options = [new ChoiceParameterOption("a", "A")];
        string? description = null;

        // Act
        var exception = Assert.Throws<ArgumentNullException>(
            () => new ChoiceParameter("id", "Name", description!, options, "a"));

        // Assert
        Assert.Equal("description", exception.ParamName);
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
        // Arrange
        const string value = "";

        // Act
        var exception = Assert.Throws<ArgumentException>(() => new ChoiceParameterOption(value, "Label"));

        // Assert
        Assert.Equal("value", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a null option value is rejected with <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void ChoiceParameterOption_Constructor_NullValue_ThrowsArgumentNullException()
    {
        // Arrange
        string? value = null;

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new ChoiceParameterOption(value!, "Label"));

        // Assert
        Assert.Equal("value", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a null option label is rejected with <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void ChoiceParameterOption_Constructor_NullLabel_ThrowsArgumentNullException()
    {
        // Arrange
        string? label = null;

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new ChoiceParameterOption("a", label!));

        // Assert
        Assert.Equal("label", exception.ParamName);
    }
}
