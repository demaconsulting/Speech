## NormalizationSubsystem Design

![NormalizationSubsystem Structure](TextEnNormalizationSubsystemView.svg)

### Overview

The SpeechTextEn NormalizationSubsystem decides which spans of an English text are spoken differently from
how they are written (abbreviations, symbols, apostrophes, acronyms and initialisms, invisible characters)
and records those decisions as spans with their spoken replacements, together with the sentence-break edits
that follow from them. It never rewrites the text and never consults the part-of-speech tagger. It contains
the following units:

- **NormalizationAlignment**: the alignment model: the public `NormalizationKind` and `CliticKind`
  enumerations, the internal token, span and segment values, the `AlignedTextBuilder` that enforces the
  alignment contract and the immutable `AlignedText` result
- **TextNormalizer**: the internal orchestrator; one linear pass applies the ordered rules (invisible
  characters, clitics, abbreviations, acronyms and initialisms, symbols and marks, elision apostrophes) and
  the period and sentence rules
- **AbbreviationResolver**: the internal embedded abbreviation lexicon and the neighbor-based choice among
  the readings of `St.`, `Dr.`, `Sr.` and `No.`
- **AcronymRecognizer**: the internal embedded initialism lexicon and the case-sensitive rules for dotted,
  ampersand and vowel-less all-capital forms

### Interfaces

The subsystem exposes only the `NormalizationKind` and `CliticKind` enumerations publicly, in the
`DemaConsulting.Speech.Text.En.NormalizationSubsystem` namespace; they are declared in
`NormalizationAlignment.cs` because the public analysis result of the AnalysisSubsystem names them. Everything
else is internal: the analyzer calls `TextNormalizer.Normalize(text, tokens, classifyClitics,
cancellationToken)` with the neutralized text and an array of token offsets, and reads the returned
`AlignedText` (spans, forced and suppressed breaks, clitic marks, ambiguity count, and
`ReconcileBreaks`). The subsystem consumes nothing but the Speech library's `TextAnalysisConventions` (for
the tag placeholder character) and the framework: its production code does not reference the
`DemaConsulting.PartOfSpeech` package, the analyzer, the diagnostics reporter or the options. Only the tests
use the real tokenizer of that OTS package (see _PartOfSpeech Design_), through a test helper, to produce
the token offsets the analyzer would supply.

### Design

`TextNormalizer.Normalize` validates its arguments and the token list, creates one `AlignedTextBuilder` and
one `Pass` over the tokens, and returns the builder's `AlignedText`. For each token, in order, the first
matching rule wins (see _TextNormalizer Design_ for the rule order and every table):

1. invisible characters and bullets are dropped explicitly;
2. clitic tokens have their typographic apostrophe canonicalized and, on request, their role recorded;
3. lexicon abbreviations are resolved by `AbbreviationResolver` from the neighboring tokens, and the period
   and sentence rules decide the span extent and the break edits;
4. acronyms and initialisms are recognized by `AcronymRecognizer` with the same period and sentence rules
   for dotted forms;
5. symbols, marks and the slash and ampersand inside a word are spoken as words;
6. typographic apostrophes inside a word (elisions) are canonicalized;
7. everything else is identity: digits and the capabilities listed in _SpeechTextEn System Design_,
   _Deferred Capabilities_ pass through untouched.

Every span goes through `AlignedTextBuilder.Add`, which enforces the alignment contract (see _Alignment
Contract_ in _SpeechTextEn System Design_ and _NormalizationAlignment Design_), so a defective rule fails
loudly with an `ArgumentException` instead of producing an annotation that a backend would misapply. The
normalizer adds the A6 separating spaces itself when it emits a span, including the separator between two
adjacent word-producing replacements.

**Unit collaboration.** `AbbreviationResolver` and `AcronymRecognizer` are pure lookups over their lexicons
and a few neighbor tokens; they know nothing of spans or breaks. `TextNormalizer` owns every decision about
what a span covers, whether its period is consumed or kept and which sentence breaks change.
`NormalizationAlignment` is the passive model both sides share. This is what lets a later increment add the
numeric rules, or a host override table, as further rules or units in front of the identity case without
changing the contract.

**Concurrency.** Every unit is immutable after construction. The two default lexicons are loaded under
`Lazy<T>` with `ExecutionAndPublication`, so concurrent first use loads each exactly once; a failure to load
means a defective build (a malformed embedded table), is deterministic, and is therefore cached rather
than retried. The per-request state of a pass lives in a `ref struct` on the caller's stack, so one
`TextNormalizer` serves any number of concurrent requests.

**Cancellation and linearity.** The normalizer checks the token at the start and every 256 visited tokens.
Each rule looks at a constant number of neighboring tokens and each character of a token is visited a
constant number of times, so the pass is linear in the text length, which the subsystem tests assert
with one megabyte of realistic and of adversarially repetitive text and with single huge tokens.

**Resources.** The two lexicons are embedded resources of the library assembly, `abbreviations.tsv` and
`acronyms.tsv` in the `Resources` folder, written from scratch by DEMA Consulting under the package's MIT
License. They are plain tab-separated text so a non-programmer can review them; their formats are documented
in _AbbreviationResolver Design_ and _AcronymRecognizer Design_.
