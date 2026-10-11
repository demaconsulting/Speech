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

    /// <summary>Gets the golden cases as xunit theory data.</summary>
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
        // Act
        var ids = _tokenizer.Encode(text);

        // Assert
        Assert.Equal(expected, ids);
    }

    /// <summary>Proves the golden data is non-trivial, so the theory above cannot pass vacuously.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_GoldenCases_ContainManyCases()
    {
        // Assert
        Assert.True(GoldenCases().Count >= 30);
    }

    /// <summary>Proves the post-processor appends the end-of-text id twice, even for empty text.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_Encode_EmptyText_ReturnsOnlyTrailingPair()
    {
        // Act
        var ids = _tokenizer.Encode(string.Empty);

        // Assert
        Assert.Equal([50256L, 50256L], ids);
    }

    /// <summary>Proves bracketed paralinguistic tags are single atomic ids, with a space before one tokenizing as 220 first.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_Encode_Tag_IsAtomicAndSpaceIsSeparate()
    {
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

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => new ByteLevelBpeTokenizer(stream));
    }

    /// <summary>Proves a stream that is not gzip is rejected with <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_FromGzipStream_NotGzip_ThrowsInvalidData()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not gzip at all"));

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => ByteLevelBpeTokenizer.FromGzipStream(stream));
    }

    /// <summary>Proves null arguments are rejected.</summary>
    [Fact]
    public void ByteLevelBpeTokenizer_NullArguments_Throw()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => new ByteLevelBpeTokenizer(null!));
        Assert.Throws<ArgumentNullException>(() => ByteLevelBpeTokenizer.FromGzipStream(null!));
        Assert.Throws<ArgumentNullException>(() => _tokenizer.Encode(null!));
    }

    /// <summary>Reads a stream fully.</summary>
    private static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
