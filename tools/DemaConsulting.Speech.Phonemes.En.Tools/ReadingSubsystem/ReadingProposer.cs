using System.Text;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tools.ReadingSubsystem;

/// <summary>
///     One reading of the Text.En inventory as the proposer needs it.
/// </summary>
/// <param name="Id">The reading id, such as <c>record.noun</c>.</param>
/// <param name="Lemma">The lemma.</param>
/// <param name="PosHint">The part-of-speech hint, such as <c>NOUN|VERB</c>.</param>
/// <param name="Description">The sense description.</param>
internal sealed record ReadingRow(string Id, string Lemma, string PosHint, string Description)
{
    /// <summary>Gets the discriminator after the first period of the id (<c>noun</c>, <c>adj-noun</c>, <c>fish</c>).</summary>
    public string Discriminator => Id[(Id.IndexOf('.') + 1)..];
}

/// <summary>
///     The proposal for one reading: a pronunciation (or none), its provenance and a note for the reviewer.
/// </summary>
/// <param name="Reading">The reading.</param>
/// <param name="Pronunciation">The proposed lemma-form pronunciation, or <see langword="null"/> when nothing can be proposed.</param>
/// <param name="Source">The provenance: <c>cmu#N</c>, <c>rule:&lt;name&gt;</c> or <c>authored</c>.</param>
/// <param name="Note">What the proposer did and why, for the reviewer.</param>
internal sealed record ReadingProposal(ReadingRow Reading, Pronunciation? Pronunciation, string Source, string Note);

/// <summary>
///     Proposes a pronunciation for every reading id from the CMU lexicon variants of its lemma.
/// </summary>
/// <remarks>
///     <para>
///     The proposals are a starting point for a human review, never the frozen table. For a lemma that has a noun
///     (or adjective) reading and a verb reading, the variants are matched by stress signature (initial primary stress
///     for the noun, final or non-initial for the verb; the <c>-ate</c> ending is the unstressed <c>AH0 T</c> for the
///     noun or adjective and <c>EY2 T</c> for the verb). A missing partner is derived by a named rule
///     (<c>ate-schwa</c>, <c>ment-schwa</c>, <c>verb-stress-shift</c>). Every other reading takes the lemma's variants
///     in order; a reading with no variant left is marked <c>authored</c> without a pronunciation.
///     </para>
///     <para>
///     Pronunciations come from CMUdict and the rules above only; no other pronunciation source is consulted.
///     </para>
/// </remarks>
internal static class ReadingProposer
{
    /// <summary>
    ///     Reads the reading inventory table of the text analysis package.
    /// </summary>
    /// <param name="path">The path of <c>data/text-en/readings.tsv</c>.</param>
    /// <returns>The readings in file order.</returns>
    public static IReadOnlyList<ReadingRow> LoadReadings(string path)
    {
        var rows = new List<ReadingRow>();
        foreach (var line in File.ReadLines(path).Skip(1))
        {
            var fields = line.Split('\t');
            if (fields.Length >= 4)
            {
                rows.Add(new ReadingRow(fields[0], fields[1], fields[2], fields[3]));
            }
        }

        return rows;
    }

    /// <summary>
    ///     Proposes a pronunciation for every reading.
    /// </summary>
    /// <param name="readings">The readings.</param>
    /// <param name="lexicon">The lexicon.</param>
    /// <returns>One proposal per reading, in the order of <paramref name="readings"/>.</returns>
    public static IReadOnlyList<ReadingProposal> Propose(IReadOnlyList<ReadingRow> readings, CmuLexicon lexicon)
    {
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(lexicon);

        var proposals = new Dictionary<string, ReadingProposal>(StringComparer.Ordinal);
        foreach (var group in readings.GroupBy(r => r.Lemma, StringComparer.Ordinal))
        {
            var members = group.ToList();
            var variants = lexicon.Lookup(group.Key);
            if (variants.Count == 0)
            {
                foreach (var member in members)
                {
                    proposals[member.Id] = new ReadingProposal(member, null, "authored", "the lemma is not in CMUdict");
                }

                continue;
            }

            var verbs = members.Where(IsVerb).ToList();
            var nominals = members.Where(m => IsNominal(m) && !IsVerb(m)).ToList();
            if (verbs.Count == 1 && nominals.Count == 1 && members.Count == 2)
            {
                ProposeShiftPair(group.Key, nominals[0], verbs[0], variants, proposals);
                continue;
            }

            // Every other group: the lemma's variants in order
            for (var i = 0; i < members.Count; i++)
            {
                proposals[members[i].Id] = i < variants.Count
                    ? new ReadingProposal(members[i], variants[i], "cmu#" + (i + 1), "variant by order")
                    : new ReadingProposal(members[i], variants[^1], "authored", "fewer variants than readings; the last variant is a placeholder");
            }
        }

        return readings.Select(r => proposals[r.Id]).ToList();
    }

    /// <summary>
    ///     Formats the proposals as the three-column reading table.
    /// </summary>
    /// <param name="proposals">The proposals.</param>
    /// <returns>The table text, header first, one row per proposal; a reading without a pronunciation gets <c>?</c>.</returns>
    public static string FormatTable(IReadOnlyList<ReadingProposal> proposals)
    {
        var builder = new StringBuilder();
        builder.Append("reading_id\tarpabet\tsource\n");
        foreach (var proposal in proposals)
        {
            builder.Append(proposal.Reading.Id).Append('\t').Append(proposal.Pronunciation?.ToString() ?? "?").Append('\t').Append(proposal.Source).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Formats the review sheet: each proposal beside every CMUdict variant of its lemma.
    /// </summary>
    /// <param name="proposals">The proposals.</param>
    /// <param name="lexicon">The lexicon.</param>
    /// <returns>A tab-separated sheet with a header line.</returns>
    public static string FormatReview(IReadOnlyList<ReadingProposal> proposals, CmuLexicon lexicon)
    {
        var builder = new StringBuilder();
        builder.Append("reading_id\tpos\tproposal\tsource\tnote\tcmudict variants (number:stress:phones)\tdescription\n");
        foreach (var proposal in proposals)
        {
            var variants = lexicon.Lookup(proposal.Reading.Lemma);
            var listed = string.Join(" ;; ", variants.Select((v, i) => $"{i + 1}:{Signature(v)}:{v}"));
            builder.Append(proposal.Reading.Id).Append('\t')
                .Append(proposal.Reading.PosHint).Append('\t')
                .Append(proposal.Pronunciation?.ToString() ?? "?").Append('\t')
                .Append(proposal.Source).Append('\t')
                .Append(proposal.Note).Append('\t')
                .Append(listed).Append('\t')
                .Append(proposal.Reading.Description).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Gets the stress signature of a pronunciation: the stress digit of each vowel, in order.
    /// </summary>
    /// <param name="pronunciation">The pronunciation.</param>
    /// <returns>For example <c>102</c>.</returns>
    public static string Signature(Pronunciation pronunciation)
    {
        var builder = new StringBuilder();
        foreach (var phone in pronunciation.Phones)
        {
            if (phone.IsVowel)
            {
                builder.Append(phone.Stress);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Proposes the noun-or-adjective reading and the verb reading of one lemma together.
    /// </summary>
    /// <param name="lemma">The lemma.</param>
    /// <param name="nominal">The noun or adjective reading.</param>
    /// <param name="verb">The verb reading.</param>
    /// <param name="variants">The lemma's lexicon variants.</param>
    /// <param name="proposals">Receives the two proposals.</param>
    private static void ProposeShiftPair(string lemma, ReadingRow nominal, ReadingRow verb, IReadOnlyList<Pronunciation> variants, Dictionary<string, ReadingProposal> proposals)
    {
        var isAte = lemma.EndsWith("ate", StringComparison.Ordinal);
        var isMent = lemma.EndsWith("ment", StringComparison.Ordinal);

        var nominalIndex = IndexWhere(variants, v => MatchesRole(v, isAte, isMent, nominal: true));
        var verbIndex = IndexWhere(variants, v => MatchesRole(v, isAte, isMent, nominal: false));

        // Direct matches
        if (nominalIndex >= 0 && verbIndex >= 0 && nominalIndex != verbIndex)
        {
            proposals[nominal.Id] = new ReadingProposal(nominal, variants[nominalIndex], "cmu#" + (nominalIndex + 1), "matched by stress signature");
            proposals[verb.Id] = new ReadingProposal(verb, variants[verbIndex], "cmu#" + (verbIndex + 1), "matched by stress signature");
            return;
        }

        // One partner is missing from the lexicon: derive it by a named rule
        if (nominalIndex >= 0 || verbIndex >= 0)
        {
            var knownIsNominal = nominalIndex >= 0;
            var known = variants[knownIsNominal ? nominalIndex : verbIndex];
            var (derived, rule) = Derive(known, knownIsNominal, isAte, isMent);
            var knownSource = "cmu#" + ((knownIsNominal ? nominalIndex : verbIndex) + 1);
            proposals[(knownIsNominal ? nominal : verb).Id] = new ReadingProposal(knownIsNominal ? nominal : verb, known, knownSource, "matched by stress signature");
            proposals[(knownIsNominal ? verb : nominal).Id] = new ReadingProposal(knownIsNominal ? verb : nominal, derived, "rule:" + rule, "derived from the other reading");
            return;
        }

        // Nothing matches by signature: take the lemma's variants in order and let the reviewer decide
        proposals[nominal.Id] = new ReadingProposal(nominal, variants[0], "cmu#1", "no stress match; first variant");
        proposals[verb.Id] = variants.Count > 1
            ? new ReadingProposal(verb, variants[1], "cmu#2", "no stress match; second variant")
            : new ReadingProposal(verb, variants[0], "authored", "no stress match; placeholder");
    }

    /// <summary>
    ///     Derives the partner reading by the sound rule of the lemma's ending.
    /// </summary>
    /// <param name="known">The known pronunciation.</param>
    /// <param name="knownIsNominal"><see langword="true"/> when <paramref name="known"/> is the noun or adjective reading.</param>
    /// <param name="isAte">Whether the lemma ends in <c>ate</c>.</param>
    /// <param name="isMent">Whether the lemma ends in <c>ment</c>.</param>
    /// <returns>The derived pronunciation and the rule name.</returns>
    private static (Pronunciation Pronunciation, string Rule) Derive(Pronunciation known, bool knownIsNominal, bool isAte, bool isMent)
    {
        var phones = known.Phones.ToArray();
        if (isAte && phones.Length >= 2)
        {
            // noun/adjective AH0 T <-> verb EY2 T
            phones[^2] = knownIsNominal ? Phone.Create("EY", 2) : Phone.Create("AH", 0);
            return (new Pronunciation(phones), "ate-schwa");
        }

        if (isMent && phones.Length >= 3)
        {
            // noun/adjective M AH0 N T <-> verb M EH2 N T
            phones[^3] = knownIsNominal ? Phone.Create("EH", 2) : Phone.Create("AH", 0);
            return (new Pronunciation(phones), "ment-schwa");
        }

        // Stress shift: noun initial primary, verb primary on the last vowel
        var vowels = phones.Select((p, i) => (p, i)).Where(t => t.p.IsVowel).Select(t => t.i).ToArray();
        for (var k = 0; k < vowels.Length; k++)
        {
            var wantsPrimary = knownIsNominal ? k == vowels.Length - 1 : k == 0;
            var index = vowels[k];
            phones[index] = phones[index].WithStress(wantsPrimary ? 1 : 0);
        }

        return (new Pronunciation(phones), "verb-stress-shift");
    }

    /// <summary>Tests whether a variant has the sound pattern of the noun/adjective or of the verb reading.</summary>
    /// <param name="variant">The variant.</param>
    /// <param name="isAte">Whether the lemma ends in <c>ate</c>.</param>
    /// <param name="isMent">Whether the lemma ends in <c>ment</c>.</param>
    /// <param name="nominal"><see langword="true"/> for the noun/adjective pattern, <see langword="false"/> for the verb pattern.</param>
    /// <returns>The result.</returns>
    private static bool MatchesRole(Pronunciation variant, bool isAte, bool isMent, bool nominal)
    {
        if (isAte)
        {
            return EndsWithAte(variant, stressed: !nominal);
        }

        if (isMent)
        {
            return EndsWithMent(variant, stressed: !nominal);
        }

        return nominal == InitialStress(variant);
    }

    /// <summary>Finds the first index whose pronunciation satisfies a test.</summary>
    /// <param name="variants">The variants.</param>
    /// <param name="test">The test.</param>
    /// <returns>The index, or <c>-1</c>.</returns>
    private static int IndexWhere(IReadOnlyList<Pronunciation> variants, Func<Pronunciation, bool> test)
    {
        for (var i = 0; i < variants.Count; i++)
        {
            if (test(variants[i]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Tests whether the first vowel carries primary stress.</summary>
    /// <param name="pronunciation">The pronunciation.</param>
    /// <returns><see langword="true"/> for initial primary stress.</returns>
    private static bool InitialStress(Pronunciation pronunciation)
    {
        foreach (var phone in pronunciation.Phones)
        {
            if (phone.IsVowel)
            {
                return phone.Stress == 1;
            }
        }

        return false;
    }

    /// <summary>Tests whether the pronunciation ends in <c>AH0 T</c> (unstressed) or <c>EY? T</c> (stressed).</summary>
    /// <param name="pronunciation">The pronunciation.</param>
    /// <param name="stressed"><see langword="true"/> to test for <c>EY</c>.</param>
    /// <returns>The result.</returns>
    private static bool EndsWithAte(Pronunciation pronunciation, bool stressed)
    {
        var count = pronunciation.Count;
        if (count < 2 || pronunciation[count - 1] != Phone.Create("T"))
        {
            return false;
        }

        var vowel = pronunciation[count - 2];
        return stressed ? vowel.BaseSymbol == "EY" : vowel.BaseSymbol is "AH" or "IH";
    }

    /// <summary>Tests whether the pronunciation ends in <c>M AH0 N T</c> (unstressed) or <c>M EH? N T</c> (stressed).</summary>
    /// <param name="pronunciation">The pronunciation.</param>
    /// <param name="stressed"><see langword="true"/> to test for <c>EH</c>.</param>
    /// <returns>The result.</returns>
    private static bool EndsWithMent(Pronunciation pronunciation, bool stressed)
    {
        var count = pronunciation.Count;
        if (count < 4 || pronunciation[count - 1] != Phone.Create("T") || pronunciation[count - 2] != Phone.Create("N") || pronunciation[count - 4] != Phone.Create("M"))
        {
            return false;
        }

        var vowel = pronunciation[count - 3];
        return stressed ? vowel.BaseSymbol == "EH" : vowel.BaseSymbol is "AH" or "IH";
    }

    /// <summary>Tests whether the reading is a verb reading.</summary>
    /// <param name="reading">The reading.</param>
    /// <returns><see langword="true"/> for a verb.</returns>
    private static bool IsVerb(ReadingRow reading) =>
        reading.Discriminator.StartsWith("verb", StringComparison.Ordinal) || reading.PosHint == "VERB";

    /// <summary>Tests whether the reading is a noun or adjective reading.</summary>
    /// <param name="reading">The reading.</param>
    /// <returns><see langword="true"/> for a noun or adjective.</returns>
    private static bool IsNominal(ReadingRow reading) =>
        reading.Discriminator is "noun" or "adj" or "adj-noun";
}
