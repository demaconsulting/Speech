using System.Globalization;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;

namespace DemaConsulting.Speech.Text.En.Tests.HeteronymSubsystem;

/// <summary>
///     Unit tests for <see cref="ReadingInventory"/> and the reading-id grammar it shares with the
///     override validation and the typed annotation reader.
/// </summary>
public sealed class ReadingInventoryTests
{
    /// <summary>The header line of a readings table.</summary>
    private const string ReadingsHeader = "reading_id\tlemma\tpos_hint\tdescription\tstatus\n";

    /// <summary>The header line of a forms table.</summary>
    private const string FormsHeader = "form\treading_id\tkind\n";

    /// <summary>A valid two-reading table with one ambiguous form (<c>wind</c>) and one single-candidate form (<c>winds</c>).</summary>
    private const string ValidReadings = ReadingsHeader + "wind.air\twind\tNOUN\td\tactive\nwind.coil\twind\tVERB\td\tactive\n";

    /// <summary>The forms of <see cref="ValidReadings"/>.</summary>
    private const string ValidForms = FormsHeader + "wind\twind.air\torigin\nwind\twind.coil\torigin\nwinds\twind.air\tinflection\n";

    /// <summary>Proves the embedded tables load and match the pinned counts (a data change needs a deliberate version decision).</summary>
    [Fact]
    public void ReadingInventory_Default_EmbeddedTables_MatchPinnedVersionAndCounts()
    {
        // Act
        var inventory = ReadingInventory.Default;

        // Assert: revision 2 (inventory v1.1): 395 readings, 501 distinct forms, 350 ambiguous and 151 single-candidate
        Assert.Equal(2, inventory.Version);
        Assert.Equal(395, inventory.ReadingIds.Count);
        Assert.Equal(501, inventory.FormCount);
        var ambiguous = 0;
        for (var form = 0; form < inventory.FormCount; form++)
        {
            ambiguous += inventory.GetCandidates(form).Length > 1 ? 1 : 0;
        }

        Assert.Equal(350, ambiguous);
        Assert.Same(inventory, ReadingInventory.Default);
    }

    /// <summary>Proves the tables are embedded under the fixed logical names, not copied.</summary>
    [Fact]
    public void ReadingInventory_Default_EmbeddedTables_AreAssemblyResourcesWithFixedNames()
    {
        // Arrange
        var assembly = typeof(ReadingInventory).Assembly;

        // Act / Assert
        Assert.NotNull(assembly.GetManifestResourceInfo("DemaConsulting.Speech.Text.En.Resources.readings.tsv"));
        Assert.NotNull(assembly.GetManifestResourceInfo("DemaConsulting.Speech.Text.En.Resources.forms.tsv"));
        Assert.Null(assembly.GetManifestResourceInfo("DemaConsulting.Speech.Text.En.Resources.heteronyms.bin"));
    }

    /// <summary>Proves every reading id of the embedded inventory satisfies the id grammar, is contained and the list is sorted by ordinal comparison.</summary>
    [Fact]
    public void ReadingInventory_ReadingIds_AllSatisfyGrammarAndAreOrdinalSorted()
    {
        // Arrange
        var inventory = ReadingInventory.Default;

        // Act
        var ids = inventory.ReadingIds.ToArray();

        // Assert
        Assert.All(ids, id =>
        {
            Assert.True(ReadingIdSyntax.IsValid(id), id);
            Assert.True(inventory.Contains(id));
        });
        Assert.Equal(ids.OrderBy(id => id, StringComparer.Ordinal), ids);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Proves the candidates of a form keep the row order of the table and the lookup folds ASCII case only.</summary>
    [Theory]
    [InlineData("read", new[] { "read.past", "read.present" })]
    [InlineData("Read", new[] { "read.past", "read.present" })]
    [InlineData("READ", new[] { "read.past", "read.present" })]
    [InlineData("record", new[] { "record.noun", "record.verb" })]
    public void ReadingInventory_CandidatesFor_AsciiFold_ReturnsRowOrder(string form, string[] expected)
    {
        // Act
        var candidates = ReadingInventory.Default.CandidatesFor(form);

        // Assert
        Assert.Equal(expected, candidates);
    }

    /// <summary>Proves anything that is not exactly an inventory form has no candidates: unknown words, empty text, compounds, spaces, non-ASCII letters and the Kelvin sign.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("elephant")]
    [InlineData("wind-up")]
    [InlineData(" record")]
    [InlineData("record ")]
    [InlineData("rec\u00F3rd")]
    [InlineData("\u212A")]
    [InlineData("rec0rd")]
    [InlineData("record.")]
    public void ReadingInventory_CandidatesFor_NotAForm_ReturnsEmpty(string form)
    {
        // Act
        var candidates = ReadingInventory.Default.CandidatesFor(form);

        // Assert
        Assert.Empty(candidates);
    }

    /// <summary>Proves a null argument is rejected by the public lookups.</summary>
    [Fact]
    public void ReadingInventory_PublicLookups_NullArgument_ThrowsArgumentNullException()
    {
        // Arrange
        var inventory = ReadingInventory.Default;

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => inventory.CandidatesFor(null!));
        Assert.Throws<ArgumentNullException>(() => inventory.Contains(null!));
    }

    /// <summary>Proves Contains is exact: ids are compared ordinally, including case.</summary>
    [Fact]
    public void ReadingInventory_Contains_ExactId_ComparesOrdinally()
    {
        // Arrange
        var inventory = ReadingInventory.Default;

        // Act / Assert
        Assert.True(inventory.Contains("record.noun"));
        Assert.False(inventory.Contains("Record.noun"));
        Assert.False(inventory.Contains("record"));
        Assert.False(inventory.Contains("record.ghost"));
    }

    /// <summary>Proves the whole-token form lookup allocates nothing, for hits and misses, over many lookups.</summary>
    [Fact]
    public void ReadingInventory_TryFindForm_ManyLookups_AllocatesNothing()
    {
        // Arrange: warm up so any one-time initialization is not counted
        var inventory = ReadingInventory.Default;
        var hit = "Record".AsSpan();
        var miss = "elephant".AsSpan();
        inventory.TryFindForm(hit, out _);
        inventory.TryFindForm(miss, out _);

        // Act
        var before = GC.GetAllocatedBytesForCurrentThread();
        var found = 0;
        for (var i = 0; i < 10_000; i++)
        {
            found += inventory.TryFindForm(hit, out _) ? 1 : 0;
            found += inventory.TryFindForm(miss, out _) ? 1 : 0;
        }

        var after = GC.GetAllocatedBytesForCurrentThread();

        // Assert
        Assert.Equal(10_000, found);
        Assert.Equal(0, after - before);
    }

    /// <summary>Proves the internal accessors agree: the candidates, ids, activity and salts of a form.</summary>
    [Fact]
    public void ReadingInventory_InternalAccessors_AgreeWithPublicLookup()
    {
        // Arrange
        var inventory = ReadingInventory.Default;

        // Act
        Assert.True(inventory.TryFindForm("record", out var formIndex));
        var candidates = inventory.GetCandidates(formIndex);

        // Assert
        Assert.Equal(["record.noun", "record.verb"], candidates.ToArray().Select(inventory.ReadingIdAt));
        Assert.All(candidates.ToArray(), index => Assert.True(inventory.IsActive(index)));
        Assert.Equal(FeatureHashing.LabelSalt("record.noun"), inventory.LabelSalt(candidates[0]));
        Assert.Equal(FeatureHashing.RoleSalt("verb"), inventory.RoleSalt(candidates[1]));
        Assert.True(inventory.TryGetReadingIndex("record.verb", out var verbIndex));
        Assert.Equal(candidates[1], verbIndex);
        Assert.False(inventory.TryGetReadingIndex("nope.noun", out _));
    }

    /// <summary>Proves the public collections cannot be changed through a cast.</summary>
    [Fact]
    public void ReadingInventory_ReadingIdsAndCandidates_AreReadOnly()
    {
        // Arrange
        var inventory = ReadingInventory.Default;

        // Act
        var ids = (ICollection<string>)inventory.ReadingIds;
        var candidates = (IList<string>)inventory.CandidatesFor("record");

        // Assert
        Assert.True(ids.IsReadOnly);
        Assert.True(candidates.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => ids.Add("x.y"));
        Assert.Throws<NotSupportedException>(() => candidates[0] = "x.y");
    }

    /// <summary>Proves a hand-built valid table parses, with one ambiguous and one single-candidate form and the supplied version.</summary>
    [Fact]
    public void ReadingInventory_Parse_ValidTables_BuildsInventory()
    {
        // Act
        var inventory = Parse(ValidReadings, ValidForms, version: 7);

        // Assert
        Assert.Equal(7, inventory.Version);
        Assert.Equal(["wind.air", "wind.coil"], inventory.ReadingIds);
        Assert.Equal(["wind.air", "wind.coil"], inventory.CandidatesFor("WIND"));
        Assert.Equal(["wind.air"], inventory.CandidatesFor("winds"));
        Assert.Equal(2, inventory.FormCount);
    }

    /// <summary>Proves a deprecated reading is parsed and reported inactive.</summary>
    [Fact]
    public void ReadingInventory_Parse_DeprecatedStatus_IsInactive()
    {
        // Arrange
        var readings = ReadingsHeader + "wind.air\twind\tNOUN\td\tactive\nwind.coil\twind\tVERB\td\tdeprecated\n";

        // Act
        var inventory = Parse(readings, ValidForms);

        // Assert
        Assert.True(inventory.TryGetReadingIndex("wind.air", out var air));
        Assert.True(inventory.TryGetReadingIndex("wind.coil", out var coil));
        Assert.True(inventory.IsActive(air));
        Assert.False(inventory.IsActive(coil));
    }

    /// <summary>Proves null readers are rejected.</summary>
    [Fact]
    public void ReadingInventory_Parse_NullReader_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ReadingInventory.Parse(null!, new StringReader(ValidForms), 1));
        Assert.Throws<ArgumentNullException>(() => ReadingInventory.Parse(new StringReader(ValidReadings), null!, 1));
    }

    /// <summary>Proves each defect of the readings table is rejected with the table name, the line number and a fixed reason.</summary>
    /// <param name="readings">The readings table text.</param>
    /// <param name="expected">The text the message must contain.</param>
    [Theory]
    [InlineData("\uFEFF" + "reading_id\tlemma\tpos_hint\tdescription\tstatus\n", "readings.tsv line 1: a byte-order mark is not allowed")]
    [InlineData("reading_id\tlemma\tpos_hint\tdescription\tstatus\r\n", "readings.tsv line 1: a carriage return is not allowed")]
    [InlineData("id\tlemma\tpos_hint\tdescription\tstatus\n", "readings.tsv line 1: the header is not the expected one")]
    [InlineData("", "readings.tsv line 1: the table is empty")]
    [InlineData(ReadingsHeader + "wind.air\twind\tNOUN\tactive\n", "readings.tsv line 2: the row does not have the expected number of columns")]
    [InlineData(ReadingsHeader + "wind.air\twind\tNOUN\td\tactive\textra\n", "readings.tsv line 2: the row does not have the expected number of columns")]
    [InlineData(ReadingsHeader + "Wind.air\twind\tNOUN\td\tactive\n", "readings.tsv line 2: the reading id is not well-formed")]
    [InlineData(ReadingsHeader + "wind\twind\tNOUN\td\tactive\n", "readings.tsv line 2: the reading id is not well-formed")]
    [InlineData(ReadingsHeader + "wind.air\t\tNOUN\td\tactive\n", "readings.tsv line 2: the lemma is empty")]
    [InlineData(ReadingsHeader + "wind.air\twind\tNOUN\td\tretired\n", "readings.tsv line 2: the status must be 'active' or 'deprecated'")]
    [InlineData(ReadingsHeader + "wind.air\twind\tNOUN\td\tactive\nwind.air\twind\tNOUN\td\tactive\n", "readings.tsv line 3: duplicate reading id")]
    [InlineData(ReadingsHeader + "wind.air\twind\tNOUN\td\tactive\n\n", "readings.tsv line 3: the row does not have the expected number of columns")]
    public void ReadingInventory_Parse_MalformedReadings_ThrowsInvalidDataExceptionWithLineAndReason(string readings, string expected)
    {
        // Act
        var exception = Assert.Throws<InvalidDataException>(() => Parse(readings, ValidForms));

        // Assert
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves each defect of the forms table is rejected with the table name, the line number and a fixed reason.</summary>
    /// <param name="forms">The forms table text.</param>
    /// <param name="expected">The text the message must contain.</param>
    [Theory]
    [InlineData("form\treading_id\tkind\r\n", "forms.tsv line 1: a carriage return is not allowed")]
    [InlineData("form\treading\tkind\n", "forms.tsv line 1: the header is not the expected one")]
    [InlineData(FormsHeader + "wind\twind.ghost\torigin\n", "forms.tsv line 2: the row names a reading that is not in the readings table")]
    [InlineData(FormsHeader + "Wind\twind.air\torigin\n", "forms.tsv line 2: the form must be 1 to 32 ASCII lower-case letters")]
    [InlineData(FormsHeader + "w\u00EFnd\twind.air\torigin\n", "forms.tsv line 2: the form must be 1 to 32 ASCII lower-case letters")]
    [InlineData(FormsHeader + "wind-up\twind.air\torigin\n", "forms.tsv line 2: the form must be 1 to 32 ASCII lower-case letters")]
    [InlineData(FormsHeader + "\twind.air\torigin\n", "forms.tsv line 2: the form must be 1 to 32 ASCII lower-case letters")]
    [InlineData(FormsHeader + "wind\twind.air\t\n", "forms.tsv line 2: the kind is empty")]
    [InlineData(FormsHeader + "wind\twind.air\torigin\nwind\twind.air\torigin\n", "forms.tsv line 3: duplicate form and reading pair")]
    [InlineData(FormsHeader + "wind\twind.air\torigin\twhat\n", "forms.tsv line 2: the row does not have the expected number of columns")]
    public void ReadingInventory_Parse_MalformedForms_ThrowsInvalidDataExceptionWithLineAndReason(string forms, string expected)
    {
        // Act
        var exception = Assert.Throws<InvalidDataException>(() => Parse(ValidReadings, forms));

        // Assert
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves the messages never contain the offending data, only fixed text.</summary>
    [Fact]
    public void ReadingInventory_Parse_Malformed_MessageNeverContainsTheData()
    {
        // Arrange
        var forms = FormsHeader + "Sentinel\twind.air\torigin\n";

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => Parse(ValidReadings, forms));

        // Assert
        Assert.DoesNotContain("Sentinel", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves a form with more than 16 candidates is rejected at the seventeenth.</summary>
    [Fact]
    public void ReadingInventory_Parse_SeventeenCandidates_IsRejected()
    {
        // Arrange: 17 readings of one lemma, all candidates of the same form
        var readings = ReadingsHeader + string.Concat(Enumerable.Range(1, 17).Select(i => $"x.a{i}\tx\tNOUN\td\tactive\n"));
        var forms = FormsHeader + string.Concat(Enumerable.Range(1, 17).Select(i => $"x\tx.a{i}\torigin\n"));

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => Parse(readings, forms));

        // Assert
        Assert.Contains("forms.tsv line 18: the form has too many candidate readings", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves sixteen candidates are accepted.</summary>
    [Fact]
    public void ReadingInventory_Parse_SixteenCandidates_IsAccepted()
    {
        // Arrange
        var readings = ReadingsHeader + string.Concat(Enumerable.Range(1, 16).Select(i => $"x.a{i}\tx\tNOUN\td\tactive\n"));
        var forms = FormsHeader + string.Concat(Enumerable.Range(1, 16).Select(i => $"x\tx.a{i}\torigin\n"));

        // Act
        var inventory = Parse(readings, forms);

        // Assert
        Assert.Equal(16, inventory.CandidatesFor("x").Count);
    }

    /// <summary>Proves an over-long line is rejected without reading the rest of it.</summary>
    [Fact]
    public void ReadingInventory_Parse_OverLongLine_IsRejected()
    {
        // Arrange
        var readings = ReadingsHeader + "wind.air\twind\tNOUN\t" + new string('d', 1_100) + "\tactive\n";

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => Parse(readings, ValidForms));

        // Assert
        Assert.Contains("readings.tsv line 2: the line is too long", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves more rows than the limit are rejected in both tables.</summary>
    [Fact]
    public void ReadingInventory_Parse_TooManyRows_IsRejected()
    {
        // Arrange: 5,001 readings rows, and 5,001 forms rows (each a distinct all-letter form)
        var readings = ReadingsHeader + string.Concat(Enumerable.Range(1, 5_001).Select(i => $"w.a{i}\tw\tNOUN\td\tactive\n"));
        var forms = FormsHeader + string.Concat(Enumerable.Range(1, 5_001).Select(i => $"{Letters(i)}\twind.air\tk\n"));

        // Act
        var readingsException = Assert.Throws<InvalidDataException>(() => Parse(readings, ValidForms));
        var formsException = Assert.Throws<InvalidDataException>(() => Parse(ValidReadings, forms));

        // Assert
        Assert.Contains("the table has too many rows", readingsException.Message, StringComparison.Ordinal);
        Assert.Contains("the table has too many rows", formsException.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves the reading-id grammar accepts exactly two or three well-formed dot-separated segments.</summary>
    /// <param name="id">The candidate id.</param>
    /// <param name="valid">Whether it is valid.</param>
    [Theory]
    [InlineData("record.noun", true)]
    [InlineData("abstract.adj-noun", true)]
    [InlineData("a.b.c", true)]
    [InlineData("x1.y2-z3", true)]
    [InlineData("", false)]
    [InlineData("record", false)]
    [InlineData("a.b.c.d", false)]
    [InlineData("Record.noun", false)]
    [InlineData("record.Noun", false)]
    [InlineData("record. noun", false)]
    [InlineData("record.noun ", false)]
    [InlineData("record..noun", false)]
    [InlineData(".noun", false)]
    [InlineData("record.", false)]
    [InlineData("-a.b", false)]
    [InlineData("a-.b", false)]
    [InlineData("a.-b", false)]
    [InlineData("a.b-", false)]
    [InlineData("a_b.c", false)]
    [InlineData("a|b.c", false)]
    [InlineData("a:b.c", false)]
    [InlineData("\u0430.b", false)]
    public void ReadingIdSyntax_IsValid_Grammar_AcceptsOnlyWellFormedIds(string id, bool valid)
    {
        // Act / Assert
        Assert.Equal(valid, ReadingIdSyntax.IsValid(id));
    }

    /// <summary>Proves the id length limit is 64 characters.</summary>
    [Fact]
    public void ReadingIdSyntax_IsValid_LengthLimit_Is64()
    {
        // Arrange
        var atLimit = new string('a', 60) + ".bbb";
        var overLimit = new string('a', 61) + ".bbb";

        // Act / Assert
        Assert.Equal(64, atLimit.Length);
        Assert.True(ReadingIdSyntax.IsValid(atLimit));
        Assert.False(ReadingIdSyntax.IsValid(overLimit));
    }

    /// <summary>Proves the role of an id is the text after its first dot.</summary>
    [Fact]
    public void ReadingIdSyntax_RoleOf_ReturnsTextAfterFirstDot()
    {
        // Act / Assert
        Assert.Equal("noun", ReadingIdSyntax.RoleOf("record.noun"));
        Assert.Equal("b.c", ReadingIdSyntax.RoleOf("a.b.c"));
    }

    /// <summary>Proves the lookup behaves the same under cultures whose case rules differ (Turkish dotted I).</summary>
    [Fact]
    public void ReadingInventory_CandidatesFor_TurkishCulture_FoldsAsciiOnly()
    {
        // Arrange
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            // Act
            var upper = ReadingInventory.Default.CandidatesFor("RECORD");

            // Assert: ASCII capitals fold; the dotted capital I is not an ASCII letter and never matches
            Assert.Equal(["record.noun", "record.verb"], upper);
            Assert.Empty(ReadingInventory.Default.CandidatesFor("REC\u0130"));
            Assert.Empty(ReadingInventory.Default.CandidatesFor("m\u0130nd"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>
    ///     Writes a positive number as a string of lower-case letters (base 26), so each number is a distinct valid form.
    /// </summary>
    /// <param name="number">The number, at least 1.</param>
    /// <returns>The letters.</returns>
    private static string Letters(int number)
    {
        var letters = new System.Text.StringBuilder();
        while (number > 0)
        {
            number--;
            letters.Insert(0, (char)('a' + (number % 26)));
            number /= 26;
        }

        return letters.ToString();
    }

    /// <summary>
    ///     Parses two table texts.
    /// </summary>
    /// <param name="readings">The readings table text.</param>
    /// <param name="forms">The forms table text.</param>
    /// <param name="version">The version to assign.</param>
    /// <returns>The inventory.</returns>
    private static ReadingInventory Parse(string readings, string forms, int version = 1) =>
        ReadingInventory.Parse(new StringReader(readings), new StringReader(forms), version);
}
