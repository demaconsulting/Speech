using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;

/// <summary>
///     Real, production <see cref="ISynthesisModel"/> backing Chatterbox Turbo (English) with reference-clip
///     voice cloning, run directly through ONNX Runtime on the CPU.
/// </summary>
/// <remarks>
///     <b>Download provenance</b>: the graphs are originally published at
///     <c>https://huggingface.co/ResembleAI/chatterbox-turbo-ONNX</c> (MIT license, Copyright (c) 2025 Resemble AI).
///     The SHA-256 checksums in <see cref="BundleContents"/> were computed directly from a local copy of those
///     files; a release checklist item is to cross-check them against the repository's published LFS checksums. Each
///     <c>.onnx</c> graph keeps its <c>.onnx_data</c> weights file beside it. The tokenizer is embedded in
///     this package, not downloaded.
///     <para>
///     <b>Graphs and download</b>: the model runs one fixed set of graphs, the half-precision language model with the
///     4-bit auxiliary graphs (embedding, speech encoder and conditional decoder; about 1.1 GB, 8 files). The files
///     come from two component bundles on this project's <c>models</c> GitHub release (<see cref="Bundles"/>: one for
///     the language model and one for the auxiliary graphs), each holding the unmodified files plus the MIT
///     <c>LICENSE</c>, a <c>NOTICE</c>, a <c>README.txt</c> and a <c>model.json</c>, so a user needs no Hugging Face
///     access. A configured download mirror serves the zips at <c>{mirror}/chatterbox-turbo-onnx-en/{bundle}.zip</c>.
///     Other precisions of the upstream graphs are deliberately not offered: they are larger or slower on the CPU
///     without a quality benefit, and the 4-bit language model destroys the inline tags.
///     </para>
///     <para>
///     <b>Voices</b>: nothing is bundled in this package. Voices come from <see cref="ChatterboxTurboModelOptions"/>
///     (explicit voices, then a voice directory) followed by the model-agnostic <see cref="ReferenceVoiceLibrary"/>
///     (the user folder, then installed packs such as the CC0 voice-zero pack), which are read lazily - a clip is
///     decoded when its voice is first used. The first voice is the default. With no voices the model still
///     enumerates and downloads, but
///     <see cref="CreateBackend"/> throws <see cref="InvalidOperationException"/>, which the core library turns
///     into an unavailable synthesizer.
///     </para>
///     <para>
///     <b>Honest expectations</b>: CPU only; about 0.8x real time; loading the four graphs takes
///     roughly 7-20 seconds; synthesis is per sentence rather than live streaming; the output is not
///     watermarked.
///     </para>
/// </remarks>
public sealed class OnnxChatterboxTurboEnglishSynthesisModel : ISynthesisModel
{
    /// <summary>The stable catalog identifier for this model.</summary>
    public const string ModelId = "chatterbox-turbo-onnx-en";

    /// <summary>The parameter id used for the voice-selection <see cref="ChoiceParameter"/>.</summary>
    public const string VoiceParameterId = "voice";

    /// <summary>The name of the <c>models</c>-release bundle that carries the half-precision language model.</summary>
    internal const string LanguageModelBundleName = "chatterbox-turbo-lm-20261014-fp16-b1";

    /// <summary>
    ///     The name of the <c>models</c>-release bundle that carries the 4-bit auxiliary graphs (the repository's
    ///     <c>q4</c> graphs map to the bundle naming rule's <c>int4</c> quantization token).
    /// </summary>
    internal const string AuxiliaryBundleName = "chatterbox-turbo-aux-20261014-int4-b1";

    /// <summary>
    ///     The Hugging Face resolve URL prefix of the model repository's ONNX folder: the original source of the vendor
    ///     files, recorded in the bundles' <c>model.json</c> and <c>README.txt</c>. The runtime never downloads from it;
    ///     it downloads the bundles from the <c>models</c> release.
    /// </summary>
#pragma warning disable S1075 // The vendor files' original URLs are intentionally compiled-in, reviewed literals.
    private const string RepositoryOnnxUrl = "https://huggingface.co/ResembleAI/chatterbox-turbo-ONNX/resolve/main/onnx/";
#pragma warning restore S1075

    /// <summary>The language-model graph base name (half precision).</summary>
    private const string LanguageModelGraphName = "language_model_fp16";

    /// <summary>The embedding graph base name (4-bit).</summary>
    private const string EmbedTokensGraphName = "embed_tokens_q4";

    /// <summary>The speech encoder graph base name (4-bit).</summary>
    private const string SpeechEncoderGraphName = "speech_encoder_q4";

    /// <summary>The conditional decoder graph base name (4-bit).</summary>
    private const string ConditionalDecoderGraphName = "conditional_decoder_q4";

    /// <summary>The embedded tokenizer resource name suffix.</summary>
    private const string TokenizerResourceSuffix = "chatterbox-turbo-tokenizer.json.gz";

    /// <summary>The diagnostics category used for voice-resolution messages.</summary>
    private const string DiagnosticsCategory = "ModelManagementSubsystem";

    /// <summary>SHA-256 digests by file name, for every vendor file of the two bundles.</summary>
    private static readonly IReadOnlyDictionary<string, string> Checksums = new Dictionary<string, string>
    {
        ["language_model_fp16.onnx"] = "396b35570dd1dbc5f537ffc932c3e69130ab8a09bf1390469d92338c8eae7da4",
        ["language_model_fp16.onnx_data"] = "d730d6437126d3e232747b26e2a7adece68844f22133277ca5012298ffe36f93",
        ["embed_tokens_q4.onnx"] = "fd6ba1d22902e8f539d3dd6d7c1c44b98ebb4c84ebbb5e47fcb826ddcf667561",
        ["embed_tokens_q4.onnx_data"] = "f54a51e234b509b64c3a03bb79e1149fba7e2eba6c2d9c222f18883379e1f5d8",
        ["speech_encoder_q4.onnx"] = "37956c20b67bed85a0da4bc83509d67b5969a1b257d1c546516a5236a17ad71e",
        ["speech_encoder_q4.onnx_data"] = "58956db217c6443e49c91bdd54d7cf76b4a243f225c748b7bf746459fc27bc7d",
        ["conditional_decoder_q4.onnx"] = "dccb7a6cea3472dc7f7d070eeb70ade18e6327fb4ec61a3d62cf211bfed90ea2",
        ["conditional_decoder_q4.onnx_data"] = "b5c5317e0b79a1a19dd3d5e2b2091ea06b15716716ab801a54eaeb906c6971ec",
    };

    /// <summary>
    ///     The vendor files of the two Chatterbox Turbo component bundles on the <c>models</c> release, keyed by bundle
    ///     name: the half-precision language model (<see cref="LanguageModelBundleName"/>) and the 4-bit auxiliary
    ///     graphs, embedding, speech encoder and conditional decoder (<see cref="AuxiliaryBundleName"/>).
    /// </summary>
    /// <remarks>
    ///     Built from <see cref="Checksums"/> and the repository URL, so it is the single source of truth for the
    ///     per-file pins: each bundle in <see cref="Bundles"/> uses its entry as its contents (so the installer checks
    ///     every extracted file against these pins), and the non-shipped <c>DemaConsulting.Speech.ModelBundler</c> tool
    ///     (which sees this member through <c>InternalsVisibleTo</c>) packs exactly these files into each bundle, so
    ///     no SHA-256 is declared twice. Each graph is followed by its weights file. Immutable and safe to share
    ///     across threads.
    /// </remarks>
    internal static IReadOnlyDictionary<string, SpeechModelDownloadDescriptor> BundleContents { get; } =
        new Dictionary<string, SpeechModelDownloadDescriptor>(StringComparer.Ordinal)
        {
            [LanguageModelBundleName] = BuildVendorFiles(LanguageModelGraphName),
            [AuxiliaryBundleName] = BuildVendorFiles(EmbedTokensGraphName, SpeechEncoderGraphName, ConditionalDecoderGraphName),
        };

    /// <summary>
    ///     The two <c>models</c>-release bundles this model installs from, language model first, each pinned by its zip
    ///     SHA-256 and holding the unmodified files of its <see cref="BundleContents"/> entry.
    /// </summary>
    /// <remarks>
    ///     Public so a host or a mirror administrator can list exactly which zips (name, URL and SHA-256) this model
    ///     downloads: <c>chatterbox-turbo-lm-20261014-fp16-b1</c> and <c>chatterbox-turbo-aux-20261014-int4-b1</c>.
    ///     The bundle bytes are reproducible, so the pins never change for these bundle revisions. Declared after
    ///     <see cref="BundleContents"/> because static initializers run in textual order. Immutable and safe to share
    ///     across threads.
    /// </remarks>
    public static IReadOnlyList<SpeechModelBundle> Bundles { get; } =
    [
        new SpeechModelBundle(
            new Uri(SpeechModelBundle.ModelsReleaseBaseUri, LanguageModelBundleName + ".zip"),
            "2c2e05d62e18453fa8c726d57b8c970948f5d693400f303fc2e9dc97838a1584",
            BundleContents[LanguageModelBundleName]),
        new SpeechModelBundle(
            new Uri(SpeechModelBundle.ModelsReleaseBaseUri, AuxiliaryBundleName + ".zip"),
            "1981e10e65bb4f2954b99dee6c9b9e74242428cd87ed0dadf085ca6f8f229581",
            BundleContents[AuxiliaryBundleName]),
    ];

    /// <summary>The configuration this instance was created with.</summary>
    private readonly ChatterboxTurboModelOptions _options;

    /// <summary>The resolved voices, index = speaker id.</summary>
    private readonly IReadOnlyList<ChatterboxTurboReferenceVoice> _voices;

    /// <summary>The capability profile configured with the sentence-final tag policy.</summary>
    private readonly ChatterboxTurboCapabilityProfile _profile;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OnnxChatterboxTurboEnglishSynthesisModel"/> class.
    /// </summary>
    /// <param name="options">The configuration, or <see langword="null"/> for defaults (which supply no voices).</param>
    /// <exception cref="ArgumentException">
    ///     Thrown when two explicit or voice-directory voices share an id (ordinal comparison).
    ///     A voice from the voice library whose id is already present is skipped and reported at
    ///     <see cref="SpeechDiagnosticLevel.Info"/> instead.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when <see cref="ChatterboxTurboModelOptions.ReferenceVoiceDirectory"/> does not exist.</exception>
    /// <remarks>
    ///     Files in <see cref="ChatterboxTurboModelOptions.ReferenceVoiceDirectory"/> are decoded lazily, so a damaged
    ///     file never fails construction: it fails at first use of that voice with an <see cref="InvalidDataException"/>
    ///     naming the file.
    /// </remarks>
    public OnnxChatterboxTurboEnglishSynthesisModel(ChatterboxTurboModelOptions? options = null)
        : this(options, defaultLibrary: null)
    {
    }

    /// <summary>
    ///     Initializes a new instance with a fallback voice library (used by the catalog extension, which supplies a
    ///     library over the catalog's own store when the options name none).
    /// </summary>
    /// <param name="options">The configuration, or <see langword="null"/> for defaults.</param>
    /// <param name="defaultLibrary">The library used when <see cref="ChatterboxTurboModelOptions.ReferenceVoiceLibrary"/> is <see langword="null"/>.</param>
    internal OnnxChatterboxTurboEnglishSynthesisModel(ChatterboxTurboModelOptions? options, ReferenceVoiceLibrary? defaultLibrary)
    {
        _options = options ?? new ChatterboxTurboModelOptions();
        _profile = new ChatterboxTurboCapabilityProfile(_options.EndOfSentenceTagPolicy, _options.MoodJoinSilenceMilliseconds);
        _voices = ResolveVoices(_options, defaultLibrary);

        // The voice parameter only exists when there is at least one voice (a choice needs an option)
        Parameters = _voices.Count == 0
            ? []
            :
            [
                new ChoiceParameter(
                    VoiceParameterId,
                    "Voice",
                    "Selects the reference voice the model clones. The first voice is the default. " + ReferenceVoiceLibrary.ConsentNotice,
                    [.. _voices.Select(voice => new ChoiceParameterOption(voice.Id, voice.DisplayName))],
                    _voices[0].Id),
            ];
    }

    /// <inheritdoc/>
    public string Id => ModelId;

    /// <inheritdoc/>
    public string DisplayName => "Chatterbox Turbo English (voice cloning, ONNX, CPU)";

    /// <inheritdoc/>
    public string LicenseName => "MIT";

    /// <inheritdoc/>
#pragma warning disable S1075 // The license page is a deliberate, reviewed literal.
    public Uri? LicenseUrl { get; } = new("https://opensource.org/license/mit");
#pragma warning restore S1075

    /// <inheritdoc/>
    public SpeechModelRole Role => SpeechModelRole.Synthesis;

    /// <inheritdoc/>
    /// <remarks>One <see cref="ChoiceParameter"/> selecting the reference voice, or none when no voices were supplied.</remarks>
    public IReadOnlyList<ISpeechModelParameter> Parameters { get; }

    /// <inheritdoc/>
    /// <remarks>
    ///     Native: the model produces some paralinguistic sounds itself. Rendering is handled by this model's own
    ///     capability profile, which maps only the tags the model supports and strips or approximates the rest.
    /// </remarks>
    public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.Native;

    /// <inheritdoc/>
    /// <remarks>
    ///     The two zips of <see cref="Bundles"/>, language model first; <see cref="InstallAsync"/> then extracts
    ///     <see cref="InstalledFiles"/> from them.
    /// </remarks>
    public SpeechModelDownloadDescriptor DownloadDescriptor { get; } = SpeechModelBundle.CreateDescriptor(Bundles);

    /// <summary>
    ///     The eight vendor files the model installs: the language-model graph and weights followed by the three
    ///     auxiliary graphs and their weights (the contents of <see cref="Bundles"/>, in order).
    /// </summary>
    /// <remarks>
    ///     <see cref="CreateBackend"/> checks these files rather than <see cref="DownloadDescriptor"/>, because the
    ///     descriptor names zips that are deleted once extracted. Immutable and safe to share across threads.
    /// </remarks>
    internal static IReadOnlyList<SpeechModelDownloadFile> InstalledFiles { get; } =
        [.. Bundles.SelectMany(bundle => bundle.Contents.Files)];

    /// <summary>
    ///     Extracts and verifies the two downloaded component bundles of <see cref="Bundles"/> in the staging directory.
    /// </summary>
    /// <param name="stagedFilesDirectory">The staging directory holding the downloaded, checksum-verified zips.</param>
    /// <param name="cancellationToken">A token to observe while extracting.</param>
    /// <returns>A task that completes once the bundles are extracted and the zips are deleted.</returns>
    /// <remarks>
    ///     Delegates to <see cref="SpeechModelBundle.InstallAsync"/>, which rejects any unsafe entry name, any extra
    ///     or missing file, and any file whose SHA-256 differs from its pin, and places each bundle's license files
    ///     under <c>licenses/&lt;bundle&gt;/</c>; an exception here leaves no partial install.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stagedFilesDirectory"/> is null or empty.</exception>
    /// <exception cref="InvalidDataException">Thrown when a zip or one of its files fails verification.</exception>
    public Task InstallAsync(string stagedFilesDirectory, CancellationToken cancellationToken = default) =>
        SpeechModelBundle.InstallAsync(Bundles, stagedFilesDirectory, cancellationToken);

    /// <inheritdoc/>
    AudioFormat ISynthesisModel.PreferredAudioFormat => AudioFormat.Mono(OnnxChatterboxTurboSynthesisEngine.OutputSampleRate);

    /// <inheritdoc/>
    IModelCapabilityProfile ISynthesisModel.CapabilityProfile => _profile;

    /// <summary>
    ///     Builds a loaded backend: creates the four ONNX Runtime sessions, loads the embedded tokenizer and
    ///     wires the voices.
    /// </summary>
    /// <param name="installedModelDirectory">The directory the model files were installed into.</param>
    /// <returns>The backend.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the directory is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the directory is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no reference voice was supplied.</exception>
    /// <exception cref="FileNotFoundException">Thrown when an expected graph file is missing.</exception>
    public ISynthesisBackend CreateBackend(string installedModelDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(installedModelDirectory);

        if (_voices.Count == 0)
        {
            throw new InvalidOperationException(
                "Chatterbox Turbo needs at least one reference voice: install the reference-voice pack (CLI: 'voices install'), add .wav or .flac files to the reference-voices 'user' folder in the model store, or supply voices through ChatterboxTurboModelOptions (for example --voices-dir).");
        }

        // Check every graph and weights file up front for a clear message
        foreach (var file in InstalledFiles)
        {
            var path = file.ResolveStagedPath(installedModelDirectory);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Chatterbox Turbo model file '{path}' is missing.", path);
            }
        }

        string GraphPath(string name) => Path.Combine(installedModelDirectory, "onnx", name + ".onnx");

        // The networks dispose their own sessions if creation fails part-way
        var networks = new OnnxChatterboxTurboNetworks(
            GraphPath(SpeechEncoderGraphName),
            GraphPath(EmbedTokensGraphName),
            GraphPath(LanguageModelGraphName),
            GraphPath(ConditionalDecoderGraphName),
            _options.IntraOpThreads);
        try
        {
            return new OnnxChatterboxTurboSynthesisEngine(
                networks,
                LoadTokenizer(),
                _voices,
                _options.MaxNewTokens,
                _options.MaxAudioTokensPerTextToken,
                _options.MaxConsecutiveSpeechTokenRepeats);
        }
        catch
        {
            // Ownership never reached the engine
            networks.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Resolves the selected voice id to its index.
    /// </summary>
    /// <param name="parameterValues">The caller-supplied parameter values, or null.</param>
    /// <returns>The voice index; the first voice (0) for a null bag, a missing value or an unknown voice.</returns>
    int ISynthesisModel.ResolveSpeakerId(IReadOnlyDictionary<string, object>? parameterValues)
    {
        if (parameterValues is not null &&
            parameterValues.TryGetValue(VoiceParameterId, out var value) &&
            value is string voiceId)
        {
            for (var index = 0; index < _voices.Count; index++)
            {
                if (string.Equals(_voices[index].Id, voiceId, StringComparison.Ordinal))
                {
                    return index;
                }
            }
        }

        return 0;
    }

    /// <summary>Opens the embedded gzip tokenizer and builds the tokenizer from it.</summary>
    internal static ByteLevelBpeTokenizer LoadTokenizer()
    {
        using var stream = OpenTokenizerResource();
        return ByteLevelBpeTokenizer.FromGzipStream(stream);
    }

    /// <summary>Opens the embedded gzip-compressed tokenizer document.</summary>
    internal static Stream OpenTokenizerResource()
    {
        var assembly = typeof(OnnxChatterboxTurboEnglishSynthesisModel).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(TokenizerResourceSuffix, StringComparison.Ordinal));
        return assembly.GetManifestResourceStream(name)!;
    }

    /// <summary>
    ///     Combines explicit voices, those in the voice directory and those from the voice library (user folder, installed packs).
    ///     A duplicate id among the explicit and directory voices is a caller error; a library voice whose id is already present is skipped.
    /// </summary>
    private static List<ChatterboxTurboReferenceVoice> ResolveVoices(ChatterboxTurboModelOptions options, ReferenceVoiceLibrary? defaultLibrary)
    {
        var voices = new List<ChatterboxTurboReferenceVoice>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        void AddUnique(ChatterboxTurboReferenceVoice voice, string source)
        {
            if (!ids.Add(voice.Id))
            {
                throw new ArgumentException($"The reference voice id '{voice.Id}' is duplicated ({source}).");
            }

            voices.Add(voice);
        }

        foreach (var voice in options.ReferenceVoices)
        {
            AddUnique(voice, "ChatterboxTurboModelOptions.ReferenceVoices");
        }

        if (options.ReferenceVoiceDirectory is not null)
        {
            if (!Directory.Exists(options.ReferenceVoiceDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"Reference voice directory '{options.ReferenceVoiceDirectory}' was not found.");
            }

            // Match by extension, case-insensitively, against the formats the core audio reader understands
            var files = Directory.GetFiles(options.ReferenceVoiceDirectory)
                .Where(file => AudioFileReader.SupportedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .ToArray();
            Array.Sort(files, StringComparer.Ordinal);

            // Lazy so a damaged file never fails catalog composition: the error surfaces at first use of that voice and names the file
            foreach (var file in files)
            {
                AddUnique(ChatterboxTurboReferenceVoice.FromFileLazy(Path.GetFileNameWithoutExtension(file), file), "ChatterboxTurboModelOptions.ReferenceVoiceDirectory");
            }
        }

        if (options.IncludeStoreVoices)
        {
            AddLibraryVoices(voices, ids, options.ReferenceVoiceLibrary ?? defaultLibrary, options.Diagnostics ?? NullSpeechDiagnostics.Instance);
        }

        return voices;
    }

    /// <summary>Appends the library's voices lazily; an id that is already present wins and the skip is reported.</summary>
    private static void AddLibraryVoices(
        List<ChatterboxTurboReferenceVoice> voices,
        HashSet<string> ids,
        ReferenceVoiceLibrary? library,
        ISpeechDiagnostics diagnostics)
    {
        if (library is null)
        {
            return;
        }

        var labels = new HashSet<string>(voices.Select(voice => voice.DisplayName), StringComparer.Ordinal);
        foreach (var info in library.EnumerateVoices())
        {
            if (!ids.Add(info.Id))
            {
                diagnostics.Report(
                    SpeechDiagnosticLevel.Info,
                    DiagnosticsCategory,
                    $"Reference voice '{info.Id}' from '{info.FilePath}' is hidden by an earlier voice with the same id.");
                continue;
            }

            // Labels need not be unique, but a source suffix keeps same-named voices distinguishable; repeated
            // collisions are numbered, e.g. "Name", "Name (user)", "Name (user 2)", so every label ends up unique
            var label = info.DisplayName;
            if (!labels.Add(label))
            {
                var source = info.Source == ReferenceVoiceSource.Pack ? "pack" : "user";
                var number = 2;
                label = $"{info.DisplayName} ({source})";
                while (!labels.Add(label))
                {
                    label = $"{info.DisplayName} ({source} {number})";
                    number++;
                }
            }

            voices.Add(ChatterboxTurboReferenceVoice.FromFileLazy(info.Id, info.FilePath, label, info.MoodClips));
        }
    }

    /// <summary>Builds the vendor files of one component bundle: each graph followed by its weights file.</summary>
    /// <param name="graphNames">The graph base names (without extension), in order.</param>
    /// <returns>The descriptor of the graphs' <c>.onnx</c> and <c>.onnx_data</c> files, installed under <c>onnx/</c>.</returns>
#pragma warning disable S1075 // The vendor files' original URLs are intentionally compiled-in, reviewed literals.
    private static SpeechModelDownloadDescriptor BuildVendorFiles(params string[] graphNames)
    {
        var files = new List<SpeechModelDownloadFile>();
        foreach (var name in graphNames)
        {
            foreach (var extension in new[] { ".onnx", ".onnx_data" })
            {
                var fileName = name + extension;
                files.Add(new SpeechModelDownloadFile(
                    new Uri(RepositoryOnnxUrl + fileName),
                    Checksums[fileName],
                    "onnx/" + fileName));
            }
        }

        return new SpeechModelDownloadDescriptor(files);
    }
#pragma warning restore S1075
}
