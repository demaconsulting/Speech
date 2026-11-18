using System.Text.Json;

namespace DemaConsulting.Speech.Phonemes.En.Tools.PhonemizerSubsystem;

/// <summary>
///     The symbol inventory of a voice model: the characters its phoneme string may contain and the id of each.
/// </summary>
/// <remarks>
///     This file is compiled into both the tools project and the test project, like <c>GoldenCorpus</c>. The Piper
///     inventory is the <c>phoneme_id_map</c> of the voice's JSON file (a trimmed copy is committed as test data); the
///     Kokoro inventory is the <c>model.vocab</c> of the model's own <c>tokenizer.json</c>. Every key of both is a single
///     UTF-16 character, so a rendered phoneme string is looked up character by character.
/// </remarks>
internal sealed class SymbolInventory
{
    /// <summary>The id of every symbol.</summary>
    private readonly Dictionary<char, int> _ids;

    /// <summary>
    ///     Initializes an inventory.
    /// </summary>
    /// <param name="ids">The id of every symbol.</param>
    private SymbolInventory(Dictionary<char, int> ids) => _ids = ids;

    /// <summary>Gets the number of symbols.</summary>
    public int Count => _ids.Count;

    /// <summary>Gets the symbols.</summary>
    public IEnumerable<char> Symbols => _ids.Keys;

    /// <summary>
    ///     Loads the inventory of a Piper voice from its JSON file or the trimmed copy of its <c>phoneme_id_map</c>.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The inventory.</returns>
    public static SymbolInventory LoadPiper(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var ids = new Dictionary<char, int>();
        foreach (var entry in document.RootElement.GetProperty("phoneme_id_map").EnumerateObject())
        {
            if (entry.Name.Length == 1)
            {
                ids[entry.Name[0]] = entry.Value[0].GetInt32();
            }
        }

        return new SymbolInventory(ids);
    }

    /// <summary>
    ///     Loads the inventory of Kokoro from the <c>tokenizer.json</c> of the model.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The inventory.</returns>
    public static SymbolInventory LoadKokoro(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var ids = new Dictionary<char, int>();
        foreach (var entry in document.RootElement.GetProperty("model").GetProperty("vocab").EnumerateObject())
        {
            if (entry.Name.Length == 1)
            {
                ids[entry.Name[0]] = entry.Value.GetInt32();
            }
        }

        return new SymbolInventory(ids);
    }

    /// <summary>
    ///     Tests whether a symbol is in the inventory.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns><see langword="true"/> when the model knows it.</returns>
    public bool Contains(char symbol) => _ids.ContainsKey(symbol);

    /// <summary>
    ///     Gets the characters of a phoneme string that are not in the inventory.
    /// </summary>
    /// <param name="phonemes">The phoneme string.</param>
    /// <returns>The distinct missing characters, in order of first occurrence.</returns>
    public IReadOnlyList<char> Missing(string phonemes) => [.. phonemes.Where(c => !_ids.ContainsKey(c)).Distinct()];

    /// <summary>
    ///     Converts a phoneme string to the ids of its characters (a character the model does not know is skipped, as the models' own front ends do).
    /// </summary>
    /// <param name="phonemes">The phoneme string.</param>
    /// <returns>The ids separated by single spaces.</returns>
    public string ToIds(string phonemes) =>
        string.Join(' ', phonemes.Where(_ids.ContainsKey).Select(c => _ids[c].ToString(System.Globalization.CultureInfo.InvariantCulture)));
}
