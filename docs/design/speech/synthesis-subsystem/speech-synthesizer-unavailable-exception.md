### SpeechSynthesizerUnavailableException

**Purpose**: Give a caller a distinct, typed signal that an operation was attempted on a speech
synthesizer that honestly reported itself unavailable, mirroring
`SpeechRecognizerUnavailableException` so both subsystems signal that misuse the same way.

**Data Model**: A sealed `Exception` subclass with no state beyond the standard `Message` and
`InnerException`. Loading an engine never throws for an ordinary machine state;
`UnavailableSpeechSynthesizerEngine` and `UnavailableSynthesisSession` report themselves through
`IsAvailable == false`, and this exception is reserved for a caller that invoked an operational
member anyway. It is not what a real `SynthesisSession` throws for its own runtime failures: a
playback device failure propagates as `AudioDeviceUnavailableException`, and a call on a
`Faulted` session throws `SynthesisSessionFaultedException`.

**Key Methods**:

- **SpeechSynthesizerUnavailableException()**: Default constructor with a default message stating
  that the speech synthesizer is unavailable.
- **SpeechSynthesizerUnavailableException(string message)**: Carries a message describing which
  synthesizer or operation was affected.
- **SpeechSynthesizerUnavailableException(string message, Exception innerException)**: Carries a
  message and an inner exception describing the underlying cause.

**Error Handling**: N/A - this type is itself an error signal and performs no detection or recovery.

**Dependencies**: None beyond the Base Class Library.

**Callers**: `UnavailableSpeechSynthesizerEngine` and `UnavailableSynthesisSession` throw it from
their operational members.
