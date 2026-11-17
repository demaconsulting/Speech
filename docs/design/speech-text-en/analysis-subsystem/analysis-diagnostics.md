### AnalysisDiagnostics

**Purpose**: Report what an analysis did, and nothing else, to the optional host-supplied diagnostics sink.
The library's diagnostics contract forbids synthesized text in any report, so this unit can tell a host how
much work was done but never what was read.

**Data Model**: `AnalysisDiagnostics` is a sealed class holding one nullable `ISpeechDiagnostics`. `AnalysisCounts` is
an internal read-only record struct of nine counts: sentences (the reconciled sentence breaks plus one when the text has
any token), tokens, annotations (of all kinds), unknown words (word tokens that are not punctuation, symbols or numerals
and that the tagger's lexicon does not know; zero when the text was not tagged), normalizations (the number of
replacements, `en.norm` and `en.spell` together; zero when normalization was not selected), ambiguous readings
(abbreviation resolutions and numeric readings, such as a bare year or a month-first date, that fell back to a
documented default or chose between competing cues; zero when normalization was not selected), numeric fallbacks
(numeric-looking expressions left as written or read digit by digit because of a documented limit or an unsupported
numeric shape; zero when normalization was not selected), overrides applied (user pronunciation overrides that took
effect on a word) and overrides ignored (overrides that matched a word that an earlier multi-token match had already
consumed); the last two are appended with a default of zero and are zero without a dictionary. The category is the
constant `TextEn.Analysis`.

**Key Methods**:

- **FormatMessage(counts)**: builds the message `Analyzed one text: {n} sentences, {n} tokens, {n}
  annotations, {n} unknown words, {n} normalizations, {n} ambiguous readings, {n} numeric fallbacks, {n}
  overrides applied, {n} overrides ignored.` with
  invariant-culture numbers. The message is built only from fixed words and the nine numbers, so no word,
  token, spoken replacement, annotation value or dictionary content (key, payload, scheme or entry count) can
  be in it - a leak is impossible by construction rather
  than merely avoided. (The count that earlier read "ambiguous abbreviations" is generalized to "ambiguous
  readings"; the message is informational and a host should not parse it)
- **ReportRequest(counts)**: does nothing without a sink; otherwise sends one Info-level event in the
  `TextEn.Analysis` category. One report is made per completed request; a canceled or failed request
  reports nothing

**Error Handling**: any exception from the sink is caught and discarded here, because a diagnostics sink is
an observer whose contract forbids throwing, and a fault in it must never interrupt the speech request it
observes. This unit throws nothing else.

**Dependencies**: the Speech library's `ISpeechDiagnostics` and `SpeechDiagnosticLevel`.

**Callers**: `EnglishTextAnalyzer`.
