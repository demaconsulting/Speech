using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.Piper.Tests.ModelManagementSubsystem;

/// <summary>
///     Behavior tests for <see cref="OnnxPiperLibriTtsEnSynthesisModel"/>: its catalog facts, the speaker parameter and
///     its resolution, the download descriptor, the installer that keeps only the graph and the configuration of the
///     release archive, and loading a backend from an installed directory (with a tiny stand-in graph, never the real
///     voice).
/// </summary>
public sealed class OnnxPiperLibriTtsEnSynthesisModelTests : IDisposable
{
    private const string ArchiveFolder = "vits-piper-en_US-libritts_r-medium/";

    private readonly string _root = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Onnx.Piper.Tests", Guid.NewGuid().ToString("N"));

    /// <summary>Initializes a scratch directory that no test shares.</summary>
    public OnnxPiperLibriTtsEnSynthesisModelTests()
    {
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

    /// <summary>Proves the catalog facts: id, role, no audio tags, CC BY 4.0 with its link, the shared English text analyzer.</summary>
    [Fact]
    public void OnnxPiperLibriTtsEnSynthesisModel_CatalogFacts_AreDeclared()
    {
        // Arrange
        ISynthesisModel model = new OnnxPiperLibriTtsEnSynthesisModel();

        // Assert
        Assert.Equal("piper-onnx-libritts-r-en-us-medium", model.Id);
        Assert.Equal(SpeechModelRole.Synthesis, model.Role);
        Assert.Equal(SpeechModelAudioTagSupport.None, model.AudioTagSupport);
        Assert.Equal("CC BY 4.0", model.LicenseName);
        Assert.Equal("https://creativecommons.org/licenses/by/4.0/", model.LicenseUrl?.ToString());
        Assert.Equal(22050, model.PreferredAudioFormat.SampleRate);
        Assert.Equal(1, model.PreferredAudioFormat.ChannelCount);
        Assert.NotNull(model.TextAnalyzer);
        Assert.Same(model.TextAnalyzer, new OnnxPiperLibriTtsEnSynthesisModel().TextAnalyzer);
    }

    /// <summary>
    ///     Proves the model's capability profile speaks a sentence as one segment: the clause pieces the core chunker makes (one per
    ///     comma, semicolon and colon) are joined with one space, the text-analysis annotations move with their clause and still
    ///     cover the same words, and a sentence end, a pause, an ellipsis and the chunk budget stay boundaries; the library's default
    ///     profile on the same spans gives the clause pieces separately.
    /// </summary>
    [Fact]
    public void OnnxPiperLibriTtsEnSynthesisModel_CapabilityProfile_SentenceClauses_AreOneSegmentWithTheirAnnotations()
    {
        // Arrange
        var model = new OnnxPiperLibriTtsEnSynthesisModel();
        var profile = ((ISynthesisModel)model).CapabilityProfile;
        var analyzer = model.TextAnalyzer!;

        // Act: clauses, a number and an abbreviation inside the clauses, then a second sentence
        const string text = "Wait for 5 minutes, then ask Dr. Smith; he will know. It was late, so we left.";
        var analysis = analyzer.Analyze(text, CancellationToken.None);
        IReadOnlyList<TaggedTextSpan> spans = [TaggedTextSpan.PlainText(text, analysis.Annotations, analysis.SentenceBreaks)];
        var plan = profile.Render(spans, model);
        var defaultPlan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert: two sentence segments (the default profile has one per clause), commas kept in place, the same words in the same order
        Assert.Equal(["Wait for 5 minutes, then ask Dr. Smith; he will know.", "It was late, so we left."], plan.Segments.Select(s => s.Text));
        Assert.True(defaultPlan.Segments.Count >= 4);
        Assert.Equal(string.Join(' ', defaultPlan.Segments.Select(s => s.Text)), string.Join(' ', plan.Segments.Select(s => s.Text)));

        // Assert: every annotation of the joined segments covers the very text the same annotation covered in its clause piece
        var covered = defaultPlan.Segments.SelectMany(s => s.Annotations.Select(a => (a.Kind, a.Value, Text: s.Text.Substring(a.Start, a.Length))));
        var joinedCovered = plan.Segments.SelectMany(s => s.Annotations.Select(a => (a.Kind, a.Value, Text: s.Text.Substring(a.Start, a.Length))));
        Assert.NotEmpty(covered);
        Assert.Equal(covered, joinedCovered);
    }

    /// <summary>
    ///     Proves what keeps two pieces apart: a pause between them, a different set of parameter overrides, silence after the
    ///     first, a first piece that ends a sentence, a joined text beyond the utterance budget, and nothing at all to join; and that
    ///     a piece that ends in a clause mark, a dash or a word (a long clause the core chunker cut) is joined to the next.
    /// </summary>
    [Fact]
    public void OnnxPiperLibriTtsEnSynthesisModel_CapabilityProfile_JoinsOnlyWhereNothingSetsTheClausesApart()
    {
        // Arrange
        var slow = new Dictionary<string, object> { ["speed"] = 0.8 };
        var slowToo = new Dictionary<string, object> { ["speed"] = 0.8 };
        var fast = new Dictionary<string, object> { ["speed"] = 1.2 };
        var longClause = new string('a', (PiperCapabilityProfile.MaxJoinedLength / 2) + 10);

        // Act and assert: joined, with the silences and overrides of the pair
        var joined = Assert.Single(PiperCapabilityProfile.JoinClauses([
            new SpeechSegment("One,", 7, 0, slow),
            new SpeechSegment("two;", 0, 0, slowToo),
            new SpeechSegment("three:", 0, 0, slow),
            new SpeechSegment("four.", 0, 9, slow),
        ]));
        Assert.Equal("One, two; three: four.", joined.Text);
        Assert.Equal(7, joined.PreSilenceMs);
        Assert.Equal(9, joined.PostSilenceMs);
        Assert.Same(slow, joined.ParameterOverrides);

        // Assert: a piece cut at a dash or inside a clause is the start of the same utterance
        Assert.Equal("the task before us -- that from these dead we take devotion", Assert.Single(PiperCapabilityProfile.JoinClauses([
            new SpeechSegment("the task before us --", 0, 0, null),
            new SpeechSegment("that from these dead", 0, 0, null),
            new SpeechSegment("we take devotion", 0, 0, null),
        ])).Text);

        // Assert: kept apart
        Assert.Equal(2, PiperCapabilityProfile.JoinClauses([new SpeechSegment("One,", 0, 0, slow), new SpeechSegment("two.", 0, 0, fast)]).Count);
        Assert.Equal(2, PiperCapabilityProfile.JoinClauses([new SpeechSegment("One,", 0, 0, slow), new SpeechSegment("two.", 0, 0, null)]).Count);
        Assert.Equal(2, PiperCapabilityProfile.JoinClauses([new SpeechSegment("One,", 0, 300, null), new SpeechSegment("two.", 0, 0, null)]).Count);
        Assert.Equal(2, PiperCapabilityProfile.JoinClauses([new SpeechSegment("One,", 0, 0, null), new SpeechSegment("two.", 300, 0, null)]).Count);
        Assert.Equal(2, PiperCapabilityProfile.JoinClauses([new SpeechSegment("One.", 0, 0, null), new SpeechSegment("Two.", 0, 0, null)]).Count);
        Assert.Equal(2, PiperCapabilityProfile.JoinClauses([new SpeechSegment("Is it?", 0, 0, null), new SpeechSegment("Two", 0, 0, null)]).Count);
        Assert.Equal(2, PiperCapabilityProfile.JoinClauses([new SpeechSegment("One,", 0, 0, null), new SpeechSegment(string.Empty, 0, 300, null)]).Count);
        Assert.Equal(2, PiperCapabilityProfile.JoinClauses([new SpeechSegment(longClause + ",", 0, 0, null), new SpeechSegment(longClause + ".", 0, 0, null)]).Count);
        Assert.Empty(PiperCapabilityProfile.JoinClauses([]));
        Assert.Throws<ArgumentNullException>(() => PiperCapabilityProfile.JoinClauses(null!));
    }

    /// <summary>
    ///     Proves a pause tag stays a boundary through the whole profile (the silence is its own segment, the clause before and after
    ///     it are not joined across it) and that invalid arguments are rejected.
    /// </summary>
    [Fact]
    public void OnnxPiperLibriTtsEnSynthesisModel_CapabilityProfile_PauseTag_StaysABoundary()
    {
        // Arrange
        var model = new OnnxPiperLibriTtsEnSynthesisModel();
        var profile = ((ISynthesisModel)model).CapabilityProfile;
        IReadOnlyList<TaggedTextSpan> spans =
        [
            TaggedTextSpan.PlainText("Wait here, please,"),
            TaggedTextSpan.TagSpan(NaturalLanguageAudioTag.ShortPause),
            TaggedTextSpan.PlainText("then go, quickly."),
        ];

        // Act
        var plan = profile.Render(spans, model);

        // Assert: text, silence, text; the clauses on either side are joined within themselves only
        Assert.Equal(3, plan.Segments.Count);
        Assert.Equal("Wait here, please,", plan.Segments[0].Text);
        Assert.Equal(string.Empty, plan.Segments[1].Text);
        Assert.Equal(DefaultModelCapabilityProfile.ShortPauseMilliseconds, plan.Segments[1].PostSilenceMs);
        Assert.Equal("then go, quickly.", plan.Segments[2].Text);
        Assert.Throws<ArgumentNullException>(() => profile.Render(null!, model));
        Assert.Throws<ArgumentNullException>(() => profile.Render(spans, null!));
    }

    /// <summary>
    ///     Proves an analyzer sentence boundary stays a boundary even when the sentence before it ends in a clause mark: the sentence
    ///     <c>Consider this:</c> is not joined to the paragraph that follows it (so its first word keeps its own utterance start), while
    ///     the clause pieces inside each sentence still join, and the annotations of both sentences stay on their own words.
    /// </summary>
    [Fact]
    public void OnnxPiperLibriTtsEnSynthesisModel_CapabilityProfile_AnalyzerSentenceBreakAfterClauseMark_StaysABoundary()
    {
        // Arrange
        var model = new OnnxPiperLibriTtsEnSynthesisModel();
        var profile = ((ISynthesisModel)model).CapabilityProfile;
        const string text = "Consider 5 options:\n\nShould we pick 7, or wait?";
        var analysis = model.TextAnalyzer!.Analyze(text, CancellationToken.None);
        IReadOnlyList<TaggedTextSpan> spans = [TaggedTextSpan.PlainText(text, analysis.Annotations, analysis.SentenceBreaks)];

        // Act
        var plan = profile.Render(spans, model);
        var defaultPlan = DefaultModelCapabilityProfile.Instance.Render(spans, model);

        // Assert: the analyzer put a break between the two sentences, the profile kept it, and joined the clauses of the second
        Assert.NotNull(analysis.SentenceBreaks);
        Assert.Contains(21, analysis.SentenceBreaks);
        Assert.Equal(["Consider 5 options:", "Should we pick 7, or wait?"], plan.Segments.Select(s => s.Text));
        Assert.Equal(3, defaultPlan.Segments.Count);

        // Assert: the annotations (the two numbers) stay on their own words in their own sentence
        var covered = plan.Segments.Select(s => s.Annotations.Select(a => s.Text.Substring(a.Start, a.Length)).ToArray()).ToArray();
        Assert.Equal(["5"], covered[0]);
        Assert.Equal(["7"], covered[1]);
    }

    /// <summary>
    ///     Proves an analyzer break that falls right after a tag (where no text holds it) is kept too, that a stripped tag inside a
    ///     sentence does not separate its clauses, and that a long sentence is still delivered in budget-sized segments.
    /// </summary>
    [Fact]
    public void OnnxPiperLibriTtsEnSynthesisModel_CapabilityProfile_TagsAndLongSentences_KeepTheBoundariesAndTheBudget()
    {
        // Arrange
        var model = new OnnxPiperLibriTtsEnSynthesisModel();
        var profile = ((ISynthesisModel)model).CapabilityProfile;
        var withBreak = TaggedTextSpan.TagSpan(NaturalLanguageAudioTag.Excited) with { SentenceBreaks = [1] };
        IReadOnlyList<TaggedTextSpan> spans =
        [
            TaggedTextSpan.PlainText("First we wait,", [], []),
            withBreak,
            TaggedTextSpan.PlainText("Then we go, quickly.", [], []),
            TaggedTextSpan.TagSpan(NaturalLanguageAudioTag.Excited),
            TaggedTextSpan.PlainText(" and, again.", [], []),
        ];
        var longSentence = string.Join(", ", Enumerable.Repeat("one clause of some length", 12)) + ".";
        IReadOnlyList<TaggedTextSpan> longSpans = [TaggedTextSpan.PlainText(longSentence, [], [])];

        // Act
        var plan = profile.Render(spans, model);
        var longPlan = profile.Render(longSpans, model);

        // Assert: the break after the first tag is kept (the stripped tag adds no text); the second tag sits mid-sentence and joins
        Assert.Equal(["First we wait,", "Then we go, quickly. and, again."], plan.Segments.Select(s => s.Text));
        Assert.True(longPlan.Segments.Count > 1);
        Assert.All(longPlan.Segments, s => Assert.True(s.Text.Length <= PiperCapabilityProfile.MaxJoinedLength));
        Assert.Equal(longSentence, string.Join(' ', longPlan.Segments.Select(s => s.Text)));
    }

    /// <summary>Proves the only parameter is an integer speaker selection from 0 to 903 whose default is 0.</summary>
    [Fact]
    public void OnnxPiperLibriTtsEnSynthesisModel_Parameters_AreTheSpeakerSelectionOnly()
    {
        // Arrange
        var model = new OnnxPiperLibriTtsEnSynthesisModel();

        // Act
        var parameter = Assert.IsType<NumericParameter>(Assert.Single(model.Parameters));

        // Assert
        Assert.Equal(OnnxPiperLibriTtsEnSynthesisModel.SpeakerParameterId, parameter.Id);
        Assert.True(parameter.IsInteger);
        Assert.Equal(0, parameter.Minimum);
        Assert.Equal(903, parameter.Maximum);
        Assert.Equal(0, parameter.Default);
    }

    /// <summary>Proves a speaker selection is resolved to its number, and that a missing, out-of-range or non-numeric value selects speaker 0.</summary>
    [Theory]
    [InlineData(null, 0)]
    [InlineData(7, 7)]
    [InlineData(7.0, 7)]
    [InlineData(7.4f, 7)]
    [InlineData(903, 903)]
    [InlineData(904, 0)]
    [InlineData(-1, 0)]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    [InlineData("7", 0)]
    public void OnnxPiperLibriTtsEnSynthesisModel_ResolveSpeakerId_Value_ResolvesOrFallsBackToZero(object? value, int expected)
    {
        // Arrange
        ISynthesisModel model = new OnnxPiperLibriTtsEnSynthesisModel();
        IReadOnlyDictionary<string, object>? bag = value is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { [OnnxPiperLibriTtsEnSynthesisModel.SpeakerParameterId] = value };

        // Act & Assert
        Assert.Equal(expected, model.ResolveSpeakerId(bag));
        Assert.Equal(0, model.ResolveSpeakerId(null));
    }

    /// <summary>Proves the download descriptor names one pinned HTTPS file (a non-empty SHA-256) that the installer unpacks.</summary>
    [Fact]
    public void OnnxPiperLibriTtsEnSynthesisModel_DownloadDescriptor_PinsOneHttpsArchive()
    {
        // Arrange
        var model = new OnnxPiperLibriTtsEnSynthesisModel();

        // Act
        var file = Assert.Single(model.DownloadDescriptor.Files);

        // Assert
        Assert.Equal("https", file.Uri.Scheme);
        Assert.EndsWith(".tar.bz2", file.Uri.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal("10dc268f3e371696d721486123e2705a9fc1faa113491979fde4d88dba1f1b1c", file.Sha256Checksum);
    }

    /// <summary>Proves the installer keeps only the graph and the configuration, flat in the installed directory, and removes the archive and everything else (the espeak-ng data, the token table).</summary>
    [Fact]
    public async Task OnnxPiperLibriTtsEnSynthesisModel_InstallAsync_ReleaseArchive_KeepsOnlyGraphAndConfiguration()
    {
        // Arrange
        var model = new OnnxPiperLibriTtsEnSynthesisModel();
        var archivePath = model.DownloadDescriptor.Files[0].ResolveStagedPath(_root);
        await File.WriteAllBytesAsync(archivePath, PiperTestFixtures.BuildArchive(
        [
            (ArchiveFolder + "en_US-libritts_r-medium.onnx", [1, 2, 3]),
            (ArchiveFolder + "en_US-libritts_r-medium.onnx.json", "{}"u8.ToArray()),
            (ArchiveFolder + "tokens.txt", "a 1"u8.ToArray()),
            (ArchiveFolder + "MODEL_CARD", "card"u8.ToArray()),
            (ArchiveFolder + "espeak-ng-data/en_dict", "dict"u8.ToArray()),
        ]), TestContext.Current.CancellationToken);

        // Act
        await model.InstallAsync(_root, TestContext.Current.CancellationToken);

        // Assert
        var installed = Directory.GetFileSystemEntries(_root).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["en_US-libritts_r-medium.onnx", "en_US-libritts_r-medium.onnx.json"], installed);
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(Path.Combine(_root, "en_US-libritts_r-medium.onnx"), TestContext.Current.CancellationToken));
    }

    /// <summary>Proves an entry path that tries to leave the installed directory still lands inside it, under its bare file name.</summary>
    [Fact]
    public async Task OnnxPiperLibriTtsEnSynthesisModel_InstallAsync_HostileEntryPath_StaysInsideInstalledDirectory()
    {
        // Arrange
        var staging = Path.Combine(_root, "staging");
        Directory.CreateDirectory(staging);
        var model = new OnnxPiperLibriTtsEnSynthesisModel();
        await File.WriteAllBytesAsync(model.DownloadDescriptor.Files[0].ResolveStagedPath(staging), PiperTestFixtures.BuildArchive(
        [
            ("../../evil.onnx", [9]),
            ("x/en_US-libritts_r-medium.onnx.json", "{}"u8.ToArray()),
        ]), TestContext.Current.CancellationToken);

        // Act
        await model.InstallAsync(staging, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(Directory.GetFiles(_root, "evil.onnx", SearchOption.TopDirectoryOnly));
        Assert.True(File.Exists(Path.Combine(staging, "evil.onnx")));
    }

    /// <summary>Proves an archive that lacks the graph or the configuration fails the install and is left in place for the downloader to discard.</summary>
    [Theory]
    [InlineData("en_US-libritts_r-medium.onnx")]
    [InlineData("en_US-libritts_r-medium.onnx.json")]
    [InlineData("tokens.txt")]
    public async Task OnnxPiperLibriTtsEnSynthesisModel_InstallAsync_ArchiveMissingAFile_Throws(string onlyEntry)
    {
        // Arrange
        var model = new OnnxPiperLibriTtsEnSynthesisModel();
        var archivePath = model.DownloadDescriptor.Files[0].ResolveStagedPath(_root);
        await File.WriteAllBytesAsync(archivePath, PiperTestFixtures.BuildArchive([(ArchiveFolder + onlyEntry, [1])]), TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => model.InstallAsync(_root, TestContext.Current.CancellationToken));
        Assert.True(File.Exists(archivePath));
    }

    /// <summary>Proves a backend is created from an installed directory (with a stand-in graph) at the voice's 22,050 Hz.</summary>
    [Fact]
    public void OnnxPiperLibriTtsEnSynthesisModel_CreateBackend_InstalledDirectory_ReturnsBackendAtVoiceRate()
    {
        // Arrange
        PiperTestFixtures.WriteInstalledEchoVoice(_root);
        ISynthesisModel model = new OnnxPiperLibriTtsEnSynthesisModel();

        // Act
        using var backend = model.CreateBackend(_root);

        // Assert
        Assert.Equal(22050, backend.SampleRate);
        Assert.IsAssignableFrom<IAnnotatedSynthesisBackend>(backend);
    }

    /// <summary>Proves a preferred execution provider that is not available is skipped and the backend is created on the CPU provider.</summary>
    [Fact]
    public void OnnxPiperLibriTtsEnSynthesisModel_CreateBackend_UnavailablePreferredProvider_FallsBackToCpu()
    {
        // Arrange
        PiperTestFixtures.WriteInstalledEchoVoice(_root);
        ISynthesisModel model = new OnnxPiperLibriTtsEnSynthesisModel(["NotARealExecutionProvider"]);

        // Act
        using var backend = model.CreateBackend(_root);
        var echo = PiperTestFixtures.DecodeEcho(backend.Generate("Hello there.", 1.0f, 0));

        // Assert
        Assert.Equal(22050, backend.SampleRate);
        Assert.NotEmpty(echo.Ids);
    }

    /// <summary>Proves a configuration that does not describe exactly this voice (904 speakers at 22,050 Hz) is refused, whether it has fewer speakers, more speakers or another rate.</summary>
    [Theory]
    [InlineData(1, 22050)]
    [InlineData(903, 22050)]
    [InlineData(905, 22050)]
    [InlineData(904, 16000)]
    public void OnnxPiperLibriTtsEnSynthesisModel_CreateBackend_ConfigurationOfAnotherVoice_Throws(int speakers, int sampleRate)
    {
        // Arrange
        ISynthesisModel model = new OnnxPiperLibriTtsEnSynthesisModel();
        PiperTestFixtures.WriteInstalledEchoVoice(_root);
        File.WriteAllText(
            Path.Combine(_root, "en_US-libritts_r-medium.onnx.json"),
            PiperTestFixtures.TrimmedConfigJson
                .Replace("\"num_speakers\": 904", $"\"num_speakers\": {speakers}", StringComparison.Ordinal)
                .Replace("\"sample_rate\": 22050", $"\"sample_rate\": {sampleRate}", StringComparison.Ordinal));

        // Act
        var error = Assert.Throws<InvalidDataException>(() => model.CreateBackend(_root));

        // Assert
        Assert.Contains("904 speakers at 22050 Hz", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves loading fails clearly for an unusable installation: a missing directory argument, a configuration for another voice, a configuration that is not usable, and a missing graph.</summary>
    [Fact]
    public void OnnxPiperLibriTtsEnSynthesisModel_CreateBackend_UnusableInstallation_Throws()
    {
        // Arrange
        ISynthesisModel model = new OnnxPiperLibriTtsEnSynthesisModel();
        var otherVoice = Path.Combine(_root, "other");
        PiperTestFixtures.WriteInstalledEchoVoice(otherVoice);
        File.WriteAllText(
            Path.Combine(otherVoice, "en_US-libritts_r-medium.onnx.json"),
            PiperTestFixtures.TrimmedConfigJson.Replace("\"num_speakers\": 904", "\"num_speakers\": 1", StringComparison.Ordinal));
        var broken = Path.Combine(_root, "broken");
        PiperTestFixtures.WriteInstalledEchoVoice(broken);
        File.WriteAllText(Path.Combine(broken, "en_US-libritts_r-medium.onnx.json"), "{}");
        var noGraph = Path.Combine(_root, "missing-graph");
        PiperTestFixtures.WriteInstalledEchoVoice(noGraph);
        File.Delete(Path.Combine(noGraph, "en_US-libritts_r-medium.onnx"));

        // Act & Assert
        Assert.Throws<ArgumentException>(() => model.CreateBackend(string.Empty));
        Assert.Throws<InvalidDataException>(() => model.CreateBackend(otherVoice));
        Assert.Throws<InvalidDataException>(() => model.CreateBackend(broken));
        Assert.ThrowsAny<Exception>(() => model.CreateBackend(noGraph));
    }
}
