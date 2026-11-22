using DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;
using Microsoft.ML.OnnxRuntime;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.RecognitionSubsystem;

/// <summary>
///     Tests for <see cref="NemotronRnntNetwork"/> against tiny synthetic ONNX decoder and joint
///     models that honor the real tensor contracts, covering the production input/output wiring,
///     the recurrent-state feedback and the joint score selection without the real model files.
/// </summary>
public sealed class NemotronRnntNetworkTests
{
    /// <summary>A blank id outside the range the tests predict.</summary>
    private const int Blank = 60;

    /// <summary>An encoder frame whose maximum is 1.</summary>
    private static float[] Frame()
    {
        var frame = new float[NemotronEncoder.EncoderHiddenSize];
        Array.Fill(frame, 1f);
        return frame;
    }

    /// <summary>Creates a network over the synthetic models.</summary>
    private static NemotronRnntNetwork CreateNetwork(int blankId = Blank) =>
        new(
            new InferenceSession(NemotronNetworkFixture.BuildDecoder()),
            new InferenceSession(NemotronNetworkFixture.BuildJoint()),
            blankId);

    /// <summary>Proves the joint sees the decoder output of the blank-primed network.</summary>
    [Fact]
    public void Reset_PredictToken_UsesPrimedDecoderOutput()
    {
        using var network = CreateNetwork();
        network.Reset();

        // Priming feeds the blank token with zero states, so the decoder output is 60 and the
        // joint score is 61.
        Assert.Equal(61, network.PredictToken(Frame()));
    }

    /// <summary>Proves the fed token and both recurrent states reach the next decoder call.</summary>
    [Fact]
    public void Advance_FeedsTokenAndRecurrentState()
    {
        using var network = CreateNetwork();
        network.Reset();

        // Reset: output = 60, h = 2, c = 1. Advance(5): output = 5 + 2 + 10 = 17, so the joint
        // score is 18.
        network.Advance(5);
        Assert.Equal(18, network.PredictToken(Frame()));

        // Advance(1): h = 4, c = 2, output = 1 + 4 + 20 = 25, so the joint score is 26.
        network.Advance(1);
        Assert.Equal(26, network.PredictToken(Frame()));
    }

    /// <summary>Proves Reset clears the recurrent state.</summary>
    [Fact]
    public void Reset_AfterAdvance_ClearsRecurrentState()
    {
        using var network = CreateNetwork();
        network.Reset();
        network.Advance(5);
        network.Advance(1);

        network.Reset();

        Assert.Equal(61, network.PredictToken(Frame()));
    }

    /// <summary>Proves the encoder frame is what the joint network scores.</summary>
    [Fact]
    public void PredictToken_UsesEncoderFrame()
    {
        using var network = CreateNetwork();
        network.Reset();
        network.Advance(5);
        var frame = new float[NemotronEncoder.EncoderHiddenSize];
        frame[^1] = 6f;

        // Decoder output 17 plus the frame maximum 6.
        Assert.Equal(23, network.PredictToken(frame));
    }

    /// <summary>Proves the blank penalty applies to the production joint output.</summary>
    [Fact]
    public void PredictToken_ScoreEqualsBlank_AppliesPenalty()
    {
        using var network = CreateNetwork(blankId: 18);
        network.Reset();
        network.Advance(5);

        // The joint score is 18, which is the blank id; the penalty lowers it below its neighbors.
        Assert.Equal(17, network.PredictToken(Frame()));
    }

    /// <summary>Proves use after disposal is rejected.</summary>
    [Fact]
    public void Dispose_BlocksFurtherUse()
    {
        var network = CreateNetwork();
        network.Dispose();

        Assert.Throws<ObjectDisposedException>(() => network.Advance(1));
        Assert.Throws<ObjectDisposedException>(() => network.PredictToken(Frame()));
    }

    /// <summary>Proves null sessions are rejected.</summary>
    [Fact]
    public void Constructor_NullSessions_Throw()
    {
        using var session = new InferenceSession(NemotronNetworkFixture.BuildJoint());

        Assert.Throws<ArgumentNullException>(() => new NemotronRnntNetwork(null!, session, Blank));
        Assert.Throws<ArgumentNullException>(() => new NemotronRnntNetwork(session, null!, Blank));
    }

    /// <summary>Proves a configured blank penalty reaches the production joint output selection.</summary>
    [Fact]
    public void NemotronRnntNetwork_PredictToken_ZeroPenalty_ReturnsBlankWhenBlankScoresHighest()
    {
        // Arrange: a network whose joint score 18 is the blank id, with no blank penalty
        using var network = new NemotronRnntNetwork(
            new InferenceSession(NemotronNetworkFixture.BuildDecoder()),
            new InferenceSession(NemotronNetworkFixture.BuildJoint()),
            blankId: 18,
            blankPenalty: 0f);
        network.Reset();
        network.Advance(5);

        // Act: predict the token for a frame whose top score is the blank id
        var token = network.PredictToken(Frame());

        // Assert: without a penalty the blank wins and the configured penalty is exposed
        Assert.Equal(18, token);
        Assert.Equal(0f, network.BlankPenalty);
    }

    /// <summary>Proves a zero penalty selects the plain arg-max token, even when that is blank.</summary>
    [Fact]
    public void NemotronRnntNetwork_SelectToken_ZeroPenalty_PrefersHighestLogit()
    {
        // Arrange: logits where blank (index 2) leads by half the default penalty
        var logits = new[] { 0.0f, 5.0f, 5.0f + (NemotronRnntNetwork.DefaultBlankPenalty / 2) };

        // Act: select with no penalty and with the default penalty
        var plain = NemotronRnntNetwork.SelectToken(logits, 2, 0f);
        var penalized = NemotronRnntNetwork.SelectToken(logits, 2, NemotronRnntNetwork.DefaultBlankPenalty);

        // Assert: plain greedy keeps blank; the default penalty prefers the close non-blank token
        Assert.Equal(2, plain);
        Assert.Equal(1, penalized);
    }

    /// <summary>Proves negative or non-finite blank penalties are rejected before the sessions are taken.</summary>
    [Theory]
    [InlineData(-0.5f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void NemotronRnntNetwork_Constructor_NegativeOrNonFiniteBlankPenalty_ThrowsArgumentOutOfRangeException(float penalty)
    {
        // Arrange: valid sessions
        using var decoder = new InferenceSession(NemotronNetworkFixture.BuildDecoder());
        using var joint = new InferenceSession(NemotronNetworkFixture.BuildJoint());

        // Act / Assert: the penalty is rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => new NemotronRnntNetwork(decoder, joint, Blank, penalty));
    }
}
