# SpeechTextEn System Design

This document provides the system-level design for the SpeechTextEn library.

![SpeechTextEn Structure](SpeechTextEnView.svg)

## Architecture

SpeechTextEn is a .NET library, intended to be published as the `DemaConsulting.Speech.Text.En` NuGet
package (it is not yet published; see _Release Gate_), that
supplies an English implementation of the Speech library's `ITextAnalyzer` seam. A model package returns
an `EnglishTextAnalyzer` from `ISynthesisModel.TextAnalyzer`; the Speech library then runs the analyzer
once per speech request over the whole request text and delivers the result to an annotation-aware
synthesis backend as language-neutral annotations on each speech segment. The system is a preview: this
release provides part-of-speech annotation, alignment-preserving normalization of non-numeric English text
(abbreviations, symbols, apostrophes, acronyms and initialisms) and authoritative sentence boundaries, and
later releases are planned to add numeric normalization, pronunciation readings and host overrides on top of
the same two subsystems.

SpeechTextEn is a sibling system to Speech rather than a subsystem of it, for the same reasons the model
packages are: it is a separately built, separately packaged software item with its own dependency, and
the Speech library must never depend on it. It uses the Speech library only through public API (the
`ITextAnalyzer`, `TextAnalysisResult`, `SegmentAnnotation` and `TextAnalysisConventions` types), and the
package's project file has one project reference, to `DemaConsulting.Speech`, and no reference to ONNX
Runtime or any inference package, so any model package can use it.

The library consists of two subsystems:

- **AnalysisSubsystem**: the `EnglishTextAnalyzer` analyzer (with its `EnglishTextAnalysis`,
  `AnalyzedToken` and `AnalyzedNormalization` result types), its `EnglishTextAnalyzerOptions`, the
  `TextEnAnnotations` annotation vocabulary and typed readers, the internal `PartOfSpeechAdapter` that
  isolates the part-of-speech package, and the internal `AnalysisDiagnostics` reporter - see _SpeechTextEn
  AnalysisSubsystem Design_
- **NormalizationSubsystem**: the alignment model (`NormalizationAlignment.cs`, which also declares the
  public `NormalizationKind` and `CliticKind` enumerations), the internal `TextNormalizer` that decides
  which spans are spoken differently, the internal `AbbreviationResolver` with its embedded abbreviation
  lexicon, and the internal `AcronymRecognizer` with its embedded initialism lexicon - see _SpeechTextEn
  NormalizationSubsystem Design_

The dependency between the subsystems is one way: the AnalysisSubsystem uses the NormalizationSubsystem,
which knows nothing of the analyzer, the tagger or the diagnostics sink. The NormalizationSubsystem does not
use the part-of-speech package in production code at all; it receives token offsets from the analyzer as
plain values (only its tests feed it the real tokenizer's tokens). Both subsystems are small enough that
neither needs nested subsystems of its own.

## External Interfaces

| Interface | Direction | Format | Constraints |
| --- | --- | --- | --- |
| `EnglishTextAnalyzer` (`ITextAnalyzer.Analyze`) | Inbound | Public method call | Thread-safe; honors cancellation; throws only for a null argument or cancellation |
| `EnglishTextAnalyzer.AnalyzeDetailed` | Inbound | Public method call | Result carries words, normalizations and clitic roles; local use only |
| `EnglishTextAnalyzerOptions` | Inbound | Public record | Validated on initialization; default selects part of speech and normalization |
| `TextEnAnnotations` | Inbound | Public constants and readers | Closed kind vocabulary; ignore unknown kinds; `en.norm` follows the _Alignment Contract_ |
| `NormalizationKind`, `CliticKind` | Inbound | Public enumerations | Closed vocabularies reported by `AnalyzeDetailed` only |
| `ITextAnalyzer`, `TextAnalysisResult`, `SegmentAnnotation` | Outbound | Public Speech types | Consumed; offsets are UTF-16 indices into the exact input |
| `ISpeechDiagnostics.Report` | Outbound | Method call | Counts only; a failing sink is contained |
| `DemaConsulting.PartOfSpeech` tokenizer and tagger | Outbound | Method call | Internal to `PartOfSpeechAdapter` |
| Embedded lexicons `abbreviations.tsv`, `acronyms.tsv` | Internal | Embedded resources, tab-separated text | Read once, lazily, by the NormalizationSubsystem; a malformed table is a defective build |

## Dependencies

SpeechTextEn has one project dependency, the Speech library, and one NuGet dependency (plus its own two
embedded lexicon tables, which are data written for this package, not a dependency):

- **DemaConsulting.PartOfSpeech** supplies the English tokenizer, the sentence splitter and the
  Universal Dependencies tagger with its embedded lexicon and weights. It is an OTS item of this
  repository (see _OTS Integration Design_ and _PartOfSpeech Design_). Its use is confined to the internal
  `PartOfSpeechAdapter`, and no type of that package appears in any public signature of this library, so
  the package can be replaced or upgraded without changing this library's API

The tagging package is pre-release software and is referenced with an exact-version pin, so a restore can
never move to a different build and silently change the analysis. NuGet does not allow a stable package
to depend on a pre-release package (warning NU5104), so a stable release of this package is blocked until
`DemaConsulting.PartOfSpeech` publishes a stable version; until then this package is released only as a
pre-release, in the same release line as the rest of the repository. In addition, the package is not
published at all until the text-retention defect described in _Known Limitations_ is fixed (see _Release
Gate_).

## Risk Control Measures

N/A - SpeechTextEn provides no safety-critical functionality requiring risk control measures
(IEC 62304 §5.3.3). It annotates text for speech synthesis and has no clinical or safety role.

## Data Flow

**Analysis path (one speech request):**

1. **Input**: Speech's `SynthesisSession` has normalized the request text, parsed the audio tags and built
   the analysis text, in which every recognized tag is exactly one `U+FFFC` placeholder; it calls
   `EnglishTextAnalyzer.Analyze` on a thread-pool thread with the operation's cancellation token
2. **Neutralization**: each placeholder is replaced by a space of the same length, for tokenizing and
   tagging only, so every offset in the neutralized text is also an offset in the original
3. **Splitting**: `PartOfSpeechAdapter` splits the neutralized text into sentences and tokens (needing no
   tagger) with token offsets that index the text
4. **Tagging**: when part-of-speech annotation is selected, the adapter tags each sentence, checking the
   cancellation token before each one, after the tagger is obtained and after each sentence is tagged; the
   shared tagger is obtained on the first sentence that is tagged. Tagging runs on the raw tokens, never on
   normalized text (see _Why tagging runs on raw tokens_ below)
5. **Normalization**: when normalization is selected, the analyzer flattens the tokenizer's sentences into
   one array of token offsets (each marked with whether it opens a tokenizer sentence) and hands it, with
   the neutralized text, to `TextNormalizer`. One linear pass applies the ordered rules and returns the
   changed spans (each with its spoken replacement and kind), the sentence-break edits (breaks to force and
   to suppress), the ambiguity count and, only for `AnalyzeDetailed`, the clitic roles. It needs no tagger
   and does not look at the tags
6. **Result building**: the analyzer emits one `en.upos` annotation per word token (tokens tagged `PUNCT`
   or `SYM`, and tokens without a letter or digit, are skipped) followed by the `en.norm` annotations of the spans that start inside that token,
   so annotations are in text order and, at an equal start, the part of speech precedes the normalization.
   Sentence breaks are the tokenizer's breaks (the first token of every sentence after the first)
   **reconciled** with the normalization edits: every tokenizer break and forced break, minus the suppressed
   ones. The cancellation token is checked periodically while the result is built
7. **Reporting**: unless the request was canceled at any earlier point (including during the last
   sentence), `AnalysisDiagnostics` reports one counts-only event to the optional sink
8. **Output**: a `TextAnalysisResult` whose annotations and non-null, reconciled sentence breaks index the
   original text; the session re-bases them onto its segments and chunks, and a backend that implements
   `IAnnotatedSynthesisBackend` receives them with each segment and applies the `en.norm` annotations as
   described in _Alignment Contract_

**Why reconciliation exists.** The tokenizer treats titles such as `Dr.` and `St.` as never ending a
sentence, so `I live on Main St. It is nice.` is one tokenizer sentence, and the Speech library's chunker,
given the analyzer's breaks, splits sentences only where the analyzer says. The normalizer knows how it read
each abbreviation: a period after an abbreviation read as the last word of its sentence (a suffix such as
`Street`) gets a forced break before the next capitalized word, and a period after one read as a prefix (a
title such as `Doctor`) never ends a sentence, so a tokenizer break after it is suppressed. A name suffix or
credential (`Jr.`, `Esq.`, `Ph.D.`, `M.D.`, `III`) directly followed by another one (`John Smith Jr. Ph.D.
spoke.`) is part of the same name and behaves like a prefix, so no break is forced between them and a
tokenizer break there is suppressed; a break before a genuine sentence starter (`I met John Smith Jr. He
left.`) is unaffected. Without the
reconciliation the chunker would either keep two sentences in one chunk or split inside `Dr. Smith`.

**Why tagging runs on raw tokens.** The tagger was trained on ordinary written text, and tagging the
normalized text gives worse tags and unknown words (`FBI` becomes the three tokens `F`, `B`, `I`; `etc.`
becomes the unknown word `cetera`). Normalization and tagging are therefore independent consumers of the
same tokenizer output: every `en.upos` annotation stays on its source token, normalization never needs the
tagger (so `Annotations = Normalization` never loads its 1.3 MB of data), and a later increment that wants
the spoken form as context can read it from the detailed analysis without changing this contract. For the
same reason the abbreviation and clitic context cues are lexical and orthographic only (capitalization,
digits, short word lists), never part-of-speech tags, so the result does not depend on the options.

## Alignment Contract

This section is normative. The `en.norm` annotations are a contract between the analyzer that produces them
and every backend that applies them, and the invariants below are enforced by the NormalizationSubsystem
(see _NormalizationAlignment Design_ for the enforcement point of each) and verified by its property tests.
The analyzed text is never rewritten: an `en.norm` annotation names a span of the source text and the exact
words that replace it when the text is spoken.

| # | Invariant |
| --- | --- |
| A1 | The spans of one analysis are ascending and disjoint (each starts at or after the end of the previous one) and lie within the text |
| A2 | Every span is non-empty, starts and ends on a non-whitespace character, never contains a tag placeholder and never cuts a surrogate pair; its replacement is never null |
| A3 | The spans and the untouched gaps between them tile the text exactly: no gap, no overlap, lengths summing to the text length; applying every span as its own source text returns the input unchanged |
| A4 | Nothing outside a span is changed, re-cased or re-punctuated; unchanged text is never annotated |
| A5 | A span is chunk-safe: it contains no whitespace and none of `,` `;` `:`, and it is at most one tokenizer token, or one token and its adjacent period token, so it always lies wholly inside one chunk of the Speech library |
| A6 | Separation: where a letter or digit of the source touches a span, or the previous span directly touches it and its replacement ends with a letter (`A™®B`), and the replacement starts (or ends) with a letter, the replacement carries one separating space on that side (`he/she` becomes the replacement ` slash ` for the `/`; `A™®B` gives `trademark` and ` registered `). Dropped and apostrophe replacements are never padded, and two adjacent replacements are separated by exactly one space (the later one pads its own left side, so no offset moves) |
| A7 | No silent drop: every source character is either copied verbatim (outside spans) or replaced by a non-empty replacement; the only empty replacements are `Dropped` spans over invisible format characters and bullets, and the only source periods that vanish are those consumed inside an abbreviation or initialism span |
| A8 | Break edits sit on token starts (non-whitespace, never a placeholder) strictly inside the text, ascending and unique; the reconciled sentence breaks keep the P2-1 break rules (ascending, unique, on word starts, non-null) |
| A9 | The analysis is deterministic and thread-safe: repeated and concurrent analysis of one text yields identical annotations and breaks; neither the analyzer nor the normalizer keeps per-request state |
| A10 | An `en.norm` value is non-empty except for a `Dropped` span, contains no tag placeholder and has no leading or trailing space except the A6 padding (a leading space is also the A6 separator after a directly adjacent replacement that ends with a letter) |

**Consumer algorithm.** To obtain the spoken text of a segment, a backend that reads `en.norm`:

1. collects the segment's annotations that `TextEnAnnotations.TryReadNormalized` accepts and sorts them by
   `Start` (they never overlap);
2. walks the sorted list, copying the segment text between the end of the previous span and the start of the
   next one unchanged (a gap);
3. at each span, appends the annotation's `Value` instead of the covered characters; a `Value` carries its
   own padding (A6), so nothing is inserted between the pieces, and an empty `Value` means speak nothing for
   that span;
4. copies the text after the last span.

Annotations of other kinds, notably `en.upos`, are independent of this and may overlap an `en.norm` span
(the part of speech of `Dr.` is attached to the token, the normalization to the same characters). A backend
ignores every kind it does not recognize. The library adds no public helper that applies the annotations, so
a Phase 3 backend can implement this ten-line algorithm itself.

**Double normalization.** A model that supplies an `EnglishTextAnalyzer` with normalization selected should
keep `ISpeechModel.NormalizeText` as the identity (the default): the Speech library applies that hook to the
request text before analysis, so a model that rewrote `Dr.` itself would hand the analyzer text in which
that abbreviation no longer exists, and the two normalizations would disagree about what is spoken. A model
that deliberately selects part of speech only may keep its own normalization hook.

## Design Constraints

- **Speech must never depend on SpeechTextEn**: the dependency direction is one way. Speech carries no
  reference to this package or to the part-of-speech package
- **Offsets are the contract**: all offsets are UTF-16 code unit indices into the exact analyzed text. The
  analyzer never rewrites, normalizes or re-indexes the text, so the placeholder neutralization is a
  same-length replacement, and normalization is expressed only as `en.norm` replacement annotations (see
  _Alignment Contract_). An annotation never covers a placeholder, never starts or ends on whitespace,
  and never cuts a surrogate pair, whatever the input
- **Placeholders are separators, not text**: the words on both sides of a tag are analyzed as one running
  text, so context such as the words around "[whispers]" is preserved (an abbreviation before a tag is
  still resolved from the name after it); a sentence break is placed at the next word, never at the
  placeholder
- **Diagnostics are counts only**: the library's diagnostics contract forbids synthesized text in any
  report. The analyzer reports sentence, token, annotation, unknown-word, normalization and ambiguous
  abbreviation counts built from fixed words and numbers, never a word or a spoken replacement, and
  detailed per-word data is available only through `AnalyzeDetailed`, which returns it to the caller
  instead of a sink
- **Linear time**: every normalization rule looks at a constant number of neighboring tokens, and the
  normalizer checks the cancellation token every 256 visited tokens, so the pass is linear in the text
  length, including for adversarial repetitions and for one huge token, and a canceled request stops
- **US English only**: the lexicons, the spelling of the spoken forms (`Miz`, `Mount`, `Avenue`) and the
  context cues are those of written US English; other English varieties and other languages are not
  handled and their text is passed through unchanged
- **First-use latency of the tagger**: the shared tagger inflates and validates about 1.3 MB of embedded
  data on first use, which takes noticeable time once per process. It is deliberately not loaded when an
  analyzer is constructed, when a text has no tokens, or when part-of-speech annotation is not selected,
  and no warm-up API is provided in this release; a host that wants the cost paid early can analyze one
  short text at start-up. The two embedded lexicons are small and are parsed once, on the first
  normalization, never at construction
- **Immutable and thread-safe**: an analyzer keeps no per-request state, copies its options at
  construction, keeps no reference to an analyzed text or to a result after the call returns, and may
  serve any number of concurrent requests; the normalizer and both lexicon units are immutable after
  loading. This statement covers this library's own state only; see _Known Limitations_ for the
  dependency's separate behavior
- **No inference dependency**: the library references no ONNX Runtime package and has no native assets
- **No command-line or demo integration** in this release
- **Compliance**: all functionality must be traceable to requirements

### Deferred Capabilities

The normalization in this release is deliberately limited to non-numeric text. Everything below is passed
through unchanged (identity, never annotated) and is documented here so a host does not assume it:

- **URLs, e-mail addresses, markup and Markdown, and the hyphen and dash policy**: deferred to a later small
  increment and recorded as a follow-up that needs a decision of the repository owner (a URL contains `:`,
  which the core chunker treats as a clause boundary, so it needs evidence about annotation loss first)
- **Numbers, dates, times, currency, percentages, ordinals, Roman numerals, units, and month and weekday
  abbreviations** (`Mar.`, `Sat.`, `5 ft.`): planned for the next increment, because they need the digits
  and the date context that this release does not interpret
- **Host overrides and the `en.spell` spell-out annotation**: planned for a later increment; the
  `en.spell`, `en.reading`, `en.reading-confidence` and `en.pron` kinds stay reserved and are never emitted
- **Plural initialisms** (`DVDs`) and acronyms spoken as words (`NASA`)

A known limit of the current rules, not a deferral: an initialism is spoken as upper-case letters
separated by spaces (`FBI` becomes `F B I`), and a later phoneme-based backend may read a lone `A` or `I`
as a word until the spell-out annotation exists.

### Known Limitations

- **Analyzed words can remain in the tagging package's memory (dependency bug; currently violates the
  core `ITextAnalyzer` contract)**: the core `ITextAnalyzer` contract (see _Speech SynthesisSubsystem
  TextAnalysis Design_) requires an analyzer to "not retain `text`". This library holds no reference to the
  text or to the results (a weak-reference test proves that), but the tagging package
  `DemaConsulting.PartOfSpeech` 0.1.0-beta.4 decodes tags through a decoder that keeps the characters of
  the tokens it processes in a thread-static working buffer that it never clears. Words analyzed on a
  thread therefore remain recoverable from the process memory (for example from a memory dump) after the
  request ends, so `EnglishTextAnalyzer` **currently does not meet that contract**. The buffer is private
  to the dependency and this library does not use reflection or any other workaround to clear it; the
  defect is tracked upstream as <https://github.com/demaconsulting/PartOfSpeech/issues/29>. Because
  documentation alone is not an adequate remedy for a contract violation, the package is **gated**: it is
  not packed, published or released (`IsPackable` is `false`; see _Release Gate_) until the dependency is
  fixed and the pin is moved to the fixed version. Every "no retained text" statement in this
  repository's requirements, tests and documentation for SpeechTextEn is scoped to this library's own
  state. A host that uses this library from source before then must account for this. The same limitation
  is recorded in `THIRD-PARTY-NOTICES.md`, _PartOfSpeech Design_ and the user guide's preview note

### Release Gate

`DemaConsulting.Speech.Text.En.csproj` sets `<IsPackable>false</IsPackable>` with a comment naming this
gate, so `dotnet pack` for the solution produces no package for this project (the build's pack
verification already skips projects that are not packable) and no Text.En package reaches the NuGet push in
the release workflow. The license-file requirement stays truthful while the package cannot be inspected: the
project-file test and the `TextEn_PackageLicenseFiles_Content` FileAssert test verify the project's packing
configuration and the content of the files in the repository, and the gate itself is checked by
`SpeechTextEn_ProjectFile_Packaging_IsGatedUntilTaggerStopsRetainingText`. The license files stay in the
repository.

Before `IsPackable` is re-enabled, all of these steps are required, in one change:

1. **Bump the pin**: move the exact `DemaConsulting.PartOfSpeech` version pin to a release that fixes
   upstream issue <https://github.com/demaconsulting/PartOfSpeech/issues/29>, and update the version
   statements in `THIRD-PARTY-NOTICES.md`, _PartOfSpeech Design_, _PartOfSpeech Verification_, the user
   guide and the README, and remove the Known Limitation above.
2. **Add a retention test**: add a test that proves no request text remains in the dependency's working
   state after an analysis: analyze a sentinel string of distinctive characters, then a short, different
   string, on one dedicated thread (the affected buffer is thread-static), and assert through the fixed
   dependency's public API or a documented test hook that no character of the sentinel remains. If the
   fixed dependency offers no public way to inspect that workspace, the verification approach is to cite
   the upstream fix's own regression test by version in _PartOfSpeech Verification_ and to state the
   remaining gap there for the repository owner's approval; reflection or a memory scan of private
   state is not an acceptable substitute.
3. **Restore the package assertions**: remove `<IsPackable>false</IsPackable>` and its comment, restore the
   nupkg-based FileAssert test (the license file and notices at the package root and the nuspec license
   reference) in place of `TextEn_PackageLicenseFiles_Content`, replace the gate test and requirement
   (`SpeechTextEn-Packaging-ReleaseGate`) with the retention test, and update the requirements that
   cite them.
4. **Update the user-facing notes**: replace the "not yet published to NuGet" preview notes in the README
   and the user guide.

### Package License Files

The package contains code under the MIT License and embeds two small lexicon tables of its own (a list of
written-English abbreviations with their spoken forms and a list of initialisms), which DEMA Consulting wrote
from scratch for this package under the same MIT License; no third-party list was copied and the package
embeds no third-party data. It therefore declares MIT only: it names a license file, `PACKAGE-LICENSE.md`,
that starts with the SPDX
line `SPDX-License-Identifier: MIT`, contains the MIT license text, describes the two lexicon tables, and
states that the required package
`DemaConsulting.PartOfSpeech` is a separate NuGet dependency, not bundled in this package, that carries
its own license files. The package also ships `THIRD-PARTY-NOTICES.md`, clearly scoped as a dependency
notice: it states that this package embeds no third-party data, describes that dependency's embedded data
(Open English WordNet, derived from Princeton
WordNet, under CC BY 4.0, and the Manually Annotated Sub-Corpus under CC BY 3.0 US) and reproduces its
attribution, license links, statement of changes and the Princeton WordNet notice verbatim, for the
reader's convenience. The composite license chain belongs to the dependency, not to this package, so it
is not claimed in this package's declaration. If a later increment embeds third-party data in this package
(for example trained weights), the declaration and the notices will be revisited. Both files are packed at
the package root and are packaging files, not software items.

### Platform Support

The library targets the same frameworks as the Speech library:

| Target Framework | Runtime / Environment |
| --- | --- |
| `net8.0` | .NET 8 LTS |
| `net9.0` | .NET 9 |
| `net10.0` | .NET 10 |
| `net11.0` | .NET 11 |

### Integration Patterns

- **NuGet Packaging**: intended to be published as a separate package that depends on the Speech package
  and the part-of-speech package; not yet published (see _Release Gate_)
- **Model-supplied analyzer**: a model package returns `new EnglishTextAnalyzer()` (or one built with
  options) from `ISynthesisModel.TextAnalyzer`; one instance can be shared by every engine of the model. A
  model that does so keeps `ISpeechModel.NormalizeText` as the identity (see _Alignment Contract_)
- **Annotation-aware backend**: a backend implements `IAnnotatedSynthesisBackend` and reads the
  annotations with `TextEnAnnotations.TryReadPartOfSpeech` and `TextEnAnnotations.TryReadNormalized`
  (applying the latter as described in _Alignment Contract_), ignoring kinds it does not recognize
