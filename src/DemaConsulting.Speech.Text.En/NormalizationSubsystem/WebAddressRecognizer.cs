namespace DemaConsulting.Speech.Text.En.NormalizationSubsystem;

/// <summary>
///     Recognizes the tokens that are web addresses - a URL, an e-mail address, or a bare domain name - and says how
///     each piece of one is spoken, so <c>john.smith@example.com</c> is read as <i>john dot smith at example dot
///     com</i> instead of letters and run-together parts.
/// </summary>
/// <remarks>
///     <para>
///     The tokenizer keeps a whole address in one token (<c>https://www.example.com/docs?id=7</c>), so the recognizer
///     looks at one token. The words of the address (<c>example</c>, <c>docs</c>) are left as written for the
///     phonemizer; only the pieces that are not words get a span: the separators (<c>.</c> is <i>dot</i>, <c>/</c> is
///     <i>slash</i>, <c>@</c> is <i>at</i>, <c>-</c> is <i>dash</i>, <c>_</c> is <i>underscore</i>, <c>?</c> is
///     <i>question mark</i>, <c>=</c> is <i>equals</i>, <c>&amp;</c> is <i>and</i>, <c>#</c> is <i>hash</i>, <c>+</c> is
///     <i>plus</i>, <c>%</c> is <i>percent</i>, <c>~</c> is <i>tilde</i>), the scheme letters, which are spelled
///     (<i>H T T P S</i>), the <c>//</c> after the scheme, which is dropped, and a leading <c>www</c>, which is spelled
///     (<i>W W W</i>). A colon is never inside a span (a span never contains one, so the sentence chunker can never cut
///     a span in two), so the colon after the scheme and before a port stays as a pause mark.
///     </para>
///     <para>
///     A token with a scheme (<c>http://</c>, <c>https://</c> or <c>ftp://</c> followed by a letter or digit) is a web
///     address whether or not its host has a dot (<c>http://localhost</c>, <c>ftp://server/path</c>,
///     <c>https://localhost:8080/x</c>); a scheme with nothing after it (<c>http://</c>) is not recognized, because
///     there is no address to speak. Without a scheme, a token is a bare domain only when its last label is one of a
///     short list of top-level domains, so a file name
///     such as <c>report.pdf</c> or a name such as <c>Node.js</c> is never read as an address. The scan reads each
///     character a constant number of times and a token longer than <see cref="MaxTokenLength"/> characters is left
///     alone. The class is stateless and thread-safe.
///     </para>
/// </remarks>
internal static class WebAddressRecognizer
{
    /// <summary>The longest token that is examined.</summary>
    internal const int MaxTokenLength = 512;

    /// <summary>The shortest token that can be an address (<c>a@b.co</c>).</summary>
    private const int MinTokenLength = 6;

    /// <summary>The URL schemes that are recognized before <c>://</c>, in lower case.</summary>
    private static readonly string[] Schemes = ["https", "http", "ftp"];

    /// <summary>The top-level domains that make a bare dotted name a domain, in lower case.</summary>
    private static readonly string[] TopLevelDomains = ["com", "net", "org", "edu", "gov", "io", "dev", "app", "info", "biz", "ai", "uk"];

    /// <summary>
    ///     Finds the spans of a token that is a web address.
    /// </summary>
    /// <param name="token">The token's characters.</param>
    /// <param name="tokenStart">The offset of the token in the text; the spans index the text.</param>
    /// <returns>
    ///     The spans in text order, or <see langword="null"/> when the token is not a web address. No span contains
    ///     whitespace, a comma, a semicolon or a colon.
    /// </returns>
    internal static IReadOnlyList<NumericSpan>? Recognize(ReadOnlySpan<char> token, int tokenStart)
    {
        if (token.Length < MinTokenLength || token.Length > MaxTokenLength)
        {
            return null;
        }

        // The address starts after the scheme and the optional www, which have their own spans
        var spans = new List<NumericSpan>();
        var position = 0;
        var isAddress = false;
        var schemeLength = SchemeLength(token);
        if (schemeLength > 0)
        {
            // A scheme is itself the evidence of a URL, so its host needs no dot (http://localhost, ftp://server/path);
            // the scheme letters are spelled, the colon stays, the two slashes are dropped
            spans.Add(new NumericSpan(tokenStart, schemeLength, SpelledLetters(token[..schemeLength]), NormalizationKind.Initialism));
            spans.Add(new NumericSpan(tokenStart + schemeLength + 1, 2, string.Empty, NormalizationKind.Dropped));
            position = schemeLength + 3;
            isAddress = true;
        }
        else if (token.IndexOfAny('.', '@') < 0)
        {
            // Without a scheme only an e-mail address (an @) or a bare domain (a dot) can be a web address, so a token
            // with neither is rejected before the finer checks
            return null;
        }
        if (HasWwwPrefix(token[position..]))
        {
            spans.Add(new NumericSpan(tokenStart + position, 3, "W W W", NormalizationKind.Initialism));
            position += 3;
            isAddress = true;
        }

        if (!isAddress && !IsEmail(token) && !IsBareDomain(token))
        {
            return null;
        }

        // The rest: every separator is spoken, every run of letters and digits is left to the phonemizer
        for (var i = position; i < token.Length; i++)
        {
            if (SeparatorWords(token[i]) is { } words)
            {
                spans.Add(new NumericSpan(tokenStart + i, 1, words, NormalizationKind.Symbol));
            }
        }

        return spans;
    }

    /// <summary>
    ///     Gets the length of the scheme when the token starts with <c>scheme://</c> followed by a letter or digit.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The length of the scheme letters, or 0 when there is no scheme.</returns>
    private static int SchemeLength(ReadOnlySpan<char> token)
    {
        foreach (var scheme in Schemes)
        {
            var length = scheme.Length;
            if (token.Length > length + 3
                && token[..length].Equals(scheme, StringComparison.OrdinalIgnoreCase)
                && token[length] == ':'
                && token[length + 1] == '/'
                && token[length + 2] == '/'
                && IsAsciiLetterOrDigit(token[length + 3]))
            {
                return length;
            }
        }

        return 0;
    }

    /// <summary>
    ///     Tells whether a text starts with <c>www.</c> and a letter or digit.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> for a leading <c>www.</c> (any case) followed by a letter or digit.</returns>
    private static bool HasWwwPrefix(ReadOnlySpan<char> text) =>
        text.Length > 4 && text[..4].Equals("www.", StringComparison.OrdinalIgnoreCase) && IsAsciiLetterOrDigit(text[4]);

    /// <summary>
    ///     Tells whether a token is an e-mail address: a local part, one <c>@</c> and a domain.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> for <c>local@domain.tld</c>.</returns>
    private static bool IsEmail(ReadOnlySpan<char> token)
    {
        var at = token.IndexOf('@');
        if (at < 1 || at != token.LastIndexOf('@'))
        {
            return false;
        }

        // The local part: letters, digits and . _ % + -, not starting or ending with a dot, no two dots in a row
        var local = token[..at];
        if (local[0] == '.' || local[^1] == '.')
        {
            return false;
        }

        for (var i = 0; i < local.Length; i++)
        {
            var character = local[i];
            var valid = IsAsciiLetterOrDigit(character) || character is '_' or '%' or '+' or '-' || (character == '.' && local[i - 1] != '.');
            if (!valid)
            {
                return false;
            }
        }

        return IsDomain(token[(at + 1)..], requireKnownTopLevelDomain: false);
    }

    /// <summary>
    ///     Tells whether a token is a bare domain name, optionally followed by a path, query or fragment.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> for <c>example.com</c> or <c>example.org/docs</c> (a known top-level domain is required).</returns>
    private static bool IsBareDomain(ReadOnlySpan<char> token)
    {
        var hostEnd = token.IndexOfAny('/', '?', '#');
        var host = hostEnd < 0 ? token : token[..hostEnd];
        return IsDomain(host, requireKnownTopLevelDomain: true);
    }

    /// <summary>
    ///     Tells whether a text is a domain name: two or more labels of ASCII letters, digits and hyphens, separated by
    ///     dots, where no label starts or ends with a hyphen and the last label is letters only.
    /// </summary>
    /// <param name="host">The text.</param>
    /// <param name="requireKnownTopLevelDomain">Whether the last label must be one of <see cref="TopLevelDomains"/>.</param>
    /// <returns><see langword="true"/> for a domain name.</returns>
    private static bool IsDomain(ReadOnlySpan<char> host, bool requireKnownTopLevelDomain)
    {
        var labels = 0;
        var remaining = host;
        while (true)
        {
            var dot = remaining.IndexOf('.');
            var label = dot < 0 ? remaining : remaining[..dot];
            if (!IsLabel(label))
            {
                return false;
            }

            labels++;
            if (dot < 0)
            {
                return labels >= 2 && IsTopLevelDomain(label, requireKnownTopLevelDomain);
            }

            remaining = remaining[(dot + 1)..];
        }
    }

    /// <summary>
    ///     Tells whether a text is a domain label: ASCII letters, digits and hyphens, not empty, not starting or ending with a hyphen.
    /// </summary>
    /// <param name="label">The label.</param>
    /// <returns><see langword="true"/> for a valid label.</returns>
    private static bool IsLabel(ReadOnlySpan<char> label)
    {
        if (label.IsEmpty || label[0] == '-' || label[^1] == '-')
        {
            return false;
        }

        foreach (var character in label)
        {
            if (!IsAsciiLetterOrDigit(character) && character != '-')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Tells whether the last label of a domain is acceptable as a top-level domain.
    /// </summary>
    /// <param name="label">The last label.</param>
    /// <param name="requireKnown">Whether it must be in the list of known top-level domains rather than any two or more letters.</param>
    /// <returns><see langword="true"/> when the label is acceptable.</returns>
    private static bool IsTopLevelDomain(ReadOnlySpan<char> label, bool requireKnown)
    {
        if (requireKnown)
        {
            foreach (var known in TopLevelDomains)
            {
                if (label.Equals(known, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        if (label.Length < 2)
        {
            return false;
        }

        foreach (var character in label)
        {
            if (!(character is >= 'a' and <= 'z' or >= 'A' and <= 'Z'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Writes ASCII letters as a spelled sequence of capital letters separated by single spaces.
    /// </summary>
    /// <param name="letters">The letters.</param>
    /// <returns>The letter sequence, for example <c>H T T P S</c>.</returns>
    private static string SpelledLetters(ReadOnlySpan<char> letters)
    {
        var spoken = new char[(letters.Length * 2) - 1];
        for (var i = 0; i < letters.Length; i++)
        {
            spoken[i * 2] = char.ToUpperInvariant(letters[i]);
            if (i > 0)
            {
                spoken[(i * 2) - 1] = ' ';
            }
        }

        return new string(spoken);
    }

    /// <summary>
    ///     Gets the words a separator character of an address is spoken as.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns>The words, or <see langword="null"/> for a character that is left as written (letters, digits, colons).</returns>
    private static string? SeparatorWords(char character) => character switch
    {
        '.' => "dot",
        '/' => "slash",
        '@' => "at",
        '-' => "dash",
        '_' => "underscore",
        '?' => "question mark",
        '=' => "equals",
        '&' => "and",
        '#' => "hash",
        '+' => "plus",
        '%' => "percent",
        '~' => "tilde",
        _ => null,
    };

    /// <summary>
    ///     Tells whether a character is an ASCII letter or digit.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> for <c>A-Z</c>, <c>a-z</c> and <c>0-9</c>.</returns>
    private static bool IsAsciiLetterOrDigit(char character) =>
        character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';
}
