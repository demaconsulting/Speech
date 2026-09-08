## SpeechCli SynthesisCommandSubsystem Design

### Overview

The SynthesisCommandSubsystem implements the one text-to-speech subcommand dispatched to by
`CommandDispatch`: `speak`. It contains one command handler plus one parsing utility, and extends
`ModelCommandsSubsystem`'s existing catalog seam with two further members:

- **`SpeakCommand`**: implements
  `speak --model <id> (--text <string> | --file <path> | stdin) [--output <wav-path>]
  [--device <name>] [--param key=value ...] [--no-tags]`
- **`ParameterBagParser`**: parses and validates repeatable `--param key=value` tokens against a
  resolved model's declared parameters
- **`ICliModelCatalog.GetPreferredAudioFormat`/`CreateSynthesizer`**: the two new seam members
  (see _Extending the ModelCommandsSubsystem Seam_ below), implemented by
  `SpeechModelCatalogAdapter`

### Extending the ModelCommandsSubsystem Seam

`speak` needs to construct a real `ISpeechSynthesizer`, which requires an `ISynthesisModel` -
the library's synthesis-capable model interface. `ISynthesisModel` itself, and the members
`speak` needs from it (`PreferredAudioFormat`, and the constructor path
`SpeechSynthesizerFactory.Create` requires), are only reachable once a `SpeechModelDescriptor.Model`
is known to implement that interface. That cast is safe to perform inside
`SpeechModelCatalogAdapter` (which lives in the same assembly as the library's own internal
model types, so its `is ISynthesisModel` check compiles), but is **not** safe to perform inside a
hand-written `FakeSpeechModel` test double in `DemaConsulting.Speech.Cli.Tests` - that assembly is
not granted `InternalsVisibleTo` access to the library's internal `ISynthesisModel` members, and
widening that access purely to make this one subsystem's tests compile would weaken the isolation
the seam already provides for `ModelCommandsSubsystem`.

Rather than have `SpeakCommand` perform the `is ISynthesisModel` cast itself (which `SpeakCommand`
cannot even attempt, since its own project also lacks that internal access), two further methods
are added to `ICliModelCatalog`:

| Member | Returns | Behavior |
| --- | --- | --- |
| `GetPreferredAudioFormat(descriptor)` | `AudioFormat` | Throws for a non-synthesis model |
| `CreateSynthesizer(descriptor, device, values)` | `ISpeechSynthesizer` | Throws for a non-synthesis model |

`SpeechModelCatalogAdapter` implements both via a private `RequireSynthesisModel` helper that
performs the cast once and throws a clean `ArgumentException` naming the offending model id when
it fails. In production this branch is unreachable through the CLI's own dispatch - `SpeakCommand`
already validates `descriptor.Role == SpeechModelRole.Synthesis` itself before calling either new
member - but is retained because `ICliModelCatalog` is an interface any future caller could
misuse, and a defensive, well-worded exception is preferable to an unhandled `InvalidCastException`
surfacing as a stack trace. `CreateSynthesizer`'s production implementation forwards to the
public `SpeechSynthesizerFactory.Create(ISynthesisModel, SpeechModelCatalog, IAudioPlaybackDevice,
ISpeechDiagnostics?, IReadOnlyDictionary<string,object>?)` overload, which resolves the model's
installed directory internally via the composed catalog's own store and never throws for "not
installed" or "no playback device" - it instead returns the library's own
`UnavailableSpeechSynthesizer.Instance` singleton, a graceful-degradation contract `SpeakCommand`
relies on rather than duplicates.

This keeps `SpeakCommand`'s own logic - argument parsing, text-source resolution, `--param`
validation, tag stripping, device dispatch, disposal ordering, cancellation - fully unit-testable
against `FakeCliModelCatalog`'s delegate overrides for the two new members, with no dependency on
`ISynthesisModel` anywhere in the test project.

### ParameterBagParser

`--param key=value` is repeatable; each raw token is first split by `ParseToken` into a
`(key, value)` pair (throwing `ArgumentException` for a token with no `=` separator or an empty
key half), then the full list of raw pairs is resolved by `Resolve` against the model's own
`ISpeechModelParameter` collection (the same collection `model-info` already prints - see
_SpeechCli ModelCommandsSubsystem Design_). Resolution switches on the parameter's concrete type:

- **`NumericParameter`**: parsed as `double` (`NumberStyles.Float`, invariant culture),
  range-checked against `Minimum`/`Maximum`, and, when `IsInteger` is set, checked for a whole-number
  value via `double.IsInteger` - boxed as `double`
- **`ChoiceParameter`**: matched ordinally against the parameter's declared `Options[].Value` list -
  boxed as `string`
- **`BooleanParameter`**: parsed via `bool.TryParse` (case-insensitive) - boxed as `bool`

An unrecognized `--param` key throws `ArgumentException` naming the key. This is a deliberate
divergence from `SpeechSynthesizerFactory.Create`'s own library-level contract, which silently
ignores (and Info-logs) an unrecognized parameter key rather than throwing: at the library level,
silently ignoring an unrecognized key is the right graceful-degradation choice when parameter
values are supplied programmatically and may target multiple engine versions, but at the CLI an
unrecognized `--param` key is almost always an operator typo, and failing loudly at the command
line - before any synthesis is attempted - is friendlier than silently mistuning (or not tuning at
all) a five-minute synthesis run only to notice much later that a misspelled key was ignored.

### SpeakCommand

Parses its own flags (`--model`, `--text`, `--file`, `--output`, `--device`, `--no-tags`, and
repeatable `--param`) via a hand-rolled loop matching the same style every other command in this
tool already uses, requiring `--model` and rejecting an unsupported argument or a value-less flag
with `ArgumentException`.

**Text-source resolution runs before model resolution.** Exactly one of `--text`, `--file`, or a
redirected stdin stream (`Console.IsInputRedirected`) must supply the text to speak; supplying
more than one of `--text`/`--file`, or supplying none while stdin is not redirected, throws
`ArgumentException` immediately - before the model id is even looked up. This ordering is
deliberate: a text-source usage mistake is a cheap, model-independent input error, and reporting
it first avoids masking it behind an unrelated "unknown model id" error whenever both mistakes
happen to be present in the same invocation (for example, a placeholder model id typed alongside
both `--text` and `--file` by accident).

**Model resolution** looks up the requested `--model <id>` in `catalog.Enumerate()`, throwing a
user-facing `ArgumentException` for three distinct failure modes, each with an actionable message:
an id absent from the catalog entirely (suggesting `list-models`), an id present but with the
wrong role (suggesting `list-models --role tts`), and an id present with the synthesis role but
not yet downloaded (suggesting `download <modelId>` by name). Once resolved, `--param` values are
validated against the resolved model's own declared parameters via `ParameterBagParser.Resolve`.

**`--no-tags`** forces Natural Language Audio Tag stripping unconditionally via
`AudioTagParser.Parse`, keeping only `TaggedTextSpanKind.PlainText` spans and concatenating them -
regardless of whether the resolved model natively supports audio tags (`Native`/`ParameterMapped`
per `SpeechModelAudioTagSupport`). This is a deliberate, unconditional override: an operator who
explicitly asks for tags to be stripped is asking for literal narration text only, and a model's
own tag-support level should not silently override an explicit operator request.

**Device dispatch**: when `--output <path>` is given, the model's `PreferredAudioFormat` is
resolved via the new `GetPreferredAudioFormat` seam member and used to size a
`WavFileAudioPlaybackDevice` at that path; `--device` is documented, and enforced by construction,
to be ignored in this case, since an explicit file destination unambiguously wins and no error is
raised for supplying both. Otherwise, a real playback device is resolved from the injected
`AudioDeviceFactory`, reusing `DevicesTestCommand.ResolveDeviceSelectionOrThrow` (internal, same
assembly, different namespace - no refactor needed) to validate any requested `--device` name
against the enumerated playback devices before a device is actually created, exactly mirroring
`devices test`'s own validate-before-create pattern; an unavailable resolved device throws
`InvalidOperationException` suggesting `--output` as an alternative.

**Disposal ordering.** `ISpeechSynthesizer.Dispose()` does not dispose the playback device it was
constructed over (confirmed against the library's own `SherpaOnnxSpeechSynthesizer.Dispose()`
implementation, which disposes only its own inference engine) - so `SpeakCommand` disposes the
synthesizer first, in an inner `finally`, guaranteeing any in-flight playback/write has genuinely
quiesced, then disposes the playback device, in an outer `finally`, via `(playbackDevice as
IDisposable)?.Dispose()`. The conditional cast is necessary because `IAudioPlaybackDevice` itself
does not declare `IDisposable` - a real device manages its own native stream lifecycle entirely
through `Start()`/`Stop()` - but `WavFileAudioPlaybackDevice` (used for `--output`) additionally
implements `IDisposable` to finalize its RIFF header, and the outer `finally` must dispose it
unconditionally when present while remaining a safe no-op for every other device kind.

**Cancellation.** `Ctrl+C` is wired to a `CancellationTokenSource` via `Console.CancelKeyPress`,
mirroring `DownloadCommand.Run`'s exact subscribe/unsubscribe-in-try/finally pattern (see
_SpeechCli ModelCommandsSubsystem Design_). A cancellation during `SpeakAsync` is caught and
reported as a clean, one-line message rather than an unhandled `OperationCanceledException`
propagating as a stack trace.

### Interactions with Other Units

`SpeakCommand` depends on the `ICliModelCatalog` seam (both its original five members and the two
new ones added by this pass), the library's public `AudioFormat`/`ISpeechSynthesizer`/
`AudioTagParser`/`TaggedTextSpan` types, `AudioDeviceFactory` from the library's audio subsystem,
`WavFileAudioPlaybackDevice`, and reuses `DeviceCommandsSubsystem`'s
`DevicesTestCommand.ResolveDeviceSelectionOrThrow` internal helper rather than duplicating device
resolution logic. `ParameterBagParser` depends only on the library's public
`ISpeechModelParameter`/`NumericParameter`/`ChoiceParameter`/`BooleanParameter` types. None of this
subsystem touches `ISynthesisModel` or any other internal library type directly - that access is
confined entirely to `SpeechModelCatalogAdapter`, exactly as `ModelCommandsSubsystem`'s original
seam already established.
