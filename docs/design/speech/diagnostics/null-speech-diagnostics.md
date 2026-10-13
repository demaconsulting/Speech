### NullSpeechDiagnostics

**Purpose**: Provide a default, always-available `ISpeechDiagnostics` implementation that
discards every event without throwing, so factories always have a working diagnostics sink even
when a host supplies none.

**Data Model**: No instance fields. Exposes a single static `Instance` property returning the
shared singleton; the constructor is private. The singleton lets any factory needing a default
`ISpeechDiagnostics` obtain one without allocating and without ever failing to do so, per this
library's "nothing throws at composition" decision.

**Key Methods**:

- **Report(SpeechDiagnosticLevel level, string category, string message)**: Discards the event
  and returns immediately. Preconditions: none (accepts any input, including default enum
  values). Postconditions: no observable state change; never throws.

**Error Handling**: None required — the method has no failure modes.

**Dependencies**: `ISpeechDiagnostics` (implements it).

**Callers**: Any factory, session, or worker constructed without a diagnostics sink that needs a
safe default, including `AudioDeviceFactory`, `SpeechRecognizerFactory`, and
`SpeechSynthesizerFactory`.
