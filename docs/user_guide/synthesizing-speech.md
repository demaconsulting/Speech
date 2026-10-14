# Synthesizing Speech

`SpeechSynthesizerFactory.LoadAsync(...)` composes an `ISpeechSynthesizerEngine` over an
installed `ISynthesisModel`, mirroring `SpeechRecognizerFactory`'s nothing-throws-at-composition
contract: it never throws for an ordinary machine state, and instead returns an engine reporting
`IsAvailable == false` for a model that is not installed, a machine with no speakers, or a
missing speech-engine native runtime. A playback device is bound later, per session (or per
one-shot call), not at `LoadAsync` time.

```csharp
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Sherpa;

// 1. The catalog is the library's only "what models exist" entry point - nothing below names a
//    concrete model class, so new models added in a future release show up automatically.
using var catalog = new SpeechModelCatalog().AddSherpaModels();

// 2. Pick a synthesis model. Any model with the synthesis role will do - this is the idiomatic
//    pattern for an app that just wants "a" text-to-speech model:
var descriptor = catalog.Enumerate().First(d => d.Role == SpeechModelRole.Synthesis);
// To pick a *specific* model when more than one of the same role is installed, match on name
// instead: catalog.Enumerate().First(d => d.DisplayName.Contains("Kokoro"));
var model = (ISynthesisModel)descriptor.Model;

// 3. Ensure the chosen model is downloaded before first use. Safe to call unconditionally on
//    every launch - it's a cheap no-op once installed.
await catalog.DownloadAsync(model.Id);

// 4. Create a playback device matching the model's own preferred audio format hint.
var playbackDevice = new AudioDeviceFactory().CreatePlaybackDevice(
    AudioDeviceSelection.SystemDefault,
    model.PreferredAudioFormat);

// 5. Load the engine and speak a one-shot phrase through the engine-level convenience overload,
//    which internally creates a session, speaks, and disposes the session again.
await using var engine = await SpeechSynthesizerFactory.LoadAsync(model, catalog);

if (engine.IsAvailable)
{
    await engine.SpeakAsync(playbackDevice, "Welcome. [short pause] Let's get started!");
}
```

For a model that declares a `ChoiceParameter` or `NumericParameter` for voice/speaker selection
(such as `SherpaOnnxKokoroEnglishSynthesisModel`'s `voice` parameter, or
`SherpaOnnxVitsLibriTtsEnglishSynthesisModel`'s numeric `speaker` parameter), pass a
`parameterValues` bag keyed by each declared parameter's `Id` to `LoadAsync(...)` to select a
non-default value:

```csharp
await using var engine = await SpeechSynthesizerFactory.LoadAsync(
    model,
    catalog,
    parameterValues: new Dictionary<string, object> { ["voice"] = "bm_george" });
```

A `null` (or omitted) `parameterValues` bag resolves to every declared
parameter's own default value, including whichever speaker/voice a model's `ResolveSpeakerId`
hook treats as its default. A supplied key naming a parameter the model does not declare is
silently ignored (composition still succeeds, with only an `Info`-level diagnostic reported if a
sink is wired up) so one settings dictionary stays reusable across different models, but a
supplied value for a parameter the model *does* declare that fails that parameter's own
validation (wrong CLR type, out of range, a fractional value for a whole-number-only parameter,
or a string matching no declared `ChoiceParameterOption`) throws `ArgumentException` synchronously
from `LoadAsync(...)` naming the parameter, the model, and the reason the value is invalid -
`SpeechRecognizerFactory.LoadAsync`'s `parameterValues` argument follows the same rule.
`model.PreferredAudioFormat` is likewise only a best-effort playback
hint: after the engine loads, every session created from it always treats the loaded engine's
actual `SampleRate` as authoritative and resamples whenever needed. This is a session-level choice: it is
independent of, and does not disturb, the existing per-segment Natural Language Audio Tag
speed/volume overrides described
above, which continue to apply per rendered segment regardless of which voice is selected. The
demo's Text-to-Speech panel exposes this same choice as a combo box in its embedded Model
Settings panel for any model that declares a `ChoiceParameter`.

Under the hood, `SpeakAsync` runs the text through the same normalize/tag-parse steps as above,
then a Layer 2 rendering step decides, tag by tag, how to realize each `TaggedTextSpan` against
the selected model's own declared audio-tag support (`IModelCapabilityProfile`):

- **Native** support passes a tag through unchanged as its canonical bracket text, letting a
  model that understands the vocabulary directly consume it as a control token.
- **Parameter-mapped** support approximates a pace or delivery/volume tag as a numeric parameter
  override on the surrounding narration (for example, `[fast]` maps to a speed multiplier), and
  silently strips a tag with no such convention.
- **None** strips every tag except pauses to plain narration, so speech is never worse than
  reading the underlying text aloud.
- A pause tag (`[short pause]`/`[long pause]`) always renders as a real timed silence segment
  regardless of a model's declared support, since honoring a pause requires no model cooperation.

**The Sherpa models and the raw ONNX Kokoro model declare `None`**
(`SherpaOnnxVitsLibriTtsEnglishSynthesisModel`, `SherpaOnnxKokoroEnglishSynthesisModel` and
`OnnxKokoroEnglishSynthesisModel`), so every non-pause tag is stripped to plain narration for them -
the plain words are always spoken regardless, per the "never worse than plain narration" guarantee
above. **`OnnxChatterboxTurboEnglishSynthesisModel` declares `Native`**: its capability profile maps the
callers' tags (for example `[laughs]`, `[clears throat]` and `[gasp]`) to the model's internal
native tokens - `[laugh]`, `[clear throat]` and `[gasp]` are those internal tokens, not tags you
type - and handles the mood tags described under the "Mood tags" subsection of the Chatterbox
Turbo section below. `ParameterMapped` is fully implemented
and exercised by tests, ready for a
future or host-supplied model to declare, but no shipped model currently uses it.

For a host implementing its own `ISynthesisModel` that declares `ParameterMapped`, only seven
tags have a built-in conservative mapping - every other non-pause tag (all emotion and non-verbal
tags, the mood tags, `[emphasis]`, and `[breathy]`) has no built-in convention and is always silently stripped
even under `ParameterMapped`; pause tags are never affected by this stripping and always render
as timed silence, per the pause-handling rule above.

| Tag | Mapped parameter convention | Shift |
| --- | --- | --- |
| `[fast]` | pace parameter (id contains `tempo`, `rate`, or `speed`) | `+15%` of declared range |
| `[very fast]` | same | `+30%` of declared range |
| `[slow]` | same | `-15%` of declared range |
| `[very slow]` | same | `-30%` of declared range |
| `[loud]`/`[shouting]`/`[screams]`/`[shout]` | volume (id: `volume`, `loudness`, `gain`) | `+20%` of declared range |
| `[soft]` | same | `-20%` of declared range |
| `[whispers]`/`[whispering]` | same | `-35%` of declared range |

Each shift is applied once, to the model's own declared default value for that parameter, clamped
to the parameter's `Minimum`/`Maximum`. The override is not scoped to a single sentence: it applies
to every `SpeechSegment` produced from the text accumulated since the previous flush point (a
`[short pause]`/`[long pause]` tag, or the end of the input), including text already buffered before the
tag - and a later tag mapped to the same parameter before the next flush overwrites the earlier
value rather than combining with it. A model with no `NumericParameter` matching one of these id
conventions simply never receives the override for that tag, and the tag is silently stripped
instead - consistent with the "never worse than plain narration" guarantee.

The rendered result is an ordered `SpeechPlan` of `SpeechSegment`s, which a sentence/clause-sized
chunker further splits so that synthesis and playback can pipeline: an earlier chunk plays on the
playback device while a later chunk is still being synthesized, rather than waiting for an entire
utterance's inference to finish before any sound is heard. Calling `StopAsync()` cancels an
in-flight `SpeakAsync` call deterministically (for example, in response to a user interruption)
and is a safe no-op when nothing is speaking. A synthesis-engine fault or a playback device that
drops mid-utterance fails the awaited `SpeakAsync` task honestly rather than hanging or crashing
the process.

The library provides four production synthesis models (each also reports its license
programmatically via `ISpeechModel.LicenseName`/`LicenseUrl`, without requiring you to parse
this prose or `DisplayName`). Two are described below, followed by the raw ONNX Runtime models:

- **`SherpaOnnxVitsLibriTtsEnglishSynthesisModel`** (`vits-piper-en_US-libritts_r-medium`, a
  VITS/Piper voice fine-tuned on the LibriTTS-R corpus, 904 declared speakers, 22,050 Hz,
  licensed **CC BY 4.0** — an attribution license: crediting the LibriTTS-R dataset
  (`http://www.openslr.org/141/`) and the Piper text-to-speech project is required if you
  redistribute the model or audio generated by it, but downstream relicensing under different
  terms is not otherwise restricted, unlike a ShareAlike/CC BY-SA license). All 904 speakers are
  selectable via `SpeechSynthesizerFactory.LoadAsync`'s `parameterValues` bag by plain numeric
  index (`0`-`903`) through this model's declared `NumericParameter` named `speaker`: LibriTTS-R's
  speaker embeddings have no published
  human-readable name mapping, so speakers are identified only by their numeric id, unlike the
  named voices below.
- **`SherpaOnnxKokoroEnglishSynthesisModel`** (`kokoro-int8-en-v0_19`, an English-only,
  int8-quantized, StyleTTS2-derived text-to-speech model, 24,000 Hz, licensed **Apache-2.0**,
  confirmed directly from the archive's own `LICENSE` file). Like the model above, **voice
  selection is fully wired**, but through named voices rather than a plain numeric index: this
  model declares an 11-value `ChoiceParameter` (`af`, `af_bella`,
  `af_nicole`, `af_sarah`, `af_sky`, `am_adam`, `am_michael`, `bf_emma`, `bf_isabella`,
  `bm_george`, `bm_lewis`), each mapping through its own `ResolveSpeakerId` hook to sherpa-onnx's
  real integer speaker id for that voice, and each voice is genuinely distinct in accent, gender,
  or vocal character - not merely a numeric id with no audible difference. Kokoro has no discrete
  emotion parameter (no happy/sad/excited control); expressiveness is limited to voice choice
  plus the existing speed/length-scale and Natural Language Audio Tag mechanisms described above.

Both models above are shipped by the separate `DemaConsulting.Speech.Sherpa` package and registered via
`AddSherpaModels()`. Neither model's bytes are bundled
with this library; `SpeechModelCatalog.DownloadAsync` fetches each one directly from its own
official GitHub Releases URL only when you explicitly request it.

The optional `DemaConsulting.Speech.Onnx.Kokoro` package adds a raw ONNX Runtime Kokoro model
through `AddKokoroModels()`: **`OnnxKokoroEnglishSynthesisModel`** (`kokoro-onnx-v1_0-en`,
Kokoro v1.0 running directly on ONNX Runtime, 24,000 Hz, licensed **Apache-2.0**). It declares a
29-value English `voice` `ChoiceParameter` (default `af_heart`), declares no audio-tag support, and
needs no sherpa-onnx. The fourth synthesis model, the Chatterbox Turbo voice-cloning model, is
described in the "Chatterbox Turbo" subsection below and in the CLI subsection "Reference voices
(Chatterbox Turbo)".

## Chatterbox Turbo

The optional `DemaConsulting.Speech.Onnx.ChatterboxTurbo` package adds a voice-cloning synthesis
model through `AddChatterboxTurboModels(options)`: `OnnxChatterboxTurboEnglishSynthesisModel`
(`chatterbox-turbo-onnx-en`, 24,000 Hz, licensed **MIT**). It has no built-in voices; you supply short
reference clips (1 to 30 seconds, WAV or FLAC of any sample rate and channel count, or in-memory
samples) and the model speaks in that voice:

```csharp
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;

var options = new ChatterboxTurboModelOptions
{
    ReferenceVoices = [ChatterboxTurboReferenceVoice.FromFile("narrator", "narrator.wav")],
    // Or: ReferenceVoiceDirectory = "voices",   // every .wav and .flac file becomes a voice
};

var catalog = new SpeechModelCatalog().AddChatterboxTurboModels(options);
```

### Reference voice packs

The package bundles no voice audio, but the core package can download an optional **reference-voice
pack** and keeps a folder for your own clips, and the model lists all of them as voices
automatically when registered through `AddChatterboxTurboModels(options)` (set `IncludeStoreVoices = false`
to opt out). The built-in pack is **voice-zero 1.0** by OwenTyme (154 clips, **CC0 1.0 Universal**,
[source](https://github.com/OwenTyme/voice-zero)). Install it with `ReferenceVoiceLibrary`:

```csharp
using var voices = new ReferenceVoiceLibrary(catalog.Store);
// ~570 MiB download, ~572 MiB installed with emotions
await voices.InstallPackAsync(ReferenceVoicePack.VoiceZero10.Id);
// Base voices only (about 44 MiB installed): InstallPackAsync(id, includeEmotions: false)
// Your own .wav/.flac clips go in voices.UserDirectory (voices.EnsureUserDirectory() creates it).
```

The download is the whole upstream archive, verified against its published SHA-256 and fetched
through the same mirror-aware downloader as models (with a mirror configured the path is
`{mirror}/voice-zero-1.0/voice-zero-1.0.zip`); only the voice clips, the license files and (by default) the
emotion clips are kept: about
44 MiB for the 154 base voices, plus about 528 MiB for the emotion variants (13 moods per voice: anger,
calm, confused, enthused, excited, frustrated, happy, neutral, sad, shout, surprised, tired, worried).
Pass `includeEmotions: false` to skip them; installing again with emotions over a voices-only install
downloads the pack again, keeping the previous install aside until the new one is in place (a failed or
canceled re-download restores it). Concurrent installs of the same pack run one after another.
`ReferenceVoiceLibrary.UninstallPackAsync` waits for an in-flight install or uninstall of that pack
(honoring its `CancellationToken`) and then removes the pack, so it never races the install. Each pack voice's
`ReferenceVoiceInfo.MoodClips` maps the lower-case mood name to that speaker's emotion clip (or is
`null`; look a mood up with `TryGetMoodClip`, which ignores case and surrounding whitespace); the moods are variants
of the voice, not extra voices. Voices are stored under
`<models root>/reference-voices/`: the pack in
`voice-zero-1.0/current/voices/` and your own clips in `user/`. The voice list merges explicit
`ReferenceVoices`, then `ReferenceVoiceDirectory`, then `user/`, then the pack, and the first voice
with a given id wins, so your own clip overrides a pack voice of the same name. The voice list is
read when the catalog is built, so rebuild it after installing a pack. Cloning a real person's voice
requires that person's consent (`ReferenceVoiceLibrary.ConsentNotice`).

### Model variants and generation

The first voice is the default and the `voice` choice parameter selects another. The default
download is the fp16 language model with q4 helper graphs (eight files, roughly 1.1 GB);
`LanguageModelVariant` and `AuxiliaryModelVariant` choose other precisions, each with its own model
id. It runs on the CPU only. It renders the laugh, clear-throat and gasp tags natively (via the internal
`[laugh]`, `[clear throat]` and `[gasp]` tokens, which are not tags you type), renders
sighs and pauses as silence and omits every other tag except the mood tags below;
`EndOfSentenceTagPolicy` controls whether a
native tag that would end a sentence is moved before the final word (default), suffixed, or
stripped. Generation is bounded (`MaxNewTokens`, `MaxAudioTokensPerTextToken`), so a failure to stop
produces truncated audio rather than a hang, and `MaxConsecutiveSpeechTokenRepeats` (default 12, 0
disables) stops the model from locking onto one speech token and producing a sustained tone. You are
responsible for having the right to clone any voice you use.

### Mood tags

For a voice with emotion clips (`MoodClips`), the mood tags `[excited]`, `[enthused]`,
`[happy]`, `[sad]`, `[angry]`, `[loud]`/`[shout]`, `[calm]`, `[surprised]`, `[bored]`/`[tired]`,
`[worried]`/`[panicked]`, `[confused]`, `[frustrated]` and `[neutral]`/`[serious]` switch to that
mood's reference clip for the text that follows, until the next mood tag. A voice without that mood
(including every user-folder voice) speaks with its base clip, so the tag is harmless. `[whispers]`,
`[sarcastic]`, `[soft]` and `[breathy]` are omitted. `MoodJoinSilenceMilliseconds` (default 150,
0 to 1000) inserts a short silence where the mood changes. Honest limits: each mood segment is
synthesized separately and pays its own prefill cost, prosody resets at every join, and the tags work
best at sentence boundaries. Mood clips are not loudness-normalized, so levels can differ between
moods. Models that declare no tag support (Kokoro, Sherpa) strip these tags and speak the plain
words rather than reading an unrecognized `[angry]` aloud as literal text.
