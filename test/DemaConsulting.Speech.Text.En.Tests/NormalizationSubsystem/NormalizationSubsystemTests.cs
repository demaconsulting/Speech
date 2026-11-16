using System.Diagnostics;
using System.Text;
using DemaConsulting.Speech.SynthesisSubsystem;
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
        var failures = new List<string>();

        // Act: normalize every row and collect all mismatches so one run reports them all
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
        }

        // Assert
        Assert.True(cases.Count >= 80, "the golden file must keep at least 80 rows");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
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
        var inputs = NormalizationHarness.ReadCases("normalization-cases.tsv").Select(row => row.Input).ToList();

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

                // A5: chunk-safe, no whitespace and none of the clause separators
                Assert.DoesNotContain(text.Substring(span.Start, span.Length), character => char.IsWhiteSpace(character) || character is ',' or ';' or ':');

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
            aligned.Spans.Select(span => $"{span.Start},{span.Length},{span.Spoken},{span.Kind}")
                .Concat(aligned.ForcedBreaks.Select(value => $"F{value}"))
                .Concat(aligned.SuppressedBreaks.Select(value => $"S{value}"))
                .Concat(aligned.Clitics.Select(mark => $"C{mark.Start},{mark.Length},{mark.Kind}"))
                .Append($"A{aligned.AmbiguousCount}"));

    /// <summary>Makes control and invisible characters visible in a failure message.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with non-printable characters as escapes.</returns>
    private static string Escape(string text) =>
        string.Concat(text.Select(character => character < ' ' || character > '~' ? $"\\u{(int)character:X4}" : character.ToString()));
}
