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
        // Act
        var parameter = new BooleanParameter("denoise", "Denoise", "Applies noise suppression.", defaultValue);

        // Assert
        Assert.Equal("denoise", parameter.Id);
        Assert.Equal("Denoise", parameter.DisplayName);
        Assert.Equal("Applies noise suppression.", parameter.Description);
        Assert.Equal(defaultValue, parameter.Default);
    }

    /// <summary>
    ///     Proves that a null or whitespace-only id is rejected.
    /// </summary>
    [Fact]
    public void BooleanParameter_Constructor_WhitespaceId_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new BooleanParameter("   ", "Name", "Description.", true));
    }

    /// <summary>
    ///     Proves that a null display name is rejected.
    /// </summary>
    [Fact]
    public void BooleanParameter_Constructor_NullDisplayName_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new BooleanParameter("id", null!, "Description.", true));
    }
}
