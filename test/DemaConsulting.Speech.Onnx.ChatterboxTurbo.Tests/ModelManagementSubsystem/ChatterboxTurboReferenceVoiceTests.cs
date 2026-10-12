using System.Text;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for <see cref="ChatterboxTurboReferenceVoice"/>.
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
        catch (UnauthorizedAccessException)
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

    /// <summary>Proves a float32 WAV file is read now that the core reader supports it.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromFile_Float32Wav_LoadsVoice()
    {
        // Arrange
        var path = Path.Combine(_root, "f.wav");
        File.WriteAllBytes(path, Wav(Tone(24000, 24000, 300), 24000, 1, format: 3, bits: 32));

        // Act
        var voice = ChatterboxTurboReferenceVoice.FromFile("f", path);

        // Assert
        Assert.Equal(24000, voice.Samples.Length);
    }

    /// <summary>Proves a FLAC file is decoded, resampled to 24 kHz and kept at its natural 1.2 s duration.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromFile_Flac_LoadsVoice()
    {
        // Arrange
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "voice-mono-16-16000.flac");

        // Act
        var voice = ChatterboxTurboReferenceVoice.FromFile("flac", path, "Flac voice");

        // Assert
        Assert.Equal("flac", voice.Id);
        Assert.Equal(28800, voice.Samples.Length);
    }

    /// <summary>Proves a missing file, an unsupported encoding and a non-audio file are reported distinctly.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromFile_BadFiles_ThrowDistinctExceptions()
    {
        // Arrange: format tag 2 (ADPCM) is not supported
        var adpcm = Path.Combine(_root, "f.wav");
        File.WriteAllBytes(adpcm, Wav(Tone(24000, 24000, 300), 24000, 1, format: 2, bits: 16));
        var garbage = Path.Combine(_root, "g.wav");
        File.WriteAllBytes(garbage, Encoding.ASCII.GetBytes("this is not a wav file at all"));

        // Assert
        Assert.Throws<FileNotFoundException>(() => ChatterboxTurboReferenceVoice.FromFile("m", Path.Combine(_root, "none.wav")));
        Assert.Throws<NotSupportedException>(() => ChatterboxTurboReferenceVoice.FromFile("f", adpcm));
        Assert.Throws<InvalidDataException>(() => ChatterboxTurboReferenceVoice.FromFile("g", garbage));
    }

    /// <summary>Proves a lazy file voice defers decoding: it is created for a corrupt file and fails, naming the file, on first use.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromFileLazy_CorruptFile_FailsOnFirstUseNamingFile()
    {
        // Arrange
        var path = Path.Combine(_root, "broken.wav");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes("this is not a wav file at all"));

        // Act
        var voice = ChatterboxTurboReferenceVoice.FromFileLazy("broken", path, "Broken");

        // Assert
        Assert.Equal("broken", voice.Id);
        Assert.Equal("Broken", voice.DisplayName);
        var exception = Assert.Throws<InvalidDataException>(() => voice.Samples);
        Assert.Contains("broken.wav", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves a lazy file voice decodes a valid file on first use and reuses the result.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromFileLazy_ValidFile_LoadsOnceOnFirstUse()
    {
        // Arrange
        var path = Path.Combine(_root, "ok.wav");
        File.WriteAllBytes(path, Wav(Tone(24000, 24000, 300), 24000, 1, format: 1, bits: 16));
        var voice = ChatterboxTurboReferenceVoice.FromFileLazy("ok", path, "Ok");

        // Act
        var first = voice.Samples;

        // Assert
        Assert.Equal(24000, first.Length);
        Assert.True(first.Span == voice.Samples.Span);
    }

    /// <summary>Proves concurrent first use of a lazy file voice yields one shared, fully decoded clip for every caller.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromFileLazy_ConcurrentFirstUse_ReturnsOneSharedClip()
    {
        // Arrange
        var path = Path.Combine(_root, "shared.wav");
        File.WriteAllBytes(path, Wav(Tone(24000, 24000, 300), 24000, 1, format: 1, bits: 16));
        var voice = ChatterboxTurboReferenceVoice.FromFileLazy("shared", path, "Shared");

        // Act
        var clips = Enumerable.Range(0, 8)
            .AsParallel()
            .WithDegreeOfParallelism(8)
            .Select(_ => voice.Samples)
            .ToArray();

        // Assert
        Assert.All(clips, clip => Assert.Equal(24000, clip.Length));
        Assert.All(clips, clip => Assert.True(clip.Span == clips[0].Span));
    }

    /// <summary>Proves mood clips decode lazily, are normalized and named case-insensitively, and a missing mood is reported.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_WithMoodFiles_LoadsMoodClipsLazilyAndReportsMissingMoods()
    {
        // Arrange
        var basePath = Path.Combine(_root, "base.wav");
        var sadPath = Path.Combine(_root, "sad.wav");
        File.WriteAllBytes(basePath, Wav(Tone(24000, 24000, 300), 24000, 1, format: 1, bits: 16));
        File.WriteAllBytes(sadPath, Wav(Tone(48000, 24000, 200), 24000, 1, format: 1, bits: 16));
        var plain = ChatterboxTurboReferenceVoice.FromFileLazy("ok", basePath);

        // Act
        var voice = ChatterboxTurboReferenceVoice.FromFileLazy(
            "ok",
            basePath,
            "Ok",
            new Dictionary<string, string> { ["SAD"] = sadPath, ["shout"] = Path.Combine(_root, "missing.wav") });

        // Assert: creating the voice read nothing, so a missing file only fails when that mood is first used
        Assert.Empty(plain.Moods);
        Assert.Equal(["sad", "shout"], voice.Moods);
        Assert.True(voice.HasMood("sad"));
        Assert.False(voice.HasMood("calm"));
        Assert.True(voice.TryGetMoodSamples("sad", out var sad));
        Assert.Equal(48000, sad.Length);
        Assert.False(voice.TryGetMoodSamples("calm", out _));
        Assert.Equal(24000, voice.Samples.Length);
        Assert.Throws<InvalidDataException>(() => voice.TryGetMoodSamples("shout", out _));
    }

    /// <summary>Proves a too-short mood clip fails on first use naming the voice and mood, not at creation.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_WithMoodFiles_TooShortClip_FailsOnFirstUse()
    {
        // Arrange
        var basePath = Path.Combine(_root, "base.wav");
        var shortPath = Path.Combine(_root, "short.wav");
        File.WriteAllBytes(basePath, Wav(Tone(24000, 24000, 300), 24000, 1, format: 1, bits: 16));
        File.WriteAllBytes(shortPath, Wav(Tone(1000, 24000, 300), 24000, 1, format: 1, bits: 16));

        // Act
        var voice = ChatterboxTurboReferenceVoice.FromFileLazy("v", basePath).WithMoodFiles(new Dictionary<string, string> { ["calm"] = shortPath });

        // Assert
        var exception = Assert.Throws<InvalidDataException>(() => voice.TryGetMoodSamples("calm", out _));
        Assert.Contains("v/calm", exception.Message, StringComparison.Ordinal);
        Assert.Contains("short.wav", exception.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => voice.WithMoodFiles(new Dictionary<string, string> { [" "] = shortPath }));
        Assert.Throws<ArgumentException>(() => voice.WithMoodFiles(new Dictionary<string, string> { ["calm"] = " " }));
        var unchanged = voice.WithMoodFiles(null);
        Assert.Equal(["calm"], unchanged.Moods);
        Assert.Equal(voice.Id, unchanged.Id);
        Assert.Equal(voice.DisplayName, unchanged.DisplayName);
    }

    /// <summary>Proves mood names are trimmed and lower-cased, and a later clip of the same name replaces an earlier one.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_WithMoodFiles_NamesAreTrimmedAndLowerCased()
    {
        // Arrange
        var basePath = Path.Combine(_root, "base.wav");
        var moodPath = Path.Combine(_root, "mood.wav");
        File.WriteAllBytes(basePath, Wav(Tone(24000, 24000, 300), 24000, 1, format: 1, bits: 16));
        File.WriteAllBytes(moodPath, Wav(Tone(36000, 24000, 200), 24000, 1, format: 1, bits: 16));

        // Act
        var voice = ChatterboxTurboReferenceVoice.FromFileLazy("v", basePath)
            .WithMoodFiles(new Dictionary<string, string> { ["  Sad "] = moodPath });

        // Assert
        Assert.Equal(["sad"], voice.Moods);
        Assert.True(voice.TryGetMoodSamples("sad", out var samples));
        Assert.Equal(36000, samples.Length);
        Assert.True(voice.HasMood("Sad"));
    }

    /// <summary>Proves mood lookups match case-insensitively after trimming, and null or blank names never match.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_MoodLookup_IsCaseInsensitiveAndTrimmed()
    {
        // Arrange
        var basePath = Path.Combine(_root, "base.wav");
        var moodPath = Path.Combine(_root, "mood.wav");
        File.WriteAllBytes(basePath, Wav(Tone(24000, 24000, 300), 24000, 1, format: 1, bits: 16));
        File.WriteAllBytes(moodPath, Wav(Tone(36000, 24000, 200), 24000, 1, format: 1, bits: 16));
        var voice = ChatterboxTurboReferenceVoice.FromFileLazy("v", basePath)
            .WithMoodFiles(new Dictionary<string, string> { ["sad"] = moodPath });

        // Act and Assert
        foreach (var name in new[] { "sad", "SAD", "Sad", "  sAd\t", " sad" })
        {
            Assert.True(voice.HasMood(name), name);
            Assert.True(voice.TryGetMoodSamples(name, out var samples), name);
            Assert.Equal(36000, samples.Length);
        }

        foreach (var name in new[] { null, "", " ", "\t", "calm", "sa d" })
        {
            Assert.False(voice.HasMood(name), name);
            Assert.False(voice.TryGetMoodSamples(name, out var missing), name);
            Assert.True(missing.IsEmpty);
        }
    }

    /// <summary>Proves FromFile reports a too-short clip and blank arguments.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromFile_TooShortOrBlankArguments_Throw()
    {
        // Arrange
        var shortPath = Path.Combine(_root, "short.wav");
        File.WriteAllBytes(shortPath, Wav(Tone(1000, 24000, 300), 24000, 1, format: 1, bits: 16));

        // Assert
        Assert.Throws<ArgumentException>(() => ChatterboxTurboReferenceVoice.FromFile("s", shortPath));
        Assert.Throws<ArgumentException>(() => ChatterboxTurboReferenceVoice.FromFile(" ", shortPath));
        Assert.Throws<ArgumentException>(() => ChatterboxTurboReferenceVoice.FromFile("s", " "));
        Assert.Throws<ArgumentException>(() => ChatterboxTurboReferenceVoice.FromFileLazy(" ", shortPath));
        Assert.Throws<ArgumentException>(() => ChatterboxTurboReferenceVoice.FromFileLazy("s", " "));
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
