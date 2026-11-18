using System.Diagnostics.CodeAnalysis;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.MorphologySubsystem;

/// <summary>
///     Derives the pronunciation of a word that the lexicon does not list from the pronunciation of its stem,
///     and appends contraction clitics to a host pronunciation.
/// </summary>
/// <remarks>
///     <para>
///     <b>Why.</b> A lexicon of 125,000 words still misses most inflected and derived forms (<c>unfollowed</c>,
///     <c>tweeted</c>, <c>zebra's</c>). English spells these regularly and pronounces them regularly, and the
///     sound of the ending depends only on the last phone of the stem (voiceless, voiced, or sibilant), so the
///     stem's lexicon pronunciation plus a fixed ending gives a result that is right for the vast majority of
///     words. The same sound rules also build the pronunciation of a heteronym's inflected forms from the
///     lemma's pronunciation (<see cref="Apply"/>) and attach the contraction clitics
///     (<see cref="AppendClitic"/>) to the previous word's pronunciation.
///     </para>
///     <para>
///     <b>Stem finding.</b> <see cref="TryDerive"/> proposes stem spellings for each ending (<c>makes</c>:
///     <c>make</c> before <c>mak</c>; <c>stopped</c>: <c>stop</c> by undoubling; <c>cried</c>: <c>cry</c>) and
///     takes the first one the lexicon knows, so no spelling rule needs to know the stem in advance. The
///     endings' sounds follow the majority convention of the CMU Pronouncing Dictionary itself (for example
///     <c>IH0 Z</c> rather than <c>AH0 Z</c> after a sibilant, <c>IH0 D</c> after <c>T</c> or <c>D</c>), as
///     measured on the dictionary by the tools project.
///     </para>
///     <para>
///     Stateless; thread-safe.
///     </para>
/// </remarks>
internal static class Morphology
{
    /// <summary>The shortest stem the stem finder will accept; single letters are in the lexicon as letter names and would mislead.</summary>
    private const int MinStemLength = 2;

    /// <summary>The most derived pronunciations kept for one word, so stems with many variants cannot multiply without bound.</summary>
    private const int MaxPronunciations = 4;

    /// <summary>The deepest chain of derivations: <c>walkers</c> is <c>walker</c> plus <c>s</c>, and <c>walker</c> is <c>walk</c> plus <c>er</c>.</summary>
    private const int MaxDerivationDepth = 2;

    /// <summary>The endings the stem finder tries, in priority order, for a word without an apostrophe.</summary>
    private static readonly SuffixKind[] PlainKinds =
    [
        SuffixKind.PluralS,
        SuffixKind.PastEd,
        SuffixKind.Ing,
        SuffixKind.SuperlativeEst,
        SuffixKind.ComparativeEr,
        SuffixKind.AdverbLy,
    ];

    /// <summary>The spelling of each contraction ending (without the leading host) in priority order.</summary>
    private static readonly (string Ending, SuffixKind Kind)[] ApostropheEndings =
    [
        ("n't", SuffixKind.ContractionNt),
        ("'s", SuffixKind.PossessiveS),
        ("'d", SuffixKind.ContractionD),
        ("'ll", SuffixKind.ContractionLl),
        ("'ve", SuffixKind.ContractionVe),
        ("'re", SuffixKind.ContractionRe),
        ("'m", SuffixKind.ContractionM),
    ];

    /// <summary>Gets <c>IH0 Z</c>: the syllabic plural ending.</summary>
    private static readonly Phone[] SyllabicZ = [Phone.Create("IH", 0), Phone.Create("Z")];

    /// <summary>Gets <c>IH0 D</c>: the syllabic past ending.</summary>
    private static readonly Phone[] SyllabicD = [Phone.Create("IH", 0), Phone.Create("D")];

    /// <summary>Gets <c>IH0 NG</c>.</summary>
    private static readonly Phone[] IngEnding = [Phone.Create("IH", 0), Phone.Create("NG")];

    /// <summary>Gets <c>ER0</c>.</summary>
    private static readonly Phone[] ErEnding = [Phone.Create("ER", 0)];

    /// <summary>Gets <c>AH0 S T</c>.</summary>
    private static readonly Phone[] EstEnding = [Phone.Create("AH", 0), Phone.Create("S"), Phone.Create("T")];

    /// <summary>Gets <c>L IY0</c>.</summary>
    private static readonly Phone[] LyEnding = [Phone.Create("L"), Phone.Create("IY", 0)];

    /// <summary>Gets <c>IY0</c>: the adverb ending after a stem that already ends in <c>L</c> (<c>accidental</c>, <c>accidentally</c>), where the two L sounds merge.</summary>
    private static readonly Phone[] IyEnding = [Phone.Create("IY", 0)];

    /// <summary>Gets <c>AH0 L IY0</c>, the ending of an adverb whose stem ends in <c>y</c> (<c>happy</c>, <c>happily</c>).</summary>
    private static readonly Phone[] IlyEnding = [Phone.Create("AH", 0), Phone.Create("L"), Phone.Create("IY", 0)];

    /// <summary>
    ///     Applies an ending to a stem pronunciation by the sound rule of the ending.
    /// </summary>
    /// <remarks>
    ///     Only the sound is changed. Contractions are applied with <see cref="AppendClitic"/>. The spelling
    ///     change <c>y</c> to <c>i</c> (<c>happy</c>, <c>happily</c>) is handled by <see cref="TryDerive"/>, not here.
    /// </remarks>
    /// <param name="stem">The stem pronunciation; must not be empty.</param>
    /// <param name="kind">The ending.</param>
    /// <returns>The stem pronunciation followed by the ending's sounds.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stem"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="kind"/> is a contraction kind.</exception>
    public static Pronunciation Apply(Pronunciation stem, SuffixKind kind)
    {
        ArgumentNullException.ThrowIfNull(stem);
        if (stem.Count == 0)
        {
            throw new ArgumentException("The stem pronunciation must not be empty.", nameof(stem));
        }

        var last = stem[stem.Count - 1];
        return kind switch
        {
            SuffixKind.PluralS or SuffixKind.PossessiveS => AddVoicedSibilant(stem, last),
            SuffixKind.PluralPossessive => stem,
            SuffixKind.PastEd => AddPast(stem, last),
            SuffixKind.Ing => stem.Append(IngEnding),
            SuffixKind.ComparativeEr => stem.Append(ErEnding),
            SuffixKind.SuperlativeEst => stem.Append(EstEnding),
            SuffixKind.AdverbLy => stem.Append(last == Phone.Create("L") ? IyEnding : LyEnding),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Use AppendClitic for contractions."),
        };
    }

    /// <summary>
    ///     Appends a contraction clitic to the pronunciation of the word it follows.
    /// </summary>
    /// <remarks>
    ///     The clitic is attached the way the dictionary's own contractions are: <c>'s</c> follows the plural
    ///     rule, and the others lose their reduced vowel after a vowel (<c>I'll</c> is <c>AY1 L</c>) and take
    ///     <c>AH0</c> after a consonant (<c>it'll</c> is <c>IH1 T AH0 L</c>). Irregular contractions
    ///     (<c>don't</c>, <c>won't</c>) are in the lexicon and never come through here.
    /// </remarks>
    /// <param name="host">The host word's pronunciation; must not be empty.</param>
    /// <param name="clitic">A contraction kind (<see cref="SuffixKind.PossessiveS"/> or one of the <c>Contraction*</c> kinds).</param>
    /// <returns>The host pronunciation followed by the clitic's sounds.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="host"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="clitic"/> is not a clitic kind.</exception>
    public static Pronunciation AppendClitic(Pronunciation host, SuffixKind clitic)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (host.Count == 0)
        {
            throw new ArgumentException("The host pronunciation must not be empty.", nameof(host));
        }

        var last = host[host.Count - 1];
        var afterVowel = last.IsVowel;
        return clitic switch
        {
            SuffixKind.PossessiveS => AddVoicedSibilant(host, last),
            SuffixKind.ContractionD => host.Append(Reduced(afterVowel, "D")),
            SuffixKind.ContractionLl => host.Append(Reduced(afterVowel, "L")),
            SuffixKind.ContractionVe => host.Append(Reduced(afterVowel, "V")),
            SuffixKind.ContractionM => host.Append(Reduced(afterVowel, "M")),
            SuffixKind.ContractionRe => host.Append(afterVowel ? [Phone.Create("R")] : [Phone.Create("ER", 0)]),
            SuffixKind.ContractionNt => host.Append(afterVowel
                ? [Phone.Create("N"), Phone.Create("T")]
                : [Phone.Create("AH", 0), Phone.Create("N"), Phone.Create("T")]),
            _ => throw new ArgumentOutOfRangeException(nameof(clitic), clitic, "Not a clitic kind."),
        };
    }

    /// <summary>
    ///     Derives a pronunciation for a word that is not in the lexicon from the pronunciation of its stem.
    /// </summary>
    /// <remarks>
    ///     The caller looks the word up first; this is the fallback for a miss. When the word is in the
    ///     lexicon anyway (as the measurement of the tools project does on purpose), the result is what the
    ///     rules would have produced. A word with an apostrophe is read as a possessive or contraction (<c>cat's</c>,
    ///     <c>isn't</c>, <c>they'll</c>); any other word is tried against the plural, past, <c>-ing</c>,
    ///     superlative, comparative and adverb endings in that order. A plural ending is not tried on words that end in
    ///     <c>ss</c>, <c>us</c> or <c>is</c> (<c>glass</c>, <c>virus</c>, <c>analysis</c> are not plurals).
    /// </remarks>
    /// <param name="word">The word; case is ignored.</param>
    /// <param name="lexicon">The lexicon that supplies stem pronunciations.</param>
    /// <param name="derivation">Receives the derivation of the first ending whose stem resolves, or <see langword="null"/>.</param>
    /// <param name="allowDerivedStems"><see langword="true"/> to let the stem itself be derived (<c>walkers</c> through <c>walker</c>); <see langword="false"/> to accept only stems the lexicon lists.</param>
    /// <returns><see langword="true"/> when a derivation was found.</returns>
    public static bool TryDerive(string word, CmuLexicon lexicon, [NotNullWhen(true)] out Derivation? derivation, bool allowDerivedStems = true)
    {
        ArgumentNullException.ThrowIfNull(word);
        ArgumentNullException.ThrowIfNull(lexicon);
        derivation = DeriveCore(word.ToLowerInvariant().Replace('\u2019', '\''), lexicon, allowDerivedStems ? MaxDerivationDepth : 1);
        return derivation is not null;
    }

    /// <summary>
    ///     Applies a possessive or past/plural ending to the stem pronunciations of the stem found by a candidate stem.
    /// </summary>
    /// <param name="word">The lower-case word.</param>
    /// <param name="lexicon">The lexicon.</param>
    /// <param name="depth">The number of derivation steps still allowed, counting this one.</param>
    /// <returns>The derivation, or <see langword="null"/>.</returns>
    private static Derivation? DeriveCore(string word, CmuLexicon lexicon, int depth)
    {
        // A word with an apostrophe is a possessive or a contraction; a plain word is an inflection
        if (word.Contains('\''))
        {
            return TryDeriveApostrophe(word, lexicon, depth);
        }

        foreach (var kind in PlainKinds)
        {
            foreach (var (stem, adverbIly) in Candidates(word, kind))
            {
                var stems = Resolve(stem, lexicon, depth - 1);
                if (stems.Count == 0)
                {
                    continue;
                }

                return Build(stem, kind, stems, adverbIly);
            }
        }

        return null;
    }

    /// <summary>
    ///     Reads a word with an apostrophe as a possessive or a contraction of a host word.
    /// </summary>
    /// <param name="word">The lower-case word.</param>
    /// <param name="lexicon">The lexicon.</param>
    /// <param name="depth">The number of derivation steps still allowed.</param>
    /// <returns>The derivation, or <see langword="null"/>.</returns>
    private static Derivation? TryDeriveApostrophe(string word, CmuLexicon lexicon, int depth)
    {
        // Plural possessive: the host already ends in s ("cats'") and gets no extra sound
        if (word.EndsWith("s'", StringComparison.Ordinal) && word.Length - 1 >= MinStemLength)
        {
            var host = word[..^1];
            var stems = Resolve(host, lexicon, depth - 1);
            if (stems.Count > 0)
            {
                return new Derivation(host, SuffixKind.PluralPossessive, stems, stems);
            }
        }

        foreach (var (ending, kind) in ApostropheEndings)
        {
            if (!word.EndsWith(ending, StringComparison.Ordinal) || word.Length - ending.Length < MinStemLength)
            {
                continue;
            }

            // "isn't" -> "is", "they'll" -> "they", "zebra's" -> "zebra"
            var host = word[..^ending.Length];
            var hosts = Resolve(host, lexicon, depth - 1);
            if (hosts.Count == 0)
            {
                continue;
            }

            var derived = hosts.Select(h => AppendClitic(h, kind)).ToList();
            return Build(host, kind, hosts, derived);
        }

        return null;
    }

    /// <summary>
    ///     Builds the derivation of every stem variant, dropping repeated results.
    /// </summary>
    /// <param name="stem">The stem word.</param>
    /// <param name="kind">The ending.</param>
    /// <param name="stems">The stem's pronunciations.</param>
    /// <param name="adverbIly"><see langword="true"/> when an adverb's stem ended in <c>y</c> and changed to <c>i</c>.</param>
    /// <returns>The derivation.</returns>
    private static Derivation Build(string stem, SuffixKind kind, IReadOnlyList<Pronunciation> stems, bool adverbIly)
    {
        var derived = new List<Pronunciation>(stems.Count);
        foreach (var s in stems)
        {
            // "happy" -> "happily": the final IY0 of the stem is replaced by AH0 L IY0 when it is present
            if (adverbIly && s[s.Count - 1] == Phone.Create("IY", 0))
            {
                derived.Add(s.DropLast(1).Append(IlyEnding));
            }
            else
            {
                derived.Add(Apply(s, kind));
            }
        }

        return Build(stem, kind, stems, derived);
    }

    /// <summary>
    ///     Builds the derivation from aligned stem and derived pronunciations, dropping repeats and capping the count.
    /// </summary>
    /// <param name="stem">The stem word.</param>
    /// <param name="kind">The ending.</param>
    /// <param name="stems">The stem pronunciations.</param>
    /// <param name="derived">The derived pronunciations aligned with <paramref name="stems"/>.</param>
    /// <returns>The derivation.</returns>
    private static Derivation Build(string stem, SuffixKind kind, IReadOnlyList<Pronunciation> stems, IReadOnlyList<Pronunciation> derived)
    {
        var keptStems = new List<Pronunciation>();
        var keptDerived = new List<Pronunciation>();
        for (var i = 0; i < derived.Count && keptDerived.Count < MaxPronunciations; i++)
        {
            if (!keptDerived.Contains(derived[i]))
            {
                keptStems.Add(stems[i]);
                keptDerived.Add(derived[i]);
            }
        }

        return new Derivation(stem, kind, keptStems, keptDerived);
    }

    /// <summary>
    ///     Finds the pronunciations of a stem: the lexicon's, or when allowed a derived one.
    /// </summary>
    /// <param name="stem">The lower-case stem.</param>
    /// <param name="lexicon">The lexicon.</param>
    /// <param name="depth">The number of derivation steps still allowed for the stem.</param>
    /// <returns>The pronunciations, empty when the stem is unknown.</returns>
    private static IReadOnlyList<Pronunciation> Resolve(string stem, CmuLexicon lexicon, int depth)
    {
        if (stem.Length < MinStemLength)
        {
            return [];
        }

        var listed = lexicon.Lookup(stem);
        if (listed.Count > 0 || depth <= 0)
        {
            return listed;
        }

        return DeriveCore(stem, lexicon, depth)?.Pronunciations ?? [];
    }

    /// <summary>
    ///     Proposes the stem spellings of a word for an ending, most likely first.
    /// </summary>
    /// <param name="word">The lower-case word.</param>
    /// <param name="kind">The ending.</param>
    /// <returns>The candidate stems; the flag marks an adverb whose <c>-ily</c> ending replaced a final <c>y</c>.</returns>
    private static IEnumerable<(string Stem, bool Ily)> Candidates(string word, SuffixKind kind)
    {
        switch (kind)
        {
            case SuffixKind.PluralS:
                if (word.Length >= 3 && word.EndsWith('s') && !HasAny(word, "ss", "us", "is"))
                {
                    yield return (word[..^1], false);
                    if (word.EndsWith("ies", StringComparison.Ordinal))
                    {
                        yield return (word[..^3] + "y", false);
                    }

                    if (word.EndsWith("es", StringComparison.Ordinal))
                    {
                        yield return (word[..^2], false);
                    }
                }

                break;

            case SuffixKind.PastEd:
                if (word.Length >= 4 && word.EndsWith("ed", StringComparison.Ordinal))
                {
                    foreach (var stem in WithSilentE(word[..^2]))
                    {
                        yield return (stem, false);
                    }

                    if (word.EndsWith("ied", StringComparison.Ordinal))
                    {
                        yield return (word[..^3] + "y", false);
                    }

                    if (Doubled(word[..^2]))
                    {
                        yield return (word[..^3], false);
                    }
                }

                break;

            case SuffixKind.Ing:
                if (word.Length >= 5 && word.EndsWith("ing", StringComparison.Ordinal))
                {
                    if (word.EndsWith("ying", StringComparison.Ordinal))
                    {
                        yield return (word[..^4] + "ie", false);
                    }

                    var root = word[..^3];
                    foreach (var stem in WithSilentE(root))
                    {
                        yield return (stem, false);
                    }

                    if (Doubled(root))
                    {
                        yield return (root[..^1], false);
                    }
                }

                break;

            case SuffixKind.SuperlativeEst:
                if (word.Length >= 5 && word.EndsWith("est", StringComparison.Ordinal))
                {
                    foreach (var stem in WithSilentE(word[..^3]))
                    {
                        yield return (stem, false);
                    }

                    if (word.EndsWith("iest", StringComparison.Ordinal))
                    {
                        yield return (word[..^4] + "y", false);
                    }

                    if (Doubled(word[..^3]))
                    {
                        yield return (word[..^4], false);
                    }
                }

                break;

            case SuffixKind.ComparativeEr:
                if (word.Length >= 4 && word.EndsWith("er", StringComparison.Ordinal))
                {
                    foreach (var stem in WithSilentE(word[..^2]))
                    {
                        yield return (stem, false);
                    }

                    if (word.EndsWith("ier", StringComparison.Ordinal))
                    {
                        yield return (word[..^3] + "y", false);
                    }

                    if (Doubled(word[..^2]))
                    {
                        yield return (word[..^3], false);
                    }
                }

                break;

            case SuffixKind.AdverbLy:
                if (word.Length >= 4 && word.EndsWith("ly", StringComparison.Ordinal))
                {
                    yield return (word[..^2], false);
                    if (word.EndsWith("ily", StringComparison.Ordinal))
                    {
                        yield return (word[..^3] + "y", true);
                    }
                }

                break;

            default:
                break;
        }
    }

    /// <summary>
    ///     Proposes the two spellings of a stem before a vowel ending: the bare letters and the letters plus a silent <c>e</c>.
    /// </summary>
    /// <remarks>
    ///     English doubles the final consonant of a short stem before such an ending (<c>hop</c>, <c>hopped</c>) and
    ///     drops a silent <c>e</c> instead (<c>hope</c>, <c>hoped</c>), so a bare consonant-vowel-consonant ending
    ///     that was not doubled (<c>hop</c> in <c>hoped</c>, <c>mak</c> in <c>making</c>) is the silent-<c>e</c> stem
    ///     with its <c>e</c> dropped. The more likely spelling is returned first, which also keeps a rare name such
    ///     as <c>mak</c> from capturing <c>making</c>.
    /// </remarks>
    /// <param name="bare">The word without its ending.</param>
    /// <returns>The candidate stems, most likely first.</returns>
    private static IEnumerable<string> WithSilentE(string bare)
    {
        var shortStem = bare.Length >= 3
            && !IsVowelLetter(bare[^1]) && bare[^1] is not ('w' or 'x' or 'y')
            && IsVowelLetter(bare[^2])
            && !IsVowelLetter(bare[^3]);
        if (shortStem)
        {
            yield return bare + "e";
            yield return bare;
        }
        else
        {
            yield return bare;
            yield return bare + "e";
        }
    }

    /// <summary>
    ///     Tests whether a letter is a vowel letter.
    /// </summary>
    /// <param name="letter">The lower-case letter.</param>
    /// <returns><see langword="true"/> for a, e, i, o and u.</returns>
    private static bool IsVowelLetter(char letter) => letter is 'a' or 'e' or 'i' or 'o' or 'u';

    /// <summary>
    ///     Tests whether a word ends with a doubled consonant, as <c>stopp</c> does (<c>stopped</c>, <c>stopping</c>).
    /// </summary>
    /// <param name="stem">The word before the ending.</param>
    /// <returns><see langword="true"/> when the last two letters are the same consonant.</returns>
    private static bool Doubled(string stem) =>
        stem.Length >= 3 && stem[^1] == stem[^2] && !IsVowelLetter(stem[^1]);

    /// <summary>
    ///     Tests whether a word ends with any of the given endings.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <param name="endings">The endings.</param>
    /// <returns><see langword="true"/> when the word ends with one of them.</returns>
    private static bool HasAny(string word, params string[] endings) => endings.Any(e => word.EndsWith(e, StringComparison.Ordinal));

    /// <summary>
    ///     Adds the plural/possessive ending: <c>S</c> after a voiceless consonant, <c>IH0 Z</c> after a sibilant, <c>Z</c> otherwise.
    /// </summary>
    /// <param name="stem">The stem pronunciation.</param>
    /// <param name="last">The stem's last phone.</param>
    /// <returns>The pronunciation with the ending.</returns>
    private static Pronunciation AddVoicedSibilant(Pronunciation stem, Phone last)
    {
        if (last.IsSibilant)
        {
            return stem.Append(SyllabicZ);
        }

        return stem.Append([Phone.Create(last.IsVoiceless ? "S" : "Z")]);
    }

    /// <summary>
    ///     Adds the past ending: <c>IH0 D</c> after <c>T</c> or <c>D</c>, <c>T</c> after a voiceless consonant, <c>D</c> otherwise.
    /// </summary>
    /// <param name="stem">The stem pronunciation.</param>
    /// <param name="last">The stem's last phone.</param>
    /// <returns>The pronunciation with the ending.</returns>
    private static Pronunciation AddPast(Pronunciation stem, Phone last)
    {
        if (last == Phone.Create("T") || last == Phone.Create("D"))
        {
            return stem.Append(SyllabicD);
        }

        return stem.Append([Phone.Create(last.IsVoiceless ? "T" : "D")]);
    }

    /// <summary>
    ///     Gives a contraction consonant with the reduced vowel it takes after a consonant.
    /// </summary>
    /// <param name="afterVowel">Whether the host ends in a vowel.</param>
    /// <param name="consonant">The clitic consonant symbol.</param>
    /// <returns>The clitic's phones.</returns>
    private static Phone[] Reduced(bool afterVowel, string consonant) =>
        afterVowel ? [Phone.Create(consonant)] : [Phone.Create("AH", 0), Phone.Create(consonant)];
}
