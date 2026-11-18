using System.IO.Compression;
using System.Text;

namespace DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

/// <summary>
///     The English pronunciation lexicon derived from the CMU Pronouncing Dictionary: every pronunciation
///     variant of each word, looked up case-insensitively.
/// </summary>
/// <remarks>
///     <para>
///     <b>Why this shape.</b> The lexicon has about 125,000 words, so a managed dictionary of strings and
///     arrays would cost tens of megabytes. Instead the decompressed image (see <see cref="LexiconImageFormat"/>,
///     about 1.7 MB) stays in memory as bytes and a lookup binary-searches the first word of each front-coded
///     block, then scans at most one block. Pronunciations are created on demand.
///     </para>
///     <para>
///     <b>Lifetime and threading.</b> <see cref="Default"/> loads the embedded resource on first use under a
///     lock and is shared for the life of the process; an instance is immutable after construction and safe for
///     concurrent use. Loading never touches the network or the file system.
///     </para>
/// </remarks>
internal sealed class CmuLexicon
{
    /// <summary>The manifest resource name of the embedded gzip-compressed lexicon image.</summary>
    public const string ResourceName = "DemaConsulting.Speech.Phonemes.En.Resources.cmudict-en.bin.gz";

    /// <summary>The lazily loaded embedded lexicon; thread-safe publication.</summary>
    private static readonly Lazy<CmuLexicon> DefaultLexicon = new(LoadEmbedded, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The decompressed image.</summary>
    private readonly byte[] _image;

    /// <summary>The first word of every block (always stored in full), in sorted order.</summary>
    private readonly string[] _blockWords;

    /// <summary>The image offset of the first entry of every block.</summary>
    private readonly int[] _blockOffsets;

    /// <summary>
    ///     Initializes a lexicon from a decompressed image, validating its structure.
    /// </summary>
    /// <param name="image">The image bytes, in the format of <see cref="LexiconImageFormat"/>.</param>
    /// <exception cref="InvalidDataException">Thrown when the image is malformed.</exception>
    private CmuLexicon(byte[] image)
    {
        _image = image;

        // Validate the header
        if (image.Length < LexiconImageFormat.HeaderSize
            || !image.AsSpan(0, 4).SequenceEqual(LexiconImageFormat.Magic)
            || image[4] != LexiconImageFormat.Version
            || image[5] != LexiconImageFormat.BlockSize)
        {
            throw new InvalidDataException("The lexicon image header is not valid.");
        }

        WordCount = BitConverter.ToInt32(image, 6);

        // Walk every entry once to learn the block starts and the variant count
        var blockWords = new List<string>();
        var blockOffsets = new List<int>();
        var position = LexiconImageFormat.HeaderSize;
        var buffer = new byte[LexiconImageFormat.MaxWordLength];
        var variants = 0;
        for (var index = 0; index < WordCount; index++)
        {
            var offset = position;
            var length = ReadWord(image, ref position, buffer, index % LexiconImageFormat.BlockSize == 0, out var variantCount);
            variants += variantCount;
            if (index % LexiconImageFormat.BlockSize == 0)
            {
                blockWords.Add(Encoding.ASCII.GetString(buffer, 0, length));
                blockOffsets.Add(offset);
            }
        }

        if (position != image.Length)
        {
            throw new InvalidDataException("The lexicon image has trailing data.");
        }

        _blockWords = [.. blockWords];
        _blockOffsets = [.. blockOffsets];
        PronunciationCount = variants;
    }

    /// <summary>Gets the lexicon embedded in this assembly, loaded on first use.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the embedded resource is missing or malformed (a build defect).</exception>
    public static CmuLexicon Default => DefaultLexicon.Value;

    /// <summary>Gets the number of words.</summary>
    public int WordCount { get; }

    /// <summary>Gets the number of pronunciations of all words together.</summary>
    public int PronunciationCount { get; }

    /// <summary>
    ///     Reads a lexicon from a gzip-compressed image stream.
    /// </summary>
    /// <param name="compressed">A stream of the gzip-compressed image; not disposed.</param>
    /// <returns>The lexicon.</returns>
    /// <exception cref="InvalidDataException">Thrown when the stream is not a valid compressed lexicon image.</exception>
    public static CmuLexicon FromGZipStream(Stream compressed)
    {
        ArgumentNullException.ThrowIfNull(compressed);
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress, leaveOpen: true);
        using var image = new MemoryStream();
        gzip.CopyTo(image);
        return FromImage(image.ToArray());
    }

    /// <summary>
    ///     Reads a lexicon from a decompressed image.
    /// </summary>
    /// <param name="image">The image bytes; not copied, so the caller must not change them afterwards.</param>
    /// <returns>The lexicon.</returns>
    /// <exception cref="InvalidDataException">Thrown when the image is malformed.</exception>
    public static CmuLexicon FromImage(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return new CmuLexicon(image);
    }

    /// <summary>
    ///     Looks up all pronunciation variants of a word.
    /// </summary>
    /// <param name="word">The word; case is ignored and the typographic apostrophe is treated as <c>'</c>.</param>
    /// <returns>The variants in dictionary order (the first is the default), or an empty list when the word is not in the lexicon.</returns>
    public IReadOnlyList<Pronunciation> Lookup(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        // Fold the word to the lexicon's key alphabet; anything else cannot be a headword
        if (word.Length is 0 or > LexiconImageFormat.MaxWordLength)
        {
            return [];
        }

        Span<byte> key = stackalloc byte[word.Length];
        for (var i = 0; i < word.Length; i++)
        {
            var c = word[i];
            c = c == '\u2019' ? '\'' : char.ToLowerInvariant(c);
            if (c is not ((>= 'a' and <= 'z') or '\''))
            {
                return [];
            }

            key[i] = (byte)c;
        }

        // Find the block that can hold the key: the last block whose first word is not greater
        var block = FindBlock(key);
        if (block < 0)
        {
            return [];
        }

        // Scan the block; the entries are sorted, so stop at the first word greater than the key
        var buffer = new byte[LexiconImageFormat.MaxWordLength];
        var position = _blockOffsets[block];
        var last = Math.Min(((block + 1) * LexiconImageFormat.BlockSize), WordCount) - (block * LexiconImageFormat.BlockSize);
        for (var i = 0; i < last; i++)
        {
            var start = position;
            var length = ReadWord(_image, ref position, buffer, i == 0, out _);
            var order = buffer.AsSpan(0, length).SequenceCompareTo(key);
            if (order == 0)
            {
                return ReadVariants(start);
            }

            if (order > 0)
            {
                break;
            }
        }

        return [];
    }

    /// <summary>
    ///     Tests whether a word is in the lexicon.
    /// </summary>
    /// <param name="word">The word; case is ignored.</param>
    /// <returns><see langword="true"/> when the lexicon lists at least one pronunciation of the word.</returns>
    public bool Contains(string word) => Lookup(word).Count > 0;

    /// <summary>
    ///     Enumerates every word with its variants in sorted order.
    /// </summary>
    /// <returns>All entries.</returns>
    public IEnumerable<LexiconEntry> Entries()
    {
        var buffer = new byte[LexiconImageFormat.MaxWordLength];
        var position = LexiconImageFormat.HeaderSize;
        for (var index = 0; index < WordCount; index++)
        {
            var start = position;
            var length = ReadWord(_image, ref position, buffer, index % LexiconImageFormat.BlockSize == 0, out _);
            yield return new LexiconEntry(Encoding.ASCII.GetString(buffer, 0, length), ReadVariants(start));
        }
    }

    /// <summary>
    ///     Loads the embedded resource.
    /// </summary>
    /// <returns>The embedded lexicon.</returns>
    private static CmuLexicon LoadEmbedded()
    {
        using var stream = typeof(CmuLexicon).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The embedded lexicon resource '" + ResourceName + "' is missing from " + typeof(CmuLexicon).Assembly.GetName().Name + ".");
        try
        {
            return FromGZipStream(stream);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidOperationException("The embedded lexicon resource is malformed: " + exception.Message, exception);
        }
    }

    /// <summary>
    ///     Decodes one entry's word into a buffer and advances past the whole entry.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <param name="position">The entry offset; advanced to the next entry.</param>
    /// <param name="buffer">Holds the previous word on entry (the shared prefix) and the decoded word on return.</param>
    /// <param name="blockStart">Whether the entry starts a block and must therefore have no shared prefix.</param>
    /// <param name="variantCount">Receives the number of pronunciation variants.</param>
    /// <returns>The length of the word in <paramref name="buffer"/>.</returns>
    /// <exception cref="InvalidDataException">Thrown when the entry is truncated or inconsistent.</exception>
    private static int ReadWord(byte[] image, ref int position, byte[] buffer, bool blockStart, out int variantCount)
    {
        var p = position;
        if (p + 2 > image.Length)
        {
            throw new InvalidDataException("The lexicon image is truncated.");
        }

        // The word is the shared prefix of the previous word plus a stored suffix
        int prefix = image[p++];
        int suffix = image[p++];
        if ((blockStart && prefix != 0) || prefix + suffix > buffer.Length || p + suffix + 1 > image.Length)
        {
            throw new InvalidDataException("The lexicon image has an invalid word.");
        }

        Buffer.BlockCopy(image, p, buffer, prefix, suffix);
        p += suffix;

        // Skip the variants: a phone count and that many phone codes each
        variantCount = image[p++];
        for (var v = 0; v < variantCount; v++)
        {
            if (p >= image.Length)
            {
                throw new InvalidDataException("The lexicon image is truncated.");
            }

            p += 1 + image[p];
        }

        if (p > image.Length || variantCount == 0)
        {
            throw new InvalidDataException("The lexicon image has an invalid pronunciation list.");
        }

        position = p;
        return prefix + suffix;
    }

    /// <summary>
    ///     Decodes the pronunciation variants of the entry that starts at an offset.
    /// </summary>
    /// <param name="entryOffset">The offset of the entry.</param>
    /// <returns>The variants.</returns>
    private Pronunciation[] ReadVariants(int entryOffset)
    {
        // Skip the prefix and suffix lengths and the stored suffix to reach the variant count
        var p = entryOffset + 2 + _image[entryOffset + 1];
        var count = _image[p++];
        var variants = new Pronunciation[count];
        for (var v = 0; v < count; v++)
        {
            int phoneCount = _image[p++];
            var phones = new Phone[phoneCount];
            for (var i = 0; i < phoneCount; i++)
            {
                phones[i] = Phone.FromCode(_image[p++]);
            }

            variants[v] = new Pronunciation(phones);
        }

        return variants;
    }

    /// <summary>
    ///     Finds the block whose first word is the greatest one not above the key.
    /// </summary>
    /// <param name="key">The folded key bytes.</param>
    /// <returns>The block index, or <c>-1</c> when the key sorts before every word.</returns>
    private int FindBlock(ReadOnlySpan<byte> key)
    {
        var low = 0;
        var high = _blockWords.Length - 1;
        var found = -1;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var first = _blockWords[mid];
            var order = CompareAscii(first, key);
            if (order <= 0)
            {
                found = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return found;
    }

    /// <summary>
    ///     Compares an ASCII string with key bytes in byte order.
    /// </summary>
    /// <param name="text">The ASCII string.</param>
    /// <param name="key">The key bytes.</param>
    /// <returns>A negative, zero or positive value as <paramref name="text"/> sorts before, equal to or after the key.</returns>
    private static int CompareAscii(string text, ReadOnlySpan<byte> key)
    {
        var common = Math.Min(text.Length, key.Length);
        for (var i = 0; i < common; i++)
        {
            var difference = text[i] - key[i];
            if (difference != 0)
            {
                return difference;
            }
        }

        return text.Length - key.Length;
    }
}
