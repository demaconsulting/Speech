### WebAddressRecognizer Verification

#### Verification Approach

`WebAddressRecognizer` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/WebAddressRecognizerTests.cs`. The unit is a pure
function of one token's characters and its offset in the text, so the tests pass tokens as strings directly: no
tokenizer, no text and no other unit is involved. The tests compare the spoken texts of the returned spans, joined by a
bar, with the documented reading, and check offsets, order and span shapes where they matter. The sentence-level
rows (`web-url`, `web-email`, `web-www-path`, `web-bare-domain`, `web-not-a-domain`) are rows of the golden file run by
the subsystem tests, and the rule order against the clitic, abbreviation and acronym rules is verified by the
`TextNormalizer` tests (see *NormalizationSubsystem Verification* and *TextNormalizer Verification*).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the test project
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: no test needs network access or files; the unit keeps no state

#### Acceptance Criteria

**Pieces.** A URL with an `http`, `https` or `ftp` scheme in any letter case spells the scheme letters and drops the two
slashes after it; a leading `www` is spelled; an e-mail address and a bare domain with a known top-level domain
(in any letter case) speak every separator as its word (dot, slash, at, dash, underscore, question mark, equals, and,
hash, plus, percent, tilde); the spans index the text through the token offset, are in order and never contain a colon,
whitespace or a digit.

**Boundaries.** File names, dotted names with an unknown ending, numbers, plain words and malformed addresses are not
recognized; a token of 512 characters is recognized, one of 513 or fewer than six characters is not.

#### Test Scenarios

##### Pieces

**Tests**: `WebAddressRecognizer_Recognize_UrlWithScheme_SpellsSchemeAndDropsSlashes` (a three-row theory),
`WebAddressRecognizer_Recognize_LeadingWww_IsSpelledAndSeparatorsAreSpoken`,
`WebAddressRecognizer_Recognize_EmailAddress_SpeaksDotsAndAtSign`,
`WebAddressRecognizer_Recognize_BareDomainWithKnownTopLevelDomain_SpeaksEverySeparator` (a five-row theory),
`WebAddressRecognizer_Recognize_UrlWithPort_SpansAreOrderedAndNeverCoverColonOrDigits`

`https://example.com` gives `H T T P S`, an empty dropped span and `dot`; `HTTP://Example.com` spells `H T T P`;
`https://www.example.com/docs?id=7` adds `W W W` and the separators `dot dot slash question mark equals`. For
`john.smith@example.com` at offset 10 the spans are the three single separators at offsets 14, 20 and 28 (`dot`,
`at`, `dot`). The bare-domain theory covers `example.com`, an upper-case domain, a path, a hyphen, query and
fragment, and every separator of the table in one address. For `http://example.com:8080/x?y=1` no span covers the
colon.

##### Boundaries

**Tests**: `WebAddressRecognizer_Recognize_NotAnAddress_ReturnsNull` (a twelve-row theory),
`WebAddressRecognizer_Recognize_TokenLengthLimits_AreEnforced`

`report.pdf`, `Node.js`, `file.txt`, `John.Smith`, `1.5.2.3`, `hello`, `user@host`, `@home.com`, `a..b@example.com`,
`name@@example.com`, `-bad.com` and a domain with a space in a label return nothing. A domain padded to exactly 512
characters is recognized, one of 513 characters and `a@b.c` are not.
