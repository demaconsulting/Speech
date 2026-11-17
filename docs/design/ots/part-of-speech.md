## DemaConsulting.PartOfSpeech

### Purpose

`DemaConsulting.PartOfSpeech` is used by SpeechTextEn to split English text into sentences and tokens and to
tag each token with its Universal Dependencies part of speech. It was chosen because it is a small,
maintained, fully managed package from the same organization that ships its own trained model and lexicon
embedded in the assembly, so the analysis needs no model download, no native runtime and no inference
engine, and its tokenizer reports offsets into the text it was given. It is pre-release software, so this
repository treats it as an OTS item and pins it to one exact version in
`DemaConsulting.Speech.Text.En.csproj`, which is the single source of truth for the version.

### Features Used

- `Tokenizer.Tokenize` to split a text into sentences and tokens whose offsets index the given text
- `Tagger.Default`, the shared tagger that uses the model and lexicon embedded in the package, created on
  first use and safe to use from many threads
- `Tagger.Tag` for a tokenized sentence, returning tagged tokens with their offsets, tag, confidence and
  known-word flag
- The tag-name conversion, which gives the Universal Dependencies spelling of a tag (for example `NOUN`)

### Tokenizer Conventions Relied Upon

The NormalizationSubsystem never names a type of this package: it receives plain token offsets from the
analyzer and is written against the tokenizer's observable conventions, which are therefore part of the
contract this repository relies on and are locked by a test that fails when a version change alters them
(see *PartOfSpeech Verification*):

- **Abbreviations that keep their period inside one token**: `Dr.`, `St.`, `Mr.`, `Prof.`, `Capt.`, `Sgt.`,
  `Mt.`, `Ft.`, `Co.`, `Ltd.`, `Corp.`, `Sr.`, `Jr.`, `e.g.`, `i.e.`, `etc.`, `vs.`, the single initial `J.`,
  and the dotted forms `J.R.R.`, `U.S.`, `U.S.A.`, `p.m.` and `a.m.`. The abbreviation resolver therefore
  tries both the token alone and the token with a separate period
- **Abbreviations split into a word and an adjacent period token**: `Ave`, `Blvd`, `Rd`, `Ln`, `Apt`, `Ste`,
  `Dept` and `Bldg`; the resolver accepts either shape for every lexicon form
- **Clitics split from their host**: `do` and `n’t`, `it` and `’s`, `I` and `'d`, with a typographic
  apostrophe part of the clitic token; a leading elision such as `’til` becomes a lone apostrophe token and the
  word, while `’Tis` and `’em` stay whole
- **Standalone symbols are single tokens** when set apart by whitespace (`&`, `@`, `+`, `=`, `<`, `>`, `/`),
  and these joined forms stay one token: `he/she`, `and/or`, `w/o`, `N/A`, `24/7`, `km/h`, `R&D`, `Q&A`,
  `AT&T`, `Johnson&Johnson`, a mark glued to a word (`Brand™`), and an invisible character inside a word
  (the zero-width space and the soft hyphen)
- **Sentence rules for titles**: titles and `St.`, `Dr.`, `No.` never end a sentence, so `Dr. Smith lives on
  Maple Dr. He likes it.`, `I live on Main St. It is nice.` and `No. I don't know.` are each one tokenizer
  sentence, while a business suffix can end one (`Acme Inc. Smith left.` is two). The analyzer reconciles
  these breaks with the normalization's decisions instead of trusting the tokenizer alone

### Integration Pattern

SpeechTextEn references the package from `DemaConsulting.Speech.Text.En.csproj` with an exact version range.
The internal `PartOfSpeechAdapter` unit is the only consumer: it tokenizes with the static tokenizer, tags
each sentence with the shared tagger obtained lazily under a lock (retried after a failed attempt), and converts the
results into its own plain values. No other unit and no public API of this library names a type of the
package, so it can be upgraded or replaced by changing the adapter alone. The first use of the shared tagger
inflates and validates about 1.3 MB of embedded data once per process; the adapter therefore never obtains
the tagger for a text without tokens or when tagging is not requested. The NormalizationSubsystem tests also
call the tokenizer directly, as a test-only dependency, to feed the normalizer the same token offsets the
adapter would supply; no production code outside the adapter uses the package.

The package is a separate NuGet dependency: it is not bundled in this library's package, which declares the
MIT License only. The package embeds data under licenses other than MIT, whose attribution this library's
own package reproduces as a dependency notice (see *SpeechTextEn System Design*). Because it is a
pre-release package, a stable release of SpeechTextEn is blocked until the package publishes a stable
version (NuGet warning NU5104).

### Known Limitation: Text Retention

`DemaConsulting.PartOfSpeech` 0.1.0-beta.4 keeps the characters of the tokens it decodes in a thread-static
working buffer of its tag decoder that it never clears, so words analyzed on a thread remain recoverable
from the process memory after the request has returned, although the analyzer holds no reference to the
text or the results. This library cannot fix it and does not reach into the dependency's private state; the
defect is filed upstream as <https://github.com/demaconsulting/PartOfSpeech/issues/29>. Requirements and
tests that say SpeechTextEn "retains no text" are scoped to this library's own state. Moving the exact
version pin to a release that clears the buffer closes the limitation; until then the SpeechTextEn package is
gated (not packed or published) and a host that builds it from source with a strict no-retained-text
requirement must account for it (see *SpeechTextEn System Design*, *Known Limitations* and *Release Gate*).
