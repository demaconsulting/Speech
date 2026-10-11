using DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="ChatterboxTurboTokenSelector"/>.
/// </summary>
public sealed class ChatterboxTurboTokenSelectorTests
{
    /// <summary>Proves the highest logit wins when nothing was generated.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_NoHistory_ReturnsArgmax()
    {
        // Act
        var token = ChatterboxTurboTokenSelector.Select([0.1f, 2f, 1f], new HashSet<long>());

        // Assert
        Assert.Equal(1, token);
    }

    /// <summary>Proves a generated token's positive logit is divided by the penalty and can lose to a close rival.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_RepeatedPositive_IsPenalized()
    {
        // Act: 2.0 / 1.2 = 1.67 < 1.8
        var token = ChatterboxTurboTokenSelector.Select([0f, 2f, 1.8f], new HashSet<long> { 1 });

        // Assert
        Assert.Equal(2, token);
    }

    /// <summary>Proves a generated token's negative logit is multiplied by the penalty.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_RepeatedNegative_IsPushedDown()
    {
        // Act: -1.0 * 1.2 = -1.2 < -1.1
        var token = ChatterboxTurboTokenSelector.Select([-1f, -1.1f], new HashSet<long> { 0 });

        // Assert
        Assert.Equal(1, token);
    }

    /// <summary>Proves ties resolve to the lowest id and invalid arguments are rejected.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_TiesAndInvalidInput_AreHandled()
    {
        // Assert
        Assert.Equal(0, ChatterboxTurboTokenSelector.Select([1f, 1f, 1f], new HashSet<long>()));
        Assert.Equal(2, ChatterboxTurboTokenSelector.Select([1f, 1f, 1f], new HashSet<long> { 0, 1, 99 }));
        Assert.Throws<ArgumentException>(() => ChatterboxTurboTokenSelector.Select([], new HashSet<long>()));
        Assert.Throws<ArgumentNullException>(() => ChatterboxTurboTokenSelector.Select([1f], null!));
    }
}
