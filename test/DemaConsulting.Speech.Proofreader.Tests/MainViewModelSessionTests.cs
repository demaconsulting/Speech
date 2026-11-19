using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Proofreader.Diagnostics;
using DemaConsulting.Speech.Proofreader.Playback;
using DemaConsulting.Speech.Proofreader.Session;
using DemaConsulting.Speech.Proofreader.Synthesis;
using DemaConsulting.Speech.Proofreader.Text;

namespace DemaConsulting.Speech.Proofreader.Tests;

/// <summary>
///     Tests for loading sessions into <see cref="MainViewModel"/>.
/// </summary>
public class MainViewModelSessionTests
{
    /// <summary>
    ///     Validates that a session whose text has no sentences loads cleanly with no current sentence.
    /// </summary>
    /// <param name="text">The session text.</param>
    /// <returns>A task that completes when the check is done.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("   \n\t \n")]
    public async Task MainViewModel_LoadSessionAsync_TextWithoutSentences_HasNoCurrentSentence(string text)
    {
        // Arrange: a session saved for text with no sentences, still pointing at a sentence number
        var directory = Path.Combine(Path.GetTempPath(), "proofreader-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "empty.json");
            SessionFile.Save(
                new ProofreadSession
                {
                    SourceName = "empty.md",
                    SourceSha256 = SessionFile.ComputeHash(text),
                    Text = text,
                    CurrentSentence = 3,
                    Flags = [],
                },
                path);

            using var catalog = ProofreaderCatalog.Create(Path.Combine(directory, "models"), null);
            await using var host = new SynthesisEngineHost(catalog.Catalog);
            using var viewModel = new MainViewModel(
                catalog,
                host,
                new SentenceAudioCache(host),
                new SentencePlayer(new AudioDeviceFactory()),
                new SentenceSplitter(),
                new DiagnosticsBuilder(SentenceSplitter.CreateAnalyzer()));

            // Act
            await viewModel.LoadSessionAsync(path);

            // Assert: loaded without an error, and nothing is selected
            Assert.False(viewModel.StatusIsError, viewModel.StatusMessage);
            Assert.Empty(viewModel.Sentences);
            Assert.Equal(-1, viewModel.CurrentIndex);
            Assert.Equal(string.Empty, viewModel.CurrentSentenceText);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
