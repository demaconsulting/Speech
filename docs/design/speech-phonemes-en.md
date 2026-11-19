# SpeechPhonemesEn System Design

This document provides the system-level design for the SpeechPhonemesEn library.

![SpeechPhonemesEn Structure](SpeechPhonemesEnView.svg)

## Architecture

SpeechPhonemesEn is a .NET library, published as the `DemaConsulting.Speech.Phonemes.En` NuGet package (a pre-release;
see *Release*), that turns English text into the phoneme input of a voice model. A model package creates an
`EnglishPhonemizer`, gives it the text of one speech segment and, when it has them, the annotations that the English
text analysis (SpeechTextEn) produced for the segment, and receives a `PhonemizedText`: an ordered list of word,
punctuation and unsupported items, each word with its ARPAbet pronunciation and the source that gave it. An
`EspeakIpaRenderer` (Piper) or a `MisakiRenderer` (Kokoro) then renders that list as the phoneme string of the voice.
The library has no inference runtime and no model of a voice; it is plain managed text processing over data embedded in
the package.

SpeechPhonemesEn is a sibling system to Speech and SpeechTextEn. It uses SpeechTextEn only for the vocabulary of its
annotations (`TextEnAnnotations`) and for the reading inventory (`ReadingInventory`), and the Speech library for the
`SegmentAnnotation` type; it is used by the Kokoro and Piper model packages and by nothing else. Neither Speech nor
SpeechTextEn knows it.

The library consists of four subsystems, one per step of the pipeline, with dependencies running one way:

- **LexiconSubsystem**: the pronunciation data and the laws over it: `CmuLexicon` (the embedded CMU Pronouncing
  Dictionary), `Phone` (the phone and pronunciation value types and the letter names), `Morphology` (the sound of
  inflected, prefixed and contracted forms) and `ReadingPronunciations` (the pronunciation of every reading id)
- **LetterToSoundSubsystem**: the pronunciation of a word that no dictionary knows: `LtsModel` (the trained integer
  network) and `LetterToSound` (the converter that caches and never leaves a word silent)
- **PhonemizerSubsystem**: `EnglishPhonemizer` with its result types, `WordTokenizer` (with the spoken text and the
  annotation index) and `WordPronouncer` (the priority list for one word)
- **RenderingSubsystem**: `EspeakIpaRenderer` and `MisakiRenderer`, the `WeakForms` table and `PhonemeStream`, the
  layout and splitting both renderers share

PhonemizerSubsystem uses LexiconSubsystem and LetterToSoundSubsystem; LetterToSoundSubsystem uses LexiconSubsystem (the
phone types and the letter names); RenderingSubsystem uses the result types of PhonemizerSubsystem and the phone types
of LexiconSubsystem. LexiconSubsystem uses nothing of the others. The non-shipped tools project
(`tools/DemaConsulting.Speech.Phonemes.En.Tools`) builds the embedded data, measures the library and regenerates the
golden corpora; it is not part of the package and is described under *Data Provenance and Reproducibility*.

## External Interfaces

| Interface | Direction |
| --- | --- |
| `EnglishPhonemizer.Phonemize` (with and without annotations) | Inbound |
| `PhonemizedText`, `PhonemizedItem`, `PhonemizedItemKind`, `PronunciationSource` | Outbound result |
| `EspeakIpaRenderer`, `EspeakIpaProfile` | Inbound |
| `MisakiRenderer`, `MisakiProfile` | Inbound |
| `SegmentAnnotation` and the `en.*` annotation kinds | Consumed |
| Embedded lexicon, letter-to-sound model, reading tables | Internal |

**Interface details:**

- `EnglishPhonemizer.Phonemize`
  - Format: Public method call, `Phonemize(text)` and `Phonemize(text, annotations)`
  - Constraints: Thread-safe and immutable; throws only for a null text; annotation offsets are UTF-16 indices into
    the given text; annotations of kinds it does not use (such as `en.upos`) are ignored
- `PhonemizedText` and its item types
  - Format: Public immutable classes and enumerations; each item has a kind, its text, an ARPAbet text and phone
    symbols for a word, a source, a requested reading id and whether it was applied, an out-of-vocabulary flag and an
    optional IPA override; the text lists its out-of-vocabulary words and its unsupported tokens
  - Constraints: Phone and pronunciation types stay internal, so the public result carries ARPAbet symbol strings
- `EspeakIpaRenderer`, `MisakiRenderer`
  - Format: Public classes with `Render(PhonemizedText)` and `RenderParts(PhonemizedText, maxSymbols)`
  - Constraints: Immutable and thread-safe; only symbols of the voice model's own table; the profile enumerations
    hold the one supported dialect each (US) and exist for a later British profile
- `SegmentAnnotation`, `TextEnAnnotations`
  - Format: Consumed Speech and SpeechTextEn types
  - Constraints: Only `en.norm`, `en.spell`, `en.reading` and `en.pron` are used; malformed values are ignored
- Embedded data
  - Format: Embedded resources: two gzip-compressed binary images (`cmudict-en.bin.gz`, `lts-en.bin.gz`) and two
    tab-separated tables (`reading-pronunciations.tsv`, `form-overrides.tsv`)
  - Constraints: Loaded once on first use under `Lazy<T>`; a malformed resource is a defective build and throws

## Dependencies

SpeechPhonemesEn has two project dependencies, the Speech library and SpeechTextEn, and no NuGet dependency of its own
beyond the build-time packages. It depends on no inference runtime (ONNX Runtime or sherpa-onnx), so any model package
can use it; the package inspection test of the nuspec asserts that.

- **DemaConsulting.Speech.Text.En** supplies the annotation vocabulary and the reading inventory whose reading ids the
  reading table covers. Through it the package also depends on the part-of-speech tagging package, which is
  pre-release; hence the pre-release-only packing guard (see *Release*)
- **DemaConsulting.Speech** supplies `SegmentAnnotation` and the `ITextAnalyzer` conventions

The CMU Pronouncing Dictionary is not a dependency but embedded data (see *Data Provenance and Reproducibility*). The
non-shipped tools project references the library, and the library's `InternalsVisibleTo` entries name the tools and
tests assemblies without adding anything to the public surface.

## Risk Control Measures

N/A - SpeechPhonemesEn provides no safety-critical functionality requiring risk control measures (IEC 62304 §5.3.3). It
produces phoneme input for speech synthesis and has no clinical or safety role.

## Data Flow

1. **Input**: a model package calls `Phonemize(text, annotations)` with the text of one segment and the annotations of
   the text analysis for it (or `Phonemize(text)` with none, the plain generation path)
2. **Spoken text**: `SpokenText` applies every `en.norm` and `en.spell` replacement to the text, remembering for each
   spoken character where it came from; `AnnotationIndex` indexes the `en.reading` and `en.pron` annotations by the
   original span they cover
3. **Tokenizing**: `WordTokenizer` cuts the spoken text into word, letter, digit, clitic, punctuation and unsupported
   tokens, skipping whitespace and the audio-tag placeholder
4. **Pronouncing**: for each word token the phonemizer applies a host override or reading annotation when the token is
   unchanged original text, and otherwise asks `WordPronouncer`, which tries the sources in the order of *Source
   Priority*; letters are spoken by letter names, digits one by one, clitics attached to the word before them
5. **Result**: the items form a `PhonemizedText`, which also lists the out-of-vocabulary words and the unsupported
   tokens
6. **Rendering**: a renderer maps each word's phones to the voice's symbols with the sound rules of that voice and the
   weak-form table, passes punctuation through as the voice allows and joins the pieces with the shared layout;
   `RenderParts` cuts a long text between words so each part fits the model's limit

## Source Priority

Each word is pronounced from the first source that can pronounce it, and the item records the source
(`PronunciationSource`, whose order is the priority):

1. **Override**: a host `en.pron` annotation (ARPAbet is parsed and, when it has no stress digit, the first vowel gets
   primary stress; IPA is carried on the item for the renderer to pass through)
2. **Reading**: the reading id of an `en.reading` annotation, when the reading table resolves the word under it
3. **Lexicon**: the first variant of the dictionary entry, which is the default pronunciation of a heteronym
4. **Morphology**: the pronunciation derived from a known stem and the sound of the ending, or from a productive prefix
   and a known remainder
5. **LetterToSound**: the trained model, with the item flagged out of vocabulary and the word listed in the result

Two further sources are not alternatives but forms of speech: **Spelled** (the letter names of a spelled replacement)
and **Digits** (a bare digit). A reading id that cannot be applied to its word is not an error: the word keeps its
default and the item records the requested id with the applied flag false, so the problem is visible.

## Data Provenance and Reproducibility

Every pronunciation in the package comes from the CMU Pronouncing Dictionary (pinned by SHA-256 in
`data/phonemes-en/MANIFEST.tsv`; the dictionary text itself is fetched into a git-ignored folder and never committed) or
from phonetic knowledge written by DEMA Consulting. No misaki, WikiPron or espeak data is used or committed, and the
reference engines used to measure the renderers run only on a developer machine, their output never committed.

- **Lexicon image** (`cmudict-en.bin.gz`, about 0.8 MB): the dictionary filtered to the words made of the letters a to z
  and apostrophes, front-coded in blocks and compressed. The tools command `build-lexicon --check` rebuilds the image
  from the pinned input and compares it, and the manifest records the hash of the uncompressed image
- **Letter-to-sound model** (`lts-en.bin.gz`, about 0.67 MB): trained on the dictionary alone by `train-lts`, whose
  `--check` retrains it (the training is deterministic to the bit) and compares the hash of the image. The benchmark
  that justified shipping it (its phone error rate is less than half of a rule-based baseline) is recorded in
  `docs/evaluation/phonemes-en/oov.md`
- **Reading tables** (`reading-pronunciations.tsv`, `form-overrides.tsv`, `reading-gaps.tsv`): reviewed data in
  `data/phonemes-en/`; `propose-readings --check` verifies that every reading id and every spelling and reading pair
  resolves and that the gap list equals the committed one (empty)

The embedded images are binary and deliberately not review files; they are pinned by the reviewed manifest and verified
by the `--check` commands, which need the pinned dictionary download and therefore run on a developer machine or in a
build job that has fetched it. The tests that need nothing external (every entry of the lexicon found again, the pinned
out-of-vocabulary golden, the reading contract, the morphology agreement gate) run on every build.

## Design Constraints

- **Determinism**: the same text and annotations give the same result on every call, runtime and operating system. The
  letter-to-sound network uses exact integer arithmetic, the tables are immutable and the golden corpora run on every
  target framework
- **Concurrency**: one phonemizer or renderer serves any number of threads; shared data loads once under `Lazy<T>`
- **Size**: the two embedded images stay below 1 MB each (0.8 MB and 0.67 MB today); the training and the build commands
  check the budget
- **Never silent**: a word with letters is always pronounced and an unpronounceable token is always reported
- **Symbol sets**: the renderers emit only symbols of the Piper `phoneme_id_map` and of the Kokoro vocabulary, proven
  over the whole dictionary and every sequence of three phones
- **Language**: US English only; the profile enumerations leave room for a British profile, which is not provided
- **Public API**: nine public types (the phonemizer, its result types, the two renderers and their profiles); everything
  else is internal

### Not Provided

No sense disambiguation (the reading comes from the text analysis), no normalization of numbers or abbreviations (the
text analysis does it; a bare digit is a digit-by-digit fallback), no dialects other than US, and no pronunciation from
any source but the CMU Pronouncing Dictionary and the trained model.

### Release

The package is packable only for a pre-release version while its dependency chain (SpeechTextEn, which depends on the
part-of-speech package) is pre-release: a stable package may not depend on a pre-release package (NuGet NU5104). A
stable version skips the project and the other packages still pack. The built package is inspected by FileAssert tests
(the license files, the generated API documentation and the nuspec).

### Package License Files

The package carries `PACKAGE-LICENSE.md`, whose first line is the SPDX expression `MIT AND LicenseRef-CMUdict`, and
`THIRD-PARTY-NOTICES.md`, which reproduces the copyright notice and license of the CMU Pronouncing Dictionary, states
the changes made to it and names the embedded data derived from it. Both are packed at the package root and the nuspec
names the license file.
