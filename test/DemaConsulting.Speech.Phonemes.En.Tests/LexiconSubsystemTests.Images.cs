using System.Buffers.Binary;
using System.IO.Compression;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tests;

/// <summary>
///     Lexicon subsystem tests, image part: the CmuLexicon unit refuses a damaged lexicon image with an invalid-data
///     error. Every case is the real, valid image with one thing mutated in memory (a header field, the word count,
///     the length, a word or a phone code), so what is rejected is exactly the damage that was made.
/// </summary>
public sealed partial class LexiconSubsystemTests
{
    /// <summary>The byte offset of the word count in the image header.</summary>
    private const int WordCountOffset = 6;

    /// <summary>The decompressed embedded lexicon image, read once; tests copy it before mutating.</summary>
    private static readonly Lazy<byte[]> ValidImage = new(() =>
    {
        using var stream = typeof(CmuLexicon).Assembly.GetManifestResourceStream(CmuLexicon.ResourceName)!;
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var image = new MemoryStream();
        gzip.CopyTo(image);
        return image.ToArray();
    });

    /// <summary>Proves an undamaged copy of the image loads and has the word count its header declares (the control of the damage tests).</summary>
    [Fact]
    public void CmuLexicon_FromImage_UndamagedImage_LoadsWithItsDeclaredWordCount()
    {
        // Arrange
        var image = (byte[])ValidImage.Value.Clone();

        // Act
        var lexicon = CmuLexicon.FromImage(image);

        // Assert
        Assert.Equal(BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(WordCountOffset)), lexicon.WordCount);
        Assert.True(lexicon.Contains("cat"));
    }

    /// <summary>
    ///     Proves a damaged image is refused with an invalid-data error whose message says what is wrong: a bad header,
    ///     a negative or impossibly large word count, a count that disagrees with the data, a truncated image, trailing
    ///     data, a word outside the alphabet or out of order, and an invalid phone code.
    /// </summary>
    [Theory]
    [InlineData("empty", "header")]
    [InlineData("short-header", "header")]
    [InlineData("bad-magic", "header")]
    [InlineData("bad-version", "header")]
    [InlineData("bad-block-size", "header")]
    [InlineData("negative-count", "declares")]
    [InlineData("min-value-count", "declares")]
    [InlineData("header-only-negative-count", "declares")]
    [InlineData("max-value-count", "declares")]
    [InlineData("count-too-large-for-the-data", "declares")]
    [InlineData("count-one-too-many", "truncated")]
    [InlineData("count-one-too-few", "trailing")]
    [InlineData("truncated-last-byte", "lexicon image")]
    [InlineData("truncated-in-the-middle", "lexicon image")]
    [InlineData("trailing-byte", "trailing")]
    [InlineData("word-outside-alphabet", "alphabet")]
    [InlineData("words-out-of-order", "out of order")]
    [InlineData("phone-code-zero", "phone code")]
    [InlineData("phone-code-too-large", "phone code")]
    public void CmuLexicon_FromImage_DamagedImage_ThrowsInvalidDataNamingTheDamage(string damage, string expectedMessagePart)
    {
        // Arrange
        var image = Damage(damage);

        // Act
        var error = Assert.Throws<InvalidDataException>(() => CmuLexicon.FromImage(image));

        // Assert
        Assert.Contains(expectedMessagePart, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Proves a compressed stream that is not a gzip stream, or that is cut short, is refused with an invalid-data error, and a null argument with an argument error.</summary>
    [Fact]
    public void CmuLexicon_FromGZipStream_NotGZipOrCutShort_ThrowsInvalidData()
    {
        // Arrange
        using var notGzip = new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]);
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(ValidImage.Value);
        }

        using var cutShort = new MemoryStream(compressed.ToArray()[..(int)(compressed.Length / 2)]);

        // Act and assert
        Assert.Throws<InvalidDataException>(() => CmuLexicon.FromGZipStream(notGzip));
        Assert.Throws<InvalidDataException>(() => CmuLexicon.FromGZipStream(cutShort));
        Assert.Throws<ArgumentNullException>(() => CmuLexicon.FromGZipStream(null!));
        Assert.Throws<ArgumentNullException>(() => CmuLexicon.FromImage(null!));
    }

    /// <summary>Finds the offset of the second entry by skipping the first: its prefix and suffix lengths, the suffix, then each variant's phones.</summary>
    private static int SecondEntryOffset(byte[] image)
    {
        var position = LexiconImageFormat.HeaderSize;
        position += 2 + image[position + 1];
        var variants = image[position++];
        for (var v = 0; v < variants; v++)
        {
            position += 1 + image[position];
        }

        return position;
    }

    /// <summary>Builds a damaged copy of the valid image.</summary>
    private static byte[] Damage(string damage)
    {
        var image = (byte[])ValidImage.Value.Clone();
        var wordCount = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(WordCountOffset));
        switch (damage)
        {
            case "empty":
                return [];
            case "short-header":
                return image[..5];
            case "bad-magic":
                image[0] = (byte)'X';
                return image;
            case "bad-version":
                image[4] = 99;
                return image;
            case "bad-block-size":
                image[5] = LexiconImageFormat.BlockSize + 1;
                return image;
            case "negative-count":
                BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(WordCountOffset), -1);
                return image;
            case "min-value-count":
                BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(WordCountOffset), int.MinValue);
                return image;
            case "header-only-negative-count":
                var headerOnly = image[..LexiconImageFormat.HeaderSize];
                BinaryPrimitives.WriteInt32LittleEndian(headerOnly.AsSpan(WordCountOffset), -5);
                return headerOnly;
            case "max-value-count":
                BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(WordCountOffset), int.MaxValue);
                return image;
            case "count-too-large-for-the-data":
                BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(WordCountOffset), image.Length);
                return image;
            case "count-one-too-many":
                BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(WordCountOffset), wordCount + 1);
                return image;
            case "count-one-too-few":
                BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(WordCountOffset), wordCount - 1);
                return image;
            case "truncated-last-byte":
                return image[..^1];
            case "truncated-in-the-middle":
                return image[..(image.Length / 2)];
            case "trailing-byte":
                return [.. image, 0];
            case "word-outside-alphabet":
                // The first entry is prefix, suffix length, then the suffix: its first character
                image[LexiconImageFormat.HeaderSize + 2] = (byte)'A';
                return image;
            case "words-out-of-order":
                // Lower the first character of the second entry's suffix so that it sorts before the first word
                image[SecondEntryOffset(image) + 2] = (byte)'a';
                return image;
            case "phone-code-zero":
                image[^1] = 0;
                return image;
            case "phone-code-too-large":
                image[^1] = Phone.MaxCode + 1;
                return image;
            default:
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "Unknown damage.");
        }
    }
}
