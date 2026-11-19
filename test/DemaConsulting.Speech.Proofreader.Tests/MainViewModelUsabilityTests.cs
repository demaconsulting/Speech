using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;
using DemaConsulting.Speech.Proofreader.Playback;
using DemaConsulting.Speech.Proofreader.Session;
using DemaConsulting.Speech.Proofreader.Settings;
using DemaConsulting.Speech.Proofreader.Synthesis;

namespace DemaConsulting.Speech.Proofreader.Tests;

/// <summary>
///     Tests for which sentence a flag targets, redirecting, flag category defaults, remembered
///     settings, model labels, and the suggested export name of <see cref="MainViewModel"/>.
/// </summary>
public class MainViewModelUsabilityTests
{
    /// <summary>The text used by the tests: two sentences.</summary>
    private const string TwoSentences = "I will read the book. The second sentence ends here.";

    /// <summary>
    ///     Validates that F pressed just after a sentence ended, while the next one is still loading,
    ///     flags the sentence that was just heard, with the audio that was heard for it.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_BeginFlag_NextSentenceLoadingAfterFinish_TargetsSentenceJustHeard()
    {
        var (context, viewModel, time) = await StartAsync();
        using var disposable = context;

        // Arrange: sentence 1 was heard and finished 300 ms ago; playback moved on to sentence 2
        await HearAsync(context, 0, "first-model");
        ((IPlaybackHost)viewModel).OnSentenceFinished(0, "A");
        time.Advance(TimeSpan.FromMilliseconds(300));
        ((IPlaybackHost)viewModel).OnSentenceStarted(1, "A");
        Assert.Equal(1, viewModel.CurrentIndex);

        // Act
        viewModel.BeginFlag(0, 0);
        viewModel.SaveFlag();

        // Assert: the flag is for sentence 1 and describes what was heard for it
        var flag = Assert.Single(viewModel.Flags).Flag;
        Assert.Equal(0, flag.SentenceIndex);
        Assert.Equal("first-model", flag.Model.ModelId);
    }

    /// <summary>
    ///     Validates that F pressed the instant the next sentence began still flags the one that ended,
    ///     but once the next sentence has been sounding for a while it flags that one.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_BeginFlag_NextSentenceAudible_TargetsPreviousOnlyWhileFresh()
    {
        var (context, viewModel, time) = await StartAsync();
        using var disposable = context;
        var host = (IPlaybackHost)viewModel;
        await HearAsync(context, 0, "first-model");
        host.OnSentenceFinished(0, "A");
        time.Advance(TimeSpan.FromMilliseconds(100));
        await HearAsync(context, 1, "second-model");

        // Act / Assert: 200 ms into sentence 2, the listener is still reacting to sentence 1
        time.Advance(TimeSpan.FromMilliseconds(200));
        viewModel.BeginFlag(0, 0);
        Assert.Equal(0, FlagTargetOf(viewModel));
        viewModel.CancelFlag();

        // Act / Assert: a second later, sentence 2 has been heard for a while and is the target
        time.Advance(TimeSpan.FromMilliseconds(900));
        viewModel.BeginFlag(0, 0);
        Assert.Equal(1, FlagTargetOf(viewModel));
    }

    /// <summary>
    ///     Validates that a flag long after a sentence ended, or after the user moved the selection, is for the selected sentence.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_BeginFlag_FinishedLongAgoOrUserMoved_TargetsCurrentSentence()
    {
        var (context, viewModel, time) = await StartAsync();
        using var disposable = context;
        var host = (IPlaybackHost)viewModel;
        await HearAsync(context, 0, "first-model");
        host.OnSentenceFinished(0, "A");
        host.OnSentenceStarted(1, "A");

        // Act / Assert: 3 s later the listener means the current sentence
        time.Advance(TimeSpan.FromSeconds(3));
        viewModel.BeginFlag(0, 0);
        Assert.Equal(1, FlagTargetOf(viewModel));
        viewModel.CancelFlag();

        // Act / Assert: an explicit selection right after a finish overrides the reaction window
        viewModel.JumpTo(0, play: false);
        host.OnSentenceFinished(0, "A");
        viewModel.JumpTo(1, play: false);
        viewModel.BeginFlag(0, 0);
        Assert.Equal(1, FlagTargetOf(viewModel));
    }

    /// <summary>
    ///     Validates that a pause keeps the sentence that was interrupted as the flag target, however long the pause.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_BeginFlag_PausedMidSentence_TargetsInterruptedSentence()
    {
        var (context, viewModel, time) = await StartAsync();
        using var disposable = context;
        await HearAsync(context, 0, "first-model");
        ((IPlaybackHost)viewModel).OnStateChanged(PlaybackState.Paused);
        time.Advance(TimeSpan.FromMinutes(1));

        // Act
        viewModel.BeginFlag(0, 0);

        // Assert
        Assert.Equal(0, FlagTargetOf(viewModel));
        Assert.Equal(0, viewModel.CurrentIndex);
    }

    /// <summary>
    ///     Validates that a word span selected in the current sentence is dropped when the flag targets another sentence.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_BeginFlag_TargetsPreviousSentence_DropsSelectedSpan()
    {
        var (context, viewModel, _) = await StartAsync();
        using var disposable = context;
        var host = (IPlaybackHost)viewModel;
        await HearAsync(context, 0, "first-model");
        host.OnSentenceFinished(0, "A");
        host.OnSentenceStarted(1, "A");

        // Act: a span was selected in the (current) sentence 2
        viewModel.BeginFlag(4, 6);
        viewModel.SaveFlag();

        // Assert: the flag is whole-sentence, for sentence 1
        var flag = Assert.Single(viewModel.Flags).Flag;
        Assert.Equal(0, flag.SentenceIndex);
        Assert.False(flag.HasSpan);
    }

    /// <summary>
    ///     Validates that, while a new flag is open, the target can be moved to another sentence and then
    ///     uses that sentence's heard audio, keeping the note and category already entered.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_RedirectFlag_WhileCreating_MovesTargetAndKeepsInput()
    {
        var (context, viewModel, _) = await StartAsync();
        using var disposable = context;
        await HearAsync(context, 0, "first-model");
        await HearAsync(context, 1, "second-model");
        viewModel.BeginFlag(0, 0);
        Assert.Contains("sentence 2 of 2", viewModel.EditorHeader, StringComparison.Ordinal);
        viewModel.EditorNote = "typed already";
        viewModel.EditorCategory = viewModel.FlagCategoryChoices.First(choice => choice.Value == FlagCategory.Normalization);

        // Act: move to the previous sentence, then try to move past the first
        viewModel.RedirectFlag(-1);
        viewModel.RedirectFlag(-1);

        // Assert: the editor describes sentence 1 and kept the input
        Assert.Contains("sentence 1 of 2", viewModel.EditorHeader, StringComparison.Ordinal);
        Assert.Equal("I will read the book.", viewModel.EditorTargetText);
        Assert.Equal("typed already", viewModel.EditorNote);
        viewModel.SaveFlag();
        var flag = Assert.Single(viewModel.Flags).Flag;
        Assert.Equal(0, flag.SentenceIndex);
        Assert.Equal("first-model", flag.Model.ModelId);
        Assert.Equal(FlagCategory.Normalization, flag.Category);
    }

    /// <summary>
    ///     Validates that the target of an existing flag being edited cannot be moved.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_RedirectFlag_WhileEditingExistingFlag_DoesNothing()
    {
        var (context, viewModel, _) = await StartAsync();
        using var disposable = context;
        viewModel.BeginFlag(0, 0);
        viewModel.SaveFlag();
        viewModel.EditFlag();
        Assert.False(viewModel.IsCreatingFlag);
        var header = viewModel.EditorHeader;

        // Act
        viewModel.RedirectFlag(1);

        // Assert
        Assert.Equal(header, viewModel.EditorHeader);
    }

    /// <summary>
    ///     Validates the flag category defaults: a selected word is a mispronunciation, otherwise the last used category.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_BeginFlag_Category_DefaultsToMispronouncedForSpanElseLastUsed()
    {
        var (context, viewModel, _) = await StartAsync();
        using var disposable = context;

        // Act / Assert: a word span defaults to a mispronounced word
        viewModel.BeginFlag(2, 4);
        Assert.Equal(FlagCategory.MispronouncedWord, viewModel.EditorCategory!.Value);
        viewModel.CancelFlag();

        // Act / Assert: a whole-sentence flag with no history starts at the first category
        viewModel.BeginFlag(0, 0);
        Assert.Equal(FlagCategory.WrongHeteronym, viewModel.EditorCategory.Value);

        // Act / Assert: after saving with another category, the next whole-sentence flag starts there
        viewModel.EditorCategory = viewModel.FlagCategoryChoices.First(choice => choice.Value == FlagCategory.ProsodyOrPause);
        viewModel.SaveFlag();
        viewModel.BeginFlag(0, 0);
        Assert.Equal(FlagCategory.ProsodyOrPause, viewModel.EditorCategory.Value);
    }

    /// <summary>
    ///     Validates that the models, their settings, A/B mode, and the last flag category are remembered across launches.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_Settings_AreRememberedAcrossLaunches()
    {
        var settingsDir = Path.Combine(Path.GetTempPath(), "proofreader-settings-" + Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(settingsDir, "nested", "settings.json");
        try
        {
            string slotAId;
            string slotBId;

            // Arrange: a first launch changes both models, turns A/B on, and saves a flag
            using (var first = new ViewModelContext(settingsPath))
            {
                var viewModel = first.ViewModel;
                viewModel.SlotA.SelectedModel = viewModel.SlotA.Models[^1];
                viewModel.SlotB.SelectedModel = viewModel.SlotB.Models[0];
                viewModel.AbMode = true;
                slotAId = viewModel.SlotA.SelectedModel.Descriptor.Id;
                slotBId = viewModel.SlotB.SelectedModel.Descriptor.Id;
                viewModel.SourceText = TwoSentences;
                await viewModel.LoadPastedTextAsync();
                viewModel.BeginFlag(0, 0);
                viewModel.EditorCategory = viewModel.FlagCategoryChoices.First(choice => choice.Value == FlagCategory.VoiceGlitch);
                viewModel.SaveFlag();
            }

            // Act: a second launch reads the same settings file
            using var second = new ViewModelContext(settingsPath);

            // Assert
            Assert.Equal(slotAId, second.ViewModel.SlotA.SelectedModel!.Descriptor.Id);
            Assert.Equal(slotBId, second.ViewModel.SlotB.SelectedModel!.Descriptor.Id);
            Assert.True(second.ViewModel.AbMode);
            second.ViewModel.SourceText = TwoSentences;
            await second.ViewModel.LoadPastedTextAsync();
            second.ViewModel.BeginFlag(0, 0);
            Assert.Equal(FlagCategory.VoiceGlitch, second.ViewModel.EditorCategory!.Value);
        }
        finally
        {
            if (Directory.Exists(settingsDir))
            {
                Directory.Delete(settingsDir, recursive: true);
            }
        }
    }

    /// <summary>
    ///     Validates that a damaged settings file is ignored rather than breaking start-up.
    /// </summary>
    [Fact]
    public void MainViewModel_Settings_DamagedFile_StartsWithDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "proofreader-bad-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, "{ this is not json");
        try
        {
            // Act
            using var context = new ViewModelContext(path);

            // Assert
            Assert.NotNull(context.ViewModel.SlotA.SelectedModel);
            Assert.False(context.ViewModel.AbMode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that slot A starts on the ONNX Piper model and slot B on the ONNX Kokoro model when they are installed,
    ///     and that an installed model is used in place of one that is not.
    /// </summary>
    [Fact]
    public void ModelSlotViewModel_SetModels_PrefersOnnxPiperForAndOnnxKokoroForB_WhenInstalled()
    {
        using var context = new ViewModelContext();
        var models = context.ViewModel.SlotA.Models.Select(choice => choice.Descriptor.Model).ToList();

        // Arrange: everything is installed
        var allInstalled = models.Select(model => new SpeechModelDescriptor(model, SpeechModelState.Downloaded)).ToList();
        var slotA = new ModelSlotViewModel("A");
        var slotB = new ModelSlotViewModel("B");

        // Act
        slotA.SetModels(allInstalled, 0, OnnxPiperLibriTtsEnSynthesisModel.ModelId);
        slotB.SetModels(allInstalled, 1, OnnxKokoroEnglishSynthesisModel.ModelId);

        // Assert
        Assert.Equal(OnnxPiperLibriTtsEnSynthesisModel.ModelId, slotA.SelectedModel!.Descriptor.Id);
        Assert.Equal(OnnxKokoroEnglishSynthesisModel.ModelId, slotB.SelectedModel!.Descriptor.Id);

        // Arrange: the Piper ONNX model is not installed
        var withoutPiper = models
            .Select(model => new SpeechModelDescriptor(
                model,
                model.Id == OnnxPiperLibriTtsEnSynthesisModel.ModelId ? SpeechModelState.NotDownloaded : SpeechModelState.Downloaded))
            .ToList();
        var fallback = new ModelSlotViewModel("A");

        // Act
        fallback.SetModels(withoutPiper, 0, OnnxPiperLibriTtsEnSynthesisModel.ModelId);

        // Assert: an installed model is chosen instead
        Assert.True(fallback.SelectedModel!.IsInstalled);
        Assert.NotEqual(OnnxPiperLibriTtsEnSynthesisModel.ModelId, fallback.SelectedModel.Descriptor.Id);
    }

    /// <summary>
    ///     Validates that every model is labeled with the package that implements it, and that dropdown
    ///     entries present their label as their text (the value assistive technology reads).
    /// </summary>
    [Fact]
    public void ModelChoice_Label_PrefixesEngineAndIsTheText()
    {
        using var context = new ViewModelContext();
        var choices = context.ViewModel.SlotA.Models;

        // Assert: Sherpa and ONNX models are told apart, and each entry's text is its label
        Assert.Contains(choices, choice => choice.Label.StartsWith("[Sherpa] ", StringComparison.Ordinal));
        Assert.Contains(choices, choice => choice.Label.StartsWith("[ONNX] ", StringComparison.Ordinal));
        Assert.All(choices, choice => Assert.Equal(choice.Label, choice.ToString()));
        Assert.DoesNotContain(choices, choice => choice.ToString().Contains("Descriptor =", StringComparison.Ordinal));

        var piper = choices.Single(choice => choice.Descriptor.Id == OnnxPiperLibriTtsEnSynthesisModel.ModelId);
        Assert.StartsWith("[ONNX] Piper", piper.Label, StringComparison.Ordinal);
        Assert.Equal("Voice One", new OptionChoice(new ChoiceParameterOption("v", "Voice One")).ToString());
        Assert.Equal("System default", new DeviceChoice(null).ToString());
        Assert.Equal("Wrong heteronym", new FlagCategoryChoice(FlagCategory.WrongHeteronym).ToString());
    }

    /// <summary>
    ///     Validates the suggested export name: source file, model, and the folder of the source.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_SuggestedExportFileName_UsesSourceAndModel()
    {
        using var context = new ViewModelContext();
        var viewModel = context.ViewModel;

        // Arrange: a text file in a known folder
        var textPath = Path.Combine(context.Directory, "Chapter 1.txt");
        await File.WriteAllTextAsync(textPath, TwoSentences, TestContext.Current.CancellationToken);

        // Act
        await viewModel.LoadFileAsync(textPath);

        // Assert
        var model = viewModel.SlotA.SelectedModel!.Descriptor.Id;
        Assert.Equal($"Chapter-1-{model}-flags.tsv", viewModel.SuggestedExportFileName);
        Assert.Equal(context.Directory, viewModel.SourceFolder);

        // Act / Assert: A/B mode names both models
        viewModel.AbMode = true;
        Assert.Contains("-vs-", viewModel.SuggestedExportFileName, StringComparison.Ordinal);

        // Act / Assert: pasted text has no folder
        viewModel.SourceText = TwoSentences;
        await viewModel.LoadPastedTextAsync();
        Assert.StartsWith("Pasted-text-", viewModel.SuggestedExportFileName, StringComparison.Ordinal);
        Assert.Null(viewModel.SourceFolder);
    }

    /// <summary>
    ///     Validates that the token table shows a part of speech for tokens whose analysis carries none of its own.
    /// </summary>
    [Fact]
    public void DiagnosticsBuilder_Build_ModelAnalyzerWithoutTags_StillShowsPartsOfSpeech()
    {
        using var context = new ViewModelContext();
        var model = context.ViewModel.SlotA.Models[0].Model;

        // Act
        var diagnostics = context.Diagnostics.Build(model, "The dog barked.", null);

        // Assert
        Assert.Matches(@"dog\s+NOUN", diagnostics.FormatAnalysis());
    }

    /// <summary>
    ///     Starts a view model with a two-sentence text and a controllable clock.
    /// </summary>
    /// <returns>The context (to dispose), the view model, and the clock.</returns>
    private static async Task<(ViewModelContext Context, MainViewModel ViewModel, ManualTime Time)> StartAsync()
    {
        var time = new ManualTime();
        var context = new ViewModelContext(null, time);
        context.ViewModel.SourceText = TwoSentences;
        await context.ViewModel.LoadPastedTextAsync();
        return (context, context.ViewModel, time);
    }

    /// <summary>
    ///     Simulates the playback controller hearing a sentence: started, synthesized by a model, and sounding.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="index">The sentence index.</param>
    /// <param name="modelId">The id of the model the audio was synthesized with.</param>
    /// <returns>A task that completes when the diagnostics of the audio are built.</returns>
    private static async Task HearAsync(ViewModelContext context, int index, string modelId)
    {
        var viewModel = context.ViewModel;
        var host = (IPlaybackHost)viewModel;
        var model = viewModel.SlotA.Models[0].Model;
        host.OnSentenceStarted(index, "A");
        host.OnAudioReady(
            index,
            new PlaybackTarget("A", model, new ModelSelection(modelId, modelId, new Dictionary<string, object>()), null),
            new SentenceAudio([], []));
        await viewModel.DiagnosticsTask;
        host.OnSentenceAudible(index, "A");
    }

    /// <summary>
    ///     Gets the sentence index the open flag editor targets, read from its header.
    /// </summary>
    /// <param name="viewModel">The view model with an open new-flag editor.</param>
    /// <returns>The zero-based sentence index.</returns>
    private static int FlagTargetOf(MainViewModel viewModel)
    {
        Assert.True(viewModel.IsEditingFlag);
        var match = System.Text.RegularExpressions.Regex.Match(viewModel.EditorHeader, @"sentence (\d+) of");
        Assert.True(match.Success);
        return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) - 1;
    }

    /// <summary>
    ///     A clock that only moves when told to.
    /// </summary>
    private sealed class ManualTime : TimeProvider
    {
        /// <summary>The current time.</summary>
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => _now;

        /// <summary>
        ///     Moves the clock forward.
        /// </summary>
        /// <param name="by">How far.</param>
        public void Advance(TimeSpan by) => _now += by;
    }
}
