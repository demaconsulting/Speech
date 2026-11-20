using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Proofreader.Diagnostics;
using DemaConsulting.Speech.Proofreader.Playback;
using DemaConsulting.Speech.Proofreader.Session;
using DemaConsulting.Speech.Proofreader.Synthesis;
using DemaConsulting.Speech.Proofreader.Text;

namespace DemaConsulting.Speech.Proofreader.Tests;

/// <summary>
///     Tests for the flag snapshot and the stale-flag handling of <see cref="MainViewModel"/>.
/// </summary>
public class MainViewModelFlagTests
{
    /// <summary>The text used by the tests: two sentences, the first with a heteronym.</summary>
    private const string TwoSentences = "I will read the book. The second sentence ends here.";

    /// <summary>
    ///     Validates that a flag raised after the voice was changed carries the model, settings, slot, and
    ///     diagnostics that were heard, not the slot's current controls.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_BeginFlag_AfterSelectionChanged_UsesHeardSnapshot()
    {
        using var context = new ViewModelContext();
        var viewModel = context.ViewModel;
        viewModel.SourceText = TwoSentences;
        await viewModel.LoadPastedTextAsync();

        // Arrange: sentence 1 was heard through a model and settings that differ from the slot's controls
        var model = viewModel.SlotA.Models[0].Model;
        var heardSelection = new ModelSelection(
            "heard-model",
            "Heard model",
            new Dictionary<string, object> { ["speed"] = 1.5 });
        var audio = new SentenceAudio([], []);
        ((IPlaybackHost)viewModel).OnSentenceStarted(0, "B");
        ((IPlaybackHost)viewModel).OnAudioReady(0, new PlaybackTarget("B", model, heardSelection, null), audio);
        await viewModel.DiagnosticsTask;
        ((IPlaybackHost)viewModel).OnSentenceAudible(0, "B");

        // Act: the user then changes slot A's model and the active slot, and flags
        viewModel.SlotA.SelectedModel = viewModel.SlotA.Models[^1];
        viewModel.ActiveSlotName = "A";
        viewModel.BeginFlag(0, 0);
        viewModel.SaveFlag();

        // Assert: the flag describes the heard configuration and its matching diagnostics
        var expected = context.Diagnostics.Build(model, viewModel.Sentences[0].Sentence.Text, audio);
        var flag = Assert.Single(viewModel.Flags).Flag;
        Assert.Equal("B", flag.Slot);
        Assert.Equal("heard-model", flag.Model.ModelId);
        Assert.Equal(1.5, Assert.IsType<double>(flag.Model.Parameters["speed"]));
        Assert.Equal(0, flag.SentenceIndex);
        Assert.Equal(expected.ReadingsFor(0, 0), flag.Readings);
        Assert.Equal(expected.Phonemes.Summary, flag.Phonemes);
    }

    /// <summary>
    ///     Validates that a sentence that was never heard is flagged with the current model and no
    ///     diagnostics from another sentence.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_BeginFlag_SentenceNotHeard_HasNoStaleDiagnostics()
    {
        using var context = new ViewModelContext();
        var viewModel = context.ViewModel;
        viewModel.SourceText = TwoSentences;
        await viewModel.LoadPastedTextAsync();

        // Arrange: only sentence 1 was heard; the user then selects sentence 2
        var model = viewModel.SlotA.Models[0].Model;
        var heardSelection = new ModelSelection("heard-model", "Heard model", new Dictionary<string, object>());
        ((IPlaybackHost)viewModel).OnAudioReady(0, new PlaybackTarget("A", model, heardSelection, null), new SentenceAudio([], []));
        await viewModel.DiagnosticsTask;
        ((IPlaybackHost)viewModel).OnSentenceAudible(0, "A");
        viewModel.JumpTo(1, play: false);

        // Act
        viewModel.BeginFlag(0, 0);
        viewModel.SaveFlag();

        // Assert: nothing heard for sentence 2 is claimed
        var flag = Assert.Single(viewModel.Flags).Flag;
        Assert.Equal(1, flag.SentenceIndex);
        Assert.NotEqual("heard-model", flag.Model.ModelId);
        Assert.Equal(string.Empty, flag.Readings);
        Assert.Equal(string.Empty, flag.Phonemes);
    }

    /// <summary>
    ///     Validates that a loaded flag whose sentence no longer matches is untrusted: listed, but neither
    ///     jumpable nor exported.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_LoadSessionAsync_MismatchedFlag_IsUntrustedAndNotExported()
    {
        using var context = new ViewModelContext();
        var viewModel = context.ViewModel;
        var sessionPath = Path.Combine(context.Directory, "stale.json");
        SessionFile.Save(
            new ProofreadSession
            {
                SourceName = "stale.txt",
                SourceSha256 = SessionFile.ComputeHash(TwoSentences),
                Text = TwoSentences,
                CurrentSentence = 0,
                Flags =
                [
                    new ProofreadFlag { Id = 1, SentenceIndex = 1, SentenceText = "The second sentence ends here." },
                    new ProofreadFlag { Id = 2, SentenceIndex = 0, SentenceText = "An older sentence that was edited." },
                ],
            },
            sessionPath);

        // Act
        await viewModel.LoadSessionAsync(sessionPath);

        // Assert: both are listed, only the mismatched one is untrusted
        Assert.Equal(2, viewModel.Flags.Count);
        Assert.False(viewModel.Flags[0].IsUntrusted);
        var stale = viewModel.Flags[1];
        Assert.True(stale.IsUntrusted);
        Assert.Contains("STALE", stale.Title, StringComparison.Ordinal);
        Assert.Contains("An older sentence that was edited.", stale.Subtitle, StringComparison.Ordinal);
        Assert.False(viewModel.Sentences[0].IsFlagged);
        Assert.True(viewModel.Sentences[1].IsFlagged);

        // A trusted flag jumps; the untrusted one does not
        viewModel.SelectedFlag = viewModel.Flags[0];
        Assert.Equal(1, viewModel.CurrentIndex);
        viewModel.SelectedFlag = null;
        viewModel.SelectedFlag = stale;
        Assert.Equal(1, viewModel.CurrentIndex);
        Assert.True(viewModel.StatusIsError);

        // The export leaves the untrusted flag out and says so
        var tsvPath = Path.Combine(context.Directory, "flags.tsv");
        viewModel.ExportTsv(tsvPath);
        var rows = await File.ReadAllLinesAsync(tsvPath, TestContext.Current.CancellationToken);
        Assert.Equal(2, rows.Length);
        Assert.Contains("The second sentence ends here.", rows[1], StringComparison.Ordinal);
        Assert.DoesNotContain("older sentence", string.Join('\n', rows), StringComparison.Ordinal);
        Assert.Contains("1 stale flag", viewModel.StatusMessage, StringComparison.Ordinal);

        // The stale flag is kept when the session is saved again, so nothing is lost
        var secondSave = Path.Combine(context.Directory, "second-save.json");
        viewModel.SaveSession(secondSave);
        Assert.Equal(2, SessionFile.Load(secondSave).Flags.Count);

        // Deleting it resolves the problem
        viewModel.DeleteFlag();
        Assert.Single(viewModel.Flags);
    }

    /// <summary>
    ///     Validates that flagging the previous sentence uses the snapshot of what was heard for that
    ///     sentence, not the one of the sentence that is playing now or the slot's current controls.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_BeginFlagPrevious_AfterPlaybackAdvanced_UsesHeardSnapshotOfPreviousSentence()
    {
        using var context = new ViewModelContext();
        var viewModel = context.ViewModel;
        var host = (IPlaybackHost)viewModel;
        viewModel.SourceText = TwoSentences;
        await viewModel.LoadPastedTextAsync();

        // Arrange: sentence 1 was heard through "first-model" on slot B, then playback advanced to
        // sentence 2, which was heard through a different model
        var model = viewModel.SlotA.Models[0].Model;
        var firstSelection = new ModelSelection(
            "first-model",
            "First model",
            new Dictionary<string, object> { ["speed"] = 1.25 });
        var secondSelection = new ModelSelection("second-model", "Second model", new Dictionary<string, object>());
        var firstAudio = new SentenceAudio([], []);
        host.OnSentenceStarted(0, "B");
        host.OnAudioReady(0, new PlaybackTarget("B", model, firstSelection, null), firstAudio);
        await viewModel.DiagnosticsTask;
        host.OnSentenceAudible(0, "B");
        host.OnSentenceFinished(0, "B");
        host.OnSentenceStarted(1, "A");
        host.OnAudioReady(1, new PlaybackTarget("A", model, secondSelection, null), new SentenceAudio([], []));
        await viewModel.DiagnosticsTask;
        host.OnSentenceAudible(1, "A");
        Assert.Equal(1, viewModel.CurrentIndex);

        // Act
        viewModel.BeginFlagPrevious();
        Assert.True(viewModel.IsEditingFlag);
        viewModel.SaveFlag();

        // Assert: the flag is for sentence 1 and describes what was heard for it
        var expected = context.Diagnostics.Build(model, viewModel.Sentences[0].Sentence.Text, firstAudio);
        var flag = Assert.Single(viewModel.Flags).Flag;
        Assert.Equal(0, flag.SentenceIndex);
        Assert.Equal("B", flag.Slot);
        Assert.Equal("first-model", flag.Model.ModelId);
        Assert.Equal(1.25, Assert.IsType<double>(flag.Model.Parameters["speed"]));
        Assert.Equal(expected.ReadingsFor(0, 0), flag.Readings);
        Assert.Equal(expected.Phonemes.Summary, flag.Phonemes);

        // The current sentence is not moved by flagging the previous one
        Assert.Equal(1, viewModel.CurrentIndex);
    }

    /// <summary>
    ///     Validates that there is nothing to flag before the first sentence.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_BeginFlagPrevious_OnFirstSentence_OpensNoEditor()
    {
        using var context = new ViewModelContext();
        var viewModel = context.ViewModel;
        viewModel.SourceText = TwoSentences;
        await viewModel.LoadPastedTextAsync();

        // Act
        viewModel.BeginFlagPrevious();

        // Assert
        Assert.False(viewModel.IsEditingFlag);
        Assert.True(viewModel.StatusIsError);
        Assert.Empty(viewModel.Flags);
    }

    /// <summary>
    ///     Validates that clicking a sentence while playback is paused only selects it and that Space
    ///     then plays from the selection.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_SelectSentence_WhilePaused_SelectsWithoutPlaying()
    {
        using var context = new ViewModelContext();
        var viewModel = context.ViewModel;
        viewModel.SourceText = TwoSentences;
        await viewModel.LoadPastedTextAsync();

        // Arrange: playback is paused on sentence 1 (the state is reported as the controller would)
        ((IPlaybackHost)viewModel).OnStateChanged(PlaybackState.Paused);
        var states = RecordStates(viewModel);

        // Act: the user clicks sentence 2
        viewModel.CurrentIndex = 1;

        // Assert: it is selected, nothing started, and nothing was reported as a playback problem
        Assert.Equal(1, viewModel.CurrentIndex);
        Assert.DoesNotContain(PlaybackState.Loading, states);
        Assert.DoesNotContain(PlaybackState.Playing, states);
        Assert.False(viewModel.StatusIsError);

        // Act: Space plays from the selected sentence
        viewModel.PlayPause();

        // Assert: playback was started
        Assert.Contains(PlaybackState.Loading, states);
    }

    /// <summary>
    ///     Validates that clicking a sentence while playback is stopped only selects it.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_SelectSentence_WhileStopped_SelectsWithoutPlaying()
    {
        using var context = new ViewModelContext();
        var viewModel = context.ViewModel;
        viewModel.SourceText = TwoSentences;
        await viewModel.LoadPastedTextAsync();
        var states = RecordStates(viewModel);

        // Act
        viewModel.CurrentIndex = 1;
        viewModel.Next();

        // Assert
        Assert.Equal(1, viewModel.CurrentIndex);
        Assert.Empty(states);
        Assert.False(viewModel.StatusIsError);
    }

    /// <summary>
    ///     Validates that clicking a sentence while playback is running jumps there and keeps playing.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_SelectSentence_WhilePlaying_ContinuesPlayingFromThere()
    {
        using var context = new ViewModelContext();
        var viewModel = context.ViewModel;
        viewModel.SourceText = TwoSentences;
        await viewModel.LoadPastedTextAsync();

        // Arrange: playback is running on sentence 1
        ((IPlaybackHost)viewModel).OnStateChanged(PlaybackState.Playing);
        var states = RecordStates(viewModel);

        // Act: the user clicks sentence 2
        viewModel.CurrentIndex = 1;

        // Assert: a new run started there
        Assert.Contains(PlaybackState.Loading, states);
        Assert.Equal(1, viewModel.CurrentIndex);
    }

    /// <summary>
    ///     Records every playback state the view model reports from now on.
    /// </summary>
    /// <param name="viewModel">The view model.</param>
    /// <returns>The queue the states are appended to.</returns>
    private static System.Collections.Concurrent.ConcurrentQueue<PlaybackState> RecordStates(MainViewModel viewModel)
    {
        // A queue, because a failed synthesis reports the stop from a thread-pool thread.
        var states = new System.Collections.Concurrent.ConcurrentQueue<PlaybackState>();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.State))
            {
                states.Enqueue(viewModel.State);
            }
        };

        return states;
    }
}
