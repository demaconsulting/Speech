using System.Xml.Linq;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
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
        Assert.Matches(@"^Analyzed one text: 1 sentences, \d+ tokens, \d+ annotations, \d+ unknown words\.$", reported.Message);
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
        Assert.Contains("embeds no data of its own", text, StringComparison.Ordinal);
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
