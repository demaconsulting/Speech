using System.Text.RegularExpressions;

namespace DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

/// <summary>
///     The quotas a populated generalization set must meet: enough rows of each kind, enough families of a bounded
///     size, enough rows that the curated cues cannot solve, and rows for at least two readings of each lemma group.
/// </summary>
/// <param name="MinFamilies">The minimum number of distinct families.</param>
/// <param name="MinFamilyRows">The minimum number of rows of one family.</param>
/// <param name="MaxFamilyRows">The maximum number of rows of one family.</param>
/// <param name="MinNovelVocabulary">The minimum number of <c>novel-vocabulary</c> rows.</param>
/// <param name="MinNovelConstruction">The minimum number of <c>novel-construction</c> rows.</param>
/// <param name="MinCueUnsolvable">The minimum number of <c>cue_unsolvable=yes</c> rows.</param>
/// <param name="MinCueUnsolvableLemmas">The minimum number of distinct lemmas among the <c>cue_unsolvable=yes</c> rows.</param>
/// <param name="MinReadingsPerLemmaGroup">The minimum number of distinct readings of every lemma group that has rows.</param>
internal sealed record GeneralizationShape(
    int MinFamilies,
    int MinFamilyRows,
    int MaxFamilyRows,
    int MinNovelVocabulary,
    int MinNovelConstruction,
    int MinCueUnsolvable,
    int MinCueUnsolvableLemmas,
    int MinReadingsPerLemmaGroup)
{
    /// <summary>Gets the quotas fixed by the generalization protocol (RUN-LOG, 2026-11-20).</summary>
    public static GeneralizationShape Default { get; } = new(30, 2, 12, 90, 70, 60, 8, 2);
}

/// <summary>
///     Checks the two evaluation overlays of the data folder: the regression overlay <c>regression.tsv</c> and the blind
///     generalization set <c>generalization.tsv</c>.
/// </summary>
/// <remarks>
///     <para>
///     <b>Regression overlay.</b> Each row refers to an existing example (a training or development row, a golden test
///     row, an analyzer test or an owner-flagged sentence) and records whether that sentence was seen in training. The
///     overlay is not a split: a row may repeat a training or development sentence, so it is exempt from the split
///     separation of <see cref="LeakageChecker"/>, but it may never refer to or repeat a holdout sentence. The
///     <c>seen_in_training</c> mark must equal the recomputed one: <c>yes</c> exactly when the row refers to a training
///     row, its sentence key equals a training key, or it is a near duplicate of training (at least eight words and 80
///     percent of its word 5-grams in the training 5-grams). A <c>data-row</c> reference must resolve to a row of
///     <c>train.tsv</c> or <c>dev.tsv</c> with the same lemma, reading, sentence, offsets and license. Golden and test
///     references are resolved by a trainer test that reads the repository's test files, since the data check is a
///     tool input check that does not know the repository layout.
///     </para>
///     <para>
///     <b>Generalization set.</b> Novelty is defined over a <i>lemma group</i>: the lemmas whose readings share a surface
///     form in <c>forms.tsv</c> (<i>wound</i> is a form of both <c>wound.injury</c> and <c>wind.coil</c>, so the lemmas
///     <c>wind</c> and <c>wound</c> form one group). Rule N1: no row's sentence key equals a key of the training,
///     development, holdout or regression data, and no row is a near duplicate of a training or development sentence.
///     Rule N2 (<c>novel-vocabulary</c> rows): every designated cue form occurs as a word of the row and in no training or
///     development sentence of the row's lemma group. Rule N3 (<c>novel-construction</c> rows): no word trigram that
///     contains the target, with the target replaced by a placeholder, at the relative positions (-2..0), (-1..+1) or
///     (0..+2), occurs at the same position around the target of a training or development row of the group; trigrams
///     that would extend beyond the sentence are not formed. The shape quotas of <see cref="GeneralizationShape"/> apply
///     only once the set has rows: the set is written later by a blind writer, and the file is committed with its header
///     only until then.
///     </para>
///     <para>
///     A staged copy of the generalization set, checked before it is committed, gets the N1 to N3 findings with a generic
///     reason only (no training sentence or row id is named), so the reason can be passed to the blind writer without
///     revealing training data. Every violation is a finding on the <c>overlay</c> check; nothing is changed. Not
///     thread-safe; one instance belongs to one run.
///     </para>
/// </remarks>
internal sealed class EvaluationOverlayChecker
{
    /// <summary>The check name used for findings.</summary>
    public const string CheckName = "overlay";

    /// <summary>The only value of the <c>source</c> column of the generalization set.</summary>
    public const string GeneralizationSource = "generalization";

    /// <summary>The kind of a row whose designated cue words are new for its lemma group.</summary>
    public const string NovelVocabulary = "novel-vocabulary";

    /// <summary>The kind of a row whose words around the target form a construction new for its lemma group.</summary>
    public const string NovelConstruction = "novel-construction";

    /// <summary>The minimum number of words of a generalization sentence.</summary>
    public const int MinWords = 6;

    /// <summary>The maximum number of words of a generalization sentence.</summary>
    public const int MaxWords = 30;

    /// <summary>The generic reason of an N1 finding of a staged set.</summary>
    public const string GenericN1 = "the sentence repeats or nearly repeats an existing sentence; write a new sentence";

    /// <summary>The generic reason of an N2 finding of a staged set (the cue form is the writer's own word).</summary>
    public const string GenericN2 = "already occurs with this lemma in the training or development data; choose a different cue word";

    /// <summary>The generic reason of an N3 finding of a staged set.</summary>
    public const string GenericN3 = "the words right around the target already occur around this word in the training or development data; change the construction";

    /// <summary>The minimum number of words for the near-duplicate test (as in <see cref="LeakageChecker"/>).</summary>
    private const int NearMinimumWords = 8;

    /// <summary>The 5-gram containment at which a sentence counts as a near duplicate (as in <see cref="LeakageChecker"/>).</summary>
    private const double NearContainment = 0.8;

    /// <summary>The value of a yes/no column that means yes.</summary>
    private const string Yes = "yes";

    /// <summary>The value of a yes/no column that means no.</summary>
    private const string No = "no";

    /// <summary>The placeholder of the target word inside a construction trigram.</summary>
    private const string TargetPlaceholder = "<T>";

    /// <summary>The id grammar of a regression row.</summary>
    private static readonly Regex RegressionId = new(@"^reg-[0-9]{4}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    /// <summary>The id grammar of a generalization row.</summary>
    private static readonly Regex GeneralizationId = new(@"^gen-[0-9]{4}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    /// <summary>The family grammar: <c>{lemma}:{cue-type}</c> in lower-case ASCII.</summary>
    private static readonly Regex FamilyPattern = new(@"^([a-z]+):[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    /// <summary>The grammar of one cue form: one word as <see cref="SentenceKeys.Words"/> splits it.</summary>
    private static readonly Regex CueFormPattern = new(@"^[a-z0-9]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    /// <summary>The reference grammar of each regression origin.</summary>
    private static readonly IReadOnlyDictionary<string, Regex> ReferencePatterns = new Dictionary<string, Regex>(StringComparer.Ordinal)
    {
        ["listening"] = new(@"^(RUN-LOG\.md|[A-Za-z]+Tests\.cs):[0-9]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5)),
        ["golden-readings"] = new(@"^readings-golden\.tsv:[0-9]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5)),
        ["golden-sentences"] = new(@"^sentences-golden\.tsv:s[0-9]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5)),
        ["golden-pipeline"] = new(@"^(piper|kokoro)-pipeline-golden\.tsv:[a-z][0-9]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5)),
        ["analyzer-test"] = new(@"^[A-Za-z]+Tests\.cs:[0-9]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5)),
        ["data-row"] = new(@"^(train|dev)\.tsv:[^\s:]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5)),
    };

    /// <summary>The holdout files no overlay row may refer to or repeat.</summary>
    private static readonly IReadOnlySet<string> HoldoutFiles = new HashSet<string>(["holdout-natural.tsv", "holdout-independent.tsv"], StringComparer.Ordinal);

    /// <summary>The data set under test.</summary>
    private readonly TextEnDataSet _data;

    /// <summary>The report that receives findings.</summary>
    private readonly CheckReport _report;

    /// <summary>The quotas of a populated generalization set.</summary>
    private readonly GeneralizationShape _shape;

    /// <summary>The lemma group of every lemma of the inventory (the ordinally smallest lemma of the group names it).</summary>
    private readonly Dictionary<string, string> _groupOfLemma;

    /// <summary>The surface forms of every lemma group.</summary>
    private readonly Dictionary<string, HashSet<string>> _formsOfGroup;

    /// <summary>The indexes built from the training and development rows, created on first use.</summary>
    private Indexes? _indexes;

    /// <summary>
    ///     Creates the checker.
    /// </summary>
    /// <param name="data">The loaded data set.</param>
    /// <param name="report">The report that receives findings and informational lines.</param>
    /// <param name="shape">The quotas of a populated generalization set; <see langword="null"/> for <see cref="GeneralizationShape.Default"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> or <paramref name="report"/> is null.</exception>
    public EvaluationOverlayChecker(TextEnDataSet data, CheckReport report, GeneralizationShape? shape = null)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _report = report ?? throw new ArgumentNullException(nameof(report));
        _shape = shape ?? GeneralizationShape.Default;
        (_groupOfLemma, _formsOfGroup) = BuildLemmaGroups(data);
    }

    /// <summary>
    ///     Runs every overlay check on the committed files: the regression overlay, then the generalization set with
    ///     findings that name the matching training or development row.
    /// </summary>
    public void Run()
    {
        CheckRegression();
        CheckGeneralization(_data.Generalization, revealMatches: true);
    }

    /// <summary>
    ///     Gets the lemma group of a lemma: the ordinally smallest lemma of the lemmas whose readings share a form with it.
    /// </summary>
    /// <param name="lemma">The lemma.</param>
    /// <returns>The group name; the lemma itself when the inventory does not know it.</returns>
    public string GroupOf(string lemma) => _groupOfLemma.TryGetValue(lemma, out var group) ? group : lemma;

    /// <summary>
    ///     Checks the regression overlay: schema, spans, references, holdout separation and the seen-in-training mark.
    /// </summary>
    public void CheckRegression()
    {
        var indexes = GetIndexes();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var targets = new Dictionary<(string Key, int? Start), string>();
        var seen = 0;
        foreach (var row in _data.Regression)
        {
            // Identity: grammatical, unique ids and no second row on the same target of the same sentence
            if (!RegressionId.IsMatch(row.Id) || !ids.Add(row.Id))
            {
                Add(row.Location, $"{row.Id}: id must be unique and match reg-NNNN");
            }

            var key = SentenceKeys.Key(row.Sentence);
            if (!targets.TryAdd((key, row.Start), row.Id))
            {
                Add(row.Location, $"{row.Id}: same sentence and target as {targets[(key, row.Start)]}");
            }

            CheckReadingAndSpan(row.Location, row.Id, row.Lemma, row.ReadingId, row.Sentence, row.Start, row.End);
            if (!LicenseIds.Allowed.Contains(row.License))
            {
                Add(row.Location, $"{row.Id}: license '{row.License}' is not allowed");
            }

            var referenced = CheckReference(row);
            CheckHoldoutSeparation(row, key, indexes);

            // The seen-in-training mark is recomputed from the reference and the training sentences
            var computed = (referenced is not null && referenced.Split == "train")
                || row.Reference.StartsWith("train.tsv:", StringComparison.Ordinal)
                || indexes.TrainKeys.ContainsKey(key)
                || IsNearDuplicate(row.Sentence, indexes.TrainGrams);
            if (row.SeenInTraining is not (Yes or No))
            {
                Add(row.Location, $"{row.Id}: seen_in_training '{row.SeenInTraining}' must be yes or no");
            }
            else if ((row.SeenInTraining == Yes) != computed)
            {
                Add(row.Location, $"{row.Id}: seen_in_training is '{row.SeenInTraining}' but the training data says '{(computed ? Yes : No)}'");
            }

            seen += computed ? 1 : 0;
        }

        var origins = _data.Regression.GroupBy(row => row.Origin).OrderBy(group => group.Key, StringComparer.Ordinal);
        _report.AddInfo(
            $"regression overlay: {_data.Regression.Count} rows, seen in training {seen}, unseen {_data.Regression.Count - seen} " +
            $"({string.Join(", ", origins.Select(group => $"{group.Key}: {group.Count()}"))})");
    }

    /// <summary>
    ///     Checks a generalization set: schema, spans, the novelty rules N1 to N3 and, once it has rows, the shape quotas.
    /// </summary>
    /// <param name="rows">The rows: the committed set, or a staged copy checked before it is committed.</param>
    /// <param name="revealMatches">
    ///     <see langword="true"/> to name the matching training or development row in N1 to N3 findings (the committed
    ///     set); <see langword="false"/> to give the generic reason only (a staged set whose findings go back to the blind
    ///     writer).
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rows"/> is null.</exception>
    public void CheckGeneralization(IReadOnlyList<GeneralizationRecord> rows, bool revealMatches)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var indexes = GetIndexes();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (!GeneralizationId.IsMatch(row.Id) || !ids.Add(row.Id))
            {
                Add(row.Location, $"{row.Id}: id must be unique and match gen-NNNN");
            }

            var key = SentenceKeys.Key(row.Sentence);
            if (!keys.TryAdd(key, row.Id))
            {
                Add(row.Location, $"{row.Id}: same sentence as {keys[key]}");
            }

            var spanValid = CheckReadingAndSpan(row.Location, row.Id, row.Lemma, row.ReadingId, row.Sentence, row.Start, row.End);
            CheckGeneralizationColumns(row);
            CheckNovelty(row, key, spanValid, indexes, revealMatches);
        }

        if (rows.Count == 0)
        {
            _report.AddInfo("generalization set: no rows yet; the shape quotas apply once the set is written");
            return;
        }

        CheckShape(rows);
        _report.AddInfo(
            $"generalization set: {rows.Count} rows, {rows.Count(row => row.Kind == NovelVocabulary)} {NovelVocabulary}, " +
            $"{rows.Count(row => row.Kind == NovelConstruction)} {NovelConstruction}, {rows.Count(row => row.CueUnsolvable == Yes)} cue-unsolvable, " +
            $"{rows.Select(row => row.Family).Distinct(StringComparer.Ordinal).Count()} families");
    }

    /// <summary>
    ///     Builds the lemma groups: lemmas whose readings share a surface form are one group.
    /// </summary>
    /// <param name="data">The data set.</param>
    /// <returns>The group of every lemma and the forms of every group.</returns>
    private static (Dictionary<string, string> GroupOfLemma, Dictionary<string, HashSet<string>> FormsOfGroup) BuildLemmaGroups(TextEnDataSet data)
    {
        // Union-find over lemmas; the representative is the ordinally smallest lemma so the names are deterministic
        var parent = new Dictionary<string, string>(StringComparer.Ordinal);
        string Find(string lemma)
        {
            parent.TryAdd(lemma, lemma);
            while (!string.Equals(parent[lemma], lemma, StringComparison.Ordinal))
            {
                lemma = parent[lemma];
            }

            return lemma;
        }

        foreach (var reading in data.Readings)
        {
            Find(reading.Lemma);
        }

        foreach (var form in data.Forms.GroupBy(form => form.Form, StringComparer.Ordinal))
        {
            var lemmas = form.Select(item => data.ReadingsById.TryGetValue(item.ReadingId, out var reading) ? reading.Lemma : null)
                .OfType<string>()
                .ToList();
            for (var index = 1; index < lemmas.Count; index++)
            {
                var a = Find(lemmas[0]);
                var b = Find(lemmas[index]);
                if (!string.Equals(a, b, StringComparison.Ordinal))
                {
                    var (low, high) = string.CompareOrdinal(a, b) < 0 ? (a, b) : (b, a);
                    parent[high] = low;
                }
            }
        }

        var groupOfLemma = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var lemma in parent.Keys.ToArray())
        {
            groupOfLemma[lemma] = Find(lemma);
        }
        var formsOfGroup = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var form in data.Forms.Where(form => data.ReadingsById.ContainsKey(form.ReadingId)))
        {
            var group = groupOfLemma[data.ReadingsById[form.ReadingId].Lemma];
            if (!formsOfGroup.TryGetValue(group, out var forms))
            {
                forms = new HashSet<string>(StringComparer.Ordinal);
                formsOfGroup[group] = forms;
            }

            forms.Add(form.Form);
        }

        return (groupOfLemma, formsOfGroup);
    }

    /// <summary>
    ///     Determines whether a sentence is a near duplicate of an indexed set: at least eight words, and at least 80
    ///     percent of its word 5-grams in the index.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <param name="grams">The 5-gram index.</param>
    /// <returns><see langword="true"/> for a near duplicate.</returns>
    private static bool IsNearDuplicate(string sentence, IReadOnlyDictionary<string, DataRow> grams) =>
        NearMatch(sentence, grams) is not null;

    /// <summary>
    ///     Finds the indexed row a sentence nearly duplicates.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <param name="grams">The 5-gram index (each gram mapped to the first row that has it).</param>
    /// <returns>The row of the first matching 5-gram, or <see langword="null"/> when the sentence is not a near duplicate.</returns>
    private static DataRow? NearMatch(string sentence, IReadOnlyDictionary<string, DataRow> grams)
    {
        var own = SentenceKeys.Ngrams(sentence);
        if (own.Count == 0 || SentenceKeys.Words(sentence).Count < NearMinimumWords)
        {
            return null;
        }

        var hits = own.Where(grams.ContainsKey).Order(StringComparer.Ordinal).ToList();
        return (double)hits.Count / own.Count >= NearContainment ? grams[hits[0]] : null;
    }

    /// <summary>
    ///     Builds the construction trigrams around a target word.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <param name="start">The first UTF-16 code unit of the target.</param>
    /// <param name="end">The UTF-16 code unit after the target.</param>
    /// <returns>
    ///     The trigrams that lie inside the sentence, each prefixed with its position (<c>L</c> for -2..0, <c>C</c> for
    ///     -1..+1, <c>R</c> for 0..+2), with the target replaced by a placeholder; empty when the target is not one word.
    /// </returns>
    private static List<string> ConstructionTrigrams(string sentence, int start, int end)
    {
        var words = SentenceKeys.Words(sentence);
        var index = SentenceKeys.Words(sentence[..start]).Count;
        if (SentenceKeys.Words(sentence[start..end]).Count != 1 || index >= words.Count)
        {
            return [];
        }

        string At(int position) => position == index ? TargetPlaceholder : words[position];
        var result = new List<string>(3);
        foreach (var (name, first) in new[] { ("L", index - 2), ("C", index - 1), ("R", index) })
        {
            if (first >= 0 && first + 2 < words.Count)
            {
                result.Add($"{name}|{At(first)} {At(first + 1)} {At(first + 2)}");
            }
        }

        return result;
    }

    /// <summary>
    ///     Renders a matching row for a finding that may name it.
    /// </summary>
    /// <param name="row">The matching row.</param>
    /// <returns>The text <c> (file id)</c>.</returns>
    private static string Describe(DataRow row) => $" ({row.File} {row.Id})";

    /// <summary>
    ///     Adds a finding of this check.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <param name="message">The message.</param>
    private void Add(string location, string message) => _report.Add(CheckName, location, message);

    /// <summary>
    ///     Checks the reading, lemma and strict whole-word span of an overlay row.
    /// </summary>
    /// <param name="location">The row location.</param>
    /// <param name="id">The row id.</param>
    /// <param name="lemma">The lemma column.</param>
    /// <param name="readingId">The reading id column.</param>
    /// <param name="sentence">The sentence.</param>
    /// <param name="start">The start offset, or <see langword="null"/> when it was not an integer.</param>
    /// <param name="end">The end offset, or <see langword="null"/> when it was not an integer.</param>
    /// <returns><see langword="true"/> when the reading exists and the span is a valid whole listed form.</returns>
    private bool CheckReadingAndSpan(string location, string id, string lemma, string readingId, string sentence, int? start, int? end)
    {
        if (!_data.ReadingsById.TryGetValue(readingId, out var reading))
        {
            Add(location, $"{id}: unknown reading '{readingId}'");
            return false;
        }

        if (!string.Equals(reading.Lemma, lemma, StringComparison.Ordinal))
        {
            Add(location, $"{id}: lemma '{lemma}' is not '{reading.Lemma}', the lemma of '{readingId}'");
        }

        // Offsets that are not integers were reported when the file was loaded
        if (start is not { } first || end is not { } last)
        {
            return false;
        }

        var error = SpanValidator.Validate(sentence, first, last, _data.FormsOf(readingId));
        if (error is not null)
        {
            Add(location, $"{id}: span: {error}");
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Checks the reference of a regression row and resolves a data-row reference.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The referenced training or development row, or <see langword="null"/> when there is none.</returns>
    private DataRow? CheckReference(RegressionRecord row)
    {
        var file = row.Reference.Split(':')[0];
        if (HoldoutFiles.Contains(file))
        {
            Add(row.Location, $"{row.Id}: refers to {file}; an overlay row may never refer to a holdout");
            return null;
        }

        if (!ReferencePatterns.TryGetValue(row.Origin, out var pattern))
        {
            Add(row.Location, $"{row.Id}: unknown origin '{row.Origin}' (allowed: {string.Join(", ", ReferencePatterns.Keys.Order(StringComparer.Ordinal))})");
            return null;
        }

        if (!pattern.IsMatch(row.Reference))
        {
            Add(row.Location, $"{row.Id}: reference '{row.Reference}' does not fit origin '{row.Origin}'");
            return null;
        }

        if (row.Origin != "data-row")
        {
            return null;
        }

        // A data-row reference must resolve to the same example, with the license the example carries
        var id = row.Reference[(file.Length + 1)..];
        var target = _data.Rows.FirstOrDefault(candidate => candidate.File == file && candidate.Id == id);
        if (target is null)
        {
            Add(row.Location, $"{row.Id}: reference '{row.Reference}' does not resolve");
            return null;
        }

        if (target.Lemma != row.Lemma || target.ReadingId != row.ReadingId || target.Sentence != row.Sentence
            || target.Start != row.Start || target.End != row.End)
        {
            Add(row.Location, $"{row.Id}: lemma, reading, sentence or offsets differ from {row.Reference}");
        }

        if (target.License != row.License)
        {
            Add(row.Location, $"{row.Id}: license '{row.License}' differs from '{target.License}' of {row.Reference}");
        }

        return target;
    }

    /// <summary>
    ///     Requires a regression row not to repeat or nearly repeat a holdout sentence.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="key">The sentence key of the row.</param>
    /// <param name="indexes">The indexes.</param>
    private void CheckHoldoutSeparation(RegressionRecord row, string key, Indexes indexes)
    {
        if (indexes.HoldoutKeys.TryGetValue(key, out var exact))
        {
            Add(row.Location, $"{row.Id}: repeats a holdout sentence{Describe(exact)}");
        }
        else if (NearMatch(row.Sentence, indexes.HoldoutGrams) is { } near)
        {
            Add(row.Location, $"{row.Id}: nearly repeats a holdout sentence{Describe(near)}");
        }
    }

    /// <summary>
    ///     Checks the columns of a generalization row that are not shared with the other files.
    /// </summary>
    /// <param name="row">The row.</param>
    private void CheckGeneralizationColumns(GeneralizationRecord row)
    {
        if (row.Kind is not (NovelVocabulary or NovelConstruction))
        {
            Add(row.Location, $"{row.Id}: kind '{row.Kind}' must be {NovelVocabulary} or {NovelConstruction}");
        }

        var family = FamilyPattern.Match(row.Family);
        if (!family.Success || GroupOf(family.Groups[1].Value) != GroupOf(row.Lemma))
        {
            Add(row.Location, $"{row.Id}: family '{row.Family}' must be {{lemma}}:{{cue-type}} with a lemma of the row's lemma group");
        }

        if (row.CueUnsolvable is not (Yes or No))
        {
            Add(row.Location, $"{row.Id}: cue_unsolvable '{row.CueUnsolvable}' must be yes or no");
        }

        if (row.Source != GeneralizationSource)
        {
            Add(row.Location, $"{row.Id}: source '{row.Source}' must be '{GeneralizationSource}'");
        }

        if (row.License != LicenseIds.Cc0)
        {
            Add(row.Location, $"{row.Id}: license '{row.License}' must be {LicenseIds.Cc0}");
        }

        // Length and a single target: count the words that are any form of the row's lemma group
        var words = SentenceKeys.Words(row.Sentence);
        if (words.Count is < MinWords or > MaxWords)
        {
            Add(row.Location, $"{row.Id}: the sentence has {words.Count} words (allowed {MinWords} to {MaxWords})");
        }

        var forms = _formsOfGroup.GetValueOrDefault(GroupOf(row.Lemma)) ?? [];
        var occurrences = words.Count(forms.Contains);
        if (occurrences > 1)
        {
            Add(row.Location, $"{row.Id}: the sentence has {occurrences} words of the target's lemma (exactly one allowed)");
        }

        // Cue forms: '-' only for a construction row, otherwise lower-case words of the sentence that are not the target
        if (row.CueForms == "-")
        {
            if (row.Kind == NovelVocabulary)
            {
                Add(row.Location, $"{row.Id}: a {NovelVocabulary} row must name its cue forms");
            }

            return;
        }

        foreach (var cue in row.CueForms.Split('|'))
        {
            if (!CueFormPattern.IsMatch(cue) || forms.Contains(cue))
            {
                Add(row.Location, $"{row.Id}: cue form '{cue}' must be one lower-case word that is not a form of the target");
            }
            else if (!words.Contains(cue))
            {
                Add(row.Location, $"{row.Id}: cue form '{cue}' does not occur as a word of the sentence");
            }
        }
    }

    /// <summary>
    ///     Applies the novelty rules N1 to N3 to a generalization row.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="key">The sentence key of the row.</param>
    /// <param name="spanValid">Whether the span is valid, so the target position is known.</param>
    /// <param name="indexes">The indexes.</param>
    /// <param name="revealMatches">Whether findings may name the matching row.</param>
    private void CheckNovelty(GeneralizationRecord row, string key, bool spanValid, Indexes indexes, bool revealMatches)
    {
        // N1: no exact repeat of any committed sentence and no near repeat of a training or development sentence
        var exact = indexes.AllKeys.GetValueOrDefault(key);
        var near = exact is null ? NearMatch(row.Sentence, indexes.TrainDevGrams) : null;
        if (exact is not null || near is not null || indexes.RegressionKeys.Contains(key))
        {
            var match = exact ?? near;
            var detail = match is null ? "a regression overlay sentence" : $"a sentence{Describe(match)}";
            Add(row.Location, $"{row.Id}: N1: " + (revealMatches ? $"repeats or nearly repeats {detail}" : GenericN1));
        }

        var group = GroupOf(row.Lemma);

        // N2: every designated cue form is new for the lemma group in the training and development sentences
        if (row.Kind == NovelVocabulary && row.CueForms != "-")
        {
            var known = indexes.WordsOfGroup.GetValueOrDefault(group);
            foreach (var cue in row.CueForms.Split('|').Where(cue => known is not null && known.ContainsKey(cue)))
            {
                Add(row.Location, $"{row.Id}: N2: cue form '{cue}' " + (revealMatches
                    ? $"already occurs with this lemma group{Describe(known![cue])}"
                    : GenericN2));
            }
        }

        // N3: no construction trigram around the target is known for the lemma group
        if (row.Kind == NovelConstruction && spanValid)
        {
            var known = indexes.TrigramsOfGroup.GetValueOrDefault(group);
            foreach (var trigram in ConstructionTrigrams(row.Sentence, row.Start!.Value, row.End!.Value).Where(trigram => known is not null && known.ContainsKey(trigram)))
            {
                Add(row.Location, $"{row.Id}: N3: " + (revealMatches
                    ? $"construction '{trigram}' already occurs around this lemma group{Describe(known![trigram])}"
                    : GenericN3));
            }
        }
    }

    /// <summary>
    ///     Applies the shape quotas to a populated generalization set.
    /// </summary>
    /// <param name="rows">The rows, at least one.</param>
    private void CheckShape(IReadOnlyList<GeneralizationRecord> rows)
    {
        var file = rows[0].File;

        // Every lemma group with rows has rows for at least two of its readings
        foreach (var group in rows.GroupBy(row => GroupOf(row.Lemma), StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var readings = group.Select(row => row.ReadingId).Distinct(StringComparer.Ordinal).Count();
            if (readings < _shape.MinReadingsPerLemmaGroup)
            {
                Add(file, $"shape: lemma group '{group.Key}' has rows for {readings} reading(s) (needs at least {_shape.MinReadingsPerLemmaGroup})");
            }
        }

        // Families: enough of them, each of a bounded size
        var families = rows.GroupBy(row => row.Family, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal).ToList();
        if (families.Count < _shape.MinFamilies)
        {
            Add(file, $"shape: {families.Count} families (needs at least {_shape.MinFamilies})");
        }

        foreach (var family in families.Where(family => family.Count() < _shape.MinFamilyRows || family.Count() > _shape.MaxFamilyRows))
        {
            Add(file, $"shape: family '{family.Key}' has {family.Count()} rows (allowed {_shape.MinFamilyRows} to {_shape.MaxFamilyRows})");
        }

        // Kinds and the rows the curated cues cannot solve
        RequireAtLeast(file, rows.Count(row => row.Kind == NovelVocabulary), _shape.MinNovelVocabulary, $"{NovelVocabulary} rows");
        RequireAtLeast(file, rows.Count(row => row.Kind == NovelConstruction), _shape.MinNovelConstruction, $"{NovelConstruction} rows");
        var unsolvable = rows.Where(row => row.CueUnsolvable == Yes).ToList();
        RequireAtLeast(file, unsolvable.Count, _shape.MinCueUnsolvable, "cue_unsolvable=yes rows");
        RequireAtLeast(file, unsolvable.Select(row => row.Lemma).Distinct(StringComparer.Ordinal).Count(), _shape.MinCueUnsolvableLemmas, "lemmas among the cue_unsolvable=yes rows");
    }

    /// <summary>
    ///     Adds a shape finding when a count is below its minimum.
    /// </summary>
    /// <param name="file">The file display name.</param>
    /// <param name="count">The count.</param>
    /// <param name="minimum">The minimum.</param>
    /// <param name="what">What is counted.</param>
    private void RequireAtLeast(string file, int count, int minimum, string what)
    {
        if (count < minimum)
        {
            Add(file, $"shape: {count} {what} (needs at least {minimum})");
        }
    }

    /// <summary>
    ///     Gets the indexes of the training, development, holdout and regression sentences, building them on first use.
    /// </summary>
    /// <returns>The indexes.</returns>
    private Indexes GetIndexes()
    {
        if (_indexes is not null)
        {
            return _indexes;
        }

        var indexes = new Indexes();
        foreach (var row in _data.Rows)
        {
            var key = SentenceKeys.Key(row.Sentence);
            indexes.AllKeys.TryAdd(key, row);
            var grams = SentenceKeys.Ngrams(row.Sentence);
            if (HoldoutFiles.Contains(row.File))
            {
                indexes.HoldoutKeys.TryAdd(key, row);
                AddAll(indexes.HoldoutGrams, grams, row);
                continue;
            }

            // Training and development rows: the 5-grams, and per lemma group the words and construction trigrams
            AddAll(indexes.TrainDevGrams, grams, row);
            if (row.Split == "train")
            {
                indexes.TrainKeys.TryAdd(key, row);
                AddAll(indexes.TrainGrams, grams, row);
            }

            var group = GroupOf(row.Lemma);
            AddAll(GetOrAdd(indexes.WordsOfGroup, group), SentenceKeys.Words(row.Sentence), row);
            if (row.Start is { } start && row.End is { } end && start >= 0 && start < end && end <= row.Sentence.Length)
            {
                AddAll(GetOrAdd(indexes.TrigramsOfGroup, group), ConstructionTrigrams(row.Sentence, start, end), row);
            }
        }

        foreach (var row in _data.Regression)
        {
            indexes.RegressionKeys.Add(SentenceKeys.Key(row.Sentence));
        }

        _indexes = indexes;
        return indexes;
    }

    /// <summary>
    ///     Maps every item to its first row.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="items">The items.</param>
    /// <param name="row">The row.</param>
    private static void AddAll(Dictionary<string, DataRow> index, IEnumerable<string> items, DataRow row)
    {
        foreach (var item in items)
        {
            index.TryAdd(item, row);
        }
    }

    /// <summary>
    ///     Gets the per-group index, creating it when absent.
    /// </summary>
    /// <param name="indexes">The per-group indexes.</param>
    /// <param name="group">The group.</param>
    /// <returns>The index of the group.</returns>
    private static Dictionary<string, DataRow> GetOrAdd(Dictionary<string, Dictionary<string, DataRow>> indexes, string group)
    {
        if (!indexes.TryGetValue(group, out var index))
        {
            index = new Dictionary<string, DataRow>(StringComparer.Ordinal);
            indexes[group] = index;
        }

        return index;
    }

    /// <summary>
    ///     The lookup tables the overlay checks compare with, each item mapped to the first row that has it.
    /// </summary>
    private sealed class Indexes
    {
        /// <summary>Gets the sentence keys of every row of the four data files.</summary>
        public Dictionary<string, DataRow> AllKeys { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets the sentence keys of the training rows.</summary>
        public Dictionary<string, DataRow> TrainKeys { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets the sentence keys of the holdout rows.</summary>
        public Dictionary<string, DataRow> HoldoutKeys { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets the word 5-grams of the training rows.</summary>
        public Dictionary<string, DataRow> TrainGrams { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets the word 5-grams of the training and development rows.</summary>
        public Dictionary<string, DataRow> TrainDevGrams { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets the word 5-grams of the holdout rows.</summary>
        public Dictionary<string, DataRow> HoldoutGrams { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets the words of the training and development sentences, per lemma group.</summary>
        public Dictionary<string, Dictionary<string, DataRow>> WordsOfGroup { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets the construction trigrams around the targets of the training and development rows, per lemma group.</summary>
        public Dictionary<string, Dictionary<string, DataRow>> TrigramsOfGroup { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets the sentence keys of the regression overlay.</summary>
        public HashSet<string> RegressionKeys { get; } = new(StringComparer.Ordinal);
    }
}
