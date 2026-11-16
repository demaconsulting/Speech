using System.Xml.Linq;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
using DemaConsulting.Speech.Text.En.NormalizationSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests;

/// <summary>
///     System-level tests for SpeechTextEn: the English text analysis exercised through its public
///     API, end to end through the real core <c>SynthesisSession</c> and an annotation-aware
///     backend, and the package license files. The content of the license files is also asserted by the
///     <c>TextEn_PackageLicenseFiles_Content</c> FileAssert test; the package is not packable while it is gated.
/// </summary>
public sealed class SpeechTextEnTests
{
    /// <summary>The license file the package names as its license.</summary>
    private const string PackageLicenseFileName = "PACKAGE-LICENSE.md";

    /// <summary>The third-party notices file shipped next to the license file.</summary>
    private const string ThirdPartyNoticesFileName = "THIRD-PARTY-NOTICES.md";

    /// <summary>The package project folder, relative to the repository root.</summary>
    private const string PackageFolder = "src/DemaConsulting.Speech.Text.En";

    /// <summary>The SPDX expression of what the package itself contains: the code only, under the MIT License.</summary>
    private const string SpdxHeader = "SPDX-License-Identifier: MIT";

    /// <summary>The tag placeholder as a string, for building test texts.</summary>
    private const string Tag = "\uFFFC";

    /// <summary>Proves the analyzer tags the words of a text with a part of speech a backend can read, and leaves punctuation unannotated.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_Sentence_AnnotatesWordsWithReadablePartOfSpeech()
    {
        // Arrange
        const string text = "The cat sat on the mat.";
        ITextAnalyzer analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: one readable part-of-speech annotation per word, none on the final period
        Assert.Equal(6, result.Annotations.Count);
        Assert.All(result.Annotations, annotation => Assert.True(TextEnAnnotations.TryReadPartOfSpeech(annotation, out _)));
        Assert.Equal("NOUN", ReadTag(result, text, "cat"));
        Assert.Equal("DET", ReadTag(result, text, "The"));
        Assert.All(result.Annotations, annotation => Assert.NotEqual('.', text[annotation.Start]));
    }

    /// <summary>Proves selecting no annotations yields no annotations at all.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_PartOfSpeechDisabled_ProducesNoAnnotations()
    {
        // Arrange
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.None });

        // Act
        var result = analyzer.Analyze("The cat sat on the mat.", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Annotations);
    }

    /// <summary>Proves the analyzer supplies authoritative sentence breaks that are placeholder-safe.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_MultipleSentencesAndTag_ReturnsAuthoritativeBreaks()
    {
        // Arrange
        var text = $"Dr. Smith arrived.{Tag} He left. Nobody knew why.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: non-null, one break at the first word of each later sentence, none inside "Dr. Smith"
        Assert.NotNull(result.SentenceBreaks);
        Assert.Equal([text.IndexOf("He", StringComparison.Ordinal), text.IndexOf("Nobody", StringComparison.Ordinal)], result.SentenceBreaks);
    }

    /// <summary>Proves a placeholder is never covered by an annotation, and the words around it are still annotated.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_TagPlaceholder_IsNeverCoveredByAnAnnotation()
    {
        // Arrange
        var text = $"I've {Tag} read it. {Tag}{Tag}Say it again.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(result.Annotations);
        Assert.All(
            result.Annotations,
            annotation => Assert.DoesNotContain(Tag, text.Substring(annotation.Start, annotation.Length), StringComparison.Ordinal));
    }

    /// <summary>Proves an already-canceled token makes the analyzer throw OperationCanceledException.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_CanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        ITextAnalyzer analyzer = new EnglishTextAnalyzer();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => analyzer.Analyze("The cat sat.", cancellation.Token));
    }

    /// <summary>Proves one analyzer shared by many concurrent requests gives every request the same result.</summary>
    [Fact]
    public async Task SpeechTextEn_Analyze_ConcurrentRequestsOnSharedAnalyzer_ReturnIdenticalResults()
    {
        // Arrange
        const string text = "Please read the record. I will record it later. Then we leave.";
        var analyzer = new EnglishTextAnalyzer();
        var expected = Describe(analyzer.Analyze(text, TestContext.Current.CancellationToken));

        // Act
        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(
                () => Describe(analyzer.Analyze(text, TestContext.Current.CancellationToken)),
                TestContext.Current.CancellationToken))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, result => Assert.Equal(expected, result));
    }

    /// <summary>Proves an analyzed request reports one counts-only event and a faulty sink never interrupts the analysis.</summary>
    [Fact]
    public void SpeechTextEn_Diagnostics_AnalyzedRequest_ReportsCountsOnlyAndSinkFailureIsContained()
    {
        // Arrange: words that cannot occur in the fixed message
        const string text = "Quokka gazelles zigzag.";
        var recording = new RecordingDiagnostics();
        var recordingAnalyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Diagnostics = recording });
        var faultyAnalyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Diagnostics = new RecordingDiagnostics(throwOnReport: true) });

        // Act
        _ = recordingAnalyzer.Analyze(text, TestContext.Current.CancellationToken);
        var faultyResult = faultyAnalyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        var reported = Assert.Single(recording.Events);
        Assert.Equal("TextEn.Analysis", reported.Category);
        Assert.Matches(@"^Analyzed one text: 1 sentences, \d+ tokens, \d+ annotations, \d+ unknown words, \d+ normalizations, \d+ ambiguous readings, \d+ numeric fallbacks\.$", reported.Message);
        Assert.DoesNotContain("Quokka", reported.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, faultyResult.Annotations.Count);
    }

    /// <summary>
    ///     Proves a real synthesis session driven by a model that supplies the analyzer delivers
    ///     part-of-speech annotations to an annotation-aware backend, and that they survive
    ///     chunking of a long, multi-sentence text.
    /// </summary>
    [Fact]
    public async Task SpeechTextEn_SynthesisSession_AnnotatedBackend_ReceivesPartOfSpeechAnnotationsThatSurviveChunking()
    {
        // Arrange: well over the default 200-character chunk length, so the session must chunk it
        var sentence = "The quick brown fox jumps over the lazy dog near the quiet river bank.";
        var text = string.Join(' ', Enumerable.Repeat(sentence, 12));
        Assert.True(text.Length > 4 * SentenceChunker.DefaultMaxChunkLength);
        var analyzer = new EnglishTextAnalyzer();
        var expected = analyzer.AnalyzeDetailed(text, TestContext.Current.CancellationToken);
        var backend = new RecordingAnnotatedBackend();

        // Act
        await SynthesizeAsync(new AnalyzingSynthesisModel(analyzer, backend), text);

        // Assert: several chunks, every one annotated, and no word's annotation lost between analysis and backend
        var calls = backend.Calls;
        Assert.True(calls.Count > 1);
        Assert.All(calls, call => Assert.NotEmpty(call.Annotations));
        foreach (var call in calls)
        {
            Assert.All(
                call.Annotations,
                annotation =>
                {
                    Assert.True(TextEnAnnotations.TryReadPartOfSpeech(annotation, out _));
                    Assert.Contains(call.Text.Substring(annotation.Start, annotation.Length), sentence, StringComparison.Ordinal);
                });
        }

        Assert.Equal(expected.Annotations.Count, calls.Sum(call => call.Annotations.Count));
        Assert.Equal(
            expected.Annotations.Select(annotation => annotation.Value),
            calls.SelectMany(call => call.Annotations.Select(annotation => annotation.Value)));
    }

    /// <summary>Proves the analyzer's sentence breaks, not the punctuation heuristic, decide where the session splits sentences.</summary>
    [Fact]
    public async Task SpeechTextEn_SynthesisSession_AnalyzerSentenceBreaks_DriveChunkBoundaries()
    {
        // Arrange
        const string text = "Dr. Smith arrived. He left.";
        var withAnalyzer = new RecordingAnnotatedBackend();
        var withoutAnalyzer = new RecordingAnnotatedBackend();

        // Act
        await SynthesizeAsync(new AnalyzingSynthesisModel(new EnglishTextAnalyzer(), withAnalyzer), text);
        await SynthesizeAsync(new AnalyzingSynthesisModel(null, withoutAnalyzer), text);

        // Assert: the abbreviation stays attached with the analyzer; the heuristic alone splits after "Dr."
        Assert.Equal(["Dr. Smith arrived.", "He left."], withAnalyzer.Calls.Select(call => call.Text));
        Assert.Equal(["Dr.", "Smith arrived.", "He left."], withoutAnalyzer.Calls.Select(call => call.Text));
    }

    /// <summary>Proves a request containing an audio tag still annotates the words around it, and no annotation ever covers the tag's placeholder.</summary>
    [Fact]
    public async Task SpeechTextEn_SynthesisSession_TextWithAudioTag_AnnotatesWordsAroundTheTag()
    {
        // Arrange
        const string text = "I've [whispers] read it. Then say it again.";
        var backend = new RecordingAnnotatedBackend();

        // Act
        await SynthesizeAsync(new AnalyzingSynthesisModel(new EnglishTextAnalyzer(), backend), text);

        // Assert: words on both sides of the tag are annotated; neither text nor annotation contains a placeholder
        var calls = backend.Calls;
        Assert.NotEmpty(calls);
        Assert.All(calls, call => Assert.DoesNotContain(Tag, call.Text, StringComparison.Ordinal));
        var annotatedWords = calls
            .SelectMany(call => call.Annotations.Select(annotation => call.Text.Substring(annotation.Start, annotation.Length)))
            .ToList();
        Assert.Contains("I", annotatedWords);
        Assert.Contains("read", annotatedWords);
        Assert.Contains("again", annotatedWords);
    }

    /// <summary>Proves the project file names the combined license file, packs both notice files at the package root, and pins its one tagger dependency.</summary>
    [Fact]
    public void SpeechTextEn_PackageLicense_ProjectFile_NamesLicenseFileAtPackageRoot()
    {
        // Arrange
        var projectPath = FindRepositoryFile($"{PackageFolder}/DemaConsulting.Speech.Text.En.csproj");

        // Act
        var project = XDocument.Load(projectPath);
        var licenseFiles = project.Descendants("PackageLicenseFile").Select(element => element.Value.Trim()).ToList();
        var licenseExpressions = project.Descendants("PackageLicenseExpression").ToList();
        var packedItems = project.Descendants("None")
            .Where(element => (string?)element.Attribute("Pack") == "true")
            .ToDictionary(
                element => (string?)element.Attribute("Include") ?? string.Empty,
                element => (string?)element.Attribute("PackagePath"),
                StringComparer.Ordinal);

        // Assert
        Assert.Equal([PackageLicenseFileName], licenseFiles);
        Assert.Empty(licenseExpressions);
        foreach (var fileName in new[] { PackageLicenseFileName, ThirdPartyNoticesFileName })
        {
            Assert.True(packedItems.TryGetValue(fileName, out var packagePath), $"{fileName} is not packed");
            Assert.Equal("\\", packagePath);
            Assert.True(File.Exists(FindRepositoryFile($"{PackageFolder}/{fileName}")), $"{fileName} does not exist");
        }
    }

    /// <summary>Proves the license file declares MIT only, contains the MIT text, and presents the tagger package as a separate dependency rather than as bundled content.</summary>
    [Fact]
    public void SpeechTextEn_PackageLicense_File_DeclaresMitOnlyAndPointsToDependencyNotice()
    {
        // Arrange
        var licensePath = FindRepositoryFile($"{PackageFolder}/{PackageLicenseFileName}");

        // Act
        var firstLine = File.ReadLines(licensePath).First().TrimEnd();
        var text = File.ReadAllText(licensePath);

        // Assert: the declaration is MIT and nothing else
        Assert.Equal(SpdxHeader, firstLine);
        Assert.DoesNotContain("CC-BY", firstLine, StringComparison.Ordinal);
        Assert.Contains("MIT License", text, StringComparison.Ordinal);
        Assert.Contains("Permission is hereby granted, free of charge", text, StringComparison.Ordinal);

        // The dependency is described as separate, with its own license files, and the notices file is named
        Assert.Contains("DemaConsulting.PartOfSpeech", text, StringComparison.Ordinal);
        Assert.Contains("separate NuGet package", text, StringComparison.Ordinal);
        Assert.Contains("is not bundled", text, StringComparison.Ordinal);
        Assert.Contains("carries its own license files", text, StringComparison.Ordinal);
        var flat = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        Assert.Contains("two small lexicon tables of its own", flat, StringComparison.Ordinal);
        Assert.Contains("wrote both from scratch", flat, StringComparison.Ordinal);
        Assert.Contains("embeds no third-party data", flat, StringComparison.Ordinal);
        Assert.Contains(ThirdPartyNoticesFileName, text, StringComparison.Ordinal);
    }

    /// <summary>Proves the third-party notices are scoped as a dependency notice, carry the attribution chain (MASC, Open English WordNet, Princeton WordNet) and record the dependency's text-retention limitation.</summary>
    [Fact]
    public void SpeechTextEn_ThirdPartyNotices_File_AttributesDependencyData()
    {
        // Arrange
        var noticesPath = FindRepositoryFile($"{PackageFolder}/{ThirdPartyNoticesFileName}");

        // Act
        var text = File.ReadAllText(noticesPath);

        // Assert
        Assert.Contains("Dependency Notice", text, StringComparison.Ordinal);
        Assert.Contains("separate NuGet package", text, StringComparison.Ordinal);
        Assert.Contains("carries its own license files", text, StringComparison.Ordinal);
        Assert.Contains("embeds no third-party data in this release", text, StringComparison.Ordinal);
        Assert.Contains("Manually Annotated Sub-Corpus (MASC)", text, StringComparison.Ordinal);
        Assert.Contains("https://creativecommons.org/licenses/by/3.0/us/", text, StringComparison.Ordinal);
        Assert.Contains("Open English WordNet 2025 Edition", text, StringComparison.Ordinal);
        Assert.Contains("https://creativecommons.org/licenses/by/4.0/", text, StringComparison.Ordinal);
        Assert.Contains("WordNet 3.1 Copyright 2011 by Princeton University.  All rights reserved.", text, StringComparison.Ordinal);
        Assert.Contains("Princeton WordNet", text, StringComparison.Ordinal);
        Assert.Contains("do not endorse", text, StringComparison.Ordinal);
        Assert.Contains("https://github.com/demaconsulting/PartOfSpeech/issues/29", text, StringComparison.Ordinal);
    }

    /// <summary>Proves the project depends on exactly one version of the tagger package and on no speech-engine runtime.</summary>
    [Fact]
    public void SpeechTextEn_ProjectFile_Dependencies_PinTaggerExactlyAndReferenceNoInferenceRuntime()
    {
        // Arrange
        var project = XDocument.Load(FindRepositoryFile($"{PackageFolder}/DemaConsulting.Speech.Text.En.csproj"));

        // Act
        var packageReferences = project.Descendants("PackageReference")
            .ToDictionary(
                element => (string?)element.Attribute("Include") ?? string.Empty,
                element => (string?)element.Attribute("Version"),
                StringComparer.Ordinal);
        var projectReferences = project.Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .ToList();

        // Assert: exact-range notation pins one version; the only project reference is core Speech; no ONNX anywhere
        Assert.True(packageReferences.TryGetValue("DemaConsulting.PartOfSpeech", out var version));
        Assert.Matches(@"^\[[0-9A-Za-z.\-+]+\]$", version);
        Assert.Equal(["..\\DemaConsulting.Speech\\DemaConsulting.Speech.csproj"], projectReferences);
        Assert.DoesNotContain(packageReferences.Keys, name => name.Contains("Onnx", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Proves the project is not packable and states why, so the package cannot be built or published while the tagger package retains analyzed text.</summary>
    [Fact]
    public void SpeechTextEn_ProjectFile_Packaging_IsGatedUntilTaggerStopsRetainingText()
    {
        // Arrange
        var projectPath = FindRepositoryFile($"{PackageFolder}/DemaConsulting.Speech.Text.En.csproj");

        // Act
        var project = XDocument.Load(projectPath);
        var isPackable = project.Descendants("IsPackable").Select(element => element.Value.Trim()).ToList();
        var projectText = File.ReadAllText(projectPath);

        // Assert: packing is switched off, and the project file explains the gate and names the upstream issue
        Assert.Equal(["false"], isPackable);
        Assert.Contains("RELEASE GATE", projectText, StringComparison.Ordinal);
        Assert.Contains("https://github.com/demaconsulting/PartOfSpeech/issues/29", projectText, StringComparison.Ordinal);
    }

    /// <summary>Proves an abbreviation is described by an <c>en.norm</c> annotation over its exact source characters, the text itself is never changed, and unchanged words get no such annotation.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_Abbreviation_AnnotatesSpokenFormWithoutChangingText()
    {
        // Arrange
        const string text = "Dr. Smith lives here.";
        ITextAnalyzer analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: exactly one normalization, on "Dr.", whose reader gives the spoken form
        var normalization = Assert.Single(result.Annotations, annotation => annotation.Kind == TextEnAnnotations.Normalized);
        Assert.Equal("Dr.", text.Substring(normalization.Start, normalization.Length));
        Assert.True(TextEnAnnotations.TryReadNormalized(normalization, out var spoken));
        Assert.Equal("Doctor", spoken);
        Assert.Equal("Doctor Smith lives here.", NormalizationHarness.ApplyNormalizations(text, result.Annotations));
        Assert.Equal("Dr. Smith lives here.", text);
    }

    /// <summary>Proves the host can switch normalization off, and that normalization alone needs no tagger and gives no part of speech.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_NormalizationNotSelected_ProducesNoNormAnnotations()
    {
        // Arrange
        const string text = "Dr. Smith & Mr. Jones live on Main St. It is nice.";
        var source = new CountingTaggerSource();
        var tagsOnly = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.PartOfSpeech });
        var normalizationOnly = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization }, source.CreateAdapter());

        // Act
        var tagged = tagsOnly.Analyze(text, TestContext.Current.CancellationToken);
        var normalized = normalizationOnly.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(tagged.Annotations, annotation => annotation.Kind == TextEnAnnotations.Normalized);
        Assert.NotEmpty(tagged.Annotations);
        Assert.All(normalized.Annotations, annotation => Assert.Equal(TextEnAnnotations.Normalized, annotation.Kind));
        Assert.NotEmpty(normalized.Annotations);
        Assert.Equal(0, source.Count);
    }

    /// <summary>Proves St., Dr., Sr. and No. are resolved from their neighbors.</summary>
    /// <param name="text">The text.</param>
    /// <param name="source">The abbreviation as written.</param>
    /// <param name="spoken">The expected spoken form of the first occurrence.</param>
    [Theory]
    [InlineData("Visit St. Louis. Then go.", "St.", "Saint")]
    [InlineData("I live on Main St. It is nice.", "St", "Street")]
    [InlineData("Dr. Smith lives here.", "Dr.", "Doctor")]
    [InlineData("Maple Dr. He likes it.", "Dr", "Drive")]
    [InlineData("Sr. Maria prayed.", "Sr.", "Sister")]
    [InlineData("John Smith Sr. He left.", "Sr", "Senior")]
    [InlineData("Item No. 5 shipped.", "No.", "Number")]
    public void SpeechTextEn_Analyze_ContextDependentAbbreviations_ResolveFromNeighbors(string text, string source, string spoken)
    {
        // Arrange
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: the first normalization is the abbreviation (the number after it is read by the numeric rule)
        var normalization = result.Annotations.First(annotation => annotation.Kind == TextEnAnnotations.Normalized);
        Assert.Equal(source, text.Substring(normalization.Start, normalization.Length));
        Assert.Equal(spoken, normalization.Value);
    }

    /// <summary>Proves a sentence-final abbreviation ends its sentence, keeps its period for the sentence end, and a title never ends one.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_AbbreviationAtSentenceEnd_BreaksAfterItAndKeepsThePeriod()
    {
        // Arrange
        const string text = "I live on Main St. It is nice. Dr. Smith agrees. No. I don't know.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: breaks exactly before It, Dr., No. and I; the period after Main St stays outside its span
        Assert.Equal(
            [text.IndexOf("It is", StringComparison.Ordinal), text.IndexOf("Dr. Smith", StringComparison.Ordinal), text.IndexOf("No. I", StringComparison.Ordinal), text.IndexOf("I don't", StringComparison.Ordinal)],
            result.SentenceBreaks);
        Assert.Equal("I live on Main Street. It is nice. Doctor Smith agrees. No. I don't know.", NormalizationHarness.ApplyNormalizations(text, result.Annotations));
    }

    /// <summary>Proves the abbreviation-aware breaks, not the punctuation heuristic, decide the chunks a real session hands the backend.</summary>
    [Fact]
    public async Task SpeechTextEn_SynthesisSession_AbbreviationBreaks_DriveChunkBoundaries()
    {
        // Arrange
        const string text = "I live on Main St. It is nice. Dr. Smith agrees.";
        var backend = new RecordingAnnotatedBackend();

        // Act
        await SynthesizeAsync(new AnalyzingSynthesisModel(new EnglishTextAnalyzer(), backend), text);

        // Assert: split after "Main St." and not after "Dr."; each normalization is on its own chunk's text
        var calls = backend.Calls;
        Assert.Equal(["I live on Main St.", "It is nice.", "Dr. Smith agrees."], calls.Select(call => call.Text));
        Assert.Equal(
            ["St", "Dr."],
            calls.SelectMany(call => call.Annotations.Where(a => a.Kind == "en.norm").Select(a => call.Text.Substring(a.Start, a.Length))));
    }

    /// <summary>Proves credentials after a name suffix stay in the name's sentence, while a sentence starter after them still ends it.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_CredentialsAfterNameSuffix_StayInOneSentence()
    {
        // Arrange
        const string text = "John Smith Jr. Ph.D. spoke. Ann Lee Esq. Ph.D. M.D. left. I met John Smith Jr. He left.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: breaks only before "Ann", "I" and "He"
        Assert.Equal(
            [text.IndexOf("Ann Lee", StringComparison.Ordinal), text.IndexOf("I met", StringComparison.Ordinal), text.IndexOf("He left", StringComparison.Ordinal)],
            result.SentenceBreaks);
        Assert.Equal(
            "John Smith Junior P H D spoke. Ann Lee Esquire P H D M D left. I met John Smith Junior. He left.",
            NormalizationHarness.ApplyNormalizations(text, result.Annotations));
    }

    /// <summary>Proves adjacent marks are spoken as separate words through the public API.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_AdjacentMarks_AreSpokenAsSeparateWords()
    {
        // Arrange
        const string text = "A\u2122\u00AEB and \u00A9\u2122\u00AE.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: one annotation per mark, each on its own source character
        Assert.Equal(5, result.Annotations.Count);
        Assert.All(result.Annotations, annotation => Assert.Equal(1, annotation.Length));
        Assert.Equal("A trademark registered B and copyright trademark registered.", NormalizationHarness.ApplyNormalizations(text, result.Annotations));
    }

    /// <summary>Proves a name with a suffix and a credential reaches the backend as one chunk with both normalizations.</summary>
    [Fact]
    public async Task SpeechTextEn_SynthesisSession_NameWithCredentials_IsOneChunk()
    {
        // Arrange
        const string text = "John Smith Jr. Ph.D. spoke.";
        var backend = new RecordingAnnotatedBackend();

        // Act
        await SynthesizeAsync(new AnalyzingSynthesisModel(new EnglishTextAnalyzer(), backend), text);

        // Assert
        var call = Assert.Single(backend.Calls);
        Assert.Equal(text, call.Text);
        Assert.Equal(
            [("Jr.", "Junior"), ("Ph.D.", "P H D")],
            call.Annotations
                .Where(annotation => annotation.Kind == TextEnAnnotations.Normalized)
                .Select(annotation => (call.Text.Substring(annotation.Start, annotation.Length), annotation.Value)));
    }

    /// <summary>Proves a symbol-only text keeps its normalization when the Speech library chunks it.</summary>
    [Fact]
    public async Task SpeechTextEn_SynthesisSession_SymbolOnlyText_KeepsItsNormalization()
    {
        // Arrange
        var backend = new RecordingAnnotatedBackend();

        // Act
        await SynthesizeAsync(new AnalyzingSynthesisModel(new EnglishTextAnalyzer(), backend), "&");

        // Assert
        var call = Assert.Single(backend.Calls);
        Assert.Equal("&", call.Text);
        var annotation = Assert.Single(call.Annotations, candidate => candidate.Kind == TextEnAnnotations.Normalized);
        Assert.Equal(("&", "and"), (call.Text.Substring(annotation.Start, annotation.Length), annotation.Value));
    }

    /// <summary>Proves a symbol before clause punctuation keeps its own chunk and normalization instead of being dropped.</summary>
    [Fact]
    public async Task SpeechTextEn_SynthesisSession_SymbolBeforeClausePunctuation_KeepsItsNormalization()
    {
        // Arrange
        const string text = "\u00A9; Brand";
        var backend = new RecordingAnnotatedBackend();

        // Act
        await SynthesizeAsync(new AnalyzingSynthesisModel(new EnglishTextAnalyzer(), backend), text);

        // Assert
        var calls = backend.Calls;
        Assert.Equal(["\u00A9;", "Brand"], calls.Select(call => call.Text));
        var annotation = Assert.Single(calls[0].Annotations, candidate => candidate.Kind == TextEnAnnotations.Normalized);
        Assert.Equal(("\u00A9", "copyright"), (calls[0].Text.Substring(annotation.Start, annotation.Length), annotation.Value));
    }

    /// <summary>Proves, for a normalization-only analysis, an invisible-only clause is still merged into its neighbor, with the dropped-character annotation delivered there.</summary>
    [Fact]
    public async Task SpeechTextEn_SynthesisSession_InvisibleOnlyClause_KeepsDroppedAnnotationWithItsNeighbor()
    {
        // Arrange: normalization only, so the only annotation on the invisible character is its empty-valued drop
        const string text = "Hello, \u200B, there";
        var backend = new RecordingAnnotatedBackend();
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        await SynthesizeAsync(new AnalyzingSynthesisModel(analyzer, backend), text);

        // Assert: the invisible character does not become a chunk of its own
        var calls = backend.Calls;
        Assert.Equal(2, calls.Count);
        Assert.Equal("Hello, \u200B,", calls[0].Text);
        var dropped = Assert.Single(calls[0].Annotations);
        Assert.Equal(("\u200B", string.Empty), (calls[0].Text.Substring(dropped.Start, dropped.Length), dropped.Value));
    }

    /// <summary>Proves that with the default analyzer (part of speech included) an invisible-only clause is also merged into its neighbor, because the tagger's opinion of an invisible character is not annotated.</summary>
    [Fact]
    public async Task SpeechTextEn_SynthesisSession_InvisibleOnlyClauseWithPartOfSpeech_IsMergedIntoItsNeighbor()
    {
        // Arrange: the default analyzer tags the zero-width space of this text as an interjection
        const string text = "Hello, \u200B, there";
        var backend = new RecordingAnnotatedBackend();

        // Act
        await SynthesizeAsync(new AnalyzingSynthesisModel(new EnglishTextAnalyzer(), backend), text);

        // Assert: two chunks, the invisible character carries only its empty-valued drop
        var calls = backend.Calls;
        Assert.Equal(["Hello, \u200B,", "there"], calls.Select(call => call.Text));
        Assert.DoesNotContain(
            calls[0].Annotations,
            annotation => annotation.Kind == TextEnAnnotations.PartOfSpeech && calls[0].Text[annotation.Start] == '\u200B');
        var dropped = Assert.Single(calls[0].Annotations, annotation => annotation.Kind == TextEnAnnotations.Normalized);
        Assert.Equal(("\u200B", string.Empty), (calls[0].Text.Substring(dropped.Start, dropped.Length), dropped.Value));
    }

    /// <summary>Proves that with the default analyzer a bare symbol clause that the tagger labels a word is merged into its neighbor like any punctuation-only clause.</summary>
    [Fact]
    public async Task SpeechTextEn_SynthesisSession_BareSymbolClauseWithPartOfSpeech_IsMergedIntoItsNeighbor()
    {
        // Arrange: the default analyzer tags the asterisk of this text as an interjection
        const string text = "Hello, *, there";
        var backend = new RecordingAnnotatedBackend();

        // Act
        await SynthesizeAsync(new AnalyzingSynthesisModel(new EnglishTextAnalyzer(), backend), text);

        // Assert: two chunks, and the asterisk carries no annotation
        var calls = backend.Calls;
        Assert.Equal(["Hello, *,", "there"], calls.Select(call => call.Text));
        Assert.DoesNotContain(
            calls[0].Annotations,
            annotation => calls[0].Text.Substring(annotation.Start, annotation.Length).Contains('*'));
    }

    /// <summary>Proves non-numeric symbols are spoken as words.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_Symbols_AreSpokenAsWords()
    {
        // Arrange
        const string text = "Tom & Jerry = friends @ home, he/she \u00A9 w/o AT&T.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Tom and Jerry equals friends at home, he slash she copyright without A T and T.", NormalizationHarness.ApplyNormalizations(text, result.Annotations));
    }

    /// <summary>Proves typographic apostrophes in contractions and elisions are canonicalized, and nothing else about the words changes.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_TypographicApostrophes_AreCanonicalized()
    {
        // Arrange
        const string text = "It\u2019s true, I don\u2019t know y\u2019all, but \u2018quotes\u2019 stay.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.All(result.Annotations, annotation => Assert.Equal("'", annotation.Value));
        Assert.Equal("It's true, I don't know y'all, but \u2018quotes\u2019 stay.", NormalizationHarness.ApplyNormalizations(text, result.Annotations));
    }

    /// <summary>Proves invisible characters are dropped explicitly: an annotation with an empty value, never a silent loss.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_InvisibleCharacters_AreDroppedExplicitly()
    {
        // Arrange
        const string text = "co\u00AD" + "operate a\u200B" + "b \uFEFF" + "done";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: three annotations, each readable as "speak nothing"
        Assert.Equal(3, result.Annotations.Count);
        Assert.All(result.Annotations, annotation =>
        {
            Assert.True(TextEnAnnotations.TryReadNormalized(annotation, out var spoken));
            Assert.Equal(string.Empty, spoken);
        });
        Assert.Equal("cooperate ab done", NormalizationHarness.ApplyNormalizations(text, result.Annotations));
    }

    /// <summary>Proves the detailed analysis records the grammatical role of each clitic, and the language-neutral path does not need it.</summary>
    [Fact]
    public void SpeechTextEn_AnalyzeDetailed_Clitics_RecordTheirRole()
    {
        // Arrange
        const string text = "It's been cold, John's book is here, and I don't know.";
        var analyzer = new EnglishTextAnalyzer();

        // Act
        var analysis = analyzer.AnalyzeDetailed(text, TestContext.Current.CancellationToken);

        // Assert
        var roles = analysis.Tokens.Where(token => token.Clitic != CliticKind.None).Select(token => (token.Text, token.Clitic)).ToList();
        Assert.Equal([("'s", CliticKind.Has), ("'s", CliticKind.Possessive), ("n't", CliticKind.Not)], roles);
    }

    /// <summary>Proves acronyms and initialisms are recognized case-sensitively and spelled letter by letter.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_AcronymsAndInitialisms_AreCaseSensitive()
    {
        // Arrange
        const string text = "US troops asked us about IT. The FBI and the U.S. Army know NASA.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: the pronoun "us" and the word NASA are untouched
        Assert.Equal("U S troops asked us about I T. The F B I and the U S Army know NASA.", NormalizationHarness.ApplyNormalizations(text, result.Annotations));
    }

    /// <summary>Proves hostile random text always yields normalization annotations that satisfy the alignment contract, checked by an applier independent of the production code.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_NormalizedText_AnnotationsSatisfyAlignmentContract()
    {
        // Arrange
        string[] pieces =
        [
            "the", "Smith", "Main", "St.", "Dr.", "Mr.", "No.", "5", ".", ",", ";", ":", "!", "&", "/", "+", "=", "\u00A9", "\u2019", "'s", "n't",
            "y\u2019all", "he/she", "w/o", "AT&T", "FBI", "US", "us", "U.S.", "p.m.", "etc.", "\u200B", "\u00AD", "\uFEFF", "\u2022",
            " ", "  ", "\t", "\n", Tag, "\U0001F600", "e\u0301", "\uD83D", "\uDE00", "\0", "(", ")",
        ];
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });
        var random = new Random(20260611);

        for (var iteration = 0; iteration < 200; iteration++)
        {
            var text = string.Concat(Enumerable.Range(0, random.Next(0, 40)).Select(_ => pieces[random.Next(pieces.Length)] + (random.Next(3) == 0 ? string.Empty : " ")));

            // Act
            var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

            // Assert: ordered, disjoint, in bounds, whitespace-free edges, no placeholder, no cut pair, chunk-safe content
            var previousEnd = 0;
            foreach (var annotation in result.Annotations)
            {
                Assert.Equal(TextEnAnnotations.Normalized, annotation.Kind);
                Assert.True(annotation.Start >= previousEnd && annotation.End <= text.Length, $"order or bounds in iteration {iteration}");
                var covered = text.Substring(annotation.Start, annotation.Length);
                Assert.False(char.IsWhiteSpace(covered[0]) || char.IsWhiteSpace(covered[^1]), $"whitespace edge in iteration {iteration}");
                Assert.DoesNotContain(Tag, covered, StringComparison.Ordinal);
                Assert.DoesNotContain(covered, character => char.IsWhiteSpace(character) || character is ',' or ';' or ':');
                Assert.False(annotation.Start > 0 && char.IsLowSurrogate(text[annotation.Start]) && char.IsHighSurrogate(text[annotation.Start - 1]), $"cut pair in iteration {iteration}");
                Assert.False(annotation.End < text.Length && char.IsHighSurrogate(text[annotation.End - 1]) && char.IsLowSurrogate(text[annotation.End]), $"cut pair in iteration {iteration}");
                Assert.DoesNotContain(Tag, annotation.Value, StringComparison.Ordinal);
                previousEnd = annotation.End;
            }

            // Assert: everything outside the annotations is spoken exactly as written
            var outside = new System.Text.StringBuilder();
            var position = 0;
            foreach (var annotation in result.Annotations)
            {
                outside.Append(text, position, annotation.Start - position);
                position = annotation.End;
            }

            outside.Append(text, position, text.Length - position);
            Assert.Equal(text.Length - result.Annotations.Sum(annotation => annotation.Length), outside.Length);
        }
    }

    /// <summary>
    ///     Proves every normalization annotation of a long text that mixes every rule (so both the clause pass and the
    ///     whitespace-budget path of the core chunker run) reaches exactly one backend call, on the same source characters.
    /// </summary>
    [Fact]
    public async Task SpeechTextEn_SynthesisSession_NormalizedText_AnnotationsSurviveChunking()
    {
        // Arrange: well over 800 characters, mixing every rule
        const string paragraph = "Dr. Smith & Mr. Jones live on Main St. The FBI said it\u2019s w/o proof, etc. He left at 5 p.m. Then he met the U.S. Army at AT&T, R&D and Q&A. No. I don\u2019t know; see Fig. 3, he/she said: Y\u2019all go to Elm Dr. Smith agrees. ";
        var text = string.Concat(Enumerable.Repeat(paragraph, 5)).TrimEnd();
        Assert.True(text.Length > 800);
        var analyzer = new EnglishTextAnalyzer();
        var expected = analyzer.Analyze(text, TestContext.Current.CancellationToken).Annotations
            .Where(annotation => annotation.Kind == TextEnAnnotations.Normalized)
            .Select(annotation => (Source: text.Substring(annotation.Start, annotation.Length), annotation.Value))
            .ToList();
        var backend = new RecordingAnnotatedBackend();

        // Act
        await SynthesizeAsync(new AnalyzingSynthesisModel(analyzer, backend), text);

        // Assert: the sequence of (source characters, value) the backend sees equals the analyzer's, so nothing was lost or moved
        var delivered = backend.Calls
            .SelectMany(call => call.Annotations
                .Where(annotation => annotation.Kind == TextEnAnnotations.Normalized)
                .Select(annotation => (Source: call.Text.Substring(annotation.Start, annotation.Length), annotation.Value)))
            .ToList();
        Assert.True(backend.Calls.Count > 3);
        Assert.Equal(expected, delivered);
    }

    /// <summary>Proves an audio tag between an abbreviation and the name it belongs to does not cost either annotation.</summary>
    [Fact]
    public async Task SpeechTextEn_SynthesisSession_NormalizedTextWithAudioTag_KeepsBothAnnotations()
    {
        // Arrange
        const string text = "Dr. [whispers] Smith & Jones agree.";
        var backend = new RecordingAnnotatedBackend();

        // Act
        await SynthesizeAsync(new AnalyzingSynthesisModel(new EnglishTextAnalyzer(), backend), text);

        // Assert
        var delivered = backend.Calls
            .SelectMany(call => call.Annotations
                .Where(annotation => annotation.Kind == TextEnAnnotations.Normalized)
                .Select(annotation => (Source: call.Text.Substring(annotation.Start, annotation.Length), annotation.Value)))
            .ToList();
        Assert.Equal([("Dr.", "Doctor"), ("&", "and")], delivered);
    }

    /// <summary>Proves numbers (cardinals, decimals, ordinals, hash numbers, decades) are annotated with their spoken words, the text itself is never rewritten, and every value is plain words.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_Numbers_AreSpokenAsWordsWithoutChangingText()
    {
        // Arrange
        const string text = "I have 5 apples, 1,234 pears, 3.5 plums, the 21st, #5 and the 1990s.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: the annotations name the source characters and their words; the source is untouched
        var normalizations = result.Annotations.Where(annotation => annotation.Kind == TextEnAnnotations.Normalized).ToList();
        Assert.Equal(
            [("5", "five"), ("1,234", "one thousand two hundred thirty four"), ("3.5", "three point five"), ("21st", "twenty first"), ("#5", "number five"), ("1990s", "nineteen nineties")],
            normalizations.Select(annotation => (text.Substring(annotation.Start, annotation.Length), annotation.Value)));
        Assert.Equal(
            "I have five apples, one thousand two hundred thirty four pears, three point five plums, the twenty first, number five and the nineteen nineties.",
            NormalizationHarness.ApplyNormalizations(text, result.Annotations));
        Assert.All(normalizations, annotation => Assert.Matches("^[A-Za-z' ]+$", annotation.Value));
        Assert.Equal("I have 5 apples, 1,234 pears, 3.5 plums, the 21st, #5 and the 1990s.", text);
    }

    /// <summary>Proves the sentence breaks around numbers, decimals, amounts, abbreviated dates and units fall exactly where the sentences end.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_NumericSentenceBreaks_FollowTheSentenceEnd()
    {
        // Arrange: a decimal, an amount, a number and an abbreviated month before a period are not sentence ends by themselves
        const string text = "It costs $1.25. Then $2. Pi is 3.14. Item 5. Jan. 5, 2024 was cold. He left at 5 p.m. Then slept. He is 5 ft. tall. She is 5 ft. Then left.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            "It costs $1.25.¦Then $2.¦Pi is 3.14.¦Item 5.¦Jan. 5, 2024 was cold.¦He left at 5 p.m.¦Then slept.¦He is 5 ft. tall.¦She is 5 ft.¦Then left.",
            NormalizationHarness.JoinSentences(text, result.SentenceBreaks!));
    }

    /// <summary>Proves currency amounts, percent and units are spoken as words, per token, with singular and plural.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_CurrencyAndUnits_AreSpokenAsWords()
    {
        // Arrange
        const string text = "Pay $1,234.50, \u20AC5 and 50 % of 5 million USD for 3 kg, 1 ft. and 72\u00B0F.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            "Pay one thousand two hundred thirty four dollars and fifty cents, five euros and fifty percent of five million dollars for three kilograms, one foot and seventy two degrees Fahrenheit.",
            NormalizationHarness.ApplyNormalizations(text, result.Annotations));
        var covered = result.Annotations.Select(annotation => text.Substring(annotation.Start, annotation.Length)).ToList();
        Assert.Contains("$1,234.50", covered);
        Assert.Contains("\u20AC5", covered);
        Assert.Contains("%", covered);
        Assert.Contains("USD", covered);
        Assert.Contains("72\u00B0F", covered);
    }

    /// <summary>Proves times, numeric dates and month-name dates are spoken as words.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_DatesAndTimes_AreSpokenAsWords()
    {
        // Arrange
        const string text = "On January 5th, 2024 at 5:30pm, 12/25/2024 at 17:45 and 7 am.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            "On January fifth, twenty twenty four at five thirty P M, December twenty fifth twenty twenty four at seventeen forty five and seven A M.",
            NormalizationHarness.ApplyNormalizations(text, result.Annotations));
    }

    /// <summary>Proves month and weekday abbreviations resolve from the date around them, keep the sentence breaks that are real and drop the ones the tokenizer invented.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_MonthAndWeekdayAbbreviations_ResolveFromDateContext()
    {
        // Arrange
        const string text = "Sat. 5 Jan. 2024 and Sun. Mar. 5 and Wed. 7:30. He left Mon. He came Tue.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: one sentence for the dates (no false break after any abbreviation), then the two real ones
        Assert.Equal(
            "Saturday fifth January twenty twenty four and Sunday March fifth and Wednesday seven thirty. He left Monday. He came Tuesday.",
            NormalizationHarness.ApplyNormalizations(text, result.Annotations));
        Assert.Equal(
            [text.IndexOf("He left", StringComparison.Ordinal), text.IndexOf("He came", StringComparison.Ordinal)],
            result.SentenceBreaks);
    }

    /// <summary>Proves a period after a full month name or May ends the sentence and gives the next number no date reading, while a month abbreviation's period keeps the date together.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_FullMonthPeriod_EndsTheSentenceAndGivesNoDateContext()
    {
        // Arrange
        const string text = "We finished in May. 5 people stayed. Born Jan. 5, 2024.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: "5 people" is a cardinal after the sentence end; "Jan. 5, 2024" is one date
        Assert.Equal(
            "We finished in May. five people stayed. Born January fifth, twenty twenty four.",
            NormalizationHarness.ApplyNormalizations(text, result.Annotations));
        Assert.Equal(
            [text.IndexOf("5 people", StringComparison.Ordinal), text.IndexOf("Born", StringComparison.Ordinal)],
            result.SentenceBreaks);
    }

    /// <summary>Proves the documented defaults for ambiguous numbers (a bare year, a month-first date, a fraction) are applied and counted, and cued or equal readings are not.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_AmbiguousNumbers_UseDocumentedDefaultsAndAreCounted()
    {
        // Arrange: four ambiguous readings (1999, 3/4, 3/4/2024, 1500) among cued and unambiguous ones
        const string text = "And 1999, 3/4, 3/4/2024, 1500, in 1066, about 1500 people, 12/25/2024, 12/12/2024, 2000.";
        var sink = new RecordingDiagnostics();
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization, Diagnostics = sink });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: the defaults
        Assert.Equal(
            "And nineteen ninety nine, three quarters, March fourth twenty twenty four, fifteen hundred, in ten sixty six, about one thousand five hundred people, December twenty fifth twenty twenty four, December twelfth twenty twenty four, two thousand.",
            NormalizationHarness.ApplyNormalizations(text, result.Annotations));

        // Assert: counted once each, and only the ambiguous ones
        var reported = Assert.Single(sink.Events);
        Assert.Matches(@"\d+ normalizations, 4 ambiguous readings, 0 numeric fallbacks\.$", reported.Message);
    }

    /// <summary>Proves the diagnostics event carries the numeric counts and counts only: no digit of the analyzed text, no word, no spoken value.</summary>
    [Fact]
    public void SpeechTextEn_Diagnostics_NumericCounts_AreReportedAsCountsOnly()
    {
        // Arrange: one ambiguous year, one number in an unsupported shape (left as written)
        const string text = "He read 1999 and 1,23 pages.";
        var sink = new RecordingDiagnostics();
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization, Diagnostics = sink });

        // Act
        _ = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert
        var reported = Assert.Single(sink.Events);
        Assert.Matches(@"^Analyzed one text: 1 sentences, \d+ tokens, \d+ annotations, \d+ unknown words, 1 normalizations, 1 ambiguous readings, 1 numeric fallbacks\.$", reported.Message);
        Assert.DoesNotContain("1999", reported.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("1,23", reported.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("nineteen", reported.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("pages", reported.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves oversized numbers are bounded and counted: 16 to 64 digits are read digit by digit, longer ones and long fractions are left as written, and no value exceeds the documented size.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_OversizedNumbers_AreBoundedAndCounted()
    {
        // Arrange: a 16-digit number, a 65-digit number and a fraction of 33 digits
        var sixteen = "1234567890123456";
        var sixtyFive = new string('7', 65);
        var fraction = "0." + new string('1', 33);
        var text = $"A {sixteen} and {sixtyFive} and {fraction} end.";
        var sink = new RecordingDiagnostics();
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization, Diagnostics = sink });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: only the 16-digit number is annotated (digit by digit); the other two stay as written
        var normalization = Assert.Single(result.Annotations, annotation => annotation.Kind == TextEnAnnotations.Normalized);
        Assert.Equal(sixteen, text.Substring(normalization.Start, normalization.Length));
        Assert.Equal("one two three four five six seven eight nine zero one two three four five six", normalization.Value);
        Assert.All(result.Annotations, annotation => Assert.True(annotation.Value.Length <= NumberVerbalizer.MaxSpokenLength));
        Assert.Matches(@"3 numeric fallbacks\.$", Assert.Single(sink.Events).Message);
    }

    /// <summary>Proves hostile random numeric text always yields normalization annotations that satisfy the alignment contract, checked by an applier independent of the production code.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_NumericText_AnnotationsSatisfyAlignmentContract()
    {
        // Arrange
        string[] pieces =
        [
            "5", "50", "007", "1,234", "1,234.50", "3.5", ".5", "-5", "1,23", "$", "\u20AC", "$5", "$5.50", "5$", "5USD", "USD", "5:30", "5:30pm", "5pm", "pm", "p.m.", "12/25/2024", "2024-12-25",
            "5-10", "555-1234", "3.2.1", "1/2", "24/7", "\u00BD", "1st", "22nd", "1990s", "'90s", "\u2013", "%", "50%", "kg", "5kg", "ft.", "lbs.", "mph", "72\u00B0F", "million", "$5M", "January", "Jan.", "Mar.",
            "Sun.", "2024", "1999", "#5", ",", ".", ";", ":", "-", "/", "the", "No.", "Dr.", "&", "\u200B", "\u2022", Tag, "\U0001F600", "\uD835\uDFD9", "e\u0301", "\uD83D", "\uDE00", "\0", "(", ")",
            new string('9', 20), " ", "  ", "\t", "\n", "\u00A0",
        ];
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });
        var random = new Random(20260806);

        for (var iteration = 0; iteration < 300; iteration++)
        {
            var text = string.Concat(Enumerable.Range(0, random.Next(0, 40)).Select(_ => pieces[random.Next(pieces.Length)] + (random.Next(3) == 0 ? string.Empty : " ")));

            // Act
            var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

            // Assert: ordered, disjoint, in bounds, whitespace-free edges, no placeholder, no cut pair, chunk-safe content (A5')
            var previousEnd = 0;
            foreach (var annotation in result.Annotations)
            {
                Assert.Equal(TextEnAnnotations.Normalized, annotation.Kind);
                Assert.True(annotation.Start >= previousEnd && annotation.End <= text.Length, $"order or bounds in iteration {iteration}");
                var covered = text.Substring(annotation.Start, annotation.Length);
                Assert.False(char.IsWhiteSpace(covered[0]) || char.IsWhiteSpace(covered[^1]), $"whitespace edge in iteration {iteration}");
                Assert.DoesNotContain(Tag, covered, StringComparison.Ordinal);
                Assert.DoesNotContain(covered, character => char.IsWhiteSpace(character) || character == ';');
                for (var i = 0; i < covered.Length; i++)
                {
                    if (covered[i] is ',' or ':')
                    {
                        Assert.True(i > 0 && i < covered.Length - 1 && covered[i - 1] is >= '0' and <= '9' && covered[i + 1] is >= '0' and <= '9', $"separator not between digits in iteration {iteration}: '{covered}'");
                    }
                }

                Assert.False(annotation.Start > 0 && char.IsLowSurrogate(text[annotation.Start]) && char.IsHighSurrogate(text[annotation.Start - 1]), $"cut pair in iteration {iteration}");
                Assert.False(annotation.End < text.Length && char.IsHighSurrogate(text[annotation.End - 1]) && char.IsLowSurrogate(text[annotation.End]), $"cut pair in iteration {iteration}");
                Assert.DoesNotContain(Tag, annotation.Value, StringComparison.Ordinal);
                previousEnd = annotation.End;
            }

            // Assert: no reconciled sentence break lies strictly inside an annotation
            Assert.All(result.SentenceBreaks!, offset => Assert.DoesNotContain(result.Annotations, annotation => annotation.Start < offset && offset < annotation.End));

            // Assert: everything outside the annotations is spoken exactly as written
            var outside = new System.Text.StringBuilder();
            var position = 0;
            foreach (var annotation in result.Annotations)
            {
                outside.Append(text, position, annotation.Start - position);
                position = annotation.End;
            }

            outside.Append(text, position, text.Length - position);
            Assert.Equal(text.Length - result.Annotations.Sum(annotation => annotation.Length), outside.Length);
        }
    }

    /// <summary>
    ///     Proves every normalization annotation of a long text that mixes every numeric construct (so both the clause pass and the
    ///     whitespace-budget path of the core chunker run) reaches exactly one backend call, on the same source characters.
    /// </summary>
    [Fact]
    public async Task SpeechTextEn_SynthesisSession_NumericText_AnnotationsSurviveChunking()
    {
        // Arrange: well over 800 characters
        const string paragraph = "Dr. Smith paid $1,234.50 at 5:30pm on January 5, 2024, then 50 % of 3 1/2 kg; he ran 5 ft. in the 1990s: #5, 1st, 22nd, 3.5, 12/25/2024. In 1999 about 1500 people saw 5 million USD, 72\u00B0F, 555-1234 and Jan. 7. ";
        var text = string.Concat(Enumerable.Repeat(paragraph, 5)).TrimEnd();
        Assert.True(text.Length > 800);
        var analyzer = new EnglishTextAnalyzer();
        var expected = analyzer.Analyze(text, TestContext.Current.CancellationToken).Annotations
            .Where(annotation => annotation.Kind == TextEnAnnotations.Normalized)
            .Select(annotation => (Source: text.Substring(annotation.Start, annotation.Length), annotation.Value))
            .ToList();
        var backend = new RecordingAnnotatedBackend();

        // Act
        await SynthesizeAsync(new AnalyzingSynthesisModel(analyzer, backend), text);

        // Assert: the sequence of (source characters, value) the backend sees equals the analyzer's, so nothing was lost or moved
        var delivered = DeliveredNormalizations(backend.Calls);
        Assert.True(backend.Calls.Count > 3);
        Assert.True(expected.Count > 60, "the text must carry many numeric annotations");
        Assert.Equal(expected, delivered);
    }

    /// <summary>Proves a date whose day and year are on opposite sides of a clause cut keeps the annotation of every token.</summary>
    [Fact]
    public async Task SpeechTextEn_SynthesisSession_DateAcrossClauseCut_KeepsEveryTokenAnnotation()
    {
        // Arrange: the comma between the day and the year is a clause cut, so the two numbers land in different chunks
        const string text = "He was born on January 5, 2024, then paid $1,234.50 at 5:30pm: 50 % done.";
        var backend = new RecordingAnnotatedBackend();

        // Act
        await SynthesizeAsync(new AnalyzingSynthesisModel(new EnglishTextAnalyzer(), backend), text);

        // Assert: every token of the date, the amount, the time and the percent is delivered once
        Assert.True(backend.Calls.Count >= 3);
        Assert.Equal(
            [("5", "fifth"), ("2024", "twenty twenty four"), ("$1,234.50", "one thousand two hundred thirty four dollars and fifty cents"), ("5:30pm", "five thirty P M"), ("50", "fifty"), ("%", "percent")],
            DeliveredNormalizations(backend.Calls));
    }

    /// <summary>Proves a percent sign that ends up alone in a chunk (the budget cut falls between the number and the sign) keeps its normalization.</summary>
    [Fact]
    public async Task SpeechTextEn_SynthesisSession_PercentSymbolChunk_KeepsItsNormalization()
    {
        // Arrange: slide the budget cut across the gap between "50" and "%"
        var backend = new RecordingAnnotatedBackend();
        var sawLoneSign = false;

        // Act / Assert
        await WithEngineAsync(new AnalyzingSynthesisModel(new EnglishTextAnalyzer(), backend), async engine =>
        {
            for (var padding = 185; padding <= 199; padding++)
            {
                var text = string.Concat(Enumerable.Repeat("w", padding - 1)) + " 50 %";
                var before = backend.Calls.Count;
                _ = await engine.SynthesizeAsync(text, TestContext.Current.CancellationToken);
                var calls = backend.Calls.Skip(before).ToList();
                Assert.Equal(
                    [("50", "fifty"), ("%", "percent")],
                    DeliveredNormalizations(calls));
                sawLoneSign |= calls.Any(call => call.Text == "%");
            }
        });
        Assert.True(sawLoneSign, "no padding put the percent sign alone in a chunk");
    }

    /// <summary>Proves that wherever the 200-character budget cut falls (every padding is tried), no numeric annotation is split or lost, and two-token amounts stay whole.</summary>
    [Fact]
    public async Task SpeechTextEn_SynthesisSession_NumbersAtBudgetCut_AreNeverSplit()
    {
        // Arrange
        var backend = new RecordingAnnotatedBackend();

        // Act / Assert
        await WithEngineAsync(new AnalyzingSynthesisModel(new EnglishTextAnalyzer(), backend), async engine =>
        {
            for (var padding = 150; padding <= 199; padding++)
            {
                var text = string.Concat(Enumerable.Repeat("w", padding - 1)) + " $5 million at 5:30pm on January 5 2024 for 5 kg and 1,234.50 or 12/25/2024 end";
                var before = backend.Calls.Count;
                _ = await engine.SynthesizeAsync(text, TestContext.Current.CancellationToken);

                // Assert: the sequence the analyzer produced is the sequence delivered
                var expected = new EnglishTextAnalyzer().Analyze(text, TestContext.Current.CancellationToken).Annotations
                    .Where(annotation => annotation.Kind == TextEnAnnotations.Normalized)
                    .Select(annotation => (text.Substring(annotation.Start, annotation.Length), annotation.Value))
                    .ToList();
                Assert.True(expected.Count >= 9);
                Assert.Equal(expected, DeliveredNormalizations(backend.Calls.Skip(before).ToList()));
            }
        });
    }

    /// <summary>Proves a number, an audio tag and a unit with no spaces between them keep both annotations and the tag is never covered.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_NumberNextToAudioTag_KeepsBothAnnotations()
    {
        // Arrange: "5<tag>kg" is a number and a unit around a placeholder
        var text = "It weighs 5" + Tag + "kg now, and $" + Tag + "5 or 50" + Tag + "% too.";
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });

        // Act
        var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);

        // Assert: "5" and "kg" are each annotated, "50" and "%" too, the symbol before a tag is left alone, and no annotation covers a placeholder
        var normalizations = result.Annotations.Where(annotation => annotation.Kind == TextEnAnnotations.Normalized).ToList();
        Assert.Equal(
            [("5", "five"), ("kg", "kilograms"), ("5", "five"), ("50", "fifty"), ("%", "percent")],
            normalizations.Select(annotation => (text.Substring(annotation.Start, annotation.Length), annotation.Value)));
        Assert.All(normalizations, annotation => Assert.DoesNotContain(Tag, text.Substring(annotation.Start, annotation.Length), StringComparison.Ordinal));
    }

    /// <summary>Proves one megabyte of every numeric construct, repeated, analyzes within the time budget.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_LargeRepetitiveNumericTexts_CompleteWithinBudget()
    {
        // Arrange: normalization only, so no tagger is involved
        string[] units = ["1,234.50 ", "$5.50 ", "5:30pm ", "January 5, 2024 ", "1/2 ", "12/25/2024 ", "5 kg ", "50 % ", "Jan. 5 ", "5 ft. ", "5 million USD ", "555-123-4567 ", "1990s ", "3 1/2 ", "1999 "];
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(300));

        foreach (var unit in units)
        {
            var text = string.Concat(Enumerable.Repeat(unit, (1_000_000 / unit.Length) + 1));

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = analyzer.Analyze(text, cancellation.Token);
            stopwatch.Stop();

            // Assert
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(45), $"'{unit}' took {stopwatch.Elapsed}");
            Assert.NotEmpty(result.Annotations);
        }
    }

    /// <summary>
    ///     Reads the normalization annotations every backend call received, as pairs of the source characters and the value.
    /// </summary>
    /// <param name="calls">The backend calls, in arrival order.</param>
    /// <returns>The pairs, in the order delivered.</returns>
    private static List<(string Source, string Value)> DeliveredNormalizations(IReadOnlyList<(string Text, IReadOnlyList<SegmentAnnotation> Annotations)> calls) =>
        calls
            .SelectMany(call => call.Annotations
                .Where(annotation => annotation.Kind == TextEnAnnotations.Normalized)
                .Select(annotation => (Source: call.Text.Substring(annotation.Start, annotation.Length), annotation.Value)))
            .ToList();

    /// <summary>Loads a real engine for a model once, runs an action against it, then disposes the engine and the scratch model directory.</summary>
    /// <param name="model">The model whose analyzer and backend are under test.</param>
    /// <param name="action">The action to run against the engine.</param>
    /// <returns>A task that completes when the action has finished.</returns>
    private static async Task WithEngineAsync(AnalyzingSynthesisModel model, Func<ISpeechSynthesizerEngine, Task> action)
    {
        var directory = Directory.CreateTempSubdirectory("text-en-model-");
        try
        {
            await using var engine = await SpeechSynthesizerFactory.LoadAsync(
                model,
                directory.FullName,
                cancellationToken: TestContext.Current.CancellationToken);
            await action(engine);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Proves one megabyte of realistic and of adversarially repetitive text analyzes within the time budget, and a quadratic regression fails instead of hanging.</summary>
    [Fact]
    public void SpeechTextEn_Analyze_LargeRepetitiveTexts_CompleteWithinBudget()
    {
        // Arrange: normalization only, so no tagger is involved
        string[] units = ["Dr. Smith lives on Maple Dr. He likes it, etc. The FBI said it\u2019s w/o proof & more. ", "St. ", "A. ", "& ", "Dr. He "];
        var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Annotations = TextEnAnnotationSelection.Normalization });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        foreach (var unit in units)
        {
            var text = string.Concat(Enumerable.Repeat(unit, (1_000_000 / unit.Length) + 1));

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = analyzer.Analyze(text, cancellation.Token);
            stopwatch.Stop();

            // Assert
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(45), $"'{unit}' took {stopwatch.Elapsed}");
            Assert.NotEmpty(result.Annotations);
        }
    }

    /// <summary>Synthesizes a text with a real engine for a model, then disposes the engine and the scratch model directory.</summary>
    /// <param name="model">The model whose analyzer and backend are under test.</param>
    /// <param name="text">The text to synthesize.</param>
    /// <returns>A task that completes when synthesis has finished.</returns>
    private static async Task SynthesizeAsync(AnalyzingSynthesisModel model, string text)
    {
        var directory = Directory.CreateTempSubdirectory("text-en-model-");
        try
        {
            await using var engine = await SpeechSynthesizerFactory.LoadAsync(
                model,
                directory.FullName,
                cancellationToken: TestContext.Current.CancellationToken);
            _ = await engine.SynthesizeAsync(text, TestContext.Current.CancellationToken);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Reads the part of speech of the first annotation covering exactly the given word.</summary>
    /// <param name="result">The analysis result.</param>
    /// <param name="text">The analyzed text.</param>
    /// <param name="word">The word to look for.</param>
    /// <returns>The annotation's part-of-speech value.</returns>
    private static string ReadTag(TextAnalysisResult result, string text, string word)
    {
        var annotation = result.Annotations.First(candidate => text.Substring(candidate.Start, candidate.Length) == word);
        Assert.True(TextEnAnnotations.TryReadPartOfSpeech(annotation, out var tag));
        return tag;
    }

    /// <summary>Builds a comparable description of a result's annotations and breaks.</summary>
    /// <param name="result">The result.</param>
    /// <returns>A string equal for equal results.</returns>
    private static string Describe(TextAnalysisResult result) =>
        string.Join(',', result.Annotations.Select(a => $"{a.Start}:{a.Length}:{a.Kind}:{a.Value}"))
        + "|" + string.Join(',', result.SentenceBreaks!);

    /// <summary>Finds a file in the repository by walking up from the test output folder to the repository root.</summary>
    /// <param name="relativePath">The path of the file relative to the repository root, using forward slashes.</param>
    /// <returns>The full path of the file.</returns>
    /// <exception cref="InvalidOperationException">The repository root cannot be found above the test output folder.</exception>
    private static string FindRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Speech.slnx")))
        {
            directory = directory.Parent;
        }

        return directory == null
            ? throw new InvalidOperationException("The repository root (the folder containing Speech.slnx) was not found.")
            : Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}
