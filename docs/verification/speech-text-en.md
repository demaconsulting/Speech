# SpeechTextEn System Verification Design

This document describes the system-level verification strategy for the SpeechTextEn library.

## Verification Approach

SpeechTextEn is verified at system level through its public API, exactly as a model package and a host use it: the tests
construct `EnglishTextAnalyzer` (with default and with explicit options), call the Speech library's
`ITextAnalyzer.Analyze` on it and read the results with the library's own typed annotation readers. The end-to-end tests
go one step further and drive the real Speech `SynthesisSession`: a small test model supplies the real default
`EnglishTextAnalyzer` through `ISynthesisModel.TextAnalyzer` and a recording `IAnnotatedSynthesisBackend`, the real
`SpeechSynthesizerFactory` loads the engine, and the tests assert what the backend actually receives. That proves the
analyzer works across the real seam, that its annotations (part of speech, normalization, spell-out, overrides,
heteronym readings and confidences) survive the chunking of a long text and audio tags inside a request, that the
analyzer's sentence breaks (not the punctuation heuristic) decide where sentences are split, and that a number or a
symbol is never split from its annotation wherever a chunk budget or clause cut falls.

The tests assert observable behavior only: annotation kinds, values and positions, sentence breaks, the text a consumer
produces by following the documented consumer algorithm, the chunks and annotations a backend receives, the diagnostics
messages and the exceptions. No test asserts timing, allocation, thread or polling behavior, object lifetime, internal
state, reflection results or the text of repository files; those are design choices, recorded in the design documents.
No throughput claim is made or verified.

Below the system level, the unit and subsystem behavior is verified by the AnalysisSubsystem, HeteronymSubsystem and
NormalizationSubsystem tests (see their verification documents), and the part-of-speech package the analysis builds on
is verified separately as an OTS item (see *PartOfSpeech Verification*). The golden corpora (`TestData/*.tsv`) and the
random property tests of the alignment contract live at that level. System and unit tests reside in
`test/DemaConsulting.Speech.Text.En.Tests`.

The non-shipped trainer has its own test project, `test/DemaConsulting.Speech.Text.En.Trainer.Tests`, and no
system-level test class: its system requirements are evidenced by the subsystem-level tests of the
TrainingDataSubsystem, TrainingSubsystem, ExternalEvaluationSubsystem and ClusteringSubsystem, which drive its commands
through their one observable interface each. This cross-level link is deliberate and documented here once; a few
Heteronym system requirements likewise link the analyzer unit tests that can inject a damaged model, which the system
level cannot do through the public API.

Automated coverage does not extend to the accuracy of the tagger beyond a few unambiguous golden sentences: the analyzer
relays the tagger's decisions, so the tests assert structure, offsets and wiring and use only sentences whose tags the
tagger assigns with full confidence. The curated heteronym sentences are clear cases written independently of any
labeled data set: they show that the shipped model is wired into the default analyzer, not its accuracy, which is
claimed only as the committed evaluation record states it. A phoneme string is verified only for its syntax. The
documented limitations (Roman numerals, prefix currency codes, multi-word override entries, dotted names that are not
web addresses) are pinned by golden
rows and unit tests.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the library
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline; the FileAssert tests run against the built
  package
- **Dependencies**: no external services, no network access, no native runtime and no audio hardware; the tagging
  package's embedded model, the library's own embedded tables and heteronym model, and the sample dictionary
  `TestData/overrides-dictionary.tsv` are the only data used
- **Isolation**: each end-to-end test uses a fresh scratch model directory and its own recording backend

## External Interface Simulation

The Speech library is used for real. The model and the backend are test doubles implementing the library's public
`ISynthesisModel` and `IAnnotatedSynthesisBackend` that record what they receive; a recording `ISpeechDiagnostics` sink
(optionally throwing) simulates a host sink.

## System-Level Test Scenarios

### End-to-End Delivery Through a Real Session

**Test**: `SpeechTextEn_SynthesisSession_EndToEnd_AnnotationsReachBackendAcrossChunksAndTags`

A text of three repeated paragraphs (several times the chunk length) holds an amount with cents, a time with a glued
meridiem, a date, a percent, abbreviations at and inside sentences, a name with credentials, the spelled initialism
`FBI`, one override per action (`say`, `spell`, `pron`, `skip` and a heteronym `reading`), the sentence
`I want to record a record.` and three audio tags. Verifies that the backend receives several chunks with no tag
placeholder, that the words of the text arrive in order, and that the annotations it receives (part of speech,
`en.norm`, `en.spell`, `en.pron`, `en.reading` and `en.reading-confidence`) are exactly those the analyzer produced for
the text on the same source characters, in the same order, so none is lost, duplicated or moved by the chunking and none
covers a tag. Verifies the normalization and spelling a backend reads (`Dr.` as `Doctor`, the amount in words, `FBI` and
`Ph.D.` spelled), the override annotations, that neither `Dr.` nor `Jr.` ends a chunk and `John Smith Jr. Ph.D. spoke`
stays in one chunk, that `record` reads `record.verb` then `record.noun` in every paragraph with a confidence annotation
on the same span in the same chunk, that the overridden reading is `lead.metal` with confidence `1.000`, and that no
reading overlaps a replacement or a pronunciation.

**Test**: `SpeechTextEn_SynthesisSession_NumbersAndSymbolsAtChunkCuts_AreNeverSplitOrLost`

Verifies, for every padding that moves the 200-character budget cut across a text of two-token amounts, times, dates and
units, that the normalizations delivered equal the analyzer's; that the budget cut keeps a percent sign with its number
(it used to leave the sign alone in a chunk) and the sign keeps its normalization; that a date whose day and year fall
on opposite sides of a clause cut keeps the annotation of every token, the glued meridiem included; that a lone `&` and
`©; Brand` keep their chunks and normalizations; and that a clause of only a zero-width space or a bare `*` is merged
into its neighbor (with the dropped-character annotation delivered there) and carries no part of speech.

### Analyzer Contract and Part of Speech

**Tests**: `SpeechTextEn_Analyze_MultipleSentencesAndTag_ReturnsAuthoritativeBreaks`,
`SpeechTextEn_Analyze_TagPlaceholder_IsNeverCoveredByAnAnnotation`,
`SpeechTextEn_Analyze_CanceledToken_ThrowsOperationCanceledException`,
`SpeechTextEn_Analyze_ConcurrentRequestsWithDifferentTexts_EachGetsItsOwnResult`,
`SpeechTextEn_Analyze_LargeRepetitiveTexts_AnnotatesEveryRepetitionAlike`,
`SpeechTextEn_Analyze_Sentence_AnnotatesWordsWithReadablePartOfSpeech`,
`SpeechTextEn_Analyze_PartOfSpeechDisabled_ProducesNoAnnotations`

Verifies that a sentence gets one readable part-of-speech annotation per word (`cat` is `NOUN`) and none on its final
period, and none at all when no annotations are selected; that the sentence breaks are non-null and sit at the first
word of each later sentence, not inside `Dr. Smith`;
that no annotation covers a placeholder; that a canceled token throws `OperationCanceledException`; that 32 concurrent
requests with six different texts (numbers, abbreviations, overrides and heteronyms among them) on one shared analyzer,
the first of them racing for the tagger and the model, each get exactly the result their text gets alone; and that a
text of a quarter of a million characters built of one repeated unit (realistic text, adversarial `St.`, `A.`, `&` and
`Dr. He`, and every numeric construct), analyzed with normalization only, is annotated and broken in every repetition
away from its two ends exactly as the same repetition in a short reference text. Correctness is asserted as exact
output; no elapsed time is.

### Diagnostics

**Tests**: `SpeechTextEn_Diagnostics_AnalyzedRequest_ReportsCountsOnlyAndSinkFailureIsContained`,
`SpeechTextEn_Diagnostics_Readings_AreCountsOnly`,
`SpeechTextEn_Diagnostics_Overrides_ReportCountsOnlyAndNeverDictionaryContent`

Verifies that one event in the `TextEn.Analysis` category reaches the sink with counts only, in the fixed template
ending `... N normalizations, X ambiguous readings, F numeric fallbacks, P overrides applied, I overrides ignored.`,
containing none of the words of the text, and that an analyzer whose sink throws still returns its full result; that an
analysis of `I want to record a record.` reports in addition one `TextEn.Heteronym` event
(`Assigned 2 heteronym readings: 2 classified, 0 single-candidate, 0 by override, {n} below confidence 0.60.`)
containing neither `record` nor `noun`; and that a dictionary of sentinel keys, payloads and phonemes produces
`3 overrides applied, 1 overrides ignored` and a message with none of them.

### Normalization Annotations

**Tests**: `SpeechTextEn_Analyze_Abbreviation_AnnotatesSpokenFormWithoutChangingText`,
`SpeechTextEn_Analyze_NormalizationNotSelected_ProducesNoNormAnnotations`,
`SpeechTextEn_Analyze_NumberNextToAudioTag_KeepsBothAnnotations`,
`SpeechTextEn_Analyze_SpelledInitialisms_AreAnnotatedAsSpellOut`,
`SpeechTextEn_Analyze_Meridiems_AreTypedSpellOutSpans`,
`SpeechTextEn_Analyze_SayOverrideWithSingleLetterPayload_IsNotSpelled`,
`SpeechTextEn_AnalyzeDetailed_Clitics_RecordTheirRole`

Verifies that `Dr. Smith lives here.` yields exactly one `en.norm` annotation over the exact characters `Dr.`, which the
typed reader returns as `Doctor`, that applying it gives `Doctor Smith lives here.` while the text is unchanged and no
unchanged word is annotated; that selecting part of speech only yields no `en.norm` annotation, and normalization only
yields only `en.norm` annotations; that `5<tag>kg`, `$<tag>5` and `50<tag>%` keep both annotations and no annotation
covers the tag; that initialisms, dotted initialisms, single-letter initials, credentials and meridiems are `en.spell`
annotations whose readers report letters and that no `en.norm` value is a bare letter sequence; that `5pm` and `5 p.m.`
are an `en.norm` number and an `en.spell` meridiem, abutting inside a glued token; and that a `Say` override of a single
capital letter stays an `en.norm` word that no reader reports as spelled; and that the detailed analysis records `Has`,
`Possessive` and `Not` for the clitics of a sample sentence. Symbols, marks, apostrophes, invisible characters and
acronym case rules are verified by the golden normalization corpus and the NormalizationSubsystem tests (and their
delivery through chunking by the end-to-end scenarios), as are the random hostile-text property tests of the alignment
contract.

### Abbreviations and Sentence Boundaries

**Tests**: `SpeechTextEn_Analyze_ContextDependentAbbreviations_ResolveFromNeighbors` (a theory),
`SpeechTextEn_Analyze_AbbreviationAtSentenceEnd_BreaksAfterItAndKeepsThePeriod`,
`SpeechTextEn_Analyze_CredentialsAfterNameSuffix_StayInOneSentence`,
`SpeechTextEn_Analyze_MonthAndWeekdayAbbreviations_ResolveFromDateContext`

Verifies that `St.` in `Visit St. Louis` reads Saint and `Dr.` in `Maple Dr. He` reads Drive; that in
`I live on Main St. It is nice. Dr. Smith agrees. No. I don't know.` the breaks sit exactly before `It`, `Dr.`, `No.`
and the following `I`, none inside `Dr. Smith`; that `John Smith Jr. Ph.D. spoke.` and `Ann Lee Esq. Ph.D. M.D. left.`
stay one sentence each with breaks only before the sentence starters; and that
`Sat. 5 Jan. 2024 and Sun. Mar. 5 and Wed. 7:30. He left Mon. He came Tue.` reads the month and weekday abbreviations
from the date context with the two real sentence ends kept and none inside the dates.

### Numbers, Currency, Dates and Times

**Tests**: `SpeechTextEn_Analyze_NumbersCurrencyUnitsDatesAndTimes_AreSpokenAsWordsWithoutChangingText`,
`SpeechTextEn_Analyze_NumericSentenceBreaks_FollowTheSentenceEnd`,
`SpeechTextEn_Analyze_AmbiguousNumbers_UseDocumentedDefaultsAndAreCounted`,
`SpeechTextEn_Analyze_OversizedNumbers_AreBoundedAndCounted`

Verifies, through the public analyzer and an applier independent of the production code, that cardinals, thousands
separators, decimals, ordinals, hash numbers and decades (`1,234`, `3.5`, `21st`, `#5`, `1990s`), amounts, percent,
units (`$1,234.50`, `€5`, `50 %`, `5 million USD`, `3 kg`, `1 ft.`, `72°F`) and times and dates (`January 5th, 2024`,
`5:30pm`, `12/25/2024`, `17:45`, `7 am`) are spoken as the expected plain words, every `en.norm` value contains only
letters, spaces and apostrophes, and the text is unchanged; that the sentence breaks follow the sentence end around
numbers, decimals, amounts, abbreviated dates and units; that the documented defaults for ambiguous numbers (a bare
year, a month-first date, a fraction) are applied and counted (four ambiguous readings, cued and equal readings not
counted); and that a 16-digit run is read digit by digit while a 65-digit run and a 33-digit fraction stay as written,
each counted as a numeric fallback, with no value over the documented size.

### Web Addresses and Identifiers

**Test**: `SpeechTextEn_Analyze_WebAddressesAndIdentifiers_AreSpokenAsPiecesWithoutChangingText`

Verifies, through the public analyzer and the independent applier, that `Visit https://www.example.com/docs?id=7 today.`
is spoken with the scheme and `www` spelled, every separator as a word and the colon kept, that
`Mail john.smith@example.com, now.` keeps its comma, that `report.pdf` and `Node.js` are unchanged, and that `A4`,
`B12`, `COVID-19`, `12-year-old` and `10K` are spoken as `A four`, `B twelve`, `COVID nineteen`, `twelve-year-old` and
`ten K`, with the source text untouched.

### Pronunciation Overrides

**Tests**: `SpeechTextEn_Analyze_Overrides_AnnotateWholeWordsPerAction`,
`SpeechTextEn_Analyze_OverrideActions_ReadBackThroughTypedReaders`,
`SpeechTextEn_Analyze_OverridePrecedence_UserOverridesBuiltInRules`,
`SpeechTextEn_Analyze_OverrideOnConsumedToken_IsIgnoredAndCounted`,
`SpeechTextEn_Analyze_OverrideOnAbbreviationWord_KeepsSentenceBreaksAndChunks`,
`SpeechTextEn_Overrides_Dictionary_LoadsFromFileValidatesAndDrivesAnalysis`

Verifies, with the sample dictionary, that say, spell, pronounce and skip each act on their whole word only (a longer
word is another word) with one annotation per action on the exact characters and the text unchanged; that a consumer
written only against `TryReadReplacement` and `TryReadPronunciation` obtains
`engine ex runs koo ber net eez and kubernetes, a G I F and Polish food.` with exactly one spelled replacement and one
pronunciation; that a user override beats the acronym table, the vowel-less rule, the abbreviation lexicon, the symbol
rule and the elision rule, the built-in reading returns without it, and a case-sensitive entry is not applied to another
case; that an override on a word a number match consumed (`kg` in `5 kg`, `million` in `$3 million`) is ignored and
counted (`0 overrides applied, 2 overrides ignored`) while the same words alone are overridden; that
`Say("Fig", "figure")` leaves `See Fig. 5 for details.` as one sentence and one chunk; and that the sample file loads
through a stream reader into 23 entries and drives an analysis, while a bad line is refused with `Line 2:` and a fixed
reason that does not echo its content. The 32-way concurrent use of one dictionary is part of the analyzer contract
scenario; the exhaustive dictionary rules are verified at the AnalysisSubsystem level.

### Heteronym Readings

**Tests**: `SpeechTextEn_Analyze_DefaultAnalyzerClearHeteronymSentences_EmitTheExpectedReading` (a theory),
`SpeechTextEn_Analyze_DefaultAnalyzerTextWithoutHeteronym_EmitsNoReadingAnnotations`,
`SpeechTextEn_Analyze_ReadingOverride_BeatsClassifier`

Verifies that the default analyzer (the public constructor, with the trained model embedded and no configuration)
decides the reading in eight clear sentences written independently of any data set and covering `record`, `lead`,
`read`, `wind`, `tear`, `bass` and `bow`: exactly one `en.reading` and one `en.reading-confidence` (a number from 0
to 1) lie on exactly the word beside its `en.upos`, and the id is the expected one; that text without a heteronym word
gets part-of-speech annotations only; and that a reading entry `record` / `record.noun` makes both `record` words
`record.noun` with confidence `1.000` where the default analyzer reads `record.verb` and then `record.noun`. Delivery
through the real session is verified by the end-to-end scenario. An analyzer without a model source, a damaged or
incompatible model (no readings, one warning) and the confidence text under foreign cultures are verified at the
AnalysisSubsystem and HeteronymSubsystem levels, where a model can be injected; the requirement
`SpeechTextEn-Heteronym-ModelIntegrity` links those tests.

### Request Noninterference

**Tests**: `SpeechTextEn_Analyze_AfterAnotherRequest_ResultAndDiagnosticsDiscloseNothingOfIt`,
`SpeechTextEn_Analyze_ConcurrentRequestsWithDifferentTexts_EachGetsItsOwnResult`

Verifies the observable side of the rule that an analyzer must not retain request text: after an analyzer has analyzed
`Quokka marmoset rested. Zebra galloped.`, a later request gets exactly the result a fresh analyzer gives, and neither
its annotations nor its diagnostics event carry a word of the earlier text; concurrent requests with different texts
each get the result their text gets alone. That no reference to analyzed text remains in memory is a design obligation,
not a tested behavior: this library keeps no text (see the *EnglishTextAnalyzer* design), and the tagging package's
private working buffers have no public inspection hook, so there is no sentinel-and-buffer or forced-collection test
(both would need reflection, a memory scan or garbage-collector timing). The pinned tagging package version clears the
buffers, the upstream regression tests are cited in *PartOfSpeech Verification*, and the remaining gap is stated there
for the repository owner's approval.

### Package Metadata

**Tests**: `TextEn_PackageLicenseFiles_Content`, `TextEn_PackageDependencies_Nuspec` (both FileAssert tests over the
**built package**, not repository files)

When a `.nupkg` was built (only for a pre-release version) it contains at the package root `PACKAGE-LICENSE.md` (whose
first line, directly followed by the license heading, is `SPDX-License-Identifier: MIT AND CC-BY-3.0-US`, and which
names `THIRD-PARTY-NOTICES.md`, the model `heteronyms.bin`, the Manually Annotated Sub-Corpus and CC0) and
`THIRD-PARTY-NOTICES.md` (the dependency notice, the Princeton University notice, the corpus attribution with the
license link and the statement that its authors do not endorse the model, the model, and the CC0 1.0 dedication), and
its `.nuspec` names the license file and declares the tagging package as a dependency whose every declaration is an
exact-version range, and no dependency whose identifier names ONNX or sherpa. For a stable version no package is built
and both tests accept its absence (and reject more than one package). The tests check that the statements are present,
not that the licensing position is correct, which is the owner's decision. That the project is packable only for a
pre-release version while the tagging package is a pre-release is checked by the build script's pack verification (it
evaluates whether the project is packable with MSBuild and requires a produced package), not by a test that reads the
project file.

### Training Data, Training and External Evaluation

**Tests**: the `TrainingDataSubsystem_CheckData_*`, `TrainingSubsystem_Train_*`, `TrainingSubsystem_TrainCheck_*`,
`TrainingSubsystem_Evaluate_*`, `ExternalEvaluationSubsystem_*` and `ClusteringSubsystem_*` tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests` that the requirements `SpeechTextEn-TrainingData-*`,
`SpeechTextEn-Training-*` and `SpeechTextEn-Evaluation-*` link (see the verification documents of the four subsystems)

Verifies the trainer's requirements through the one observable interface of each command. The real committed
`data/text-en` folder passes `check-data`, and each class of defect written into a temporary copy (a restricted license,
a malformed or changed file, a wrong label or span, a sentence, document or template shared between splits, a missing
attribution file, an overlap with a synthetic external set) makes the command fail with a finding that names the check;
the labels themselves are not claimed correct. Training is verified on a small data folder written in code: two
trainings of the same inputs give the same image and record, the fixture model has a pinned hash (its identity on every
operating system is evidenced by the CI build matrix, not by one local run), the holdout files have no influence,
unusable rows are counted, the trainer and the analyzer decide alike on every aligned row, and the evaluation figures,
gate exit codes and record follow the fixture exactly. Three tests run against the committed files: the committed model
and record are what retraining produces, the committed model passes the gate on the natural holdout through the analyzer
that embeds it, and the committed external record has the expected files with a strict numeric grammar. External
evaluation runs on hand-written fake datasets in fake archives with matching pins: **no automated test uses the network,
the real datasets or a sentence of them**. A tampered or malformed source is refused with exit code 2 and no figure,
unusable rows are counted by reason, the seed study reports its spread and intervals, and neither the output nor the
record holds any dataset text. The external figures themselves are not asserted by any automated test: they come from a
local run, are committed as the external evaluation record and are claimed exactly as that record and its limits state
them. The word-class induction runs on three generated books served by a fake mirror: fetching with pins, the audit, the
write and a full re-induction check succeed, a book quoting a development sentence loses exactly that sentence, the
table words are the analyzer's lookup keys, one and three threads give byte-identical files, and a fixed-input
clustering, a fixture corpus and a snapshot have golden results that every CI platform must reproduce; the written table
and record pass the offline word-cluster check of `check-data`, which a later edit of the source list fails. The
induction of the real corpus is a manual step recorded in the trainer's run log, and the committed table and record pass
`check-data` with the tokenizer of the build.

## Acceptance Criteria

A SpeechTextEn system-level test run passes when all scenarios above pass without unexpected exceptions, every subsystem
and unit suite passes, and the two FileAssert tests of the built package pass. The automated verification boundary stays
honest:

- **Claimed as automated coverage**: structure, offsets, wiring, sentence breaks, placeholder handling and diagnostics;
  the alignment contract of the replacement and pronunciation annotations, including their survival through real
  chunking at every budget and clause cut; the documented abbreviation, symbol, clitic, initialism and numeric behavior;
  user pronunciation overrides (whole-word matching, one annotation per action, precedence, counts only, identical
  results under concurrent use); the heteronym reading behavior (the expected reading and a confidence for the curated
  clear sentences, none for a text without a heteronym word, a `Reading` override that beats the classifier, survival
  through chunking, counts-only diagnostics); large-input output checks; and the package metadata of the built package
- **Claimed only as stated elsewhere**: the accuracy of the shipped heteronym model, as the committed evaluation records
  state it (the gate on the natural holdout is verified against the committed files; the unverified labels, the tagger's
  exposure to part of the natural set, the small counts per reading and, for the external datasets, the domain shift and
  coarser mapping are limits that are not claimed away); the linguistic accuracy of the tagger, left to the tagging
  package's own evaluation; the abbreviation and numeric readings, only for the documented cases and en-US policy; and
  the licensing position, which is the owner's decision
- **Not claimed**: the correctness of the phonemes of a pronunciation override; any elapsed-time, throughput,
  allocation, thread or object-lifetime behavior; and the clearing of the tagging package's private working buffers,
  which is evidenced by the upstream package's regression tests, with the remaining gap stated for the repository
  owner's approval
- **Trainer scenarios** pass when the real committed data passes `check-data` and each seeded defect is reported by the
  check that owns it; the fixture model is repeatable with the pinned hash; holdout files have no influence; the trainer
  and the analyzer decide alike; the evaluation figures, gate and record follow the fixture; the committed model,
  training record and evaluation record are what retraining and evaluation produce and the committed model passes the
  gate; and a fake external source is verified, converted, evaluated and recorded while a tampered or malformed input
  exits 2 with no figure and no dataset text appears in the output or the record
