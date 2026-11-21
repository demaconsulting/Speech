### GutenbergBody

**Purpose**: Extract the body of a downloaded plain-text e-book and normalize it to the exact bytes that are pinned,
archived and tokenized. It is a pure function of the downloaded bytes, so the same download gives the same pin on every
platform.

**Data Model**:

- `GutenbergBodyText`: the normalized text, its UTF-8 bytes, their lowercase hexadecimal SHA-256 (the `body_sha256`
  pin), and the numbers of credit paragraphs and archive-name paragraphs that were dropped
- `CreditWindowLines` (40): the number of body lines at the start in which credit paragraphs are dropped

**Key Methods**:

- **Extract(downloaded)**:
  1. decodes the bytes as strict UTF-8, without a byte order mark, and turns CRLF and CR into LF;
  2. takes the lines strictly between the first line matching `^\*\*\* ?START OF (THE|THIS) PROJECT GUTENBERG E-?BOOK`
     and the first later line matching the same pattern with `END` (both case-insensitive, invariant culture);
  3. splits the body into paragraphs (maximal runs of lines that are not blank; a blank line holds only spaces and
     tabs) and drops a paragraph that starts in the first 40 body lines with `Produced by`, `E-text prepared by` or
     `Transcriber's Note` (either apostrophe), and any paragraph whose lines, joined by spaces, contain the archive's
     name (case-insensitive), counting each kind;
  4. removes every underscore (italic markup), trims trailing spaces and tabs, collapses runs of blank lines to one,
     trims blank lines at both ends and ends the text with one LF;
  5. returns the text, its bytes and their SHA-256. No Unicode normalization is applied, because the normalization
     tables of ICU and NLS may differ.
- **Sha256Hex(bytes)**: the lowercase hexadecimal SHA-256 used for every pin of the subsystem

**Error Handling**: a null argument throws `ArgumentNullException`. Bytes that are not UTF-8, a missing start or end
marker and an empty body throw `InvalidDataException`; the cache reports the book as failed.

**Dependencies**: the .NET base class library only (UTF-8 decoding, regular expressions, SHA-256).

**Callers**: `ClusterCorpusCache` (bodies and pins), the other units for `Sha256Hex`, and the unit tests.
