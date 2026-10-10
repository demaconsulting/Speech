using System.Reflection;
using System.Text.Json;

namespace DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;

/// <summary>
///     Loads Kokoro v1.0's own character-level IPA phoneme vocabulary (one Unicode character
///     mapped to one integer token id) from this package's embedded copy of its
///     <c>tokenizer.json</c>, and converts a phoneme string into the token id sequence the ONNX
///     model expects.
/// </summary>
/// <remarks>
///     Confirmed directly from the real <c>onnx-community/Kokoro-82M-v1.0-ONNX</c>
///     <c>tokenizer.json</c> (downloaded and inspected in this project's development sandbox, and
///     re-embedded verbatim as <c>Resources/kokoro-v1.0-tokenizer.json</c>): the model consumes
///     a simple per-character vocabulary under the <c>model.vocab</c> JSON object - no
///     byte-pair-encoding or sub-word merging, unlike a typical text LLM tokenizer. A character
///     not present in the vocabulary (for example a stray symbol the phonemizer did not produce)
///     is simply dropped, matching the proven Python reference pipeline's own
///     <c>[vocab[c] for c in phonemes if c in vocab]</c> behavior exactly.
/// </remarks>
internal sealed class KokoroPhonemeVocabulary
{
    /// <summary>
    ///     The embedded resource name of Kokoro v1.0's own <c>tokenizer.json</c>, relative to this
    ///     assembly's default namespace.
    /// </summary>
    private const string ResourceName = "DemaConsulting.Speech.Onnx.Kokoro.Resources.kokoro-v1.0-tokenizer.json";

    /// <summary>
    ///     The maximum number of phoneme token ids the ONNX model accepts, leaving room for the
    ///     pad token <c>0</c> the model expects at both the start and end of <c>input_ids</c>
    ///     (confirmed from the real pipeline's own 512-token context window: <c>512 - 2 = 510</c>).
    /// </summary>
    public const int MaxPhonemeTokens = 510;

    /// <summary>The pad token id the model expects at both the start and end of <c>input_ids</c>.</summary>
    public const int PadTokenId = 0;

    private readonly IReadOnlyDictionary<char, int> _vocab;

    /// <summary>Initializes a new instance, loading and parsing the embedded vocabulary immediately.</summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the embedded resource is missing or does not contain the expected
    ///     <c>model.vocab</c> object - both indicate this assembly was built incorrectly, never a
    ///     runtime/environment condition a caller could recover from.
    /// </exception>
    public KokoroPhonemeVocabulary()
    {
        _vocab = LoadVocab();
    }

    private static IReadOnlyDictionary<char, int> LoadVocab()
    {
        using var stream = typeof(KokoroPhonemeVocabulary).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' is missing - this assembly was built incorrectly.");

        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("model", out var model) ||
            !model.TryGetProperty("vocab", out var vocab))
        {
            throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' is missing the expected 'model.vocab' object.");
        }

        var result = new Dictionary<char, int>();
        foreach (var entry in vocab.EnumerateObject())
        {
            // Every real vocab key observed in this model is exactly one UTF-16 code unit (the
            // vocabulary is single Unicode characters, including astral-plane-free IPA symbols) -
            // an entry with any other key length cannot be produced by a phonemizer emitting
            // individual chars, so it is skipped rather than throwing, keeping this loader
            // forward-compatible with a future tokenizer.json revision.
            if (entry.Name.Length == 1)
            {
                result[entry.Name[0]] = entry.Value.GetInt32();
            }
        }

        return result;
    }

    /// <summary>
    ///     Converts a phoneme string into the model's token id sequence, dropping any character
    ///     not present in the vocabulary.
    /// </summary>
    /// <param name="phonemes">The IPA phoneme string to convert. Must not be null.</param>
    /// <returns>
    ///     The token ids for each recognized character, in order, truncated to
    ///     <see cref="MaxPhonemeTokens"/> entries if necessary - never padded with
    ///     <see cref="PadTokenId"/>; the caller adds the leading/trailing pad token itself.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="phonemes"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<int> ToTokenIds(string phonemes)
    {
        ArgumentNullException.ThrowIfNull(phonemes);

        var ids = new List<int>(phonemes.Length);
        foreach (var c in phonemes)
        {
            if (_vocab.TryGetValue(c, out var id))
            {
                ids.Add(id);
            }
        }

        return ids.Count > MaxPhonemeTokens ? ids.GetRange(0, MaxPhonemeTokens) : ids;
    }
}
