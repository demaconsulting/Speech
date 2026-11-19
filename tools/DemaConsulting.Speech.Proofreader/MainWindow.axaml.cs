using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace DemaConsulting.Speech.Proofreader;

/// <summary>
///     The main window: layout, window-level keyboard shortcuts, file pickers, and the word-span selection hand-off.
/// </summary>
/// <remarks>
///     Everything the window does beyond layout is delegated to <see cref="MainViewModel"/>; this class
///     only owns what needs a view: keys, pickers, and focus. Shortcuts are handled on the tunnel
///     route so they work wherever focus is, except where the focused control needs the key itself
///     (typing in a text box, choosing in a dropdown, moving a slider).
/// </remarks>
internal sealed partial class MainWindow : Window
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>Gets the window's view model.</summary>
    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    /// <summary>
    ///     Follows the flag editor so focus lands in the note box when it opens and returns to the list when it closes.
    /// </summary>
    /// <param name="sender">The window.</param>
    /// <param name="e">The event data.</param>
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    /// <summary>
    ///     Moves focus when the flag editor opens or closes.
    /// </summary>
    /// <param name="sender">The view model.</param>
    /// <param name="e">The changed property.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsEditingFlag))
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                if (ViewModel.IsEditingFlag)
                {
                    FlagNoteBox.Focus();
                }
                else
                {
                    SentenceList.Focus();
                }
            },
            DispatcherPriority.Background);
    }

    /// <summary>
    ///     Handles the window-level keyboard shortcuts.
    /// </summary>
    /// <param name="sender">The window.</param>
    /// <param name="e">The key event.</param>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        // Escape cancels the flag editor, or stops playback, and always returns focus to the list.
        if (e.Key == Key.Escape)
        {
            if (ViewModel.IsEditingFlag)
            {
                ViewModel.CancelFlag();
            }
            else
            {
                ViewModel.Stop();
            }

            SentenceList.Focus();
            e.Handled = true;
            return;
        }

        // Control shortcuts work everywhere, even while typing.
        if (control)
        {
            e.Handled = HandleControlKey(e.Key, shift);
            return;
        }

        // While a new flag is being created, the arrow keys move its target sentence.
        if (ViewModel.IsCreatingFlag
            && e.KeyModifiers == KeyModifiers.None
            && e.Key is Key.Left or Key.Right or Key.Up or Key.Down
            && CanRedirectFromFocus(e.Key))
        {
            ViewModel.RedirectFlag(e.Key is Key.Left or Key.Up ? -1 : 1);
            e.Handled = true;
            return;
        }

        // Plain keys are left to the focused control when it needs them.
        if (IsFocusedControlConsumingKeys() || e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift))
        {
            return;
        }

        e.Handled = true;
        switch (e.Key)
        {
            case Key.Space:
                ViewModel.PlayPause();
                break;
            case Key.F:
                if (shift)
                {
                    ViewModel.BeginFlagPrevious();
                }
                else
                {
                    BeginFlagFromSelection();
                }

                break;
            case Key.Left or Key.Up or Key.K:
                ViewModel.Previous();
                break;
            case Key.Right or Key.Down or Key.J:
                ViewModel.Next();
                break;
            case Key.R:
                ViewModel.Replay();
                break;
            case Key.A:
                ViewModel.ToggleAb();
                break;
            case Key.F2:
                ViewModel.EditFlag();
                break;
            case Key.Delete:
                ViewModel.DeleteFlag();
                break;
            default:
                e.Handled = false;
                break;
        }
    }

    /// <summary>
    ///     Handles a Control+key shortcut.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="shift">Whether Shift is also held.</param>
    /// <returns><see langword="true"/> when the key was a shortcut.</returns>
    private bool HandleControlKey(Key key, bool shift)
    {
        switch (key)
        {
            case Key.O:
                _ = shift ? OpenSessionAsync() : OpenTextAsync();
                return true;
            case Key.S:
                _ = SaveSessionAsync(saveAs: shift);
                return true;
            case Key.E:
                _ = ExportTsvAsync();
                return true;
            case Key.Enter:
                _ = ViewModel.LoadPastedTextAsync();
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    ///     Tests whether the focused control needs plain keys for itself.
    /// </summary>
    /// <returns><see langword="true"/> for text boxes, dropdowns, and sliders.</returns>
    private bool IsFocusedControlConsumingKeys() =>
        TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement()
            is TextBox or ComboBox or ComboBoxItem or NumericUpDown or ButtonSpinner or Slider or TabItem;

    /// <summary>
    ///     Tests whether an arrow key may move the flag target, rather than belong to the focused control.
    /// </summary>
    /// <param name="key">The arrow key.</param>
    /// <returns>
    ///     <see langword="true"/> unless a dropdown, slider, or number box has focus, or (for Left and Right)
    ///     a note that has text to move through.
    /// </returns>
    private bool CanRedirectFromFocus(Key key) =>
        TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() switch
        {
            ComboBox or ComboBoxItem or NumericUpDown or ButtonSpinner or Slider or TabItem => false,
            TextBox box => key is Key.Up or Key.Down || string.IsNullOrEmpty(box.Text),
            _ => true,
        };

    /// <summary>
    ///     Starts a flag for the word span selected in the current-sentence block (or the whole sentence).
    /// </summary>
    private void BeginFlagFromSelection()
    {
        var start = Math.Min(CurrentSentenceBlock.SelectionStart, CurrentSentenceBlock.SelectionEnd);
        var end = Math.Max(CurrentSentenceBlock.SelectionStart, CurrentSentenceBlock.SelectionEnd);
        ViewModel.BeginFlag(start, end - start);
    }

    /// <summary>
    ///     Handles the Flag button.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The event data.</param>
    private void OnFlagClick(object? sender, RoutedEventArgs e) => BeginFlagFromSelection();

    /// <summary>
    ///     Handles the Open text button.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The event data.</param>
    private async void OnOpenTextClick(object? sender, RoutedEventArgs e) => await OpenTextAsync();

    /// <summary>
    ///     Handles the Open session button.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The event data.</param>
    private async void OnOpenSessionClick(object? sender, RoutedEventArgs e) => await OpenSessionAsync();

    /// <summary>
    ///     Handles the Save session button.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The event data.</param>
    private async void OnSaveSessionClick(object? sender, RoutedEventArgs e) => await SaveSessionAsync(saveAs: false);

    /// <summary>
    ///     Handles the Export TSV button.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The event data.</param>
    private async void OnExportTsvClick(object? sender, RoutedEventArgs e) => await ExportTsvAsync();

    /// <summary>
    ///     Asks for a text or Markdown file and loads it.
    /// </summary>
    /// <returns>A task that completes when the file has been loaded or the dialog canceled.</returns>
    private async Task OpenTextAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open text or Markdown",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Text and Markdown") { Patterns = ["*.txt", "*.md", "*.markdown"] },
                FilePickerFileTypes.All,
            ],
        });

        if (files.Count == 1 && files[0].TryGetLocalPath() is { } path)
        {
            await ViewModel.LoadFileAsync(path);
        }
    }

    /// <summary>
    ///     Asks for a session file and loads it.
    /// </summary>
    /// <returns>A task that completes when the session has been loaded or the dialog canceled.</returns>
    private async Task OpenSessionAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open proofreading session",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Session (JSON)") { Patterns = ["*.json"] }, FilePickerFileTypes.All],
        });

        if (files.Count == 1 && files[0].TryGetLocalPath() is { } path)
        {
            await ViewModel.LoadSessionAsync(path);
        }
    }

    /// <summary>
    ///     Saves the session, asking for a file when there is none yet or Save As was requested.
    /// </summary>
    /// <param name="saveAs">Whether to always ask for a file.</param>
    /// <returns>A task that completes when the session has been saved or the dialog canceled.</returns>
    private async Task SaveSessionAsync(bool saveAs)
    {
        var path = ViewModel.SessionPath;
        if (saveAs || path is null)
        {
            path = await PickSavePathAsync("Save proofreading session", "session.json", "json", "Session (JSON)");
        }

        if (path is not null)
        {
            ViewModel.SaveSession(path);
        }
    }

    /// <summary>
    ///     Asks for a file and exports the flags to it as TSV.
    /// </summary>
    /// <returns>A task that completes when the flags have been exported or the dialog canceled.</returns>
    private async Task ExportTsvAsync()
    {
        var path = await PickSavePathAsync("Export flags as TSV", ViewModel.SuggestedExportFileName, "tsv", "Tab-separated values", ViewModel.SourceFolder);
        if (path is not null)
        {
            ViewModel.ExportTsv(path);
        }
    }

    /// <summary>
    ///     Shows a save dialog.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="suggestedName">The suggested file name.</param>
    /// <param name="extension">The default extension without a dot.</param>
    /// <param name="typeName">The file type description.</param>
    /// <param name="folder">The folder the dialog starts in, or <see langword="null"/> for the platform default.</param>
    /// <returns>The chosen local path, or <see langword="null"/> when the dialog was canceled.</returns>
    private async Task<string?> PickSavePathAsync(string title, string suggestedName, string extension, string typeName, string? folder = null)
    {
        var startFolder = folder is null ? null : await StorageProvider.TryGetFolderFromPathAsync(folder);
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = [new FilePickerFileType(typeName) { Patterns = [$"*.{extension}"] }],
            SuggestedStartLocation = startFolder,
        });

        return file?.TryGetLocalPath();
    }
}
