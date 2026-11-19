# DemaConsulting.Speech.Proofreader

Developer tool for listening to long-form text-to-speech output sentence by sentence and flagging what sounds wrong.
It is never packed or shipped, and it is not a compliance gate: it is a listening aid for finding pronunciation,
normalization, and voice problems in the models of `DemaConsulting.Speech` and for collecting them as test candidates.
It works offline; it only lists the models that are already installed (it never downloads anything).

## Running

Run from the repository root:

```text
dotnet run -c Release --project tools/DemaConsulting.Speech.Proofreader -- [--models-dir <dir>] [--voices-dir <dir>] [file]
```

- `--models-dir <dir>`: model store root (default: the per-user store shared with the Demo and the CLI).
- `--voices-dir <dir>`: directory of Chatterbox Turbo reference voices.
- `file`: a `.txt` or `.md` text, or a `.json` session, to open at start-up.

Install models first with the Demo or the `ModelDownloader` tool (Kokoro, Piper, Sherpa VITS and Chatterbox Turbo are
listed when installed; models that are not installed are shown as such).

## Workflow

1. Paste text into the *Paste text* box or open a `.txt` / `.md` file (simple Markdown is reduced to prose: heading,
   list, and quote markers, emphasis, link targets, code spans, and fenced code blocks are removed).
2. The text is split into sentences with the library's own English sentence analysis, so the boundaries match what the
   models speak. Blank lines are paragraph boundaries; a heading never merges with the text after it.
3. Choose a model and its settings for slot A (and slot B for A/B listening) in the *Models* tab, and a playback
   device. Settings use the same generic presentation as the Demo (a model declares its parameters; the tool renders
   them), so Kokoro voices, Piper speakers, and Chatterbox voices all appear without model-specific code.
4. Press Space to play from the current sentence. The current sentence is highlighted and shown large. The next two
   sentences are synthesized ahead of time, and every sentence is cached per sentence text, model, and settings, so a
   replay or an A/B repeat is instant.
5. When something sounds wrong, press `F` (playback pauses). Optionally select the offending word in the large
   sentence first. Pick a category, add a note, and press Enter. Press Space to continue.
6. Save the session and export the flags as TSV.

## Diagnostics

The *Analysis* and *Phonemes* tabs follow the current sentence and separate front-end errors from voice errors:

- **Analysis**: the Text.En analysis of the sentence: normalizations (source to spoken form, spelled-out forms),
  heteronym readings with their confidence (low-confidence readings are marked), pronunciation overrides, tokens, and
  the raw annotations. It uses the selected model's own analyzer when it has one (Kokoro, Piper), otherwise the tool's
  analyzer with a caption saying so (Sherpa and Chatterbox models speak without these annotations).
- **Phonemes**: for Kokoro and Piper, the phoneme string of every text the backend received, with the per-word
  lexicon, reading, spelled, or letter-to-sound source and out-of-vocabulary words. The texts are recorded at the
  backend (after normalization, tag handling, and chunking), and the phonemes are re-rendered by the tool with the
  same renderer configuration the model's pipeline uses (the pipelines are internal to the model libraries).

## Keyboard shortcuts

| Key | Action |
| --- | --- |
| Space | Play / pause / resume |
| Left, Up, K | Previous sentence |
| Right, Down, J | Next sentence |
| R | Replay the current sentence (and continue) |
| F | Flag the current sentence (pauses; selected words become the span) |
| A | Toggle A/B mode |
| Esc | Cancel the flag editor, or stop playback |
| Ctrl+O / Ctrl+Shift+O | Open text / open session |
| Ctrl+S / Ctrl+Shift+S | Save session / save session as |
| Ctrl+E | Export flags as TSV |
| Ctrl+Enter | Load the pasted text |
| F2 / Delete | Edit / delete the selected flag |

Plain keys are ignored while a text box, dropdown, or slider has focus. Clicking a sentence jumps there and plays;
clicking a flag jumps to its sentence.

## A/B mode

In A/B mode every sentence plays through model A and then, after a short gap, model B. A and B can be two models or
one model with two settings (for example two voices). A flag records which slot was audible.

## Session file

A session is indented JSON (version 1): the source name, the SHA-256 and the full plain text, the current sentence,
the A/B setting, the model id and parameter values of each slot, the playback device, and the flags. Each flag holds
the flag number, sentence index and text, the word span and word, the category, the note, the time, the slot and
model settings that were audible, and a snapshot of the readings and phonemes, so the export never depends on
re-synthesis. A flag describes the audio that was last *heard*: the model, settings, slot, readings, and phonemes are
captured when the sentence's audio becomes ready, so changing a voice, speaker, or slot after listening and then
pressing `F` still records what was heard. (A sentence that was selected but never played is flagged with the active
slot's current model and no readings or phonemes.)

### Stale flags

Loading verifies the text hash and re-splits the text. A flag whose stored sentence text no longer equals the re-split
sentence at its index is *untrusted*: it stays in the flag list (so nothing is lost, and it is saved back unchanged)
with a `STALE` marker and its stored text, but it is never attached to a different sentence. Clicking it does not jump,
it does not mark a sentence in the list, and it is left out of the TSV export (the status line reports how many were
skipped). Delete it (`Del`) to resolve it, or reopen the original text.

## TSV export

UTF-8 without a byte order mark, LF line endings, one header row, one row per flag sorted by sentence and span,
invariant number formatting. Tabs and line breaks inside a field are replaced with a single space. A field that
contains or starts with a double quote is wrapped in double quotes with embedded quotes doubled (the spreadsheet
convention, so quoted dialogue stays in one cell), and a field starting with `=`, `+`, `-`, or `@` gets a leading
apostrophe so a spreadsheet does not read it as a formula.

| Column | Content |
| --- | --- |
| `id` | Flag number |
| `sentence` | The sentence text |
| `span` | `start-end` (end exclusive) in sentence-relative UTF-16 offsets; empty for a whole-sentence flag |
| `word` | The selected word or phrase |
| `category` | The category key (see the list below the table) |
| `note` | The note |
| `model` | The model display name |
| `settings` | The model parameter values as `id=value; ...` |
| `readings` | `word=reading(confidence); ...` for the readings inside the span (the whole sentence without a span) |
| `phonemes` | The phoneme string(s) the model consumed, parts separated by ` \| ` |
| `sentence_index` | The zero-based sentence index |

The category keys are `wrong-heteronym`, `mispronounced-word`, `normalization`, `missing-or-extra-word`,
`prosody-or-pause`, `voice-glitch`, and `other`.

## Limitations

- Synthesis cannot be canceled mid-sentence (the native call is not cancelable), so a jump during a synthesis waits for
  that sentence; cached and prefetched sentences start instantly.
- Pause stops the audio device and resumes about 80 ms before the pause point, because the device has already consumed
  slightly more audio than has sounded.
- The phoneme view is a re-rendering with the model's renderer configuration; a voice's own handling of symbols it does
  not know is not reproduced, and Sherpa and Chatterbox models phonemize internally and show no phoneme string.
- Sentence boundaries come from one analyzer that is independent of the selected model, so sentence numbers (which
  flags store) never shift when the model changes.
