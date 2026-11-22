using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.OnnxRuntimeSubsystem;
using DemaConsulting.Speech.Onnx.Piper.SynthesisSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;

namespace DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;

/// <summary>
///     Production <see cref="ISynthesisModel"/> for the Piper English (US) voice <c>en_US-libritts_r-medium</c>, run
///     directly through ONNX Runtime with no bundled native inference engine and no espeak-ng, unlike the sibling
///     <c>DemaConsulting.Speech.Sherpa</c> package's sherpa-onnx-backed <c>SherpaOnnxVitsLibriTtsEnglishSynthesisModel</c>
///     for the same voice.
/// </summary>
/// <remarks>
///     <para>
///     <b>What this class owns.</b> The Piper graph does no phonemization: it takes phoneme ids. This class supplies the
///     text analysis (<see cref="TextAnalyzer"/>: spoken forms of numbers and abbreviations, the chosen reading of each
///     heteronym) and its engine phonemizes English from the CMU Pronouncing Dictionary, renders the result as the
///     espeak-style IPA the voice was trained on and encodes it through the voice's own symbol table. The speaking style
///     is therefore ours, not espeak-ng's: it follows the voice's phoneme inventory closely but is a listening-quality
///     approximation of espeak-ng's rules (reductions, stress and flapping are rule-based), and words outside the
///     dictionary are pronounced by a small trained letter-to-sound model. This voice is a LibriTTS-R fine-tune with
///     904 speakers of widely varying quality; speaker 0 is the default.
///     </para>
///     <para>
///     <b>Download</b>: the voice is installed from one bundle on this project's <c>models</c> GitHub release
///     (<see cref="Bundles"/>, <c>piper-en-us-libritts-r-medium-20261122-fp32-b1.zip</c>), so a user needs no Hugging
///     Face access. The zip holds the voice's three original files from <c>rhasspy/piper-voices</c>
///     (<see cref="BundleContents"/>: the graph <c>en_US-libritts_r-medium.onnx</c>, the voice configuration
///     <c>en_US-libritts_r-medium.onnx.json</c> and the <c>MODEL_CARD</c>), unmodified, together with the CC BY 4.0
///     <c>LICENSE</c>, a <c>NOTICE</c> with the full attribution and lineage, a <c>README.txt</c> and a
///     <c>model.json</c> that records each file's original URL. The zip is pinned by its SHA-256 and every extracted file
///     is checked again against its own pin. A configured download mirror serves the zip at
///     <c>{mirror}/piper-onnx-libritts-r-en-us-medium/piper-en-us-libritts-r-medium-20261122-fp32-b1.zip</c>.
///     </para>
///     <para>
///     <b>Graph compatibility</b>: the engine reads the sample rate, the speaker count, the inference scales and the
///     symbol table from the <c>.onnx.json</c> voice configuration only, and feeds the graph through the standard Piper
///     input names (<c>input</c>, <c>input_lengths</c>, <c>scales</c>, <c>sid</c>); it never reads the graph's own
///     metadata. A voice installed by an earlier version of this package from the sherpa-onnx re-packaged archive (whose
///     graph carries added metadata, with the same voice configuration) therefore keeps working unchanged, and a
///     reinstall moves it to the original vendor graph.
///     </para>
///     <para>
///     <b>Speaker selection</b>: the voice has 904 speakers identified only by number (0-903); LibriTTS-R publishes no
///     speaker names. The <see cref="SpeakerParameterId"/> numeric parameter selects one; a missing, non-numeric or
///     out-of-range value selects speaker 0 rather than failing synthesis. Speaking speed is the library's own speed
///     argument, applied through the voice's length scale.
///     </para>
///     <para>
///     <b>License</b>: CC BY 4.0, as declared by the voice's own <c>MODEL_CARD</c> (dataset
///     <c>http://www.openslr.org/141/</c>, license CC BY 4.0). It is an attribution license: a redistribution of the
///     voice, or of audio it generates where the license's attribution applies, must credit the LibriTTS-R corpus
///     (Koizumi et al., 2023; CC BY 4.0; OpenSLR resource 141) and the Piper project (Rhasspy) the voice was trained
///     with. <b>Lineage</b>: the same model card states that the voice was fine-tuned from the English <c>lessac</c>
///     medium voice, which was trained on the Blizzard Challenge 2013 Lessac data; that data is licensed for
///     non-commercial research use only. Consumers must review the upstream terms and decide whether the voice fits
///     their use. The full credit and the lineage notice are in this package's <c>THIRD-PARTY-NOTICES.md</c> and in the
///     bundle's <c>NOTICE</c>, which is installed with the voice under <c>licenses/</c>.
///     </para>
/// </remarks>
public sealed class OnnxPiperLibriTtsEnSynthesisModel : ISynthesisModel
{
    /// <summary>The stable catalog identifier for this model.</summary>
    public const string ModelId = "piper-onnx-libritts-r-en-us-medium";

    /// <summary>The parameter id of the numeric speaker selection.</summary>
    public const string SpeakerParameterId = "speaker";

    /// <summary>The install path of the ONNX graph.</summary>
    private const string ModelFileName = "en_US-libritts_r-medium.onnx";

    /// <summary>The install path of the voice configuration.</summary>
    private const string ConfigFileName = "en_US-libritts_r-medium.onnx.json";

    /// <summary>The install path of the voice's model card (installed for provenance; the engine does not read it).</summary>
    private const string ModelCardFileName = "MODEL_CARD";

    /// <summary>
    ///     The Hugging Face resolve URL prefix of the voice's folder in <c>rhasspy/piper-voices</c>: the original source
    ///     of the vendor files, recorded in the bundle's <c>model.json</c> and <c>README.txt</c>. The runtime never
    ///     downloads from it; it downloads the bundle from the <c>models</c> release.
    /// </summary>
#pragma warning disable S1075 // The vendor files' original URLs are intentionally compiled-in, reviewed literals.
    private const string VoiceSourceUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/libritts_r/medium/";
#pragma warning restore S1075

    /// <summary>The output sample rate of the voice, in Hz (the configuration is checked against it).</summary>
    private const int PreferredSampleRate = 22050;

    /// <summary>The lowest valid speaker index.</summary>
    private const int MinSpeakerId = 0;

    /// <summary>The highest valid speaker index (904 speakers).</summary>
    private const int MaxSpeakerId = 903;

    /// <summary>The default speaker index.</summary>
    private const int DefaultSpeakerId = 0;

    /// <summary>
    ///     The shared English text analyzer: created on first use (the analyzer loads its data lazily) and then shared by
    ///     every instance, because it is immutable and thread-safe. It selects the annotations the Piper phonemization
    ///     uses: normalization, reading and part of speech (the tag tells the noun <i>will</i> from the auxiliary, which
    ///     the renderer weakens).
    /// </summary>
    private static readonly Lazy<EnglishTextAnalyzer> SharedAnalyzer = new(
        () => new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions
        {
            Annotations = TextEnAnnotationSelection.Normalization | TextEnAnnotationSelection.Reading | TextEnAnnotationSelection.PartOfSpeech,
        }));

    /// <summary>The execution provider names attempted, in order, before falling back to CPU.</summary>
    private readonly IReadOnlyList<string>? _preferredExecutionProviderNames;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OnnxPiperLibriTtsEnSynthesisModel"/> class, using
    ///     <see cref="OnnxExecutionProviderSelector.DefaultProviderNames"/> (CPU-only).
    /// </summary>
    public OnnxPiperLibriTtsEnSynthesisModel()
        : this(null)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="OnnxPiperLibriTtsEnSynthesisModel"/> class, attempting
    ///     <paramref name="preferredExecutionProviderNames"/> before falling back to CPU.
    /// </summary>
    /// <param name="preferredExecutionProviderNames">
    ///     The ordered, accelerated execution provider names to attempt first (for example <c>"DmlExecutionProvider"</c>),
    ///     or <see langword="null"/> for CPU-only. A host that wants acceleration adds the matching native runtime package
    ///     itself and passes the provider name here; see <see cref="OnnxExecutionProviderSelector"/>.
    /// </param>
    public OnnxPiperLibriTtsEnSynthesisModel(IReadOnlyList<string>? preferredExecutionProviderNames)
    {
        _preferredExecutionProviderNames = preferredExecutionProviderNames;
    }

    /// <inheritdoc/>
    public string Id => ModelId;

    /// <inheritdoc/>
    public string DisplayName => "Piper LibriTTS-R English (US, 904 speakers, ONNX)";

    /// <summary>
    ///     <inheritdoc/>
    ///     The declared license of the voice. See the type-level remarks' "License" paragraph for the required attribution
    ///     (LibriTTS-R, Piper) and the lineage notice (fine-tuned from the English <c>lessac</c> medium voice, trained on
    ///     the Blizzard Challenge 2013 Lessac data, which is licensed for non-commercial research use only; consumers must
    ///     review the upstream terms).
    /// </summary>
    public string LicenseName => "CC BY 4.0";

    /// <inheritdoc/>
    public Uri? LicenseUrl { get; } = new("https://creativecommons.org/licenses/by/4.0/");

    /// <inheritdoc/>
    /// <remarks>
    ///     The English text analysis of <c>DemaConsulting.Speech.Text.En</c>: it writes out numbers, dates, currency and
    ///     abbreviations and chooses the reading of each heteronym (<c>record</c> the noun or the verb), which the
    ///     synthesis engine uses when it phonemizes the text.
    /// </remarks>
    public ITextAnalyzer? TextAnalyzer => SharedAnalyzer.Value;

    /// <inheritdoc/>
    public SpeechModelRole Role => SpeechModelRole.Synthesis;

    /// <inheritdoc/>
    /// <remarks>Declares one parameter: the numeric speaker selection (0-903, default 0).</remarks>
    public IReadOnlyList<ISpeechModelParameter> Parameters { get; } =
    [
        new NumericParameter(
            SpeakerParameterId,
            "Speaker",
            "Selects one of this voice's 904 speakers by number (0-903). LibriTTS-R publishes no speaker names, so " +
            "speakers are identified only by number; voice quality differs between speakers.",
            new NumericParameterBounds(MinSpeakerId, MaxSpeakerId, 1, DefaultSpeakerId),
            isInteger: true),
    ];

    /// <inheritdoc/>
    /// <remarks>
    ///     A plain VITS voice has no inline audio-tag support: the profile (see <see cref="ISynthesisModel.CapabilityProfile"/>)
    ///     strips tags to plain narration and renders pauses as inserted silence.
    /// </remarks>
    public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.None;

    /// <inheritdoc/>
    /// <remarks>
    ///     The library's default profile followed by one change: the clause pieces of a sentence (the core chunker splits
    ///     at every comma, semicolon and colon) are joined again, so the voice speaks a sentence as one utterance with its
    ///     commas, as it was trained to; see <see cref="PiperCapabilityProfile"/> for the reasons and the limits.
    /// </remarks>
    IModelCapabilityProfile ISynthesisModel.CapabilityProfile => PiperCapabilityProfile.Instance;

    /// <summary>
    ///     The name of the <c>models</c>-release bundle that carries this voice's unmodified vendor files (see
    ///     <see cref="BundleContents"/>). The upstream release token is the snapshot date: the voice has no verifiable
    ///     upstream version tag, so the date the files were taken from <c>rhasspy/piper-voices</c> names the snapshot.
    /// </summary>
    internal const string BundleName = "piper-en-us-libritts-r-medium-20261122-fp32-b1";

    /// <summary>
    ///     The vendor files of the <see cref="BundleName"/> bundle: the voice's graph, its configuration and its model
    ///     card, each with its original upstream URL, SHA-256 pin and install path (flat, at the installed root).
    /// </summary>
    /// <remarks>
    ///     This is the single source of truth for the per-file pins: <see cref="Bundles"/> uses it as the bundle's
    ///     contents (so the installer checks every extracted file against these pins), and the non-shipped
    ///     <c>DemaConsulting.Speech.ModelBundler</c> tool (which sees this member through <c>InternalsVisibleTo</c>)
    ///     packs exactly these files into the bundle, so no SHA-256 is declared twice. The pins are the SHA-256 of the
    ///     original <c>rhasspy/piper-voices</c> files (78,580,914, 20,123 and 279 bytes). Immutable and safe to share
    ///     across threads.
    /// </remarks>
    internal static SpeechModelDownloadDescriptor BundleContents { get; } = new(
    [
        new SpeechModelDownloadFile(
            new Uri(VoiceSourceUrl + ModelFileName),
            "10bb85e071d616fcf4071f369f1799d0491492ab3c5d552ec19fb548fac13195",
            ModelFileName),
        new SpeechModelDownloadFile(
            new Uri(VoiceSourceUrl + ConfigFileName),
            "b471dc60d2d8335e819c393d196d6fbf792817f40051257b269878505bc9afb3",
            ConfigFileName),
        new SpeechModelDownloadFile(
            new Uri(VoiceSourceUrl + ModelCardFileName),
            "0ccde6927e5bb4d743f4ea39618a9387ba18cca3351220a8a9cfdbc68b30fcb9",
            ModelCardFileName),
    ]);

    /// <summary>
    ///     The <c>models</c>-release bundle this voice installs from: one zip holding the unmodified files of
    ///     <see cref="BundleContents"/>, pinned by the zip's own SHA-256.
    /// </summary>
    /// <remarks>
    ///     Public so a host or a mirror administrator can list exactly which zip (name, URL and SHA-256) this model
    ///     downloads. The zip pin is the published asset's checksum; the bundle bytes are reproducible, so the pin never
    ///     changes for this bundle revision. Declared after <see cref="BundleContents"/> because static initializers run
    ///     in textual order. Immutable and safe to share across threads.
    /// </remarks>
    public static IReadOnlyList<SpeechModelBundle> Bundles { get; } =
    [
        new SpeechModelBundle(
            new Uri(SpeechModelBundle.ModelsReleaseBaseUri, BundleName + ".zip"),
            "5196a8aa97f9053e0bdbbc2299007d48d55f37a74e88199869daebb710d8bd0f",
            BundleContents),
    ];

    /// <inheritdoc/>
    /// <remarks>
    ///     Declares the single zip of <see cref="Bundles"/>; <see cref="InstallAsync"/> then extracts the graph, the voice
    ///     configuration and the model card (the files of <see cref="BundleContents"/>).
    /// </remarks>
    public SpeechModelDownloadDescriptor DownloadDescriptor { get; } = SpeechModelBundle.CreateDescriptor(Bundles);

    /// <inheritdoc/>
    AudioFormat ISynthesisModel.PreferredAudioFormat => AudioFormat.Mono(PreferredSampleRate);

    /// <summary>
    ///     Extracts and verifies the downloaded bundle of <see cref="Bundles"/> in the staging directory, leaving the
    ///     vendor files at the installed root and the bundle's license files under
    ///     <c>licenses/piper-en-us-libritts-r-medium-20261122-fp32-b1/</c>.
    /// </summary>
    /// <param name="stagedFilesDirectory">The staging directory holding the downloaded, checksum-verified zip.</param>
    /// <param name="cancellationToken">A token to observe while extracting.</param>
    /// <returns>A task that completes once the bundle is extracted and the zip is deleted.</returns>
    /// <remarks>
    ///     Delegates to <see cref="SpeechModelBundle.InstallAsync"/>, which rejects any unsafe entry name, any extra or
    ///     missing file, and any file whose SHA-256 differs from its pin in <see cref="BundleContents"/>; an exception
    ///     here leaves no partial install.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stagedFilesDirectory"/> is null or empty.</exception>
    /// <exception cref="InvalidDataException">Thrown when the zip or one of its files fails verification.</exception>
    public Task InstallAsync(string stagedFilesDirectory, CancellationToken cancellationToken = default) =>
        SpeechModelBundle.InstallAsync(Bundles, stagedFilesDirectory, cancellationToken);

    /// <summary>
    ///     Builds a loaded <see cref="OnnxPiperSynthesisEngine"/>: reads the voice configuration, then creates the ONNX
    ///     Runtime session (through <see cref="OnnxExecutionProviderSelector"/>, using this instance's preferred
    ///     providers).
    /// </summary>
    /// <remarks>
    ///     The configuration and the phoneme pipeline are created before the session, so a configuration that is invalid
    ///     fails without a native session ever existing, and the engine's constructor, the only step after the session is
    ///     created, cannot fail. The selector disposes a candidate session whose probe fails.
    /// </remarks>
    /// <inheritdoc/>
    /// <exception cref="InvalidDataException">
    ///     The voice configuration is unusable, or does not describe this voice (exactly 904 speakers at 22,050 Hz).
    /// </exception>
    public ISynthesisBackend CreateBackend(string installedModelDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(installedModelDirectory);

        var config = PiperVoiceConfig.Load(Path.Combine(installedModelDirectory, ConfigFileName));
        if (config.SampleRate != PreferredSampleRate || config.NumSpeakers != MaxSpeakerId + 1)
        {
            throw new InvalidDataException(
                $"The voice configuration describes {config.NumSpeakers} speakers at {config.SampleRate} Hz; " +
                $"this model expects {MaxSpeakerId + 1} speakers at {PreferredSampleRate} Hz.");
        }

        var pipeline = new PiperPhonemePipeline(config);
        var session = OnnxExecutionProviderSelector.Create(
            Path.Combine(installedModelDirectory, ModelFileName),
            _preferredExecutionProviderNames,
            candidate => OnnxPiperSynthesisEngine.RunProbeInference(candidate, config, pipeline));
        return new OnnxPiperSynthesisEngine(session, config, pipeline);
    }

    /// <summary>
    ///     Resolves the <see cref="SpeakerParameterId"/> value in <paramref name="parameterValues"/> to the speaker id.
    /// </summary>
    /// <param name="parameterValues">The untyped parameter bag supplied by the host, or <see langword="null"/>.</param>
    /// <returns>
    ///     The selected speaker when it is a whole number within 0-903; otherwise speaker 0 (including when the bag is
    ///     <see langword="null"/>, has no speaker value, or has a value that is not a finite number).
    /// </returns>
    /// <remarks>
    ///     Never throws. Accepts a boxed <see cref="double"/> (what a generic numeric-parameter UI supplies), a boxed
    ///     <see cref="int"/> and a boxed <see cref="float"/>; a fractional value is rounded to the nearest speaker.
    /// </remarks>
    int ISynthesisModel.ResolveSpeakerId(IReadOnlyDictionary<string, object>? parameterValues)
    {
        if (parameterValues is null || !parameterValues.TryGetValue(SpeakerParameterId, out var value))
        {
            return DefaultSpeakerId;
        }

        double? numericValue = value switch
        {
            double doubleValue => doubleValue,
            int intValue => intValue,
            float floatValue => floatValue,
            _ => null,
        };

        if (numericValue is not double resolved || !double.IsFinite(resolved))
        {
            return DefaultSpeakerId;
        }

        var speakerId = (int)Math.Round(resolved, MidpointRounding.AwayFromZero);
        return speakerId is >= MinSpeakerId and <= MaxSpeakerId ? speakerId : DefaultSpeakerId;
    }
}
