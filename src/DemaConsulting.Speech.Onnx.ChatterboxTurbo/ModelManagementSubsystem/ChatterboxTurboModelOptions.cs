using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;

/// <summary>
///     Caller-supplied configuration for the Chatterbox Turbo model: graph variants, CPU threads, generation
///     guards, sentence-final tag policy and the reference voices.
/// </summary>
/// <remarks>
///     Every property has a safe default, so <c>new ChatterboxTurboModelOptions { ReferenceVoices = ... }</c>
///     is the minimal useful configuration. Invalid values are rejected when set. The 4-bit and 8-bit
///     language-model graphs are not representable by design (see
///     <see cref="ChatterboxTurboLanguageModelVariant"/>). Instances are not thread-safe while being
///     configured; treat them as immutable once passed to the catalog.
/// </remarks>
public sealed class ChatterboxTurboModelOptions
{
    /// <summary>The default hard cap on generated speech tokens (about 40 seconds of audio).</summary>
    public const int DefaultMaxNewTokens = 1024;

    /// <summary>The default cap on generated speech tokens per text token.</summary>
    public const double DefaultMaxAudioTokensPerTextToken = 25.0;

    /// <summary>The default cap on identical consecutive speech tokens; the reference model can lock into one token.</summary>
    public const int DefaultMaxConsecutiveSpeechTokenRepeats = 12;

    /// <summary>The default silence, in milliseconds, inserted where the mood changes between segments.</summary>
    public const int DefaultMoodJoinSilenceMilliseconds = 150;

    /// <summary>The largest accepted mood join silence, in milliseconds.</summary>
    public const int MaxMoodJoinSilenceMilliseconds = 1000;

    /// <summary>The largest default intra-op thread count; more threads do not speed this workload up.</summary>
    private const int DefaultThreadCeiling = 6;

    /// <summary>Backing field for <see cref="LanguageModelVariant"/>.</summary>
    private ChatterboxTurboLanguageModelVariant _languageModelVariant = ChatterboxTurboLanguageModelVariant.Fp16;

    /// <summary>Backing field for <see cref="AuxiliaryModelVariant"/>.</summary>
    private ChatterboxTurboAuxiliaryModelVariant _auxiliaryModelVariant = ChatterboxTurboAuxiliaryModelVariant.Q4;

    /// <summary>Backing field for <see cref="MoodJoinSilenceMilliseconds"/>.</summary>
    private int _moodJoinSilenceMilliseconds = DefaultMoodJoinSilenceMilliseconds;

    /// <summary>Backing field for <see cref="IntraOpThreads"/>.</summary>
    private int _intraOpThreads = DefaultIntraOpThreads;

    /// <summary>Backing field for <see cref="MaxNewTokens"/>.</summary>
    private int _maxNewTokens = DefaultMaxNewTokens;

    /// <summary>Backing field for <see cref="MaxConsecutiveSpeechTokenRepeats"/>.</summary>
    private int _maxConsecutiveSpeechTokenRepeats = DefaultMaxConsecutiveSpeechTokenRepeats;

    /// <summary>Backing field for <see cref="MaxAudioTokensPerTextToken"/>.</summary>
    private double _maxAudioTokensPerTextToken = DefaultMaxAudioTokensPerTextToken;

    /// <summary>Gets the default intra-op thread count: half the logical processors, between one and six.</summary>
    public static int DefaultIntraOpThreads => Math.Min(DefaultThreadCeiling, Math.Max(1, Environment.ProcessorCount / 2));

    /// <summary>Gets or sets the language-model precision. Defaults to <see cref="ChatterboxTurboLanguageModelVariant.Fp16"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a value that is not a defined variant.</exception>
    public ChatterboxTurboLanguageModelVariant LanguageModelVariant
    {
        get => _languageModelVariant;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The language model variant is not defined.");
            }

            _languageModelVariant = value;
        }
    }

    /// <summary>Gets or sets the auxiliary-graph precision. Defaults to <see cref="ChatterboxTurboAuxiliaryModelVariant.Q4"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a value that is not a defined variant.</exception>
    public ChatterboxTurboAuxiliaryModelVariant AuxiliaryModelVariant
    {
        get => _auxiliaryModelVariant;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The auxiliary model variant is not defined.");
            }

            _auxiliaryModelVariant = value;
        }
    }

    /// <summary>Gets or sets the intra-op CPU thread count used by every graph; must be positive.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a value below one.</exception>
    public int IntraOpThreads
    {
        get => _intraOpThreads;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _intraOpThreads = value;
        }
    }

    /// <summary>Gets or sets the hard cap on generated speech tokens per utterance; must be positive.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a value below one.</exception>
    public int MaxNewTokens
    {
        get => _maxNewTokens;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maxNewTokens = value;
        }
    }

    /// <summary>
    ///     Gets or sets the cap on generated speech tokens per text token (a runaway guard); must be positive.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a value that is not positive.</exception>
    public double MaxAudioTokensPerTextToken
    {
        get => _maxAudioTokensPerTextToken;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0);
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The ratio must be a finite number.");
            }

            _maxAudioTokensPerTextToken = value;
        }
    }

    /// <summary>
    ///     Gets or sets the most times the same speech token may be generated in a row before it is excluded from
    ///     the next step (a "stuck in a loop" guard); 0 disables the cap. The stop token is never excluded.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative value.</exception>
    public int MaxConsecutiveSpeechTokenRepeats
    {
        get => _maxConsecutiveSpeechTokenRepeats;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _maxConsecutiveSpeechTokenRepeats = value;
        }
    }

    /// <summary>Gets or sets how a sentence-final tag is rendered. Defaults to moving it before the final word.</summary>
    public ChatterboxTurboEndOfSentenceTagPolicy EndOfSentenceTagPolicy { get; set; } =
        ChatterboxTurboEndOfSentenceTagPolicy.MoveBeforeFinalWord;

    /// <summary>
    ///     Gets or sets the silence, in milliseconds, inserted where the mood (reference clip) changes between two
    ///     spoken segments, for example at <c>[sad]</c> in <c>Fine. [sad] Not really.</c>; 0 inserts none, and a
    ///     longer explicit pause is never shortened. Defaults to <see cref="DefaultMoodJoinSilenceMilliseconds"/>;
    ///     must be between 0 and <see cref="MaxMoodJoinSilenceMilliseconds"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside the allowed range.</exception>
    public int MoodJoinSilenceMilliseconds
    {
        get => _moodJoinSilenceMilliseconds;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxMoodJoinSilenceMilliseconds);
            _moodJoinSilenceMilliseconds = value;
        }
    }

    /// <summary>
    ///     Gets or sets the reference voices. The first voice is the default. Together with
    ///     <see cref="ReferenceVoiceDirectory"/> at least one voice is needed before the model can speak.
    /// </summary>
    public IReadOnlyList<ChatterboxTurboReferenceVoice> ReferenceVoices { get; set; } = [];

    /// <summary>
    ///     Gets or sets a directory whose <c>.wav</c> and <c>.flac</c> files each become a voice (id = file name without
    ///     extension, ordinal order, after <see cref="ReferenceVoices"/>); <see langword="null"/> for none.
    /// </summary>
    /// <remarks>
    ///     The files are decoded lazily, on first use of each voice, so one damaged file does not fail model creation;
    ///     the failure names the file. Voices passed in <see cref="ReferenceVoices"/> are whatever the caller built
    ///     (<see cref="ChatterboxTurboReferenceVoice.FromFile"/> validates eagerly and throws at that call).
    /// </remarks>
    public string? ReferenceVoiceDirectory { get; set; }

    /// <summary>
    ///     Gets or sets the library that supplies the user-folder and installed-pack voices, or <see langword="null"/>
    ///     to let <c>AddChatterboxTurboModels</c> create one over the catalog's own model store.
    /// </summary>
    /// <remarks>
    ///     The library's voices are listed after <see cref="ReferenceVoices"/> and <see cref="ReferenceVoiceDirectory"/>;
    ///     an id that is already present wins. The list is read when the model is created, so a newly installed pack
    ///     appears once the catalog is rebuilt.
    /// </remarks>
    public ReferenceVoiceLibrary? ReferenceVoiceLibrary { get; set; }

    /// <summary>
    ///     Gets or sets whether voices from <see cref="ReferenceVoiceLibrary"/> (the user folder and installed packs)
    ///     are offered. Defaults to <see langword="true"/>; set to <see langword="false"/> to use only the explicit voices.
    /// </summary>
    public bool IncludeStoreVoices { get; set; } = true;

    /// <summary>
    ///     Gets or sets the sink that receives informational messages while the voice list is resolved (for example when
    ///     a library voice is hidden by an earlier voice with the same id); <see langword="null"/> discards them.
    /// </summary>
    public ISpeechDiagnostics? Diagnostics { get; set; }
}
