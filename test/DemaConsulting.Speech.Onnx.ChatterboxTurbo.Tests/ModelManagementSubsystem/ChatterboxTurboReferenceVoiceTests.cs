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
        Assert.True(samples.AsSpan().SequenceEqual(voice.Samples.Span));
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

        // The 440 Hz tone keeps its frequency: two seconds hold about 880 rising zero crossings
        var span = voice.Samples.Span;
        var risingCrossings = 0;
        for (var i = 1; i < span.Length; i++)
        {
            if (span[i - 1] < 0 && span[i] >= 0)
            {
                risingCrossings++;
            }
        }

        Assert.InRange(risingCrossings, 870, 890);
    }

    /// <summary>Proves a clip shorter than the minimum is rejected.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromSamples_TooShort_Throws()
    {
        // Arrange
        var samples = new float[1000];

        // Act
        var exception = Record.Exception(() => ChatterboxTurboReferenceVoice.FromSamples("x", samples, 24000));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    /// <summary>Proves a clip longer than the maximum is trimmed to it.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromSamples_TooLong_IsTrimmed()
    {
        // Arrange
        var samples = new float[24000 * 40];

        // Act
        var voice = ChatterboxTurboReferenceVoice.FromSamples("long", samples, 24000);

        // Assert
        Assert.Equal(24000 * 30, voice.Samples.Length);
    }

    /// <summary>Proves invalid arguments are rejected.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromSamples_InvalidArguments_Throw()
    {
        // Arrange
        var samples = new float[48000];

        // Act
        var nullSamplesId = Record.Exception(() => ChatterboxTurboReferenceVoice.FromSamples(null!, samples, 24000));
        var nullFilePath = Record.Exception(() => ChatterboxTurboReferenceVoice.FromFile("x", null!));
        var nullFileId = Record.Exception(() => ChatterboxTurboReferenceVoice.FromFile(null!, "x.wav"));
        var blankId = Record.Exception(() => ChatterboxTurboReferenceVoice.FromSamples(" ", samples, 24000));
        var zeroRate = Record.Exception(() => ChatterboxTurboReferenceVoice.FromSamples("x", samples, 0));
        var zeroChannels = Record.Exception(() => ChatterboxTurboReferenceVoice.FromSamples("x", samples, 24000, 0));

        // Assert
        Assert.IsType<ArgumentNullException>(nullSamplesId);
        Assert.IsType<ArgumentNullException>(nullFilePath);
        Assert.IsType<ArgumentNullException>(nullFileId);
        Assert.IsType<ArgumentException>(blankId);
        Assert.IsType<ArgumentOutOfRangeException>(zeroRate);
        Assert.IsType<ArgumentOutOfRangeException>(zeroChannels);
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

    /// <summary>Proves a float32 WAV file is read.</summary>
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

        // Act
        var missing = Record.Exception(() => ChatterboxTurboReferenceVoice.FromFile("m", Path.Combine(_root, "none.wav")));
        var unsupported = Record.Exception(() => ChatterboxTurboReferenceVoice.FromFile("f", adpcm));
        var malformed = Record.Exception(() => ChatterboxTurboReferenceVoice.FromFile("g", garbage));

        // Assert
        Assert.IsType<FileNotFoundException>(missing);
        Assert.IsType<NotSupportedException>(unsupported);
        Assert.IsType<InvalidDataException>(malformed);
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
        var exception = Record.Exception(() => voice.Samples);

        // Assert
        Assert.Equal("broken", voice.Id);
        Assert.Equal("Broken", voice.DisplayName);
        var invalid = Assert.IsType<InvalidDataException>(exception);
        Assert.Contains("broken.wav", invalid.Message, StringComparison.Ordinal);
        Assert.StartsWith("Reference voice 'broken' (", invalid.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a lazy file voice decodes a valid file on first use and keeps the result: deleting the file after
    ///     the first use leaves the later reads unchanged, so the file was not read again.
    /// </summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromFileLazy_ValidFile_LoadsOnceOnFirstUse()
    {
        // Arrange
        var path = Path.Combine(_root, "ok.wav");
        File.WriteAllBytes(path, Wav(Tone(24000, 24000, 300), 24000, 1, format: 1, bits: 16));
        var voice = ChatterboxTurboReferenceVoice.FromFileLazy("ok", path, "Ok");

        // Act
        var first = voice.Samples;
        File.Delete(path);
        var second = voice.Samples;

        // Assert
        Assert.Equal(24000, first.Length);
        Assert.True(first.Span == second.Span);
    }

    /// <summary>Proves a lazy voice does not decode until first use and then decodes exactly once.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromLoader_DecodesOnlyOnFirstUseAndOnlyOnce()
    {
        // Arrange
        var decodes = 0;
        var voice = ChatterboxTurboReferenceVoice.FromLoader("v", () =>
        {
            Interlocked.Increment(ref decodes);
            return new float[24000];
        });
        var beforeUse = decodes;

        // Act
        _ = voice.Samples;
        _ = voice.Samples;

        // Assert
        Assert.Equal(0, beforeUse);
        Assert.Equal(1, decodes);
    }

    /// <summary>Proves a failed decode is not cached, so the next use retries.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromLoader_FailureIsNotCached()
    {
        // Arrange
        var attempts = 0;
        var voice = ChatterboxTurboReferenceVoice.FromLoader("v", () =>
            Interlocked.Increment(ref attempts) == 1 ? throw new InvalidDataException("bad") : new float[24000]);

        // Act
        var failure = Record.Exception(() => voice.Samples);
        var retried = voice.Samples;

        // Assert
        Assert.IsType<InvalidDataException>(failure);
        Assert.Equal(24000, retried.Length);
        Assert.Equal(2, attempts);
    }

    /// <summary>Proves a blank or null mood name is rejected by the test-only mood helper.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_WithMoodSamples_BlankMood_Throws()
    {
        // Arrange
        var voice = ChatterboxTurboReferenceVoice.FromSamples("v", new float[24000], 24000);

        // Act
        var nullMood = Record.Exception(() => voice.WithMoodSamples(null!, new float[24000], 24000));
        var blankMood = Record.Exception(() => voice.WithMoodSamples(" ", new float[24000], 24000));

        // Assert
        Assert.IsType<ArgumentNullException>(nullMood);
        Assert.IsType<ArgumentException>(blankMood);
    }

    /// <summary>Proves concurrent first use of a lazy voice decodes once and yields one shared, fully decoded clip for every caller.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromLoader_ConcurrentFirstUse_DecodesOnceAndSharesClip()
    {
        // Arrange
        var decodes = 0;
        var voice = ChatterboxTurboReferenceVoice.FromLoader("shared", () =>
        {
            Interlocked.Increment(ref decodes);
            SpinWait.SpinUntil(() => false, 50);
            return Tone(24000, 24000, 300);
        });
        using var gate = new Barrier(8);

        // Act
        var tasks = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() =>
            {
                gate.SignalAndWait();
                return voice.Samples;
            }))
            .ToArray();
        var clips = tasks.Select(t => t.GetAwaiter().GetResult()).ToArray();

        // Assert: one decode, one shared clip
        Assert.Equal(1, decodes);
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
        var foundSad = voice.TryGetMoodSamples("sad", out var sad);
        var foundCalm = voice.TryGetMoodSamples("calm", out _);
        var shoutFailure = Record.Exception(() => voice.TryGetMoodSamples("shout", out _));

        // Assert: creating the voice read nothing, so a missing file only fails when that mood is first used
        Assert.Empty(plain.Moods);
        Assert.Equal(["sad", "shout"], voice.Moods);
        Assert.True(voice.HasMood("sad"));
        Assert.False(voice.HasMood("calm"));
        Assert.True(foundSad);
        Assert.Equal(48000, sad.Length);
        Assert.False(foundCalm);
        Assert.Equal(24000, voice.Samples.Length);
        Assert.IsType<InvalidDataException>(shoutFailure);
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
        var exception = Record.Exception(() => voice.TryGetMoodSamples("calm", out _));

        // Assert
        var invalid = Assert.IsType<InvalidDataException>(exception);
        Assert.StartsWith("Reference voice 'v/calm' ('", invalid.Message, StringComparison.Ordinal);
        Assert.Contains(shortPath, invalid.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves blank mood names or paths are rejected and a null mood map adds nothing.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_WithMoodFiles_BlankEntriesAndNullMap_RejectedOrIgnored()
    {
        // Arrange
        var basePath = Path.Combine(_root, "base.wav");
        var moodPath = Path.Combine(_root, "mood.wav");
        File.WriteAllBytes(basePath, Wav(Tone(24000, 24000, 300), 24000, 1, format: 1, bits: 16));
        File.WriteAllBytes(moodPath, Wav(Tone(24000, 24000, 300), 24000, 1, format: 1, bits: 16));
        var voice = ChatterboxTurboReferenceVoice.FromFileLazy("v", basePath).WithMoodFiles(new Dictionary<string, string> { ["calm"] = moodPath });

        // Act
        var unchanged = voice.WithMoodFiles(null);
        var blankName = Record.Exception(() => voice.WithMoodFiles(new Dictionary<string, string> { [" "] = moodPath }));
        var blankPath = Record.Exception(() => voice.WithMoodFiles(new Dictionary<string, string> { ["calm"] = " " }));

        // Assert
        Assert.IsType<ArgumentException>(blankName);
        Assert.IsType<ArgumentException>(blankPath);
        Assert.Equal(["calm"], unchanged.Moods);
        Assert.Equal(voice.Id, unchanged.Id);
        Assert.Equal(voice.DisplayName, unchanged.DisplayName);
    }

    /// <summary>Proves a later clip of the same mood name replaces an earlier one.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_WithMoodFiles_SameNameAgain_ReplacesEarlierClip()
    {
        // Arrange
        var basePath = Path.Combine(_root, "base.wav");
        var firstPath = Path.Combine(_root, "first.wav");
        var secondPath = Path.Combine(_root, "second.wav");
        File.WriteAllBytes(basePath, Wav(Tone(24000, 24000, 300), 24000, 1, format: 1, bits: 16));
        File.WriteAllBytes(firstPath, Wav(Tone(30000, 24000, 200), 24000, 1, format: 1, bits: 16));
        File.WriteAllBytes(secondPath, Wav(Tone(40000, 24000, 200), 24000, 1, format: 1, bits: 16));
        var voice = ChatterboxTurboReferenceVoice.FromFileLazy("v", basePath)
            .WithMoodFiles(new Dictionary<string, string> { ["sad"] = firstPath });

        // Act
        var replaced = voice.WithMoodFiles(new Dictionary<string, string> { ["SAD"] = secondPath });

        // Assert
        Assert.Equal(["sad"], replaced.Moods);
        Assert.True(replaced.TryGetMoodSamples("sad", out var samples));
        Assert.Equal(40000, samples.Length);
        Assert.True(voice.TryGetMoodSamples("sad", out var original));
        Assert.Equal(30000, original.Length);
    }

    /// <summary>Proves an already-decoded mood clip is normalized to mono 24 kHz and named case-insensitively.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_WithMoodSamples_NormalizesClipAndTrimsName()
    {
        // Arrange
        var voice = ChatterboxTurboReferenceVoice.FromSamples("v", Tone(24000, 24000, 300), 24000);

        // Act: two seconds of 48 kHz mono become 48000 samples at 24 kHz
        var withMood = voice.WithMoodSamples(" Angry ", Tone(96000, 48000, 200), 48000);

        // Assert
        Assert.Equal(["angry"], withMood.Moods);
        Assert.True(withMood.TryGetMoodSamples("ANGRY", out var samples));
        Assert.InRange(samples.Length, 47990, 48010);
        Assert.Equal(24000, withMood.Samples.Length);
        Assert.Empty(voice.Moods);
    }

    /// <summary>Proves mood names are trimmed and lower-cased.</summary>
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

    /// <summary>Proves the mood names are listed in ordinal ascending order regardless of the order they were supplied in.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_Moods_AreListedInOrdinalAscendingOrder()
    {
        // Arrange
        var basePath = Path.Combine(_root, "base.wav");
        var moodPath = Path.Combine(_root, "mood.wav");
        File.WriteAllBytes(basePath, Wav(Tone(24000, 24000, 300), 24000, 1, format: 1, bits: 16));
        File.WriteAllBytes(moodPath, Wav(Tone(24000, 24000, 200), 24000, 1, format: 1, bits: 16));

        // Act
        var voice = ChatterboxTurboReferenceVoice.FromFileLazy("v", basePath, moodClips: new Dictionary<string, string>
        {
            ["zeta"] = moodPath,
            ["Alpha"] = moodPath,
            ["mid"] = moodPath,
        });

        // Assert
        Assert.Equal(["alpha", "mid", "zeta"], voice.Moods);
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

        // Act
        var results = new[] { "sad", "SAD", "Sad", "  sAd\t", " sad" }
            .Select(name => (Name: name, Has: voice.HasMood(name), Found: voice.TryGetMoodSamples(name, out var samples), Samples: samples))
            .ToArray();
        var misses = new[] { null, "", " ", "\t", "calm", "sa d" }
            .Select(name => (Name: name, Has: voice.HasMood(name), Found: voice.TryGetMoodSamples(name, out var samples), Samples: samples))
            .ToArray();

        // Assert
        foreach (var hit in results)
        {
            Assert.True(hit.Has, hit.Name);
            Assert.True(hit.Found, hit.Name);
            Assert.Equal(36000, hit.Samples.Length);
        }

        foreach (var miss in misses)
        {
            Assert.False(miss.Has, miss.Name);
            Assert.False(miss.Found, miss.Name);
            Assert.True(miss.Samples.IsEmpty);
        }
    }

    /// <summary>Proves FromFile reports a too-short clip and blank arguments.</summary>
    [Fact]
    public void ChatterboxTurboReferenceVoice_FromFile_TooShortOrBlankArguments_Throw()
    {
        // Arrange
        var shortPath = Path.Combine(_root, "short.wav");
        File.WriteAllBytes(shortPath, Wav(Tone(1000, 24000, 300), 24000, 1, format: 1, bits: 16));

        // Act
        var tooShort = Record.Exception(() => ChatterboxTurboReferenceVoice.FromFile("s", shortPath));
        var blankId = Record.Exception(() => ChatterboxTurboReferenceVoice.FromFile(" ", shortPath));
        var blankPath = Record.Exception(() => ChatterboxTurboReferenceVoice.FromFile("s", " "));
        var lazyBlankId = Record.Exception(() => ChatterboxTurboReferenceVoice.FromFileLazy(" ", shortPath));
        var lazyBlankPath = Record.Exception(() => ChatterboxTurboReferenceVoice.FromFileLazy("s", " "));
        var lazyNullPath = Record.Exception(() => ChatterboxTurboReferenceVoice.FromFileLazy("s", null!));

        // Assert
        Assert.IsType<ArgumentException>(tooShort);
        Assert.IsType<ArgumentException>(blankId);
        Assert.IsType<ArgumentException>(blankPath);
        Assert.IsType<ArgumentException>(lazyBlankId);
        Assert.IsType<ArgumentException>(lazyBlankPath);
        Assert.IsType<ArgumentNullException>(lazyNullPath);
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
