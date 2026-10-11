using DemaConsulting.Speech.Onnx.NemotronStt.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.ModelManagementSubsystem;

/// <summary>Unit tests for <see cref="NemotronVocabulary"/>.</summary>
public sealed class NemotronVocabularyTests
{
    /// <summary>A small vocabulary with the real file's structure: unk, locale tokens, words, blank last.</summary>
    private static readonly string[] Tokens =
    [
        "<unk>", "<de-DE>", "<en-GB>", "<en-US>", "\u2581hello", "\u2581wor", "ld", ".", "<blank>",
    ];

    /// <summary>Proves language ids are the ordinal among unk-plus-locale tokens.</summary>
    [Fact]
    public void TryGetLanguageId_KnownLocales_ReturnsOrdinal()
    {
        var vocabulary = new NemotronVocabulary(Tokens);

        Assert.True(vocabulary.TryGetLanguageId("de-DE", out var de));
        Assert.True(vocabulary.TryGetLanguageId("en-GB", out var gb));
        Assert.True(vocabulary.TryGetLanguageId("en-US", out var us));
        Assert.Equal(1, de);
        Assert.Equal(2, gb);
        Assert.Equal(3, us);
    }

    /// <summary>Proves an unknown locale is reported as missing.</summary>
    [Fact]
    public void TryGetLanguageId_UnknownLocale_ReturnsFalse()
    {
        var vocabulary = new NemotronVocabulary(Tokens);

        Assert.False(vocabulary.TryGetLanguageId("xx-XX", out _));
    }

    /// <summary>Proves the blank id is the index of the blank token.</summary>
    [Fact]
    public void BlankId_IsBlankTokenIndex()
    {
        var vocabulary = new NemotronVocabulary(Tokens);

        Assert.Equal(8, vocabulary.BlankId);
        Assert.Equal(9, vocabulary.Count);
    }

    /// <summary>Proves the blank id defaults to the last token when no blank token exists.</summary>
    [Fact]
    public void BlankId_NoBlankToken_IsLastIndex()
    {
        var vocabulary = new NemotronVocabulary(["a", "b", "c"]);

        Assert.Equal(2, vocabulary.BlankId);
    }

    /// <summary>Proves detokenization maps word markers to spaces, strips locale tokens, and normalizes whitespace.</summary>
    [Fact]
    public void Detokenize_Tokens_NormalizesText()
    {
        var vocabulary = new NemotronVocabulary(Tokens);

        var text = vocabulary.Detokenize([3, 4, 5, 6, 7, 3, 4, 99]);

        Assert.Equal("hello world. hello", text);
    }

    /// <summary>Proves an empty token list detokenizes to an empty string.</summary>
    [Fact]
    public void Detokenize_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, new NemotronVocabulary(Tokens).Detokenize([]));
    }

    /// <summary>Proves an empty vocabulary is rejected.</summary>
    [Fact]
    public void Constructor_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => new NemotronVocabulary([]));
    }

    /// <summary>Proves Load reads one token per line from a file.</summary>
    [Fact]
    public void Load_File_ReadsTokens()
    {
        var path = Path.Join(Path.GetTempPath(), $"nemotron-vocab-{Guid.NewGuid():N}.txt");
        File.WriteAllLines(path, Tokens);
        try
        {
            var vocabulary = NemotronVocabulary.Load(path);

            Assert.Equal(Tokens.Length, vocabulary.Count);
            Assert.True(vocabulary.TryGetLanguageId("en-US", out var id));
            Assert.Equal(3, id);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
