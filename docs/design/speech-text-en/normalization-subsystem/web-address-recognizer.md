### WebAddressRecognizer

**Purpose**: Decide whether one token is a web address (a URL, an e-mail address or a bare domain name) and say
how each piece of it is spoken, so that `john.smith@example.com` is read `john dot smith at example dot com`
instead of being sent to the phonemizer as one unpronounceable word. The tokenizer keeps a whole address in one
token, so the unit looks at one token and never at its neighbors. The words of the address (`example`, `docs`)
are left as written for the phonemizer; only the pieces that are not words get a span.

**Data Model**: `Recognize(token, tokenStart)` returns the spans (`NumericSpan`: start, length, spoken text,
kind) of one token in text order, or `null` when the token is not an address. Every span indexes the text
(`tokenStart` plus the offset in the token). These spans occur:

- **Scheme letters** (`Initialism`): `https`, `http` or `ftp` in any letter case before `://`, spelled with
  single spaces (`H T T P S`)
- **Dropped** (empty replacement): the two slashes after the scheme, the only characters that are removed
- **Leading `www`** (`Initialism`, `W W W`) after the scheme or at the start of the token
- **Separators** (`Symbol`): one span per character, `.` dot, `/` slash, `@` at, `-` dash, `_` underscore,
  `?` question mark, `=` equals, `&` and, `#` hash, `+` plus, `%` percent, `~` tilde

A colon is never inside a span, so the colon after the scheme and the colon before a port stay as the pause
marks the sentence chunker already knows.

Fixed limits (code constants): a token has at least 6 and at most 512 characters; the schemes are `https`,
`http` and `ftp`; the top-level domains that make a bare dotted name a domain are `com`, `net`, `org`, `edu`,
`gov`, `io`, `dev`, `app`, `info`, `biz`, `ai` and `uk`.

**Key Methods**:

- **Recognize(token, tokenStart)**: returns `null` for a token outside the length limits or without a `.` or
  `@`. Otherwise it accepts a scheme URL (`scheme://` followed by a letter or digit), a leading `www.`
  followed by a letter or digit, an e-mail address, or a bare domain, and then produces one span per separator
  after the scheme and `www`
- **E-mail address**: a local part of ASCII letters, digits and `. _ % + -` (not starting or ending with a dot,
  no two dots in a row), exactly one `@`, and a domain of two or more labels whose last label is two or more
  letters (any such label, not only the known list)
- **Bare domain**: two or more labels of ASCII letters, digits and hyphens (a label neither starts nor ends
  with a hyphen) optionally followed by a path, query or fragment; the last label must be one of the known
  top-level domains in any case. A file name such as `report.pdf` and a name such as `Node.js` are therefore
  never read as addresses

**Error Handling**: none. A token of any content, including an empty one, is simply not recognized. The scan
reads each character a constant number of times, so hostile input cannot make the work grow beyond the token
length limit.

**Dependencies**: `NumericSpan` and `NormalizationKind` of the same subsystem, and the framework.

**Callers**: `TextNormalizer` (rule 1b, which runs before the clitic, abbreviation and acronym rules), and the
tests.
