using System.Text;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for <see cref="ChatterboxTurboReferenceVoice"/> and its internal audio resampler.
/// </summary>
public sealed class ChatterboxTurboReferenceVoiceTests : IDisposable
{
    /// <summary>A scratch directory unique to this test instance.</summary>
    private readonly string _root;

    /// <summary>Initializes a fresh scratch directory.</summary>
    public ChatterboxTurboReferenceVoiceTests()
    {
        _root = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    /// <summary>Deletes the scratch directory.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup only.
        }
    }

    /// <summary>Proves 24 kHz mono audio is kept as-is and the id doubles as the display name.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromSamples_Mono24k_KeepsSamples()
    {
        // Arrange
        var samples = Tone(24000, 24000, 440);

        // Act
        var voice = ChatterboxTurboReferenceVoice.FromSamples("tone", samples, 24000);

        // Assert
        Assert.Equal("tone", voice.Id);
        Assert.Equal("tone", voice.DisplayName);
        Assert.Equal(24000, voice.Samples.Length);
    }

    /// <summary>Proves stereo 48 kHz audio is down-mixed and resampled to mono 24 kHz.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromSamples_Stereo48k_IsDownMixedAndResampled()
    {
        // Arrange: two seconds of identical-channel stereo at 48 kHz
        var mono = Tone(96000, 48000, 440);
        var stereo = mono.SelectMany(s => new[] { s, s }).ToArray();

        // Act
        var voice = ChatterboxTurboReferenceVoice.FromSamples("s", stereo, 48000, channelCount: 2, displayName: "Stereo");

        // Assert
        Assert.Equal("Stereo", voice.DisplayName);
        Assert.InRange(voice.Samples.Length, 47990, 48010);
        Assert.InRange(voice.Samples.Span.ToArray().Max(Math.Abs), 0.5f, 1.01f);
    }

    /// <summary>Proves a clip shorter than the minimum is rejected.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromSamples_TooShort_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => ChatterboxTurboReferenceVoice.FromSamples("x", new float[1000], 24000));
    }

    /// <summary>Proves a clip longer than the maximum is trimmed to it.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromSamples_TooLong_IsTrimmed()
    {
        // Act
        var voice = ChatterboxTurboReferenceVoice.FromSamples("long", new float[24000 * 40], 24000);

        // Assert
        Assert.Equal(24000 * 30, voice.Samples.Length);
    }

    /// <summary>Proves invalid arguments are rejected.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromSamples_InvalidArguments_Throw()
    {
        // Assert
        Assert.Throws<ArgumentException>(() => ChatterboxTurboReferenceVoice.FromSamples(" ", new float[48000], 24000));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChatterboxTurboReferenceVoice.FromSamples("x", new float[48000], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChatterboxTurboReferenceVoice.FromSamples("x", new float[48000], 24000, 0));
    }

    /// <summary>Proves a 16-bit PCM WAV file is read.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromFile_Pcm16Wav_LoadsVoice()
    {
        // Arrange
        var path = Path.Combine(_root, "a.wav");
        File.WriteAllBytes(path, Wav(Tone(24000, 24000, 300), 24000, 1, format: 1, bits: 16));

        // Act
        var voice = ChatterboxTurboReferenceVoice.FromFile("a", path);

        // Assert
        Assert.Equal(24000, voice.Samples.Length);
    }

    /// <summary>Proves a stereo WAV file is accepted and down-mixed.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromFile_StereoWav_LoadsVoice()
    {
        // Arrange
        var mono = Tone(24000, 24000, 300);
        var path = Path.Combine(_root, "s.wav");
        File.WriteAllBytes(path, Wav(mono.SelectMany(s => new[] { s, s }).ToArray(), 24000, 2, format: 1, bits: 16));

        // Act
        var voice = ChatterboxTurboReferenceVoice.FromFile("s", path);

        // Assert
        Assert.Equal(24000, voice.Samples.Length);
    }

    /// <summary>Proves a missing file, a non-PCM16 file and a non-WAV file are reported distinctly.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromFile_BadFiles_ThrowDistinctExceptions()
    {
        // Arrange
        var float32 = Path.Combine(_root, "f.wav");
        File.WriteAllBytes(float32, Wav(Tone(24000, 24000, 300), 24000, 1, format: 3, bits: 32));
        var garbage = Path.Combine(_root, "g.wav");
        File.WriteAllBytes(garbage, Encoding.ASCII.GetBytes("this is not a wav file at all"));

        // Assert
        Assert.Throws<FileNotFoundException>(() => ChatterboxTurboReferenceVoice.FromFile("m", Path.Combine(_root, "none.wav")));
        Assert.Throws<NotSupportedException>(() => ChatterboxTurboReferenceVoice.FromFile("f", float32));
        Assert.Throws<InvalidDataException>(() => ChatterboxTurboReferenceVoice.FromFile("g", garbage));
    }

    /// <summary>Proves the resampler preserves duration and a low-frequency tone's amplitude, and is the identity at equal rates.</summary>
    [Fact]
    public void ReferenceAudioResampler_Resample_ChangesRateAndPreservesTone()
    {
        // Arrange
        var input = Tone(16000, 16000, 200);

        // Act
        var up = ReferenceAudioResampler.Resample(input, 16000, 24000);
        var same = ReferenceAudioResampler.Resample(input, 16000, 16000);

        // Assert
        Assert.Equal(24000, up.Length);
        Assert.InRange(up.Skip(2000).Take(20000).Max(), 0.9f, 1.05f);
        Assert.Equal(input, same);
    }

    /// <summary>Proves the down-mix averages interleaved channels.</summary>
    [Fact]
    public void ReferenceAudioResampler_DownMix_AveragesChannels()
    {
        // Act
        var mono = ReferenceAudioResampler.DownMix([1f, 0f, 0.5f, 0.5f], 2);

        // Assert
        Assert.Equal([0.5f, 0.5f], mono);
    }

    /// <summary>Builds a sine tone.</summary>
    private static float[] Tone(int count, int rate, double frequency) =>
        [.. Enumerable.Range(0, count).Select(i => (float)Math.Sin(2 * Math.PI * frequency * i / rate))];

    /// <summary>Builds a RIFF/WAVE image; 16-bit uses PCM samples, 32-bit writes raw float samples.</summary>
    private static byte[] Wav(float[] interleaved, int rate, int channels, int format, int bits)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var bytesPerSample = bits / 8;
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + (interleaved.Length * bytesPerSample));
        writer.Write("WAVEfmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)format);
        writer.Write((short)channels);
        writer.Write(rate);
        writer.Write(rate * channels * bytesPerSample);
        writer.Write((short)(channels * bytesPerSample));
        writer.Write((short)bits);
        writer.Write("data"u8.ToArray());
        writer.Write(interleaved.Length * bytesPerSample);
        foreach (var sample in interleaved)
        {
            if (bits == 16)
            {
                writer.Write((short)(sample * 32767));
            }
            else
            {
                writer.Write(sample);
            }
        }

        return stream.ToArray();
    }
}
