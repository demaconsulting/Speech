### SpanValidator

**Purpose**: Decide whether the span of one labeled row points at one whole word that the row's reading can take. It is
a pure function, shared by every check that needs the rule, so the definition of a valid span exists in one place.

**Data Model**: `SpanValidator` is a static class with no state. Offsets are UTF-16 code units of the .NET string, the
start is inclusive and the end is exclusive. The listed forms of the row's reading are passed in as a set of lowercase
strings (from `TextEnDataSet.FormsOf`), so the validator knows no file and no reading identifier.

**Key Methods**:

- **Validate(sentence, start, end, listedForms)**: returns null when the span is valid and otherwise a short error
  message, the first rule that fails giving the message. In order: the span is invalid when `0 <= start < end <= length`
  does not hold; when `start` or `end` lies between a high and a low surrogate; when the text `sentence[start..end]`
  lowercased with `ToLowerInvariant()` is not a listed form; when the character before `start` (if any) is a letter or a
  number; or when the character at `end` (if any) is a letter or a number. The neighbors are judged by Unicode scalars,
  not by code units: the scalar that ends at `start` and the scalar that starts at `end` are decoded with `Rune`, so a
  letter outside the Basic Multilingual Plane is one neighbor, and a lone surrogate is not a letter.
- **IsLetterOrNumber(rune)**: true for a letter category (uppercase, lowercase, title case, modifier or other letter) or
  a number category (decimal digit, letter number or other number); this includes a digit, a non-ASCII letter and a
  superscript digit, so the rule equals the letter-or-digit rule of the one-off prototype that built the data.
  Punctuation, symbols, spaces and the ends of the sentence are valid neighbors.

**Error Handling**: the method never throws for the content of a row; every defect is a returned message, which the
caller turns into a finding with file and line. A null argument is a programming error and throws
`ArgumentNullException`.

**Dependencies**: the .NET base class library (`System.Text.Rune`, `System.Globalization.UnicodeCategory`) only.

**Callers**: `SchemaChecker` and the unit tests.
