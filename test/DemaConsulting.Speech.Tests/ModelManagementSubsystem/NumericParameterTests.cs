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
        // Act
        var parameter = new NumericParameter(
            "tempo", "Tempo", "Speaking rate.", new NumericParameterBounds(0.5, 2.0, 0.1, 1.0), "x");

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
        // Act
        var parameter = new NumericParameter("gain", "Gain", "Input gain.", new NumericParameterBounds(0, 1, 0.05, 0.5));

        // Assert
        Assert.Null(parameter.Unit);
    }

    /// <summary>
    ///     Proves that a minimum greater than the maximum is rejected, since such a range could
    ///     never be honored by any slider control.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_MinimumGreaterThanMaximum_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new NumericParameter("bad", "Bad", "Bad range.", new NumericParameterBounds(1.0, 0.0, 0.1, 0.5)));
    }

    /// <summary>
    ///     Proves that a non-positive step is rejected, since a zero or negative step has no
    ///     meaningful slider increment.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_NonPositiveStep_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new NumericParameter("bad", "Bad", "Bad step.", new NumericParameterBounds(0.0, 1.0, 0.0, 0.5)));
    }

    /// <summary>
    ///     Proves that a default outside [minimum, maximum] is rejected, since a host could never
    ///     supply this parameter's own declared default value back to the model.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_DefaultOutsideRange_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new NumericParameter("bad", "Bad", "Bad default.", new NumericParameterBounds(0.0, 1.0, 0.1, 2.0)));
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
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new NumericParameter("bad", "Bad", "Bad bound.", new NumericParameterBounds(minimum, 1.0, 0.1, 0.5)));
    }

    /// <summary>
    ///     Proves that a null id is rejected.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_NullId_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new NumericParameter(null!, "Name", "Description.", new NumericParameterBounds(0, 1, 0.1, 0.5)));
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
        // Act
        var parameter = new NumericParameter(
            "speaker", "Speaker", "Speaker index.", new NumericParameterBounds(0, 903, 1, 0), isInteger: true);

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
        // Act
        var parameter = new NumericParameter("tempo", "Tempo", "Speaking rate.", new NumericParameterBounds(0.5, 2.0, 0.1, 1.0));

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
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new NumericParameter("bad", "Bad", "Bad minimum.", new NumericParameterBounds(0.5, 10, 1, 1), isInteger: true));
    }

    /// <summary>
    ///     Proves that a non-whole-number <c>maximum</c> is rejected when <c>isInteger</c> is
    ///     <see langword="true"/>.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_IsIntegerWithFractionalMaximum_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new NumericParameter("bad", "Bad", "Bad maximum.", new NumericParameterBounds(0, 10.5, 1, 1), isInteger: true));
    }

    /// <summary>
    ///     Proves that a non-whole-number <c>step</c> is rejected when <c>isInteger</c> is
    ///     <see langword="true"/>.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_IsIntegerWithFractionalStep_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new NumericParameter("bad", "Bad", "Bad step.", new NumericParameterBounds(0, 10, 0.5, 1), isInteger: true));
    }

    /// <summary>
    ///     Proves that a non-whole-number <c>default</c> is rejected when <c>isInteger</c> is
    ///     <see langword="true"/>.
    /// </summary>
    [Fact]
    public void NumericParameter_Constructor_IsIntegerWithFractionalDefault_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new NumericParameter("bad", "Bad", "Bad default.", new NumericParameterBounds(0, 10, 1, 1.5), isInteger: true));
    }
}
