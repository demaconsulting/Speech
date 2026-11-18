using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingSubsystem;

/// <summary>
///     Unit tests for <see cref="DeterministicMath"/>: the exponential and logarithm built from basic arithmetic
///     agree with the framework's functions over the domain the trainer uses.
/// </summary>
public sealed class DeterministicMathTests
{
    /// <summary>The largest relative error the trainer's math may have against the framework's functions.</summary>
    private const double Tolerance = 1e-12;

    /// <summary>Proves the exponential matches the framework's over the range the calibration uses and beyond.</summary>
    [Fact]
    public void DeterministicMath_Exp_SweepOfArguments_MatchesFrameworkWithinTolerance()
    {
        // Arrange: from far below any real score difference up to modest positive values
        var arguments = Enumerable.Range(-70000, 70800).Select(i => i / 100.0).ToList();

        // Act / Assert
        foreach (var x in arguments)
        {
            var expected = Math.Exp(x);
            Assert.InRange(Math.Abs(DeterministicMath.Exp(x) - expected) / expected, 0.0, Tolerance);
        }
    }

    /// <summary>Proves the logarithm matches the framework's over many orders of magnitude.</summary>
    [Fact]
    public void DeterministicMath_Log_SweepOfArguments_MatchesFrameworkWithinTolerance()
    {
        // Arrange: 1e-300 to 1e300 in steps, and a fine sweep around one where the logarithm is near zero
        var arguments = Enumerable.Range(-300, 601).Select(e => Math.Pow(10, e) * 1.2345).ToList();
        arguments.AddRange(Enumerable.Range(1, 2000).Select(i => 0.9 + (i * 0.0001)));

        // Act / Assert
        foreach (var x in arguments)
        {
            var expected = Math.Log(x);
            var actual = DeterministicMath.Log(x);
            Assert.InRange(Math.Abs(actual - expected), 0.0, Tolerance * Math.Max(1.0, Math.Abs(expected)));
        }
    }

    /// <summary>Proves the special values of both functions.</summary>
    [Fact]
    public void DeterministicMath_ExpAndLog_SpecialValues_AreDefined()
    {
        // Assert
        Assert.Equal(1.0, DeterministicMath.Exp(0.0));
        Assert.Equal(0.0, DeterministicMath.Exp(-1000.0));
        Assert.True(double.IsPositiveInfinity(DeterministicMath.Exp(1000.0)));
        Assert.True(double.IsNaN(DeterministicMath.Exp(double.NaN)));
        Assert.Equal(0.0, DeterministicMath.Log(1.0));
        Assert.True(double.IsNegativeInfinity(DeterministicMath.Log(0.0)));
        Assert.True(double.IsNaN(DeterministicMath.Log(-1.0)));
        Assert.True(double.IsNaN(DeterministicMath.Log(double.NaN)));
        Assert.True(double.IsPositiveInfinity(DeterministicMath.Log(double.PositiveInfinity)));
    }

    /// <summary>Proves a subnormal argument of the logarithm is handled.</summary>
    [Fact]
    public void DeterministicMath_Log_SubnormalArgument_MatchesFramework()
    {
        // Arrange
        var x = double.Epsilon * 12345;

        // Act
        var actual = DeterministicMath.Log(x);

        // Assert
        Assert.InRange(Math.Abs(actual - Math.Log(x)), 0.0, 1e-9);
    }

    /// <summary>Proves the logarithm inverts the exponential.</summary>
    [Fact]
    public void DeterministicMath_Log_OfExp_ReturnsTheArgument()
    {
        // Arrange / Act / Assert
        foreach (var x in new[] { -5.5, -1.0, -0.1, 0.1, 1.0, 3.25, 20.0 })
        {
            Assert.InRange(Math.Abs(DeterministicMath.Log(DeterministicMath.Exp(x)) - x), 0.0, 1e-11);
        }
    }
}
