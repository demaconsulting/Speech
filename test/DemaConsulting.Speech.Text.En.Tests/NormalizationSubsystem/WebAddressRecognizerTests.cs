using DemaConsulting.Speech.Text.En.NormalizationSubsystem;

namespace DemaConsulting.Speech.Text.En.Tests.NormalizationSubsystem;

/// <summary>
///     Unit tests for <see cref="WebAddressRecognizer"/>: the spoken pieces of URLs, e-mail addresses and bare domains,
///     and the tokens that look like addresses but are not (file names, dotted names, malformed shapes).
/// </summary>
public sealed class WebAddressRecognizerTests
{
    /// <summary>Joins the spoken texts of the spans with a vertical bar, so a whole reading is one comparable string.</summary>
    /// <param name="spans">The spans, or null.</param>
    /// <returns>The joined spoken texts, or null when there are no spans.</returns>
    private static string? Spoken(IReadOnlyList<NumericSpan>? spans) =>
        spans is null ? null : string.Join('|', spans.Select(span => span.Spoken));

    /// <summary>Proves a URL with a scheme speaks the scheme letters, drops the two slashes after it and speaks each separator, in any scheme case.</summary>
    /// <param name="token">The token.</param>
    /// <param name="spoken">The expected spoken texts of the spans, joined by a bar.</param>
    [Theory]
    [InlineData("https://example.com", "H T T P S||dot")]
    [InlineData("HTTP://Example.com", "H T T P||dot")]
    [InlineData("ftp://files.example.org/a", "F T P||dot|dot|slash")]
    public void WebAddressRecognizer_Recognize_UrlWithScheme_SpellsSchemeAndDropsSlashes(string token, string spoken)
    {
        // Act
        var spans = WebAddressRecognizer.Recognize(token, 0);

        // Assert
        Assert.Equal(spoken, Spoken(spans));
        Assert.NotNull(spans);
        Assert.Equal(NormalizationKind.Initialism, spans[0].Kind);
        Assert.Equal(NormalizationKind.Dropped, spans[1].Kind);
        Assert.Equal(2, spans[1].Length);
        Assert.Equal(token.IndexOf(':') + 1, spans[1].Start);
    }

    /// <summary>
    ///     Proves a scheme alone makes a URL, so a host without a dot (a local name, a server name, a host with a port)
    ///     is recognized, while a scheme with nothing usable after it is not an address.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="spoken">The expected spoken texts of the spans, joined by a bar, or null when the token is not an address.</param>
    [Theory]
    [InlineData("http://localhost", "H T T P|")]
    [InlineData("ftp://server/path", "F T P||slash")]
    [InlineData("https://localhost:8080/x", "H T T P S||slash")]
    [InlineData("HTTP://Intranet/a-b", "H T T P||slash|dash")]
    [InlineData("http://127.0.0.1/", "H T T P||dot|dot|dot|slash")]
    [InlineData("http://", null)]
    [InlineData("https://.", null)]
    [InlineData("ftp://-x", null)]
    [InlineData("gopher://localhost", null)]
    [InlineData("localhost/path", null)]
    public void WebAddressRecognizer_Recognize_SchemeWithDotlessHost_IsAnAddressOnlyWithAHost(string token, string? spoken)
    {
        // Act
        var spans = WebAddressRecognizer.Recognize(token, 0);

        // Assert
        Assert.Equal(spoken, Spoken(spans));
    }

    /// <summary>Proves a leading www is spelled, with or without a scheme, and that the separators after it are spoken.</summary>
    [Fact]
    public void WebAddressRecognizer_Recognize_LeadingWww_IsSpelledAndSeparatorsAreSpoken()
    {
        // Act
        var withScheme = WebAddressRecognizer.Recognize("https://www.example.com/docs?id=7", 0);
        var withoutScheme = WebAddressRecognizer.Recognize("www.example.org/x-y_z", 0);

        // Assert
        Assert.Equal("H T T P S||W W W|dot|dot|slash|question mark|equals", Spoken(withScheme));
        Assert.Equal("W W W|dot|dot|slash|dash|underscore", Spoken(withoutScheme));
        Assert.NotNull(withoutScheme);
        Assert.Equal(NormalizationKind.Initialism, withoutScheme[0].Kind);
        Assert.All(withoutScheme.Skip(1), span => Assert.Equal(NormalizationKind.Symbol, span.Kind));
    }

    /// <summary>Proves an e-mail address speaks its dots, its at sign and the separators of its local part, and that the spans index the text through the token offset.</summary>
    [Fact]
    public void WebAddressRecognizer_Recognize_EmailAddress_SpeaksDotsAndAtSign()
    {
        // Arrange: the token starts at offset 10 of its text
        const string token = "john.smith@example.com";

        // Act
        var spans = WebAddressRecognizer.Recognize(token, 10);

        // Assert
        Assert.Equal("dot|at|dot", Spoken(spans));
        Assert.NotNull(spans);
        Assert.Equal([14, 20, 28], spans.Select(span => span.Start));
        Assert.All(spans, span => Assert.Equal(1, span.Length));
        Assert.Equal("at|dot", Spoken(WebAddressRecognizer.Recognize("a@b.co", 0)));
        Assert.Equal("underscore|plus|at|dot", Spoken(WebAddressRecognizer.Recognize("a_b+c@example.org", 0)));
    }

    /// <summary>Proves a bare domain with a known top-level domain is read, in any letter case, with its path, query and fragment separators.</summary>
    /// <param name="token">The token.</param>
    /// <param name="spoken">The expected spoken texts of the spans, joined by a bar.</param>
    [Theory]
    [InlineData("example.com", "dot")]
    [InlineData("EXAMPLE.ORG", "dot")]
    [InlineData("example.com/docs", "dot|slash")]
    [InlineData("my-site.dev?a=1#top", "dash|dot|question mark|equals|hash")]
    [InlineData("www.a.com/b_c-d?e=f&g#h+i%j~k", "W W W|dot|dot|slash|underscore|dash|question mark|equals|and|hash|plus|percent|tilde")]
    public void WebAddressRecognizer_Recognize_BareDomainWithKnownTopLevelDomain_SpeaksEverySeparator(string token, string spoken)
    {
        // Act
        var spans = WebAddressRecognizer.Recognize(token, 0);

        // Assert
        Assert.Equal(spoken, Spoken(spans));
    }

    /// <summary>Proves file names, dotted names, numbers and malformed shapes are not web addresses.</summary>
    /// <param name="token">The token.</param>
    [Theory]
    [InlineData("report.pdf")]
    [InlineData("Node.js")]
    [InlineData("file.txt")]
    [InlineData("John.Smith")]
    [InlineData("1.5.2.3")]
    [InlineData("hello")]
    [InlineData("user@host")]
    [InlineData("@home.com")]
    [InlineData("a..b@example.com")]
    [InlineData("name@@example.com")]
    [InlineData("-bad.com")]
    [InlineData("my site.com")]
    public void WebAddressRecognizer_Recognize_NotAnAddress_ReturnsNull(string token)
    {
        // Act
        var spans = WebAddressRecognizer.Recognize(token, 0);

        // Assert
        Assert.Null(spans);
    }

    /// <summary>Proves a token shorter than the shortest address or longer than the examined limit is left alone.</summary>
    [Fact]
    public void WebAddressRecognizer_Recognize_TokenLengthLimits_AreEnforced()
    {
        // Arrange: exactly at the limit, one over it, and one below the shortest address
        var atLimit = new string('a', WebAddressRecognizer.MaxTokenLength - 4) + ".com";
        var overLimit = new string('a', WebAddressRecognizer.MaxTokenLength - 3) + ".com";

        // Act / Assert
        Assert.Equal("dot", Spoken(WebAddressRecognizer.Recognize(atLimit, 0)));
        Assert.Null(WebAddressRecognizer.Recognize(overLimit, 0));
        Assert.Null(WebAddressRecognizer.Recognize("a@b.c", 0));
    }

    /// <summary>Proves the spans are ordered, inside the token, one character each for the separators, and never contain a colon, whitespace or a digit, so a port colon stays a pause mark.</summary>
    [Fact]
    public void WebAddressRecognizer_Recognize_UrlWithPort_SpansAreOrderedAndNeverCoverColonOrDigits()
    {
        // Arrange
        const string token = "http://example.com:8080/x?y=1";

        // Act
        var spans = WebAddressRecognizer.Recognize(token, 0);

        // Assert
        Assert.NotNull(spans);
        var end = 0;
        foreach (var span in spans)
        {
            Assert.True(span.Start >= end, "ordered and not overlapping");
            Assert.True(span.Start + span.Length <= token.Length);
            var covered = token.Substring(span.Start, span.Length);
            Assert.False(covered.Contains(':', StringComparison.Ordinal), "no colon");
            Assert.False(covered.Any(char.IsWhiteSpace), "no whitespace");
            Assert.False(span.Spoken.Any(char.IsDigit), "no digit");
            end = span.Start + span.Length;
        }

        Assert.Equal("H T T P||dot|slash|question mark|equals", Spoken(spans));
    }
}
