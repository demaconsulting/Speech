### SpeechRecognizerUnavailableException

**Purpose**: Give a caller a distinct, typed signal for the two genuine recognizer error cases: an
operational member invoked on a recognizer that honestly reported itself unavailable, and a
session whose capture device failed when actually started. It mirrors
`AudioDeviceUnavailableException` so both subsystems signal misuse the same way.

**Data Model**: A sealed `Exception` subclass with no state beyond the standard `Message` and
`InnerException`. Obtaining and holding an engine or session never throws: for every ordinary
"cannot recognize on this machine right now" state (model not installed, native runtime absent, no
capture device) `SpeechRecognizerFactory` and `CreateSessionAsync` return
`UnavailableSpeechRecognizerEngine.Instance` and `UnavailableRecognitionSession.Instance`.

**Key Methods**:

- **SpeechRecognizerUnavailableException()**: Default constructor with a default message stating
  that the speech recognizer is unavailable.
- **SpeechRecognizerUnavailableException(string message)**: Carries a message describing which
  recognizer or operation was affected.
- **SpeechRecognizerUnavailableException(string message, Exception innerException)**: Carries a
  message and an inner exception describing the underlying cause, such as the capture device's
  own failure.

**Error Handling**: N/A - this type is itself an error signal and performs no detection or recovery.

**Dependencies**: None beyond the Base Class Library.

**Callers**: `UnavailableRecognitionSession` throws it from its operational members;
`RecognitionSession` throws it when the capture device fails on start or becomes unavailable
mid-session.
