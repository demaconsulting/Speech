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
release provides part-of-speech annotation and authoritative sentence boundaries, and later releases are
planned to add text normalization, pronunciation readings and host overrides on top of the same
subsystem.

SpeechTextEn is a sibling system to Speech rather than a subsystem of it, for the same reasons the model
packages are: it is a separately built, separately packaged software item with its own dependency, and
the Speech library must never depend on it. It uses the Speech library only through public API (the
`ITextAnalyzer`, `TextAnalysisResult`, `SegmentAnnotation` and `TextAnalysisConventions` types), and the
package's project file has one project reference, to `DemaConsulting.Speech`, and no reference to ONNX
Runtime or any inference package, so any model package can use it.

The library consists of one subsystem:

- **AnalysisSubsystem**: the `EnglishTextAnalyzer` analyzer (with its `EnglishTextAnalysis` and
  `AnalyzedToken` result types), its `EnglishTextAnalyzerOptions`, the `TextEnAnnotations` annotation
  vocabulary and typed reader, the internal `PartOfSpeechAdapter` that isolates the part-of-speech
  package, and the internal `AnalysisDiagnostics` reporter - see _SpeechTextEn AnalysisSubsystem Design_

## External Interfaces

| Interface | Direction | Format | Constraints |
| --- | --- | --- | --- |
| `EnglishTextAnalyzer` (`ITextAnalyzer.Analyze`) | Inbound | Public method call | Thread-safe; honors cancellation; throws only for a null argument or cancellation |
| `EnglishTextAnalyzer.AnalyzeDetailed` | Inbound | Public method call | Result carries words; local use only |
| `EnglishTextAnalyzerOptions` | Inbound | Public record | Validated on initialization |
| `TextEnAnnotations` | Inbound | Public constants and reader | Closed kind vocabulary; ignore unknown kinds |
| `ITextAnalyzer`, `TextAnalysisResult`, `SegmentAnnotation` | Outbound | Public Speech types | Consumed; offsets are UTF-16 indices into the exact input |
| `ISpeechDiagnostics.Report` | Outbound | Method call | Counts only; a failing sink is contained |
| `DemaConsulting.PartOfSpeech` tokenizer and tagger | Outbound | Method call | Internal to `PartOfSpeechAdapter` |

## Dependencies

SpeechTextEn has one project dependency, the Speech library, and one NuGet dependency:

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
   shared tagger is obtained on the first sentence that is tagged
5. **Result building**: the analyzer emits one `en.upos` annotation per word token (tokens tagged `PUNCT`
   or `SYM` are skipped) and one sentence break at the first token of every sentence after the first,
   checking the cancellation token periodically while it builds the result
6. **Reporting**: unless the request was canceled at any earlier point (including during the last
   sentence), `AnalysisDiagnostics` reports one counts-only event to the optional sink
7. **Output**: a `TextAnalysisResult` whose annotations and non-null sentence breaks index the original
   text; the session re-bases them onto its segments and chunks, and a backend that implements
   `IAnnotatedSynthesisBackend` receives them with each segment

## Design Constraints

- **Speech must never depend on SpeechTextEn**: the dependency direction is one way. Speech carries no
  reference to this package or to the part-of-speech package
- **Offsets are the contract**: all offsets are UTF-16 code unit indices into the exact analyzed text. The
  analyzer never rewrites, normalizes or re-indexes the text, so the placeholder neutralization is a
  same-length replacement. An annotation never covers a placeholder, never starts or ends on whitespace,
  and never cuts a surrogate pair, whatever the input
- **Placeholders are separators, not text**: the words on both sides of a tag are analyzed as one running
  text, so context such as the words around "[whispers]" is preserved; a sentence break is placed at the
  next word, never at the placeholder
- **Diagnostics are counts only**: the library's diagnostics contract forbids synthesized text in any
  report. The analyzer reports sentence, token, annotation and unknown-word counts built from fixed words
  and numbers, never a word, and detailed per-word data is available only through `AnalyzeDetailed`, which
  returns it to the caller instead of a sink
- **First-use latency of the tagger**: the shared tagger inflates and validates about 1.3 MB of embedded
  data on first use, which takes noticeable time once per process. It is deliberately not loaded when an
  analyzer is constructed, when a text has no tokens, or when part-of-speech annotation is not selected,
  and no warm-up API is provided in this release; a host that wants the cost paid early can analyze one
  short text at start-up
- **Immutable and thread-safe**: an analyzer keeps no per-request state, copies its options at
  construction, keeps no reference to an analyzed text or to a result after the call returns, and may
  serve any number of concurrent requests. This statement covers this library's own state only; see
  _Known Limitations_ for the dependency's separate behavior
- **No inference dependency**: the library references no ONNX Runtime package and has no native assets
- **No command-line or demo integration** in this release
- **Compliance**: all functionality must be traceable to requirements

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

The package itself contains only code under the MIT License and embeds no data of its own in this
release, so it declares MIT only: it names a license file, `PACKAGE-LICENSE.md`, that starts with the SPDX
line `SPDX-License-Identifier: MIT`, contains the MIT license text, and states that the required package
`DemaConsulting.PartOfSpeech` is a separate NuGet dependency, not bundled in this package, that carries
its own license files. The package also ships `THIRD-PARTY-NOTICES.md`, clearly scoped as a dependency
notice: it describes that dependency's embedded data (Open English WordNet, derived from Princeton
WordNet, under CC BY 4.0, and the Manually Annotated Sub-Corpus under CC BY 3.0 US) and reproduces its
attribution, license links, statement of changes and the Princeton WordNet notice verbatim, for the
reader's convenience. The composite license chain belongs to the dependency, not to this package, so it
is not claimed in this package's declaration. If a later increment embeds data in this package, the
declaration and the notices will be revisited. Both files are packed at the package root and are
packaging files, not software items.

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
  options) from `ISynthesisModel.TextAnalyzer`; one instance can be shared by every engine of the model
- **Annotation-aware backend**: a backend implements `IAnnotatedSynthesisBackend` and reads the
  annotations with `TextEnAnnotations.TryReadPartOfSpeech`, ignoring kinds it does not recognize
