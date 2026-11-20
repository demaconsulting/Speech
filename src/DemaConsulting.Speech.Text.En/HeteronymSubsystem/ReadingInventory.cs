using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace DemaConsulting.Speech.Text.En.HeteronymSubsystem;

/// <summary>
///     The grammar of a reading id such as <c>record.noun</c>: the one definition the inventory
///     loader, the override validation and the typed annotation reader all share, so what is
///     accepted when a reading is declared and when an annotation is read cannot drift apart.
/// </summary>
/// <remarks>
///     An id has two or three segments separated by a single dot. Each segment is one or more of
///     the characters <c>a</c> to <c>z</c>, <c>0</c> to <c>9</c> and <c>-</c>, and does not start
///     or end with <c>-</c>; the whole id is at most <see cref="MaxLength"/> characters. The
///     role of an id is the text after its first dot. The type is static and thread-safe.
/// </remarks>
internal static class ReadingIdSyntax
{
    /// <summary>The most UTF-16 code units of a reading id.</summary>
    internal const int MaxLength = 64;

    /// <summary>The most dot-separated segments of a reading id.</summary>
    private const int MaxSegments = 3;

    /// <summary>
    ///     Tells whether text is a well-formed reading id.
    /// </summary>
    /// <param name="id">The candidate.</param>
    /// <returns><see langword="true"/> when the text satisfies the grammar; the id is not looked up in any inventory.</returns>
    internal static bool IsValid(ReadOnlySpan<char> id)
    {
        if (id.IsEmpty || id.Length > MaxLength)
        {
            return false;
        }

        // Walk the segments: each must be non-empty, made of [a-z0-9-] and not touch a hyphen at either edge.
        var segments = 0;
        var segmentStart = 0;
        for (var i = 0; i <= id.Length; i++)
        {
            if (i < id.Length && id[i] != '.')
            {
                if (id[i] is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
                {
                    return false;
                }

                continue;
            }

            var length = i - segmentStart;
            if (length == 0 || id[segmentStart] == '-' || id[i - 1] == '-')
            {
                return false;
            }

            segments++;
            segmentStart = i + 1;
        }

        return segments is >= 2 and <= MaxSegments;
    }

    /// <summary>
    ///     Gets the role of a well-formed reading id: the text after its first dot.
    /// </summary>
    /// <param name="id">A reading id that satisfies <see cref="IsValid"/>.</param>
    /// <returns>The role, for example <c>noun</c> for <c>record.noun</c>.</returns>
    internal static string RoleOf(string id) => id[(id.IndexOf('.') + 1)..];
}

/// <summary>
///     The inventory of heteronym readings: which pronunciation readings exist (for example
///     <c>record.noun</c> and <c>record.verb</c>) and which written forms (<c>record</c>,
///     <c>records</c>, <c>recorded</c>) can carry which of them.
/// </summary>
/// <remarks>
///     <para>
///     The inventory is the pair of tables <c>readings.tsv</c> and <c>forms.tsv</c> of the
///     repository's <c>data/text-en</c> folder, embedded in the assembly; the data is the single
///     source of truth. <see cref="Default"/> loads the embedded tables once, on first use, and the
///     instance is immutable and thread-safe. A malformed embedded table is a defective build and
///     throws <see cref="InvalidDataException"/> with a line number and a fixed reason; the
///     failure is not cached as a degraded state because nothing can recover from it.
///     </para>
///     <para>
///     <b>Version.</b> <see cref="Version"/> is an integer revision of the inventory kept in code
///     (revision 2 is inventory v1.1 with 395 readings); a test pins it so a data change cannot
///     pass without a deliberate version decision. A model records the revision it was trained
///     for and is accepted only by an inventory of the same or a newer revision, because a model
///     binds its weights to reading ids, never to positions.
///     </para>
///     <para>
///     <b>Matching.</b> A form is an ASCII-letter word compared with ASCII case folding only (never
///     a culture-aware comparison), against a whole tokenizer token; a hyphenated compound never
///     matches because <c>-</c> is not a letter. The candidates of a form keep the order of the
///     rows in <c>forms.tsv</c>, which is also the tie-break order of the classifier (the
///     inventory has no default reading).
///     </para>
/// </remarks>
public sealed class ReadingInventory
{
    /// <summary>The revision of the inventory that this build embeds.</summary>
    internal const int CurrentVersion = 2;

    /// <summary>The most readings a table may declare.</summary>
    internal const int MaxReadings = 5_000;

    /// <summary>The most rows the forms table may have.</summary>
    internal const int MaxFormRows = 5_000;

    /// <summary>The most candidate readings of one form.</summary>
    internal const int MaxCandidates = 16;

    /// <summary>The most characters of one table line, not counting the terminator.</summary>
    internal const int MaxLineLength = 1_024;

    /// <summary>The most characters of a form.</summary>
    internal const int MaxFormLength = 32;

    /// <summary>The manifest name of the embedded readings table.</summary>
    internal const string ReadingsResourceName = "DemaConsulting.Speech.Text.En.Resources.readings.tsv";

    /// <summary>The manifest name of the embedded forms table.</summary>
    internal const string FormsResourceName = "DemaConsulting.Speech.Text.En.Resources.forms.tsv";

    /// <summary>The header of the readings table.</summary>
    private const string ReadingsHeader = "reading_id\tlemma\tpos_hint\tdescription\tstatus";

    /// <summary>The header of the forms table.</summary>
    private const string FormsHeader = "form\treading_id\tkind";

    /// <summary>The number of tab-separated columns of the readings table.</summary>
    private const int ReadingsColumns = 5;

    /// <summary>The number of tab-separated columns of the forms table.</summary>
    private const int FormsColumns = 3;

    /// <summary>The lazily loaded embedded inventory.</summary>
    private static readonly Lazy<ReadingInventory> DefaultInventory = new(LoadEmbedded, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The shared empty candidate list returned for an unknown form.</summary>
    private static readonly IReadOnlyList<string> NoCandidates = Array.Empty<string>();

    /// <summary>The revision of this inventory.</summary>
    private readonly int _version;

    /// <summary>The reading ids in table order.</summary>
    private readonly string[] _ids;

    /// <summary>Whether each reading (by table index) is active.</summary>
    private readonly bool[] _active;

    /// <summary>Whether each reading's part-of-speech hint (by table index) includes <c>ADJ</c>.</summary>
    private readonly bool[] _adjective;

    /// <summary>The index of each reading id.</summary>
    private readonly Dictionary<string, int> _indexById;

    /// <summary>The ordinal-sorted, read-only list of ids handed out by <see cref="ReadingIds"/>.</summary>
    private readonly ReadOnlyCollection<string> _sortedIds;

    /// <summary>The label salt of each reading.</summary>
    private readonly uint[] _labelSalts;

    /// <summary>The role salt of each reading.</summary>
    private readonly uint[] _roleSalts;

    /// <summary>The distinct forms in order of first appearance.</summary>
    private readonly string[] _forms;

    /// <summary>The first index into <see cref="_candidates"/> of each form.</summary>
    private readonly int[] _candidateStart;

    /// <summary>The number of candidates of each form.</summary>
    private readonly int[] _candidateCount;

    /// <summary>The candidate reading indexes of every form, form after form.</summary>
    private readonly int[] _candidates;

    /// <summary>The candidate ids of each form, for <see cref="CandidatesFor"/>.</summary>
    private readonly IReadOnlyList<string>[] _candidateIds;

    /// <summary>The open-addressing table of form indexes plus one (0 is an empty slot); its length is a power of two.</summary>
    private readonly int[] _table;

    /// <summary>The length of the shortest form.</summary>
    private readonly int _minFormLength;

    /// <summary>The length of the longest form.</summary>
    private readonly int _maxFormLength;

    /// <summary>
    ///     Initializes a new instance from validated tables.
    /// </summary>
    /// <param name="version">The revision.</param>
    /// <param name="ids">The reading ids, unique, in table order.</param>
    /// <param name="active">Whether each reading is active.</param>
    /// <param name="adjective">Whether each reading's part-of-speech hint includes <c>ADJ</c>.</param>
    /// <param name="forms">The distinct forms in order of first appearance.</param>
    /// <param name="candidateLists">For each form, its candidate reading indexes in table order.</param>
    private ReadingInventory(int version, string[] ids, bool[] active, bool[] adjective, string[] forms, List<int>[] candidateLists)
    {
        _version = version;
        _ids = ids;
        _active = active;
        _adjective = adjective;
        _forms = forms;

        // Index and salts of the readings, computed once so the hot path does no hashing of ids.
        _indexById = new Dictionary<string, int>(ids.Length, StringComparer.Ordinal);
        _labelSalts = new uint[ids.Length];
        _roleSalts = new uint[ids.Length];
        for (var i = 0; i < ids.Length; i++)
        {
            _indexById[ids[i]] = i;
            _labelSalts[i] = FeatureHashing.LabelSalt(ids[i]);
            _roleSalts[i] = FeatureHashing.RoleSalt(ReadingIdSyntax.RoleOf(ids[i]));
        }

        var sorted = (string[])ids.Clone();
        Array.Sort(sorted, StringComparer.Ordinal);
        _sortedIds = Array.AsReadOnly(sorted);

        // Flatten the candidate lists and keep one read-only id list per form for the public lookup.
        _candidateStart = new int[forms.Length];
        _candidateCount = new int[forms.Length];
        _candidateIds = new IReadOnlyList<string>[forms.Length];
        var flat = new List<int>();
        _minFormLength = int.MaxValue;
        _maxFormLength = 0;
        for (var f = 0; f < forms.Length; f++)
        {
            _candidateStart[f] = flat.Count;
            _candidateCount[f] = candidateLists[f].Count;
            var names = new string[candidateLists[f].Count];
            for (var c = 0; c < names.Length; c++)
            {
                flat.Add(candidateLists[f][c]);
                names[c] = ids[candidateLists[f][c]];
            }

            _candidateIds[f] = Array.AsReadOnly(names);
            _minFormLength = Math.Min(_minFormLength, forms[f].Length);
            _maxFormLength = Math.Max(_maxFormLength, forms[f].Length);
        }

        _candidates = [.. flat];

        // Open-addressing table over the folded FNV-1a of each form; at most half full, so a probe is short.
        var size = 16;
        while (size < forms.Length * 2)
        {
            size <<= 1;
        }

        _table = new int[size];
        for (var f = 0; f < forms.Length; f++)
        {
            var slot = (int)(FeatureHashing.Fnv1aFolded(forms[f]) & (uint)(size - 1));
            while (_table[slot] != 0)
            {
                slot = (slot + 1) & (size - 1);
            }

            _table[slot] = f + 1;
        }
    }

    /// <summary>
    ///     Gets the inventory embedded in this assembly, loaded once on first use.
    /// </summary>
    /// <exception cref="InvalidDataException">The embedded tables are malformed (a defective build); the message names the table, the line and a fixed reason.</exception>
    /// <remarks>Thread-safe; the first caller loads the tables and every later caller shares the instance.</remarks>
    public static ReadingInventory Default => DefaultInventory.Value;

    /// <summary>
    ///     Gets the revision of this inventory (2 for the embedded tables).
    /// </summary>
    /// <remarks>
    ///     A heteronym model records the revision it was trained for and is accepted only by an
    ///     inventory with the same or a higher revision.
    /// </remarks>
    public int Version => _version;

    /// <summary>
    ///     Gets every reading id of the inventory, sorted by ordinal comparison.
    /// </summary>
    public IReadOnlyCollection<string> ReadingIds => _sortedIds;

    /// <summary>
    ///     Gets the number of distinct forms.
    /// </summary>
    internal int FormCount => _forms.Length;

    /// <summary>
    ///     Gets the number of readings.
    /// </summary>
    internal int ReadingCount => _ids.Length;

    /// <summary>
    ///     Tells whether a reading id is declared in the inventory (active or not).
    /// </summary>
    /// <param name="readingId">The reading id, compared exactly. Must not be null.</param>
    /// <returns><see langword="true"/> when the inventory declares the id.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="readingId"/> is null.</exception>
    /// <remarks>Thread-safe; allocation-free.</remarks>
    public bool Contains(string readingId)
    {
        ArgumentNullException.ThrowIfNull(readingId);
        return _indexById.ContainsKey(readingId);
    }

    /// <summary>
    ///     Gets the reading ids a written form can carry.
    /// </summary>
    /// <param name="surfaceForm">
    ///     The word, compared with ASCII case folding. Must not be null. Anything that is not
    ///     exactly an inventory form (an empty text, a hyphenated compound, a word with a non-ASCII
    ///     letter, the Kelvin sign) has no candidates.
    /// </param>
    /// <returns>
    ///     The candidate reading ids in the order of the rows of <c>forms.tsv</c>; an empty list for
    ///     an unknown form. A form with exactly one candidate is not ambiguous.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="surfaceForm"/> is null.</exception>
    /// <remarks>
    ///     For hosts and tests. The analyzer uses an allocation-free internal lookup instead. The
    ///     returned list is immutable and shared. Thread-safe.
    /// </remarks>
    public IReadOnlyList<string> CandidatesFor(string surfaceForm)
    {
        ArgumentNullException.ThrowIfNull(surfaceForm);
        return TryFindForm(surfaceForm, out var formIndex) ? _candidateIds[formIndex] : NoCandidates;
    }

    /// <summary>
    ///     Finds the form that equals a token, without allocating.
    /// </summary>
    /// <param name="token">The token's characters, compared with ASCII case folding.</param>
    /// <param name="formIndex">The index of the form when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the whole token is a form of the inventory.</returns>
    /// <remarks>
    ///     A token of the wrong length, or one with any character that is not an ASCII letter, is
    ///     rejected before any hashing is finished, so the common word costs a few comparisons.
    /// </remarks>
    internal bool TryFindForm(ReadOnlySpan<char> token, out int formIndex)
    {
        formIndex = -1;
        if (token.Length < _minFormLength || token.Length > _maxFormLength)
        {
            return false;
        }

        // Fold and hash in one pass; a non-letter can never be part of a form, so stop at once.
        var hash = FeatureHashing.FnvOffsetBasis;
        foreach (var unit in token)
        {
            if (unit is not (>= 'a' and <= 'z' or >= 'A' and <= 'Z'))
            {
                return false;
            }

            hash = FeatureHashing.Update(hash, FeatureHashing.FoldAscii(unit));
        }

        // Probe until an empty slot; compare the folded characters of the form at each occupied slot.
        var mask = _table.Length - 1;
        var slot = (int)(hash & (uint)mask);
        while (true)
        {
            var entry = _table[slot];
            if (entry == 0)
            {
                return false;
            }

            if (EqualsFolded(_forms[entry - 1], token))
            {
                formIndex = entry - 1;
                return true;
            }

            slot = (slot + 1) & mask;
        }
    }

    /// <summary>
    ///     Gets the candidate reading indexes of a form.
    /// </summary>
    /// <param name="formIndex">A form index returned by <see cref="TryFindForm"/>.</param>
    /// <returns>The reading indexes in table order; one or more.</returns>
    internal ReadOnlySpan<int> GetCandidates(int formIndex) => _candidates.AsSpan(_candidateStart[formIndex], _candidateCount[formIndex]);

    /// <summary>
    ///     Gets the id of a reading.
    /// </summary>
    /// <param name="readingIndex">The reading's table index.</param>
    /// <returns>The reading id.</returns>
    internal string ReadingIdAt(int readingIndex) => _ids[readingIndex];

    /// <summary>
    ///     Gets the text of a form.
    /// </summary>
    /// <param name="formIndex">A form index returned by <see cref="TryFindForm"/>.</param>
    /// <returns>The form's lower-case text.</returns>
    internal string FormAt(int formIndex) => _forms[formIndex];

    /// <summary>
    ///     Tells whether a reading is active.
    /// </summary>
    /// <param name="readingIndex">The reading's table index.</param>
    /// <returns><see langword="true"/> for an active reading.</returns>
    internal bool IsActive(int readingIndex) => _active[readingIndex];

    /// <summary>
    ///     Tells whether a reading is an adjective use of its lemma.
    /// </summary>
    /// <param name="readingIndex">The reading's table index.</param>
    /// <returns><see langword="true"/> when the reading's part-of-speech hint in <c>readings.tsv</c> includes <c>ADJ</c> (<c>minute.tiny</c>, <c>close.adj-noun</c>).</returns>
    internal bool IsAdjective(int readingIndex) => _adjective[readingIndex];

    /// <summary>
    ///     Looks up the table index of a reading id.
    /// </summary>
    /// <param name="readingId">The id, compared exactly.</param>
    /// <param name="readingIndex">The index when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the inventory declares the id.</returns>
    internal bool TryGetReadingIndex(string readingId, out int readingIndex) => _indexById.TryGetValue(readingId, out readingIndex);

    /// <summary>
    ///     Gets the label salt of a reading.
    /// </summary>
    /// <param name="readingIndex">The reading's table index.</param>
    /// <returns>The salt that binds a feature to this exact reading.</returns>
    internal uint LabelSalt(int readingIndex) => _labelSalts[readingIndex];

    /// <summary>
    ///     Gets the role salt of a reading.
    /// </summary>
    /// <param name="readingIndex">The reading's table index.</param>
    /// <returns>The salt that binds a feature to the reading's role, shared across lemmas.</returns>
    internal uint RoleSalt(int readingIndex) => _roleSalts[readingIndex];

    /// <summary>
    ///     Parses an inventory from two tables.
    /// </summary>
    /// <param name="readings">The readings table (header, then one row per reading). Must not be null; not disposed.</param>
    /// <param name="forms">The forms table (header, then one row per candidate). Must not be null; not disposed.</param>
    /// <param name="version">The revision to assign.</param>
    /// <returns>The immutable inventory.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="readings"/> or <paramref name="forms"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     A table is malformed: a byte-order mark or carriage return, a wrong header, a wrong
    ///     column count, an over-long line, too many rows, a duplicate or ill-formed reading id, an
    ///     unknown status, a form that is not 1 to 32 ASCII lower-case letters, a row that names an
    ///     unknown reading, a duplicate form/reading pair or more than 16 candidates. The message
    ///     names the table and line and a fixed reason, never the data.
    /// </exception>
    /// <remarks>
    ///     The parser is bounded in rows, line length and candidates, so a hostile or corrupt table
    ///     cannot make it allocate without limit. Production code uses <see cref="Default"/>; this
    ///     factory exists so tests can feed hand-built tables.
    /// </remarks>
    internal static ReadingInventory Parse(TextReader readings, TextReader forms, int version)
    {
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(forms);

        // Table 1: the readings, unique by id.
        var ids = new List<string>();
        var active = new List<bool>();
        var adjective = new List<bool>();
        var indexById = new Dictionary<string, int>(StringComparer.Ordinal);
        var buffer = new char[MaxLineLength];
        ReadTable(readings, "readings.tsv", ReadingsHeader, ReadingsColumns, buffer, MaxReadings, (fields, line) =>
        {
            var id = fields[0].ToString();
            if (!ReadingIdSyntax.IsValid(id))
            {
                throw Error("readings.tsv", line, "the reading id is not well-formed");
            }

            if (fields[1].IsEmpty)
            {
                throw Error("readings.tsv", line, "the lemma is empty");
            }

            var status = fields[4];
            if (!status.SequenceEqual("active") && !status.SequenceEqual("deprecated"))
            {
                throw Error("readings.tsv", line, "the status must be 'active' or 'deprecated'");
            }

            if (!indexById.TryAdd(id, ids.Count))
            {
                throw Error("readings.tsv", line, "duplicate reading id");
            }

            ids.Add(id);
            active.Add(status.SequenceEqual("active"));
            adjective.Add(HasTag(fields[2], "ADJ"));
        });

        // Table 2: the forms, grouped by form in order of first appearance, each with its candidates in row order.
        var formNames = new List<string>();
        var formIndexByName = new Dictionary<string, int>(StringComparer.Ordinal);
        var candidateLists = new List<List<int>>();
        ReadTable(forms, "forms.tsv", FormsHeader, FormsColumns, buffer, MaxFormRows, (fields, line) =>
        {
            var form = fields[0];
            if (form.IsEmpty || form.Length > MaxFormLength || !IsLowerAscii(form))
            {
                throw Error("forms.tsv", line, "the form must be 1 to 32 ASCII lower-case letters");
            }

            if (!indexById.TryGetValue(fields[1].ToString(), out var readingIndex))
            {
                throw Error("forms.tsv", line, "the row names a reading that is not in the readings table");
            }

            if (fields[2].IsEmpty)
            {
                throw Error("forms.tsv", line, "the kind is empty");
            }

            var name = form.ToString();
            if (!formIndexByName.TryGetValue(name, out var formIndex))
            {
                formIndex = formNames.Count;
                formIndexByName[name] = formIndex;
                formNames.Add(name);
                candidateLists.Add([]);
            }

            var list = candidateLists[formIndex];
            if (list.Contains(readingIndex))
            {
                throw Error("forms.tsv", line, "duplicate form and reading pair");
            }

            if (list.Count >= MaxCandidates)
            {
                throw Error("forms.tsv", line, "the form has too many candidate readings");
            }

            list.Add(readingIndex);
        });

        return new ReadingInventory(version, [.. ids], [.. active], [.. adjective], [.. formNames], [.. candidateLists]);
    }

    /// <summary>
    ///     Tells whether a part-of-speech hint (tags separated by <c>|</c>, such as <c>ADJ|NOUN</c>) holds a tag.
    /// </summary>
    /// <param name="hint">The hint column.</param>
    /// <param name="tag">The tag, compared exactly.</param>
    /// <returns><see langword="true"/> when one of the hint's tags is <paramref name="tag"/>.</returns>
    private static bool HasTag(ReadOnlySpan<char> hint, string tag)
    {
        // Walk the tags one separator at a time (allocation-free on every target framework)
        var rest = hint;
        while (true)
        {
            var separator = rest.IndexOf('|');
            var current = separator < 0 ? rest : rest[..separator];
            if (current.SequenceEqual(tag))
            {
                return true;
            }

            if (separator < 0)
            {
                return false;
            }

            rest = rest[(separator + 1)..];
        }
    }

    /// <summary>
    ///     Loads the inventory from the tables embedded in this assembly.
    /// </summary>
    /// <returns>The inventory with the current revision.</returns>
    /// <exception cref="InvalidDataException">A table is missing from the assembly or malformed.</exception>
    private static ReadingInventory LoadEmbedded()
    {
        var assembly = typeof(ReadingInventory).Assembly;
        using var readingsStream = assembly.GetManifestResourceStream(ReadingsResourceName)
            ?? throw new InvalidDataException("The embedded readings table is missing from the assembly.");
        using var formsStream = assembly.GetManifestResourceStream(FormsResourceName)
            ?? throw new InvalidDataException("The embedded forms table is missing from the assembly.");

        // A strict UTF-8 decoder that never skips a byte-order mark, so a BOM is reported as a defect.
        var encoding = new UTF8Encoding(false, true);
        using var readings = new StreamReader(readingsStream, encoding, false);
        using var forms = new StreamReader(formsStream, encoding, false);
        return Parse(readings, forms, CurrentVersion);
    }

    /// <summary>
    ///     Reads a table line by line through a bounded buffer and hands each data row's fields to a callback.
    /// </summary>
    /// <param name="reader">The table text.</param>
    /// <param name="table">The table's file name, for messages.</param>
    /// <param name="header">The exact header line.</param>
    /// <param name="columns">The exact number of tab-separated columns.</param>
    /// <param name="buffer">A reusable buffer of <see cref="MaxLineLength"/> characters.</param>
    /// <param name="maxRows">The most data rows.</param>
    /// <param name="onRow">Called with the row's fields and its one-based line number.</param>
    /// <exception cref="InvalidDataException">The table is malformed.</exception>
    private static void ReadTable(TextReader reader, string table, string header, int columns, char[] buffer, int maxRows, RowHandler onRow)
    {
        var lineNumber = 0;
        var rows = 0;
        var fields = new Range[columns];
        while (true)
        {
            var length = ReadLine(reader, buffer, table, lineNumber + 1);
            if (length < 0)
            {
                break;
            }

            lineNumber++;
            var line = buffer.AsSpan(0, length);
            if (lineNumber == 1)
            {
                if (!line.SequenceEqual(header))
                {
                    throw Error(table, 1, "the header is not the expected one");
                }

                continue;
            }

            if (++rows > maxRows)
            {
                throw Error(table, lineNumber, "the table has too many rows");
            }

            // Split at the tabs into exactly the expected number of columns, without allocating.
            var count = 0;
            var fieldStart = 0;
            for (var i = 0; i <= line.Length; i++)
            {
                if (i < line.Length && line[i] != '\t')
                {
                    continue;
                }

                if (count == columns)
                {
                    throw Error(table, lineNumber, "the row does not have the expected number of columns");
                }

                fields[count++] = fieldStart..i;
                fieldStart = i + 1;
            }

            if (count != columns)
            {
                throw Error(table, lineNumber, "the row does not have the expected number of columns");
            }

            var parts = new FieldList(line, fields);
            onRow(parts, lineNumber);
        }

        if (lineNumber == 0)
        {
            throw Error(table, 1, "the table is empty");
        }
    }

    /// <summary>
    ///     Reads one LF-terminated line through the bounded buffer.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="buffer">The buffer; its length is the line limit.</param>
    /// <param name="table">The table's file name, for messages.</param>
    /// <param name="lineNumber">The one-based number of the line being read.</param>
    /// <returns>The length of the line without its terminator, or -1 at the end of the table.</returns>
    /// <exception cref="InvalidDataException">The line has a byte-order mark, a carriage return or too many characters.</exception>
    private static int ReadLine(TextReader reader, char[] buffer, string table, int lineNumber)
    {
        var length = 0;
        var any = false;
        while (true)
        {
            var next = reader.Read();
            if (next < 0)
            {
                return any ? length : -1;
            }

            any = true;
            if (next == '\n')
            {
                return length;
            }

            if (next == '\r')
            {
                throw Error(table, lineNumber, "a carriage return is not allowed");
            }

            if (next == '\uFEFF' && lineNumber == 1 && length == 0)
            {
                throw Error(table, lineNumber, "a byte-order mark is not allowed");
            }

            if (length == buffer.Length)
            {
                throw Error(table, lineNumber, "the line is too long");
            }

            buffer[length++] = (char)next;
        }
    }

    /// <summary>
    ///     Tells whether every character of a form is an ASCII lower-case letter.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <returns><see langword="true"/> when all characters are <c>a</c> to <c>z</c>.</returns>
    private static bool IsLowerAscii(ReadOnlySpan<char> form)
    {
        foreach (var unit in form)
        {
            if (unit is < 'a' or > 'z')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Compares a stored lower-case form with a token under ASCII case folding.
    /// </summary>
    /// <param name="form">The stored form, already lower case.</param>
    /// <param name="token">The token; known to hold only ASCII letters.</param>
    /// <returns><see langword="true"/> when they are equal after folding.</returns>
    private static bool EqualsFolded(string form, ReadOnlySpan<char> token)
    {
        if (form.Length != token.Length)
        {
            return false;
        }

        for (var i = 0; i < form.Length; i++)
        {
            if (form[i] != FeatureHashing.FoldAscii(token[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Creates the exception for a malformed table.
    /// </summary>
    /// <param name="table">The table's file name.</param>
    /// <param name="line">The one-based line number.</param>
    /// <param name="reason">The fixed-text reason.</param>
    /// <returns>The exception to throw.</returns>
    private static InvalidDataException Error(string table, int line, string reason) =>
        new($"{table} line {line.ToString(CultureInfo.InvariantCulture)}: {reason}.");

    /// <summary>
    ///     The callback that receives one data row.
    /// </summary>
    /// <param name="fields">The row's fields.</param>
    /// <param name="line">The one-based line number.</param>
    private delegate void RowHandler(FieldList fields, int line);

    /// <summary>
    ///     The tab-separated fields of one table line, indexable by column.
    /// </summary>
    /// <remarks>
    ///     A small value wrapper (it holds the buffer as a reference, not a span) so a row can be handed
    ///     to a callback that captures local state, which a span parameter could not be.
    /// </remarks>
    private readonly ref struct FieldList
    {
        /// <summary>The line's characters.</summary>
        private readonly ReadOnlySpan<char> _line;

        /// <summary>The ranges of the fields within the line.</summary>
        private readonly Range[] _ranges;

        /// <summary>
        ///     Initializes a new instance.
        /// </summary>
        /// <param name="line">The line's characters.</param>
        /// <param name="ranges">The field ranges within the line.</param>
        internal FieldList(ReadOnlySpan<char> line, Range[] ranges)
        {
            _line = line;
            _ranges = ranges;
        }

        /// <summary>
        ///     Gets one field.
        /// </summary>
        /// <param name="column">The zero-based column.</param>
        /// <returns>The field's characters.</returns>
        internal ReadOnlySpan<char> this[int column] => _line[_ranges[column]];
    }
}
