using DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.RecognitionSubsystem;

/// <summary>Unit tests for <see cref="NemotronRnntGreedyDecoder"/> against a scripted network.</summary>
public sealed class NemotronRnntGreedyDecoderTests
{
    /// <summary>The blank id used by the fake.</summary>
    private const int Blank = 9;

    /// <summary>Proves the constructor resets the network, and Reset resets it again.</summary>
    [Fact]
    public void Reset_ResetsNetwork()
    {
        var network = new FakeRnntNetwork(Blank);
        var decoder = new NemotronRnntGreedyDecoder(network);
        Assert.Equal(1, network.ResetCount);

        decoder.Reset();

        Assert.Equal(2, network.ResetCount);
    }

    /// <summary>Proves an all-blank network emits nothing and does not advance the state.</summary>
    [Fact]
    public void Decode_AllBlank_EmitsNothing()
    {
        var network = new FakeRnntNetwork(Blank);
        var decoder = new NemotronRnntGreedyDecoder(network);
        var tokens = new List<int>();

        var emitted = decoder.Decode(new float[3 * 4], 3, 4, tokens);

        Assert.Equal(0, emitted);
        Assert.Empty(tokens);
        Assert.Empty(network.Advanced);
        Assert.Equal(3, network.Predictions);
    }

    /// <summary>Proves emitted tokens are collected in order and advance the prediction network.</summary>
    [Fact]
    public void Decode_Tokens_AreCollectedAndAdvanced()
    {
        var network = new FakeRnntNetwork(Blank, 1, 2, Blank, 3, Blank);
        var decoder = new NemotronRnntGreedyDecoder(network);
        var tokens = new List<int>();

        var emitted = decoder.Decode(new float[2 * 4], 2, 4, tokens);

        Assert.Equal(3, emitted);
        Assert.Equal([1, 2, 3], tokens);
        Assert.Equal([1, 2, 3], network.Advanced);
    }

    /// <summary>Proves at most ten symbols are emitted per frame.</summary>
    [Fact]
    public void Decode_NeverBlank_StopsAtTenSymbolsPerFrame()
    {
        var network = new FakeRnntNetwork(Blank, Enumerable.Repeat(1, 100).ToArray());
        var decoder = new NemotronRnntGreedyDecoder(network);
        var tokens = new List<int>();

        var emitted = decoder.Decode(new float[2 * 4], 2, 4, tokens);

        Assert.Equal(20, emitted);
    }

    /// <summary>Proves Dispose disposes the network.</summary>
    [Fact]
    public void Dispose_DisposesNetwork()
    {
        var network = new FakeRnntNetwork(Blank);

        new NemotronRnntGreedyDecoder(network).Dispose();

        Assert.True(network.Disposed);
    }

    /// <summary>Proves the blank penalty lets a close non-blank token win over blank.</summary>
    [Fact]
    public void SelectToken_BlankWithinPenalty_PrefersNonBlank()
    {
        // Blank (index 2) leads by 2.0, less than the 3.0 penalty, so token 1 wins.
        var logits = new[] { 0.0f, 5.0f, 7.0f };

        Assert.Equal(1, NemotronRnntNetwork.SelectToken(logits, 2));
    }

    /// <summary>Proves blank still wins when it leads by more than the penalty.</summary>
    [Fact]
    public void SelectToken_BlankBeyondPenalty_ReturnsBlank()
    {
        var logits = new[] { 0.0f, 5.0f, 9.0f };

        Assert.Equal(2, NemotronRnntNetwork.SelectToken(logits, 2));
    }

    /// <summary>Proves the penalty applies only to the blank id.</summary>
    [Fact]
    public void SelectToken_NonBlankIds_AreNotPenalized()
    {
        var logits = new[] { 4.0f, 1.0f, -2.0f };

        Assert.Equal(0, NemotronRnntNetwork.SelectToken(logits, 2));
    }

    /// <summary>Proves null arguments are rejected.</summary>
    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new NemotronRnntGreedyDecoder(null!));
        var decoder = new NemotronRnntGreedyDecoder(new FakeRnntNetwork(Blank));
        Assert.Throws<ArgumentNullException>(() => decoder.Decode(new float[4], 1, 4, null!));
    }
}
