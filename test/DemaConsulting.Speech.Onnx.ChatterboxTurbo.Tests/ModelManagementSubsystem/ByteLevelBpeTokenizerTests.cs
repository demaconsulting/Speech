using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for <see cref="ByteLevelBpeTokenizer"/>, anchored on golden ids generated from the reference
///     Hugging Face tokenizer by <c>TestData/generate_golden_ids.py</c>.
/// </summary>
public sealed class ByteLevelBpeTokenizerTests
{
    /// <summary>The SHA-256 of the original <c>tokenizer.json</c> that the embedded resource decompresses to.</summary>
    private const string TokenizerSha256 = "3f04e34bea22f9144d1a19151154095bc9ce0430bf421304f5797e716288a906";

    /// <summary>The embedded tokenizer, loaded once per test class instance.</summary>
    private readonly ByteLevelBpeTokenizer _tokenizer = OnnxChatterboxTurboEnglishSynthesisModel.LoadTokenizer();

    /// <summary>Gets the golden cases (text and reference ids) read from the golden JSON test data as xunit theory data.</summary>
    /// <returns>The theory data, one row per golden text.</returns>
    public static TheoryData<string, long[]> GoldenCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "chatterbox-turbo-tokenizer-golden.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var data = new TheoryData<string, long[]>();
        foreach (var entry in document.RootElement.EnumerateArray())
        {
            data.Add(
                entry.GetProperty("text").GetString()!,
                [.. entry.GetProperty("ids").EnumerateArray().Select(id => id.GetInt64())]);
        }

        return data;
    }

    /// <summary>Proves every golden text encodes to exactly the reference tokenizer's ids.</summary>
    /// <param name="text">The text.</param>
    /// <param name="expected">The reference ids.</param>
    [Theory]
    [MemberData(nameof(GoldenCases))]
    public void ByteLevelBpeTokenizer_Encode_GoldenText_MatchesReferenceIds(string text, long[] expected)
    {
        // Arrange: the tokenizer is built by the fixture

        // Act
        var ids = _tokenizer.Encode(text);

        // Assert
        Assert.Equal(expected, ids);
    }

    /// <summary>Proves the golden data is non-trivial, so the theory above cannot pass vacuously.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_GoldenCases_ContainManyCases()
    {
        // Arrange
        var cases = GoldenCases();

        // Act
        var count = cases.Count;

        // Assert
        Assert.True(count >= 30);
    }

    /// <summary>Proves the post-processor appends the end-of-text id twice, even for empty text.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_Encode_EmptyText_ReturnsOnlyTrailingPair()
    {
        // Arrange: the tokenizer is built by the fixture

        // Act
        var ids = _tokenizer.Encode(string.Empty);

        // Assert
        Assert.Equal([50256L, 50256L], ids);
    }

    /// <summary>Proves bracketed paralinguistic tags are single atomic ids, with a space before one tokenizing as 220 first.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_Encode_Tag_IsAtomicAndSpaceIsSeparate()
    {
        // Arrange: the tokenizer is built by the fixture

        // Act
        var ids = _tokenizer.Encode("so [laugh] funny");

        // Assert
        Assert.Contains(50275L, ids);
        var index = Array.IndexOf(ids, 50275L);
        Assert.Equal(220L, ids[index - 1]);
    }

    /// <summary>Proves the trailing end-of-text count is configurable (zero disables it).</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_FromGzipStream_ZeroTrailing_AddsNoEndOfText()
    {
        // Arrange
        using var stream = OnnxChatterboxTurboEnglishSynthesisModel.OpenTokenizerResource();
        var tokenizer = ByteLevelBpeTokenizer.FromGzipStream(stream, trailingEndOfTextCount: 0);

        // Act
        var ids = tokenizer.Encode("Hello world.");

        // Assert
        Assert.Equal([15496L, 995L, 13L], ids);
    }

    /// <summary>Proves the embedded resource decompresses to the pinned upstream tokenizer document.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_EmbeddedResource_MatchesPinnedUpstreamHash()
    {
        // Arrange
        using var stream = OnnxChatterboxTurboEnglishSynthesisModel.OpenTokenizerResource();
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);

        // Act
        var hash = Convert.ToHexString(SHA256.HashData(ReadAll(gzip))).ToLowerInvariant();

        // Assert
        Assert.Equal(TokenizerSha256, hash);
    }

    /// <summary>Proves a document that is not a tokenizer is rejected with <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_Constructor_InvalidDocument_ThrowsInvalidData()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"model\":{}}"));

        // Act
        var exception = Record.Exception(() => new ByteLevelBpeTokenizer(stream));

        // Assert
        Assert.IsType<InvalidDataException>(exception);
    }

    /// <summary>Proves a stream that is not gzip is rejected with <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_FromGzipStream_NotGzip_ThrowsInvalidData()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not gzip at all"));

        // Act
        var exception = Record.Exception(() => ByteLevelBpeTokenizer.FromGzipStream(stream));

        // Assert
        Assert.IsType<InvalidDataException>(exception);
    }

    /// <summary>Proves a duplicate merge keeps its first (lowest) rank.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_Constructor_DuplicateMerge_KeepsFirstRank()
    {
        // Arrange: "a b" ranks first and again last; the later duplicate must not demote it below "b c"
        var tokenizer = Create(
            "{\"a\":0,\"b\":1,\"c\":2,\"ab\":3,\"bc\":4}",
            "[\"a b\",\"b c\",\"a b\"]");

        // Act
        var ids = tokenizer.Encode("abc");

        // Assert
        Assert.Equal([3L, 2L], ids);
    }

    /// <summary>Proves the earlier-listed merge applies first when two merges overlap.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_Encode_EarlierMergeWins_OverLaterMerge()
    {
        // Arrange: "b c" is listed before "a b", so "abc" becomes a + bc rather than ab + c
        var tokenizer = Create(
            "{\"a\":0,\"b\":1,\"c\":2,\"ab\":3,\"bc\":4}",
            "[\"b c\",\"a b\"]");

        // Act
        var ids = tokenizer.Encode("abc");

        // Assert
        Assert.Equal([0L, 4L], ids);
    }

    /// <summary>Proves a negative trailing end-of-text count is rejected.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_Constructor_NegativeTrailingCount_ThrowsArgumentOutOfRange()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"model\":{\"vocab\":{\"a\":0},\"merges\":[]}}"));

        // Act
        var exception = Record.Exception(() => new ByteLevelBpeTokenizer(stream, trailingEndOfTextCount: -1));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    /// <summary>Proves a missing end-of-text token is rejected when ids must be appended.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_Constructor_MissingEndOfText_ThrowsInvalidData()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"model\":{\"vocab\":{\"a\":0},\"merges\":[]}}"));

        // Act
        var exception = Record.Exception(() => new ByteLevelBpeTokenizer(stream, trailingEndOfTextCount: 1));

        // Assert
        Assert.IsType<InvalidDataException>(exception);
    }

    /// <summary>Proves a document with an empty vocabulary is rejected.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_Constructor_EmptyVocabulary_ThrowsInvalidData()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"model\":{\"vocab\":{},\"merges\":[]}}"));

        // Act
        var exception = Record.Exception(() => new ByteLevelBpeTokenizer(stream, trailingEndOfTextCount: 0));

        // Assert
        Assert.IsType<InvalidDataException>(exception);
    }

    /// <summary>Proves null or malformed merge entries are reported as <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("[null]")]
    [InlineData("[\"a\"]")]
    [InlineData("[[\"a\",null]]")]
    public void ByteLevelBpeTokenizer_Constructor_MalformedMerge_ThrowsInvalidData(string merges)
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"model\":{\"vocab\":{\"a\":0},\"merges\":" + merges + "}}"));

        // Act
        var exception = Record.Exception(() => new ByteLevelBpeTokenizer(stream, trailingEndOfTextCount: 0));

        // Assert
        Assert.IsType<InvalidDataException>(exception);
    }

    /// <summary>Proves encoding a piece without a vocabulary entry reports a corrupt tokenizer.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_Encode_PieceWithoutVocabularyEntry_ThrowsInvalidOperation()
    {
        // Arrange
        var tokenizer = Create("{\"a\":0}", "[]");

        // Act
        var exception = Record.Exception(() => tokenizer.Encode("z"));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    /// <summary>Proves null arguments are rejected.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_NullArguments_Throw()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        var nullStream = Record.Exception(() => new ByteLevelBpeTokenizer(null!));
        var nullGzip = Record.Exception(() => ByteLevelBpeTokenizer.FromGzipStream(null!));
        var nullText = Record.Exception(() => _tokenizer.Encode(null!));
        var nullEndOfText = Record.Exception(() => new ByteLevelBpeTokenizer(stream, endOfTextToken: null!));

        // Assert
        Assert.IsType<ArgumentNullException>(nullStream);
        Assert.IsType<ArgumentNullException>(nullGzip);
        Assert.IsType<ArgumentNullException>(nullText);
        Assert.IsType<ArgumentNullException>(nullEndOfText);
    }

    /// <summary>Proves an added token with null content is reported as <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_Constructor_NullAddedTokenContent_ThrowsInvalidData()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            "{\"model\":{\"vocab\":{\"a\":0},\"merges\":[]},\"added_tokens\":[{\"id\":1,\"content\":null}]}"));

        // Act
        var exception = Record.Exception(() => new ByteLevelBpeTokenizer(stream, trailingEndOfTextCount: 0));

        // Assert
        Assert.IsType<InvalidDataException>(exception);
    }

    /// <summary>Proves an added token with empty content is rejected instead of matching everywhere.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_Constructor_EmptyAddedTokenContent_ThrowsInvalidData()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            "{\"model\":{\"vocab\":{\"a\":0},\"merges\":[]},\"added_tokens\":[{\"id\":1,\"content\":\"\"}]}"));

        // Act
        var exception = Record.Exception(() => new ByteLevelBpeTokenizer(stream, trailingEndOfTextCount: 0));

        // Assert
        Assert.IsType<InvalidDataException>(exception);
    }

    /// <summary>Proves a custom end-of-text token found in the vocabulary is the one appended.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_Constructor_CustomEndOfTextInVocabulary_IsAppended()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"model\":{\"vocab\":{\"a\":0,\"<eot>\":7},\"merges\":[]}}"));
        var tokenizer = new ByteLevelBpeTokenizer(stream, endOfTextToken: "<eot>", trailingEndOfTextCount: 1);

        // Act
        var ids = tokenizer.Encode("a");

        // Assert
        Assert.Equal([0L, 7L], ids);
    }

    /// <summary>Proves an added token that prefixes a longer one cannot shadow it.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_Encode_AddedTokenPrefixingAnother_LongestWins()
    {
        // Arrange: the shorter token is listed first
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            "{\"model\":{\"vocab\":{\"a\":0},\"merges\":[]},\"added_tokens\":[{\"id\":5,\"content\":\"<t>\"},{\"id\":6,\"content\":\"<t>x\"}]}"));
        var tokenizer = new ByteLevelBpeTokenizer(stream, trailingEndOfTextCount: 0);

        // Act
        var ids = tokenizer.Encode("<t>x<t>");

        // Assert
        Assert.Equal([6L, 5L], ids);
    }

    /// <summary>Builds a tokenizer without trailing ids from a vocabulary and merges JSON fragment.</summary>
    private static ByteLevelBpeTokenizer Create(string vocabulary, string merges)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"model\":{\"vocab\":" + vocabulary + ",\"merges\":" + merges + "}}"));
        return new ByteLevelBpeTokenizer(stream, trailingEndOfTextCount: 0);
    }

    /// <summary>Reads a stream fully.</summary>
    private static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
