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
///     phonemes that were audible are snapshotted into a draft when the flag is raised, so the export
///     never depends on re-synthesis and navigating while the editor is open does not change what is
///     saved. Saving the editor keeps playback paused; Space resumes.
/// </remarks>
internal sealed partial class MainViewModel
{
    /// <summary>The flag being edited, or <see langword="null"/> while a new flag is being created.</summary>
    private FlagItemViewModel? _editedFlag;

    /// <summary>The snapshot taken when a new flag was started, or <see langword="null"/> when none is being created.</summary>
    private FlagDraft? _draft;

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

        var index = CurrentIndex;
        var sentence = _sentences[index];

        // A span that does not fit the sentence (a stale selection) is dropped rather than trusted.
        var (start, length) = spanLength > 0 && spanStart >= 0 && spanStart + spanLength <= sentence.Text.Length
            ? (spanStart, spanLength)
            : (0, 0);

        var slot = ActiveSlotName;
        var selection = SlotNamed(slot).CurrentSelection;

        // The diagnostics are used only when they belong to the sentence being flagged.
        var diagnostics = _shownDiagnostics is { } shown && shown.Index == index ? shown.Diagnostics : null;

        _draft = new FlagDraft(
            index,
            sentence.Text,
            start,
            length,
            slot,
            selection is null ? new SessionModel { ModelId = "(none)" } : SessionModel.From(selection),
            diagnostics?.ReadingsFor(start, length) ?? string.Empty,
            diagnostics?.Phonemes.Summary ?? string.Empty);

        _editedFlag = null;
        EditorCategory ??= FlagCategoryChoices[0];
        EditorNote = string.Empty;
        EditorHeader = length > 0
            ? $"New flag: sentence {index + 1}, \"{sentence.Text.Substring(start, length)}\" (slot {slot})"
            : $"New flag: sentence {index + 1}, whole sentence (slot {slot})";
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
            if (_draft is { } draft)
            {
                AddFlag(CreateFlag(draft));
            }
        }

        IsEditingFlag = false;
        _editedFlag = null;
        _draft = null;
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
        _draft = null;
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

        // A flag being edited must not be saved into after it has been removed.
        if (ReferenceEquals(item, _editedFlag))
        {
            CancelFlag();
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
    ///     Builds a flag from the draft taken when the flag was raised.
    /// </summary>
    /// <param name="draft">The snapshot taken by <see cref="BeginFlag"/>.</param>
    /// <returns>The new flag, not yet numbered or added.</returns>
    private ProofreadFlag CreateFlag(FlagDraft draft) =>
        new()
        {
            SentenceIndex = draft.Index,
            SentenceText = draft.SentenceText,
            SpanStart = draft.SpanStart,
            SpanLength = draft.SpanLength,
            Word = draft.SpanLength > 0 ? draft.SentenceText.Substring(draft.SpanStart, draft.SpanLength) : string.Empty,
            Category = EditorCategory!.Value,
            Note = EditorNote.Trim(),
            CreatedUtc = DateTimeOffset.UtcNow,
            Slot = draft.Slot,
            Model = draft.Model,
            Readings = draft.Readings,
            Phonemes = draft.Phonemes,
        };

    /// <summary>
    ///     Everything a new flag needs, captured when the flag is raised.
    /// </summary>
    /// <param name="Index">The sentence index.</param>
    /// <param name="SentenceText">The sentence text.</param>
    /// <param name="SpanStart">The start of the flagged span, sentence-relative.</param>
    /// <param name="SpanLength">The length of the flagged span, or 0 for the whole sentence.</param>
    /// <param name="Slot">The name of the slot that was audible.</param>
    /// <param name="Model">The model and settings that were audible.</param>
    /// <param name="Readings">The word readings for the span.</param>
    /// <param name="Phonemes">The phoneme summary of the sentence.</param>
    private sealed record FlagDraft(
        int Index,
        string SentenceText,
        int SpanStart,
        int SpanLength,
        string Slot,
        SessionModel Model,
        string Readings,
        string Phonemes);

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
