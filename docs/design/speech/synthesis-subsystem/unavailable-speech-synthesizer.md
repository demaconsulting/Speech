### UnavailableSpeechSynthesizer

**Purpose**: Provide a safe, always-obtainable `ISpeechSynthesizer` fallback for use when
synthesis is not possible on the current machine.

**Data Model**: No instance state. Exposes a single static `Instance` singleton; the constructor
is private, since the type carries no state and multiple instances would provide no value.
`IsAvailable` always returns `false`.

**Key Methods**:

- **SynthesizeStreamAsync(...)** / **PlayStreamAsync(...)** / **SpeakAsync(...)** / **Stop()**:
  Always throw `SpeechSynthesizerUnavailableException`.
- **Dispose()**: A no-op that never throws and never invalidates `Instance`, so a host that wraps
  its synthesizer in a disposal scope runs unchanged on a machine without synthesis.

**Error Handling**: Obtaining and holding the instance never throws. Only the operational members
throw, and only when actually invoked - a caller that checks `IsAvailable` first never triggers
them. This mirrors `UnavailableSpeechRecognizer` exactly, so both subsystems degrade the same
recognizable way.

**Dependencies**: `SpeechSynthesizerUnavailableException`; implements `ISpeechSynthesizer`.

**Callers**: `SpeechSynthesizerFactory.Create(...)` when the model is not installed, the model's
role is not synthesis, the playback device is unavailable, or the engine cannot be loaded.

### SpeechSynthesizerUnavailableException

**Purpose**: Signal that an operational member of an unavailable synthesizer was invoked, or that
a synthesizer that claimed to be available failed on first use.

**Data Model**: No additional fields beyond the standard `Exception` base members.

**Key Methods**: Standard three-constructor exception pattern.

**Error Handling**: This type is itself the error-handling mechanism.

**Dependencies**: `Exception`.

**Callers**: `UnavailableSpeechSynthesizer` for every operational member, and
`SherpaOnnxSpeechSynthesizer` when its playback device fails to start.
