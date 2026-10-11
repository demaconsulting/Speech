using DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.RecognitionSubsystem;

/// <summary>
///     Unit tests for <see cref="SilenceRunLimiter"/> using synthetic signals: digital silence,
///     low-level noise, tone bursts, an immediate speech start, and very quiet speech.
/// </summary>
public sealed class SilenceRunLimiterTests
{
    /// <summary>Samples per second.</summary>
    private const int Rate = 16000;

    /// <summary>Builds <paramref name="seconds"/> of constant-amplitude uniform noise from a seeded generator.</summary>
    private static float[] Noise(double seconds, double amplitude, int seed = 1)
    {
        var random = new Random(seed);
        var samples = new float[(int)(seconds * Rate)];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)(((random.NextDouble() * 2) - 1) * amplitude);
        }

        return samples;
    }

    /// <summary>Builds <paramref name="seconds"/> of a 300 Hz sine tone.</summary>
    private static float[] Tone(double seconds, double amplitude)
    {
        var samples = new float[(int)(seconds * Rate)];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)(amplitude * Math.Sin(2 * Math.PI * 300 * i / Rate));
        }

        return samples;
    }

    /// <summary>Runs the whole signal through a limiter, then flushes, returning the output.</summary>
    private static List<float> Run(SilenceRunLimiter limiter, params float[][] parts)
    {
        var output = new List<float>();
        foreach (var part in parts)
        {
            limiter.Process(part, output);
        }

        limiter.Flush(output);
        return output;
    }

    /// <summary>Proves a long run of digital silence is truncated to the 400 ms cap.</summary>
    [Fact]
    public void Process_DigitalSilence_TruncatesToCap()
    {
        var limiter = new SilenceRunLimiter();

        var output = Run(limiter, new float[3 * Rate]);

        Assert.Equal(0.4 * Rate, output.Count);
        Assert.Equal((3 * Rate) - output.Count, limiter.DroppedSamples);
        Assert.Equal(0, limiter.LoudSamples);
    }

    /// <summary>Proves steady 1e-3 noise (below any clamped threshold) is truncated as quiet.</summary>
    [Fact]
    public void Process_LowLevelNoise_TruncatesToCap()
    {
        var limiter = new SilenceRunLimiter();

        var output = Run(limiter, Noise(3, 1e-3));

        Assert.Equal(0.4 * Rate, output.Count);
        Assert.True(limiter.IsCalibrated);
        Assert.InRange(limiter.Threshold, 0.0015, 0.006);
    }

    /// <summary>Proves speech-level tone bursts pass through untouched.</summary>
    [Fact]
    public void Process_ToneBurst_PassesThrough()
    {
        var limiter = new SilenceRunLimiter();
        var tone = Tone(1, 0.3);

        var output = Run(limiter, tone);

        Assert.Equal(tone.Length, output.Count);
        Assert.Equal(tone.Length, limiter.LoudSamples);
        Assert.Equal(0, limiter.DroppedSamples);
    }

    /// <summary>Proves leading silence is truncated but the speech that follows is fully kept.</summary>
    [Fact]
    public void Process_SilenceThenTone_KeepsAllToneSamples()
    {
        var limiter = new SilenceRunLimiter();
        var tone = Tone(1, 0.3);

        var output = Run(limiter, new float[3 * Rate], tone);

        Assert.Equal((int)(0.4 * Rate) + tone.Length, output.Count);
        Assert.Equal(tone.Length, limiter.LoudSamples);
    }

    /// <summary>Proves speech starting at sample zero is not eaten by calibration or the gate.</summary>
    [Fact]
    public void Process_ImmediateSpeechStart_KeepsEverything()
    {
        var limiter = new SilenceRunLimiter();
        var signal = Tone(0.5, 0.2).Concat(new float[Rate]).Concat(Tone(0.5, 0.2)).ToArray();

        var output = Run(limiter, signal);

        // The 1 s gap is shortened to 400 ms; both speech bursts remain complete.
        Assert.Equal(signal.Length - (int)(0.6 * Rate), output.Count);
        Assert.Equal(Rate, limiter.LoudSamples);
    }

    /// <summary>Proves very quiet speech above the clamp ceiling is still detected as loud.</summary>
    [Fact]
    public void Process_VeryQuietSpeech_IsNotDropped()
    {
        var limiter = new SilenceRunLimiter();
        var quietSpeech = Tone(1, 0.02);

        var output = Run(limiter, new float[Rate], quietSpeech);

        Assert.Equal(quietSpeech.Length, limiter.LoudSamples);
        Assert.Equal((int)(0.4 * Rate) + quietSpeech.Length, output.Count);
    }

    /// <summary>Proves a loud calibration window cannot push the threshold above the maximum clamp.</summary>
    [Fact]
    public void Process_LoudCalibration_ThresholdClampedToMaximum()
    {
        var limiter = new SilenceRunLimiter();

        Run(limiter, Tone(1, 0.5));

        Assert.Equal(new SilenceRunLimiterOptions().MaxThreshold, limiter.Threshold);
    }

    /// <summary>Proves digital silence calibration cannot drive the threshold below the minimum clamp.</summary>
    [Fact]
    public void Process_SilentCalibration_ThresholdClampedToMinimum()
    {
        var limiter = new SilenceRunLimiter();

        Run(limiter, new float[Rate]);

        Assert.Equal(new SilenceRunLimiterOptions().MinThreshold, limiter.Threshold);
    }

    /// <summary>Proves that before calibration completes, audio still flows through.</summary>
    [Fact]
    public void Process_DuringCalibration_AudioStillFlows()
    {
        var limiter = new SilenceRunLimiter();
        var output = new List<float>();

        limiter.Process(Tone(0.1, 0.3), output);

        Assert.False(limiter.IsCalibrated);
        Assert.Equal(1600, output.Count);
    }

    /// <summary>Proves the non-adaptive mode keeps the fixed default threshold.</summary>
    [Fact]
    public void Process_NonAdaptive_KeepsDefaultThreshold()
    {
        var options = new SilenceRunLimiterOptions(Adaptive: false, DefaultThreshold: 0.01);
        var limiter = new SilenceRunLimiter(options);

        Run(limiter, Noise(1, 1e-3));

        Assert.Equal(0.01, limiter.Threshold);
    }

    /// <summary>Proves feeding the audio in odd-sized pieces gives the same output as one call.</summary>
    [Fact]
    public void Process_ChunkedFeed_EqualsSingleFeed()
    {
        var signal = new float[Rate].Concat(Tone(0.3, 0.3)).Concat(Noise(2, 1e-3)).Concat(Tone(0.2, 0.3)).ToArray();
        var whole = Run(new SilenceRunLimiter(), signal);

        var pieces = new List<float[]>();
        for (var offset = 0; offset < signal.Length; offset += 777)
        {
            pieces.Add(signal[offset..Math.Min(signal.Length, offset + 777)]);
        }

        var chunked = Run(new SilenceRunLimiter(), [.. pieces]);

        Assert.Equal(whole, chunked);
    }

    /// <summary>Proves the partial trailing frame is emitted by Flush.</summary>
    [Fact]
    public void Flush_PartialFrame_IsEmitted()
    {
        var limiter = new SilenceRunLimiter();
        var output = new List<float>();
        limiter.Process(Tone(0.0125, 0.3), output);
        Assert.True(output.Count < 200);

        limiter.Flush(output);

        Assert.Equal(200, output.Count);
    }

    /// <summary>Proves Reset restores the start-of-stream state, including calibration.</summary>
    [Fact]
    public void Reset_AfterUse_RestoresInitialState()
    {
        var limiter = new SilenceRunLimiter();
        Run(limiter, Noise(2, 1e-3));

        limiter.Reset();

        Assert.False(limiter.IsCalibrated);
        Assert.Equal(0, limiter.QuietRunSamples);
        Assert.Equal(0, limiter.DroppedSamples);
        Assert.Equal(0, limiter.LoudSamples);
        Assert.Equal(new SilenceRunLimiterOptions().DefaultThreshold, limiter.Threshold);
    }

    /// <summary>Proves the quiet-run length tracks the input clock and resets on loud audio.</summary>
    [Fact]
    public void QuietRunSamples_TracksInputClock()
    {
        var limiter = new SilenceRunLimiter();
        var output = new List<float>();

        limiter.Process(new float[Rate], output);
        var quiet = limiter.QuietRunSamples;
        limiter.Process(Tone(0.1, 0.3), output);

        Assert.Equal(Rate, quiet);
        Assert.Equal(0, limiter.QuietRunSamples);
    }
}
