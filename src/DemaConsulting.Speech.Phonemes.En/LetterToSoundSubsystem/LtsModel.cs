using System.IO.Compression;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.LetterToSoundSubsystem;

/// <summary>
///     The trained letter-to-sound model: three small integer networks that turn the spelling of a word the lexicon
///     does not know into an ARPAbet pronunciation with lexical stress.
/// </summary>
/// <remarks>
///     <para>
///     <b>Method.</b> Training aligned every letter of every CMU Pronouncing Dictionary word with zero, one or two
///     phones (a silent <c>e</c> gets none, <c>x</c> gets <c>K S</c>). The <em>phone network</em> reads a window of
///     thirteen letters around a letter and the letter's distance from both word ends and chooses that letter's
///     phone chunk (stress ignored). The <em>primary network</em> then scores every vowel of the resulting phone
///     string as the carrier of the primary stress from its phone context, its syllable position, the first and last
///     letters of the word and the weight of its syllable; the best-scoring vowel receives primary stress (a
///     one-vowel word always does). The <em>secondary network</em> decides for each other vowel whether it carries
///     secondary stress, knowing how far it is from the primary vowel. Every other vowel is unstressed.
///     </para>
///     <para>
///     <b>Provenance.</b> The weights are derived only from the CMU Pronouncing Dictionary by the non-shipped tools
///     project (<c>train-lts</c>); the package carries the dictionary's license notice. The serialized form is
///     documented in <see cref="FromImage"/>. The model is immutable and thread-safe, and its integer arithmetic gives
///     the same answer on every machine.
///     </para>
/// </remarks>
internal sealed class LtsModel
{
    /// <summary>The manifest resource name of the embedded gzip-compressed model image.</summary>
    public const string ResourceName = "DemaConsulting.Speech.Phonemes.En.Resources.lts-en.bin.gz";

    /// <summary>The number of ARPAbet base symbols: 24 consonants (ids 0 to 23) then 15 vowels (ids 24 to 38).</summary>
    public const int BasePhoneCount = 39;

    /// <summary>The letter id of the apostrophe; letters <c>a</c> to <c>z</c> are <c>1</c> to <c>26</c> and <c>0</c> is the word boundary.</summary>
    public const int ApostropheId = 27;

    /// <summary>The number of letter ids.</summary>
    public const int LetterIdCount = 28;

    /// <summary>The number of letters read on each side of the letter being converted.</summary>
    public const int Window = 6;

    /// <summary>The first base id that is a vowel.</summary>
    public const int FirstVowelId = 24;

    /// <summary>The number of chunk choices of earlier letters the phone network sees.</summary>
    public const int History = 3;

    /// <summary>The largest number of phone chunk classes (a history value is the class plus one, below <see cref="HistoryCardinality"/>).</summary>
    public const int MaxClasses = 127;

    /// <summary>The number of values of a history group.</summary>
    public const int HistoryCardinality = MaxClasses + 1;

    /// <summary>The image magic.</summary>
    private static readonly byte[] Magic = "LTS1"u8.ToArray();

    /// <summary>The lazily loaded embedded model.</summary>
    private static readonly Lazy<LtsModel> DefaultModel = new(LoadEmbedded, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The input groups of the phone network.</summary>
    private static readonly int[] PhoneGroupSizes = BuildPhoneGroups();

    /// <summary>The input groups of the primary stress network.</summary>
    private static readonly int[] PrimaryGroupSizes = BuildStressGroups(secondary: false);

    /// <summary>The input groups of the secondary stress network.</summary>
    private static readonly int[] SecondaryGroupSizes = BuildStressGroups(secondary: true);

    /// <summary>The phone chunk (zero to two base ids) of every chunk id.</summary>
    private readonly byte[][] _chunks;

    /// <summary>The network that chooses the phone chunk of a letter.</summary>
    private readonly LtsNetwork _phones;

    /// <summary>The network that scores a vowel as the primary stress carrier.</summary>
    private readonly LtsNetwork _primary;

    /// <summary>The network that decides secondary stress of a vowel.</summary>
    private readonly LtsNetwork _secondary;

    /// <summary>
    ///     Initializes a model from a decompressed image.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <exception cref="InvalidDataException">Thrown when the image is malformed.</exception>
    private LtsModel(byte[] image)
    {
        using var stream = new MemoryStream(image, writable: false);
        using var reader = new BinaryReader(stream);
        try
        {
            if (!reader.ReadBytes(Magic.Length).AsSpan().SequenceEqual(Magic))
            {
                throw new InvalidDataException("The letter-to-sound image header is not valid.");
            }

            // The phone chunk table
            var count = reader.ReadUInt16();
            if (count is 0 or > MaxClasses)
            {
                throw new InvalidDataException("The letter-to-sound chunk table has an invalid size.");
            }

            _chunks = new byte[count][];
            for (var i = 0; i < count; i++)
            {
                var length = reader.ReadByte();
                _chunks[i] = reader.ReadBytes(length);

                // ReadBytes returns fewer bytes at the end of the data instead of throwing, so check the count read
                if (_chunks[i].Length != length)
                {
                    throw new InvalidDataException("The letter-to-sound image is truncated.");
                }

                if (length > 2 || _chunks[i].Any(id => id >= BasePhoneCount))
                {
                    throw new InvalidDataException("The letter-to-sound chunk table is not valid.");
                }
            }

            // The three networks
            _phones = LtsNetwork.Read(reader, PhoneGroupSizes);
            _primary = LtsNetwork.Read(reader, PrimaryGroupSizes);
            _secondary = LtsNetwork.Read(reader, SecondaryGroupSizes);
            if (_phones.Outputs != count || _primary.Outputs != 2 || _secondary.Outputs != 2)
            {
                throw new InvalidDataException("The letter-to-sound networks do not have the expected outputs.");
            }

            if (stream.Position != stream.Length)
            {
                throw new InvalidDataException("The letter-to-sound image has trailing data.");
            }
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("The letter-to-sound image is truncated.", exception);
        }
    }

    /// <summary>Gets the model embedded in this assembly, loaded on first use; shared and thread-safe.</summary>
    public static LtsModel Default => DefaultModel.Value;

    /// <summary>Gets the cardinality of every input group of the phone network.</summary>
    public static IReadOnlyList<int> PhoneGroups => PhoneGroupSizes;

    /// <summary>Gets the cardinality of every input group of the primary stress network.</summary>
    public static IReadOnlyList<int> PrimaryGroups => PrimaryGroupSizes;

    /// <summary>Gets the cardinality of every input group of the secondary stress network.</summary>
    public static IReadOnlyList<int> SecondaryGroups => SecondaryGroupSizes;

    /// <summary>
    ///     Loads a model from a gzip stream.
    /// </summary>
    /// <param name="stream">A gzip stream of the image.</param>
    /// <returns>The model.</returns>
    /// <exception cref="InvalidDataException">Thrown when the data is not a valid model.</exception>
    public static LtsModel FromGZipStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var buffer = new MemoryStream();
        gzip.CopyTo(buffer);
        return new LtsModel(buffer.ToArray());
    }

    /// <summary>
    ///     Loads a model from an uncompressed image.
    /// </summary>
    /// <remarks>
    ///     The image is the magic <c>LTS1</c>, the chunk table (a 16-bit count, then per chunk a length byte and that
    ///     many base-phone ids), and the phone, primary and secondary networks in the order of
    ///     <see cref="LtsNetwork.Read"/> (group count byte, 16-bit cardinalities, 16-bit hidden widths and output
    ///     count, 16-bit embedding rows, 32-bit first bias and multiplier, 8-bit second weights, 32-bit second bias
    ///     and multiplier, 8-bit output weights, 32-bit output bias), all little-endian.
    /// </remarks>
    /// <param name="image">The image bytes.</param>
    /// <returns>The model.</returns>
    /// <exception cref="InvalidDataException">Thrown when the image is malformed.</exception>
    public static LtsModel FromImage(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return new LtsModel(image);
    }

    /// <summary>
    ///     Gets the id of a letter.
    /// </summary>
    /// <param name="letter">A character.</param>
    /// <returns>The id (1 to 26 for a letter of either case, 27 for an apostrophe), or <c>0</c> for any other character.</returns>
    public static int LetterId(char letter) => letter switch
    {
        >= 'a' and <= 'z' => letter - 'a' + 1,
        >= 'A' and <= 'Z' => letter - 'A' + 1,
        '\'' or '\u2019' => ApostropheId,
        _ => 0,
    };

    /// <summary>
    ///     Gets the base id of a phone: the consonant or vowel without its stress.
    /// </summary>
    /// <param name="phone">The phone.</param>
    /// <returns>The base id, <c>0</c> to <see cref="BasePhoneCount"/> - 1.</returns>
    public static int BaseId(Phone phone) => phone.IsVowel ? FirstVowelId + ((phone.Code - 25) / 3) : phone.Code - 1;

    /// <summary>
    ///     Fills the input of the phone network for one letter.
    /// </summary>
    /// <param name="letters">The letter ids of the whole word.</param>
    /// <param name="position">The index of the letter being converted.</param>
    /// <param name="chunkClasses">The chunk classes chosen for the letters before <paramref name="position"/> (only the last <see cref="History"/> are read).</param>
    /// <param name="values">Receives one value per group of <see cref="PhoneGroups"/>.</param>
    public static void PhoneFeatures(ReadOnlySpan<int> letters, int position, ReadOnlySpan<int> chunkClasses, Span<int> values)
    {
        // The window of letters, the word boundary where the word ends
        for (var k = 0; k <= 2 * Window; k++)
        {
            var at = position - Window + k;
            values[k] = at < 0 || at >= letters.Length ? 0 : letters[at];
        }

        // The distance from both word ends
        values[(2 * Window) + 1] = Math.Min(position, 9);
        values[(2 * Window) + 2] = Math.Min(letters.Length - 1 - position, 9);

        // The chunks already chosen for the letters to the left (+1 so that 0 is "no letter")
        for (var k = 1; k <= History; k++)
        {
            values[(2 * Window) + 2 + k] = position - k >= 0 ? chunkClasses[position - k] + 1 : 0;
        }

        // The first and last three letters of the word: a cue of the word's family (-ski, -ez, -ova, -tion, ...)
        var edge = (2 * Window) + 3 + History;
        for (var k = 0; k < 3; k++)
        {
            values[edge + k] = k < letters.Length ? letters[k] : 0;
            var last = letters.Length - 3 + k;
            values[edge + 3 + k] = last >= 0 ? letters[last] : 0;
        }
    }

    /// <summary>
    ///     Fills the input of a stress network for one vowel.
    /// </summary>
    /// <param name="letters">The letter ids of the whole word.</param>
    /// <param name="baseIds">The base ids of the word's phones.</param>
    /// <param name="vowels">The positions in <paramref name="baseIds"/> of the vowel phones, in order.</param>
    /// <param name="vowelIndex">The index in <paramref name="vowels"/> of the vowel being scored.</param>
    /// <param name="primary">The index in <paramref name="vowels"/> of the primary vowel, or <c>-1</c> for the primary network (which has no such input).</param>
    /// <param name="values">Receives one value per group of <see cref="PrimaryGroups"/> (or <see cref="SecondaryGroups"/> when <paramref name="primary"/> is not <c>-1</c>).</param>
    public static void StressFeatures(ReadOnlySpan<int> letters, ReadOnlySpan<byte> baseIds, ReadOnlySpan<int> vowels, int vowelIndex, int primary, Span<int> values)
    {
        var at = vowels[vowelIndex];

        // The phones around the vowel, +1 so that 0 is "no phone"
        for (var k = -3; k <= 3; k++)
        {
            var p = at + k;
            values[k + 3] = p < 0 || p >= baseIds.Length ? 0 : baseIds[p] + 1;
        }

        // The vowel's place among the word's vowels
        values[7] = Math.Min(vowelIndex, 7);
        values[8] = Math.Min(vowels.Length - 1 - vowelIndex, 7);
        values[9] = Math.Min(vowels.Length, 8) - 1;

        // The last four and the first three letters (suffixes and prefixes decide most stress)
        for (var k = 0; k < 4; k++)
        {
            var l = letters.Length - 4 + k;
            values[10 + k] = l < 0 ? 0 : letters[l];
        }

        for (var k = 0; k < 3; k++)
        {
            values[14 + k] = k < letters.Length ? letters[k] : 0;
        }

        // The weight of the syllable: the consonants before and after the vowel up to the next vowel
        var onset = 0;
        for (var p = at - 1; p >= 0 && baseIds[p] < FirstVowelId && onset < 3; p--)
        {
            onset++;
        }

        var coda = 0;
        for (var p = at + 1; p < baseIds.Length && baseIds[p] < FirstVowelId && coda < 3; p++)
        {
            coda++;
        }

        values[17] = onset;
        values[18] = coda;

        // The neighboring vowels
        values[19] = vowelIndex > 0 ? baseIds[vowels[vowelIndex - 1]] + 1 : 0;
        values[20] = vowelIndex + 1 < vowels.Length ? baseIds[vowels[vowelIndex + 1]] + 1 : 0;
        if (primary >= 0)
        {
            values[21] = Math.Clamp(vowelIndex - primary, -6, 6) + 6;
        }
    }

    /// <summary>
    ///     Pronounces a word that the lexicon does not know.
    /// </summary>
    /// <remarks>
    ///     Characters other than letters and apostrophes are skipped, and apostrophes at either end are dropped, so
    ///     any string is accepted. A word without letters has no pronunciation. A word with letters always has one,
    ///     except that a model output of no phone at all (not expected for a word with a vowel letter) is returned
    ///     as the empty pronunciation, which the caller must handle. The cost is linear in the length of the word.
    /// </remarks>
    /// <param name="word">The word in any case.</param>
    /// <returns>The pronunciation with stress; empty when the word has no letters.</returns>
    public Pronunciation Convert(ReadOnlySpan<char> word)
    {
        // Keep the letters, trim apostrophes at both ends
        var all = new int[word.Length];
        var n = 0;
        foreach (var c in word)
        {
            var id = LetterId(c);
            if (id != 0)
            {
                all[n++] = id;
            }
        }

        var start = 0;
        while (start < n && all[start] == ApostropheId)
        {
            start++;
        }

        while (n > start && all[n - 1] == ApostropheId)
        {
            n--;
        }

        var letters = all.AsSpan(start, n - start);
        if (letters.IsEmpty)
        {
            return Pronunciation.Empty;
        }

        // The phone chunk of every letter
        var scratch = new int[Math.Max(_phones.ScratchSize, Math.Max(_primary.ScratchSize, _secondary.ScratchSize))];
        var scores = new int[Math.Max(_phones.Outputs, 2)];
        var features = new int[PhoneGroupSizes.Length];
        var classes = new int[letters.Length];
        var baseIds = new List<byte>(letters.Length + 2);
        for (var i = 0; i < letters.Length; i++)
        {
            PhoneFeatures(letters, i, classes, features);
            _phones.Score(features, scratch, scores);
            classes[i] = Argmax(scores.AsSpan(0, _phones.Outputs));
            baseIds.AddRange(_chunks[classes[i]]);
        }

        // The stress of the vowels
        var phoneIds = baseIds.ToArray();
        var vowels = new List<int>();
        for (var p = 0; p < phoneIds.Length; p++)
        {
            if (phoneIds[p] >= FirstVowelId)
            {
                vowels.Add(p);
            }
        }

        var stress = ChooseStress(letters, phoneIds, vowels.ToArray(), scratch, scores);
        var phones = new Phone[phoneIds.Length];
        var next = 0;
        for (var p = 0; p < phoneIds.Length; p++)
        {
            var id = phoneIds[p];
            phones[p] = id < FirstVowelId
                ? Phone.FromCode((byte)(id + 1))
                : Phone.FromCode((byte)(25 + ((id - FirstVowelId) * 3) + stress[next++]));
        }

        return new Pronunciation(phones);
    }

    /// <summary>
    ///     Chooses the stress digit of every vowel.
    /// </summary>
    /// <param name="letters">The letter ids.</param>
    /// <param name="baseIds">The base ids of the phones.</param>
    /// <param name="vowels">The positions of the vowel phones.</param>
    /// <param name="scratch">The network scratch buffer.</param>
    /// <param name="scores">The network score buffer (at least two values).</param>
    /// <returns>One stress digit per vowel: one primary (a single vowel is always primary), some secondary, the rest none.</returns>
    private int[] ChooseStress(ReadOnlySpan<int> letters, byte[] baseIds, int[] vowels, int[] scratch, int[] scores)
    {
        var stress = new int[vowels.Length];
        if (vowels.Length == 0)
        {
            return stress;
        }

        // The primary vowel: the one the primary network likes best (ties: the first)
        var primary = 0;
        if (vowels.Length > 1)
        {
            var features = new int[PrimaryGroupSizes.Length];
            var best = int.MinValue;
            for (var v = 0; v < vowels.Length; v++)
            {
                StressFeatures(letters, baseIds, vowels, v, -1, features);
                _primary.Score(features, scratch, scores);
                var margin = scores[1] - scores[0];
                if (margin > best)
                {
                    best = margin;
                    primary = v;
                }
            }
        }

        stress[primary] = 1;

        // Secondary stress of the other vowels
        var secondaryFeatures = new int[SecondaryGroupSizes.Length];
        for (var v = 0; v < vowels.Length && vowels.Length > 1; v++)
        {
            if (v == primary)
            {
                continue;
            }

            StressFeatures(letters, baseIds, vowels, v, primary, secondaryFeatures);
            _secondary.Score(secondaryFeatures, scratch, scores);
            if (scores[1] > scores[0])
            {
                stress[v] = 2;
            }
        }

        return stress;
    }

    /// <summary>
    ///     Finds the index of the largest score; the first one of equal scores.
    /// </summary>
    /// <param name="scores">The scores; not empty.</param>
    /// <returns>The index.</returns>
    private static int Argmax(ReadOnlySpan<int> scores)
    {
        var best = 0;
        for (var i = 1; i < scores.Length; i++)
        {
            if (scores[i] > scores[best])
            {
                best = i;
            }
        }

        return best;
    }

    /// <summary>
    ///     Builds the group cardinalities of the phone network: thirteen letter positions and two word-end distances.
    /// </summary>
    /// <returns>The cardinalities.</returns>
    private static int[] BuildPhoneGroups()
    {
        var groups = new int[(2 * Window) + 3 + History + 6];
        Array.Fill(groups, LetterIdCount, 0, (2 * Window) + 1);
        groups[(2 * Window) + 1] = 10;
        groups[(2 * Window) + 2] = 10;
        Array.Fill(groups, HistoryCardinality, (2 * Window) + 3, History);
        Array.Fill(groups, LetterIdCount, (2 * Window) + 3 + History, 6);
        return groups;
    }

    /// <summary>
    ///     Builds the group cardinalities of a stress network.
    /// </summary>
    /// <param name="secondary">Whether this is the secondary network, which has the extra distance-to-primary input.</param>
    /// <returns>The cardinalities.</returns>
    private static int[] BuildStressGroups(bool secondary)
    {
        var groups = new List<int>();
        groups.AddRange(Enumerable.Repeat(BasePhoneCount + 1, 7));
        groups.AddRange([8, 8, 8]);
        groups.AddRange(Enumerable.Repeat(LetterIdCount, 7));
        groups.AddRange([4, 4, BasePhoneCount + 1, BasePhoneCount + 1]);
        if (secondary)
        {
            groups.Add(13);
        }

        return [.. groups];
    }

    /// <summary>
    ///     Loads the model embedded in this assembly.
    /// </summary>
    /// <returns>The model.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the resource is missing or malformed.</exception>
    private static LtsModel LoadEmbedded()
    {
        using var stream = typeof(LtsModel).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The embedded letter-to-sound resource '" + ResourceName + "' is missing from " + typeof(LtsModel).Assembly.GetName().Name + ".");
        try
        {
            return FromGZipStream(stream);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidOperationException("The embedded letter-to-sound resource is malformed: " + exception.Message, exception);
        }
    }
}
