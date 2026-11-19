using DemaConsulting.Speech.Proofreader.Session;

namespace DemaConsulting.Speech.Proofreader.Tests;

/// <summary>
///     Tests for <see cref="SessionFile"/>: the session JSON must round-trip every field and reject files it cannot trust.
/// </summary>
public class SessionFileTests
{
    /// <summary>
    ///     Builds a session that exercises every field, including all three parameter value kinds.
    /// </summary>
    /// <returns>The session.</returns>
    private static ProofreadSession CreateSession()
    {
        const string text = "First sentence. Second sentence with a record.";
        var model = new SessionModel
        {
            ModelId = "kokoro-onnx-v1_0-en",
            DisplayName = "Kokoro v1.0",
            Parameters = new Dictionary<string, object> { ["voice"] = "af_heart", ["speaker"] = 7.0, ["flag"] = true },
        };

        return new ProofreadSession
        {
            SourceName = "chapter1.md",
            SourceSha256 = SessionFile.ComputeHash(text),
            Text = text,
            CurrentSentence = 1,
            AbMode = true,
            SlotA = model,
            SlotB = new SessionModel { ModelId = "piper", DisplayName = "Piper", Parameters = new Dictionary<string, object> { ["speaker"] = 12.5 } },
            PlaybackDevice = "Speakers (Test)",
            Flags =
            [
                new ProofreadFlag
                {
                    Id = 3,
                    SentenceIndex = 1,
                    SentenceText = "Second sentence with a record.",
                    SpanStart = 24,
                    SpanLength = 6,
                    Word = "record",
                    Category = FlagCategory.WrongHeteronym,
                    Note = "Should be the noun reading.",
                    CreatedUtc = new DateTimeOffset(2026, 11, 18, 20, 0, 0, TimeSpan.Zero),
                    Slot = "B",
                    Model = model,
                    Readings = "record=record.noun(0.91)",
                    Phonemes = "first phonemes | second phonemes",
                },
            ],
        };
    }

    /// <summary>
    ///     Validates that saving and loading reproduces the text, the hash, the typed slot settings, and every flag field.
    /// </summary>
    [Fact]
    public void SessionFile_RoundTrip_PreservesEveryField()
    {
        // Arrange: a session with string, double, and bool parameter values and one flag
        var original = CreateSession();

        // Act: serialize and parse again
        var loaded = SessionFile.FromJson(SessionFile.ToJson(original));

        // Assert: nothing was lost or retyped
        Assert.Multiple(
            () => Assert.Equal(original.Text, loaded.Text),
            () => Assert.Equal(original.SourceSha256, loaded.SourceSha256),
            () => Assert.Equal(original.SourceName, loaded.SourceName),
            () => Assert.Equal(1, loaded.CurrentSentence),
            () => Assert.True(loaded.AbMode),
            () => Assert.Equal("Speakers (Test)", loaded.PlaybackDevice),
            () => Assert.Equal("af_heart", Assert.IsType<string>(loaded.SlotA!.Parameters["voice"])),
            () => Assert.Equal(7.0, Assert.IsType<double>(loaded.SlotA!.Parameters["speaker"])),
            () => Assert.True(Assert.IsType<bool>(loaded.SlotA!.Parameters["flag"])),
            () => Assert.Equal(12.5, Assert.IsType<double>(loaded.SlotB!.Parameters["speaker"])));

        var flag = Assert.Single(loaded.Flags);
        var expected = original.Flags[0];
        Assert.Multiple(
            () => Assert.Equal(expected.Id, flag.Id),
            () => Assert.Equal(expected.SentenceIndex, flag.SentenceIndex),
            () => Assert.Equal(expected.SentenceText, flag.SentenceText),
            () => Assert.Equal(expected.SpanStart, flag.SpanStart),
            () => Assert.Equal(expected.SpanLength, flag.SpanLength),
            () => Assert.Equal(expected.Word, flag.Word),
            () => Assert.Equal(expected.Category, flag.Category),
            () => Assert.Equal(expected.Note, flag.Note),
            () => Assert.Equal(expected.CreatedUtc, flag.CreatedUtc),
            () => Assert.Equal(expected.Slot, flag.Slot),
            () => Assert.Equal("kokoro-onnx-v1_0-en", flag.Model.ModelId),
            () => Assert.True(Assert.IsType<bool>(flag.Model.Parameters["flag"])),
            () => Assert.Equal(expected.Readings, flag.Readings),
            () => Assert.Equal(expected.Phonemes, flag.Phonemes));
    }

    /// <summary>
    ///     Validates that unsupported versions, malformed JSON, incomplete sessions (JSON null), and a text that does not match its hash are rejected with a clear error.
    /// </summary>
    [Fact]
    public void SessionFile_FromJson_UntrustworthyFile_ThrowsInvalidDataException()
    {
        // Arrange: a future-version file, garbage, and a session whose text was edited after saving
        var futureVersion = SessionFile.ToJson(CreateSession()).Replace("\"version\": 1", "\"version\": 2", StringComparison.Ordinal);
        var edited = CreateSession();
        edited.Text += " Extra.";
        var noModel = CreateSession();
        noModel.Flags[0].Model = null!;
        var noParameters = CreateSession();
        noParameters.Flags[0].Model.Parameters = null!;

        // Act / Assert: each is rejected as invalid data, not as an arbitrary exception
        Assert.Contains("version", Assert.Throws<InvalidDataException>(() => SessionFile.FromJson(futureVersion)).Message, StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => SessionFile.FromJson("{ not json"));
        Assert.Contains("SHA-256", Assert.Throws<InvalidDataException>(() => SessionFile.FromJson(SessionFile.ToJson(edited))).Message, StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => SessionFile.FromJson("{ \"version\": 1, \"text\": null }"));
        Assert.Throws<InvalidDataException>(() => SessionFile.FromJson("{ \"version\": 1, \"text\": \"a\", \"sourceSha256\": \"x\", \"flags\": null }"));
        Assert.Contains("Flag 1", Assert.Throws<InvalidDataException>(() => SessionFile.FromJson(SessionFile.ToJson(noModel))).Message, StringComparison.Ordinal);
        Assert.Contains("incomplete", Assert.Throws<InvalidDataException>(() => SessionFile.FromJson(SessionFile.ToJson(noParameters))).Message, StringComparison.Ordinal);
    }
}
