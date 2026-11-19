using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tools.LetterToSoundSubsystem;

/// <summary>
///     The rules baseline of the letter-to-sound benchmark: hand-written context-sensitive letter-to-phone rules in
///     the classic public format of the Naval Research Laboratory report 7948 (1976), followed by stress
///     heuristics.
/// </summary>
/// <remarks>
///     <para>
///     <b>Clean-room.</b> The rule format (left context, matched letters, right context, phones) and the context
///     symbols below are the published ones; every rule in this file was written for this project from English
///     orthography and phonetics, not copied from any rule table, program or data set. The rules are not a
///     model of CMUdict and were never tuned against it: they are the reference that a trained model has to beat by a
///     wide margin to be worth shipping, so the benchmark compares against an honest, not a straw-man, rules system.
///     </para>
///     <para>
///     <b>Rule syntax.</b> <c>left(letters)right=PHONES</c> with ARPAbet phones separated by spaces (none after the
///     <c>=</c> means silent). A space in a context means a word boundary. Context symbols: <c>#</c> one or more
///     vowels (<c>a e i o u y</c>), <c>:</c> zero or more consonants, <c>^</c> one consonant, <c>.</c> one voiced
///     consonant (<c>b d v g j l m n r w z</c>), <c>+</c> a front vowel (<c>e i y</c>), <c>%</c> (right context only) a
///     word-final suffix: <c>e</c>, <c>es</c>, <c>ed</c>, <c>er</c>, <c>ing</c> or <c>ely</c>. Within the rules of a
///     letter the first rule that matches wins; the left context is read right to left.
///     </para>
///     <para>
///     <b>Stress.</b> The rules give phones without stress. A one-vowel word gets primary stress on its vowel; longer
///     words get it by orthographic suffix class (<c>-tion</c>, <c>-ic</c>, <c>-ity</c> and the like stress the
///     syllable before them), else by the stem's length (first syllable, or the second after an unstressed prefix of
///     two-syllable words, or the antepenultimate), with secondary stress two syllables before the primary and
///     vowel reduction to <c>AH0</c> in unstressed syllables. Inflectional endings (<c>-ing</c>, <c>-ed</c>,
///     <c>-er</c>, <c>-ly</c>) do not count as syllables of the stem.
///     </para>
///     <para>Stateless and thread-safe.</para>
/// </remarks>
internal static class NrlRules
{
    /// <summary>The suffixes the <c>%</c> symbol stands for.</summary>
    private static readonly string[] SuffixAlternatives = ["e", "es", "ed", "er", "ing", "ely"];

    /// <summary>The rules by letter, in matching order.</summary>
    private static readonly Dictionary<char, Rule[]> RulesByLetter = Compile(RuleText);

    /// <summary>The rules, one per line, grouped by letter in matching order.</summary>
    private static string RuleText => """
        " (A) =AH
        " (ARE) =AA R
        " (AR)O=AH R
        (AR)#=EH R
        " ^(AS)#=EY S
        (A)WA=AH
        (AW)=AO
        " :(ANY)=EH N IY
        (A)^+#=EY
        #:(ALLY)=AH L IY
        " (AL)#=AH L
        (AGAIN)=AH G EH N
        #:(AG)E=IH JH
        (A)^+:#=AE
        " :(A)^+ =EY
        (A)^%=EY
        " (ARR)=AH R
        (ARR)=AE R
        " :(AR) =AA R
        " (AR) =ER
        (AR)=AA R
        (AIR)=EH R
        (AI)=EY
        (AY)=EY
        (AU)=AO
        #:(AL) =AH L
        #:(ALS) =AH L Z
        (ALK)=AO K
        (AL)^=AO L
        " :(ABLE)=EY B AH L
        (ABLE)=AH B AH L
        (A)VO=EY
        (ANG)+=EY N JH
        (A)TOM=AE
        (A)TTI=AE
        (A)=AE
        (B)=B
        " (CH)^=K
        ^E(CH)=K
        (CH)=CH
        " S(CI)#=S AY
        (CI)A=SH
        (CI)O=SH
        (CI)EN=SH
        (CC)+=K S
        (C)+=S
        (CK)=K
        (COM)%=K AH M
        (C)=K
        (DG)+=JH
        (D)=D
        #:(E) =
        " :(E) =IY
        T(ED) =IH D
        D(ED) =IH D
        " :^(ED) =EH D
        P(ED) =T
        K(ED) =T
        S(ED) =T
        F(ED) =T
        X(ED) =T
        CH(ED) =T
        SH(ED) =T
        .(ED) =D
        #(ED) =D
        (ED) =D
        S(ES) =IH Z
        X(ES) =IH Z
        Z(ES) =IH Z
        CH(ES) =IH Z
        SH(ES) =IH Z
        C(ES) =IH Z
        G(ES) =IH Z
        J(ES) =IH Z
        (ES) =Z
        (ER) =ER
        " (EVEN)=IY V AH N
        (EV)ER=EH V
        (EW)=Y UW
        " (EYE) =AY
        (EY)=IY
        (EAR)=IY R
        (EA)=IY
        (EE)=IY
        C(EI)=IY
        (EI)=EY
        (EU)=Y UW
        (EO)=IY AH
        #:(E)S =
        #:(E)D =
        (E)=EH
        (FUL)=F UH L
        (F)=F
        (GIV)=G IH V
        " (G)I^=G
        (GE)T=G EH
        (GG)=G
        " B#(G)=G
        (G)+=JH
        (GREAT)=G R EY T
        (GH)T=
        " (GH)=G
        #(GH)=
        (G)N =
        (G)=G
        " (HAV)=HH AE V
        " (HERE)=HH IY R
        " (HOUR)=AW ER
        (HOW)=HH AW
        (H)#=HH
        (H)=
        " (IN)=IH N
        " (I) =AY
        (IN)D=AY N
        " ANT(I)=AY
        (IER)=IY ER
        #:R(IED) =IY D
        (IED) =AY D
        (IEN)=IY EH N
        (IE)T=AY EH
        " :(I)%=AY
        (I)%=IY
        (IE)=IY
        " (IDEA)=AY D IY AH
        (I)^+:#=IH
        (IR)#=AY R
        (IZ)%=AY Z
        (IS)%=AY Z
        (IGH)=AY
        (ILD)=AY L D
        (IR)=ER
        (IQUE)=IY K
        (I)^%=AY
        (I)=IH
        (J)=JH
        " (K)N=
        (K)=K
        (LO)C#=L OW
        #:^(L)%=AH L
        (LEAD)=L IY D
        (L)=L
        (MOV)=M UW V
        (M)=M
        (NG)+=N JH
        (NGL)%=NG G AH L
        (NG)R=NG G
        (NG)#=NG G
        (NG)=NG
        (NK)=NG K
        " (NOW) =N AW
        (N)=N
        (OF) =AH V
        (OROUGH)=ER OW
        #:(OR) =ER
        #:(ORS) =ER Z
        (OR)=AO R
        " (ONE)=W AH N
        " (ONCE)=W AH N S
        " (OVER)=OW V ER
        PR(OV)=UW V
        (OV)=AH V
        ^(O)^%=OW
        ^(O)^EN=OW
        ^(O)^I#=OW
        (OL)D=OW L
        (OUGHT)=AO T
        (OUGH)=AH F
        " (OU)=AW
        H(OU)S#=AW
        (OUS)=AH S
        (OUR)=AO R
        (OULD)=UH D
        (OUP)=UW P
        (OU)=AW
        (OY)=OY
        (OING)=OW IH NG
        (OI)=OY
        (OOR)=AO R
        (OOK)=UH K
        (OOD)=UH D
        (OO)=UW
        (OA)=OW
        (O)E=OW
        (O) =OW
        (ONLY)=OW N L IY
        C(O)N=AA
        (O)NG=AO
        " ^:(O)N=AH
        I(ON)=AH N
        #:(ON) =AH N
        #^(ON)=AH N
        (O)ST =OW
        (OF)^=AO F
        (OTHER)=AH DH ER
        R(O)B=AA
        PR(O):#=UW
        (OW)=OW
        (O)=AA
        (PH)=F
        (PEOP)=P IY P
        (POW)=P AW
        " (PUT) =P UH T
        (P)=P
        (QUAR)=K W AO R
        (QU)=K W
        (Q)=K
        (RE)^#=R IY
        (R)=R
        (SH)=SH
        #(SION)=ZH AH N
        (SOME)=S AH M
        #(SUR)#=ZH ER
        (SURE)=SH UH R
        #(SU)#=ZH UW
        #(SED)=Z D
        (SAID)=S EH D
        ^(SION)=SH AH N
        " (SCH)=S K
        (S)C+=
        #(SM)=Z M
        .(S) =Z
        #:.E(S) =Z
        #:^#(S) =Z
        U(S) =S
        " :#(S) =Z
        #(S)#=Z
        (S)=S
        " (THE) =DH AH
        (TO) =T UW
        " (THAT) =DH AE T
        " (THIS) =DH IH S
        " (THEY)=DH EY
        " (THERE)=DH EH R
        (THER)=DH ER
        (THEIR)=DH EH R
        " (THAN) =DH AE N
        " (THEM) =DH EH M
        (THESE) =DH IY Z
        " (THEN)=DH EH N
        (THROUGH)=TH R UW
        (THOSE)=DH OW Z
        (THOUGH) =DH OW
        (TODAY)=T AH D EY
        (THUS)=DH AH S
        (TH)=TH
        #:(TED) =T IH D
        (TI)O=SH
        (TIA)=SH AH
        (TIEN)=SH AH N
        (TUR)#=CH ER
        (TU)A=CH UW
        " (TWO)=T UW
        (TCH)=CH
        (T)=T
        " (UN)I=Y UW N
        " (UN)=AH N
        " (UPON)=AH P AO N
        (UR)#=Y UH R
        (UR)=ER
        (U)^%=Y UW
        (U)^ =AH
        (U)^^=AH
        (UY)=AY
        G(U)#=
        G(U)%=
        #N(U)=Y UW
        (U)=AH
        (VIEW)=V Y UW
        (V)=V
        " (WERE)=W ER
        (WA)S=W AA
        (WA)T=W AA
        (WHERE)=W EH R
        (WHAT)=W AA T
        (WHOL)=HH OW L
        (WHO)=HH UW
        (WH)=W
        (WAR)=W AO R
        (WOR)^=W ER
        (WR)=R
        (W)=W
        " (X)=Z
        (X)=K S
        " (YOUNG)=Y AH NG
        " (YOU)=Y UW
        " (YES)=Y EH S
        " (Y)=Y
        F(Y)=AY
        #:^(Y) =IY
        #:^(Y)I=IY
        " :(Y) =AY
        " :(Y)#=AY
        " :(Y)^+:#=IH
        " :(Y)^#=AY
        (Y)=IH
        (Z)=Z
        """;

    /// <summary>Gets the number of rules.</summary>
    public static int RuleCount => RulesByLetter.Values.Sum(rules => rules.Length);

    /// <summary>
    ///     Pronounces a word with the rules and the stress heuristics.
    /// </summary>
    /// <param name="word">A word of the letters <c>a</c> to <c>z</c> and apostrophes, any case.</param>
    /// <returns>The pronunciation; empty when the word has no letters.</returns>
    public static Pronunciation Convert(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        // Keep the letters; a doubled consonant letter is one sound (except cc and gg, which have rules)
        var letters = new List<char>();
        foreach (var c in word.ToLowerInvariant())
        {
            if (c is < 'a' or > 'z')
            {
                continue;
            }

            var isVowel = IsVowel(c);
            if (letters.Count > 0 && letters[^1] == c && !isVowel && c is not ('c' or 'g'))
            {
                continue;
            }

            letters.Add(c);
        }

        var text = new string([.. letters]);
        var symbols = new List<string>();
        var position = 0;
        while (position < text.Length)
        {
            var rule = RulesByLetter.GetValueOrDefault(text[position], []).FirstOrDefault(r => r.Matches(text, position));
            if (rule is null)
            {
                position++;
                continue;
            }

            symbols.AddRange(rule.Phones);
            position += rule.Letters.Length;
        }

        return AssignStress(text, symbols);
    }

    /// <summary>
    ///     Tests whether a letter counts as a vowel in the context symbols.
    /// </summary>
    /// <param name="c">A lower-case letter.</param>
    /// <returns><see langword="true"/> for <c>a e i o u y</c>.</returns>
    private static bool IsVowel(char c) => c is 'a' or 'e' or 'i' or 'o' or 'u' or 'y';

    /// <summary>
    ///     Gives the phones their stress with the orthographic heuristics.
    /// </summary>
    /// <param name="word">The letters of the word.</param>
    /// <param name="symbols">The base phone symbols from the rules.</param>
    /// <returns>The pronunciation with stress digits.</returns>
    private static Pronunciation AssignStress(string word, List<string> symbols)
    {
        var vowels = Enumerable.Range(0, symbols.Count).Where(i => IsVowelSymbol(symbols[i])).ToList();
        var stress = new int[vowels.Count];
        if (vowels.Count > 0)
        {
            var primary = PrimaryIndex(word, symbols, vowels);
            stress[primary] = 1;

            // Secondary stress two vowels before the primary, and on the full vowel of -ate / -ize / -ify / -ment
            for (var v = primary - 2; v >= 0; v -= 2)
            {
                stress[v] = 2;
            }

            if (vowels.Count >= 3 && primary < vowels.Count - 1 && (word.EndsWith("ate", StringComparison.Ordinal) || word.EndsWith("ize", StringComparison.Ordinal) || word.EndsWith("ify", StringComparison.Ordinal)))
            {
                stress[^1] = 2;
            }
        }

        // Build the phones; unstressed full vowels reduce to schwa
        var phones = new List<Phone>(symbols.Count);
        var next = 0;
        foreach (var symbol in symbols)
        {
            if (!IsVowelSymbol(symbol))
            {
                phones.Add(Phone.Create(symbol));
                continue;
            }

            var level = stress[next++];
            var name = level == 0 && symbol is "AA" or "AE" or "AO" or "EH" or "UH" ? "AH" : symbol;
            phones.Add(Phone.Create(name, level));
        }

        return new Pronunciation([.. phones]);
    }

    /// <summary>
    ///     Chooses the vowel that carries the primary stress.
    /// </summary>
    /// <param name="word">The letters.</param>
    /// <param name="symbols">The phone symbols.</param>
    /// <param name="vowels">The positions of the vowel phones.</param>
    /// <returns>The index into <paramref name="vowels"/>.</returns>
    private static int PrimaryIndex(string word, List<string> symbols, List<int> vowels)
    {
        var count = vowels.Count;
        if (count == 1)
        {
            return 0;
        }

        // A final syllable that attracts stress to itself
        if (EndsWithAny(word, "ee", "eer", "oon", "ese", "ette", "ique", "esque", "ade"))
        {
            return count - 1;
        }

        // Endings that stress the vowel just before them
        if (EndsWithAny(word, "tion", "sion", "cian", "tial", "cial", "tious", "cious", "ic", "ics", "ian", "ial", "ious", "eous", "ium", "ual", "ience", "iency"))
        {
            return Math.Max(0, count - 2);
        }

        if (EndsWithAny(word, "ical", "ity", "ify", "ography", "ology"))
        {
            return Math.Max(0, count - 3);
        }

        // The stem: inflectional and light derivational endings are not syllables of the stem
        var stem = count - EndingVowels(word, symbols);
        stem = Math.Max(1, stem);
        if (stem == 1)
        {
            return 0;
        }

        if (stem == 2)
        {
            // An unstressed prefix puts the stress on the second syllable
            return EndsWithStartingPrefix(word) && count > 1 ? 1 : 0;
        }

        return stem - 3;
    }

    /// <summary>
    ///     Counts the vowels of a syllabic ending that does not belong to the stem.
    /// </summary>
    /// <param name="word">The letters.</param>
    /// <param name="symbols">The phone symbols.</param>
    /// <returns>The number of vowel phones of the ending (0 when there is none).</returns>
    private static int EndingVowels(string word, List<string> symbols)
    {
        if (EndsWithAny(word, "able", "ible"))
        {
            return 2;
        }

        if (EndsWithAny(word, "ing", "ness", "less", "ful", "ment", "ish", "est", "er", "or", "ly"))
        {
            return 1;
        }

        if (word.EndsWith("ed", StringComparison.Ordinal) && symbols.Count >= 2 && symbols[^2] is "IH" or "AH")
        {
            return 1;
        }

        return 0;
    }

    /// <summary>
    ///     Tests whether a word starts with a prefix that is normally unstressed.
    /// </summary>
    /// <param name="word">The letters.</param>
    /// <returns><see langword="true"/> for a leading <c>a be de re in en ex con com pre pro dis mis sub un</c>.</returns>
    private static bool EndsWithStartingPrefix(string word) =>
        new[] { "a", "be", "de", "re", "in", "en", "ex", "con", "com", "pre", "pro", "dis", "mis", "sub", "un" }
            .Any(prefix => word.Length > prefix.Length + 2 && word.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>
    ///     Tests whether a word ends with any of the given endings.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <param name="endings">The endings.</param>
    /// <returns><see langword="true"/> when one matches.</returns>
    private static bool EndsWithAny(string word, params string[] endings) =>
        endings.Any(e => word.Length > e.Length && word.EndsWith(e, StringComparison.Ordinal));

    /// <summary>
    ///     Tests whether a phone symbol is a vowel.
    /// </summary>
    /// <param name="symbol">The base symbol.</param>
    /// <returns><see langword="true"/> for a vowel.</returns>
    private static bool IsVowelSymbol(string symbol) => symbol is "AA" or "AE" or "AH" or "AO" or "AW" or "AY" or "EH" or "ER" or "EY" or "IH" or "IY" or "OW" or "OY" or "UH" or "UW";

    /// <summary>
    ///     Compiles the rule text.
    /// </summary>
    /// <param name="text">The rules, one per line; the first character of a line that starts with a quote stands for a leading space.</param>
    /// <returns>The rules by first letter of their matched text.</returns>
    private static Dictionary<char, Rule[]> Compile(string text)
    {
        var rules = new Dictionary<char, List<Rule>>();
        foreach (var raw in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // A leading quote protects a leading space of the left context
            var line = raw.StartsWith('"') ? " " + raw[1..] : raw;
            var open = line.IndexOf('(', StringComparison.Ordinal);
            var close = line.IndexOf(')', StringComparison.Ordinal);
            var equals = line.LastIndexOf('=');
            var rule = new Rule(
                line[..open].ToLowerInvariant(),
                line[(open + 1)..close].ToLowerInvariant(),
                line[(close + 1)..equals].ToLowerInvariant(),
                line[(equals + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var key = rule.Letters[0];
            if (!rules.TryGetValue(key, out var list))
            {
                list = [];
                rules[key] = list;
            }

            list.Add(rule);
        }

        return rules.ToDictionary(p => p.Key, p => p.Value.ToArray());
    }

    /// <summary>
    ///     One letter-to-sound rule.
    /// </summary>
    /// <param name="Left">The left context pattern.</param>
    /// <param name="Letters">The letters the rule consumes.</param>
    /// <param name="Right">The right context pattern.</param>
    /// <param name="Phones">The phone symbols the rule produces.</param>
    private sealed record Rule(string Left, string Letters, string Right, string[] Phones)
    {
        /// <summary>
        ///     Tests whether the rule applies at a position.
        /// </summary>
        /// <param name="word">The word.</param>
        /// <param name="position">The index of the first letter the rule would consume.</param>
        /// <returns><see langword="true"/> when letters and both contexts match.</returns>
        public bool Matches(string word, int position) =>
            string.CompareOrdinal(word, position, Letters, 0, Letters.Length) == 0
            && MatchLeft(word, position - 1)
            && MatchRight(word, position + Letters.Length);

        /// <summary>
        ///     Matches the left context, which is read right to left from the letter before the match.
        /// </summary>
        /// <param name="word">The word.</param>
        /// <param name="index">The index of the letter before the match (<c>-1</c> at the word start).</param>
        /// <returns><see langword="true"/> when the context matches.</returns>
        private bool MatchLeft(string word, int index)
        {
            for (var p = Left.Length - 1; p >= 0; p--)
            {
                var symbol = Left[p];
                switch (symbol)
                {
                    case ' ':
                        if (index >= 0)
                        {
                            return false;
                        }

                        break;
                    case '#':
                        if (index < 0 || !IsVowel(word[index]))
                        {
                            return false;
                        }

                        while (index >= 0 && IsVowel(word[index]))
                        {
                            index--;
                        }

                        break;
                    case ':':
                        while (index >= 0 && IsConsonant(word[index]))
                        {
                            index--;
                        }

                        break;
                    default:
                        if (index < 0 || !MatchesOne(symbol, word[index]))
                        {
                            return false;
                        }

                        index--;
                        break;
                }
            }

            return true;
        }

        /// <summary>
        ///     Matches the right context from the letter after the match.
        /// </summary>
        /// <param name="word">The word.</param>
        /// <param name="index">The index of the letter after the match.</param>
        /// <returns><see langword="true"/> when the context matches.</returns>
        private bool MatchRight(string word, int index)
        {
            foreach (var symbol in Right)
            {
                switch (symbol)
                {
                    case ' ':
                        if (index < word.Length)
                        {
                            return false;
                        }

                        break;
                    case '#':
                        if (index >= word.Length || !IsVowel(word[index]))
                        {
                            return false;
                        }

                        while (index < word.Length && IsVowel(word[index]))
                        {
                            index++;
                        }

                        break;
                    case ':':
                        while (index < word.Length && IsConsonant(word[index]))
                        {
                            index++;
                        }

                        break;
                    case '%':
                        return SuffixAlternatives.Any(s => word.Length - index == s.Length && string.CompareOrdinal(word, index, s, 0, s.Length) == 0);
                    default:
                        if (index >= word.Length || !MatchesOne(symbol, word[index]))
                        {
                            return false;
                        }

                        index++;
                        break;
                }
            }

            return true;
        }

        /// <summary>
        ///     Matches one letter against a single-letter context symbol.
        /// </summary>
        /// <param name="symbol">A context symbol or a letter.</param>
        /// <param name="letter">The word's letter.</param>
        /// <returns><see langword="true"/> when the letter satisfies the symbol.</returns>
        private static bool MatchesOne(char symbol, char letter) => symbol switch
        {
            '^' => IsConsonant(letter),
            '.' => letter is 'b' or 'd' or 'v' or 'g' or 'j' or 'l' or 'm' or 'n' or 'r' or 'w' or 'z',
            '+' => letter is 'e' or 'i' or 'y',
            _ => symbol == letter,
        };

        /// <summary>
        ///     Tests whether a letter is a consonant.
        /// </summary>
        /// <param name="letter">A lower-case letter.</param>
        /// <returns><see langword="true"/> for a letter that is not <c>a e i o u y</c>.</returns>
        private static bool IsConsonant(char letter) => letter is >= 'a' and <= 'z' && !IsVowel(letter);
    }
}
