using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.Speech.Proofreader.Session;

namespace DemaConsulting.Speech.Proofreader;

/// <summary>
///     Flag creation, editing, and the flag list (see <see cref="MainViewModel"/>).
/// </summary>
/// <remarks>
///     A flag is raised with one key: playback pauses, the flag editor opens for the current
///     sentence (and the word span the listener selected), and the model, settings, readings, and
///     phonemes that were audible are snapshotted into the flag, so the export never depends on
///     re-synthesis. Saving the editor keeps playback paused; Space resumes.
/// </remarks>
internal sealed partial class MainViewModel
{
    /// <summary>The flag being edited, or <see langword="null"/> while a new flag is being created.</summary>
    private FlagItemViewModel? _editedFlag;

    /// <summary>The span selected when the new flag was started.</summary>
    private (int Start, int Length) _draftSpan;

    /// <summary>Set while the view model selects a flag itself, so it is not mistaken for a click that jumps.</summary>
    private bool _suppressFlagJump;

    /// <summary>Gets the flag categories offered by the editor.</summary>
    public IReadOnlyList<FlagCategoryChoice> FlagCategoryChoices { get; } =
        [.. FlagCategories.All.Select(category => new FlagCategoryChoice(category))];

    /// <summary>Gets the flags raised so far, sorted by sentence and span.</summary>
    public ObservableCollection<FlagItemViewModel> Flags { get; } = [];

    /// <summary>Gets or sets the flag selected in the list; selecting one jumps to its sentence.</summary>
    [ObservableProperty]
    public partial FlagItemViewModel? SelectedFlag { get; set; }

    /// <summary>Gets or sets a value indicating whether the flag editor is open.</summary>
    [ObservableProperty]
    public partial bool IsEditingFlag { get; set; }

    /// <summary>Gets or sets the category chosen in the flag editor.</summary>
    [ObservableProperty]
    public partial FlagCategoryChoice? EditorCategory { get; set; }

    /// <summary>Gets or sets the note typed in the flag editor.</summary>
    [ObservableProperty]
    public partial string EditorNote { get; set; } = string.Empty;

    /// <summary>Gets or sets the heading of the flag editor.</summary>
    [ObservableProperty]
    public partial string EditorHeader { get; set; } = string.Empty;

    /// <summary>
    ///     Starts a flag for the current sentence: pauses playback and opens the editor.
    /// </summary>
    /// <param name="spanStart">The start of the selected word span within the sentence.</param>
    /// <param name="spanLength">The length of the selected span, or 0 to flag the whole sentence.</param>
    internal void BeginFlag(int spanStart, int spanLength)
    {
        if (CurrentIndex < 0 || CurrentIndex >= _sentences.Count)
        {
            SetMessage("Load a text and select a sentence before flagging.", true);
            return;
        }

        _controller.Pause();

        // A span that does not fit the sentence (a stale selection) is dropped rather than trusted.
        var length = _sentences[CurrentIndex].Text.Length;
        _draftSpan = spanLength > 0 && spanStart >= 0 && spanStart + spanLength <= length
            ? (spanStart, spanLength)
            : (0, 0);

        _editedFlag = null;
        EditorCategory ??= FlagCategoryChoices[0];
        EditorNote = string.Empty;
        EditorHeader = _draftSpan.Length > 0
            ? $"New flag: sentence {CurrentIndex + 1}, \"{_sentences[CurrentIndex].Text.Substring(_draftSpan.Start, _draftSpan.Length)}\" (slot {ActiveSlotName})"
            : $"New flag: sentence {CurrentIndex + 1}, whole sentence (slot {ActiveSlotName})";
        IsEditingFlag = true;
    }

    /// <summary>
    ///     Opens the editor for the selected flag.
    /// </summary>
    [RelayCommand]
    internal void EditFlag()
    {
        if (SelectedFlag is not { } item)
        {
            return;
        }

        _editedFlag = item;
        EditorCategory = FlagCategoryChoices.First(choice => choice.Value == item.Flag.Category);
        EditorNote = item.Flag.Note;
        EditorHeader = $"Edit flag #{item.Flag.Id} (sentence {item.Flag.SentenceIndex + 1})";
        IsEditingFlag = true;
    }

    /// <summary>
    ///     Saves the flag editor: adds a new flag or updates the edited one.
    /// </summary>
    [RelayCommand]
    internal void SaveFlag()
    {
        if (!IsEditingFlag || EditorCategory is null)
        {
            return;
        }

        if (_editedFlag is { } edited)
        {
            edited.Flag.Category = EditorCategory.Value;
            edited.Flag.Note = EditorNote.Trim();
            edited.Refresh();
        }
        else
        {
            AddFlag(CreateFlag());
        }

        IsEditingFlag = false;
        _editedFlag = null;
        SetMessage("Flag saved. Press Space to resume.", false);
    }

    /// <summary>
    ///     Closes the flag editor without saving.
    /// </summary>
    [RelayCommand]
    internal void CancelFlag()
    {
        IsEditingFlag = false;
        _editedFlag = null;
    }

    /// <summary>
    ///     Deletes the selected flag.
    /// </summary>
    [RelayCommand]
    internal void DeleteFlag()
    {
        if (SelectedFlag is not { } item)
        {
            return;
        }

        var index = Flags.IndexOf(item);
        Flags.Remove(item);
        SelectFlagWithoutJump(Flags.Count > 0 ? Flags[Math.Min(index, Flags.Count - 1)] : null);
        MarkFlaggedSentences();
    }

    /// <summary>
    ///     Jumps to the sentence of the flag the user selected in the list.
    /// </summary>
    /// <param name="value">The newly selected flag.</param>
    partial void OnSelectedFlagChanged(FlagItemViewModel? value)
    {
        if (value is not null && !_suppressFlagJump)
        {
            JumpTo(value.Flag.SentenceIndex, play: false);
        }
    }

    /// <summary>
    ///     Builds a flag for the current sentence, snapshotting what was audible.
    /// </summary>
    /// <returns>The new flag, not yet numbered or added.</returns>
    private ProofreadFlag CreateFlag()
    {
        var sentence = _sentences[CurrentIndex];
        var selection = SlotNamed(ActiveSlotName).CurrentSelection;

        // The diagnostics are used only when they belong to the sentence being flagged.
        var diagnostics = _shownDiagnostics is { } shown && shown.Index == CurrentIndex ? shown.Diagnostics : null;

        return new ProofreadFlag
        {
            SentenceIndex = sentence.Index,
            SentenceText = sentence.Text,
            SpanStart = _draftSpan.Start,
            SpanLength = _draftSpan.Length,
            Word = _draftSpan.Length > 0 ? sentence.Text.Substring(_draftSpan.Start, _draftSpan.Length) : string.Empty,
            Category = EditorCategory!.Value,
            Note = EditorNote.Trim(),
            CreatedUtc = DateTimeOffset.UtcNow,
            Slot = ActiveSlotName,
            Model = selection is null ? new SessionModel { ModelId = "(none)" } : SessionModel.From(selection),
            Readings = diagnostics?.ReadingsFor(_draftSpan.Start, _draftSpan.Length) ?? string.Empty,
            Phonemes = diagnostics?.Phonemes.Summary ?? string.Empty,
        };
    }

    /// <summary>
    ///     Numbers a flag, inserts it in sentence order, and selects it.
    /// </summary>
    /// <param name="flag">The flag to add.</param>
    private void AddFlag(ProofreadFlag flag)
    {
        flag.Id = Flags.Count == 0 ? 1 : Flags.Max(item => item.Flag.Id) + 1;
        var item = new FlagItemViewModel(flag);

        var position = 0;
        while (position < Flags.Count
            && (Flags[position].Flag.SentenceIndex, Flags[position].Flag.SpanStart).CompareTo((flag.SentenceIndex, flag.SpanStart)) <= 0)
        {
            position++;
        }

        Flags.Insert(position, item);
        MarkFlaggedSentences();
        SelectFlagWithoutJump(item);
    }

    /// <summary>
    ///     Selects a flag in the list without the jump a user's click would perform.
    /// </summary>
    /// <param name="item">The flag to select, or <see langword="null"/> for none.</param>
    private void SelectFlagWithoutJump(FlagItemViewModel? item)
    {
        _suppressFlagJump = true;
        try
        {
            SelectedFlag = item;
        }
        finally
        {
            _suppressFlagJump = false;
        }
    }

    /// <summary>
    ///     Refreshes the flag marker of every sentence row.
    /// </summary>
    private void MarkFlaggedSentences()
    {
        var flagged = Flags.Select(item => item.Flag.SentenceIndex).ToHashSet();
        foreach (var row in Sentences)
        {
            row.IsFlagged = flagged.Contains(row.Sentence.Index);
        }
    }
}
