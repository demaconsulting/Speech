using DemaConsulting.Speech.Proofreader.Synthesis;

namespace DemaConsulting.Speech.Proofreader.Session;

/// <summary>
///     The serializable form of a <see cref="ModelSelection"/>: a model id with its parameter values.
/// </summary>
/// <remarks>
///     Parameter values are string, double, or bool. They round-trip through JSON as the matching
///     JSON kinds and are converted back to those CLR types by <see cref="SessionFile"/> on load.
/// </remarks>
internal sealed class SessionModel
{
    /// <summary>Gets or sets the catalog id of the model.</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>Gets or sets the model's display name when the session was written.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets the parameter values the model was loaded with.</summary>
    public Dictionary<string, object> Parameters { get; set; } = [];

    /// <summary>
    ///     Captures a selection.
    /// </summary>
    /// <param name="selection">The selection to capture. Must not be <see langword="null"/>.</param>
    /// <returns>The serializable copy.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="selection"/> is <see langword="null"/>.</exception>
    public static SessionModel From(ModelSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return new SessionModel
        {
            ModelId = selection.ModelId,
            DisplayName = selection.DisplayName,
            Parameters = new Dictionary<string, object>(selection.Parameters, StringComparer.Ordinal),
        };
    }

    /// <summary>
    ///     Converts back to a selection.
    /// </summary>
    /// <returns>The selection.</returns>
    public ModelSelection ToSelection() => new(ModelId, DisplayName, Parameters);
}

/// <summary>
///     One issue a listener flagged in a sentence.
/// </summary>
/// <remarks>
///     Everything the TSV export needs - the readings and phonemes the model consumed - is
///     snapshotted when the flag is created, so the export never depends on re-synthesis or on the
///     model still being installed.
/// </remarks>
internal sealed class ProofreadFlag
{
    /// <summary>Gets or sets the flag number, unique within the session.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the zero-based index of the flagged sentence.</summary>
    public int SentenceIndex { get; set; }

    /// <summary>Gets or sets the flagged sentence's text.</summary>
    public string SentenceText { get; set; } = string.Empty;

    /// <summary>Gets or sets the start of the flagged word span, relative to the sentence.</summary>
    public int SpanStart { get; set; }

    /// <summary>Gets or sets the length of the flagged span, or 0 when the whole sentence is flagged.</summary>
    public int SpanLength { get; set; }

    /// <summary>Gets or sets the flagged word or phrase (empty when no span was selected).</summary>
    public string Word { get; set; } = string.Empty;

    /// <summary>Gets or sets the problem category.</summary>
    public FlagCategory Category { get; set; }

    /// <summary>Gets or sets the listener's free-text note.</summary>
    public string Note { get; set; } = string.Empty;

    /// <summary>Gets or sets when the flag was created.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>Gets or sets the slot (A or B) that was audible when the flag was raised.</summary>
    public string Slot { get; set; } = "A";

    /// <summary>Gets or sets the model and settings that were audible.</summary>
    public SessionModel Model { get; set; } = new();

    /// <summary>Gets or sets the readings (<c>word=reading(confidence)</c> pairs) of the flagged span.</summary>
    public string Readings { get; set; } = string.Empty;

    /// <summary>Gets or sets the phoneme string(s) the model consumed for the sentence.</summary>
    public string Phonemes { get; set; } = string.Empty;

    /// <summary>Gets a value indicating whether a word span (rather than the whole sentence) was flagged.</summary>
    public bool HasSpan => SpanLength > 0;
}

/// <summary>
///     A proofreading session: the text, the playback configuration, and the flags raised so far.
/// </summary>
internal sealed class ProofreadSession
{
    /// <summary>The session file format version this tool reads and writes.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Gets or sets the session file format version.</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Gets or sets the name of the text source (usually a file name), for display.</summary>
    public string SourceName { get; set; } = string.Empty;

    /// <summary>Gets or sets the SHA-256 (lower-case hex) of <see cref="Text"/> in UTF-8.</summary>
    public string SourceSha256 { get; set; } = string.Empty;

    /// <summary>Gets or sets the plain text that was proofread (already stripped of Markdown).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets the index of the sentence that was current when the session was saved.</summary>
    public int CurrentSentence { get; set; }

    /// <summary>Gets or sets a value indicating whether A/B mode was on.</summary>
    public bool AbMode { get; set; }

    /// <summary>Gets or sets model A and its settings, or <see langword="null"/> when none was selected.</summary>
    public SessionModel? SlotA { get; set; }

    /// <summary>Gets or sets model B and its settings, or <see langword="null"/> when none was selected.</summary>
    public SessionModel? SlotB { get; set; }

    /// <summary>Gets or sets the playback device name, or <see langword="null"/> for the system default.</summary>
    public string? PlaybackDevice { get; set; }

    /// <summary>Gets or sets the flags raised so far.</summary>
    public List<ProofreadFlag> Flags { get; set; } = [];

    /// <summary>
    ///     Finds the flags that no longer match the sentences of the current text.
    /// </summary>
    /// <remarks>
    ///     A loaded flag must never be silently attached to a different sentence, so the caller
    ///     reports these instead of trusting the stored index.
    /// </remarks>
    /// <param name="sentences">The sentence texts of the re-split text, by index.</param>
    /// <returns>The flags whose index is out of range or whose stored sentence text differs.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sentences"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<ProofreadFlag> FindMismatchedFlags(IReadOnlyList<string> sentences)
    {
        ArgumentNullException.ThrowIfNull(sentences);

        return Flags
            .Where(flag => flag.SentenceIndex < 0
                || flag.SentenceIndex >= sentences.Count
                || !string.Equals(sentences[flag.SentenceIndex], flag.SentenceText, StringComparison.Ordinal))
            .ToList();
    }
}
