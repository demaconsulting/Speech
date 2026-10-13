using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;

/// <summary>
///     Self-contained GPT-2 style byte-level BPE tokenizer driven by a Hugging Face
///     <c>tokenizer.json</c>, reproducing the reference tokenization of the model family without
///     any additional package dependency.
/// </summary>
/// <remarks>
///     <b>Why a hand-written tokenizer</b>: a general-purpose tokenizer package would add a
///     dependency (and a prior attempt with one diverged from the reference ids); the algorithm is
///     small and fully pinned by golden ids generated from the real Hugging Face tokenizer.
///     <para>
///     <b>Algorithm</b>: added (special) tokens are matched atomically first, longest first, before any
///     BPE work; the remaining text is split with the GPT-2 pre-tokenizer pattern, each pre-token is
///     mapped byte-for-byte onto the GPT-2 printable-character alphabet and merged by merge rank.
///     The tokenizer's post-processor appends <c>&lt;|endoftext|&gt;</c> a configurable number of
///     times (two for Chatterbox Turbo: its <c>TemplateProcessing</c> post-processor does so, and
///     the reference pipeline embeds those ids).
///     </para>
///     <para>
///     The type is model-family neutral (no Chatterbox identifiers) so another model sharing this
///     tokenizer scheme could reuse it unchanged. Instances are immutable after construction apart
///     from a thread-safe pre-token cache, so one instance may be shared across threads.
///     </para>
/// </remarks>
internal sealed class ByteLevelBpeTokenizer
{
    /// <summary>Regex time limit.</summary>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The GPT-2 pre-tokenizer pattern (contractions, letters, digits, symbols, whitespace).</summary>
    private static readonly Regex PreTokenizerPattern = new(
        @"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    /// <summary>Maps each byte to its printable GPT-2 alphabet character.</summary>
    private static readonly char[] ByteToChar = BuildByteToChar();

    /// <summary>Token text to id.</summary>
    private readonly Dictionary<string, int> _vocabulary;

    /// <summary>Merge pair (left, right) to merge rank (lower merges first).</summary>
    private readonly Dictionary<(string Left, string Right), int> _mergeRanks;

    /// <summary>Added (special) token text to id.</summary>
    private readonly Dictionary<string, int> _addedTokens;

    /// <summary>Pattern matching any added token, longest alternative first; null when there are none.</summary>
    private readonly Regex? _addedTokenPattern;

    /// <summary>The id appended by the post-processor.</summary>
    private readonly int _endOfTextId;

    /// <summary>How many times the end-of-text id is appended.</summary>
    private readonly int _trailingEndOfTextCount;

    /// <summary>Cache of BPE results per pre-token text.</summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int[]> _cache = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes a new instance of the <see cref="ByteLevelBpeTokenizer"/> class from a
    ///     <c>tokenizer.json</c> document.
    /// </summary>
    /// <param name="tokenizerJson">A readable stream over the JSON document. Must not be null.</param>
    /// <param name="endOfTextToken">The end-of-text token text appended by the post-processor.</param>
    /// <param name="trailingEndOfTextCount">How many end-of-text ids to append to every encoding (0 disables).</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="InvalidDataException">Thrown when the document is not a valid byte-level BPE tokenizer.</exception>
    public ByteLevelBpeTokenizer(Stream tokenizerJson, string endOfTextToken = "<|endoftext|>", int trailingEndOfTextCount = 2)
    {
        ArgumentNullException.ThrowIfNull(tokenizerJson);
        ArgumentNullException.ThrowIfNull(endOfTextToken);
        ArgumentOutOfRangeException.ThrowIfNegative(trailingEndOfTextCount);

        _vocabulary = new Dictionary<string, int>(StringComparer.Ordinal);
        _mergeRanks = [];
        _addedTokens = new Dictionary<string, int>(StringComparer.Ordinal);

        // Parse the document, translating any structural fault into one documented exception
        try
        {
            using var document = JsonDocument.Parse(tokenizerJson);
            var root = document.RootElement;
            var model = root.GetProperty("model");

            foreach (var entry in model.GetProperty("vocab").EnumerateObject())
            {
                _vocabulary[entry.Name] = entry.Value.GetInt32();
            }

            var rank = 0;
            foreach (var merge in model.GetProperty("merges").EnumerateArray())
            {
                var pair = merge.ValueKind == JsonValueKind.String
                    ? (merge.GetString() ?? throw new InvalidDataException("A merge entry is null.")).Split(' ')
                    : [merge[0].GetString() ?? throw new InvalidDataException("A merge entry is null."),
                       merge[1].GetString() ?? throw new InvalidDataException("A merge entry is null.")];
                if (pair.Length != 2)
                {
                    throw new InvalidDataException("A merge entry must contain exactly two pieces.");
                }

                // The first occurrence has the lowest rank and wins, as in the reference implementation
                _mergeRanks.TryAdd((pair[0], pair[1]), rank);
                rank++;
            }

            if (root.TryGetProperty("added_tokens", out var added))
            {
                foreach (var token in added.EnumerateArray())
                {
                    var content = token.GetProperty("content").GetString()
                                  ?? throw new InvalidDataException("An added token has null content.");
                    if (content.Length == 0)
                    {
                        throw new InvalidDataException("An added token has empty content.");
                    }

                    _addedTokens[content] = token.GetProperty("id").GetInt32();
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException
                                       or FormatException or IndexOutOfRangeException)
        {
            throw new InvalidDataException("The tokenizer document is not a valid byte-level BPE tokenizer.", ex);
        }

        if (_vocabulary.Count == 0)
        {
            throw new InvalidDataException("The tokenizer document declares an empty vocabulary.");
        }

        // Added tokens are matched longest-first so a token that prefixes another cannot shadow it
        if (_addedTokens.Count > 0)
        {
            var alternatives = _addedTokens.Keys.OrderByDescending(token => token.Length).Select(Regex.Escape);
            _addedTokenPattern = new Regex(string.Join('|', alternatives), RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
        }

        _trailingEndOfTextCount = trailingEndOfTextCount;
        if (trailingEndOfTextCount > 0 &&
            !_addedTokens.TryGetValue(endOfTextToken, out _endOfTextId) &&
            !_vocabulary.TryGetValue(endOfTextToken, out _endOfTextId))
        {
            throw new InvalidDataException($"The end-of-text token '{endOfTextToken}' is not in the tokenizer.");
        }
    }

    /// <summary>
    ///     Creates a tokenizer from a gzip-compressed <c>tokenizer.json</c> stream, as embedded in this package.
    /// </summary>
    /// <param name="gzipStream">The gzip-compressed document stream. Must not be null.</param>
    /// <param name="trailingEndOfTextCount">How many end-of-text ids to append to every encoding.</param>
    /// <returns>The tokenizer.</returns>
    /// <exception cref="InvalidDataException">Thrown when the stream is not valid gzip or not a valid tokenizer.</exception>
    public static ByteLevelBpeTokenizer FromGzipStream(Stream gzipStream, int trailingEndOfTextCount = 2)
    {
        ArgumentNullException.ThrowIfNull(gzipStream);

        // Decompress fully into memory first so gzip corruption surfaces as InvalidDataException
        using var gzip = new GZipStream(gzipStream, CompressionMode.Decompress);
        using var buffer = new MemoryStream();
        gzip.CopyTo(buffer);
        buffer.Position = 0;
        return new ByteLevelBpeTokenizer(buffer, trailingEndOfTextCount: trailingEndOfTextCount);
    }

    /// <summary>
    ///     Encodes <paramref name="text"/> into token ids, followed by the configured trailing end-of-text ids.
    /// </summary>
    /// <param name="text">The text to encode. Must not be null; empty text yields only the trailing ids.</param>
    /// <returns>The token ids.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a piece has no vocabulary entry (corrupt tokenizer).</exception>
    public long[] Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var ids = new List<long>();

        // Split around atomic added tokens, BPE-encoding only the text between them
        var position = 0;
        if (_addedTokenPattern is not null)
        {
            foreach (Match match in _addedTokenPattern.Matches(text))
            {
                EncodePlain(text.AsSpan(position, match.Index - position), ids);
                ids.Add(_addedTokens[match.Value]);
                position = match.Index + match.Length;
            }
        }

        EncodePlain(text.AsSpan(position), ids);

        // The post-processor appends the end-of-text token after every encoded text
        for (var i = 0; i < _trailingEndOfTextCount; i++)
        {
            ids.Add(_endOfTextId);
        }

        return [.. ids];
    }

    /// <summary>Encodes text that contains no added tokens.</summary>
    private void EncodePlain(ReadOnlySpan<char> text, List<long> ids)
    {
        if (text.IsEmpty)
        {
            return;
        }

        foreach (Match match in PreTokenizerPattern.Matches(text.ToString()))
        {
            foreach (var id in _cache.GetOrAdd(match.Value, EncodePreToken))
            {
                ids.Add(id);
            }
        }
    }

    /// <summary>Runs BPE over one pre-token and returns its ids.</summary>
    private int[] EncodePreToken(string preToken)
    {
        // Map UTF-8 bytes onto the printable alphabet, one symbol per byte
        var bytes = Encoding.UTF8.GetBytes(preToken);
        var symbols = new List<string>(bytes.Length);
        foreach (var b in bytes)
        {
            symbols.Add(ByteToChar[b].ToString());
        }

        // Repeatedly merge the adjacent pair with the lowest rank until none remains
        while (symbols.Count > 1)
        {
            var bestRank = int.MaxValue;
            var bestIndex = -1;
            for (var i = 0; i < symbols.Count - 1; i++)
            {
                if (_mergeRanks.TryGetValue((symbols[i], symbols[i + 1]), out var rank) && rank < bestRank)
                {
                    bestRank = rank;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                break;
            }

            // Merge every occurrence of the winning pair left to right, as the reference does
            var left = symbols[bestIndex];
            var right = symbols[bestIndex + 1];
            var merged = new List<string>(symbols.Count);
            var index = 0;
            while (index < symbols.Count)
            {
                if (index < symbols.Count - 1 && symbols[index] == left && symbols[index + 1] == right)
                {
                    merged.Add(left + right);
                    index += 2;
                }
                else
                {
                    merged.Add(symbols[index]);
                    index++;
                }
            }

            symbols = merged;
        }

        var result = new int[symbols.Count];
        for (var i = 0; i < symbols.Count; i++)
        {
            result[i] = _vocabulary.TryGetValue(symbols[i], out var id)
                ? id
                : throw new InvalidOperationException($"Tokenizer piece '{symbols[i]}' has no vocabulary entry.");
        }

        return result;
    }

    /// <summary>Builds the GPT-2 byte to printable-character table.</summary>
    private static char[] BuildByteToChar()
    {
        // Printable bytes map to themselves; the rest map to consecutive code points from 256
        var table = new char[256];
        var next = 256;
        for (var b = 0; b < 256; b++)
        {
            var printable = (b >= '!' && b <= '~') || (b >= 0xA1 && b <= 0xAC) || (b >= 0xAE && b <= 0xFF);
            table[b] = printable ? (char)b : (char)next++;
        }

        return table;
    }
}
