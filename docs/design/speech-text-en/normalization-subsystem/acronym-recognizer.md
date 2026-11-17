### AcronymRecognizer

**Purpose**: Decide whether one token is an acronym or initialism that is spoken letter by letter, and give
its spoken form. The recognition is case-sensitive on purpose: `US` is the country, but `us` and `Us` are the
pronoun, and `IT` is the department, but `It` is the pronoun. Acronyms that are read as words (`NASA`,
`NATO`) are deliberately not listed and, because they contain a vowel, are left as written.

**Data Model**:

*The lexicon* is the embedded resource `acronyms.tsv`: UTF-8, LF, tab-separated, written from scratch by DEMA
Consulting, with the same comment, blank-line and header conventions as the abbreviation lexicon. Each data
line has three columns:

| Column | Meaning |
| --- | --- |
| `form` | The all-capital token exactly as written: one or more upper-case ASCII letters |
| `spoken` | The letters spoken one by one, separated by single spaces (`F B I`); never empty |
| `ambiguous` | `yes` when the form is also an ordinary English word in capitals (`US`, `IT`, `AM`): it is then left alone inside an all-capital sentence; otherwise `no` |

The listed forms are US, USA, UK, EU, UN, UAE, FBI, CIA, NSA, IRS, DOJ, TV, PC, CEO, CFO, CTO, HR, PR, ID, IT,
AI, ML, API, URL, HTML, CSS, SQL, PDF, CPU, GPU, USB, LED, LCD, DVD, CD, GPS, ATM, SUV, DNA, RNA, HIV, MRI,
AM, PM, BBC, CNN, NBC, NPR, HBO, MBA, LLC, VIP, FAQ, ASAP, DIY and FYI; `US`, `IT` and `AM` are marked
ambiguous. The recognizer holds the entries in a dictionary from form to the spoken text and the ambiguity
flag. `AcronymMatch(Spoken, TokenEndsWithPeriod)` is the result: the letters to speak, and whether the token
itself ends with the period of a dotted form.

Fixed limits (code constants): an initialism has at least two and at most six upper-case letters, a
rule-spelled one at most five, and a group beside an ampersand at most four.

**Key Methods**:

- **FromTsv(reader)**: parses a lexicon. It rejects, with an `InvalidDataException` that names the one-based
  line number, a wrong column count, a form that is empty or not entirely upper-case ASCII letters, an empty
  spoken text or one with surrounding whitespace, an ambiguity column other than `yes` or `no`, and a
  duplicate form. A null reader throws `ArgumentNullException`
- **Default**: the recognizer over the embedded lexicon, created once on first use under `Lazy<T>`
  (`ExecutionAndPublication`)
- **TryRecognize(token, sentenceIsAllCapitals, out match)**: applies the rules below in order and returns
  the first that matches. `sentenceIsAllCapitals` is the shouting guard computed by the caller for the
  token's tokenizer sentence

**Rule order for one token**:

1. **Dotted letters** (`U.S.`, `U.S.A.`, `J.R.R.`, `p.m.`, and `U.S` without its final period): ASCII letters
   at even offsets and periods at odd offsets, at least two letters, and no other character. The spoken form
   upper-cases every letter and separates them with spaces (`U.S.A.` becomes `U S A`);
   `TokenEndsWithPeriod` says whether the final period is part of the token. The caller treats the form
   without its own period as an initialism only when the tokenizer split the period off as an adjacent token.
   Lexicon abbreviations such as `e.g.`, `i.e.`, `a.k.a.` and `Ph.D.` are matched by the abbreviation
   resolver first, because it runs before this unit
2. **Ampersand initialisms** (`AT&T`, `R&D`, `Q&A`): groups of one to four capital ASCII letters joined by
   `&` (at least two groups), optionally followed by a possessive `'s` in any apostrophe style. The groups are
   spelled and joined by `and`: `AT&T` becomes `A T and T`, and `AT&T's` becomes `A T and T's`
3. **Listed forms**: an upper-case token of two to six capital ASCII letters that is in the lexicon is spoken
   as listed, unless it is marked ambiguous and the sentence is all capitals
4. **Rule-spelled forms**: an unlisted token of two to five capital ASCII letters is spelled letter by letter
   when the sentence is not all capitals, it contains none of A, E, I, O, U and it is not shaped like a Roman
   numeral (only the letters I, V, X, L, C, D, M). `NYC` and `HTTP` are spelled; `NASA`, `NATO`, a Roman-numeral
   shape such as `MMMM` and a single letter are not
5. Anything else is not an initialism

**Error Handling**: `FromTsv` throws `InvalidDataException` naming the line for a malformed table and
`ArgumentNullException` for a null reader. `Default` throws `InvalidDataException` if the embedded resource is
missing or malformed, a defective build that is deterministic and therefore cached by the `Lazy<T>`.
`TryRecognize` never throws: a token of any content, including an empty one or one with characters outside
the ASCII letters, simply is not recognized.

**Dependencies**: the framework only (resource loading and string handling).

**Callers**: `TextNormalizer` (rule 4), and the tests.
