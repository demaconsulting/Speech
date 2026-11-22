using System.Globalization;
using DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.RecognitionSubsystem;

/// <summary>
///     Unit tests for <see cref="NemotronFeatureExtractor"/>: golden values from an independent
///     implementation of the NeMo front-end formula (see <c>TestData/NOTICE.md</c>), the
///     constant-padding frame layout, and streaming-versus-one-shot equivalence.
/// </summary>
public sealed class NemotronFeatureExtractorTests
{
    /// <summary>The largest allowed absolute difference from a golden value (natural-log units).</summary>
    private const float GoldenTolerance = 1e-3f;

    /// <summary>The sample rate of the synthetic signals.</summary>
    private const double Rate = 16000;

    /// <summary>Builds the deterministic two-tone signal (1600 samples).</summary>
    private static float[] TwoTone()
    {
        var x = new float[1600];
        for (var i = 0; i < x.Length; i++)
        {
            x[i] = (float)((0.3 * Math.Sin(2 * Math.PI * 440 * i / Rate)) + (0.1 * Math.Sin(2 * Math.PI * 1234 * i / Rate)));
        }

        return x;
    }

    /// <summary>Builds one of the synthetic signals the golden values were computed from.</summary>
    /// <param name="name">The signal name used in the golden file.</param>
    /// <returns>The samples.</returns>
    private static float[] Signal(string name)
    {
        switch (name)
        {
            case "two-tone":
                return TwoTone();
            case "chirp":
                // Linear sweep from 100 Hz to 7000 Hz over 0.2 s
                var chirp = new float[3200];
                const double duration = 3200 / Rate;
                for (var i = 0; i < chirp.Length; i++)
                {
                    var t = i / Rate;
                    chirp[i] = (float)(0.5 * Math.Sin(2 * Math.PI * ((100 * t) + ((7000 - 100) * t * t / (2 * duration)))));
                }

                return chirp;
            case "silence":
                return new float[800];
            default:
                var tone = new float[100];
                for (var i = 0; i < tone.Length; i++)
                {
                    tone[i] = (float)(0.5 * Math.Sin(2 * Math.PI * 1000 * i / Rate));
                }

                return tone;
        }
    }

    /// <summary>Reads the golden rows of one signal: frame index and the 128 band values.</summary>
    /// <param name="name">The signal name.</param>
    /// <returns>The golden frames in order.</returns>
    private static List<float[]> Golden(string name)
    {
        var path = Path.Join(AppContext.BaseDirectory, "TestData", "nemotron-features-golden.tsv");
        return File.ReadLines(path)
            .Where(line => !line.StartsWith('#') && line.Length > 0)
            .Select(line => line.Split('\t'))
            .Where(fields => fields[0] == name)
            .OrderBy(fields => int.Parse(fields[1], CultureInfo.InvariantCulture))
            .Select(fields => fields.Skip(2).Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray())
            .ToList();
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

    /// <summary>Proves every feature value matches the independent NeMo-formula reference within 1e-3.</summary>
    /// <param name="name">The golden signal.</param>
    [Theory]
    [InlineData("two-tone")]
    [InlineData("chirp")]
    [InlineData("silence")]
    [InlineData("short")]
    public void NemotronFeatureExtractor_Extract_GoldenSignals_MatchNeMoReference(string name)
    {
        // Arrange: the signal and its reference frames
        var signal = Signal(name);
        var golden = Golden(name);

        // Act: extract the signal in one piece
        var features = Extract(signal, signal.Length, out var frames);

        // Assert: the same number of frames, and every band of every frame within tolerance
        Assert.Equal(golden.Count, frames);
        for (var frame = 0; frame < frames; frame++)
        {
            for (var band = 0; band < NemotronFeatureExtractor.MelBands; band++)
            {
                var actual = features[(frame * NemotronFeatureExtractor.MelBands) + band];
                Assert.True(
                    Math.Abs(actual - golden[frame][band]) <= GoldenTolerance,
                    $"{name} frame {frame} band {band}: expected {golden[frame][band]}, got {actual}.");
            }
        }
    }

    /// <summary>Proves the constant center padding yields len / 160 + 1 frames, the NeMo frame count.</summary>
    /// <param name="samples">The input length.</param>
    /// <param name="expectedFrames">The expected frame count.</param>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(159, 1)]
    [InlineData(160, 2)]
    [InlineData(1600, 11)]
    public void NemotronFeatureExtractor_Flush_InputLength_ProducesNeMoFrameCount(int samples, int expectedFrames)
    {
        // Arrange: digital silence of the given length
        var signal = new float[samples];

        // Act: extract and flush
        Extract(signal, samples, out var frames);

        // Assert: one frame per hop plus the frame centered on the first sample
        Assert.Equal(expectedFrames, frames);
    }

    /// <summary>Proves streaming in odd-sized pieces equals the one-shot result.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(97)]
    [InlineData(500)]
    public void Extract_Chunked_EqualsOneShot(int piece)
    {
        // Arrange: the two-tone signal and its one-shot features
        var signal = TwoTone();
        var whole = Extract(signal, signal.Length, out var wholeFrames);

        // Act: extract the same signal in pieces
        var chunked = Extract(signal, piece, out var chunkedFrames);

        // Assert: identical frames
        Assert.Equal(wholeFrames, chunkedFrames);
        Assert.Equal(whole, chunked);
    }

    /// <summary>Proves frames are emitted while streaming, before any flush, once the samples after their center exist.</summary>
    [Fact]
    public void NemotronFeatureExtractor_Accept_StreamingInput_EmitsFramesWithoutFlush()
    {
        // Arrange: a fresh extractor
        var extractor = new NemotronFeatureExtractor();

        // Act: accept 416 samples (frame 1 needs samples up to 160 + 256)
        extractor.Accept(new float[416]);

        // Assert: frames 0 and 1 are available without a flush
        Assert.Equal(2, extractor.FrameCount);
    }

    /// <summary>Proves flushing with no audio produces no frames.</summary>
    [Fact]
    public void Flush_NoAudio_ProducesNoFrames()
    {
        // Arrange: a fresh extractor
        var extractor = new NemotronFeatureExtractor();

        // Act: flush without audio
        extractor.Flush();

        // Assert: nothing was produced
        Assert.Equal(0, extractor.FrameCount);
    }

    /// <summary>Proves digital silence maps to the 2^-24 log guard.</summary>
    [Fact]
    public void Extract_Silence_IsLogEpsilon()
    {
        // Arrange / Act: extract 0.1 s of digital silence
        var features = Extract(new float[1600], 1600, out _);

        // Assert: every value is log(2^-24)
        Assert.Equal(NemotronFeatureExtractor.LogEpsilon, Math.Pow(2, -24));
        Assert.All(features, v => Assert.Equal((float)Math.Log(NemotronFeatureExtractor.LogEpsilon), v, 1e-3f));
    }

    /// <summary>Proves Reset restores the start-of-stream state so the same signal gives the same features.</summary>
    [Fact]
    public void Reset_AfterUse_ReproducesFeatures()
    {
        // Arrange: an extractor that has processed and flushed the signal once
        var signal = TwoTone();
        var extractor = new NemotronFeatureExtractor();
        extractor.Accept(signal);
        extractor.Flush();

        // Act: reset and process the signal again
        extractor.Reset();
        var framesAfterReset = extractor.FrameCount;
        extractor.Accept(signal);
        extractor.Flush();
        var again = new float[extractor.FrameCount * NemotronFeatureExtractor.MelBands];
        extractor.Dequeue(extractor.FrameCount, again);

        // Assert: the queue was emptied and the features match a fresh extraction
        Assert.Equal(0, framesAfterReset);
        Assert.Equal(Extract(signal, signal.Length, out _), again);
    }
}
