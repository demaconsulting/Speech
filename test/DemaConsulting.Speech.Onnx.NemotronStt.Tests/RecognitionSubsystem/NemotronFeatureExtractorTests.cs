using DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.RecognitionSubsystem;

/// <summary>
///     Unit tests for <see cref="NemotronFeatureExtractor"/>: golden values captured from the
///     reference Python implementation (numpy/scipy), and streaming-versus-one-shot equivalence.
/// </summary>
public sealed class NemotronFeatureExtractorTests
{
    /// <summary>The golden-signal length (10 hops plus one, giving 11 frames).</summary>
    private const int GoldenSamples = 1600;

    /// <summary>Builds the deterministic two-tone signal the golden values were computed from.</summary>
    private static float[] GoldenSignal()
    {
        var x = new float[GoldenSamples];
        for (var i = 0; i < x.Length; i++)
        {
            x[i] = (float)((0.3 * Math.Sin(2 * Math.PI * 440 * i / 16000)) + (0.1 * Math.Sin(2 * Math.PI * 1234 * i / 16000)));
        }

        return x;
    }

    /// <summary>Extracts every frame for <paramref name="signal"/> fed in pieces of <paramref name="piece"/>.</summary>
    private static float[] Extract(float[] signal, int piece, out int frames)
    {
        var extractor = new NemotronFeatureExtractor();
        for (var offset = 0; offset < signal.Length; offset += piece)
        {
            extractor.Accept(signal.AsSpan(offset, Math.Min(piece, signal.Length - offset)));
        }

        extractor.Flush();
        frames = extractor.FrameCount;
        var result = new float[frames * NemotronFeatureExtractor.MelBands];
        extractor.Dequeue(frames, result);
        return result;
    }

    /// <summary>Proves the feature values equal the reference implementation's.</summary>
    [Fact]
    public void Extract_GoldenSignal_MatchesReference()
    {
        var features = Extract(GoldenSignal(), GoldenSamples, out var frames);

        Assert.Equal(11, frames);
        var expected = new (int Frame, int Band, float Value)[]
        {
            (0, 0, -7.8397f), (0, 40, -12.514f), (0, 127, -8.0513f),
            (5, 0, -18.4435f), (5, 40, -14.1931f), (5, 127, -23.0258f),
            (10, 0, -9.034f), (10, 40, -4.7077f), (10, 127, -12.7152f),
        };
        foreach (var (frame, band, value) in expected)
        {
            Assert.Equal(value, features[(frame * NemotronFeatureExtractor.MelBands) + band], 0.25f);
        }
    }

    /// <summary>Proves streaming in odd-sized pieces equals the one-shot result.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(97)]
    [InlineData(500)]
    public void Extract_Chunked_EqualsOneShot(int piece)
    {
        var signal = GoldenSignal();
        var whole = Extract(signal, signal.Length, out var wholeFrames);

        var chunked = Extract(signal, piece, out var chunkedFrames);

        Assert.Equal(wholeFrames, chunkedFrames);
        Assert.Equal(whole, chunked);
    }

    /// <summary>Proves a signal shorter than the reflect pad is zero-extended and still produces frames.</summary>
    [Fact]
    public void Flush_VeryShortInput_ProducesFrames()
    {
        var features = Extract(new float[50], 50, out var frames);

        Assert.True(frames >= 1);
        Assert.All(features, v => Assert.True(float.IsFinite(v)));
    }

    /// <summary>Proves flushing with no audio produces no frames.</summary>
    [Fact]
    public void Flush_NoAudio_ProducesNoFrames()
    {
        var extractor = new NemotronFeatureExtractor();

        extractor.Flush();

        Assert.Equal(0, extractor.FrameCount);
    }

    /// <summary>Proves silence maps to the log epsilon floor.</summary>
    [Fact]
    public void Extract_Silence_IsLogEpsilon()
    {
        var features = Extract(new float[1600], 1600, out _);

        Assert.All(features, v => Assert.Equal((float)Math.Log(NemotronFeatureExtractor.LogEpsilon), v, 1e-3f));
    }

    /// <summary>Proves Reset restores the start-of-stream state so the same signal gives the same features.</summary>
    [Fact]
    public void Reset_AfterUse_ReproducesFeatures()
    {
        var signal = GoldenSignal();
        var extractor = new NemotronFeatureExtractor();
        extractor.Accept(signal);
        extractor.Flush();
        extractor.Reset();
        Assert.Equal(0, extractor.FrameCount);

        extractor.Accept(signal);
        extractor.Flush();
        var again = new float[extractor.FrameCount * NemotronFeatureExtractor.MelBands];
        extractor.Dequeue(extractor.FrameCount, again);

        Assert.Equal(Extract(signal, signal.Length, out _), again);
    }
}
