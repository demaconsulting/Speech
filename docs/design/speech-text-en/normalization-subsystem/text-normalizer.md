### TextNormalizer

**Purpose**: Decide, token by token, which spans of an English text are spoken differently from how they are
written, and record the decisions as an `AlignedText`. The unit owns every decision about what a span covers,
whether the period of an abbreviation is consumed or kept, which sentence breaks change, and the symbol, mark,
clitic, apostrophe and dropped-character tables. It never rewrites the text and never consults the tagger.

**Data Model**: `TextNormalizer` is a sealed class holding the `AbbreviationResolver`, the
`AcronymRecognizer` and an optional token-visited observer (a test seam; production passes null). It keeps no
per-request state; the numeric rule uses the static `NumericExpressionRecognizer`. The static `Default`
instance is created once, on first use, over the default lexicons.
One pass runs in a private `ref struct` named `Pass` that holds the text, the token array, the user directives
and a cursor into them, the builder, the
cancellation token, the index of the first token of the current tokenizer sentence, the cached
"sentence is all capitals" decision (not yet computed, no, or yes) and the visited-token counter; it lives on
the caller's stack. The constant `CancellationCheckInterval` is 256 and equals the part-of-speech adapter's.
The word sets used by the clitic classification are code constants listed below.

**Rule order.** For each token in order, the first rule that matches decides, and a multi-token span consumes
its tokens (the next token processed is the one after it):

| Rule | Condition | Result |
| --- | --- | --- |
| 0. User override | A directive from the host's pronunciation dictionary names this token | The directive wins over every rule below for the spoken form; the sentence-break edits of rules 3 and 4 are still made, see _User directives (rule 0)_ |
| 1. Dropped | The token contains an invisible format character, or is a standalone bullet | One `Dropped` span (empty replacement) per maximal run of invisible characters, or for the whole bullet; no other rule looks at that token |
| 2. Clitic | The token is `n't`, `'re`, `'ve`, `'ll`, `'m`, `'s` or `'d` (any apostrophe style, any case) | A typographic apostrophe becomes an `Apostrophe` span with the replacement `'`; when roles are requested, the role is recorded |
| 2a. Numeric expression | `NumericExpressionRecognizer.TryRecognize` accepts the token (a number, currency amount, percent, unit, time, date or digit group, alone or with its context tokens) or flags it as a numeric fallback | Up to two spans, emitted in order through `Emit`; the ambiguity and fallback counts are recorded; the tokens the expression consumed are skipped; a unit that may carry an abbreviation period uses the period and sentence rules (see _Numeric rule_) |
| 3. Abbreviation | `AbbreviationResolver.TryResolve` accepts the token (alone, with its adjacent period token, or as an initial) | The period and sentence rules below decide the span, its period and the break edits |
| 4. Acronym or initialism | `AcronymRecognizer.TryRecognize` accepts the token | A single `Initialism` span (spelled); a dotted form uses the period and sentence rules as class `Free`; an ampersand initialism is decomposed, see _Spelled replacements_ |
| 5. Symbol or mark | A standalone operator or mark, or a mark, `&` or `/` inside a word (tables below) | `Symbol` spans |
| 6. Elision apostrophe | A typographic apostrophe between two letters, or leading `'tis`, `'twas`, `'til`, `'cause`, `'em` | `Apostrophe` spans |
| 7. Identity | Anything else | Nothing is recorded: the deferred capabilities and every number the recognizer does not read pass through |

**User directives (rule 0).** `Normalize` takes a strictly ascending list of `TokenDirective(tokenIndex, kind,
value)` values that the analyzer resolved from the host's dictionary (see *PronunciationOverrides Design*);
the default is none, which gives exactly the behavior without overrides. The pass keeps a cursor into the list.
At the start of each token it first discards every directive whose index is below the token: such a token was
consumed as a follow-up token of an earlier multi-token match (the `kg` of `5 kg`, the `million` of `$5
million`, the `pm` of `5 pm`), the match is atomic and wins, and each such directive is counted as **ignored**.
A directive at the token's own index is applied and the token is done (`return index + 1`): rules 1, 2, 2a, 5
and 6 do not run, and rules 3 and 4 run only in decision-only mode (below), so a directive beats clitics,
symbols, numeric heads, lexicon abbreviations, the acronym table and rule, and ampersand initialisms for the
spoken form. The four kinds:

| Kind | Effect | Counted |
| --- | --- | --- |
| `Skip` | Nothing is emitted; the token is spoken as written | applied |
| `Replace` | One `Override` span over the token whose spoken text is the directive's value, through `Emit` (so the A6 separating space applies); never spelled | applied |
| `Spell` | One `Override` span, spelled, whose spoken text is the token's ASCII letters upper-cased and separated by single spaces (`GIF` gives `G I F`) | applied; a token that is empty or not only ASCII letters is spoken as written and counted **ignored** (defensive: the dictionary cannot produce it) |
| `Pronounce` | One `PronunciationMark` over the token with the directive's `scheme:phonemes` value; no span | applied |

A `Replace` or `Pronounce` directive without a value is defective and handled like a defective `Spell`
(spoken as written, ignored). After the last token the directives that remain (consumed by the last match) are
counted as ignored. A directive list that is not strictly ascending, or names an index outside the token list
(checked without arithmetic, so extreme values cannot overflow), throws `ArgumentException` before any rule
runs.

**Sentence breaks under a directive (decision-only mode).** A directive changes how a word is spoken, never
where a sentence ends, and the abbreviation and acronym period rules (rules 3 and 4) are the only producers of
force and suppress edits for a word a host can override. Before applying a directive (applied or defective
alike), the pass therefore calls `PreserveBreakEdits`. Only a period makes those rules touch breaks, so it
returns at once unless the token ends with a period or a lone period token touches it (no lookup, no
allocation, constant work); otherwise it runs the same chain as `ProcessToken` (an abbreviation match wins
over the acronym rule) through the same `ApplyPeriodRules` and `TryApplyAcronym` bodies with the parameter
`decisionOnly` set. In that mode the period rules still find the following token, apply `SuppressBreak` or
`ForceBreak` (including the name-continuation rule), and then return before `CountAmbiguous` and `Emit`: no span
is emitted, no count changes, and so no replacement of the built-in rule can overlap the directive's own span.
The period token that follows an overridden word (`Fig` and `.`) is then visited normally and makes no edit of
its own. Consequences: the reconciled sentence breaks are identical with and without the directive, for
example `See Fig. 5 for details.` keeps no break (the tokenizer's break before `5` is suppressed exactly as
for the built-in reading) and `Hello—Dr. Smith left.` keeps its suppression, where the tokenizer separates the
period of `Dr`; a built-in reading that a directive bypassed is not counted as ambiguous; every token is
still visited once, so the cancellation cadence is unchanged; and the extra work is constant per directive
token, so the pass stays linear. A numeric rule never needs a decision for a directive token, because a
numeric expression starts with a digit, sign, currency symbol or `#` and a dictionary key starts with a
letter.

**Spelled replacements.** `Emit` decides once whether a replacement is spelled: unless the kind is `Override`
(whose `Replace` directive is never spelled and whose `Spell` directive is spelled by definition), the replacement
is spelled when its spoken text, before the A6 padding, is `[A-Z]( [A-Z])*` (`SpelledGrammar.IsLetterSequence`).
That covers every initialism of the table and rule, dotted initialisms (`U.S.`), single-letter initials
(`J.`), dotted credentials (`Ph.D.` gives `P H D`, `M.D.` gives `M D`) and the
meridiem tokens of the lexicon (`p.m.`). Word replacements (`Doctor`) are not spelled. The builder then rejects
a span marked spelled whose value is not a letter sequence (A11). The meridiem of a glued time or hour
(`5:30pm`, `5pm`) is not part of the numeric span: the recognizer emits it as its own adjacent span (`P M`, a
`DateTime` replacement) after the span of the words (`five thirty`), so `Emit` marks it spelled like the
separate-token meridiem and it is annotated `en.spell`. The spelled flag of an `en.norm` value is therefore never
inferred downstream: a replacement is `en.spell` exactly when the normalizer marked it spelled, and every other
replacement, including a `Say` payload such as `I`, is `en.norm` and spoken as words.

**Ampersand initialisms** (`AT&T`, `R&D`, `Q&A`, with an optional possessive tail `'s`) cannot be one spelled
span because the ampersand is the word `and`. `EmitAmpersandInitialism` emits one `Initialism` span per group of
capital letters (spelled, letters only: `A T`) and one `and` `Symbol` span per ampersand; a possessive tail is
left outside the spans, glued to the last group (the last group is not padded on its right), and a typographic
apostrophe in the tail gets the usual `Apostrophe` span. The spoken text, obtained by applying the spans, is
identical to what the single span used to give: `A T and T's`, `R and D`, `Q and A`, and `AT&T’s` gives `A T and
T's`. Where a letter group touches an adjacent replacement, `Emit` separates the two with exactly one space.

**Numeric rule (2a).** The rule sits before the abbreviation lexicon so that `5 ft.` reads the unit and
`Jan. 5` leaves the number to this rule. A token with an invisible character is dropped by rule 1 first, so a
numeric token that contains one (`5` + U+200B + `0`) is never read as a number. For a match, `ApplyNumeric`:

1. counts a numeric fallback (`CountFallback`) and an ambiguous reading (`CountAmbiguous`, at most once per
   match) when the match asks for them;
2. moves the "sentence start" bookkeeping when a token the expression consumed started a tokenizer sentence
   (the tokenizer cuts `Jan.` from `5`, and sometimes a `$` from its number), and suppresses a tokenizer
   break that would lie strictly inside a span (A8b);
3. emits each span through `Emit`, so the A6 padding and "exactly one separator between adjacent
   replacements" apply (`50%` gives `fifty` and `percent`; `5–10` gives `five`, `to` and `ten`);
4. for a unit that may carry an abbreviation period (`ft`, `lb`, `lbs`, `oz`, `yd`, `mi`, `hr`, `hrs`, `min`
   and `Ft`), applies the period and sentence rules below to the unit token as class `Suffix`: the period
   (inside the token, or a separate adjacent token) is consumed in the middle of a sentence and kept at the
   end of one, and a capitalized next word forces a break (`He is 6 ft. tall.` gives `six feet tall`;
   `He is 6 ft. Then he left.` keeps the period and forces a break before `Then`; `5 Ft. Worth` gives
   `five feet. Worth`, because the suffix rules keep the period and force the break before any capitalized
   word, even a name);
5. returns the index after the last consumed token. A consumed token with no span of its own (the word
   `million` in `5 million USD`) is not visited again, but counts as visited for the cancellation cadence.

The span extent, spoken words and policy are decided by the recognizer (see *NumericExpressionRecognizer
Design*); this unit adds only the counting, the break edits and the period rules.

**Period and sentence rules.** They apply when the candidate has a period, either inside the token (`Dr.`, `etc.`,
`U.S.`, `ft.`, `Fri.`) or as a separate adjacent token (`Ave` and `.`, `p.m` and `.`, `lbs` and `.`). Let `last` be the
period token (or the token itself), `following` the first token after `last` that is not a closing quote or bracket, and
`endsSentence` true when there is no `following` token or the tokenizer starts a sentence at `following` (or at a
closing mark before it).

| Resolved class | Break edit | The period |
| --- | --- | --- |
| `Prefix` (a title, `Saint`, `Doctor`, `Number`, an initial), or a `Suffix`, `Word` or `Free` form that **continues a name** (see below) | If `endsSentence` and the tokenizer has a break there: **suppress** it (a title never ends a sentence) | Consumed: the span includes it |
| `Suffix` or `Word` | If not `endsSentence` and `following` starts with an upper-case letter: **force** a break at `following`, and `endsSentence` becomes true (a lower-case or digit next word leaves the sentence running) | Kept as the sentence terminator, outside the span, if `endsSentence`; otherwise consumed |
| `Free` (`etc.`, `e.g.`, `U.S.`) | None (the tokenizer already decides) | Kept outside the span if `endsSentence`; otherwise consumed |

For the `Word` reading (`No.` the word) no span is emitted: only the break edit is recorded. **Continuing a name.** A
candidate whose class is not `Prefix` *continues a name* when `following` is the very next token after `last` (no
closing mark between) and both the candidate token and `following` are listed name suffixes or credentials
(`AbbreviationResolver.IsNameContinuation`: `Jr`, `Sr`, `Esq`, `II`, `III`, `IV`, `Ph.D`, `PhD`, `M.D`, `D.D.S`,
`D.M.D`, `D.V.M`, `D.O`, `J.D`, `R.N`, `C.P.A`, `MBA`, `CPA`, with or without the final period). Such a pair
(`John Smith Jr. Ph.D. spoke.`, `Ann Lee Esq. Ph.D. M.D. left.`) belongs to one name, so the candidate is handled
exactly like a `Prefix`: no break is forced, the tokenizer's break after it (the tokenizer itself breaks after `Esq.`
and `Ph.D.` before another capitalized token) is suppressed, and the period is consumed. Both tokens must be listed, so
`Maple Ave. Ph.D.` and `Acme Inc. Ph.D. here.` (address and business suffixes) keep their break, and a following word
that is not a listed credential, in particular a sentence starter such as `He`, `Then` or `I`, still gets its forced
break (`I met John Smith Jr. He left.`). The check is O(1) per abbreviation, so the pass stays linear. "Consumed" means
the span includes the period (the whole token, or both tokens, contiguous). "Kept" means the span ends one character
earlier (`St` of `St.`, or only the word token) and the period stays identity text, so a backend that applies the
replacement keeps the sentence terminator and never invents one in mid-sentence (`Main Street. It is nice.` versus
`Doctor Smith`). A resolution that used the default or chose between both cues is counted once as ambiguous, whichever
class it ends up with. Examples: `I live on Main St. It is nice.` annotates `St` as `Street` and forces a break before
`It`; `Sen. Smith spoke.` consumes the period (the tokenizer splits `Sen` from its period and may break before `Smith`,
which is suppressed because a title never ends a sentence); `pens, etc. and more` consumes the period of `etc.`;
`Meet at 5 p.m. Then sleep.` annotates `5` (`five`) and `p.m` (`P M`, the period kept); `Jan. 5, 2024` reads the month
abbreviation as a prefix (period consumed, the tokenizer's break after it suppressed) and `Then Fri. ended.` reads
`Fri.` as a suffix with a lower-case next word, so the period is consumed and no break is forced (`Then Friday ended.`),
while `He left Mon. He came Tue.` keeps the period and forces a break before the capitalized next word.

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

**Marks** (class `Symbol`; the token is the mark, or the mark is a character inside a word token, and it may
touch a word): `©` copyright, `®` registered, `™` trademark, `§` section, `¶` paragraph. Where a letter or
digit touches a mark, A6 adds the separating space (`Brand™` becomes `Brand trademark`). Two adjacent marks
are separate words with exactly one space between them (`A™®B` becomes `A trademark registered B`, with the
replacements `trademark` and `registered` on the two source characters).

**Inside a word** (class `Symbol`, the span is the single character): an `&` between two letters is spoken
`and` (`Johnson&Johnson`), and the only `/` of a token that is two or more ASCII letters, a slash and two or
more ASCII letters is spoken `slash` (`he/she`). `km/h`, `a/b`, `24/7`, `://` and `www.` forms are left
alone. The padding spaces are part of the replacement (A6).

**Dropped characters** (kind `Dropped`, empty replacement): U+00AD (soft hyphen), U+200B (zero-width space),
U+2060 (word joiner), U+FEFF (byte-order mark), U+200E and U+200F (direction marks), U+202A to U+202E and
U+2066 to U+2069 (bidirectional controls), anywhere in a token (one span per maximal run), and a standalone
bullet token (`•` `‣` `◦` `⁃` `▪` `●`). U+200C and U+200D are not dropped because they glue emoji sequences.
Nothing else is ever dropped.

**Apostrophes** (kind `Apostrophe`, replacement `'`, never padded): every U+2018, U+2019, U+201B or U+02BC
inside a clitic token, between two letters inside a word (`y’all`, `o’clock`, `ma’am`), or leading one of the
listed elisions (`’tis`, `’twas`, `’til`, `’cause`, `’em`). The real tokenizer keeps `’Tis` and `’em` whole
but splits `’til` and `’cause` into a lone apostrophe token followed by the word, so a lone typographic
apostrophe token that touches an elision word and is not itself preceded by a letter or digit is canonicalized
as well. Quote uses (`‘hello’`) and the plural-possessive tail (`dogs’`) stay identity.

**Clitic classification** (only when requested; no annotation is emitted): the spelling gives `n't` Not,
`'re` Are, `'ve` Have, `'ll` Will, `'m` Am, and `'s` and `'d` a provisional Is and Would that context
refines. The next word is the following token when it is a word that does not start a new tokenizer
sentence, skipping at most one adverb of the stated list.

| Clitic | Rule |
| --- | --- |
| `'d` | `Had` if the next word (after skipping one of never, always, just, already, also) is in {better, been, had, got, gone, done, seen, taken, made, come}; otherwise `Would` |
| `'s` after `let` | `Us` |
| `'s` after a contraction host | The hosts are it, he, she, that, there, here, what, who, where, when, how, why, which, everyone, everybody, someone, somebody, anyone, anybody, nobody, everything, something, anything and nothing (case-insensitive). The role is `Has` if the next word is in {been, got, gotten, had, done, gone, seen, made, taken, come, become}, otherwise `Is` |
| `'s` after any other host (a noun or name) or after no adjacent host | A contraction (then `Has` or `Is` as above) if the next word (after skipping one of never, always, just, already, also, still, really, now) is in {a, an, the, not, no, been, got, gotten, going, gone, being, here, there, so, too, very, all, only, back, out, up, down, over, in, on, at}, or has at least five letters and ends in `ing`; otherwise `Possessive` |
| A lone `'` after a word ending in `s` | Not a clitic token: not classified |

The host of an `'s` is the previous token only when it touches the clitic and starts with a letter. A
documented heuristic limit, kept in the golden tests: `the dog's training session` classifies `Is`, because
the next word ends in `ing`; a later increment may refine this with tags.

**Shouting guard.** A tokenizer sentence is all capitals when it has at least three alphabetic tokens of two
or more letters and all of them are upper-case. It is computed at most once per sentence, and only when a
plain upper-case token of up to six characters needs it; the acronym recognizer then leaves ambiguous listed
forms and rule-spelled forms alone.

**Key Methods**:

- **TextNormalizer(abbreviations, acronyms, tokenVisited)**: stores the collaborators; a null collaborator
  throws `ArgumentNullException`
- **Default**: the shared instance over the default lexicons, created on first use under `Lazy<T>`; nothing
  is loaded when an analyzer is constructed
- **Normalize(text, tokens, classifyClitics, cancellationToken, directives)**: checks the arguments, checks the
  token, verifies that every token is non-empty, ascending, non-overlapping and inside the text, validates the
  directives (strictly ascending, in range; `ArgumentException` otherwise), then runs one
  `Pass` and returns the builder's `AlignedText`. The cancellation token is checked at the start and each
  time the visited-token count reaches a multiple of 256. The observer, if any, is called with the index of
  each processed token before it is processed
- **Internal static helpers** (`TryGetClitic`, `IsWordPair`, `OperatorWords`, `MarkWords`, `ContainsDropped`,
  `IsDroppedCharacter`, `IsBullet`, the apostrophe and letter-case predicates): stateless lexical functions
  that carry the tables above; internal so the unit tests can check them directly
- **Emit** (inside the pass): adds a span, giving the replacement one leading space where a letter or digit of
  the source touches the span, or the previous span directly touches it and its spoken text ends with a
  letter (`AlignedTextBuilder.TouchesPreviousSpokenLetter`), and one trailing space where a letter or digit of
  the source touches the span, provided the replacement starts (leading) or ends (trailing) with a letter
  (A6). Only the later of two adjacent replacements pads the shared boundary, so it carries exactly one space
  and no offset moves; `Dropped` and `Apostrophe` replacements are never padded. It also decides the spelled
  flag of the span (see *Spelled replacements*)
- **ApplyDirective, SpellAsciiLetters, EmitAmpersandInitialism, SpellGroup** (inside the pass): the effect of
  one directive, the letters of a token made only of ASCII letters (null otherwise), the decomposition of an
  ampersand initialism, and the letter-group joiner
- **PreserveBreakEdits** (inside the pass): makes the sentence-break edits the abbreviation and acronym rules
  would make for a directive's token, through `ApplyPeriodRules` and `TryApplyAcronym` with `decisionOnly` set,
  emitting and counting nothing (see *Sentence breaks under a directive*)

**Error Handling**: a null text, null collaborator, malformed token list or malformed directive list throws
`ArgumentNullException` or `ArgumentException` before any rule runs; a canceled token throws
`OperationCanceledException` at the next check, and nothing partial is returned. An `ArgumentException` from
`AlignedTextBuilder.Add` means a rule broke the alignment contract and propagates unchanged: it is a defect of this
unit, not of any input, and the property tests are there to prove it never happens. A malformed embedded lexicon (a
defective build) propagates from `Default` as an `InvalidDataException`. A numeric match that the recognizer reports is
trusted only through the builder: its spans are validated like any other (a numeric span that broke the contract would
throw `ArgumentException`, a defect of the recognizer that the property tests exclude). Input text can never cause any
other failure, because every neighbor access is bounds-checked and every span is validated.

**Dependencies**: `NumericExpressionRecognizer`, `AbbreviationResolver`, `AcronymRecognizer` and the
*NormalizationAlignment* unit (the builder, tokens, spans and enumerations). `NumberVerbalizer` is reached
only through the recognizer. No other unit and no OTS item.

**Callers**: `EnglishTextAnalyzer`, and the tests.
