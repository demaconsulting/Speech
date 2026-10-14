using DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="ChatterboxTurboTokenSelector"/>.
/// </summary>
public sealed class ChatterboxTurboTokenSelectorTests
{
    /// <summary>Proves a blocked token is skipped for the call and the next best id wins.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_BlockedToken_IsSkipped()
    {
        // Act
        var token = ChatterboxTurboTokenSelector.Select([0.1f, 2f, 1f], new HashSet<long>(), 1);

        // Assert
        Assert.Equal(2, token);
    }

    /// <summary>Proves the stop token is never blocked.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_BlockedStopToken_IsIgnored()
    {
        // Arrange
        var logits = new float[OnnxChatterboxTurboSynthesisEngine.StopSpeechToken + 1];
        logits[OnnxChatterboxTurboSynthesisEngine.StopSpeechToken] = 5f;

        // Act
        var token = ChatterboxTurboTokenSelector.Select(logits, new HashSet<long>(), OnnxChatterboxTurboSynthesisEngine.StopSpeechToken);

        // Assert
        Assert.Equal(OnnxChatterboxTurboSynthesisEngine.StopSpeechToken, token);
    }

    /// <summary>Proves a blocked only id is returned, since selection must always yield a token.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_SingleIdBlocked_ReturnsOnlyId()
    {
        // Act
        var token = ChatterboxTurboTokenSelector.Select([3f], new HashSet<long>(), 0);

        // Assert
        Assert.Equal(0, token);
    }

    /// <summary>Proves a blocked id outside the vocabulary is ignored.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_BlockedIdOutOfRange_IsIgnored()
    {
        // Act
        var token = ChatterboxTurboTokenSelector.Select([0.1f, 2f, 1f], new HashSet<long>(), 99);

        // Assert
        Assert.Equal(1, token);
    }

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

    /// <summary>Proves ties resolve to the lowest id.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_Ties_ResolveToLowestId()
    {
        // Act
        var token = ChatterboxTurboTokenSelector.Select([1f, 1f, 1f], new HashSet<long>());

        // Assert
        Assert.Equal(0, token);
    }

    /// <summary>Proves penalized ties still resolve to the lowest id and out-of-vocabulary history ids are ignored.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_PenalizedTiesAndOutOfRangeHistory_ResolveToUnpenalizedId()
    {
        // Act
        var token = ChatterboxTurboTokenSelector.Select([1f, 1f, 1f], new HashSet<long> { 0, 1, 99 });

        // Assert
        Assert.Equal(2, token);
    }

    /// <summary>Proves empty logits are rejected.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_EmptyLogits_ThrowsArgumentException()
    {
        // Act
        var exception = Record.Exception(() => ChatterboxTurboTokenSelector.Select([], new HashSet<long>()));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    /// <summary>Proves a null history is rejected.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_NullHistory_ThrowsArgumentNullException()
    {
        // Act
        var exception = Record.Exception(() => ChatterboxTurboTokenSelector.Select([1f], null!));

        // Assert
        Assert.IsType<ArgumentNullException>(exception);
    }

    /// <summary>Proves selection is deterministic: identical inputs always choose the same token.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_SameInputs_ChooseSameTokenEveryTime()
    {
        // Arrange
        float[] logits = [0.5f, 2f, 1.9f, -1f];
        var history = new HashSet<long> { 1 };

        // Act
        var tokens = Enumerable.Range(0, 5).Select(_ => ChatterboxTurboTokenSelector.Select(logits, history)).ToArray();

        // Assert
        Assert.All(tokens, token => Assert.Equal(2, token));
    }

    /// <summary>Proves a generated token still wins when its penalized score stays ahead of the rivals.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_RepeatedTokenFarAhead_StillWins()
    {
        // Act: 5.0 / 1.2 = 4.17 > 1.0
        var token = ChatterboxTurboTokenSelector.Select([1f, 5f], new HashSet<long> { 1 });

        // Assert
        Assert.Equal(1, token);
    }

    /// <summary>Proves all-NaN logits terminate and resolve to the lowest id.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_AllNaN_ReturnsLowestId()
    {
        // Act
        var token = ChatterboxTurboTokenSelector.Select([float.NaN, float.NaN, float.NaN], new HashSet<long>());

        // Assert
        Assert.Equal(0, token);
    }

    /// <summary>Proves all-negative-infinity logits terminate and resolve to the lowest unblocked id.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_AllNegativeInfinity_ReturnsLowestUnblockedId()
    {
        // Arrange
        float[] logits = [float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity];

        // Act
        var token = ChatterboxTurboTokenSelector.Select(logits, new HashSet<long>(), 0);

        // Assert
        Assert.Equal(1, token);
    }

    /// <summary>Proves a NaN logit never beats a real score.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_NaNAmongRealScores_IsNeverChosen()
    {
        // Act
        var token = ChatterboxTurboTokenSelector.Select([float.NaN, -5f, float.NaN], new HashSet<long>());

        // Assert
        Assert.Equal(1, token);
    }

    /// <summary>Proves a NaN logit of a generated id is still never chosen.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_NaNWithHistoryId_IsNeverChosen()
    {
        // Act
        var token = ChatterboxTurboTokenSelector.Select([float.NaN, -5f], new HashSet<long> { 0 });

        // Assert
        Assert.Equal(1, token);
    }

    /// <summary>Proves a positive-infinity logit is chosen over finite scores.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_PositiveInfinity_IsChosen()
    {
        // Act
        var token = ChatterboxTurboTokenSelector.Select([1f, float.PositiveInfinity, 100f], new HashSet<long>());

        // Assert
        Assert.Equal(1, token);
    }

    /// <summary>Proves a positive-infinity logit of a generated id stays infinite after the penalty and is still chosen.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_PositiveInfinityWithHistoryId_IsChosen()
    {
        // Act
        var token = ChatterboxTurboTokenSelector.Select([1f, float.PositiveInfinity, 100f], new HashSet<long> { 1 });

        // Assert
        Assert.Equal(1, token);
    }

    /// <summary>Proves a negative-infinity logit of a generated id never beats a finite score.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_NegativeInfinityWithHistoryId_IsNotChosen()
    {
        // Act
        var token = ChatterboxTurboTokenSelector.Select([float.NegativeInfinity, -50f], new HashSet<long> { 0 });

        // Assert
        Assert.Equal(1, token);
    }

    /// <summary>Proves a blocked id that is also in the history is still skipped.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_BlockedTokenInHistory_IsSkipped()
    {
        // Act
        var token = ChatterboxTurboTokenSelector.Select([5f, 1f, 0.5f], new HashSet<long> { 0 }, 0);

        // Assert
        Assert.Equal(1, token);
    }

    /// <summary>Proves a negative blocked id other than -1 blocks nothing.</summary>
    [Fact]
    public void ChatterboxTurboTokenSelector_Select_NegativeBlockedIdOtherThanNone_BlocksNothing()
    {
        // Act
        var token = ChatterboxTurboTokenSelector.Select([0.1f, 2f, 1f], new HashSet<long>(), -5);

        // Assert
        Assert.Equal(1, token);
    }
}
