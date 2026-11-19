using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.Speech.Proofreader.Session;
using DemaConsulting.Speech.Proofreader.Text;

namespace DemaConsulting.Speech.Proofreader;

/// <summary>
///     One row of the sentence list.
/// </summary>
/// <remarks>
///     The list selection is the current sentence; the row only adds the number and a marker for
///     sentences that carry at least one flag.
/// </remarks>
internal sealed partial class SentenceItemViewModel : ObservableObject
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SentenceItemViewModel"/> class.
    /// </summary>
    /// <param name="sentence">The sentence the row shows. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sentence"/> is <see langword="null"/>.</exception>
    public SentenceItemViewModel(ProofreadSentence sentence)
    {
        ArgumentNullException.ThrowIfNull(sentence);
        Sentence = sentence;
    }

    /// <summary>Gets the sentence.</summary>
    public ProofreadSentence Sentence { get; }

    /// <summary>Gets the one-based number shown in the list.</summary>
    public string Number => (Sentence.Index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Gets the sentence text with line breaks flattened for the one-line list rows.</summary>
    public string Text => Sentence.Text.ReplaceLineEndings(" ");

    /// <summary>Gets or sets a value indicating whether the sentence carries a flag.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Marker))]
    public partial bool IsFlagged { get; set; }

    /// <summary>Gets the flag marker shown beside a flagged sentence (empty otherwise).</summary>
    public string Marker => IsFlagged ? "\u2691" : string.Empty;
}

/// <summary>
///     One row of the flag list.
/// </summary>
internal sealed partial class FlagItemViewModel : ObservableObject
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FlagItemViewModel"/> class.
    /// </summary>
    /// <param name="flag">The flag the row shows. Must not be <see langword="null"/>.</param>
    /// <param name="isUntrusted">
    ///     Whether the flag's stored sentence no longer matches the loaded text: it is shown with its
    ///     stored text but cannot be jumped to or exported.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="flag"/> is <see langword="null"/>.</exception>
    public FlagItemViewModel(ProofreadFlag flag, bool isUntrusted = false)
    {
        ArgumentNullException.ThrowIfNull(flag);
        Flag = flag;
        IsUntrusted = isUntrusted;
    }

    /// <summary>Gets the flag.</summary>
    public ProofreadFlag Flag { get; }

    /// <summary>Gets a value indicating whether the flag's sentence no longer matches the loaded text.</summary>
    public bool IsUntrusted { get; }

    /// <summary>Gets the first line: flag number, sentence, and category.</summary>
    public string Title => IsUntrusted
        ? $"\u26A0 STALE #{Flag.Id}  was sentence {Flag.SentenceIndex + 1}  [{Flag.Slot}]  {FlagCategories.Label(Flag.Category)}"
        : $"#{Flag.Id}  sentence {Flag.SentenceIndex + 1}  [{Flag.Slot}]  {FlagCategories.Label(Flag.Category)}";

    /// <summary>Gets the second line: the flagged word, or the sentence start when no span was selected.</summary>
    public string Subtitle
    {
        get
        {
            var subject = Flag.HasSpan ? $"\"{Flag.Word}\"" : Flag.SentenceText.ReplaceLineEndings(" ");
            if (IsUntrusted)
            {
                subject = $"Text no longer matches (not jumpable or exported). Stored: {Flag.SentenceText.ReplaceLineEndings(" ")}";
            }

            return string.IsNullOrWhiteSpace(Flag.Note) ? subject : $"{subject} - {Flag.Note}";
        }
    }

    /// <summary>
    ///     Re-reads the flag after it was edited.
    /// </summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
    }
}

/// <summary>
///     One entry of the flag category dropdown.
/// </summary>
/// <param name="Value">The category.</param>
internal sealed record FlagCategoryChoice(FlagCategory Value)
{
    /// <summary>Gets the label shown in the dropdown.</summary>
    public string Label => FlagCategories.Label(Value);
}

/// <summary>
///     One entry of the playback device dropdown.
/// </summary>
/// <param name="Name">The device name, or <see langword="null"/> for the system default.</param>
internal sealed record DeviceChoice(string? Name)
{
    /// <summary>Gets the label shown in the dropdown.</summary>
    public string Label => Name ?? "System default";
}
