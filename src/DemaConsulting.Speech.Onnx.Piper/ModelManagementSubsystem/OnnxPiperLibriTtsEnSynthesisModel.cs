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
///     <b>Download</b>: the voice is installed from the SHA-256-pinned archive that the sherpa-onnx project publishes for
///     it (<c>vits-piper-en_US-libritts_r-medium.tar.bz2</c>, 82,038,311 bytes). The archive also contains the
///     espeak-ng data (GPL-3.0) and the sherpa-onnx token table, which this class does not use: only the graph
///     (<c>en_US-libritts_r-medium.onnx</c>) and the voice configuration (<c>en_US-libritts_r-medium.onnx.json</c>)
///     are extracted (<see cref="InstallAsync"/>), so no espeak-ng data is ever written to the installed model. The
///     voice's own distribution (Hugging Face, <c>rhasspy/piper-voices</c>) could not be reached from this repository's
///     development machine to verify its file hashes, so this archive is the one whose hash was verified.
///     </para>
///     <para>
///     <b>Speaker selection</b>: the voice has 904 speakers identified only by number (0-903); LibriTTS-R publishes no
///     speaker names. The <see cref="SpeakerParameterId"/> numeric parameter selects one; a missing, non-numeric or
///     out-of-range value selects speaker 0 rather than failing synthesis. Speaking speed is the library's own speed
///     argument, applied through the voice's length scale.
///     </para>
///     <para>
///     <b>License</b>: CC BY 4.0, confirmed from the voice's own <c>MODEL_CARD</c> in the archive (dataset
///     <c>http://www.openslr.org/141/</c>, license CC BY 4.0). It is an attribution license: a redistribution of the
///     voice, or of audio it generates where the license's attribution applies, must credit the LibriTTS-R corpus
///     (Koizumi et al.; OpenSLR resource 141) and the Piper project the voice was trained with. The required
///     credit is in this package's <c>THIRD-PARTY-NOTICES.md</c>.
///     </para>
/// </remarks>
public sealed class OnnxPiperLibriTtsEnSynthesisModel : ISynthesisModel
{
    /// <summary>The stable catalog identifier for this model.</summary>
    public const string ModelId = "piper-onnx-libritts-r-en-us-medium";

    /// <summary>The parameter id of the numeric speaker selection.</summary>
    public const string SpeakerParameterId = "speaker";

    /// <summary>The relative install path of the downloaded archive, before it is unpacked.</summary>
    private const string ArchiveRelativeInstallPath = "vits-piper-en_US-libritts_r-medium.tar.bz2";

    /// <summary>The install path of the ONNX graph.</summary>
    private const string ModelFileName = "en_US-libritts_r-medium.onnx";

    /// <summary>The install path of the voice configuration.</summary>
    private const string ConfigFileName = "en_US-libritts_r-medium.onnx.json";

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
    ///     every instance, because it is immutable and thread-safe. It selects the normalization and reading annotations
    ///     the Piper phonemization uses (not the part-of-speech annotations, which only the classifier needs internally).
    /// </summary>
    private static readonly Lazy<EnglishTextAnalyzer> SharedAnalyzer = new(
        () => new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions
        {
            Annotations = TextEnAnnotationSelection.Normalization | TextEnAnnotationSelection.Reading,
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
    ///     See the type-level remarks' "License" paragraph.
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
    ///     A plain VITS voice has no inline audio-tag support: the library's default capability profile strips tags to
    ///     plain narration and renders pauses as inserted silence.
    /// </remarks>
    public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.None;

    /// <inheritdoc/>
    /// <remarks>
    ///     One file: the sherpa-onnx release archive of the voice, which <see cref="InstallAsync"/> unpacks (only the
    ///     graph and the configuration are kept). See the type-level remarks for why this archive and not the voice's
    ///     own Hugging Face files.
    /// </remarks>
#pragma warning disable S1075 // This model's download URL is intentionally a compiled-in, reviewed literal: a catalog entry names one specific, checksum-verified release asset.
    public SpeechModelDownloadDescriptor DownloadDescriptor { get; } = new(
    [
        new SpeechModelDownloadFile(
            new Uri("https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/vits-piper-en_US-libritts_r-medium.tar.bz2"),
            "10dc268f3e371696d721486123e2705a9fc1faa113491979fde4d88dba1f1b1c",
            ArchiveRelativeInstallPath),
    ]);
#pragma warning restore S1075

    /// <inheritdoc/>
    AudioFormat ISynthesisModel.PreferredAudioFormat => AudioFormat.Mono(PreferredSampleRate);

    /// <inheritdoc/>
    /// <remarks>
    ///     Keeps only <c>en_US-libritts_r-medium.onnx</c> and <c>en_US-libritts_r-medium.onnx.json</c> from the archive,
    ///     placed directly in the installed directory, and deletes the archive.
    /// </remarks>
    public Task InstallAsync(string stagedFilesDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(stagedFilesDirectory);

        var archivePath = DownloadDescriptor.Files[0].ResolveStagedPath(stagedFilesDirectory);
        return PiperArchiveExtractor.ExtractAndDeleteAsync(archivePath, stagedFilesDirectory, cancellationToken);
    }

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
    /// <exception cref="InvalidDataException">The voice configuration is unusable, or does not describe this voice.</exception>
    public ISynthesisBackend CreateBackend(string installedModelDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(installedModelDirectory);

        var config = PiperVoiceConfig.Load(Path.Combine(installedModelDirectory, ConfigFileName));
        if (config.SampleRate != PreferredSampleRate || config.NumSpeakers <= MaxSpeakerId)
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
