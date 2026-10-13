### ISpeechDiagnostics

**Purpose**: Define the single contract through which the library reports structural diagnostic
events to a host, independent of which component raised the event.

**Data Model**: No fields or properties; a pure behavioral contract. The
`SpeechDiagnosticLevel` enum (`Info`, `Warning`, `Error`) declared alongside it classifies event
severity.

**Key Methods**:

- **Report(SpeechDiagnosticLevel level, string category, string message)**: Reports a single
  structural diagnostic event. Preconditions: `category` and `message` are non-null (enforced by
  each implementation, not by the interface itself). Postconditions: the event has been
  delivered to whatever the implementation does with it (logged, discarded, forwarded); the
  method itself never throws for a well-formed call, by convention for all implementations
  shipped by this library.

**Error Handling**: The interface itself defines no error handling; each implementation decides
how to treat invalid input. `NullSpeechDiagnostics`, the library's own implementation, never
throws.

**Dependencies**: None.

**Callers**: Every subsystem that reports structural events accepts an `ISpeechDiagnostics` and
calls `Report`:

- AudioSubsystem: `AudioDeviceFactory` and the PortAudio-backed capture and playback devices
- ModelManagementSubsystem: `SpeechModelCatalog`, `SpeechModelDownloader`, `ReferenceVoiceLibrary`,
  `HttpModelDownloadClient`, and `SpeechModelParameterDiagnostics`
- RecognitionSubsystem: `SpeechRecognizerFactory`, `SpeechRecognizerEngine`, `RecognitionSession`,
  `RecognitionResultBuffer`, and `DedicatedWorker`
- SynthesisSubsystem: `SpeechSynthesizerFactory`, `SpeechSynthesizerEngine`, `SynthesisSession`,
  and `DedicatedWorker`

Hosts that implement their own sink are the other side of the contract.
