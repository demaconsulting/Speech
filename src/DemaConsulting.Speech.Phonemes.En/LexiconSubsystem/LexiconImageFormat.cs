namespace DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

/// <summary>
///     The binary format of the lexicon image (the decompressed content of <c>cmudict-en.bin.gz</c>), shared by the
///     reader in this library and the writer of the non-shipped tools project.
/// </summary>
/// <remarks>
///     <para>
///     Layout, all integers little-endian: the magic <c>CMUL</c>, a version byte, the block size byte, the
///     word count (int32), then one entry per word in ordinal (byte) order of the word. An entry is
///     <c>prefixLength</c> (byte, the number of leading characters shared with the previous word; always 0 for
///     the first word of every block of <see cref="BlockSize"/> words), <c>suffixLength</c> (byte), the suffix
///     as ASCII, <c>variantCount</c> (byte, at least 1), then per variant a <c>phoneCount</c> byte and that many
///     phone codes (see <see cref="Phone"/>).
///     </para>
///     <para>
///     Front coding makes the words about half their size; block restarts let a lookup binary-search the
///     block heads and decode at most one block without an index in the file.
///     </para>
/// </remarks>
internal static class LexiconImageFormat
{
    /// <summary>The size of the header in bytes: magic, version, block size and word count.</summary>
    public const int HeaderSize = 10;

    /// <summary>
    ///     The smallest possible entry in bytes: the prefix and suffix lengths, the variant count and one variant of a
    ///     phone count and a single phone, with an empty suffix. A reader uses it to refuse a declared word count that the
    ///     image is too short to hold.
    /// </summary>
    public const int MinEntrySize = 5;

    /// <summary>The format version.</summary>
    public const byte Version = 1;

    /// <summary>The number of words per front-coded block.</summary>
    public const byte BlockSize = 32;

    /// <summary>The longest word (and the largest prefix, suffix or phone count) the byte-sized fields can hold.</summary>
    public const int MaxWordLength = 255;

    /// <summary>Gets the four magic bytes at the start of the image.</summary>
    public static ReadOnlySpan<byte> Magic => "CMUL"u8;
}
