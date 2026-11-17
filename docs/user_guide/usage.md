# Usage

## Current Capabilities

The library provides the following building blocks, starting with the `Diagnostics` and
`AudioSubsystem` foundations (recognition and synthesis are covered in later sections):

- **`ISpeechDiagnostics`** (`DemaConsulting.Speech.Diagnostics`): a contract for reporting
  structural diagnostic events to an optional host-supplied sink. `NullSpeechDiagnostics`
  provides the default no-op implementation via `NullSpeechDiagnostics.Instance`.
- **`AudioDeviceFactory`** (`DemaConsulting.Speech.AudioSubsystem`): the composition entry point
  for creating audio capture/playback devices and probes. When PortAudio initializes
  successfully, it returns real devices backed by PortAudio; otherwise it falls back to honest
  unavailable implementations. Its `RefreshDevices()` method forces PortAudio to re-scan its
  device table, so hardware attached or removed after start-up becomes visible to devices and
  probes created afterward; already-created device instances are not re-resolved by a refresh,
  so calling `Start()` on one afterward throws `AudioDeviceUnavailableException` and it must be
  replaced via `AudioDeviceFactory.CreateCaptureDevice()`/`CreatePlaybackDevice()` before starting
  again. This is the one deliberate exception to the library's "never throws at composition" policy: it throws
  `AudioDeviceInUseException` if any device created from the factory currently has an open or
  started stream, and the caller must stop that device before retrying.
- **`AudioDeviceDescription`** and **`AudioDeviceSelection`**: immutable, name-only
  representations of an audio device and a persistable user device preference.

The library also provides the storage and download machinery of the `ModelManagementSubsystem`
(`DemaConsulting.Speech.ModelManagementSubsystem`), used to fetch and install speech models on
a per-user basis:

- **`SpeechModelStore`**: per-user model storage rooted under `LocalApplicationData` by default
  (or a host-configured override via `SpeechModelStoreOptions`). Installs are atomic: a
  replacement is only swapped into `current/` after being fully staged and verified, so a
  failed repair never disturbs a working installation. Also provides honest `IsInstalled(...)`
  queries and `Uninstall(...)`.
- **`SpeechModelDownloader`**: allows unlimited concurrent downloads across different models,
  while serializing concurrent download attempts for the same model id so `SpeechModelStore`'s
  atomic install is never raced; fetches each file via an injected `IModelDownloadClient`
  (`HttpModelDownloadClient` by default), verifies each file's SHA-256 checksum, and only then
  hands the verified files to `SpeechModelStore` for atomic install. A corrupted, partial, or
  checksum-mismatched download is never marked installed.
- **`SpeechModelDownloaderOptions`** / **`DownloadMirror`**: optional configuration letting a host
  redirect every model download through an internal, possibly-authenticated HTTPS mirror instead
  of each model's own hardcoded source URI - see below.
- **`SpeechModelDownloadDescriptor`** / **`SpeechModelDownloadFile`**: describe the HTTPS URL(s)
  and expected SHA-256 checksum(s) for a model's downloadable files.
- **`SpeechModelDownloadProgress`**: reports per-file transfer progress, suitable for
  `IProgress<T>` consumption by a GUI progress bar.

Typical model download composition:

```csharp
using DemaConsulting.Speech.ModelManagementSubsystem;

var store = new SpeechModelStore();
var downloader = new SpeechModelDownloader(store);

var descriptor = new SpeechModelDownloadDescriptor(
[
    new SpeechModelDownloadFile(
        new Uri("https://example.com/model.bin"),
        "…64-character SHA-256 hex checksum…",
        "model.bin")
]);

await downloader.DownloadAsync("my-model", descriptor);
```

A host whose network policy blocks the public hosts a model's own declared URI points at (for
example a TLS-interception policy breaking `huggingface.co`) can redirect every model download
through an internal mirror it controls instead, with no change to any model's own declared
download descriptor:

```csharp
var mirror = new DownloadMirror(
    new Uri("https://models.internal.example.com/mirror"),
    bearerToken: "…token…"); // or: credentials: new NetworkCredential("user", "pass")

var options = new SpeechModelDownloaderOptions { Mirror = mirror };
var mirroredDownloader = new SpeechModelDownloader(store, client: null, diagnostics: null, options: options);

// Every file resolves to https://models.internal.example.com/mirror/my-model/model.bin
// and carries the configured bearer token/credentials, instead of the file's own declared URI.
await mirroredDownloader.DownloadAsync("my-model", descriptor);
```

`DownloadMirror.Credentials` and `DownloadMirror.BearerToken` are mutually exclusive - supply at
most one. A `DownloadAsync` call that fails (beyond a checksum mismatch) reports one of
several honest outcomes rather than one generic `Failed`, so a host can react to *why* it failed:
`NetworkBlocked` (a TLS/certificate failure, a DNS/connection failure, or a timeout - the shape a
blocked or intercepted network path typically produces, and a strong signal to try configuring a
`DownloadMirror`), `HttpError` (a real non-success HTTP response, e.g. 404/403/5xx), `IoFailure`
(a local disk/permission failure), or the final generic `Failed` fallback for anything else - with
the underlying exception always available in `SpeechModelDownloadResult.Error`.

The library also provides the model catalog/contract seam of the `ModelManagementSubsystem`,
describing *which* models are available and their tunable parameters:

- **`ISpeechModel`** (with **`IRecognitionModel`**/**`ISynthesisModel`** role markers): the
  common per-model contract — identity, `Role`, declared `Parameters`, declared
  `AudioTagSupport`, and `DownloadDescriptor`.
- **`ISpeechModelParameter`** (with **`NumericParameter`**, **`ChoiceParameter`**, and
  **`BooleanParameter`**): typed, self-validating tunable-parameter descriptors a host can use
  to render an appropriate generic control (slider, dropdown, checkbox) for any model.
- **`SpeechModelDescriptor`**: pairs one `ISpeechModel` with its current `SpeechModelState`
  (`NotDownloaded`, `Downloading`, `Downloaded`, or `FailedOrCorrupt`).
- **`SpeechModelCatalog`**: enumerates the library's known/compiled-in models alongside each
  one's current install state, and orchestrates downloading a known model by id.
  `DownloadAsync` is safe to call unconditionally on every launch - it's a cheap no-op once a
  model is installed, and otherwise returns `ChecksumMismatch`, `NetworkBlocked`, `HttpError`,
  `IoFailure`, or `Failed` on a real download problem (with the underlying exception in
  `SpeechModelDownloadResult.Error`), or throws `ArgumentException` for an unrecognized model id.

The library provides **seven production model classes** across both roles it defines, via four model
packages. `DemaConsulting.Speech.Sherpa` supplies four through `.AddSherpaModels()`:
`SherpaOnnxZipformerEnRecognitionModel`, `SherpaOnnxNemotronStreamingEnRecognitionModel`,
`SherpaOnnxVitsLibriTtsEnglishSynthesisModel` and `SherpaOnnxKokoroEnglishSynthesisModel`.
`DemaConsulting.Speech.Onnx.Kokoro` supplies `OnnxKokoroEnglishSynthesisModel` through
`.AddKokoroModels()`, `DemaConsulting.Speech.Onnx.NemotronStt` supplies
`OnnxNemotronMultilingualRecognitionModel` through `.AddNemotronSttModels()`, and
`DemaConsulting.Speech.Onnx.ChatterboxTurbo` supplies `OnnxChatterboxTurboEnglishSynthesisModel`
through `.AddChatterboxTurboModels(options)`. The catalog/contract seam, together with each model's
own `CreateBackend(...)` and (for synthesis) `CapabilityProfile` members, lets a host build a
model-settings-style UI and compose a real recognizer/synthesizer against any of them. See
"Recognizing Speech" and "Synthesizing Speech" below for each model's identity and license.

A model may also unpack its own downloaded archive before it becomes usable: `ISpeechModel`
declares an `InstallAsync(stagedFilesDirectory, cancellationToken)` hook, defaulting to a no-op
for the common case of a single directly-usable downloaded file, but overridable by a model
whose declared payload is a zip/tar archive. `SpeechModelDownloader`/`SpeechModelCatalog` invoke
this hook on the verified staged files before they are atomically installed, so this unpacking
never touches an existing installation until it succeeds.

Typical catalog usage:

```csharp
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Sherpa;

var catalog = new SpeechModelCatalog().AddSherpaModels();

foreach (var descriptor in catalog.Enumerate())
{
    Console.WriteLine($"{descriptor.DisplayName}: {descriptor.State}");
}
```

Typical capture composition:

```csharp
using DemaConsulting.Speech.AudioSubsystem;

var factory = new AudioDeviceFactory();
var device = factory.CreateCaptureDevice(AudioDeviceSelection.SystemDefault);
// Optionally pass a preferred AudioFormat when you already know the target format.

if (device.IsAvailable)
{
    device.FrameCaptured += (_, args) =>
    {
        // args.Samples contains normalized float audio samples.
    };

    device.Start();
}
```

### Text analysis for model authors

A model author whose engine needs sentence-level context - for example a phoneme-driven engine that
chooses a word's pronunciation from its part of speech - can opt in to a language-neutral text
analysis seam. It is entirely optional: a model that declares nothing, and a backend that does not
accept annotations, behave exactly as they always have.

- **`ISynthesisModel.TextAnalyzer`** is an optional `ITextAnalyzer` that defaults to `null` (no
  analysis). The library calls `Analyze(text, cancellationToken)` **once per `SpeakAsync` or
  `SynthesizeAsync` request**, before the text is split into chunks, with the whole request text
  after `ISpeechModel.NormalizeText`. Return a shared, cheap instance, because the member is read
  once per request; the analyzer runs on a thread-pool thread (never the thread that called
  `SpeakAsync`/`SynthesizeAsync`, so a slow analyzer cannot freeze a UI thread), so it must be
  thread-safe or stateless per call, must not assume thread affinity (no `ThreadStatic` state, no UI
  or synchronization-context access), must honor the token (an analyzer that ignores it delays stop
  and dispose until it returns), and must not retain `text`.
- **`TextAnalysisConventions.TagPlaceholder`** (the Unicode object replacement character) stands for
  each recognized audio tag in
  the text you analyze, one placeholder per tag; bracket text that is not a recognized tag stays
  literal. Treat the placeholder as an opaque, non-letter, non-word object that is neither a
  sentence terminator nor part of any word, avoid covering it with an annotation, and feel free to
  place a sentence break before or after it.
- **`SegmentAnnotation(Start, Length, Kind, Value)`** marks a range of the text. Offsets are UTF-16
  code unit indices (ordinary C# string indices) and `Length` must be positive. `Kind` and `Value`
  are opaque to the library - you choose them, for example `"reading"` or `"pos"` - and a consumer
  must ignore kinds it does not recognize. An annotation should begin and end on a non-whitespace
  character. The library keeps an annotation only when it lies wholly inside one stretch of text
  between tags and, later, wholly inside one chunk; otherwise it is **dropped, never clipped or
  moved**. A chunk that consists only of punctuation or symbols (no letter or digit) is normally
  merged into the previous chunk, or dropped at the start of the text, but it is kept as its own
  chunk, with its annotation, when an annotation with a non-empty `Value` lies wholly on it (for
  example a symbol your analyzer maps to a word); an annotation with an empty `Value` does not keep
  it, and text without any annotations is chunked exactly as before.
- **`TextAnalysisResult(annotations, sentenceBreaks)`** carries the annotations and optional
  sentence boundaries. A `null` `SentenceBreaks` means "no opinion", so the default punctuation
  heuristic splits sentences; any non-null list, even an empty one, is authoritative and replaces
  that heuristic. A break offset `k` means a sentence ends immediately before index `k`. Returning
  `null` or `TextAnalysisResult.Empty` means no analysis.
- **`IAnnotatedSynthesisBackend`** extends `ISynthesisBackend` with
  `GenerateAnnotated(text, annotations, speed, speakerId)`. The session calls it, instead of
  `Generate`, only for a chunk that has at least one annotation. The offsets are relative to the
  `text` argument, and a backend must still produce audio for `text` if it ignores every annotation.
  A backend that only implements `ISynthesisBackend` never sees annotations.

A failure in your analyzer never faults a session: the request continues without annotations and a
single warning that names only the exception type (never the message or the text) is reported
through the diagnostics sink. A cancellation requested while the analyzer runs is honored.

```csharp
using DemaConsulting.Speech.SynthesisSubsystem;

public sealed class ReadingAnalyzer : ITextAnalyzer
{
    public TextAnalysisResult Analyze(string text, CancellationToken cancellationToken)
    {
        var annotations = new List<SegmentAnnotation>();
        var index = text.IndexOf("read", StringComparison.Ordinal);
        if (index >= 0)
        {
            // Opaque kind and value: this model's backend decides what "reading" means.
            annotations.Add(new SegmentAnnotation(index, "read".Length, "reading", "red"));
        }

        return new TextAnalysisResult(annotations, null);
    }
}

// In your ISynthesisModel implementation:
//     private static readonly ITextAnalyzer Analyzer = new ReadingAnalyzer();
//     public ITextAnalyzer? TextAnalyzer => Analyzer;

public sealed class MyBackend : IAnnotatedSynthesisBackend
{
    public int SampleRate => 24000;

    public BackendAudio Generate(string text, float speed, int speakerId) =>
        GenerateAnnotated(text, [], speed, speakerId);

    public BackendAudio GenerateAnnotated(
        string text, IReadOnlyList<SegmentAnnotation> annotations, float speed, int speakerId)
    {
        foreach (var annotation in annotations)
        {
            if (annotation.Kind != "reading")
            {
                continue;
            }

            var word = text.Substring(annotation.Start, annotation.Length);
            // Use annotation.Value to pick the pronunciation of `word`.
        }

        // Synthesize `text` with your engine and return its samples at SampleRate.
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        // Release the loaded engine.
    }
}
```

**English text analysis (preview; not yet published to NuGet).** Instead of writing your own analyzer, a model for
English can return the `EnglishTextAnalyzer` of the optional, pre-release `DemaConsulting.Speech.Text.En` package (it
depends on the pre-release `DemaConsulting.PartOfSpeech` tagger). Its analysis gives each word one annotation of kind
`"en.upos"` whose value is the word's Universal Dependencies part of speech (for example `"NOUN"`), skips punctuation
and symbols, and supplies authoritative sentence boundaries so an abbreviation such as "Dr." does not split a sentence.
By default it also normalizes the text: each abbreviation, symbol, apostrophe, initialism, or number it can read (for
example "Dr.", "&", a typographic apostrophe, "FBI", or "$5.50") gets one annotation whose range is the original text
and whose value is the words to speak instead: kind `"en.norm"` for words, and kind `"en.spell"` for a sequence of
letters that is spoken by letter name (an initialism such as "FBI" gives the value `"F B I"`; see *Spelled letters*
below). Adjacent marks are spoken as separate words (`A™®B` as "A trademark registered B"), and a name suffix stays in
one sentence with the credentials that follow it ("John Smith Jr. Ph.D. spoke."). A host can also override how
individual words are spoken or pronounced with a dictionary of *pronunciation overrides* (see *Pronunciation overrides
for English text analysis* below).

*What is read.* The numeric normalization reads en-US text only (a comma separates thousands, a period is the
decimal point, and a numeric date is month first): cardinal numbers and numbers with thousands separators
("1,234" is "one thousand two hundred thirty four"), negatives, decimals ("3.5" is "three point five"),
ordinals ("22nd" is "twenty second"), percentages, fractions ("3 1/2" is "three and one half"), years and
decades ("in 1999" is "in nineteen ninety nine", "1990s" is "nineteen nineties"), currency (the symbols
`$ € £ ¥` and the codes USD, EUR, GBP, and JPY: "$5.50" is "five dollars and fifty cents"), a closed list of
units ("5 kg" is "five kilograms", "72°F" is "seventy two degrees Fahrenheit", "6 ft."), clock times ("5:30pm"
is "five thirty P M", "17:00" is "seventeen hundred"), dates ("12/25/2024" and "January 5, 2024"), ranges,
phone numbers and dotted numbers ("5-10" is "five to ten", "555-1234" is read digit by digit; an en dash is read
as "to" only when both numbers around it are read, so "5–10" is "five to ten" but "1–2D" keeps its dash), and
the month and weekday abbreviations ("Jan. 5", "Sat."). A period after a full month name such as "May." ends the
sentence and gives the next number no date reading, while the abbreviation period of "Jan. 5" does not. Only the
ASCII digits are numbers. Where the words around a
number do not decide its reading, a documented default is used and counted (a bare "1999" is a year and a bare
"1066" a quantity, "3/4/2024" is March fourth, "7/16" is a fraction); a number beyond the limits (a cardinal of
at most 15 digits, 16 to 64 digits read digit by digit, anything longer, or a token of more than 128 characters,
left as written) or written with another locale's separators ("1,23") is left as written. These are not read
yet and pass through unchanged: Roman numerals ("World War II"), hyphenated compounds with digits ("5-year-old",
"F-16"), prefix currency codes ("USD 5"), "h:mm:ss" times and ratios, `10k` without a currency symbol, URLs,
e-mail, and markup.

*Spoken-text conventions.* The value of every number annotation is plain lowercase words separated by single
spaces, with no hyphens, commas, or digits ("twenty four", never "twenty-four"). The apostrophe appears only in
"o'clock", and capital letters only in month names, "Fahrenheit" and "Celsius", and the separate `"en.spell"`
annotation of a meridiem ("P M" and "A M", see *Spelled letters*). A value
is at most 1,024 characters long. The words of one construct belong to separate annotations: "January 5, 2024"
annotates the "5" and the "2024" and leaves the month name and the comma alone, and "$5 million" annotates
"$5" as "five" and "million" as "million dollars"; apply each annotation on its own range. A number the
analyzer cannot read is not annotated, so a backend must still be prepared to receive digits.

Read an annotation with `TextEnAnnotations.TryReadPartOfSpeech`, `TextEnAnnotations.TryReadReplacement` (the
`"en.norm"` and `"en.spell"` annotations), or `TextEnAnnotations.TryReadPronunciation` (the `"en.pron"`
annotations of the overrides), and ignore the other kinds in `TextEnAnnotations`, which are reserved for
later releases (`"en.reading"` and `"en.reading-confidence"`) or that your backend does not use. The analyzer is
thread-safe and cheap to share; the first request that needs tags loads the tagger (about 1.3 MB of embedded
data), so that request is slower than the rest. `EnglishTextAnalyzerOptions` selects the annotation families
with the `TextEnAnnotationSelection` flags (`PartOfSpeech` and the new `Normalization`, both on by default;
numbers are normalized whenever `Normalization` is selected, there is no separate option), carries the optional
`Overrides` dictionary,
and supplies a diagnostics sink, which receives only counts, never text: one event per analyzed text with the
message `Analyzed one text: N sentences, N tokens, N annotations, N unknown words, N normalizations, N
ambiguous readings, N numeric fallbacks, N overrides applied, N overrides ignored.` The "ambiguous readings"
count covers abbreviations and numbers that
were read by a default, "numeric fallbacks" the numbers that were left as written or read digit by digit
because of a limit, and "overrides applied" and "overrides ignored" the words your override dictionary changed
and the entries that matched a word but were overruled (see *Overrides that are ignored*); the message is
informational and is not meant to be parsed. It never contains a word, and never any content of your
dictionary.

A backend applies the replacement annotations (`"en.norm"` and `"en.spell"`) of a chunk in four steps: read
each with `TextEnAnnotations.TryReadReplacement`, sort them by `Start` (they never overlap), copy each gap of
the original text between them unchanged, insert each annotation's `Value` in
place of its range, and speak the result. A value that is an empty string means the range is spoken as
nothing, and a value already carries any single space the replacement needs, so the backend adds none. When
`TryReadReplacement` reports `isSpelled`, speak each capital letter of the value by its letter name, never as a
word (see *Spelled letters*). Only `isSpelled` (the `"en.spell"` kind) means spell out: the value of an
`"en.norm"` annotation is always spoken as words, whatever letters it holds, so never infer spelling from an
`"en.norm"` value.

*Preview note: retained text; not yet published to NuGet.* The analyzer itself keeps no reference to the
text it analyzes after a call returns, but the pre-release `DemaConsulting.PartOfSpeech` tagger
(0.1.0-beta.4) keeps the characters of analyzed words in a private per-thread buffer that it never clears,
so analyzed words can remain in process memory after the request. That currently violates the core
`ITextAnalyzer` contract, which requires an analyzer not to retain `text`
([upstream issue](https://github.com/demaconsulting/PartOfSpeech/issues/29)). For that reason the
`DemaConsulting.Speech.Text.En` package is **not yet published to NuGet**: it will not be published until
the tagger is fixed and its version pin is updated. Until then it can be built from source only, and a
host that must guarantee that no spoken text outlives a request must not rely on it.

```csharp
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;

// In your ISynthesisModel implementation:
//     private static readonly ITextAnalyzer Analyzer = new EnglishTextAnalyzer();
//     public ITextAnalyzer? TextAnalyzer => Analyzer;

// In your IAnnotatedSynthesisBackend.GenerateAnnotated:
foreach (var annotation in annotations)
{
    if (TextEnAnnotations.TryReadPartOfSpeech(annotation, out var partOfSpeech))
    {
        var word = text.Substring(annotation.Start, annotation.Length);
        // Use partOfSpeech (for example "VERB") to pick the pronunciation of `word`.
    }
}
```

#### Pronunciation overrides for English text analysis

No built-in table knows a product name, a brand, or a person's name, and some words (an acronym that is
said as a word, an initialism that must be spelled) are read wrongly by any general rule. A host can correct
them with a **pronunciation override dictionary**: a list of whole words, each with an action, that the
analyzer applies before its built-in rules. Overrides are applied by annotation only: the analyzed text is
never rewritten, so every offset stays valid. The dictionary is part of the `DemaConsulting.Speech.Text.En`
preview package and has the same preview status as the rest of it.

**Spelled letters (`"en.spell"`).** An initialism (`FBI`), a dotted initialism (`U.S.`), a single-letter
initial (`J.`), a credential (`M.D.`), the letters of a meridiem (a separate `p.m.`, or the `pm` glued to a
number as in `5pm`) and the letters of an
ampersand initialism (`AT&T`, `R&D`, `Q&A`) are annotated `"en.spell"` rather than `"en.norm"`. The value is
single capital letters separated by single spaces (`"F B I"`), and the annotation is a promise: speak each
letter **by its letter name, never as a word**. That matters for a phoneme backend, because a lone `"A"` is
the letter in `"A T and T"`, not the article, and a lone `"I"` is not the pronoun. The spoken text is the
same as before. An ampersand initialism is split into letter annotations and an `"and"` annotation:
`AT&T's network` is annotated `AT` (`"A T"`), `&` (`" and "`, kind `"en.norm"`, with the padding spaces that a
value may carry) and `T` (`"T"`), and the `'s`
stays as written, so the replacements give `A T and T's network`. A meridiem glued to its time or hour
(`5:30pm`, `5pm`, `5PM`) is split inside its token into two adjacent annotations that never overlap: the number
or time as `"en.norm"` words and the meridiem letters as `"en.spell"`. `5pm` is annotated `5` (`"five"`, kind
`"en.norm"`) and `pm` (`" P M"`, kind `"en.spell"`, with the padding space that a value may carry), and
`5:30pm` is annotated `5:30` (`"five thirty"`) and `pm` (`" P M"`); a separate meridiem (`5 p.m.`) is already
its own `"en.spell"` annotation. The kind is the only signal: `"en.spell"` means spell out, and an `"en.norm"`
value is always spoken as words, even when it is a single capital letter (a host's `Say("foo", "I")` override
gives the word `"I"`), so a backend never infers spelling from an `"en.norm"` value.

**The four actions.** Each entry has a word, a case flag, an action and, for two actions, a payload:

| Action | Payload |
| --- | --- |
| `Skip` (`skip`) | none |
| `Say` (`say`) | plain words |
| `SpellOut` (`spell`) | none |
| `Pronunciation` (`pron`) | `ipa:...` or `arpabet:...` |

**Action details:**

- `Skip` (`skip`)
  - What the matching word gets: Spoken as written; no built-in rule runs for it and nothing is annotated
  - Example: `NASA` stays `NASA` (and `NASA's launch` stays as written)
- `Say` (`say`)
  - What the matching word gets: One `"en.norm"` annotation whose value is the payload
  - Example: `Use Nginx today.` is spoken `Use engine ex today.`
- `SpellOut` (`spell`)
  - What the matching word gets: One `"en.spell"` annotation: the word's letters in capitals
  - Example: `A GIF file` is spoken `A G I F file`
- `Pronunciation` (`pron`)
  - What the matching word gets: One `"en.pron"` annotation on the word; **the word is not replaced**
  - Example: `Polish food` keeps its text, and a backend that supports the notation receives the phonemes

A `Say` payload is plain words: letters, apostrophes, hyphens, and single spaces, with no digit and no
punctuation, so it can never change how a backend splits clauses (write a number in words). A selection of a
heteronym reading (the noun or the verb of "record") is reserved for a later release and is not an action yet.

**Precedence.** Several sources can decide how a word is spoken. The first row that applies wins:

| Rank | Source | Notes |
| --- | --- | --- |
| 1 | An entry with a case-sensitive match (exact characters) | `Polish` beats `polish` for the word `Polish` |
| 2 | An entry with a case-insensitive match | consulted only when no case-sensitive entry matches |
| 3 | Built-in rules: abbreviations, acronyms, initialisms, symbols, numbers | an override beats them all for its word |
| 4 | The part of speech | independent: an overridden word still gets its `"en.upos"` |

The one exception is a word that an *earlier* match of a number or unit has already used (see *Overrides
that are ignored*).

An override changes how a word is spoken, never where a sentence ends: the sentence breaks, and therefore the
chunk boundaries, are identical with and without the dictionary, even when the overridden word is an
abbreviation whose period the tokenizer splits off (`See Fig. 5 for details.` stays one sentence when `Fig` is
overridden). A title such as `Dr.` or `St.` is one token with its period and is never matched by a key (see
*How a word is matched*), so it is not affected at all.

**How a word is matched.**

- **Whole words only.** An entry matches one whole word of the text, never a part of a word and never several
  words together: an entry for `Ann` does not touch `Annual`, and an entry cannot contain a space (a phrase is
  overridden word by word). The characters before and after the word must not be a letter, a combining mark,
  or a digit.
- **Case.** A case-sensitive entry compares the exact characters. A case-insensitive entry ignores letter case
  using the invariant rules, never the machine's culture: `nginx`, `Nginx`, and `NGINX` all match `Nginx`, on a
  Turkish computer as well, and the Turkish dotted `İ` and dotless `ı` match neither `I` nor `i`. No Unicode
  normalization form is applied, so write keys in normalization form C. A case-sensitive entry beats a
  case-insensitive one for the same word. Prefer a case-sensitive entry for a name or an acronym that differs
  from a common word only by case (`US` and `us`, `Polish` and `polish`).
- **Apostrophes.** A word with an apostrophe matches with the plain `'` and with the typographic `’`
  (`O'Brien` matches `O’Brien`); one key must not mix the two.
- **The tokenizer decides what a word is.** The text is split the way the part-of-speech tagger splits it, so a
  key must be a word that survives as one piece. A possessive that the tokenizer splits off leaves the stem
  alone: `NASA's` is `NASA` and `'s`, so the key `NASA` matches. A hyphenated compound is one word and matches
  only as a whole: the key `Nginx` does not match inside `Nginx-based`, so add `Nginx-based` as its own key,
  and the key `Wi-Fi` does not match `Wi-Fi's`. A contraction that the tokenizer splits (`Don't` is `Do` and
  `n't`) is never matched by its written form, and the `Do` of it is never matched on its own.
- **A key cannot end with a period.** A word such as `Dr.`, `Mr.`, or `U.S.` belongs to the built-in
  abbreviation rules and cannot be a key. A word followed by a period that the tokenizer splits off (`Ph.D` and
  the final `.` of `She has a Ph.D.`) matches its key (`Ph.D`) and the period stays as written.
- **A key starts with a letter and ends with a letter or a digit**, is at most 64 characters, and contains only
  letters, combining marks, digits, and `'` `’` `-` `.` `&` `/` `_`. A key that ends in a combining mark (as
  decomposed text can) is rejected, so keep keys in normalization form C.

**Overrides that are ignored.** A number with its unit or magnitude is one construct that is read as a whole.
When an override is for the *later* word of such a construct, the built-in reading wins and the override is
counted as ignored in the diagnostics. With an entry that says `kg` is `kay gee`:

| Text | Result | Applied / ignored |
| --- | --- | --- |
| `5 kg of rice` | `five kilograms of rice` | 0 / 1 (`kg` is the unit of `5`) |
| `The kg is a unit` | `The kay gee is a unit` | 1 / 0 (`kg` is the first word of its own construct) |
| `$5 million` with an entry for `million` | `five million dollars` | 0 / 1 |
| `at 5 pm today` with an entry for `pm` | `at five P M today` | 0 / 1 |

An entry for the *first* word of a construct always wins (an entry for `US` beats the acronym table), and
`Skip` is the way to stop a built-in rule from touching a word.

**Building a dictionary in code.** The dictionary is immutable and thread-safe; build it once and share it:

```csharp
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;

var overrides = PronunciationOverrides.Create(
[
    PronunciationOverrideEntry.Say("Nginx", "engine ex"),                  // case-insensitive by default
    PronunciationOverrideEntry.Say("Kubernetes", "koo ber net eez", caseSensitive: true),
    PronunciationOverrideEntry.SpellOut("GIF", caseSensitive: true),
    PronunciationOverrideEntry.Pronounce("Polish", PronunciationScheme.Ipa, "ˈpoʊlɪʃ", caseSensitive: true),
    PronunciationOverrideEntry.Pronounce("polish", PronunciationScheme.Ipa, "ˈpɑːlɪʃ"),
    PronunciationOverrideEntry.Say("SQL", "sequel"),
    PronunciationOverrideEntry.Skip("NASA", caseSensitive: true),
]);

var analyzer = new EnglishTextAnalyzer(new EnglishTextAnalyzerOptions { Overrides = overrides });
```

`Create` validates every entry and throws an `ArgumentException` for the first bad one, with the message
`Entry 3: <reason>.` (the zero-based position and a fixed reason; never the word or the payload, because a
dictionary can hold names that must not reach a log). A duplicate (the same word, compared the way the
entry's case flag says, with the same case flag) is an error, never "the last one wins", but a case-sensitive
and a case-insensitive entry for the same word are allowed, as above.

**The tab-separated file.** The same dictionary can be kept in a text file that someone who is not a
programmer maintains, and loaded with `PronunciationOverrides.FromTsv`:

```csharp
using var reader = File.OpenText("pronunciations.tsv");
var overrides = PronunciationOverrides.FromTsv(reader);
```

One entry per line: `word`, `case`, `action` and, for `say` and `pron`, a `payload`, separated by tab
characters (shown here as `→`):

```text
# domain terms
Nginx→insensitive→say→engine ex
Kubernetes→sensitive→say→koo ber net eez
GIF→sensitive→spell
Polish→sensitive→pron→ipa:ˈpoʊlɪʃ
polish→insensitive→pron→ipa:ˈpɑːlɪʃ
SQL→insensitive→say→sequel
NASA→sensitive→skip
```

The format rules:

- the `case` column is exactly `sensitive` or `insensitive`, and the `action` column exactly `skip`, `say`,
  `spell`, or `pron`; `skip` and `spell` take no payload (an empty fourth column is tolerated); `say` and `pron`
  require one; no column may start or end with whitespace; there is no quoting and no header row
- empty lines and lines whose first character is `#` are ignored; a line of only whitespace is an error
- lines end with LF or CRLF (a lone carriage return is an error), a byte-order mark at the start of the file is
  skipped, and a control character other than the tab separator is an error on any line, comments included
- limits: at most 10,000 entries, 1,024 characters per line, 50,000 lines (blank and comment lines count), a
  payload of at most 256 characters; an endless input is rejected rather than read forever
- the reader is read to its end and is neither disposed nor kept, and its own exceptions pass through
- a malformed file throws an `InvalidDataException` with the message `Line 4: <reason>.` (the one-based line
  number and a fixed reason, never the content of the line)

**Phonemes (`"en.pron"`).** A `Pronunciation` entry carries `ipa:` or `arpabet:` followed by the phonemes, and
the annotation value is always that same `scheme:phonemes` text with a lower-case scheme. The list of schemes is
closed (`PronunciationScheme.Ipa` and `PronunciationScheme.Arpabet`); a new notation arrives with a new
release. The library checks only that the phonemes are written in the notation's syntax (`ipa`: 1 to 128
characters without whitespace or control characters; `arpabet`: 1 to 32 phones of one to three capital letters
with an optional stress digit 0 to 2, separated by single spaces, such as `K AE0 F EY1`); whether a symbol is
in your backend's phoneme inventory is for the backend to decide. A backend that cannot use a scheme, or a
particular phoneme string, speaks the word as it would without the annotation.

**Turning it on, and what it needs.** Set `EnglishTextAnalyzerOptions.Overrides`. Overrides are applied by
the normalization step, so a non-empty dictionary needs `TextEnAnnotationSelection.Normalization` (selected by
default): the `EnglishTextAnalyzer` constructor throws an `ArgumentException` for a non-empty dictionary
without it, instead of quietly ignoring it. An empty dictionary is accepted and changes nothing.

**Reading the result in a backend.** Collect the replacement annotations as before, and use the pronunciation
annotations for the words they cover:

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
        // The word text.Substring(annotation.Start, annotation.Length) keeps its text; when the backend
        // supports `scheme` (Ipa or Arpabet), speak `phonemes` for that word instead of the
        // backend's own letter-to-sound result. Otherwise speak the word as usual.
    }
}

replacements.Sort((a, b) => a.Start.CompareTo(b.Start));
// Copy the text between the replacements unchanged and insert each Value in place of its range.
// For an IsSpelled value, speak each capital letter by its letter name, never as a word.
```

A replacement and a pronunciation never overlap, because a word is either replaced or pronounced, and
`"en.upos"` is independent of both.

**Names and acronyms: recipes.**

- **Say a product name as words**
  - Entry: `Nginx`, insensitive, `say`, `engine ex`
  - Result: `Use Nginx today.` is spoken `Use engine ex today.`
- **Say a name the way it sounds**
  - Entry: `Kubernetes`, sensitive, `say`, `koo ber net eez`
  - Result: `Kubernetes runs` is spoken `koo ber net eez runs`; `kubernetes runs` is not changed
- **Spell an initialism that is not in the table**
  - Entry: `GIF`, sensitive, `spell`
  - Result: `A GIF file` is spoken `A G I F file`
- **Speak a listed initialism as written**
  - Entry: `FBI`, sensitive, `skip`
  - Result: `The FBI and CIA` is spoken `The FBI and C I A` (`CIA` is still spelled)
- Keep the country `US` as written
  - Entry: `US`, sensitive, `skip`
  - Result: `US troops asked us` is not changed
- **Give a name its own phonemes**
  - Entry: `Polish`, sensitive, `pron`, `ipa:ˈpoʊlɪʃ` and `polish`, insensitive, `pron`, `ipa:ˈpɑːlɪʃ`
  - Result: `Polish polish` gets one `"en.pron"` annotation per word, each with its own phonemes
- **Replace a whole hyphenated word**
  - Entry: `Nginx-based`, insensitive, `say`, `engine ex based`
  - Result: `Nginx-based apps` is spoken `engine ex based apps`; `Nginx-free apps` is not changed

The same text with a key that cannot match is simply left alone: `Mr. Smith` is read by the abbreviation rules
(`Mister Smith`) because a key cannot end with a period, and `Don't go` is not changed by a key for `Do`.

*Preview note.* The override dictionary and the new annotation kinds are part of the same preview as the rest
of the package: they are not yet published to NuGet (see the retained-text note above), and the details of the
annotation values may still change before the first release.

#### Further public building blocks for model authors

Two further building blocks are public so a model with its own tag syntax does not have to copy
them. **`DefaultModelCapabilityProfile`** exposes its `Instance` singleton, a
`RenderText(text, annotations, sentenceBreaks, parameterOverrides)` chunk-then-render step, and the
`ShortPauseMilliseconds`, `LongPauseMilliseconds`, and `EllipsisPauseMilliseconds` constants, so a
custom `IModelCapabilityProfile` can wrap or delegate to it. **`SentenceChunker`** exposes `Chunk`,
`ChunkWithMetadata`, and `ChunkAligned`, which re-bases annotations onto each chunk and accepts
analyzer-supplied sentence breaks.
