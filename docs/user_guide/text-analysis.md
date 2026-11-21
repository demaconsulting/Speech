# English Text Analysis

This chapter describes the optional, pre-release `DemaConsulting.Speech.Text.En` package: an English implementation of
the `ITextAnalyzer` seam that a synthesis model can supply (see "Text analysis for model authors" in the usage guide for
the seam itself). It gives a phoneme-driven engine the sentence-level context it needs to say English text well: the
part of speech of each word, how abbreviations, symbols, initialisms and numbers are spoken, which sentence a word is
in, and which reading a heteronym such as "record" has. The analysis is delivered as **annotations on ranges of the
original text**; the text itself is never rewritten.

The package is a preview. It is published only in pre-release versions (it depends on the pre-release
`DemaConsulting.PartOfSpeech` tagger, and NuGet does not allow a stable package to depend on a pre-release one), and the
details of the annotation values may still change before a first release. The command-line tool and the demo
application have no switch for it, but they use it: the Kokoro and Piper models (see "Kokoro and Piper" in the
synthesizing chapter) return an `EnglishTextAnalyzer` from `ISynthesisModel.TextAnalyzer`, so `speech-cli speak` and
the demo speak numbers, dates, URLs and heteronyms the way this chapter describes. Install it with a pre-release
version:

```bash
dotnet add package DemaConsulting.Speech.Text.En --prerelease
```

## Using the Analyzer

A model for English returns the analyzer from `ISynthesisModel.TextAnalyzer`, and its backend implements
`IAnnotatedSynthesisBackend` to receive the annotations of each chunk:

```csharp
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;

// In your ISynthesisModel implementation (one shared instance is fine; it is thread-safe):
//     private static readonly ITextAnalyzer Analyzer = new EnglishTextAnalyzer();
//     public ITextAnalyzer? TextAnalyzer => Analyzer;
```

The Speech library calls the analyzer once per request, before it splits the text into chunks, and re-bases the
annotations onto each chunk. An annotation that a chunk boundary would split is dropped rather than clipped, so the
analyzer only produces spans that the chunker never cuts. The analyzer
also supplies authoritative sentence boundaries, so the abbreviation period of "Dr. Smith" does not split a sentence
and the period of "Main St. It is nice." does. A model that supplies this analyzer should keep
`ISpeechModel.NormalizeText` as the identity (the default), because the analyzer already describes how abbreviations and
numbers are spoken and two normalizations would disagree.

## The Annotations

There are six annotation kinds. Each has a typed reader on `TextEnAnnotations`; a reader returns `false` for another
kind or a malformed value and never throws. A backend ignores every kind it does not recognize.

| Kind | Reader | Value | Meaning |
| --- | --- | --- | --- |
| `en.upos` | `TryReadPartOfSpeech` | `NOUN`, `VERB`, ... | The Universal Dependencies part of speech of a word |
| `en.norm` | `TryReadReplacement` | plain words | Speak these words instead of the covered text |
| `en.spell` | `TryReadReplacement` | `F B I` | Speak these letters by letter name |
| `en.pron` | `TryReadPronunciation` | `ipa:...`, `arpabet:...` | Phonemes for the covered word |
| `en.reading` | `TryReadReading` | `record.noun` | The reading id of a heteronym |
| `en.reading-confidence` | `TryReadReadingConfidence` | `0.000` to `1.000` | How sure the model is |

All offsets are UTF-16 indices into the text the analyzer received. `en.norm` and `en.spell` are the *replacement*
annotations. They are ascending, never overlap each other, and never start or end on whitespace, so a backend can apply
them as one ordered sequence. `en.pron` marks a word that is **not** replaced. `en.upos` is independent of everything
else and may overlap any other annotation.

**What gets annotated.** A word gets `en.upos` (punctuation and symbols do not). Normalization annotates only what is
spoken differently from how it is written:

- abbreviations (`Dr.` is `Doctor`, `St.` is `Saint` or `Street` from the words around it, `Jan.` is `January`)
- symbols (`&` is `and`, the copyright sign is `copyright`) and typographic apostrophes in contractions (which are
  canonicalized to the plain `'`)
- initialisms, as `en.spell` (`FBI` is `F B I`, `U.S.` is `U S`, a glued meridiem such as the `pm` of `5pm` is `P M`)
- en-US numbers: cardinals, thousands separators, negatives, decimals, ordinals, percentages, fractions, years and
  decades, currency (`$5.50` is `five dollars and fifty cents`), a closed list of units (`5 kg`, `72°F`, `205 kPa`; the
  units written like a word or a letter, such as `in`, `m` and `s`, only where nothing else can stand:
  `10 cm (4 in) tall`, `5 m x 5 m`), dimensions (`2 x 4`, `1920x1080` with `by`), `~1000` as
  `approximately one thousand`, clock times, numeric and month-name dates, ranges, phone numbers and dotted numbers
  (`1,234` is `one thousand two hundred thirty four`, `1990s` is `nineteen nineties`), the number of a hyphenated
  compound (`12-year-old`), and a number with a glued capital letter or `k` (`10K` is `ten K`, `5G`, `3D`)
- web addresses: a URL with an `http`, `https` or `ftp` scheme or a leading `www`, an e-mail address and a bare domain
  with a well-known top-level domain are spoken with their separators as words (`john.smith@example.com` is `john dot
  smith at example dot com`; the scheme is spelled `H T T P S`, `www` is spelled, and the two slashes after the scheme
  are dropped); the words inside an address are left for the phonemizer
- letter-digit identifiers (`A4` is `A four`, `MP3`, `H2O`, `R2D2`) and a name joined to a number by a hyphen
  (`COVID-19`, `Catch-22`, `SCP-093-T2` is `S C P zero nine three T two`), keeping a possessive on the last word
- bracketed asides that are not meant to be read: an editorial `(?)` or `(!)`, and a numeral that restates the number
  before it (`One (1)` is read once)
- invisible formatting characters such as a soft hyphen or a zero-width space, as an `en.norm` annotation with an empty
  value ("speak nothing") rather than a silent loss

The value of a numeric annotation is plain words separated by single spaces, with no hyphen, comma or digit (`twenty
four`, never `twenty-four`); the words of one construct are separate annotations (`January 5, 2024` annotates the `5`
and the `2024` and leaves the month name and the comma alone, and `$5 million` annotates `$5` as `five` and `million` as
`million dollars`), so apply each annotation on its own range.

## Consuming the Annotations in a Backend

To get the spoken text of a chunk, collect the replacement annotations, sort them by `Start`, copy the text between them
unchanged, and insert each `Value` in place of its range. A value already carries any single separating space it needs,
so insert nothing else, and an empty value means "speak nothing". When `TryReadReplacement` reports `isSpelled`, speak
each capital letter by its **letter name**, never as a word: a lone `A` in `A T and T` is a letter, not the article.
Only `en.spell` means spell out; an `en.norm` value is always spoken as words, whatever letters it holds, so never infer
spelling from its shape.

```csharp
var replacements = new List<(int Start, int Length, string Value, bool IsSpelled)>();
foreach (var annotation in annotations)
{
    if (TextEnAnnotations.TryReadReplacement(annotation, out var spoken, out var isSpelled))
    {
        replacements.Add((annotation.Start, annotation.Length, spoken, isSpelled));
    }
    else if (TextEnAnnotations.TryReadPronunciation(annotation, out var scheme, out var phonemes))
    {
        // The word keeps its text. If the backend supports `scheme` (Ipa or Arpabet), speak `phonemes` for it
        // instead of the backend's own letter-to-sound result; otherwise speak the word as usual.
    }
    else if (TextEnAnnotations.TryReadReading(annotation, out var readingId))
    {
        // For example "record.noun"; the matching "en.reading-confidence" annotation covers the same range.
    }
}

replacements.Sort((a, b) => a.Start.CompareTo(b.Start));
// Copy the gaps unchanged, insert each Value in place of its range, and spell the IsSpelled values by letter name.
```

A few rules make this safe to implement:

- A word is either replaced or pronounced, never both, and a word that has a reading has neither (the reading is
  emitted only for a word that is spoken as written), so a backend never reconciles two decisions for one word.
- A number the analyzer cannot read is not annotated, so a backend must still be ready to receive digits.
- A backend that cannot use a reading id or a phoneme scheme speaks the word as it would without the annotation.

## Options and Diagnostics

`EnglishTextAnalyzerOptions` configures the analyzer:

| Option | Default | Effect |
| --- | --- | --- |
| `Annotations` | all | `TextEnAnnotationSelection` flags `PartOfSpeech`, `Normalization` and `Reading` |
| `Overrides` | none | A dictionary of pronunciation overrides; needs `Normalization` |
| `LowConfidenceThreshold` | `0.60` | A reading below it is counted as low confidence (still emitted) |
| `Diagnostics` | none | An `ISpeechDiagnostics` sink that receives counts, never text |

Normalization works on the tokens alone and does not load the tagger, so selecting only `Normalization` is the cheap
option. The first request that needs the tagger (part of speech or readings) loads about 1.3 MB of embedded data and is
slower than the rest; a host that wants that cost paid early can analyze one short text at start-up. The analyzer is
thread-safe, keeps no per-request state and never retains the text it analyzes. A non-empty `Overrides` dictionary with
`Normalization` not selected makes the constructor throw an `ArgumentException` instead of silently ignoring it.

The diagnostics sink receives counts only:

- `TextEn.Analysis`, once per analyzed text: `Analyzed one text: N sentences, N tokens, N annotations, N unknown words,
  N normalizations, N ambiguous readings, N numeric fallbacks, N overrides applied, N overrides ignored.` An *ambiguous
  reading* is an abbreviation or number read by a documented default, a *numeric fallback* is a number left as written
  (or read digit by digit) because of a limit, and the override counts say what your dictionary changed. The message is
  informational; do not parse it.
- `TextEn.Heteronym` (level `Info`), when readings were assigned: how many were classified, had a single candidate, came
  from an override, or fell below the threshold.
- `TextEn.Model` (level `Warning`), once per analyzer, only when the heteronym model is unusable; the analysis then
  continues without classified readings.

No event contains a word, a spoken replacement, a reading id or any content of a dictionary. `AnalyzeDetailed` returns
the per-word data (tokens, normalizations, clitic roles, readings) to the caller instead of a sink.

## Pronunciation Overrides

No built-in table knows a product name, a brand or a person's name, and some words are read wrongly by any general rule.
A host corrects them with a **pronunciation override dictionary**: whole words, each with an action, applied before the
built-in rules. Overrides work by annotation only, so every offset stays valid.

| Action | TSV | Payload | What the word gets |
| --- | --- | --- | --- |
| `Skip` | `skip` | none | Spoken as written; no built-in rule runs for it |
| `Say` | `say` | plain words | One `en.norm` annotation with the payload |
| `SpellOut` | `spell` | none | One `en.spell` annotation with its letters |
| `Pronunciation` | `pron` | `ipa:...` or `arpabet:...` | One `en.pron` annotation; the word is not replaced |
| `Reading` | `reading` | a reading id | `en.reading` with confidence `1.000`; the model is not asked |

Build the dictionary once in code (it is immutable and thread-safe) or load it from a tab-separated file that someone
who is not a programmer maintains:

```csharp
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;

var overrides = PronunciationOverrides.Create(
[
    PronunciationOverrideEntry.Say("Nginx", "engine ex"),                   // case-insensitive by default
    PronunciationOverrideEntry.Say("Kubernetes", "koo ber net eez", caseSensitive: true),
    PronunciationOverrideEntry.SpellOut("GIF", caseSensitive: true),
    PronunciationOverrideEntry.Pronounce("Polish", PronunciationScheme.Ipa, "ˈpoʊlɪʃ", caseSensitive: true),
    PronunciationOverrideEntry.Skip("NASA", caseSensitive: true),
    PronunciationOverrideEntry.Reading("record", "record.noun"),
]);

// Or from a file: PronunciationOverrides.FromTsv(File.OpenText("pronunciations.tsv"))
var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = overrides });
```

The file has one entry per line, `word`, `case` (`sensitive` or `insensitive`), `action` and a payload for `say`, `pron`
and `reading`, separated by tabs (shown here as `→`); empty lines and lines starting with `#` are ignored:

```text
# domain terms
Nginx→insensitive→say→engine ex
Kubernetes→sensitive→say→koo ber net eez
GIF→sensitive→spell
Polish→sensitive→pron→ipa:ˈpoʊlɪʃ
NASA→sensitive→skip
record→insensitive→reading→record.noun
```

With these entries `Use Nginx today.` is spoken `Use engine ex today.`, `A GIF file` is spoken `A G I F file`,
`Polish food` keeps its text and gives the backend the phonemes, and `NASA` stays as written.

- **Whole words only.** An entry matches one whole word, never part of a word and never several words (a key cannot
  contain a space). The text is split the way the tagger splits it, so `NASA's` matches the key `NASA`, while the
  hyphenated `Nginx-based` is one word that matches only as a whole.
- **A key cannot end with a period.** `Dr.`, `Mr.` and `U.S.` belong to the built-in abbreviation rules.
- **Case.** A case-sensitive entry beats a case-insensitive one for the same word; matching never depends on the
  machine's culture (`nginx`, `Nginx` and `NGINX` all match `Nginx`, on a Turkish computer too). Prefer case-sensitive
  entries for names and acronyms (`US` and `us`).
- **Precedence.** An override beats every built-in rule for its word. The one exception is a word that an *earlier*
  match of a number or unit has already used: with an entry for `kg`, `5 kg` is still `five kilograms` (the override is
  counted in `overrides ignored`), while a `kg` on its own is overridden.
- **Sentence breaks never change.** An override changes how a word is spoken, never where a sentence ends.
- **Errors.** `Create` throws an `ArgumentException` for the first bad entry (`Entry 3: <reason>.`) and `FromTsv` an
  `InvalidDataException` (`Line 4: <reason>.`); the reason is a fixed text and never the word or the payload, because a
  dictionary can hold names that must not reach a log. A dictionary holds at most 10,000 entries.
- **Phonemes.** The schemes `ipa` and `arpabet` are a closed list; the library checks only the syntax of the phonemes,
  and whether a symbol is in your backend's inventory is for the backend to decide.

## Heteronym Readings

A heteronym is a word whose pronunciation depends on its meaning, such as the noun and the verb of "record". The
analyzer marks each such word of its inventory (207 words with 395 readings, US English) with two annotations on the
same range: `en.reading`, a *reading id* such as `record.noun` or `record.verb`, and `en.reading-confidence`. A small
trained model (about 250 KB) that ships inside the package chooses the reading from the words around the heteronym and
from the part of speech, one sentence at a time; there is nothing to download or configure, and the readings are part of
the default selection. `ReadingInventory.Default` lists the reading ids and the forms they can carry, so a host can
validate its own.

The reading never overlaps a replacement or a pronunciation, and a word has at most one. A reading below
`LowConfidenceThreshold` is counted and flagged (`AnalyzedToken.IsLowConfidenceReading` in the detailed result) but
still emitted, so a backend that wants to ignore uncertain readings compares the confidence itself. A host that needs a
specific reading for a word fixes it with a `Reading` override, which beats the model. If the model is present but
unusable, the readings are disabled and one warning is reported.

## Accuracy

The figures below are agreement with labeled readings, measured on held-out sentences that were never used to train the
model or to choose any setting. A word that gets no reading counts as wrong. They are the committed evaluation records
`docs/evaluation/text-en/` (internal sets) and `docs/evaluation/text-en/external/` (external sets).

| Held-out set | Words | Model | Part of speech alone | Most common reading |
| --- | ---: | ---: | ---: | ---: |
| Natural sentences (266 documents) | 1,167 | 96.2% | 90.7% | 82.0% |
| Sentences written blind by a different model family | 1,972 | 95.4% | 91.2% | 51.4% |
| External: Wikipedia homographs (WHD `eval`) | 1,615 | 93.0% | 90.6% | 69.2% |
| External: heteronym-bench (299 sentences) | 299 | 93.3% | 80.3% | 40.1% |

"Part of speech alone" is the reading the tag of the word decides, and "most common reading" is the reading the form has
most often in the training data. The 95 percent intervals of the external figures are 91.7 to 94.2 percent (WHD) and
90.3 to 96.0 percent (benchmark). Five training runs that differ only in the random seed give 95.5 to 97.0 percent on
the natural set, 95.4 to 97.1 percent on the independent one, 92.4 to 93.0 percent on WHD and 92.6 to 97.0 percent on
the benchmark, which shows how little 299 sentences can tell.

These numbers come with limits that you should know:

- **The labels are not human-verified.** The internal labels were chosen by a rule from the part of speech or by a
  language model and audited blind by a second model; no person reviewed them. The accuracy is agreement with those
  labels, not a listener's judgment of the pronunciation, and the phoneme error rate is not measured.
- **The natural set flatters the tagger.** Part of its sentences come from the data on which the part-of-speech tagger
  was itself trained. The set written by a different model family never reached the tagger and agrees within about one
  point, but it is synthetic text.
- **The gain over the tag comes from the hard cases.** Where the reading follows from the part of speech the tag alone
  is already right, and the model is sometimes worse there: "read" in the past tense is right for 11 of 17 natural
  sentences where the tag alone gets all 17. The improvement over the tag (about 5.6 points on the natural set, 95
  percent interval 3.2 to 8.2) comes from words whose reading depends on meaning.
- **Some readings are weak.** On the independent set, "bass" the fish, "sewer" the seamstress, "number" the noun,
  "August" the month, "bow" the weapon, "frequent" the verb and "put" are right in at most two of five sentences.
- **The external figures are lower and are informational.** The text is Wikipedia prose or a language-model-written
  benchmark, the labels follow another convention (our mapping of their ids to our readings is ours), and the external
  sets cover only the 162 homographs of WHD (which include the benchmark's 30 words), not the other 45 words of the
  inventory. They never gate a build, and the datasets themselves are never part of the repository.
- **Confidence is informative.** At a confidence of 0.90 or more the reading was right for 99.3 percent of the natural
  words (1,035 of 1,042); below 0.60 only about half were right (10 of 21 in the natural set, 30 of 53 in the other).

## Limitations

- **US English only.** The lexicons, the spelling of the spoken forms and the number conventions are those of written US
  English: a comma separates thousands, the period is the decimal point and a numeric date is month first. Text in other
  conventions (`1,23`) is left as written and counted.
- **Not read yet; passed through unchanged.** Roman numerals (`World War II`), names before a number (`mid-1990s`),
  prefix currency codes (`USD 5`), `h:mm:ss` times, ratios, `1.5M`, feet and inches written with primes (`5'10"`), the
  ampere, kelvin, newton and watt letters (`5 A`, `300 K`, `5 N`, `60 W`), non-ASCII digits, file names and
  dotted names such as `report.pdf` or `Node.js`, a domain with a top-level domain outside the short list, and markup
  (the address of a Markdown link is spoken after its text). A backend must be ready to receive digits.
- **Number limits.** A cardinal has at most 15 digits; 16 to 64 digits are read digit by digit; longer runs, tokens over
  128 characters and decimals with more than 32 fraction digits are left as written. Each case is counted as a numeric
  fallback.
- **Ambiguous numbers use documented defaults.** A bare `1999` is a year and a bare `1066` a quantity, `3/4/2024` is
  March fourth and `7/16` a fraction; each default is counted so you can see how often the analysis had to guess.
- **One sentence at a time.** The reading model sees only its own sentence, and a heteronym that is not in the inventory
  gets no reading.
- **Retained text.** The analyzer keeps no reference to analyzed text. The tagger it uses keeps the characters of the
  words it tags in private per-thread buffers while it works; the package pins a tagger version that clears them when
  each call ends, including when it throws. Managed strings that the tagger creates itself (for example the normalized
  copy of a non-ASCII word) cannot be cleared, so a host with a strict memory-forensics requirement should account for
  that.
- **No throughput claim.** The analysis is linear in the length of the text, but no speed figure is made or verified.

## Reproducing the Numbers

The training data, the trainer and the evaluation records are in the repository folders `data/text-en/`,
`tools/DemaConsulting.Speech.Text.En.Trainer/` and `docs/evaluation/text-en/`. A maintainer can verify the internal
record and, with network access once, the external one:

```bash
dotnet run -c Release --project tools/DemaConsulting.Speech.Text.En.Trainer -- evaluate --check
dotnet run -c Release --project tools/DemaConsulting.Speech.Text.En.Trainer -- evaluate-external --fetch --write
```

The external datasets are fetched into a folder that version control ignores, from pinned revisions verified file by
file, and nothing of them is committed or shipped.
