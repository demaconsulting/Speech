### UnavailableSynthesisSession

**Purpose**: Provide the honest `ISynthesisSession` fallback returned by
`UnavailableSpeechSynthesizerEngine.CreateSessionAsync`, so a host that composes a session from an
unavailable engine still gets a safe, well-behaved object rather than a null or an exception from
composition itself.

**Data Model**: No instance state. Exposes a single static `Instance` singleton; the constructor
is private. `IsAvailable` always returns `false`. `State` always reports
`SynthesisSessionState.Created`, since this session never transitions - there is no operation it
can ever genuinely perform.

**Key Methods**:

- **SpeakAsync(...)** / **SynthesizeAsync(...)**: Always throw
  `SpeechSynthesizerUnavailableException`. Throw `ArgumentNullException` for a null `text`.
- **StopAsync(...)**: A safe no-op, since no operation is ever in flight.
- **DisposeAsync()**: A no-op that never throws and never invalidates `Instance`.

**Error Handling**: Obtaining and holding the instance never throws. Only the operational members
throw, and only when actually invoked. Mirrors `UnavailableSpeechSynthesizerEngine`.

**Dependencies**: `SpeechSynthesizerUnavailableException`, `SynthesisSessionState`; implements
`ISynthesisSession`.

**Callers**: `UnavailableSpeechSynthesizerEngine.CreateSessionAsync(...)`.
