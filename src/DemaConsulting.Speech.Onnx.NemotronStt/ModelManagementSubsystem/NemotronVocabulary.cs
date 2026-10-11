using System.Text;
using System.Text.RegularExpressions;

namespace DemaConsulting.Speech.Onnx.NemotronStt.ModelManagementSubsystem;

/// <summary>
///     The Nemotron model's SentencePiece-style token vocabulary: maps token ids to text,
///     derives the RNN-T blank id and each supported locale's <c>lang_id</c> from the
///     vocabulary itself, and detokenizes decoded token ids into plain text.
/// </summary>
/// <remarks>
///     The downloaded <c>vocab.txt</c> holds one token per line; a token's id is its zero-based
///     line number. Locale marker tokens (for example <c>&lt;en-US&gt;</c>) sit among the
///     ordinary tokens. The encoder's <c>lang_id</c> input is the locale's ordinal within the
///     ordered list formed by <c>&lt;unk&gt;</c> followed by every locale marker token - for
///     this vocabulary <c>&lt;en-GB&gt;</c> is 24 and <c>&lt;en-US&gt;</c> is 25. Deriving the
///     ordinal at load time from the vocabulary file (rather than hard-coding it) means the
///     value can never silently disagree with the downloaded vocabulary. The blank id is the
///     index of the <c>&lt;blank&gt;</c> token (13087 here), likewise derived rather than
///     hard-coded.
/// </remarks>
internal sealed partial class NemotronVocabulary
{
    /// <summary>The SentencePiece word-boundary marker (U+2581) that detokenization turns into a space.</summary>
    private const char WordBoundaryMarker = '\u2581';

    /// <summary>The literal token that marks the RNN-T blank symbol.</summary>
    private const string BlankToken = "<blank>";

    /// <summary>The literal token that precedes the locale markers in the <c>lang_id</c> ordering.</summary>
    private const string UnknownToken = "<unk>";

    /// <summary>The tokens, indexed by id.</summary>
    private readonly string[] _tokens;

    /// <summary>Each locale (for example <c>en-US</c>) mapped to its <c>lang_id</c> ordinal.</summary>
    private readonly Dictionary<string, int> _languageIds = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes a new instance of the <see cref="NemotronVocabulary"/> class from an
    ///     ordered token list.
    /// </summary>
    /// <param name="tokens">The tokens, where a token's index is its id. Must not be null or empty.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="tokens"/> is empty.</exception>
    public NemotronVocabulary(IReadOnlyList<string> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        if (tokens.Count == 0)
        {
            throw new ArgumentException("The vocabulary must contain at least one token.", nameof(tokens));
        }

        _tokens = [.. tokens];
        BlankId = _tokens.Length - 1;

        var ordinal = 0;
        for (var id = 0; id < _tokens.Length; id++)
        {
            var token = _tokens[id];
            if (token == BlankToken)
            {
                BlankId = id;
            }

            if (token == UnknownToken || LocaleTokenPattern().IsMatch(token))
            {
                if (token != UnknownToken)
                {
                    _languageIds[token[1..^1]] = ordinal;
                }

                ordinal++;
            }
        }
    }

    /// <summary>Gets the number of tokens in the vocabulary.</summary>
    public int Count => _tokens.Length;

    /// <summary>Gets the RNN-T blank token id.</summary>
    public int BlankId { get; }

    /// <summary>
    ///     Loads a vocabulary from a downloaded <c>vocab.txt</c> file (one token per line, UTF-8).
    /// </summary>
    /// <param name="path">The absolute path of the vocabulary file.</param>
    /// <returns>The loaded vocabulary.</returns>
    public static NemotronVocabulary Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return new NemotronVocabulary(File.ReadAllLines(path, Encoding.UTF8));
    }

    /// <summary>
    ///     Gets the encoder <c>lang_id</c> for a locale such as <c>en-US</c>.
    /// </summary>
    /// <param name="locale">The locale name, without angle brackets.</param>
    /// <param name="languageId">The locale's <c>lang_id</c> when found; otherwise <c>0</c>.</param>
    /// <returns><see langword="true"/> when the vocabulary declares the locale.</returns>
    public bool TryGetLanguageId(string locale, out int languageId)
    {
        ArgumentNullException.ThrowIfNull(locale);
        return _languageIds.TryGetValue(locale, out languageId);
    }

    /// <summary>
    ///     Converts decoded token ids to plain text: concatenates the token strings, turns the
    ///     SentencePiece word-boundary marker into a space, removes locale marker tokens such as
    ///     <c>&lt;en-US&gt;</c>, collapses runs of whitespace, and trims the result.
    /// </summary>
    /// <param name="tokenIds">The decoded token ids (the blank id is never present).</param>
    /// <returns>The detokenized text, possibly empty.</returns>
    public string Detokenize(IReadOnlyList<int> tokenIds)
    {
        ArgumentNullException.ThrowIfNull(tokenIds);

        var builder = new StringBuilder();
        foreach (var id in tokenIds)
        {
            if ((uint)id < (uint)_tokens.Length)
            {
                builder.Append(_tokens[id]);
            }
        }

        builder.Replace(WordBoundaryMarker, ' ');
        var withoutMarkers = LocaleMarkerPattern().Replace(builder.ToString(), string.Empty);
        return WhitespacePattern().Replace(withoutMarkers, " ").Trim();
    }

    /// <summary>Matches a whole token that is a locale marker (for example <c>&lt;en-US&gt;</c>).</summary>
    [GeneratedRegex("^<[a-z]{2}-[A-Z]{2}>$")]
    private static partial Regex LocaleTokenPattern();

    /// <summary>Matches a locale marker embedded anywhere in detokenized text.</summary>
    [GeneratedRegex("<[a-z]{2}-[A-Z]{2}>")]
    private static partial Regex LocaleMarkerPattern();

    /// <summary>Matches a run of whitespace.</summary>
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
