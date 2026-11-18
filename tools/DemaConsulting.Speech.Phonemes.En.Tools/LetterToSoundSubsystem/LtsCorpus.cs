using System.Security.Cryptography;
using System.Text;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;
using DemaConsulting.Speech.Phonemes.En.Tools.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tools.LetterToSoundSubsystem;

/// <summary>
///     The words the letter-to-sound model is trained and measured on, and the deterministic stem-family split of
///     them into training, validation and held-out test words.
/// </summary>
/// <remarks>
///     <para>
///     <b>Why families.</b> A random split would put <c>cats</c> in the test set and <c>cat</c> in the training
///     set, and the model would be credited for memorizing it. Words are therefore grouped by stem family: a word
///     is joined to the headword it derives from by stripping an inflection or a common derivational suffix or
///     prefix (<c>cats</c>, <c>catty</c>, <c>unhappy</c> go with <c>cat</c> and <c>happy</c>), and all the words of a
///     connected group go to the same side. The side is a function of the group alone: the SHA-256 of
///     <c>lts-split-v1:</c> and the group's alphabetically first word, read as a little-endian number <c>n</c>,
///     decides (<c>n mod 10 = 0</c> is the test set, about 10 percent of the families; of the others
///     <c>(n / 10) mod 20 = 3</c> is a validation set of about 5 percent used only to watch training). The split has
///     no seed and needs no stored list: it is recomputed from the pinned dictionary.
///     </para>
///     <para>
///     Only the first pronunciation of a headword is trained on; every variant is scored against (see
///     <see cref="LtsBenchmark"/>).
///     </para>
/// </remarks>
internal static class LtsCorpus
{
    /// <summary>The name of the pinned dictionary in the manifest.</summary>
    private const string InputName = "cmudict.dict";

    /// <summary>The prefixes a word may carry on top of a known stem, as the phonemizer's own prefix rule.</summary>
    private static readonly string[] Prefixes =
        ["counter", "under", "super", "inter", "multi", "semi", "anti", "over", "non", "out", "sub", "dis", "mis", "pre", "un", "re"];

    /// <summary>The suffix rewrites: the ending to remove and the text to put in its place.</summary>
    private static readonly (string Ending, string Replacement)[] Suffixes =
    [
        ("ies", "y"), ("ied", "y"), ("ier", "y"), ("iest", "y"), ("ily", "y"), ("ing", string.Empty), ("ing", "e"),
        ("ed", string.Empty), ("ed", "e"), ("es", string.Empty), ("s", string.Empty), ("er", string.Empty), ("er", "e"),
        ("est", string.Empty), ("est", "e"), ("ly", string.Empty), ("y", string.Empty), ("y", "e"), ("ness", string.Empty),
        ("ment", string.Empty), ("ful", string.Empty), ("less", string.Empty), ("able", string.Empty), ("ish", string.Empty),
        ("ist", string.Empty), ("ism", string.Empty), ("ize", string.Empty), ("ise", string.Empty), ("ization", "ize"),
    ];

    /// <summary>
    ///     Reads the pinned CMUdict and returns its entries.
    /// </summary>
    /// <param name="root">The repository root.</param>
    /// <returns>The entries, sorted by word, with the same filtering as the embedded lexicon.</returns>
    /// <exception cref="InvalidDataException">Thrown when the file does not match the pin of <c>MANIFEST.tsv</c>.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the dictionary has not been fetched.</exception>
    public static IReadOnlyList<LexiconEntry> Load(string root)
    {
        var dataDirectory = ToolPaths.DataDirectory(root);
        var sourcePath = Path.Combine(dataDirectory, "external", "cmudict", "cmudict.dict");
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException(sourcePath + " was not found; fetch CMUdict first (see data/phonemes-en/README.md).");
        }

        var pinned = DataManifest.Read(Path.Combine(dataDirectory, "MANIFEST.tsv")).FirstOrDefault(r => r.Name == InputName && r.Kind == "input")
            ?? throw new InvalidDataException("MANIFEST.tsv has no input row for " + InputName + ".");
        var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(sourcePath)));
        if (hash != pinned.Sha256)
        {
            throw new InvalidDataException(InputName + " does not match the pin in MANIFEST.tsv.");
        }

        return CmuDictSource.Parse(File.ReadLines(sourcePath, Encoding.UTF8)).Entries;
    }

    /// <summary>
    ///     Splits the entries by stem family into training, validation and test words.
    /// </summary>
    /// <param name="entries">All entries.</param>
    /// <returns>The three parts; their union is <paramref name="entries"/>, with no family in two parts.</returns>
    public static LtsSplit Split(IReadOnlyList<LexiconEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var families = Families(entries);
        var train = new List<LexiconEntry>();
        var validation = new List<LexiconEntry>();
        var test = new List<LexiconEntry>();
        foreach (var entry in entries)
        {
            var bucket = Bucket(families[entry.Word]);
            if (bucket == 0)
            {
                test.Add(entry);
            }
            else if (bucket == 11)
            {
                validation.Add(entry);
            }
            else
            {
                train.Add(entry);
            }
        }

        return new LtsSplit(train, validation, test, families.Values.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    ///     Computes the stem family of every word: the alphabetically first word of its connected group.
    /// </summary>
    /// <param name="entries">All entries.</param>
    /// <returns>The family key of every word.</returns>
    public static Dictionary<string, string> Families(IReadOnlyList<LexiconEntry> entries)
    {
        var words = new HashSet<string>(entries.Select(e => e.Word), StringComparer.Ordinal);
        var parent = words.ToDictionary(w => w, w => w, StringComparer.Ordinal);

        // Union a word with every stem the word set knows
        foreach (var word in words)
        {
            foreach (var stem in Stems(word))
            {
                if (stem.Length >= 3 && words.Contains(stem))
                {
                    Union(parent, word, stem);
                }
            }
        }

        // The key of a group is its alphabetically first member
        var first = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var word in words)
        {
            var root = Find(parent, word);
            if (!first.TryGetValue(root, out var current) || string.CompareOrdinal(word, current) < 0)
            {
                first[root] = word;
            }
        }

        return words.ToDictionary(w => w, w => first[Find(parent, w)], StringComparer.Ordinal);
    }

    /// <summary>
    ///     Gets the bucket of a family key: <c>0</c> test (one tenth), <c>11</c> validation (one twentieth), otherwise a training bucket.
    /// </summary>
    /// <param name="familyKey">The family key.</param>
    /// <returns>The bucket.</returns>
    private static int Bucket(string familyKey)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes("lts-split-v1:" + familyKey));
        var number = BitConverter.ToUInt32(hash, 0);
        if (number % 10 == 0)
        {
            return 0;
        }

        return (number / 10) % 20 == 3 ? 11 : 1;
    }

    /// <summary>
    ///     Lists the words a word may derive from by one inflection, suffix or prefix.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <returns>The candidate stems (not necessarily words).</returns>
    private static IEnumerable<string> Stems(string word)
    {
        // Possessives and plural possessives
        if (word.EndsWith("'s", StringComparison.Ordinal))
        {
            yield return word[..^2];
        }

        if (word.EndsWith('\''))
        {
            yield return word[..^1];
        }

        // Suffixes, also with the final consonant undoubled (catty -> cat)
        foreach (var (ending, replacement) in Suffixes)
        {
            if (!word.EndsWith(ending, StringComparison.Ordinal) || word.Length <= ending.Length + 1)
            {
                continue;
            }

            var stem = word[..^ending.Length] + replacement;
            yield return stem;
            if (replacement.Length == 0 && stem.Length >= 2 && stem[^1] == stem[^2])
            {
                yield return stem[..^1];
            }
        }

        // A prefix on a longer remainder
        foreach (var prefix in Prefixes)
        {
            if (word.Length >= prefix.Length + 4 && word.StartsWith(prefix, StringComparison.Ordinal))
            {
                yield return word[prefix.Length..];
            }
        }
    }

    /// <summary>
    ///     Finds the root of a union-find set, compressing the path.
    /// </summary>
    /// <param name="parent">The parent links.</param>
    /// <param name="word">The word.</param>
    /// <returns>The root.</returns>
    private static string Find(Dictionary<string, string> parent, string word)
    {
        var root = word;
        while (parent[root] != root)
        {
            root = parent[root];
        }

        while (parent[word] != root)
        {
            var next = parent[word];
            parent[word] = root;
            word = next;
        }

        return root;
    }

    /// <summary>
    ///     Joins the sets of two words; the alphabetically smaller root wins so the structure is deterministic.
    /// </summary>
    /// <param name="parent">The parent links.</param>
    /// <param name="a">The first word.</param>
    /// <param name="b">The second word.</param>
    private static void Union(Dictionary<string, string> parent, string a, string b)
    {
        var ra = Find(parent, a);
        var rb = Find(parent, b);
        if (ra == rb)
        {
            return;
        }

        if (string.CompareOrdinal(ra, rb) < 0)
        {
            parent[rb] = ra;
        }
        else
        {
            parent[ra] = rb;
        }
    }
}

/// <summary>
///     The training, validation and test parts of the dictionary.
/// </summary>
/// <param name="Train">The training entries.</param>
/// <param name="Validation">The validation entries (watched during training, never trained on in the split run).</param>
/// <param name="Test">The held-out test entries.</param>
/// <param name="Families">The number of stem families.</param>
internal sealed record LtsSplit(IReadOnlyList<LexiconEntry> Train, IReadOnlyList<LexiconEntry> Validation, IReadOnlyList<LexiconEntry> Test, int Families);
