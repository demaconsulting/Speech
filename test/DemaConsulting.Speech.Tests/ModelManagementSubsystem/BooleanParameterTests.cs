using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for the <see cref="BooleanParameter"/> record.
/// </summary>
public class BooleanParameterTests
{
    /// <summary>
    ///     Proves that valid values construct successfully and expose the supplied values.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BooleanParameter_Constructor_ValidValues_ExposesValues(bool defaultValue)
    {
        // Arrange
        const string id = "denoise";

        // Act
        var parameter = new BooleanParameter(id, "Denoise", "Applies noise suppression.", defaultValue);

        // Assert
        Assert.Equal("denoise", parameter.Id);
        Assert.Equal("Denoise", parameter.DisplayName);
        Assert.Equal("Applies noise suppression.", parameter.Description);
        Assert.Equal(defaultValue, parameter.Default);
    }

    /// <summary>
    ///     Proves that a whitespace-only id is rejected.
    /// </summary>
    [Fact]
    public void BooleanParameter_Constructor_WhitespaceId_ThrowsArgumentException()
    {
        // Arrange
        const string id = "   ";

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => new BooleanParameter(id, "Name", "Description.", true));

        // Assert
        Assert.Equal("id", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a null display name is rejected.
    /// </summary>
    [Fact]
    public void BooleanParameter_Constructor_NullDisplayName_ThrowsArgumentNullException()
    {
        // Arrange
        string? displayName = null;

        // Act
        var exception = Assert.Throws<ArgumentNullException>(
            () => new BooleanParameter("id", displayName!, "Description.", true));

        // Assert
        Assert.Equal("displayName", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a null id is rejected.
    /// </summary>
    [Fact]
    public void BooleanParameter_Constructor_NullId_ThrowsArgumentNullException()
    {
        // Arrange
        string? id = null;

        // Act
        var exception = Assert.Throws<ArgumentNullException>(
            () => new BooleanParameter(id!, "Name", "Desc.", false));

        // Assert
        Assert.Equal("id", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a null description is rejected.
    /// </summary>
    [Fact]
    public void BooleanParameter_Constructor_NullDescription_ThrowsArgumentNullException()
    {
        // Arrange
        string? description = null;

        // Act
        var exception = Assert.Throws<ArgumentNullException>(
            () => new BooleanParameter("id", "Name", description!, false));

        // Assert
        Assert.Equal("description", exception.ParamName);
    }
}
