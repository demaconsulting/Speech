### AbbreviationResolver

**Purpose**: Recognize the abbreviations of the embedded lexicon and, for the few that have more than one
reading (`St.`, `Dr.`, `Sr.`, `No.`), choose the reading from the neighboring words alone. The unit answers
one question for one candidate token: is this a lexicon abbreviation, and which reading applies. It decides
nothing about spans, periods or sentence breaks, which belong to `TextNormalizer`.

**Data Model**:

*The lexicon* is the embedded resource `abbreviations.tsv`: UTF-8, LF, tab-separated, written from scratch by
DEMA Consulting. Blank lines and lines that start with `#` are ignored; the first remaining line is the
header; every other line is one reading of one written form, in six columns:

- `form`: The token exactly as written, matched case-sensitively; a trailing `.` is part of the form
- `spoken`: The words spoken instead; for class `word`, the written word itself (it is never applied)
- `class`: `prefix` (precedes a name or number), `suffix` (follows a name or street), `free` (either way) or `word` (the
  ordinary word followed by a real period)
- `default`: `yes` on exactly one row per form: the reading used when the context is inconclusive
- `guard`: `-` for none, or conditions joined by `+`: `next-cap` (the next token starts with an upper-case letter),
  `next-digit` (the next token starts with a digit), `prev-not-number` (the previous token does not start with a digit),
  `next-date` (the next token starts with an ASCII digit or is a month name or abbreviation) and `prev-number` (the
  previous token starts with an ASCII digit)
- `note`: Free text

Several rows with the same `form` are alternative readings. `Count` is the number of rows (alternatives
count separately). The loaded rows are held in a dictionary from form to an array of rows; the length of the
longest form is kept so a longer token is rejected without a lookup.

The lexicon covers these groups (US English spoken forms):

- **Titles and ranks**
  - Forms (spoken form): `Mr.` Mister, `Mrs.` Missus, `Ms.` Miz, `Messrs.` Messieurs, `Mmes.` Mesdames, `Prof.`
    Professor, `Rev.` Reverend, `Gen.` General, `Col.` Colonel, `Capt.` Captain, `Lt.` Lieutenant, `Sgt.` Sergeant,
    `Cpl.` Corporal, `Maj.` Major, `Cmdr.` Commander, `Adm.` Admiral, `Sen.` Senator, `Gov.` Governor, `Pres.`
    President, `Supt.` Superintendent, `Mt.` Mount
  - Class and guard: prefix, no guard
- **Titles without a period**
  - Forms (spoken form): `Mr`, `Mrs`, `Ms`, `Messrs`, `Prof` (same spoken forms)
  - Class and guard: prefix, `next-cap`
- **Guarded**
  - Forms (spoken form): `Ft.` Fort
  - Class and guard: prefix, `next-cap+prev-not-number` (so `5 ft.` is never read as Fort; the numeric rule reads it as
    the unit `feet`, and `5 Ft. Worth` is read as `five feet. Worth` because the number before it wins)
- **References**
  - Forms (spoken form): `Fig.` Figure, `Vol.` Volume
  - Class and guard: prefix, `next-digit` (never ends a sentence)
- **Name suffixes**
  - Forms (spoken form): `Jr.` Junior, `Esq.` Esquire
  - Class and guard: suffix
- **Address suffixes**
  - Forms (spoken form): `Ave.` Avenue, `Blvd.` Boulevard, `Rd.` Road, `Ln.` Lane, `Ct.` Court, `Pl.` Place, `Sq.`
    Square, `Hwy.` Highway, `Pkwy.` Parkway, `Apt.` Apartment, `Ste.` Suite, `Bldg.` Building, `Dept.` Department
  - Class and guard: suffix
- **Business suffixes**
  - Forms (spoken form): `Inc.` Incorporated, `Ltd.` Limited, `Corp.` Corporation, `Co.` Company, `Bros.` Brothers
  - Class and guard: suffix
- **Ambiguous**
  - Forms (spoken form): `St.`: Saint (prefix) or Street (suffix, default); `Dr.`: Doctor (prefix, default) or Drive
    (suffix); `Sr.`: Sister (prefix) or Senior (suffix, default); `No.`: Number (prefix, `next-digit`) or `No` (word,
    default)
  - Class and guard: see the decision order below
- **Months with a period**
  - Forms (spoken form): `Jan.` January, `Feb.` February, `Apr.` April, `Jun.` June, `Jul.` July, `Aug.` August, `Sep.`
    and `Sept.` September, `Oct.` October, `Nov.` November, `Dec.` December
  - Class and guard: two rows per form: prefix with `next-date`; suffix, no guard, the default
- `Mar.`
  - Forms (spoken form): March (it collides with the verb `mar`)
  - Class and guard: prefix with `next-date`; suffix with `prev-number` as the default, so a bare `Mar.` has no reading
    and stays as written
- **Months without a period**
  - Forms (spoken form): `Jan`, `Feb`, `Mar`, `Apr`, `Jun`, `Jul`, `Aug`, `Sep`, `Sept`, `Oct`, `Nov`, `Dec` (`May` is a
    word and has no row)
  - Class and guard: one row each: prefix, `next-digit`, default (`Jan 5`, `Sept 11`)
- **Weekdays with a period**
  - Forms (spoken form): `Mon.`, `Tue.`, `Tues.`, `Wed.`, `Thu.`, `Thur.`, `Thurs.`, `Fri.`, `Sat.` (Monday to Saturday)
  - Class and guard: two rows per form: prefix with `next-date`; suffix, no guard, the default
- `Sun.`
  - Forms (spoken form): Sunday (it collides with the star and the newspaper)
  - Class and guard: prefix with `next-date`; suffix with `prev-number` as the default, so a bare `Sun.` stays as
    written
- **Latin and general**
  - Forms (spoken form): `vs.` versus, `etc.` et cetera, `e.g.` for example, `E.g.` For example, `i.e.` that is, `I.e.`
    That is, `a.k.a.` also known as, `viz.` namely, `approx.` approximately, `misc.` miscellaneous, `cf.` compare,
    `Ph.D.` P H D
  - Class and guard: free
- **Slash forms**
  - Forms (spoken form): `w/` with, `w/o` without, `N/A` and `n/a` not applicable, `c/o` care of, `and/or` and or
  - Class and guard: free

`Fig.` and `Vol.` are class `prefix` rather than `free` because the tokenizer splits `Fig` from its period
and breaks before the digit: a prefix period is consumed and that break suppressed, so `Fig. 3` is spoken
`Figure 3` in one sentence. The month and weekday rows follow the same idea. The tokenizer breaks after
`Jan.` and `Sat.` even in `Jan. 5, Feb. 3` and `Sat. 5 Jan. 2024 and Sun. Mar. 5`, so a month or weekday
abbreviation followed by a date (a digit or another month) is a prefix, which consumes the period and
suppresses the false break; elsewhere it is a suffix, which keeps the period and forces a break before a
capitalized next word (`He left Mon. He came Tue.` keeps both sentence ends) and consumes the period before
a lower-case word (`Then Fri. ended.` gives `Then Friday ended.`). A prefix reading whose guard passes
replaces the suffix alternative, so no cue analysis is needed and no month resolution is counted as
ambiguous. `est.`, `min.`, `max.`, `avg.`, `Rep.`, `Hon.`, `Fl.` and the full month and weekday names are
deliberately not listed (ambiguous, or dependent on a number context; `min.` and `ft.` after a number are
read by the numeric rule). `May` and the weekdays written without a period are never rows. Because the full
month names and `May` have no row, the break the tokenizer places after a period that follows one of them is
kept (`We finished in May. 5 people stayed.` has a sentence end after `May.`), and the numeric date context
ignores that period as well, so `5` there is a cardinal and not a day.

*The month vocabulary* is shared with the numeric rule: the `next-date` guard asks
`NumericExpressionRecognizer.IsMonthToken` whether a token is a month, so the lexicon and the numeric rule
agree on what a month is, and a test cross-checks the recognizer's month and weekday abbreviation lists
against the rows of the table. The two new guards test the **ASCII digits** only (a date is written with
`0-9`); the three older guards keep `char.IsDigit`, which is unchanged and documented.

*The sentence-starting words* (`SentenceStarters`, a code constant, matched exactly and case-sensitively) are
capitalized words that begin a sentence and are therefore no evidence of a name: The, A, An, He, She, It,
They, We, I, You, This, That, These, Those, There, Then, Here, His, Her, Our, Their, My, Your, In, On, At,
As, But, And, Or, So, If, When, While, After, Before, Because, However, Also, What, Who, Why, How, Where,
Please, Let, Do, Does, Did, Is, Are, Was, Were, Will, Would, Can, Could, Should, May, Might, Must, Not, No,
Yes, Now, Today, Tomorrow, Yesterday, Next. *The determiners* (matched case-insensitively) are: the, a, an,
my, our, your, his, her, their, this, that, these, those, any. *The name continuations* (`NameContinuations`,
a code constant, matched exactly and case-sensitively, without the final period) are the name suffixes and
credentials that continue a name: Jr, Sr, Esq, II, III, IV, Ph.D, PhD, M.D, D.D.S, D.M.D, D.V.M, D.O, J.D,
R.N, C.P.A, MBA, CPA. They are a code constant rather than a lexicon column because no existing lexicon class
separates them (`Jr.` and `Ave.` are both `suffix`, `Ph.D.` is `free`, `M.D.` is a rule-spelled initialism)
and a new column would change the documented file format for no gain.

*The match* `AbbreviationMatch(Spoken, Class, UsedDefault, IncludesPeriodToken)` carries the chosen spoken
form, the class of the chosen reading (`AbbreviationClass`: `Prefix`, `Suffix`, `Free`, `Word`), whether the
choice is counted as ambiguous, and whether the candidate is a word token plus its own adjacent period
token (the tokenizer emits `Ave` and `.` as two tokens but keeps `Dr.` whole).

**Key Methods**:

- **FromTsv(reader)**: parses a lexicon in the format above. It rejects, with an `InvalidDataException`
  that names the one-based line number, a wrong column count, an empty form or spoken text (or surrounding
  whitespace), an unknown class, a default column other than `yes` or `no`, an unknown guard, a duplicate
  reading (the same class and spoken text for a form) and a form that does not have exactly one default
  reading. A null reader throws `ArgumentNullException`
- **Default**: the resolver over the embedded lexicon, created once on first use under
  `Lazy<T>` (`ExecutionAndPublication`)
- **TryResolve(text, tokens, index, out match)**: tries the token plus its adjacent period token first
  (when the next token is a single `.` that touches it), then the token alone, then the single-letter
  initial rule. The lookup rejects, without allocating, a candidate longer than the longest form, one that
  does not start with a letter, and a lower-case candidate that has neither a period nor a slash (every
  lower-case form has one of them)
- **Guard filtering** (inside the lookup): each reading's guards are checked against the next token (after
  skipping closing tokens) and the previous token. A guarded reading that passes replaces the unguarded
  alternatives of the same form (this is how `No.` before a digit becomes `Number` and otherwise the word
  reading wins). No passing reading means no match; exactly one means that reading, not ambiguous. With
  several readings that are not a prefix and a suffix pair, the default reading is used and is not
  counted
- **Reading choice** (inside the lookup, for a prefix and suffix pair): see the cues and the decision order
  below
- **Initial rule**: a token that is exactly one upper-case letter and a period (`J.`), followed by a
  name-like token or another initial, is class `prefix` with the letter itself as the spoken form (`J.
  Smith` becomes `J Smith`). Before a sentence-starting word it is left alone (`Add vitamin C. The end.`
  keeps its text; the tokenizer itself does not break there, a documented limit)
- **IsNameContinuation(text, tokens, index)**: tells whether a token is one of the name continuations, with
  or without its final period. Only a token of two to six characters that starts with an upper-case letter
  is looked up, so the check is constant time and allocates only for such short capitalized tokens. It is
  used by `TextNormalizer` (a name suffix directly followed by another continuation continues a name, see
  *TextNormalizer Design*) and by the reading choice below

**The cues** are computed from at most three neighbor tokens and cross tokenizer sentence boundaries. `P` is
the previous token (a possessive clitic directly before the abbreviation is skipped, so `Patrick's St.`
looks at `Patrick`), `P2` the token before `P`, and `N` the first token after the abbreviation that is not
a closing quote or bracket (`) ] } " ' ” ’ »`). A tag placeholder is already a space, so context crosses it.

- **name-like**: starts with an upper-case letter and is not a sentence-starting word
- `prefixCue`: `N` exists, is name-like or is an initial such as `J.`, and is not a name continuation (so a
  credential after `Sr.` is not taken for the name of a Sister)
- `suffixCue`: `P` starts with a digit (a number or an ordinal), or `P` is name-like and either `P` is not
  the first token of its tokenizer sentence or there is no `prefixCue`
- `strongSuffix`: `P` starts with a digit, or `P` is name-like and `P2` starts with a digit (`5th St.`,
  `123 Main St.`)
- `strongPrefix`: there is no `P`, or `P` starts with a lower-case letter, or `P` is an opening quote or
  bracket (`( [ { " ' “ ‘ «`), or `P` is a determiner

**Decision order** for a reading pair (the first rule that applies wins):

| Step | Condition | Reading | Counted as ambiguous |
| --- | --- | --- | --- |
| 1 | `strongSuffix` | suffix | no |
| 2 | `strongPrefix` and `prefixCue` | prefix | no |
| 3 | `prefixCue` and not `suffixCue` | prefix | no |
| 4 | `suffixCue` and not `prefixCue` | suffix | no |
| 5 | both cues | prefix | yes |
| 6 | neither cue, and `strongPrefix` | prefix | no |
| 7 | neither cue | the row marked default | yes |

Worked examples (the golden file of the subsystem tests holds each of them):

| Text | Reading |
| --- | --- |
| `Dr. Smith lives here.` | Doctor (step 2) |
| `Maple Dr. He likes it.` | Drive (step 4) |
| `Main St. It is nice.` | Street (step 4) |
| `Visit St. Louis.` | Saint (step 3) |
| `5th St. Louis is ahead.` | Street (step 1) |
| `Go to 123 Elm Dr. Smith lives there.` | Drive (step 1) |
| `The Dr. said hello.` | Doctor (step 6, not counted) |
| `St. Patrick's St. is closed.` | Saint, then Street |
| `John Smith Sr. He left.` | Senior (step 4) |
| `Sr. Maria prayed.` | Sister (step 2) |
| `John Smith Sr. Ph.D. spoke.` / `Joe Smith Sr. Esq. arrived.` | Senior (step 4) |
| `Go see Jones St. Smith` | Saint, counted (step 5) |
| `They St. said so.` | Street by default, counted (step 7) |
| `No. 5 is here.` / `No. I don't know.` | Number / the word `No` |

**Text details** (Cues):

- `Dr. Smith lives here.`: no `P` (strong prefix), `N` Smith is a name
- `Maple Dr. He likes it.`: `P` Maple is a name and starts its sentence; `N` He is a sentence starter, so no `prefixCue`
- `Main St. It is nice.`: same shape
- `Visit St. Louis.`: `P` Visit is a name but `N` Louis is a name and `P` is first in its sentence, so only `prefixCue`
- `5th St. Louis is ahead.`: `P` starts with a digit
- `Go to 123 Elm Dr. Smith lives there.`: `P` Elm is a name and `P2` is a number
- `The Dr. said hello.`: `P` The is a determiner; `N` is lower-case; no cue
- `St. Patrick's St. is closed.`: first `St.`: no `P`, `N` Patrick; second: possessive skipped, `P` Patrick is a name,
  `N` is lower-case
- `John Smith Sr. He left.`: `P` Smith is a name that is not first; `N` He is a sentence starter
- `Sr. Maria prayed.`: no `P`; `N` Maria
- `John Smith Sr. Ph.D. spoke.` / `Joe Smith Sr. Esq. arrived.`: `N` is a name continuation, so no `prefixCue`; `P`
  Smith is a name
- `Go see Jones St. Smith`: both cues
- `They St. said so.`: `P` They is a sentence starter and not a determiner; no cue
- `No. 5 is here.` / `No. I don't know.`: the `Number` reading needs a digit next

Two cases are wrong by default and documented as such in the golden file: `Wall St. Journal` and `Elm St.
Louis is far` read `Saint` with an ambiguity count of zero, because the previous word starts its sentence and
the next one is a name, which is also what `Visit St. Louis` looks like.

**Error Handling**: `FromTsv` throws `InvalidDataException` naming the line for a malformed table and
`ArgumentNullException` for a null reader. `Default` throws `InvalidDataException` if the embedded resource
is missing or malformed; that is a defective build, deterministic, and cached by the `Lazy<T>`, so it
surfaces on every normalization rather than being retried. `TryResolve` never throws for any text: a
candidate that is not an abbreviation simply returns false.

**Dependencies**: the framework only (resource loading by reflection over the library assembly and string
handling). It reads token offsets as `NormalizationToken` values from the *NormalizationAlignment* unit.

**Callers**: `TextNormalizer` (rule 3), and the tests.
