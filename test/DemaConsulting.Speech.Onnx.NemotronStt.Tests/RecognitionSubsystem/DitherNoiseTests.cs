using DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.RecognitionSubsystem;

/// <summary>Unit tests for <see cref="DitherNoise"/>.</summary>
public sealed class DitherNoiseTests
{
    /// <summary>Proves the same seed yields identical noise, and Reset replays it.</summary>
    [Fact]
    public void Apply_SameSeed_IsDeterministicAndResettable()
    {
        var a = new float[1000];
        var b = new float[1000];
        var dither = new DitherNoise(1e-3f, 7);
        var other = new DitherNoise(1e-3f, 7);

        dither.Apply(a);
        other.Apply(b);
        var c = new float[1000];
        dither.Reset();
        dither.Apply(c);

        Assert.Equal(a, b);
        Assert.Equal(a, c);
        Assert.Contains(a, v => v != 0f);
    }

    /// <summary>Proves the noise is roughly zero-mean with the requested standard deviation.</summary>
    [Fact]
    public void Apply_Statistics_MatchAmplitude()
    {
        var samples = new float[100000];
        new DitherNoise(0.01f, 3).Apply(samples);

        var mean = samples.Average(v => (double)v);
        var std = Math.Sqrt(samples.Average(v => Math.Pow(v - mean, 2)));

        Assert.InRange(mean, -0.001, 0.001);
        Assert.InRange(std, 0.009, 0.011);
    }

    /// <summary>Proves a zero amplitude leaves the samples untouched.</summary>
    [Fact]
    public void Apply_ZeroAmplitude_LeavesSamples()
    {
        var samples = new float[] { 0.1f, -0.2f, 0.3f };

        new DitherNoise(0f).Apply(samples);

        Assert.Equal(new[] { 0.1f, -0.2f, 0.3f }, samples);
    }

    /// <summary>Proves different seeds give different noise.</summary>
    [Fact]
    public void Apply_DifferentSeeds_Differ()
    {
        var a = new float[100];
        var b = new float[100];

        new DitherNoise(1e-3f, 1).Apply(a);
        new DitherNoise(1e-3f, 2).Apply(b);

        Assert.NotEqual(a, b);
    }
}
