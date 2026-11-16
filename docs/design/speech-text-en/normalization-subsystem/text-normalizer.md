### TextNormalizer

**Purpose**: Decide, token by token, which spans of an English text are spoken differently from how they are
written, and record the decisions as an `AlignedText`. The unit owns every decision about what a span covers,
whether the period of an abbreviation is consumed or kept, which sentence breaks change, and the symbol, mark,
clitic, apostrophe and dropped-character tables. It never rewrites the text and never consults the tagger.

**Data Model**: `TextNormalizer` is a sealed class holding the `AbbreviationResolver`, the
`AcronymRecognizer` and an optional token-visited observer (a test seam; production passes null). It keeps no
per-request state. The static `Default` instance is created once, on first use, over the default lexicons.
One pass runs in a private `ref struct` named `Pass` that holds the text, the token array, the builder, the
cancellation token, the index of the first token of the current tokenizer sentence, the cached
"sentence is all capitals" decision (not yet computed, no, or yes) and the visited-token counter; it lives on
the caller's stack. The constant `CancellationCheckInterval` is 256 and equals the part-of-speech adapter's.
The word sets used by the clitic classification are code constants listed below.

**Rule order.** For each token in order, the first rule that matches decides, and a multi-token span consumes
its tokens (the next token processed is the one after it):

| Rule | Condition | Result |
| --- | --- | --- |
| 1. Dropped | The token contains an invisible format character, or is a standalone bullet | One `Dropped` span (empty replacement) per maximal run of invisible characters, or for the whole bullet; no other rule looks at that token |
| 2. Clitic | The token is `n't`, `'re`, `'ve`, `'ll`, `'m`, `'s` or `'d` (any apostrophe style, any case) | A typographic apostrophe becomes an `Apostrophe` span with the replacement `'`; when roles are requested, the role is recorded |
| 3. Abbreviation | `AbbreviationResolver.TryResolve` accepts the token (alone, with its adjacent period token, or as an initial) | The period and sentence rules below decide the span, its period and the break edits |
| 4. Acronym or initialism | `AcronymRecognizer.TryRecognize` accepts the token | A single `Initialism` span; a dotted form uses the period and sentence rules as class `Free` |
| 5. Symbol or mark | A standalone operator or mark, or a mark, `&` or `/` inside a word (tables below) | `Symbol` spans |
| 6. Elision apostrophe | A typographic apostrophe between two letters, or leading `'tis`, `'twas`, `'til`, `'cause`, `'em` | `Apostrophe` spans |
| 7. Identity | Anything else | Nothing is recorded: digits and every deferred capability pass through |

**Period and sentence rules.** They apply when the candidate has a period, either inside the token (`Dr.`,
`etc.`, `U.S.`) or as a separate adjacent token (`Ave` and `.`, `p.m` and `.`). Let `last` be the period
token (or the token itself), `following` the first token after `last` that is not a closing quote or bracket,
and `endsSentence` true when there is no `following` token or the tokenizer starts a sentence at
`following` (or at a closing mark before it).

| Resolved class | Break edit | The period |
| --- | --- | --- |
| `Prefix` (a title, `Saint`, `Doctor`, `Number`, an initial), or a `Suffix`, `Word` or `Free` form that **continues a name** (see below) | If `endsSentence` and the tokenizer has a break there: **suppress** it (a title never ends a sentence) | Consumed: the span includes it |
| `Suffix` or `Word` | If not `endsSentence` and `following` starts with an upper-case letter: **force** a break at `following`, and `endsSentence` becomes true (a lower-case or digit next word leaves the sentence running) | Kept as the sentence terminator, outside the span, if `endsSentence`; otherwise consumed |
| `Free` (`etc.`, `e.g.`, `U.S.`) | None (the tokenizer already decides) | Kept outside the span if `endsSentence`; otherwise consumed |

For the `Word` reading (`No.` the word) no span is emitted: only the break edit is recorded. **Continuing a
name.** A candidate whose class is not `Prefix` *continues a name* when `following` is the very next token
after `last` (no closing mark between) and both the candidate token and `following` are listed name
suffixes or credentials (`AbbreviationResolver.IsNameContinuation`: `Jr`, `Sr`, `Esq`, `II`, `III`, `IV`,
`Ph.D`, `PhD`, `M.D`, `D.D.S`, `D.M.D`, `D.V.M`, `D.O`, `J.D`, `R.N`, `C.P.A`, `MBA`, `CPA`, with or without
the final period). Such a pair (`John Smith Jr. Ph.D. spoke.`, `Ann Lee Esq. Ph.D. M.D. left.`) belongs to
one name, so the candidate is handled exactly like a `Prefix`: no break is forced, the tokenizer's break
after it (the tokenizer itself breaks after `Esq.` and `Ph.D.` before another capitalized token) is
suppressed, and the period is consumed. Both tokens must be listed, so `Maple Ave. Ph.D.` and `Acme Inc.
Ph.D. here.` (address and business suffixes) keep their break, and a following word that is not a listed
credential, in particular a sentence starter such as `He`, `Then` or `I`, still gets its forced break
(`I met John Smith Jr. He left.`). The check is O(1) per abbreviation, so the pass stays linear.
"Consumed" means the
span includes the period (the whole token, or both tokens, contiguous). "Kept" means the span ends one
character earlier (`St` of `St.`, or only the word token) and the period stays identity text, so a backend
that applies the replacement keeps the sentence terminator and never invents one in mid-sentence (`Main
Street. It is nice.` versus `Doctor Smith`). A resolution that used the default or chose between both cues
is counted once as ambiguous, whichever class it ends up with. Examples: `I live on Main St. It is nice.`
annotates `St` as `Street` and forces a break before `It`; `Sen. Smith spoke.` consumes the period (the tokenizer splits `Sen` from its period
and may break before `Smith`, which is suppressed because a title never ends a sentence);
`pens, etc. and more` consumes the period of `etc.`; `Meet at 5 p.m. Then sleep.` annotates `p.m` (the period
kept), with the digit left for a later increment.

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
replacements `trademark` and ` registered ` on the two source characters).

**Inside a word** (class `Symbol`, the span is the single character): an `&` between two letters is spoken
` and ` (`Johnson&Johnson`), and the only `/` of a token that is two or more ASCII letters, a slash and two or
more ASCII letters is spoken ` slash ` (`he/she`). `km/h`, `a/b`, `24/7`, `://` and `www.` forms are left
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
- **Normalize(text, tokens, classifyClitics, cancellationToken)**: checks the arguments, checks the token,
  verifies that every token is non-empty, ascending, non-overlapping and inside the text, then runs one
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
  and no offset moves; `Dropped` and `Apostrophe` replacements are never padded

**Error Handling**: a null text, null collaborator or malformed token list throws `ArgumentNullException` or
`ArgumentException` before any rule runs; a canceled token throws `OperationCanceledException` at the next
check, and nothing partial is returned. An `ArgumentException` from `AlignedTextBuilder.Add` means a rule
broke the alignment contract and propagates unchanged: it is a defect of this unit, not of any input, and
the property tests are there to prove it never happens. A malformed embedded lexicon (a defective build)
propagates from `Default` as an `InvalidDataException`. Input text can never cause any other failure, because
every neighbor access is bounds-checked and every span is validated.

**Dependencies**: `AbbreviationResolver`, `AcronymRecognizer` and the *NormalizationAlignment* unit (the
builder, tokens, spans and enumerations). No other unit and no OTS item.

**Callers**: `EnglishTextAnalyzer`, and the tests.
