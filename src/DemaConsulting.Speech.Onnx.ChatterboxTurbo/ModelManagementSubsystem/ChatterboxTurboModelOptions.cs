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

    /// <summary>The largest default intra-op thread count; more threads do not speed this workload up.</summary>
    private const int DefaultThreadCeiling = 6;

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
    public ChatterboxTurboLanguageModelVariant LanguageModelVariant { get; set; } = ChatterboxTurboLanguageModelVariant.Fp16;

    /// <summary>Gets or sets the auxiliary-graph precision. Defaults to <see cref="ChatterboxTurboAuxiliaryModelVariant.Q4"/>.</summary>
    public ChatterboxTurboAuxiliaryModelVariant AuxiliaryModelVariant { get; set; } = ChatterboxTurboAuxiliaryModelVariant.Q4;

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
    ///     Gets or sets the reference voices. The first voice is the default. Together with
    ///     <see cref="ReferenceVoiceDirectory"/> at least one voice is needed before the model can speak.
    /// </summary>
    public IReadOnlyList<ChatterboxTurboReferenceVoice> ReferenceVoices { get; set; } = [];

    /// <summary>
    ///     Gets or sets a directory whose <c>.wav</c> and <c>.flac</c> files each become a voice (id = file name without
    ///     extension, ordinal order, after <see cref="ReferenceVoices"/>); <see langword="null"/> for none.
    /// </summary>
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
}
