### TextNormalizer

**Purpose**: Decide, token by token, which spans of an English text are spoken differently from how they are
written, and record the decisions as an `AlignedText`. The unit owns every decision about what a span covers,
whether the period of an abbreviation is consumed or kept, which sentence breaks change, and the symbol, mark,
clitic, apostrophe and dropped-character tables. It never rewrites the text and never consults the tagger.

**Data Model**: `TextNormalizer` is a sealed class holding the `AbbreviationResolver`, the
`AcronymRecognizer` and an optional token-visited observer (a test seam; production passes null). It keeps no
per-request state; the numeric rule uses the static `NumericExpressionRecognizer`. The static `Default`
instance is created once, on first use, over the default lexicons. One pass runs in a private `ref struct`
named `Pass` on the caller's stack; it holds the text, the tokens, the user directives and a cursor into them,
the builder, the cancellation token, the first token of the current tokenizer sentence, the cached "sentence is
all capitals" decision and the visited-token counter. `CancellationCheckInterval` is 256 and equals the
part-of-speech adapter's.

**Rule order.** For each token in order, the first rule that matches decides, and a multi-token span consumes
its tokens (the next token processed is the one after it):

- **0. User override**: a directive from the host's pronunciation dictionary names the token. It wins over every
  rule below for the spoken form; the sentence-break edits of rules 3 and 4 are still made (see *User
  directives*)
- **1. Dropped**: the token contains an invisible format character, or is a standalone bullet. One `Dropped`
  span (empty replacement) per maximal run of invisible characters, or for the whole bullet; no other rule looks
  at the token
- **1b. Web address**: `WebAddressRecognizer.Recognize` accepts a token that is a URL (`http://`, `https://` or
  `ftp://` scheme, or a leading `www.`), an e-mail address, or a bare domain whose last label is a known
  top-level domain. The tokenizer keeps such an address in one token, so the spans are inside it: the scheme
  letters and `www` are spelled (`H T T P S`, `W W W`), the two slashes after the scheme are `Dropped`, and each
  separator is a `Symbol` span (`.` is `dot`, `/` is `slash`, `@` is `at`, `-` is `dash`, `_` is `underscore`,
  `?` is `question mark`, `=` is `equals`, `&` is `and`, `#` is `hash`, `+` is `plus`, `%` is `percent`, `~` is
  `tilde`). The words between the separators are left for the phonemizer. No span contains a colon (A5'), so the
  colon after the scheme and before a port stays as a pause mark. Runs before the clitic rule so that an address
  is never taken for a clitic or an abbreviation
- **2. Clitic**: `n't`, `'re`, `'ve`, `'ll`, `'m`, `'s` or `'d` (any apostrophe style, any case). A typographic
  apostrophe becomes an `Apostrophe` span with the replacement `'`; when roles are requested, the role is
  recorded
- **2a. Numeric expression**: `NumericExpressionRecognizer.TryRecognize` accepts the token or flags it as a
  numeric fallback. Up to two spans through `Emit`; the ambiguity and fallback counts are recorded; consumed
  tokens are skipped (see *Numeric rule*)
- **3. Abbreviation**: `AbbreviationResolver.TryResolve` accepts the token (alone, with its adjacent period
  token, or as an initial). The period and sentence rules decide the span, its period and the break edits
- **4. Acronym or initialism**: `AcronymRecognizer.TryRecognize` accepts the token. A single spelled
  `Initialism` span; a dotted form uses the period and sentence rules as class `Free`; an ampersand initialism
  is decomposed (see *Ampersand initialisms*)
- **4b. Mixed letters and digits**: `AlphanumericTokenRecognizer` accepts an identifier of capital letters and
  digits (`A4`, `B12`, `H2O`, `R2D2`, `MP3`: alternating runs of one to three letters and one to four digits) or
  a name joined to one to three digits by a hyphen (`COVID-19`, `Catch-22`). Each letter run is a spelled
  `Initialism` span, each digit run a `Number` span (a cardinal for up to three digits without a leading zero,
  digit by digit otherwise); for the hyphenated name only the digits get a span, so the phonemizer sees
  `COVID-nineteen`. It runs after the lexicons so that an entry of either wins
- **5. Symbol or mark**: a standalone operator or mark, or a mark, `&` or `/` inside a word. `Symbol` spans
- **6. Elision apostrophe**: a typographic apostrophe between two letters, or leading `'tis`, `'twas`, `'til`,
  `'cause`, `'em`. `Apostrophe` spans
- **7. Identity**: anything else. Nothing is recorded; numbers the recognizer does not read pass through

**User directives (rule 0).** `Normalize` takes a strictly ascending list of `TokenDirective(tokenIndex, kind,
value)` values that the analyzer resolved from the host's dictionary (see *PronunciationOverrides Design*); the
default is none, which gives exactly the behavior without overrides. The pass keeps a cursor into the list. At
the start of each token it discards every directive whose index is below the token: such a token was consumed
as a follow-up token of an earlier multi-token match (the `kg` of `5 kg`, the `million` of `$5 million`), the
match is atomic and wins, and the directive is counted as **ignored**. A directive at the token's own index is
applied and the token is done: rules 1, 2, 2a, 5 and 6 do not run and rules 3 and 4 run only in decision-only
mode, so a directive beats clitics, symbols, numeric heads, lexicon abbreviations, the acronym table and rule,
and ampersand initialisms for the spoken form.

- `Skip`: nothing is emitted; the token is spoken as written (applied)
- `Reading`: same as `Skip`; the reading id only concerns the analyzer's reading annotation, never the spoken
  text (applied)
- `Replace`: one `Override` span over the token whose spoken text is the directive's value, through `Emit` (so
  the A6 separating space applies); never spelled (applied)
- `Spell`: one spelled `Override` span whose spoken text is the token's ASCII letters upper-cased and separated
  by single spaces (`GIF` gives `G I F`); a token that is empty or not only ASCII letters is spoken as written
  and counted **ignored**, otherwise applied
- `Pronounce`: one `PronunciationMark` over the token with the directive's `scheme:phonemes` value; no span
  (applied)

A `Replace` or `Pronounce` directive without a value is defective and handled like a defective `Spell` (spoken
as written, ignored). After the last token the directives that remain are counted as ignored. A directive list
that is not strictly ascending, or names an index outside the token list, throws `ArgumentException` before any
rule runs.

**Sentence breaks under a directive (decision-only mode).** A directive changes how a word is spoken, never
where a sentence ends. Before applying a directive, the pass therefore makes the sentence-break edits the
abbreviation and acronym rules would make (only when the token ends with a period or a lone period token
touches it): it runs the same chain as the normal rules with `decisionOnly` set, so the period rules still apply
the suppress or force edit but return before counting and emitting. No span is emitted, no count changes and no
built-in replacement can overlap the directive's own span, and the reconciled sentence breaks are identical with
and without the directive (`See Fig. 5 for details.` keeps no break, exactly as for the built-in reading). A
numeric rule never needs a decision for a directive token, because a numeric expression starts with a digit,
sign, currency symbol or `#` and a dictionary key starts with a letter.

**Spelled replacements.** `Emit` decides once whether a replacement is spelled: unless the kind is `Override`
(`Replace` is never spelled, `Spell` is spelled by definition), the replacement is spelled when its spoken text,
before the A6 padding, is `[A-Z]( [A-Z])*`. That covers every initialism of the table and rule, dotted
initialisms (`U.S.`), single-letter initials (`J.`), dotted credentials (`Ph.D.` gives `P H D`) and the
meridiem tokens of the lexicon (`p.m.`). Word replacements (`Doctor`) are not spelled. The builder rejects a
span marked spelled whose value is not a letter sequence (A11). The meridiem of a glued time (`5:30pm`) is its
own adjacent numeric span (`P M`) after the span of the words, so it is spelled like a separate-token meridiem.
A replacement is therefore `en.spell` exactly when the normalizer marked it spelled; every other replacement,
including a `Say` payload such as `I`, is `en.norm`.

**Ampersand initialisms** (`AT&T`, `R&D`, `Q&A`, with an optional possessive tail `'s`) cannot be one spelled
span because the ampersand is the word `and`. The normalizer emits one spelled `Initialism` span per group of
capital letters (`A T`) and one `" and "` `Symbol` span per ampersand; a possessive tail stays outside the spans,
glued to the last group, and a typographic apostrophe in the tail gets the usual `Apostrophe` span. Applying the
spans gives `A T and T's`, `R and D`, `Q and A`; `AT&T’s` gives `A T and T's`.

**Numeric rule (2a).** The rule sits before the abbreviation lexicon so that `5 ft.` reads the unit and
`Jan. 5` leaves the number to this rule. For a match the normalizer counts a numeric fallback and an ambiguous
reading (at most once per match) when the match asks for them; moves the "sentence start" bookkeeping when a
consumed token started a tokenizer sentence and suppresses a tokenizer break that would lie strictly inside a
span (A8b); and emits each span through `Emit`, so the A6 padding applies (`50%` gives `fifty` and `" percent"`;
`5–10` gives `five`, `" to "` and `ten`). For a unit that may carry an abbreviation period it applies the period
and sentence rules below as class `Suffix` (`He is 6 ft. tall.` gives `six feet tall`; `He is 6 ft. Then he
left.` keeps the period and forces a break before `Then`). A consumed token with no span of its own (`million`
in `5 million USD`) is not visited again but counts as visited for the cancellation cadence.

**Period and sentence rules.** They apply when the candidate has a period, either inside the token (`Dr.`,
`etc.`, `U.S.`, `ft.`) or as a separate adjacent token (`Ave` and `.`, `p.m` and `.`). Let `last` be the period
token (or the token itself), `following` the first token after `last` that is not a closing quote or bracket,
and `endsSentence` true when there is no `following` token or the tokenizer starts a sentence at `following`
(or at a closing mark before it).

- `Prefix` (a title, `Saint`, `Doctor`, `Number`, an initial), or a `Suffix`, `Word` or `Free` form that
  **continues a name**: if `endsSentence` and the tokenizer has a break there, **suppress** it (a title never
  ends a sentence); the period is consumed, the span includes it
- `Suffix` or `Word`: if not `endsSentence` and `following` starts with an upper-case letter, **force** a break
  at `following`, and `endsSentence` becomes true; the period is kept as the terminator, outside the span, if
  `endsSentence`, otherwise consumed
- `Free` (`etc.`, `e.g.`, `U.S.`): no break edit (the tokenizer decides); the period is kept outside the span if
  `endsSentence`, otherwise consumed

For the `Word` reading (`No.` the word) no span is emitted: only the break edit is recorded. **Continuing a
name.** A candidate whose class is not `Prefix` *continues a name* when `following` is the very next token after
`last` and both the candidate token and `following` are listed name suffixes or credentials
(`AbbreviationResolver.IsNameContinuation`: `Jr`, `Sr`, `Esq`, `II`, `III`, `IV`, `Ph.D`, `PhD`, `M.D`, `D.D.S`,
`D.M.D`, `D.V.M`, `D.O`, `J.D`, `R.N`, `C.P.A`, `MBA`, `CPA`, with or without the final period). Such a pair
(`John Smith Jr. Ph.D. spoke.`) belongs to one name, so the candidate is handled like a `Prefix`. Both tokens
must be listed, so `Maple Ave. Ph.D.` keeps its break, and a following sentence starter such as `He` still gets
its forced break (`I met John Smith Jr. He left.`). "Consumed" means the span includes the period; "kept" means
the span ends one character earlier and the period stays identity text, so a backend that applies the
replacement keeps the sentence terminator and never invents one in mid-sentence. A resolution that used the
default or chose between both cues is counted once as ambiguous. Examples: `I live on Main St. It is nice.`
annotates `St` as `Street` and forces a break before `It`; `Sen. Smith spoke.` consumes the period and suppresses
the tokenizer's break before `Smith`; `He left Mon. He came Tue.` keeps the period and forces a break before the
capitalized next word.

**Standalone operators** (class `Symbol`): the token is exactly the character and whitespace or a text edge
lies on both sides, so `<b>`, `(+)` and `a=b` stay identity.

| Character | Spoken | Character | Spoken |
| --- | --- | --- | --- |
| `&` | and | `×` (U+00D7) | times |
| `@` | at | `÷` (U+00F7) | divided by |
| `+` | plus | `±` (U+00B1) | plus or minus |
| `=` | equals | `≈` (U+2248) | approximately equal to |
| `<` | less than | `≠` (U+2260) | not equal to |
| `>` | greater than | `≤` (U+2264) | less than or equal to |
| `/` | slash | `≥` (U+2265) | greater than or equal to |
| `\` | backslash | | |

**Marks** (class `Symbol`; the token is the mark, or the mark is a character inside a word token): `©`
copyright, `®` registered, `™` trademark, `§` section, `¶` paragraph. Where a letter or digit touches a mark, A6
adds the separating space (`Brand™` becomes `Brand trademark`); two adjacent marks are separate words with
exactly one space between them (`A™®B` becomes `A trademark registered B`).

**Inside a word** (class `Symbol`, the span is the single character): an `&` between two letters is spoken
`" and "` (`Johnson&Johnson`), and the only `/` of a token that is two or more ASCII letters, a slash and two or
more ASCII letters is spoken `" slash "` (`he/she`). `km/h`, `a/b`, `24/7`, `://` and `www.` forms are left
alone.

**Dropped characters** (kind `Dropped`, empty replacement): U+00AD (soft hyphen), U+200B (zero-width space),
U+2060 (word joiner), U+FEFF (byte-order mark), U+200E and U+200F (direction marks), U+202A to U+202E and
U+2066 to U+2069 (bidirectional controls), anywhere in a token (one span per maximal run), and a standalone
bullet token (`•` `‣` `◦` `⁃` `▪` `●`). U+200C and U+200D are not dropped because they glue emoji sequences.
Nothing else is ever dropped.

**Apostrophes** (kind `Apostrophe`, replacement `'`, never padded): every U+2018, U+2019, U+201B or U+02BC
inside a clitic token, between two letters inside a word (`y’all`, `o’clock`), or leading one of the listed
elisions (`’tis`, `’twas`, `’til`, `’cause`, `’em`). The real tokenizer splits `’til` and `’cause` into a lone
apostrophe token followed by the word, so a lone typographic apostrophe token that touches an elision word and
is not preceded by a letter or digit is canonicalized as well. Quote uses (`‘hello’`) and the plural-possessive
tail (`dogs’`) stay identity.

**Clitic classification** (only when requested; no annotation is emitted): the spelling gives `n't` Not,
`'re` Are, `'ve` Have, `'ll` Will, `'m` Am, and `'s` and `'d` a provisional Is and Would that context refines.
The next word is the following token when it is a word that does not start a new tokenizer sentence, skipping
at most one adverb of the stated list.

- `'d`: `Had` if the next word (after skipping one of never, always, just, already, also) is in {better, been,
  had, got, gone, done, seen, taken, made, come}; otherwise `Would`
- `'s` after `let`: `Us`
- `'s` after a contraction host (it, he, she, that, there, here, what, who, where, when, how, why, which,
  everyone, everybody, someone, somebody, anyone, anybody, nobody, everything, something, anything, nothing;
  case-insensitive): `Has` if the next word is in {been, got, gotten, had, done, gone, seen, made, taken, come,
  become}, otherwise `Is`
- `'s` after any other host or no adjacent host: a contraction (`Has` or `Is` as above) if the next word (after
  skipping one of never, always, just, already, also, still, really, now) is in {a, an, the, not, no, been, got,
  gotten, going, gone, being, here, there, so, too, very, all, only, back, out, up, down, over, in, on, at}, or
  has at least five letters and ends in `ing`; otherwise `Possessive`
- a lone `'` after a word ending in `s`: not a clitic token, not classified

The host of an `'s` is the previous token only when it touches the clitic and starts with a letter. A known
heuristic limit: `the dog's training session` classifies `Is`, because the next word ends in `ing`.

**Shouting guard.** A tokenizer sentence is all capitals when it has at least three alphabetic tokens of two
or more letters and all of them are upper-case. It is computed at most once per sentence, and only when a plain
upper-case token of up to six characters needs it; the acronym recognizer then leaves ambiguous listed forms and
rule-spelled forms alone.

**Key Methods**:

- **TextNormalizer(abbreviations, acronyms, tokenVisited)**: stores the collaborators; a null collaborator
  throws `ArgumentNullException`
- **Default**: the shared instance over the default lexicons, created on first use under `Lazy<T>`
- **Normalize(text, tokens, classifyClitics, cancellationToken, directives)**: checks the arguments, verifies
  that every token is non-empty, ascending, non-overlapping and inside the text, validates the directives, then
  runs one `Pass` and returns the builder's `AlignedText`. The cancellation token is checked at the start and
  each time the visited-token count reaches a multiple of 256 (a token consumed by a numeric expression still
  counts as visited); the observer, if any, is called with the index of each processed token
- **Emit** (inside the pass): adds a span with the A6 padding: one leading space where a letter or digit of the
  source touches the span, or the previous span directly touches it and its spoken text ends with a letter, and
  one trailing space where a letter or digit of the source touches the span, provided the replacement starts
  (leading) or ends (trailing) with a letter. Only the later of two adjacent replacements pads the shared
  boundary, so no offset moves; `Dropped` and `Apostrophe` replacements are never padded. It also decides the
  spelled flag
- **Internal static helpers** (`TryGetClitic`, `IsWordPair`, `OperatorWords`, `MarkWords`, `ContainsDropped`,
  `IsBullet`, the apostrophe and letter-case predicates): stateless lexical functions that carry the tables
  above

**Error Handling**: a null text, null collaborator, malformed token list or malformed directive list throws
`ArgumentNullException` or `ArgumentException` before any rule runs; a canceled token throws
`OperationCanceledException` at the next check, and nothing partial is returned. An `ArgumentException` from
`AlignedTextBuilder.Add` means a rule broke the alignment contract and propagates unchanged: it is a defect of
this unit or the recognizer, not of any input. A malformed embedded lexicon (a defective build) propagates from
`Default` as an `InvalidDataException`. Input text can never cause any other failure, because every neighbor
access is bounds-checked and every span is validated.

**Dependencies**: `NumericExpressionRecognizer`, `AbbreviationResolver`, `AcronymRecognizer` and the
*NormalizationAlignment* unit (the builder, tokens, spans and enumerations). `NumberVerbalizer` is reached
only through the recognizer. No other unit and no OTS item.

**Callers**: `EnglishTextAnalyzer`, and the tests.
