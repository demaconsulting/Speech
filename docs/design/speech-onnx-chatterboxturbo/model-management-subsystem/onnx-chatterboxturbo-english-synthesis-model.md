### OnnxChatterboxTurboEnglishSynthesisModel

**Purpose**: The one concrete `ISynthesisModel` this package ships. It declares the Chatterbox Turbo
English model's identity, downloadable files, voice choice, capability profile and output format,
and builds the loaded backend from an installed directory. Its companion types are
`ChatterboxTurboModelOptions`, `ChatterboxTurboLanguageModelVariant`,
`ChatterboxTurboAuxiliaryModelVariant` and `ChatterboxTurboEndOfSentenceTagPolicy`.

**Data Model**: Immutable after construction: the options, the resolved list of
`ChatterboxTurboReferenceVoice` values (supplied directly, loaded from a directory of `.wav` or
`.flac` files, or lazily from the reference-voice library), the selected variants, and a static table of SHA-256
checksums keyed by file. The internal static `BundleContents` maps each of the five component bundle names
(`chatterbox-turbo-lm-20261014-{fp16,fp32}-b1`, `chatterbox-turbo-aux-20261014-{int4,fp16,fp32}-b1`; the
repository's `q4` graphs use the `int4` token) to its vendor files, each with its original Hugging Face
address, pin and `onnx/` install path. A private table holds the zip SHA-256 of each published bundle (today
the fp16 language-model and int4 auxiliary bundles), and the public static `PublishedBundles` builds one
`SpeechModelBundle` per entry. Each instance holds `InstalledFiles` (internal; its language-model bundle's
files followed by its auxiliary bundle's files) and, when both of its bundles are published, the two bundles it
installs from. The model id
is `chatterbox-turbo-onnx-en` (`BaseModelId`) for the default fp16 language model with q4 auxiliary
graphs, and `chatterbox-turbo-onnx-en-lm-<variant>-aux-<variant>` for any other combination.

**Key Methods**:

- **Metadata**: id, display name, English language, license, 24 kHz mono `PreferredAudioFormat`,
  and the capability profile with native-tag support
- **DownloadDescriptor**: when both of the variant's component bundles are published, the two zips
  (`SpeechModelBundle.CreateDescriptor`, language model first); otherwise the eight files of
  `InstalledFiles` (four graphs, each an `.onnx` plus `.onnx_data` file, with pinned checksums and
  relative install paths under `onnx/`), downloaded individually from Hugging Face. Adding a bundle's pin
  to the published table switches the variants that use it to the release with no other change
- **InstallAsync**: for a bundle-installed variant, delegates to `SpeechModelBundle.InstallAsync`, which
  extracts and verifies the vendor files and places each bundle's license files under
  `licenses/<bundle>/`; for any other variant, does nothing, as before bundles existed
- **Parameters**: a `voice` choice listing the available voices (first is default); its description
  appends the core `ReferenceVoiceLibrary.ConsentNotice`
- **Voice resolution** (constructor): `ChatterboxTurboModelOptions.ReferenceVoices` rejects a null list with
  `ArgumentNullException`. Voices are merged in this order: explicit `ReferenceVoices`
  (eager), then `ReferenceVoiceDirectory` files (lazy via `FromFileLazy`, sorted, so a damaged file fails
  only at first use of that voice, naming the file, and never breaks catalog composition; a missing
  directory throws `DirectoryNotFoundException`), then - unless `IncludeStoreVoices` is `false` - the voices
  of the core `ReferenceVoiceLibrary` (user folder, then installed pack), created lazily with
  `ChatterboxTurboReferenceVoice.FromFileLazy` so listing a hundred clips decodes none. Ids are compared
  ordinally. A duplicate id among the explicit and directory voices (including `a.wav` and `a.flac` in one
  directory) is a caller error and throws `ArgumentException`; a library voice whose id is already present
  is skipped and reported to `ChatterboxTurboModelOptions.Diagnostics` at `Info` level. A library voice
  whose display name clashes with an earlier label gets a `(user)` or `(pack)` suffix, and repeated
  clashes are numbered (`(user 2)`, `(user 3)`), so every voice label in the list is unique. The library is
  `ChatterboxTurboModelOptions.ReferenceVoiceLibrary` when set; the catalog extension
  `AddChatterboxTurboModels` otherwise builds `new ReferenceVoiceLibrary(catalog.Store)` (without
  mutating the caller's options). The list is fixed at construction, so it refreshes when the catalog is
  rebuilt after a pack is installed.
- **Options** (`ChatterboxTurboModelOptions`): every property has a safe default and is validated when
  assigned: the variants and `EndOfSentenceTagPolicy` must be defined enum values
  (`ArgumentOutOfRangeException` otherwise, leaving the previous value), `IntraOpThreads` and
  `MaxNewTokens` at least 1, `MaxAudioTokensPerTextToken` positive, `MaxConsecutiveSpeechTokenRepeats`
  non-negative (0 disables the cap), `MoodJoinSilenceMilliseconds` between 0 and 1000 (default 150), and
  `ReferenceVoices` non-null (`ArgumentNullException`). The default thread count is half the logical
  processors, between 1 and 6; that is a default only, not a clamp, so an explicitly assigned value of 1 or
  more is kept. The end-of-sentence tag policy selects how a trailing tag is rendered.
- **Variant rationale**: only the fp16 and fp32 language models are offered. The q4 language model
  destroys the inline audio tags the package exists to support, and the 8-bit (`quantized`) one is slower
  than fp16 on CPU with no quality benefit (see the remarks on `ChatterboxTurboLanguageModelVariant`). The
  auxiliary graphs (embeddings, speech encoder, conditional decoder) tolerate q4, fp16 or fp32.
- **ResolveSpeakerId(parameters)**: maps the selected voice id to its index; an unknown or absent id
  selects index 0
- **LoadTokenizer / OpenTokenizerResource**: open the gzip-compressed tokenizer document embedded in this
  assembly (the single resource whose name ends with the tokenizer suffix; a missing or ambiguous resource
  fails with the `InvalidOperationException` of `Single`) and build the `ByteLevelBpeTokenizer` from it
  with `FromGzipStream`.
- **SessionFactory** (internal static): a test seam, forwarding to the networks unit's seam, that when set
  creates every ONNX session in place of the default constructor. Tests use it to substitute a derived
  session that records its own disposal, to prove no session is left open when backend creation fails.
- **CreateBackend(installDirectory)**: in order, validates the directory argument (null throws
  `ArgumentNullException`, empty throws `ArgumentException`), requires at least one voice
  (`InvalidOperationException`), checks every file of `InstalledFiles` exists (`FileNotFoundException`, before any
  session is created; the descriptor is not used, because a bundle-installed variant's zips are deleted once
  extracted), builds the networks (an invalid graph throws `InvalidOperationException`), the tokenizer and the
  engine, and disposes created sessions if a later step fails. The voices are decoded lazily by the engine on
  first use; concurrent first use of one voice decodes its clip once (see `ChatterboxTurboReferenceVoice`).

**Error Handling**: `ArgumentException` for duplicate explicit or directory voice ids (the message names the
id and whether it came from `ReferenceVoices` or `ReferenceVoiceDirectory`) or an empty install directory;
`ArgumentOutOfRangeException` from the options setters for an out-of-range number or an undefined variant or
policy (so the model's own undefined-variant `ArgumentException` is a defensive check that normal use never
reaches); `ArgumentNullException` for a null voice list assigned to the options and for a null
install directory; `DirectoryNotFoundException` for a missing voice directory; `InvalidOperationException` when no
voice exists at backend creation or a graph is invalid; `FileNotFoundException` for missing files; other ONNX
Runtime exceptions propagate after cleanup. With no voices at all, `Parameters` is empty (there is no voice
choice to offer).

**Dependencies**: `OnnxChatterboxTurboNetworks`, `OnnxChatterboxTurboSynthesisEngine`,
`ByteLevelBpeTokenizer`, `ChatterboxTurboCapabilityProfile`, `ChatterboxTurboReferenceVoice`,
core `ReferenceVoiceLibrary` and `SpeechModelBundle`.

**Callers**: `SpeechModelCatalogChatterboxTurboExtensions.AddChatterboxTurboModels` and Speech's
model store and synthesizer factory; the non-shipped ModelBundler tool reads `BundleContents` and the bundle
names through `InternalsVisibleTo`.
