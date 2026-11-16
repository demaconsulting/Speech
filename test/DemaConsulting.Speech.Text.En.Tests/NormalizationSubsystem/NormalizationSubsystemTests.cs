using System.Diagnostics;
using System.Globalization;
using System.Text;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
using DemaConsulting.Speech.Text.En.NormalizationSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests.NormalizationSubsystem;

/// <summary>
///     Subsystem tests for the normalization subsystem: its units (the alignment model, the text
///     normalizer, the abbreviation resolver and the acronym recognizer) working together over
///     the real tokenizer of the part-of-speech package, which feeds the normalizer exactly as the
///     analyzer does (the tagger is not involved). The golden file, the alignment property tests,
///     the sentence-break table, the linear-time guards and the concurrency test live here.
/// </summary>
public sealed class NormalizationSubsystemTests
{
    /// <summary>The tag placeholder as a string, for building texts.</summary>
    private const string Tag = "\uFFFC";

    /// <summary>The time budget for the linear-time guards (a linear pass takes about a second on an idle machine; the budget leaves room for a heavily loaded build server, while a quadratic pass over these inputs would take minutes).</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(45);

    /// <summary>Proves every row of the golden file normalizes to the expected spoken text, sentences and ambiguity count.</summary>
    [Fact]
    public void NormalizationSubsystem_GoldenFile_EveryRow_MatchesExpectedSpokenTextSentencesAndAmbiguity()
    {
        // Arrange
        var cases = NormalizationHarness.ReadCases("normalization-cases.tsv");

        // Act
        var failures = CheckGoldenRows(cases);

        // Assert
        Assert.True(cases.Count >= 80, "the golden file must keep at least 80 rows");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>Proves every row of the numeric golden file normalizes to the expected spoken text, sentences, ambiguity count and fallback count.</summary>
    [Fact]
    public void NormalizationSubsystem_GoldenFile_NumberCases_EveryRow_MatchesExpectedSpokenTextSentencesAndCounts()
    {
        // Arrange
        var cases = NormalizationHarness.ReadCases("number-cases.tsv");

        // Act
        var failures = CheckGoldenRows(cases);

        // Assert
        Assert.True(cases.Count >= 300, "the numeric golden file must keep at least 300 rows");
        Assert.Equal(cases.Count, cases.Select(row => row.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    ///     Normalizes every row of a golden file and collects all mismatches, so one run reports them all.
    /// </summary>
    /// <param name="cases">The rows.</param>
    /// <returns>One message per mismatch; empty when every row matches.</returns>
    private static List<string> CheckGoldenRows(IReadOnlyList<NormalizationCase> cases)
    {
        var failures = new List<string>();
        foreach (var row in cases)
        {
            var aligned = NormalizationHarness.Normalize(row.Input);
            var annotations = aligned.Spans.Select(span => new SegmentAnnotation(span.Start, span.Length, "en.norm", span.Spoken)).ToList();
            var spoken = NormalizationHarness.ApplyNormalizations(row.Input, annotations);
            var breaks = aligned.ReconcileBreaks(NormalizationHarness.TokenizerBreaks(NormalizationHarness.Neutralize(row.Input)));
            var sentences = NormalizationHarness.JoinSentences(row.Input, breaks);

            if (spoken != row.Spoken)
            {
                failures.Add($"[{row.Id}] spoken: expected '{Escape(row.Spoken)}' but was '{Escape(spoken)}'");
            }

            if (sentences != row.Sentences)
            {
                failures.Add($"[{row.Id}] sentences: expected '{Escape(row.Sentences)}' but was '{Escape(sentences)}'");
            }

            if (aligned.AmbiguousCount != row.Ambiguous)
            {
                failures.Add($"[{row.Id}] ambiguous: expected {row.Ambiguous} but was {aligned.AmbiguousCount}");
            }

            if (aligned.FallbackCount != row.Fallbacks)
            {
                failures.Add($"[{row.Id}] fallbacks: expected {row.Fallbacks} but was {aligned.FallbackCount}");
            }
        }

        return failures;
    }

    /// <summary>Proves the golden file covers every lexicon abbreviation (each form appears in some row's input).</summary>
    [Fact]
    public void NormalizationSubsystem_GoldenFile_Coverage_ContainsEveryLexiconForm()
    {
        // Arrange: every form of the shipped lexicon, read the way the loader reads it
        var assembly = typeof(AbbreviationResolver).Assembly;
        using var stream = assembly.GetManifestResourceStream("DemaConsulting.Speech.Text.En.Resources.abbreviations.tsv");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var forms = reader.ReadToEnd().Split('\n')
            .Where(line => line.Length > 0 && line[0] != '#')
            .Skip(1)
            .Select(line => line.Split('\t')[0])
            .Distinct()
            .ToList();
        var inputs = NormalizationHarness.ReadCases("normalization-cases.tsv")
            .Concat(NormalizationHarness.ReadCases("number-cases.tsv"))
            .Select(row => row.Input).ToList();

        // Act: find the forms no golden input contains
        var missing = forms.Where(form => !inputs.Any(input => input.Contains(form, StringComparison.Ordinal))).ToList();

        // Assert: the abbreviations the golden file does not exercise are exactly those the unit tests cover
        string[] coveredByUnitTests = ["Mrs", "Messrs.", "Messrs", "Mmes.", "Prof", "Rev.", "Gen.", "Lt.", "Cpl.", "Maj.", "Cmdr.", "Adm.", "Gov.", "Pres.", "Supt.", "Ln.", "Rd.", "Ct.", "Pl.", "Sq.", "Hwy.", "Pkwy.", "Apt.", "Ste.", "Bldg.", "Dept.", "Ltd.", "Corp.", "Esq.", "viz.", "misc.", "cf.", "Vol.", "E.g.", "I.e.", "n/a", "c/o", "w/"];
        Assert.Empty(missing.Except(coveredByUnitTests));
    }

    /// <summary>Proves the sentence breaks the analyzer would hand the chunker are right where abbreviation periods are concerned.</summary>
    /// <param name="text">The text.</param>
    /// <param name="expectedSentences">The expected sentences joined by the broken bar.</param>
    [Theory]
    [InlineData("Dr. Smith lives here. He sleeps.", "Dr. Smith lives here.¦He sleeps.")]
    [InlineData("I live on Main St. It is nice.", "I live on Main St.¦It is nice.")]
    [InlineData("Visit St. Louis. Then go.", "Visit St. Louis.¦Then go.")]
    [InlineData("St. Patrick's St. is closed.", "St. Patrick's St. is closed.")]
    [InlineData("Maple Dr. He likes it.", "Maple Dr.¦He likes it.")]
    [InlineData("Dr. Smith lives on Maple Dr. He likes it.", "Dr. Smith lives on Maple Dr.¦He likes it.")]
    [InlineData("(see Elm St.) Then go.", "(see Elm St.)¦Then go.")]
    [InlineData("No. I don't know.", "No.¦I don't know.")]
    [InlineData("John Smith Sr. He left.", "John Smith Sr.¦He left.")]
    [InlineData("pens, etc. The end.", "pens, etc.¦The end.")]
    [InlineData("pens, etc. and more", "pens, etc. and more")]
    [InlineData("He left at 5 p.m. Then slept.", "He left at 5 p.m.¦Then slept.")]
    [InlineData("He lives in the U.S. He works.", "He lives in the U.S.¦He works.")]
    [InlineData("He lives in the U.S. Army camp.", "He lives in the U.S. Army camp.")]
    [InlineData("Sen. Smith spoke.", "Sen. Smith spoke.")]
    [InlineData("John Smith Jr. Ph.D. spoke.", "John Smith Jr. Ph.D. spoke.")]
    [InlineData("Ann Lee Ph.D. M.D. spoke.", "Ann Lee Ph.D. M.D. spoke.")]
    [InlineData("John Smith Jr. Ph.D. He spoke.", "John Smith Jr. Ph.D.¦He spoke.")]
    [InlineData("I met John Smith Jr. He left.", "I met John Smith Jr.¦He left.")]
    public void NormalizationSubsystem_SentenceBreaks_Abbreviations_BreakOnlyWhereTheSentenceEnds(string text, string expectedSentences)
    {
        // Arrange
        var aligned = NormalizationHarness.Normalize(text);
        var tokenizerBreaks = NormalizationHarness.TokenizerBreaks(text);

        // Act
        var breaks = aligned.ReconcileBreaks(tokenizerBreaks);

        // Assert
        Assert.Equal(expectedSentences, NormalizationHarness.JoinSentences(text, breaks));
    }

    /// <summary>Proves random hostile text always yields a result that satisfies every alignment invariant, checked by an applier independent of the production alignment code.</summary>
    [Fact]
    public void NormalizationSubsystem_AlignmentContract_RandomHostileText_SatisfiesAllInvariants()
    {
        // Arrange: a deterministic generator over a deliberately hostile alphabet
        string[] pieces =
        [
            "the", "cat", "Smith", "Louis", "Main", "St.", "Dr.", "Mr.", "Sr.", "No.", "No", "5", "Ave", ".", ",", ";", ":", "!", "?",
            "&", "/", "+", "=", "<", ">", "\u00A9", "\u2122", "\u2022", "\u2019", "\u2018", "'s", "n't", "'re", "\u2019d", "y\u2019all",
            "he/she", "w/o", "N/A", "AT&T", "R&D", "FBI", "US", "us", "IT", "NYC", "NASA", "U.S.", "U.S", "p.m.", "J.", "e.g.", "etc.",
            "\u200B", "\u00AD", "\uFEFF", "a\u200Bb", "\u202A", "\u200D", " ", "  ", "\t", "\n", "\r\n", "\u00A0", "\u2028",
            Tag, Tag + Tag, "\U0001F600", "e\u0301", "\uD83D", "\uDE00", "\0", "\u4F60\u597D", "(", ")", "\"",
        ];
        var random = new Random(20260607);

        for (var iteration = 0; iteration < 500; iteration++)
        {
            var text = string.Concat(Enumerable.Range(0, random.Next(0, 40)).Select(_ => pieces[random.Next(pieces.Length)] + (random.Next(3) == 0 ? string.Empty : " ")));
            var neutralized = NormalizationHarness.Neutralize(text);
            var aligned = NormalizationHarness.Normalize(text);
            var context = $"iteration {iteration}: '{Escape(text)}'";

            // Assert A1 and A2: ascending, disjoint, in bounds, non-empty, no whitespace edge, no placeholder, no cut pair
            var previousEnd = 0;
            NormalizedSpan? previousSpan = null;
            foreach (var span in aligned.Spans)
            {
                Assert.True(span.Length > 0 && span.Start >= previousEnd && span.End <= text.Length, $"order/bounds {context}");
                Assert.False(char.IsWhiteSpace(text[span.Start]) || char.IsWhiteSpace(text[span.End - 1]), $"whitespace edge {context}");
                Assert.DoesNotContain(Tag, text.Substring(span.Start, span.Length), StringComparison.Ordinal);
                Assert.False(span.Start > 0 && char.IsLowSurrogate(text[span.Start]) && char.IsHighSurrogate(text[span.Start - 1]), $"cut pair at start {context}");
                Assert.False(span.End < text.Length && char.IsHighSurrogate(text[span.End - 1]) && char.IsLowSurrogate(text[span.End]), $"cut pair at end {context}");
                previousEnd = span.End;

                // A5': chunk-safe, no whitespace, never a semicolon, a comma or colon only between two ASCII digits
                AssertChunkSafe(text, span, context);

                // A7 and A10: only dropped spans are empty and only invisible characters are dropped; no placeholder in a value
                Assert.Equal(span.Kind == NormalizationKind.Dropped, span.Spoken.Length == 0);
                Assert.DoesNotContain(Tag, span.Spoken, StringComparison.Ordinal);
                if (span.Kind == NormalizationKind.Dropped)
                {
                    Assert.All(text.Substring(span.Start, span.Length), character => Assert.True(IsInvisibleOrBullet(character), $"dropped a visible character {context}"));
                }

                // A6 and A10: a value has a leading or trailing space only as the separator a touching letter or digit needs;
                // a leading space is also the separator after a directly adjacent replacement that ends with a letter
                if (span.Kind is not (NormalizationKind.Dropped or NormalizationKind.Apostrophe))
                {
                    var previousEndsWithLetter = previousSpan is { } before && before.End == span.Start && before.Spoken.Length > 0 && char.IsLetter(before.Spoken[^1]);
                    var leftNeeds = (span.Start > 0 && char.IsLetterOrDigit(text[span.Start - 1])) || previousEndsWithLetter;
                    var rightTouches = span.End < text.Length && char.IsLetterOrDigit(text[span.End]);
                    var core = span.Spoken.Trim();
                    Assert.Equal(leftNeeds && char.IsLetter(core[0]), span.Spoken.StartsWith(' '));
                    Assert.Equal(rightTouches && char.IsLetter(core[^1]), span.Spoken.EndsWith(' '));
                }

                // exactly one separator between adjacent replacements: never a trailing and a leading space across the seam
                if (previousSpan is { } seam && seam.End == span.Start)
                {
                    Assert.False(seam.Spoken.EndsWith(' ') && span.Spoken.StartsWith(' '), $"double space at a seam {context}");
                }

                previousSpan = span;
            }

            // Assert A3: the segments tile the text exactly and identity segments are the source
            var position = 0;
            foreach (var segment in aligned.Segments())
            {
                Assert.Equal(position, segment.Start);
                if (segment.Kind is null)
                {
                    Assert.Equal(neutralized.Substring(segment.Start, segment.Length), segment.Spoken);
                }

                position += segment.Length;
            }

            Assert.Equal(neutralized.Length, position);

            // Assert A4 and A7: an applier that never touches AlignedText agrees with it, and nothing outside a span changed
            var annotations = aligned.Spans.Select(span => new SegmentAnnotation(span.Start, span.Length, "en.norm", span.Spoken)).ToList();
            var applied = NormalizationHarness.ApplyNormalizations(text, annotations);
            Assert.Equal(aligned.ApplySpokenForms(), applied.Replace(Tag, " ", StringComparison.Ordinal));
            var outside = new StringBuilder();
            foreach (var gap in aligned.Segments().Where(segment => segment.Kind is null))
            {
                outside.Append(neutralized, gap.Start, gap.Length);
            }

            Assert.Equal(aligned.Source.Length - aligned.Spans.Sum(span => span.Length), outside.Length);

            // Assert A8: forced and suppressed breaks, and the reconciled breaks, are ascending, unique, inside the text and on token starts
            var reconciled = aligned.ReconcileBreaks(NormalizationHarness.TokenizerBreaks(neutralized));
            foreach (var list in new IReadOnlyList<int>[] { aligned.ForcedBreaks, aligned.SuppressedBreaks, reconciled })
            {
                Assert.Equal(list.OrderBy(value => value).Distinct(), list);
                Assert.All(list, offset =>
                {
                    Assert.InRange(offset, 1, text.Length - 1);
                    Assert.False(char.IsWhiteSpace(text[offset]) || text[offset] == Tag[0], $"break on whitespace or placeholder {context}");
                });
            }
        }
    }

    /// <summary>Proves analyzing the same text repeatedly and concurrently through the shared default normalizer gives identical results.</summary>
    [Fact]
    public async Task NormalizationSubsystem_Normalize_ConcurrentRequests_ReturnIdenticalResults()
    {
        // Arrange
        const string text = "Dr. Smith & Mr. Jones live on Main St. The FBI said it\u2019s w/o proof, etc. He left at 5 p.m. No. I don't know.";
        var expected = Key(NormalizationHarness.Normalize(text, classifyClitics: true));

        // Act: many tasks race on the lazily created default lexicons
        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(
                () => Enumerable.Range(0, 20).Select(_ => Key(NormalizationHarness.Normalize(text, classifyClitics: true))).ToList(),
                TestContext.Current.CancellationToken))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results.SelectMany(keys => keys), key => Assert.Equal(expected, key));
    }

    /// <summary>Proves one megabyte of realistic and of adversarially repetitive text normalizes within the time budget (linear time).</summary>
    [Fact]
    public void NormalizationSubsystem_Normalize_LargeRepetitiveTexts_CompleteWithinBudget()
    {
        // Arrange: realistic text and four repetitive texts that would expose any quadratic look-around
        const int size = 1_000_000;
        string[] units =
        [
            "Dr. Smith lives on Maple Dr. He likes it, etc. The FBI said it\u2019s w/o proof & more. ",
            "St. ",
            "A. ",
            "& ",
            "Dr. He ",
        ];
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        foreach (var unit in units)
        {
            var text = string.Concat(Enumerable.Repeat(unit, (size / unit.Length) + 1));
            var tokens = NormalizationHarness.Tokenize(text);

            // Act
            var stopwatch = Stopwatch.StartNew();
            var aligned = TextNormalizer.Default.Normalize(text, tokens, classifyClitics: true, cancellation.Token);
            stopwatch.Stop();

            // Assert
            Assert.True(stopwatch.Elapsed < Budget, $"'{unit}' took {stopwatch.Elapsed}");
            Assert.NotEmpty(aligned.Spans);
        }
    }

    /// <summary>Proves one huge token (so any per-character look-around would be quadratic) of an ampersand, a dropped character or an apostrophe pattern normalizes within the time budget.</summary>
    /// <param name="unit">The repeated unit that forms the single huge token.</param>
    [Theory]
    [InlineData("a&")]
    [InlineData("a\u200B")]
    [InlineData("a\u2019b")]
    [InlineData("&")]
    public void NormalizationSubsystem_Normalize_OneHugeToken_CompletesWithinBudget(string unit)
    {
        // Arrange: the tokenizer is not under test for giant tokens, so the token list is built by hand
        var text = string.Concat(Enumerable.Repeat(unit, 1_000_000 / unit.Length));
        NormalizationToken[] tokens = [new NormalizationToken(0, text.Length, true)];
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        // Act
        var stopwatch = Stopwatch.StartNew();
        var aligned = TextNormalizer.Default.Normalize(text, tokens, classifyClitics: false, cancellation.Token);
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.Elapsed < Budget, $"took {stopwatch.Elapsed}");
        Assert.NotNull(aligned);
    }

    /// <summary>Proves a hand-built surrogate pair next to every rule is never cut by a span.</summary>
    [Fact]
    public void NormalizationSubsystem_Normalize_SurrogatePairsNextToRules_AreNeverCut()
    {
        // Arrange
        string[] texts =
        [
            "\U0001F600Dr. Smith \U0001F600& \U0001F600St. \U0001F600FBI\U0001F600 it\u2019s\U0001F600",
            "x\U0001F600\u2122 \U0001F600\u2019s \u200B\U0001F600 \U0001F600/\U0001F600 \U0001F600U.S.\U0001F600",
            "\uD83D Dr. \uDE00 & \uD83D\u2122\uDE00 St.\uD83D",
        ];

        foreach (var text in texts)
        {
            // Act
            var aligned = NormalizationHarness.Normalize(text);

            // Assert
            foreach (var span in aligned.Spans)
            {
                Assert.False(span.Start > 0 && char.IsLowSurrogate(text[span.Start]) && char.IsHighSurrogate(text[span.Start - 1]));
                Assert.False(span.End < text.Length && char.IsHighSurrogate(text[span.End - 1]) && char.IsLowSurrogate(text[span.End]));
            }
        }
    }

    /// <summary>Proves combining marks inside and around abbreviations do not make a span start or end inside a character.</summary>
    [Fact]
    public void NormalizationSubsystem_Normalize_CombiningMarksAroundAbbreviations_KeepSpansOnTokenEdges()
    {
        // Arrange
        const string text = "Cafe\u0301 Dr. Smith\u0301 lives on Mai\u0301n St. It\u0301 is nice.";

        // Act
        var aligned = NormalizationHarness.Normalize(text);

        // Assert: both abbreviations resolve and every span is whitespace-bounded text
        Assert.Equal(2, aligned.Spans.Count(span => span.Kind == NormalizationKind.Abbreviation));
        Assert.All(aligned.Spans, span => Assert.False(char.IsWhiteSpace(text[span.Start]) || char.IsWhiteSpace(text[span.End - 1])));
    }

    /// <summary>Proves a text with no abbreviation, symbol or initialism yields no span and no break edit.</summary>
    [Fact]
    public void NormalizationSubsystem_Normalize_PlainText_ChangesNothing()
    {
        // Arrange
        const string text = "The quick brown fox jumps over the lazy dog. It was fun, wasn't it?";

        // Act
        var aligned = NormalizationHarness.Normalize(text);

        // Assert
        Assert.Empty(aligned.Spans);
        Assert.Empty(aligned.ForcedBreaks);
        Assert.Empty(aligned.SuppressedBreaks);
        Assert.Equal(text, aligned.ApplySpokenForms());
    }

    /// <summary>Proves every year from 1000 to 2099 reads as the independent reference says under a year cue, a quantity cue and no cue, and the default is counted only where the readings differ.</summary>
    [Fact]
    public void NormalizationSubsystem_Years_EveryYearFrom1000To2099_ReadAsReference()
    {
        // Act / Assert
        for (var year = 1000; year <= 2099; year++)
        {
            var digits = year.ToString(CultureInfo.InvariantCulture);
            var yearWords = ReferenceNumberWords.Year((ulong)year);
            var cardinalWords = ReferenceNumberWords.Cardinal((ulong)year);

            // A year cue: the year reading, never counted
            var cued = NormalizationHarness.Normalize("in " + digits);
            Assert.True($"in {yearWords}" == cued.ApplySpokenForms(), $"cued {year}");
            Assert.Equal(0, cued.AmbiguousCount);

            // A quantity cue: the cardinal, never counted
            var counted = NormalizationHarness.Normalize("about " + digits);
            Assert.True($"about {cardinalWords}" == counted.ApplySpokenForms(), $"quantity {year}");
            Assert.Equal(0, counted.AmbiguousCount);

            // No cue: a year from 1100, a cardinal below; counted exactly when the two readings differ
            var bare = NormalizationHarness.Normalize(digits);
            Assert.True((year >= 1100 ? yearWords : cardinalWords) == bare.ApplySpokenForms(), $"bare {year}");
            Assert.True((yearWords != cardinalWords ? 1 : 0) == bare.AmbiguousCount, $"ambiguity {year}");
        }
    }

    /// <summary>Proves every time from 0:00 to 23:59 reads as the independent reference says, with a leading zero and with a glued meridiem.</summary>
    [Fact]
    public void NormalizationSubsystem_Times_EveryHourAndMinute_ReadAsReference()
    {
        // Act / Assert
        for (var hour = 0; hour < 24; hour++)
        {
            for (var minute = 0; minute < 60; minute++)
            {
                var minuteText = minute.ToString("00", CultureInfo.InvariantCulture);
                var plain = $"{hour}:{minuteText}";
                Assert.True(
                    ReferenceNumberWords.Clock((ulong)hour, (ulong)minute, false, false) == NormalizationHarness.Normalize(plain).ApplySpokenForms(),
                    plain);

                if (hour < 10)
                {
                    var padded = $"0{hour}:{minuteText}";
                    Assert.True(
                        ReferenceNumberWords.Clock((ulong)hour, (ulong)minute, true, false) == NormalizationHarness.Normalize(padded).ApplySpokenForms(),
                        padded);
                }

                if (hour is >= 1 and <= 12)
                {
                    Assert.True(
                        ReferenceNumberWords.Clock((ulong)hour, (ulong)minute, false, true) + " P M" == NormalizationHarness.Normalize(plain + "pm").ApplySpokenForms(),
                        plain + "pm");
                }
            }
        }
    }

    /// <summary>Proves currency amounts are singular only for exactly one, speak minor units for exactly two fraction digits, and yen never does.</summary>
    [Fact]
    public void NormalizationSubsystem_Currency_SingularPluralAndCents_AreCorrect()
    {
        // Arrange: (symbol, singular, plural, minor singular, minor plural)
        (string Symbol, string Singular, string Plural, string? MinorSingular, string? MinorPlural)[] currencies =
        [
            ("$", "dollar", "dollars", "cent", "cents"),
            ("\u20AC", "euro", "euros", "cent", "cents"),
            ("\u00A3", "pound", "pounds", "penny", "pence"),
            ("\u00A5", "yen", "yen", null, null),
        ];
        ulong[] wholes = [0, 1, 2, 5, 10, 21, 100, 101, 1000, 1234];
        ulong[] cents = [0, 1, 2, 5, 10, 50, 99];

        // Act / Assert
        foreach (var (symbol, singular, plural, minorSingular, minorPlural) in currencies)
        {
            foreach (var whole in wholes)
            {
                // A whole amount: singular only for exactly one
                var amount = NormalizationHarness.Normalize($"{symbol}{whole}").ApplySpokenForms();
                Assert.True($"{ReferenceNumberWords.Cardinal(whole)} {(whole == 1 ? singular : plural)}" == amount, $"{symbol}{whole}");

                foreach (var minor in cents)
                {
                    var text = $"{symbol}{whole}.{minor:00}";
                    var spoken = NormalizationHarness.Normalize(text).ApplySpokenForms();
                    string expected;
                    if (minorPlural is null)
                    {
                        // Yen takes no minor unit: a decimal with its fraction digits spelled
                        var digits = string.Join(' ', $"{minor:00}".Select(digit => ReferenceNumberWords.Cardinal((ulong)(digit - '0'))));
                        expected = $"{ReferenceNumberWords.Cardinal(whole)} point {digits} {plural}";
                    }
                    else if (whole == 0 && minor > 0)
                    {
                        expected = $"{ReferenceNumberWords.Cardinal(minor)} {(minor == 1 ? minorSingular : minorPlural)}";
                    }
                    else
                    {
                        expected = $"{ReferenceNumberWords.Cardinal(whole)} {(whole == 1 ? singular : plural)}";
                        if (minor > 0)
                        {
                            expected += $" and {ReferenceNumberWords.Cardinal(minor)} {(minor == 1 ? minorSingular : minorPlural)}";
                        }
                    }

                    Assert.True(expected == spoken, $"{text}: expected '{expected}' but was '{spoken}'");
                }
            }
        }
    }

    /// <summary>Proves every unit is singular for exactly one and plural otherwise, glued and spaced, and its abbreviation period is consumed mid-sentence and kept at the end.</summary>
    [Fact]
    public void NormalizationSubsystem_Units_SingularPluralAndPeriods_AreCorrect()
    {
        // Arrange: written form, singular, plural, whether an abbreviation period is allowed
        (string Written, string Singular, string Plural, bool Period)[] units =
        [
            ("mm", "millimeter", "millimeters", false), ("cm", "centimeter", "centimeters", false), ("km", "kilometer", "kilometers", false),
            ("ft", "foot", "feet", true), ("Ft", "foot", "feet", true), ("yd", "yard", "yards", true), ("mi", "mile", "miles", true),
            ("mph", "mile per hour", "miles per hour", false), ("km/h", "kilometer per hour", "kilometers per hour", false),
            ("mg", "milligram", "milligrams", false), ("kg", "kilogram", "kilograms", false), ("lb", "pound", "pounds", true),
            ("lbs", "pound", "pounds", true), ("oz", "ounce", "ounces", true), ("ml", "milliliter", "milliliters", false),
            ("KB", "kilobyte", "kilobytes", false), ("MB", "megabyte", "megabytes", false), ("GB", "gigabyte", "gigabytes", false),
            ("TB", "terabyte", "terabytes", false), ("Hz", "hertz", "hertz", false), ("kHz", "kilohertz", "kilohertz", false),
            ("MHz", "megahertz", "megahertz", false), ("GHz", "gigahertz", "gigahertz", false), ("ms", "millisecond", "milliseconds", false),
            ("hr", "hour", "hours", true), ("hrs", "hour", "hours", true), ("min", "minute", "minutes", true),
            ("\u00B0", "degree", "degrees", false), ("\u00B0C", "degree Celsius", "degrees Celsius", false), ("\u00B0F", "degree Fahrenheit", "degrees Fahrenheit", false),
        ];

        // Act / Assert
        foreach (var (written, singular, plural, period) in units)
        {
            // Glued and spaced (a bare degree sign is only tested glued: the tokenizer decides how it splits a spaced one)
            Assert.Equal($"one {singular}", NormalizationHarness.Normalize($"1{written}").ApplySpokenForms());
            Assert.Equal($"two {plural}", NormalizationHarness.Normalize($"2{written}").ApplySpokenForms());
            Assert.Equal($"one {singular} and two {plural}", NormalizationHarness.Normalize($"1{written} and 2{written}").ApplySpokenForms());
            if (written != "\u00B0")
            {
                Assert.Equal($"one {singular}", NormalizationHarness.Normalize($"1 {written}").ApplySpokenForms());
                Assert.Equal($"two {plural}", NormalizationHarness.Normalize($"2 {written}").ApplySpokenForms());
            }

            if (period)
            {
                // The period is consumed in the middle of a sentence and kept at its end
                Assert.Equal($"a two {plural} b", NormalizationHarness.Normalize($"a 2 {written}. b").ApplySpokenForms());
                Assert.Equal($"a two {plural}.", NormalizationHarness.Normalize($"a 2 {written}.").ApplySpokenForms());
                Assert.Equal($"a two {plural} b", NormalizationHarness.Normalize($"a 2{written}. b").ApplySpokenForms());
            }
        }
    }

    /// <summary>Proves every numeric spoken value, over the golden file and generated numeric text, contains no digit and only words of the independent reference's closed vocabulary.</summary>
    [Fact]
    public void NormalizationSubsystem_Numbers_NoDigitsInSpokenText_AndOnlyClosedVocabulary()
    {
        // Arrange: every golden input, plus seeded random numeric text
        var texts = NormalizationHarness.ReadCases("number-cases.tsv").Select(row => row.Input).ToList();
        var random = new Random(20260803);
        texts.AddRange(Enumerable.Range(0, 400).Select(_ => RandomNumericText(random, 12)));
        var checkedSpans = 0;

        // Act / Assert
        foreach (var text in texts)
        {
            var aligned = NormalizationHarness.Normalize(text);
            foreach (var span in aligned.Spans.Where(span => span.Kind is NormalizationKind.Number or NormalizationKind.Currency or NormalizationKind.DateTime or NormalizationKind.Unit))
            {
                checkedSpans++;
                Assert.DoesNotContain(span.Spoken, character => character is >= '0' and <= '9');
                Assert.True(ReferenceNumberWords.IsInVocabulary(span.Spoken), $"'{span.Spoken}' leaves the closed vocabulary in '{Escape(text)}'");
                Assert.True(span.Spoken.Trim().All(character => character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '\'' or ' '), $"'{span.Spoken}'");
                Assert.True(span.Spoken.Length <= NumberVerbalizer.MaxSpokenLength);
            }
        }

        Assert.True(checkedSpans > 1_000, "the sample must exercise many numeric spans");
    }

    /// <summary>Proves the sentence breaks stay where the sentence ends and are neither created nor suppressed wrongly around numbers, decimals, dates and units.</summary>
    [Fact]
    public void NormalizationSubsystem_SentenceBreaks_NumbersAndDates_BreakOnlyWhereTheSentenceEnds()
    {
        // Arrange: (text, expected sentences joined by the broken bar)
        (string Text, string Sentences)[] cases =
        [
            ("It costs $1.25. Then $2.", "It costs $1.25.¦Then $2."),
            ("Pi is 3.14. Then 2.71.", "Pi is 3.14.¦Then 2.71."),
            ("It is 3.5 miles long.", "It is 3.5 miles long."),
            ("No. 5 is here.", "No. 5 is here."),
            ("Item 5.", "Item 5."),
            ("He left at 5 p.m. He left.", "He left at 5 p.m.¦He left."),
            ("Due Jan. 5, 2024. He left.", "Due Jan. 5, 2024.¦He left."),
            ("Sat. 5 Jan. 2024 and more.", "Sat. 5 Jan. 2024 and more."),
            ("He is 5 ft. tall.", "He is 5 ft. tall."),
            ("He is 5 ft. Then left.", "He is 5 ft.¦Then left."),
            ("Version 3.2.1 is out. It works.", "Version 3.2.1 is out.¦It works."),
            ("See 5-10. Then go.", "See 5-10.¦Then go."),
            ("Call 555-1234. Then go.", "Call 555-1234.¦Then go."),
            ("Due 12/25/2024. Then rest.", "Due 12/25/2024.¦Then rest."),
            ("Meet at 5:30 pm. Then go.", "Meet at 5:30 pm.¦Then go."),
            ("He paid 50 % of it. Then left.", "He paid 50 % of it.¦Then left."),
            ("Sept. 11, 2001 was sad. Then it ended.", "Sept. 11, 2001 was sad.¦Then it ended."),
            ("He left Mon. He came Tue.", "He left Mon.¦He came Tue."),
            ("Mon. 5 Jan. is the day. Then go.", "Mon. 5 Jan. is the day.¦Then go."),
            ("The 1990s. Then the 2000s.", "The 1990s.¦Then the 2000s."),
            ("It was in 1999. He left.", "It was in 1999.¦He left."),
            ("We finished in May. 5 people stayed.", "We finished in May.¦5 people stayed."),
            ("It is January. 1999 people came.", "It is January.¦1999 people came."),
            ("Jan. 5. He left.", "Jan. 5.¦He left."),
            ("Month: May\n\n5 people stayed.", "Month: May¦5 people stayed."),
        ];

        // Act / Assert
        foreach (var (text, sentences) in cases)
        {
            var aligned = NormalizationHarness.Normalize(text);
            var breaks = aligned.ReconcileBreaks(NormalizationHarness.TokenizerBreaks(text));
            Assert.True(sentences == NormalizationHarness.JoinSentences(text, breaks), $"'{text}'");
        }
    }

    /// <summary>Proves random numeric and hostile text always yields spans that satisfy every alignment invariant (A1 to A3, A5', A6, A8b, A10), deterministically, and the analyzer's annotations come out ordered.</summary>
    [Fact]
    public void NormalizationSubsystem_AlignmentContract_RandomNumericText_SatisfiesAllInvariants()
    {
        // Arrange
        var random = new Random(20260804);
        var analyzer = new EnglishTextAnalyzer();

        for (var iteration = 0; iteration < 600; iteration++)
        {
            var text = RandomNumericText(random, 24);
            var neutralized = NormalizationHarness.Neutralize(text);
            var aligned = NormalizationHarness.Normalize(text);
            var context = $"iteration {iteration}: '{Escape(text)}'";

            // Assert: deterministic
            Assert.Equal(Key(aligned), Key(NormalizationHarness.Normalize(text)));

            // Assert A1, A2, A5', A6, A10 on every span
            var previousEnd = 0;
            NormalizedSpan? previousSpan = null;
            foreach (var span in aligned.Spans)
            {
                Assert.True(span.Length > 0 && span.Start >= previousEnd && span.End <= text.Length, $"order or bounds {context}");
                Assert.False(char.IsWhiteSpace(text[span.Start]) || char.IsWhiteSpace(text[span.End - 1]), $"whitespace edge {context}");
                Assert.DoesNotContain(Tag, text.Substring(span.Start, span.Length), StringComparison.Ordinal);
                Assert.False(span.Start > 0 && char.IsLowSurrogate(text[span.Start]) && char.IsHighSurrogate(text[span.Start - 1]), $"cut pair at start {context}");
                Assert.False(span.End < text.Length && char.IsHighSurrogate(text[span.End - 1]) && char.IsLowSurrogate(text[span.End]), $"cut pair at end {context}");
                previousEnd = span.End;
                AssertChunkSafe(text, span, context);
                Assert.DoesNotContain(Tag, span.Spoken, StringComparison.Ordinal);
                Assert.Equal(span.Kind == NormalizationKind.Dropped, span.Spoken.Length == 0);

                // A10: numeric kinds speak letters, spaces and an apostrophe only
                if (span.Kind is NormalizationKind.Number or NormalizationKind.Currency or NormalizationKind.DateTime or NormalizationKind.Unit)
                {
                    Assert.True(span.Spoken.Length <= NumberVerbalizer.MaxSpokenLength, $"too long {context}");
                    Assert.True(span.Spoken.Trim().Length > 0 && span.Spoken.All(character => character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '\'' or ' '), $"spoken shape '{span.Spoken}' {context}");
                }

                // A6: padding only as the separator a touching letter, digit or previous replacement needs
                if (span.Kind is not (NormalizationKind.Dropped or NormalizationKind.Apostrophe))
                {
                    var previousEndsWithLetter = previousSpan is { } before && before.End == span.Start && before.Spoken.Length > 0 && char.IsLetter(before.Spoken[^1]);
                    var leftNeeds = (span.Start > 0 && char.IsLetterOrDigit(text[span.Start - 1])) || previousEndsWithLetter;
                    var rightTouches = span.End < text.Length && char.IsLetterOrDigit(text[span.End]);
                    var core = span.Spoken.Trim();
                    Assert.Equal(leftNeeds && char.IsLetter(core[0]), span.Spoken.StartsWith(' '));
                    Assert.Equal(rightTouches && char.IsLetter(core[^1]), span.Spoken.EndsWith(' '));
                }

                if (previousSpan is { } seam && seam.End == span.Start)
                {
                    Assert.False(seam.Spoken.EndsWith(' ') && span.Spoken.StartsWith(' '), $"double space at a seam {context}");
                }

                previousSpan = span;
            }

            // Assert A3 and A7: the segments tile the text, identity segments are the source, an independent applier agrees
            var position = 0;
            foreach (var segment in aligned.Segments())
            {
                Assert.Equal(position, segment.Start);
                if (segment.Kind is null)
                {
                    Assert.Equal(neutralized.Substring(segment.Start, segment.Length), segment.Spoken);
                }

                position += segment.Length;
            }

            Assert.Equal(neutralized.Length, position);
            var annotations = aligned.Spans.Select(span => new SegmentAnnotation(span.Start, span.Length, "en.norm", span.Spoken)).ToList();
            Assert.Equal(aligned.ApplySpokenForms(), NormalizationHarness.ApplyNormalizations(text, annotations).Replace(Tag, " ", StringComparison.Ordinal));

            // Assert A8 and A8b: breaks are on token starts, and no reconciled break lies strictly inside a span
            var reconciled = aligned.ReconcileBreaks(NormalizationHarness.TokenizerBreaks(neutralized));
            Assert.All(reconciled, offset =>
            {
                Assert.InRange(offset, 1, text.Length - 1);
                Assert.False(char.IsWhiteSpace(text[offset]) || text[offset] == Tag[0], $"break on whitespace or placeholder {context}");
                Assert.DoesNotContain(aligned.Spans, span => span.Start < offset && offset < span.End);
            });

            // Assert: through the analyzer, the annotations are ordered by start, the part of speech before the normalization at an equal start
            if (iteration % 6 == 0)
            {
                var result = analyzer.Analyze(text, TestContext.Current.CancellationToken);
                for (var i = 1; i < result.Annotations.Count; i++)
                {
                    var previous = result.Annotations[i - 1];
                    var current = result.Annotations[i];
                    Assert.True(
                        previous.Start < current.Start || (previous.Start == current.Start && previous.Kind == TextEnAnnotations.PartOfSpeech && current.Kind is TextEnAnnotations.Normalized or TextEnAnnotations.SpellOut),
                        $"annotation order {context}");
                }

                Assert.Equal(
                    aligned.Spans.Select(span => (span.Start, span.Length, span.Spoken)),
                    result.Annotations.Where(annotation => annotation.Kind is TextEnAnnotations.Normalized or TextEnAnnotations.SpellOut).Select(annotation => (annotation.Start, annotation.Length, annotation.Value)));
            }
        }
    }

    /// <summary>
    ///     Proves, with the real core <c>SentenceChunker.ChunkAligned</c>, that every normalization annotation of random numeric text
    ///     lands in exactly one chunk on the same characters, at every length budget, and nothing is invented.
    /// </summary>
    [Fact]
    public void NormalizationSubsystem_NumericSpans_RealChunker_KeepsEveryAnnotationInExactlyOneChunk()
    {
        // Arrange: numeric constructs joined by clause punctuation, so the clause pass and the budget pass both cut often
        string[] pieces =
        [
            "$1,234.50", "$5 million", "5:30pm", "5:30 pm", "January 5, 2024", "12/25/2024", "50 %", "50%", "3 1/2 kg", "5 ft. tall", "the 1990s",
            "#5", "1st", "22nd", "3.5", "\u00BD", "5\u201310", "555-123-4567", "192.168.0.1", "'90s", "\u20AC5.50", "\u00A31.01", "72\u00B0F",
            "in 1999", "about 1500 people", "007", "1,000,000", "Jan. 5", "Sat. 7:30", "5 USD", "word", "and", "then", "kg", "at",
        ];
        string[] separators = [" ", " ", " ", ", ", "; ", ": ", ". ", ", "];
        var random = new Random(20260805);

        foreach (var budget in new[] { 200, 120, 60, 25 })
        {
            for (var iteration = 0; iteration < 150; iteration++)
            {
                var builder = new StringBuilder();
                for (var i = 0; i < random.Next(3, 26); i++)
                {
                    builder.Append(pieces[random.Next(pieces.Length)]).Append(separators[random.Next(separators.Length)]);
                }

                var text = builder.ToString().TrimEnd();
                var aligned = NormalizationHarness.Normalize(text);
                var annotations = aligned.Spans.Select(span => new SegmentAnnotation(span.Start, span.Length, "en.norm", span.Spoken)).ToList();
                var breaks = aligned.ReconcileBreaks(NormalizationHarness.TokenizerBreaks(text));

                // Act
                var chunks = SentenceChunker.ChunkAligned(text, annotations, breaks, budget);

                // Assert: the sequence of (source characters, value) the chunks carry equals the sequence emitted
                var delivered = chunks
                    .SelectMany(chunk => chunk.Annotations.Select(annotation => (chunk.Text.Substring(annotation.Start, annotation.Length), annotation.Value)))
                    .ToList();
                var emitted = aligned.Spans.Select(span => (text.Substring(span.Start, span.Length), span.Spoken)).ToList();
                Assert.True(
                    emitted.SequenceEqual(delivered),
                    $"budget {budget}, iteration {iteration}: '{text}' emitted {emitted.Count}, delivered {delivered.Count}");
            }
        }
    }

    /// <summary>Proves supplementary-plane digits and letters, lone surrogates and combining marks next to numbers are never cut, never read as ASCII numbers, and never leave a span on half a character.</summary>
    [Fact]
    public void NormalizationSubsystem_Normalize_NumericSurrogatesAndCombiningMarks_AreNeverCutOrRecognized()
    {
        // Arrange
        string[] texts =
        [
            "\uD835\uDFD9\uD835\uDFD9\uD835\uDFD9",
            "5 \uD835\uDFD9 6",
            "\uD835\uDFD95 and 5\uD835\uDFD9",
            "$\uD835\uDFD9 and $5\uD801\uDC00",
            "5\uD801\uDC00 \uD801\uDC005 and 5 \uD801\uDC00",
            "x\uD83D\uDE005 \uD83D\uDE00 50%\uD83D\uDE00 5:30\uD83D\uDE00",
            "5 \uD83D 6 \uDE00 7 \uD83D",
            "5\u0301 5\u0301kg 5:30\u0301 $5\u0301 1\u0301st",
            "1\u0301,000 \u0301$5 5\u0301% 3\u0301/4",
            "\uD83D5\uDE00 50\uD83D\uDE00% $\uD83D\uDE005",
        ];

        foreach (var text in texts)
        {
            // Act
            var aligned = NormalizationHarness.Normalize(text);

            // Assert: no span cuts a pair, and a supplementary-plane digit is no number
            foreach (var span in aligned.Spans)
            {
                Assert.False(span.Start > 0 && char.IsLowSurrogate(text[span.Start]) && char.IsHighSurrogate(text[span.Start - 1]), Escape(text));
                Assert.False(span.End < text.Length && char.IsHighSurrogate(text[span.End - 1]) && char.IsLowSurrogate(text[span.End]), Escape(text));
                Assert.False(char.IsWhiteSpace(text[span.Start]) || char.IsWhiteSpace(text[span.End - 1]), Escape(text));
            }
        }

        Assert.Empty(NormalizationHarness.Normalize("\uD835\uDFD9\uD835\uDFD9\uD835\uDFD9").Spans);
        Assert.DoesNotContain(NormalizationHarness.Normalize("5\u0301").Spans, span => span.Kind == NormalizationKind.Number && span.Start == 0 && span.Length == 2);
        Assert.Equal("five \uD835\uDFD9 six", NormalizationHarness.Normalize("5 \uD835\uDFD9 6").ApplySpokenForms());
    }

    /// <summary>Proves one megabyte of every numeric construct, repeated, normalizes within the time budget, and the work per token stays constant as the text grows.</summary>
    [Fact]
    public void NormalizationSubsystem_Normalize_LargeRepetitiveNumericTexts_CompleteWithinBudget()
    {
        // Arrange: units that would expose any quadratic look-behind or look-ahead
        const int size = 1_000_000;
        string[] units =
        [
            "1,234.50 ", "$5.50 ", "5:30pm ", "January 5, 2024 ", "1/2 ", "12/25/2024 ", "5 kg ", "50 % ", "Jan. 5 ", "5 ft. ", "5 million USD ",
            "555-123-4567 ", "192.168.0.1 ", "1990s ", "3 1/2 ", "5\u201310 ", "$ 5 ", "5 ", "1999 ",
        ];
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(300));

        foreach (var unit in units)
        {
            var text = string.Concat(Enumerable.Repeat(unit, (size / unit.Length) + 1));
            var tokens = NormalizationHarness.Tokenize(text);

            // Act
            var stopwatch = Stopwatch.StartNew();
            var aligned = TextNormalizer.Default.Normalize(text, tokens, classifyClitics: false, cancellation.Token);
            stopwatch.Stop();

            // Assert
            Assert.True(stopwatch.Elapsed < Budget, $"'{unit}' took {stopwatch.Elapsed}");
            Assert.NotEmpty(aligned.Spans);
        }

        // Assert: four times the tokens take well under eight times the time (the best of three runs, to ignore scheduling noise)
        static TimeSpan Best(string text, CancellationToken token)
        {
            var tokens = NormalizationHarness.Tokenize(text);
            var best = TimeSpan.MaxValue;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var watch = Stopwatch.StartNew();
                TextNormalizer.Default.Normalize(text, tokens, false, token);
                watch.Stop();
                best = watch.Elapsed < best ? watch.Elapsed : best;
            }

            return best;
        }

        const string repeated = "$1,234.50 at 5:30pm on January 5, 2024 for 50 % of 3 1/2 kg. ";
        var small = Best(string.Concat(Enumerable.Repeat(repeated, 3_000)), cancellation.Token);
        var large = Best(string.Concat(Enumerable.Repeat(repeated, 12_000)), cancellation.Token);
        Assert.True(large.TotalMilliseconds < Math.Max(small.TotalMilliseconds, 20) * 12, $"3,000 repetitions took {small}, 12,000 took {large}");
    }

    /// <summary>Proves one huge numeric-looking token (a million digits, separators, a symbol, or trailing letters) is rejected by the length cap and normalizes within the time budget.</summary>
    [Fact]
    public void NormalizationSubsystem_Normalize_OneHugeNumericToken_CompletesWithinBudget()
    {
        // Arrange: the tokenizer is not under test for giant tokens, so the token list is built by hand
        string[] texts =
        [
            new string('7', 1_000_000),
            string.Concat(Enumerable.Repeat("1,", 500_000)),
            string.Concat(Enumerable.Repeat("1.", 500_000)),
            string.Concat(Enumerable.Repeat("1-", 500_000)),
            string.Concat(Enumerable.Repeat("1/", 500_000)),
            string.Concat(Enumerable.Repeat("1:", 500_000)),
            "$" + new string('7', 1_000_000),
            "5" + new string('k', 1_000_000),
        ];
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        foreach (var text in texts)
        {
            NormalizationToken[] tokens = [new NormalizationToken(0, text.Length, true)];

            // Act
            var stopwatch = Stopwatch.StartNew();
            var aligned = TextNormalizer.Default.Normalize(text, tokens, classifyClitics: false, cancellation.Token);
            stopwatch.Stop();

            // Assert: left as written and counted, in a fraction of the budget
            Assert.True(stopwatch.Elapsed < Budget, $"took {stopwatch.Elapsed}");
            Assert.Empty(aligned.Spans);
            Assert.Equal(1, aligned.FallbackCount);
        }
    }

    /// <summary>Proves the normalization never depends on the current culture: the whole numeric golden file passes unchanged under Turkish, Arabic, German, Thai and Persian cultures.</summary>
    [Fact]
    public void NormalizationSubsystem_Normalize_UnderForeignCultures_GivesIdenticalResults()
    {
        // Arrange
        var cases = NormalizationHarness.ReadCases("number-cases.tsv");
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            // Act / Assert: the Turkish capital and lowercase I, other digits, other decimal and group separators, other calendars
            foreach (var name in new[] { "tr-TR", "ar-SA", "de-DE", "th-TH", "fa-IR" })
            {
                CultureInfo.CurrentCulture = new CultureInfo(name);
                CultureInfo.CurrentUICulture = new CultureInfo(name);
                var failures = CheckGoldenRows(cases);
                Assert.True(failures.Count == 0, $"{name}: {string.Join(Environment.NewLine, failures)}");
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    /// <summary>Proves analyzing the same numeric text repeatedly and concurrently through the shared default normalizer gives identical results.</summary>
    [Fact]
    public async Task NormalizationSubsystem_Normalize_ConcurrentNumericRequests_ReturnIdenticalResults()
    {
        // Arrange
        const string text = "Pay $1,234.50 at 5:30pm on January 5, 2024, then 50 % of 3 1/2 kg or 12/25/2024 for 2 ft. in the '90s. Version 3.2.1, #5, 1st, 555-1234.";
        var expected = Key(NormalizationHarness.Normalize(text));

        // Act: many tasks race on the lazily created default lexicons
        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(
                () => Enumerable.Range(0, 20).Select(_ => Key(NormalizationHarness.Normalize(text))).ToList(),
                TestContext.Current.CancellationToken))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results.SelectMany(keys => keys), key => Assert.Equal(expected, key));
    }

    /// <summary>
    ///     Builds a deterministic random text from overridable words, numeric constructs, initialisms, punctuation, tag
    ///     placeholders, whitespace variants and hostile characters.
    /// </summary>
    /// <param name="random">The generator.</param>
    /// <param name="maxPieces">The most pieces to join.</param>
    /// <returns>The text; may be empty.</returns>
    private static string RandomOverrideText(Random random, int maxPieces)
    {
        string[] pieces =
        [
            "Nginx", "nginx", "NGINX", "Kubernetes", "kubernetes", "GIF", "gif", "Polish", "polish", "POLISH", "SQL", "sql", "NASA", "NASA's", "Nginx-based", "Wi-Fi", "Wi-Fi's", "Ph.D", "Ph.D.", "kg", "million",
            "pm", "PM", "US", "us", "FBI", "CIA", "Mr", "Mr.", "Do", "Don't", "don't", "O'Brien", "O\u2019Brien", "\u0130stanbul", "ISTANBUL", "\U00010400\U00010401x", "Cafe", "AT&T", "AT&T's", "R&D", "Q&A",
            "5", "$5", "$5 million", "5 kg", "5 pm", "5:30pm", "3 million", "J.", "U.S.", "p.m.", "Dr.", "St.", "&", "%", "50%", "January 5, 2024", "1,234.50",
            ",", ".", ";", ":", "!", "?", "(", ")", "-", "/", "'", "\u2019", "\u200B", "\u2022", "\uD83D", "\uDE00", "\U0001F600", "e\u0301", "\u00E9", Tag, Tag + Tag, "\u00A0", "\0", new string('N', 70),
        ];
        string[] gaps = ["", " ", " ", " ", "\u00A0", "  ", "\t", "\n", ", ", "; "];
        var builder = new StringBuilder();
        var count = random.Next(0, maxPieces);
        for (var i = 0; i < count; i++)
        {
            builder.Append(pieces[random.Next(pieces.Length)]).Append(gaps[random.Next(gaps.Length)]);
        }

        return builder.ToString();
    }

    /// <summary>Loads the documented sample dictionary.</summary>
    /// <returns>The dictionary.</returns>
    private static PronunciationOverrides SampleOverrides() =>
        PronunciationOverrides.FromTsv(new StringReader(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "overrides-dictionary.tsv"))));

    /// <summary>Turns the result of a normalization into the annotations the analyzer would emit: replacements as <c>en.norm</c> or <c>en.spell</c>, pronunciations as <c>en.pron</c>.</summary>
    /// <param name="aligned">The result.</param>
    /// <returns>The annotations in text order.</returns>
    private static List<SegmentAnnotation> ToAnnotations(AlignedText aligned) =>
    [
        .. aligned.Spans.Select(span => new SegmentAnnotation(span.Start, span.Length, span.Spelled ? "en.spell" : "en.norm", span.Spoken))
            .Concat(aligned.Pronunciations.Select(mark => new SegmentAnnotation(mark.Start, mark.Length, "en.pron", mark.Value)))
            .OrderBy(annotation => annotation.Start),
    ];

    /// <summary>
    ///     Proves hostile random text with a real dictionary always yields results that satisfy the alignment contract:
    ///     replacements (normalization and spelling together) and pronunciations are ascending and disjoint in bounds, spelled
    ///     spans are letter sequences, every span and mark is chunk-safe, an independent applier agrees, and every directive is
    ///     counted exactly once as applied or ignored.
    /// </summary>
    [Fact]
    public void NormalizationSubsystem_AlignmentContract_RandomTextWithOverrides_SatisfiesAllInvariants()
    {
        // Arrange
        var overrides = SampleOverrides();
        var random = new Random(20260902);

        for (var iteration = 0; iteration < 600; iteration++)
        {
            var text = RandomOverrideText(random, 30);
            var neutralized = NormalizationHarness.Neutralize(text);
            var tokens = NormalizationHarness.Tokenize(neutralized);
            var directives = overrides.Resolve(neutralized, tokens, CancellationToken.None);
            var context = $"in iteration {iteration}: '{Escape(text)}'";

            // Act
            var aligned = TextNormalizer.Default.Normalize(neutralized, tokens, false, CancellationToken.None, directives);

            // Assert: every directive is counted exactly once
            Assert.True(directives.Length == aligned.OverridesApplied + aligned.OverridesIgnored, $"directive count {context}");

            // Assert A1', A12: one ascending, disjoint sequence of spans and marks, all in bounds
            var covering = aligned.Spans.Select(span => (span.Start, span.End)).Concat(aligned.Pronunciations.Select(mark => (mark.Start, mark.End))).OrderBy(item => item.Start).ToList();
            var previousEnd = 0;
            foreach (var (start, end) in covering)
            {
                Assert.True(start >= previousEnd && end <= neutralized.Length && end > start, $"order or bounds {context}");
                Assert.False(char.IsWhiteSpace(neutralized[start]) || char.IsWhiteSpace(neutralized[end - 1]), $"whitespace edge {context}");
                Assert.False(start > 0 && char.IsLowSurrogate(neutralized[start]) && char.IsHighSurrogate(neutralized[start - 1]), $"cut pair {context}");
                Assert.False(end < neutralized.Length && char.IsHighSurrogate(neutralized[end - 1]) && char.IsLowSurrogate(neutralized[end]), $"cut pair {context}");
                previousEnd = end;
            }

            // Assert A11: spelled spans are letter sequences without whitespace, comma, semicolon or colon in their source
            foreach (var span in aligned.Spans)
            {
                if (span.Spelled)
                {
                    Assert.Matches("^[A-Z]( [A-Z])*$", span.Spoken.Trim());
                    Assert.True(span.Spoken.Length - span.Spoken.TrimStart().Length <= 1 && span.Spoken.Length - span.Spoken.TrimEnd().Length <= 1, $"padding {context}");
                }

                AssertChunkSafe(neutralized, span, context);
            }

            foreach (var mark in aligned.Pronunciations)
            {
                var source = neutralized.Substring(mark.Start, mark.Length);
                Assert.DoesNotContain(source, character => char.IsWhiteSpace(character) || character is ',' or ';' or ':');
                Assert.Contains(tokens, token => token.Start == mark.Start && token.Length == mark.Length);
            }

            // Assert: the independent consumer algorithm (en.norm and en.spell together) agrees with the production application
            Assert.Equal(aligned.ApplySpokenForms(), NormalizationHarness.ApplyNormalizations(neutralized, ToAnnotations(aligned)));
        }
    }

    /// <summary>
    ///     Proves, with the real core <c>SentenceChunker.ChunkAligned</c>, that every replacement and pronunciation annotation of
    ///     random override text lands in exactly one chunk on the same characters, at every length budget, with clause cuts,
    ///     budget cuts and tag placeholders next to overridden words, and nothing is invented.
    /// </summary>
    [Fact]
    public void NormalizationSubsystem_OverrideAnnotations_RealChunker_KeepEveryAnnotationInExactlyOneChunk()
    {
        // Arrange
        var overrides = SampleOverrides();
        string[] pieces =
        [
            "Nginx", "SQL", "Polish", "polish", "GIF", "kg", "US", "FBI", "CIA", "AT&T", "R&D", "5 kg", "$5 million", "U.S.", "J.", "Ph.D", "word", "and", "then", "Wi-Fi", "NASA's", "Cafe", Tag, "5:30pm",
        ];
        string[] separators = [" ", " ", " ", ", ", "; ", ": ", ". ", ", ", Tag, Tag + " "];
        var random = new Random(20260903);

        foreach (var budget in new[] { 200, 120, 60, 25 })
        {
            for (var iteration = 0; iteration < 150; iteration++)
            {
                var builder = new StringBuilder();
                for (var i = 0; i < random.Next(3, 26); i++)
                {
                    builder.Append(pieces[random.Next(pieces.Length)]).Append(separators[random.Next(separators.Length)]);
                }

                var text = builder.ToString().TrimEnd();
                var neutralized = NormalizationHarness.Neutralize(text);
                var aligned = NormalizationHarness.NormalizeWithOverrides(text, overrides);
                var annotations = ToAnnotations(aligned);
                var breaks = aligned.ReconcileBreaks(NormalizationHarness.TokenizerBreaks(neutralized));

                // Act: the chunker sees the text with its placeholders, as the session does
                var chunks = SentenceChunker.ChunkAligned(text, annotations, breaks, budget);

                // Assert: the sequence of (source characters, kind, value) the chunks carry equals the sequence emitted
                var delivered = chunks
                    .SelectMany(chunk => chunk.Annotations.Select(annotation => (chunk.Text.Substring(annotation.Start, annotation.Length), annotation.Kind, annotation.Value)))
                    .ToList();
                var emitted = annotations.Select(annotation => (text.Substring(annotation.Start, annotation.Length), annotation.Kind, annotation.Value)).ToList();
                Assert.True(
                    emitted.SequenceEqual(delivered),
                    $"budget {budget}, iteration {iteration}: '{Escape(text)}' emitted {emitted.Count}, delivered {delivered.Count}");
            }
        }
    }

    /// <summary>Proves that for texts without any dictionary hit, a dictionary changes nothing: the whole result equals the result without a dictionary, and no override is counted.</summary>
    [Fact]
    public void NormalizationSubsystem_Normalize_DictionaryWithoutHits_EqualsNoDictionary()
    {
        // Arrange: a dictionary whose words never occur in the generated numeric texts
        var overrides = PronunciationOverrides.Create(
        [
            PronunciationOverrideEntry.Say("Zorblax", "zor blax"),
            PronunciationOverrideEntry.SpellOut("Qwerty", true),
            PronunciationOverrideEntry.Pronounce("Frobnicate", PronunciationScheme.Ipa, "frobnikeit"),
        ]);
        var random = new Random(20260904);

        for (var iteration = 0; iteration < 400; iteration++)
        {
            var text = RandomNumericText(random, 30);

            // Act
            var without = NormalizationHarness.Normalize(text);
            var with = NormalizationHarness.NormalizeWithOverrides(text, overrides);

            // Assert
            Assert.True(Key(without) == Key(with), $"result differs in iteration {iteration}: '{Escape(text)}'");
            Assert.Equal(0, with.OverridesApplied + with.OverridesIgnored);
        }
    }

    /// <summary>Proves every override golden row passes unchanged under Turkish, Azeri, German, Thai and Persian current cultures: no culture-sensitive comparison or casing is involved.</summary>
    [Fact]
    public void NormalizationSubsystem_Overrides_UnderForeignCultures_GiveIdenticalResults()
    {
        // Arrange
        var cases = NormalizationHarness.ReadOverrideCases("overrides-cases.tsv");
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            foreach (var name in new[] { "tr-TR", "az-Latn-AZ", "de-DE", "th-TH", "fa-IR" })
            {
                CultureInfo.CurrentCulture = new CultureInfo(name);
                CultureInfo.CurrentUICulture = new CultureInfo(name);
                var overrides = SampleOverrides();

                // Act / Assert
                foreach (var row in cases)
                {
                    var aligned = NormalizationHarness.NormalizeWithOverrides(row.Input, overrides);
                    Assert.True(row.Spoken == aligned.ApplySpokenForms(), $"{name}: row '{row.Id}' spoken '{aligned.ApplySpokenForms()}'");
                    Assert.True(row.Applied == aligned.OverridesApplied && row.Ignored == aligned.OverridesIgnored, $"{name}: row '{row.Id}' counts");
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    /// <summary>The four directive kinds, with the value each needs, that the sentence-break differential test applies in turn.</summary>
    private static readonly (TokenDirectiveKind Kind, string? Value)[] AllDirectiveKinds =
    [
        (TokenDirectiveKind.Skip, null),
        (TokenDirectiveKind.Replace, "other words"),
        (TokenDirectiveKind.Spell, null),
        (TokenDirectiveKind.Pronounce, "ipa:abc"),
    ];

    /// <summary>
    ///     Gets the reconciled sentence breaks of a normalization result: the tokenizer's breaks of the neutralized text with the
    ///     result's forced breaks added and its suppressed breaks removed.
    /// </summary>
    /// <param name="aligned">The result.</param>
    /// <param name="neutralized">The neutralized text the result indexes.</param>
    /// <returns>The ascending break offsets.</returns>
    private static IReadOnlyList<int> BreaksOf(AlignedText aligned, string neutralized) =>
        aligned.ReconcileBreaks(NormalizationHarness.TokenizerBreaks(neutralized));

    /// <summary>Builds a deterministic random text rich in abbreviations, acronyms, periods, dashes and placeholders.</summary>
    /// <param name="random">The generator.</param>
    /// <returns>The text; never empty.</returns>
    private static string RandomAbbreviationText(Random random)
    {
        string[] pieces =
        [
            "Fig. 5", "Fig.", "Dr. Smith", "Dr.", "Maple Dr.", "St. Louis", "Main St.", "No. 5", "No.", "Jan. 5", "Jan.", "Mr Smith", "Mrs.", "Jr.", "Ph.D.", "Sr.", "etc.", "e.g.",
            "U.S.", "U.S. Army", "J. Smith", "5 kg", "$5 million", "5 p.m.", "NASA", "FBI", "Hello\u2014Dr. Smith", "word", "Then", "He", "She", "It", "\"quoted.\"", "(see Elm St.)", "Wi-Fi", Tag,
            "Fig", "Dr", "Jan", "St", "Ph.D", "Prof. Lee", "Mt. Everest", "Inc.", "vs.",
        ];
        string[] separators = [" ", " ", " ", ". ", ", ", "; ", ": ", "\n", " \u2014 ", Tag, Tag + " "];
        var builder = new StringBuilder();
        var count = random.Next(1, 12);
        for (var i = 0; i < count; i++)
        {
            builder.Append(pieces[random.Next(pieces.Length)]).Append(separators[random.Next(separators.Length)]);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Proves, by a seeded differential test, that no directive on any token ever changes the reconciled sentence breaks:
    ///     over random abbreviation-rich text, every directive kind on every sampled word token (always the words that a period
    ///     touches) gives exactly the breaks of the text without it, counts the directive once, keeps the spans ascending and
    ///     disjoint and puts no break inside a span; one run puts a directive on every eligible word at once.
    /// </summary>
    [Fact]
    public void NormalizationSubsystem_Normalize_DirectiveAnywhere_NeverChangesSentenceBreaks()
    {
        // Arrange
        var random = new Random(20261116);
        var checkedDirectives = 0;

        for (var iteration = 0; iteration < 400; iteration++)
        {
            var text = RandomAbbreviationText(random);
            var neutralized = NormalizationHarness.Neutralize(text);
            var tokens = NormalizationHarness.Tokenize(neutralized);
            var baselineBreaks = BreaksOf(NormalizationHarness.Normalize(text), neutralized);

            // The word tokens: those a period touches always, the others up to 16 at random
            var touched = new List<int>();
            var others = new List<int>();
            for (var i = 0; i < tokens.Length; i++)
            {
                if (!char.IsLetter(neutralized[tokens[i].Start]))
                {
                    continue;
                }

                var periodAdjacent = (tokens[i].End < neutralized.Length && neutralized[tokens[i].End] == '.') || neutralized[tokens[i].End - 1] == '.';
                (periodAdjacent ? touched : others).Add(i);
            }

            var sampled = touched.Concat(others.OrderBy(_ => random.Next()).Take(16)).Order().ToList();

            foreach (var index in sampled)
            {
                var ascii = neutralized.Substring(tokens[index].Start, tokens[index].Length).All(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
                foreach (var (kind, value) in AllDirectiveKinds)
                {
                    if (kind == TokenDirectiveKind.Spell && !ascii)
                    {
                        continue;
                    }

                    // Act
                    var directive = new TokenDirective(index, kind, value);
                    var aligned = NormalizationHarness.Normalize(text, false, [directive]);
                    checkedDirectives++;

                    // Assert
                    AssertDirectiveKeepsBreaks(aligned, neutralized, baselineBreaks, 1, $"iteration {iteration}, token {index} ({kind}): '{Escape(text)}'");
                }
            }

            // Act: a directive on every sampled word at once, kinds rotating
            var all = new TokenDirective[sampled.Count];
            for (var position = 0; position < all.Length; position++)
            {
                var index = sampled[position];
                var ascii = neutralized.Substring(tokens[index].Start, tokens[index].Length).All(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
                all[position] = (position % 4) switch
                {
                    1 => new TokenDirective(index, TokenDirectiveKind.Replace, "other words"),
                    2 => new TokenDirective(index, TokenDirectiveKind.Pronounce, "ipa:abc"),
                    3 when ascii => new TokenDirective(index, TokenDirectiveKind.Spell, null),
                    _ => new TokenDirective(index, TokenDirectiveKind.Skip, null),
                };
            }

            var together = NormalizationHarness.Normalize(text, false, all);

            // Assert
            AssertDirectiveKeepsBreaks(together, neutralized, baselineBreaks, all.Length, $"iteration {iteration}, all {all.Length} directives: '{Escape(text)}'");
        }

        Assert.True(checkedDirectives > 1000, $"only {checkedDirectives} directives were checked");
    }

    /// <summary>Asserts the contract of a directive result: the breaks equal the baseline, every directive is counted, and the alignment is well formed.</summary>
    /// <param name="aligned">The result with the directives.</param>
    /// <param name="neutralized">The neutralized text.</param>
    /// <param name="baselineBreaks">The reconciled breaks without any directive.</param>
    /// <param name="directiveCount">The number of directives given.</param>
    /// <param name="context">The failure context.</param>
    private static void AssertDirectiveKeepsBreaks(AlignedText aligned, string neutralized, IReadOnlyList<int> baselineBreaks, int directiveCount, string context)
    {
        var breaks = BreaksOf(aligned, neutralized);
        Assert.True(baselineBreaks.SequenceEqual(breaks), $"breaks [{string.Join(",", breaks)}] instead of [{string.Join(",", baselineBreaks)}], {context}");
        Assert.True(aligned.OverridesApplied + aligned.OverridesIgnored == directiveCount, $"directive count, {context}");
        var previousEnd = 0;
        foreach (var (start, end) in aligned.Spans.Select(span => (span.Start, span.End)).Concat(aligned.Pronunciations.Select(mark => (mark.Start, mark.End))).OrderBy(item => item.Start))
        {
            Assert.True(start >= previousEnd && end > start && end <= neutralized.Length, $"span order or bounds, {context}");
            Assert.DoesNotContain(breaks, offset => offset > start && offset < end);
            previousEnd = end;
        }
    }

    /// <summary>
    ///     Proves, with the real <c>PronunciationOverrides</c>, the real normalizer and the real core chunker, that an override
    ///     on an abbreviation word never changes where a text is chunked: for every action, for case-sensitive and case-insensitive
    ///     entries, mid-sentence and sentence-final, around an em dash and next to tag placeholders, at every budget, the chunks
    ///     equal those of the text without a dictionary, and every annotation of the override is still delivered once.
    /// </summary>
    [Fact]
    public void NormalizationSubsystem_Overrides_OnAbbreviations_RealChunker_ChunksEqualTheBuiltInChunks()
    {
        // Arrange
        (string Text, string Word)[] rows =
        [
            ("See Fig. 5 for details.", "Fig"), ("We saw the plot in Fig. It was good.", "Fig"),
            ("Dr. Smith lives here. He sleeps.", "Dr"), ("Dr. Smith lives on Maple Dr. He likes it.", "Dr"),
            ("Visit St. Louis. Then go.", "St"), ("I live on Main St. It is nice.", "St"),
            ("No. 5 is here. Then go.", "No"), ("He said No. I don't know.", "No"),
            ("Jan. 5 was cold. Then go.", "Jan"), ("We met in Jan. It was cold.", "Jan"),
            ("Hello\u2014Dr. Smith left.", "Dr"), ("Hello\u2014Dr. Smith left. Bye.", "Dr"),
            ("John Smith Jr. Ph.D. spoke.", "Ph.D"), ("John Smith Jr. Ph.D. He spoke.", "Ph.D"),
            (Tag + "Fig" + Tag + ". 5 for details.", "Fig"), ("Hello" + Tag + "\u2014Dr. Smith left.", "Dr"), ("See Fig." + Tag + "5 for details.", "Fig"),
        ];
        var applied = 0;

        foreach (var (text, word) in rows)
        {
            var neutralized = NormalizationHarness.Neutralize(text);
            var baseline = NormalizationHarness.Normalize(text);
            var baselineBreaks = baseline.ReconcileBreaks(NormalizationHarness.TokenizerBreaks(neutralized));

            foreach (var caseSensitive in new[] { true, false })
            {
                var spelling = caseSensitive ? word : word.ToUpperInvariant();
                var entries = new List<PronunciationOverrideEntry>
                {
                    PronunciationOverrideEntry.Skip(spelling, caseSensitive),
                    PronunciationOverrideEntry.Say(spelling, "other words", caseSensitive),
                    PronunciationOverrideEntry.Pronounce(spelling, PronunciationScheme.Ipa, "abc", caseSensitive),
                };
                if (spelling.All(char.IsAsciiLetter))
                {
                    entries.Add(PronunciationOverrideEntry.SpellOut(spelling, caseSensitive));
                }

                foreach (var entry in entries)
                {
                    // Act
                    var aligned = NormalizationHarness.NormalizeWithOverrides(text, PronunciationOverrides.Create([entry]));
                    var annotations = ToAnnotations(aligned);
                    var breaks = aligned.ReconcileBreaks(NormalizationHarness.TokenizerBreaks(neutralized));
                    applied += aligned.OverridesApplied;

                    // Assert
                    var context = $"'{Escape(text)}', {entry.Action} '{spelling}' (case-sensitive: {caseSensitive})";
                    Assert.True(baselineBreaks.SequenceEqual(breaks), $"breaks [{string.Join(",", breaks)}] instead of [{string.Join(",", baselineBreaks)}]: {context}");
                    foreach (var budget in new[] { SentenceChunker.DefaultMaxChunkLength, 60, 25 })
                    {
                        var expected = SentenceChunker.ChunkAligned(text, ToAnnotations(baseline), baselineBreaks, budget);
                        var chunks = SentenceChunker.ChunkAligned(text, annotations, breaks, budget);
                        Assert.True(
                            expected.Select(chunk => (chunk.Text, chunk.EndsWithEllipsis)).SequenceEqual(chunks.Select(chunk => (chunk.Text, chunk.EndsWithEllipsis))),
                            $"chunks differ at budget {budget}: {context}");

                        var delivered = chunks.SelectMany(chunk => chunk.Annotations.Select(annotation => (chunk.Text.Substring(annotation.Start, annotation.Length), annotation.Kind, annotation.Value))).ToList();
                        var emitted = annotations.Select(annotation => (text.Substring(annotation.Start, annotation.Length), annotation.Kind, annotation.Value)).ToList();
                        Assert.True(emitted.SequenceEqual(delivered), $"annotations differ at budget {budget}: {context}");
                    }
                }
            }
        }

        // The review example, with a directive, is one chunk at every budget
        var example = NormalizationHarness.NormalizeWithOverrides("See Fig. 5 for details.", PronunciationOverrides.Create([PronunciationOverrideEntry.Say("Fig", "figure")]));
        var exampleBreaks = example.ReconcileBreaks(NormalizationHarness.TokenizerBreaks("See Fig. 5 for details."));
        Assert.Equal(["See Fig. 5 for details."], SentenceChunker.ChunkAligned("See Fig. 5 for details.", ToAnnotations(example), exampleBreaks, 25).Select(chunk => chunk.Text));
        Assert.True(applied > 0, "no override was ever applied");
    }

    /// <summary>
    ///     Builds a deterministic random text from numeric constructs, words, punctuation, tag placeholders, whitespace
    ///     variants and hostile characters.
    /// </summary>
    /// <param name="random">The generator.</param>
    /// <param name="maxPieces">The most pieces to join.</param>
    /// <returns>The text; may be empty.</returns>
    private static string RandomNumericText(Random random, int maxPieces)
    {
        string[] pieces =
        [
            "5", "50", "007", "1,234", "1,234.50", "3.5", ".5", "-5", "+3", "\u22127", "1,23", "12,34,567", "$", "\u20AC", "\u00A3", "\u00A5", "$5", "$5.50", "5$", "5\u20AC",
            "5USD", "USD", "EUR", "US$5", "5:30", "5:30pm", "5pm", "5:30PM", "pm", "am", "p.m.", "P.M.", "25:99", "12/25/2024", "2024-12-25", "5-10", "555-1234", "3.2.1",
            "1/2", "3/4", "24/7", "\u00BD", "5\u00BD", "1st", "22nd", "1th", "1990s", "90s", "'", "\u2019", "'90s", "\u2013", "%", "50%", "kg", "5kg", "ft.", "ft", "lbs", "lbs.",
            "mph", "\u00B0F", "72\u00B0F", "million", "billion", "thousand", "$5M", "5M", "January", "Jan.", "Jan", "May", "Mar.", "Sun.", "Mon.", "Dec", "2024", "1999", "1066", "#5", "#",
            "(", ")", ",", ".", ";", ":", "-", "/", "!", "?", "in", "about", "the", "No.", "Dr.", "St.", "&", "w/o", "AD", "BC", "1e5", "0x1F", "5\u00B2", "\uFF15", "\u200B",
            "\uD835\uDFD9", "\uD83D", "\uDE00", "\U0001F600", "e\u0301", Tag, Tag + Tag, "\u00A0", "\0", new string('1', 17), new string('1', 70), new string('1', 130), "1.2.3.4.5.6.7.8.9",
        ];
        string[] gaps = ["", " ", " ", " ", "\u00A0", "\u202F", "  ", "\t", "\n", "\r\n"];
        var builder = new StringBuilder();
        var count = random.Next(0, maxPieces);
        for (var i = 0; i < count; i++)
        {
            builder.Append(pieces[random.Next(pieces.Length)]).Append(gaps[random.Next(gaps.Length)]);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Asserts invariant A5': a span has no whitespace, never a semicolon, and a comma or colon only between two ASCII digits.
    /// </summary>
    /// <param name="text">The text the span indexes.</param>
    /// <param name="span">The span.</param>
    /// <param name="context">The failure context.</param>
    private static void AssertChunkSafe(string text, NormalizedSpan span, string context)
    {
        var covered = text.AsSpan(span.Start, span.Length);
        for (var i = 0; i < covered.Length; i++)
        {
            Assert.False(char.IsWhiteSpace(covered[i]) || covered[i] == ';', $"whitespace or semicolon in a span {context}");
            if (covered[i] is ',' or ':')
            {
                Assert.True(
                    i > 0 && i < covered.Length - 1 && covered[i - 1] is >= '0' and <= '9' && covered[i + 1] is >= '0' and <= '9',
                    $"separator not between digits {context}");
            }
        }
    }

    /// <summary>Tells whether a character is an invisible format character or a bullet the normalizer may drop.</summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> for a droppable character.</returns>
    private static bool IsInvisibleOrBullet(char character) =>
        character is '\u00AD' or '\u200B' or '\u2060' or '\uFEFF' or '\u200E' or '\u200F'
        || character is >= '\u202A' and <= '\u202E'
        || character is >= '\u2066' and <= '\u2069'
        || character is '\u2022' or '\u2023' or '\u25E6' or '\u2043' or '\u25AA' or '\u25CF';

    /// <summary>Builds a comparable key of a whole normalization result.</summary>
    /// <param name="aligned">The result.</param>
    /// <returns>A string that is equal for equal results.</returns>
    private static string Key(AlignedText aligned) =>
        string.Join(
            "|",
            aligned.Spans.Select(span => $"{span.Start},{span.Length},{span.Spoken},{span.Kind},{span.Spelled}")
                .Concat(aligned.Pronunciations.Select(mark => $"P{mark.Start},{mark.Length},{mark.Value}"))
                .Concat(aligned.ForcedBreaks.Select(value => $"F{value}"))
                .Concat(aligned.SuppressedBreaks.Select(value => $"S{value}"))
                .Concat(aligned.Clitics.Select(mark => $"C{mark.Start},{mark.Length},{mark.Kind}"))
                .Append($"A{aligned.AmbiguousCount}")
                .Append($"N{aligned.FallbackCount}")
                .Append($"O{aligned.OverridesApplied},{aligned.OverridesIgnored}"));

    /// <summary>Makes control and invisible characters visible in a failure message.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with non-printable characters as escapes.</returns>
    private static string Escape(string text) =>
        string.Concat(text.Select(character => character < ' ' || character > '~' ? $"\\u{(int)character:X4}" : character.ToString()));
}
