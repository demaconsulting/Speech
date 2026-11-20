using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Proofreader.Playback;
using DemaConsulting.Speech.Proofreader.Synthesis;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Proofreader.Tests;

/// <summary>
///     Tests for the playback marks of <see cref="MainViewModel"/>: which audio counts as heard, and how
///     explicit stops, jumps, and selections reset what a flag targets.
/// </summary>
public class MainViewModelPlaybackMarksTests
{
    /// <summary>The text used by the tests: two sentences, the first with a heteronym.</summary>
    private const string TwoSentences = "I will read the book. The second sentence ends here.";

    /// <summary>The marker sample of the first sentence's audio.</summary>
    private const float FirstMarker = 0.1f;

    /// <summary>The marker sample of the second sentence's audio.</summary>
    private const float SecondMarker = 0.2f;

    /// <summary>
    ///     Validates that in A/B mode a flag right after the B rendition begins is for B (the audible
    ///     rendition of that sentence), not for the A rendition that just finished.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_BeginFlag_AbModeBRenditionJustStarted_UsesBSnapshot()
    {
        var time = new ManualTime();
        using var context = new ViewModelContext(null, time);
        var viewModel = context.ViewModel;
        var host = (IPlaybackHost)viewModel;
        await LoadAsync(viewModel);

        // Arrange: sentence 1 sounded through slot A, finished, and its B rendition began 100 ms ago
        await HearAsync(context, 0, "A", "a-model");
        host.OnSentenceFinished(0, "A");
        time.Advance(TimeSpan.FromMilliseconds(300));
        await HearAsync(context, 0, "B", "b-model");
        time.Advance(TimeSpan.FromMilliseconds(100));

        // Act
        viewModel.BeginFlag(0, 0);
        viewModel.SaveFlag();

        // Assert: the flag describes the B rendition, not the A rendition that just ended
        var flag = Assert.Single(viewModel.Flags).Flag;
        Assert.Equal(0, flag.SentenceIndex);
        Assert.Equal("B", flag.Slot);
        Assert.Equal("b-model", flag.Model.ModelId);
    }

    /// <summary>
    ///     Validates that changing the playback device stops playback for good, so a sentence that had just
    ///     finished is no longer what F targets.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_BeginFlag_AfterDeviceChange_IgnoresSentenceThatJustFinished()
    {
        var time = new ManualTime();
        using var context = new ViewModelContext(null, time);
        var viewModel = context.ViewModel;
        var host = (IPlaybackHost)viewModel;
        await LoadAsync(viewModel);
        await HearAsync(context, 0, "A", "first-model");
        host.OnSentenceFinished(0, "A");
        host.OnSentenceStarted(1, "A");
        time.Advance(TimeSpan.FromMilliseconds(200));

        // Act: the device is changed (an explicit stop), then the listener flags
        viewModel.SelectedDevice = new DeviceChoice("another device");
        viewModel.BeginFlag(0, 0);

        // Assert: the flag is for the selected sentence, not the one that finished before the stop
        Assert.Contains("sentence 2 of 2", viewModel.EditorHeader, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that audio that became ready but was stopped, jumped away from, or canceled before it
    ///     was audible never counts as heard.
    /// </summary>
    /// <param name="how">How the audio was dropped before it was heard.</param>
    /// <returns>A task that completes when the check is done.</returns>
    [Theory]
    [InlineData("stop")]
    [InlineData("jump")]
    [InlineData("run-ended")]
    [InlineData("device")]
    public async Task MainViewModel_BeginFlag_AudioDroppedBeforeAudible_IsNotHeard(string how)
    {
        using var context = new ViewModelContext();
        var viewModel = context.ViewModel;
        var host = (IPlaybackHost)viewModel;
        await LoadAsync(viewModel);

        // Arrange: audio for sentence 1 is ready (diagnostics built) but its samples never started
        host.OnSentenceStarted(0, "A");
        host.OnAudioReady(
            0,
            new PlaybackTarget("A", viewModel.SlotA.Models[0].Model, new ModelSelection("never-heard", "Never heard", new Dictionary<string, object>()), null),
            new SentenceAudio([], []));
        await viewModel.DiagnosticsTask;

        // Act
        switch (how)
        {
            case "stop":
                viewModel.Stop();
                break;
            case "jump":
                viewModel.JumpTo(1, play: false);
                viewModel.JumpTo(0, play: false);
                break;
            case "run-ended":
                host.OnStateChanged(PlaybackState.Stopped);
                break;
            default:
                viewModel.SelectedDevice = new DeviceChoice("another device");
                break;
        }

        viewModel.BeginFlag(0, 0);
        viewModel.SaveFlag();

        // Assert: nothing is claimed about audio that was never heard
        var flag = Assert.Single(viewModel.Flags).Flag;
        Assert.NotEqual("never-heard", flag.Model.ModelId);
        Assert.Equal(string.Empty, flag.Readings);
    }

    /// <summary>
    ///     Validates the click-while-paused flow with the real playback controller: the click stops the
    ///     paused run and only selects, nothing plays by itself, F flags the clicked sentence, and Space then
    ///     plays the clicked sentence instead of resuming the old one.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_SelectSentence_WhilePausedMidSentence_SpacePlaysSelectionAndFlagTargetsIt()
    {
        var device = new FakePlaybackDevice();
        using var context = new ViewModelContext(null, null, Synthesize(null), device);
        var viewModel = context.ViewModel;
        await LoadAsync(viewModel);

        // Arrange: sentence 1 is playing, then paused mid-sentence
        viewModel.PlayPause();
        await WaitUntilAsync(() => Marker(device, FirstMarker) && viewModel.State == PlaybackState.Playing);
        viewModel.PlayPause();
        Assert.Equal(PlaybackState.Paused, viewModel.State);
        var writes = device.WriteCount;

        // Act: the listener clicks sentence 2
        viewModel.CurrentIndex = 1;
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Assert: the old run is stopped, nothing started by itself, and F is for the clicked sentence
        Assert.Equal(PlaybackState.Stopped, viewModel.State);
        Assert.Equal(1, viewModel.CurrentIndex);
        Assert.Equal(writes, device.WriteCount);
        viewModel.BeginFlag(0, 0);
        Assert.Contains("sentence 2 of 2", viewModel.EditorHeader, StringComparison.Ordinal);
        viewModel.CancelFlag();

        // Act: Space
        viewModel.PlayPause();

        // Assert: it plays the clicked sentence rather than resuming the paused one
        await WaitUntilAsync(() => Marker(device, SecondMarker));
        Assert.Equal(1, viewModel.CurrentIndex);
    }

    /// <summary>
    ///     Validates that a sentence that was paused before its samples ever started is not recorded as heard,
    ///     and is once playback resumes.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task MainViewModel_BeginFlag_PausedWhileAudioPreparing_IsNotHeardUntilItSounds()
    {
        var device = new FakePlaybackDevice();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var context = new ViewModelContext(null, null, Synthesize(release.Task), device);
        var viewModel = context.ViewModel;
        await LoadAsync(viewModel);

        // Arrange: playback starts, and the listener pauses while sentence 1 is still being synthesized
        viewModel.PlayPause();
        viewModel.PlayPause();
        Assert.Equal(PlaybackState.Paused, viewModel.State);
        var before = viewModel.DiagnosticsTask;
        release.SetResult();
        await WaitUntilAsync(() => !ReferenceEquals(viewModel.DiagnosticsTask, before));
        await viewModel.DiagnosticsTask;
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Act: flag while the prepared audio is waiting behind the pause
        viewModel.BeginFlag(0, 0);
        viewModel.SaveFlag();

        // Assert: nothing sounded, and nothing is claimed as heard
        Assert.Equal(0, device.WriteCount);
        Assert.Equal(string.Empty, viewModel.Flags[0].Flag.Readings);

        // Act: resume
        viewModel.PlayPause();
        await WaitUntilAsync(() => Marker(device, FirstMarker));
        await viewModel.DiagnosticsTask;
        viewModel.BeginFlag(0, 0);
        viewModel.SaveFlag();

        // Assert: now that it sounds, a flag describes the audio that was heard
        Assert.NotEqual(string.Empty, viewModel.Flags[1].Flag.Readings);
    }

    /// <summary>
    ///     Creates a synthesis function that returns marker audio, optionally waiting for a gate first.
    /// </summary>
    /// <param name="gate">A task the first sentence waits for, or <see langword="null"/> for none.</param>
    /// <returns>The synthesis function.</returns>
    private static Func<ISynthesisModel, ModelSelection, string, Task<SentenceAudio>> Synthesize(Task? gate) =>
        async (_, _, text) =>
        {
            if (gate is not null && text.StartsWith("I will", StringComparison.Ordinal))
            {
                await gate;
            }

            var marker = text.StartsWith("I will", StringComparison.Ordinal) ? FirstMarker : SecondMarker;
            return new SentenceAudio([new SynthesizedSpeech(new float[1600].Select(_ => marker).ToArray(), 16000, TimeSpan.Zero, TimeSpan.Zero)], []);
        };

    /// <summary>
    ///     Tests whether the last block written to the device carries a sentence's marker sample.
    /// </summary>
    /// <param name="device">The device.</param>
    /// <param name="marker">The marker.</param>
    /// <returns><see langword="true"/> when the last block is that sentence's audio.</returns>
    private static bool Marker(FakePlaybackDevice device, float marker) => Math.Abs(device.LastWritten - marker) < 0.01f;

    /// <summary>
    ///     Loads the two-sentence text.
    /// </summary>
    /// <param name="viewModel">The view model.</param>
    /// <returns>A task that completes when the text is loaded.</returns>
    private static async Task LoadAsync(MainViewModel viewModel)
    {
        viewModel.SourceText = TwoSentences;
        await viewModel.LoadPastedTextAsync();
    }

    /// <summary>
    ///     Simulates a sentence being heard through a slot: started, its audio ready, and its samples sounding.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="index">The sentence index.</param>
    /// <param name="slot">The slot name.</param>
    /// <param name="modelId">The id of the model the audio was synthesized with.</param>
    /// <returns>A task that completes when the diagnostics of the audio are built.</returns>
    private static async Task HearAsync(ViewModelContext context, int index, string slot, string modelId)
    {
        var viewModel = context.ViewModel;
        var host = (IPlaybackHost)viewModel;
        host.OnSentenceStarted(index, slot);
        host.OnAudioReady(
            index,
            new PlaybackTarget(slot, viewModel.SlotA.Models[0].Model, new ModelSelection(modelId, modelId, new Dictionary<string, object>()), null),
            new SentenceAudio([], []));
        await viewModel.DiagnosticsTask;
        host.OnSentenceAudible(index, slot);
    }

    /// <summary>
    ///     Waits until a condition holds, failing the test after a timeout.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <returns>A task that completes when the condition holds.</returns>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The expected playback state was not reached in time.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
