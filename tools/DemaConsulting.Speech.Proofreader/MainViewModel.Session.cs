using System.Text;
using DemaConsulting.Speech.Proofreader.Session;
using DemaConsulting.Speech.Proofreader.Settings;
using DemaConsulting.Speech.Proofreader.Synthesis;

namespace DemaConsulting.Speech.Proofreader;

/// <summary>
///     Session save and load and the TSV export (see <see cref="MainViewModel"/>).
/// </summary>
internal sealed partial class MainViewModel
{
    /// <summary>Gets or sets the folder the loaded text (or session) came from, or <see langword="null"/> for pasted text.</summary>
    internal string? SourceFolder { get; set; }

    /// <summary>
    ///     Gets the file name the TSV export suggests: <c>&lt;source&gt;-&lt;model&gt;-flags.tsv</c>, where the
    ///     model is the one audible (or <c>A-vs-B</c> models in A/B mode).
    /// </summary>
    internal string SuggestedExportFileName =>
        $"{FileNamePart(Path.GetFileNameWithoutExtension(SourceName))}-{(AbMode ? $"{ShortModelName(SlotA)}-vs-{ShortModelName(SlotB)}" : ShortModelName(SlotNamed(ActiveSlotName)))}-flags.tsv";

    /// <summary>Gets or sets the path of the session file last saved or loaded, or <see langword="null"/>.</summary>
    internal string? SessionPath { get; set; }

    /// <summary>
    ///     Writes the session (text, model settings, device, current sentence, and flags) to a file.
    /// </summary>
    /// <param name="path">The destination file; an existing file is replaced.</param>
    internal void SaveSession(string path)
    {
        try
        {
            SessionFile.Save(BuildSession(), path);
            SessionPath = path;
            SetMessage($"Session saved to {path}.", false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetMessage($"The session could not be saved: {ex.Message}", true);
        }
    }

    /// <summary>
    ///     Loads a session: its text, model settings, device, current sentence, and flags.
    /// </summary>
    /// <param name="path">The session file.</param>
    /// <returns>A task that completes when the session is loaded (or the problem has been reported).</returns>
    internal async Task LoadSessionAsync(string path)
    {
        if (IsBusy)
        {
            return;
        }

        ProofreadSession session;
        try
        {
            session = SessionFile.Load(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            SetMessage($"The session could not be loaded: {ex.Message}", true);
            return;
        }

        _controller.Stop();
        IsBusy = true;
        try
        {
            var sentences = await Task.Run(() => _splitter.Split(session.Text));
            ApplyText(session.Text, sentences, string.IsNullOrEmpty(session.SourceName) ? Path.GetFileName(path) : session.SourceName);
            RestoreSession(session, sentences.Select(sentence => sentence.Text).ToList(), path);
        }
        catch (Exception ex)
        {
            // Intentionally broad: splitting runs on the thread pool and must not leave the window half-loaded.
            SetMessage($"The session could not be loaded: {ex.Message}", true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    ///     Writes the flags as tab-separated text.
    /// </summary>
    /// <param name="path">The destination file; an existing file is replaced.</param>
    internal void ExportTsv(string path)
    {
        try
        {
            var trusted = Flags.Where(item => !item.IsUntrusted).Select(item => item.Flag).ToList();
            File.WriteAllText(path, TsvExporter.Export(trusted), new UTF8Encoding(false));
            var skipped = Flags.Count - trusted.Count;
            SetMessage(
                skipped == 0
                    ? $"Exported {trusted.Count} flag(s) to {path}."
                    : $"Exported {trusted.Count} flag(s) to {path}; {skipped} stale flag(s) that no longer match the text were not exported.",
                skipped > 0);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetMessage($"The export failed: {ex.Message}", true);
        }
    }

    /// <summary>
    ///     Gets the short, file-name-safe name of a slot's model.
    /// </summary>
    /// <param name="slot">The slot.</param>
    /// <returns>The model id, or <c>no-model</c>.</returns>
    private static string ShortModelName(ModelSlotViewModel slot) =>
        slot.SelectedModel is { } choice ? FileNamePart(choice.Descriptor.Id) : "no-model";

    /// <summary>
    ///     Makes text safe as part of a file name: invalid characters and white space become hyphens.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The sanitized text, or <c>text</c> when nothing is left.</returns>
    private static string FileNamePart(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string([.. text.Select(ch => char.IsWhiteSpace(ch) || invalid.Contains(ch) ? '-' : ch)]).Trim('-');
        return cleaned.Length == 0 ? "text" : cleaned;
    }

    /// <summary>
    ///     Captures the current state as a session.
    /// </summary>
    /// <returns>The session.</returns>
    private ProofreadSession BuildSession() => new()
    {
        SourceName = SourceName,
        SourceSha256 = SessionFile.ComputeHash(_plainText),
        Text = _plainText,
        CurrentSentence = Math.Max(CurrentIndex, 0),
        AbMode = AbMode,
        SlotA = SlotA.CurrentSelection is { } a ? SessionModel.From(a) : null,
        SlotB = SlotB.CurrentSelection is { } b ? SessionModel.From(b) : null,
        PlaybackDevice = SelectedDevice?.Name,
        Flags = [.. Flags.Select(item => item.Flag)],
    };

    /// <summary>
    ///     Applies the configuration and flags of a loaded session to the (already split) text.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="sentenceTexts">The texts of the re-split sentences.</param>
    /// <param name="path">The session file path.</param>
    private void RestoreSession(ProofreadSession session, IReadOnlyList<string> sentenceTexts, string path)
    {
        var problems = new List<string>();

        // Models are restored by id; one that this machine does not register is reported, not guessed.
        if (!SlotA.Restore(session.SlotA))
        {
            problems.Add($"model A '{session.SlotA?.DisplayName}' is not registered");
        }

        if (!SlotB.Restore(session.SlotB))
        {
            problems.Add($"model B '{session.SlotB?.DisplayName}' is not registered");
        }

        AbMode = session.AbMode;
        if (session.PlaybackDevice is not null)
        {
            var device = PlaybackDevices.FirstOrDefault(choice => choice.Name == session.PlaybackDevice);
            if (device is null)
            {
                problems.Add($"playback device '{session.PlaybackDevice}' was not found (using the current device)");
            }
            else
            {
                SelectedDevice = device;
            }
        }

        // A flag whose stored sentence no longer equals the re-split sentence is untrusted: it stays
        // in the list (with its stored text, and in the saved session) so nothing is lost silently,
        // but it cannot be jumped to or exported until the user deletes it.
        Flags.Clear();
        var mismatched = session.FindMismatchedFlags(sentenceTexts).ToHashSet();
        foreach (var flag in session.Flags)
        {
            Flags.Add(new FlagItemViewModel(flag, mismatched.Contains(flag)));
        }

        foreach (var flag in mismatched)
        {
            problems.Add($"flag #{flag.Id} no longer matches sentence {flag.SentenceIndex + 1} of the text (marked stale; it is not exported)");
        }

        MarkFlaggedSentences();
        SessionPath = path;
        SourceFolder = Path.GetDirectoryName(Path.GetFullPath(path));
        // A session whose text yields no sentences has no current sentence (-1), not index 0.
        SetCurrent(sentenceTexts.Count == 0 ? -1 : Math.Clamp(session.CurrentSentence, 0, sentenceTexts.Count - 1));
        CurrentSentenceText = CurrentIndex >= 0 ? sentenceTexts[CurrentIndex] : string.Empty;
        RefreshDiagnostics(CurrentIndex, ActiveSlotName, null);

        SetMessage(
            problems.Count == 0
                ? $"Session loaded: {sentenceTexts.Count} sentences, {session.Flags.Count} flag(s)."
                : $"Session loaded with problems: {string.Join("; ", problems)}.",
            problems.Count > 0);
    }
}
