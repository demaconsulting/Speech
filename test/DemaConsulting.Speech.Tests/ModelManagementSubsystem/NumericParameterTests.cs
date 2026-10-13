using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for the <see cref="NumericParameter"/> record.
/// </summary>
public class NumericParameterTests
{
    /// <summary>
    ///     Proves that valid, well-ordered bounds construct successfully and expose the supplied
    ///     values.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_ValidRange_ExposesValues()
    {
        // Arrange
        var bounds = new NumericParameterBounds(0.5, 2.0, 0.1, 1.0);

        // Act
        var parameter = new NumericParameter("tempo", "Tempo", "Speaking rate.", bounds, "x");

        // Assert
        Assert.Equal("tempo", parameter.Id);
        Assert.Equal("Tempo", parameter.DisplayName);
        Assert.Equal("Speaking rate.", parameter.Description);
        Assert.Equal(0.5, parameter.Minimum);
        Assert.Equal(2.0, parameter.Maximum);
        Assert.Equal(0.1, parameter.Step);
        Assert.Equal(1.0, parameter.Default);
        Assert.Equal("x", parameter.Unit);
    }

    /// <summary>
    ///     Proves that omitting <c>unit</c> leaves it <see langword="null"/>, since a
    ///     dimensionless parameter has no unit label to display.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_NoUnit_UnitIsNull()
    {
        // Arrange
        var bounds = new NumericParameterBounds(0, 1, 0.05, 0.5);

        // Act
        var parameter = new NumericParameter("gain", "Gain", "Input gain.", bounds);

        // Assert
        Assert.Null(parameter.Unit);
    }

    /// <summary>
    ///     Proves that a minimum greater than the maximum is rejected, since such a range could
    ///     never be honored by any range-based control.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_MinimumGreaterThanMaximum_ThrowsArgumentException()
    {
        // Arrange
        var bounds = new NumericParameterBounds(1.0, 0.0, 0.1, 0.5);

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new NumericParameter("bad", "Bad", "Bad range.", bounds));

        // Assert
        Assert.Equal("bounds", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a non-positive step is rejected, since a zero or negative step has no
    ///     meaningful increment.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_NonPositiveStep_ThrowsArgumentException()
    {
        // Arrange
        var bounds = new NumericParameterBounds(0.0, 1.0, 0.0, 0.5);

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new NumericParameter("bad", "Bad", "Bad step.", bounds));

        // Assert
        Assert.Equal("bounds", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a default outside [minimum, maximum] is rejected, since a host could never
    ///     supply this parameter's own declared default value back to the model.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_DefaultOutsideRange_ThrowsArgumentException()
    {
        // Arrange
        var bounds = new NumericParameterBounds(0.0, 1.0, 0.1, 2.0);

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new NumericParameter("bad", "Bad", "Bad default.", bounds));

        // Assert
        Assert.Equal("bounds", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a non-finite bound (NaN or infinity) is rejected outright.
    /// </summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NumericParameter_Constructor_NonFiniteMinimum_ThrowsArgumentException(double minimum)
    {
        // Arrange
        var bounds = new NumericParameterBounds(minimum, 1.0, 0.1, 0.5);

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new NumericParameter("bad", "Bad", "Bad bound.", bounds));

        // Assert
        Assert.Equal("bounds", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a null id is rejected.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_NullId_ThrowsArgumentNullException()
    {
        // Arrange
        string? id = null;
        var bounds = new NumericParameterBounds(0, 1, 0.1, 0.5);

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new NumericParameter(id!, "Name", "Description.", bounds));

        // Assert
        Assert.Equal("id", exception.ParamName);
    }

    /// <summary>
    ///     Proves that an empty or whitespace-only id is rejected.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void NumericParameter_Constructor_EmptyOrWhitespaceId_ThrowsArgumentException(string id)
    {
        // Arrange
        var bounds = new NumericParameterBounds(0, 1, 0.1, 0.5);

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new NumericParameter(id, "Name", "Desc.", bounds));

        // Assert
        Assert.Equal("id", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a null display name is rejected.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_NullDisplayName_ThrowsArgumentNullException()
    {
        // Arrange
        string? displayName = null;
        var bounds = new NumericParameterBounds(0, 1, 0.1, 0.5);

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new NumericParameter("id", displayName!, "Desc.", bounds));

        // Assert
        Assert.Equal("displayName", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a null description is rejected.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_NullDescription_ThrowsArgumentNullException()
    {
        // Arrange
        string? description = null;
        var bounds = new NumericParameterBounds(0, 1, 0.1, 0.5);

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new NumericParameter("id", "Name", description!, bounds));

        // Assert
        Assert.Equal("description", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a non-finite Maximum, Step, or Default is rejected, naming
    ///     <c>bounds</c> as the offending parameter.
    /// </summary>
    [Theory]
    [InlineData(double.NaN, 0.1, 0.5)]
    [InlineData(double.PositiveInfinity, 0.1, 0.5)]
    [InlineData(1.0, double.NaN, 0.5)]
    [InlineData(1.0, double.PositiveInfinity, 0.5)]
    [InlineData(1.0, 0.1, double.NaN)]
    [InlineData(1.0, 0.1, double.NegativeInfinity)]
    public void NumericParameter_Constructor_NonFiniteMaximumStepOrDefault_ThrowsArgumentException(
        double maximum, double step, double @default)
    {
        // Arrange
        var bounds = new NumericParameterBounds(0.0, maximum, step, @default);

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new NumericParameter("bad", "Bad", "Bad bound.", bounds));

        // Assert
        Assert.Equal("bounds", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a default exactly at Minimum or Maximum is accepted (inclusive range).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void NumericParameter_Constructor_DefaultAtBoundary_Succeeds(double @default)
    {
        // Arrange
        var bounds = new NumericParameterBounds(0.0, 1.0, 0.1, @default);

        // Act
        var parameter = new NumericParameter("edge", "Edge", "Boundary default.", bounds);

        // Assert
        Assert.Equal(@default, parameter.Default);
    }

    /// <summary>
    ///     Proves that an integer declaration with a fractional bound reports <c>bounds</c> as
    ///     the offending parameter name.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_IsIntegerWithFractionalBound_ParamNameIsBounds()
    {
        // Arrange
        var bounds = new NumericParameterBounds(0.5, 10, 1, 1);

        // Act
        var exception = Assert.Throws<ArgumentException>(() => new NumericParameter(
            "bad", "Bad", "Fractional.", bounds, isInteger: true));

        // Assert
        Assert.Equal("bounds", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a valid whole-number declaration with <c>isInteger: true</c> constructs
    ///     successfully and exposes <see cref="NumericParameter.IsInteger"/> as
    ///     <see langword="true"/>, since a discrete parameter (for example a speaker index) has
    ///     no fractional meaning.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_IsIntegerWithWholeNumberBounds_ExposesIsIntegerTrue()
    {
        // Arrange
        var bounds = new NumericParameterBounds(0, 903, 1, 0);

        // Act
        var parameter = new NumericParameter("speaker", "Speaker", "Speaker index.", bounds, isInteger: true);

        // Assert
        Assert.True(parameter.IsInteger);
        Assert.Equal(0, parameter.Minimum);
        Assert.Equal(903, parameter.Maximum);
        Assert.Equal(1, parameter.Step);
        Assert.Equal(0, parameter.Default);
    }

    /// <summary>
    ///     Proves that omitting <c>isInteger</c> defaults it to <see langword="false"/>, keeping
    ///     every existing continuous-parameter declaration unaffected.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_IsIntegerOmitted_DefaultsToFalse()
    {
        // Arrange
        var bounds = new NumericParameterBounds(0.5, 2.0, 0.1, 1.0);

        // Act
        var parameter = new NumericParameter("tempo", "Tempo", "Speaking rate.", bounds);

        // Assert
        Assert.False(parameter.IsInteger);
    }

    /// <summary>
    ///     Proves that a non-whole-number <c>minimum</c> is rejected when <c>isInteger</c> is
    ///     <see langword="true"/>, since a fractional bound could never be honored by a
    ///     whole-number-only control.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_IsIntegerWithFractionalMinimum_ThrowsArgumentException()
    {
        // Arrange
        var bounds = new NumericParameterBounds(0.5, 10, 1, 1);

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new NumericParameter("bad", "Bad", "Bad minimum.", bounds, isInteger: true));

        // Assert
        Assert.Equal("bounds", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a non-whole-number <c>maximum</c> is rejected when <c>isInteger</c> is
    ///     <see langword="true"/>.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_IsIntegerWithFractionalMaximum_ThrowsArgumentException()
    {
        // Arrange
        var bounds = new NumericParameterBounds(0, 10.5, 1, 1);

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new NumericParameter("bad", "Bad", "Bad maximum.", bounds, isInteger: true));

        // Assert
        Assert.Equal("bounds", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a non-whole-number <c>step</c> is rejected when <c>isInteger</c> is
    ///     <see langword="true"/>.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_IsIntegerWithFractionalStep_ThrowsArgumentException()
    {
        // Arrange
        var bounds = new NumericParameterBounds(0, 10, 0.5, 1);

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new NumericParameter("bad", "Bad", "Bad step.", bounds, isInteger: true));

        // Assert
        Assert.Equal("bounds", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a non-whole-number <c>default</c> is rejected when <c>isInteger</c> is
    ///     <see langword="true"/>.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_IsIntegerWithFractionalDefault_ThrowsArgumentException()
    {
        // Arrange
        var bounds = new NumericParameterBounds(0, 10, 1, 1.5);

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new NumericParameter("bad", "Bad", "Bad default.", bounds, isInteger: true));

        // Assert
        Assert.Equal("bounds", exception.ParamName);
    }
}
