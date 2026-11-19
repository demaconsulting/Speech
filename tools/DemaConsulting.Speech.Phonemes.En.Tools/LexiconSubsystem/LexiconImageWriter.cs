using System.Text;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tools.LexiconSubsystem;

/// <summary>
///     Serializes lexicon entries into the lexicon image format read by <see cref="CmuLexicon"/>.
/// </summary>
/// <remarks>
///     The format is documented on <see cref="LexiconImageFormat"/>. The output is a pure function of the
///     entries, so building twice gives identical bytes (the compressed form is not compared, only the image).
/// </remarks>
internal static class LexiconImageWriter
{
    /// <summary>
    ///     Writes the image.
    /// </summary>
    /// <param name="entries">The entries, sorted by ordinal word order, each with at least one variant.</param>
    /// <returns>The uncompressed image bytes.</returns>
    /// <exception cref="InvalidDataException">Thrown when the entries are unsorted, a word or pronunciation is too long, or a word has no variant.</exception>
    public static byte[] Write(IReadOnlyList<LexiconEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        using var stream = new MemoryStream();

        // Header: magic, version, block size, word count
        stream.Write(LexiconImageFormat.Magic);
        stream.WriteByte(LexiconImageFormat.Version);
        stream.WriteByte(LexiconImageFormat.BlockSize);
        stream.Write(BitConverter.GetBytes(entries.Count));

        // Entries: front-coded against the previous word, restarting at every block head
        var previous = string.Empty;
        for (var index = 0; index < entries.Count; index++)
        {
            var (word, pronunciations) = entries[index];
            if (index > 0 && string.CompareOrdinal(previous, word) >= 0)
            {
                throw new InvalidDataException("The entries are not sorted at '" + word + "'.");
            }

            if (word.Length > LexiconImageFormat.MaxWordLength || pronunciations.Count is 0 or > byte.MaxValue)
            {
                throw new InvalidDataException("The entry '" + word + "' cannot be stored.");
            }

            var prefix = index % LexiconImageFormat.BlockSize == 0 ? 0 : CommonPrefix(previous, word);
            stream.WriteByte((byte)prefix);
            stream.WriteByte((byte)(word.Length - prefix));
            stream.Write(Encoding.ASCII.GetBytes(word[prefix..]));
            stream.WriteByte((byte)pronunciations.Count);
            foreach (var pronunciation in pronunciations)
            {
                if (pronunciation.Count > byte.MaxValue)
                {
                    throw new InvalidDataException("A pronunciation of '" + word + "' is too long.");
                }

                stream.WriteByte((byte)pronunciation.Count);
                foreach (var phone in pronunciation.Phones)
                {
                    stream.WriteByte(phone.Code);
                }
            }

            previous = word;
        }

        return stream.ToArray();
    }

    /// <summary>
    ///     Counts the leading characters two words share.
    /// </summary>
    /// <param name="a">The first word.</param>
    /// <param name="b">The second word.</param>
    /// <returns>The length of the common prefix.</returns>
    private static int CommonPrefix(string a, string b)
    {
        var length = Math.Min(a.Length, b.Length);
        var i = 0;
        while (i < length && a[i] == b[i])
        {
            i++;
        }

        return i;
    }
}
