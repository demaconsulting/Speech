### ISpeechSynthesizerEngine

#### Verification Approach

`ISpeechSynthesizerEngine` is verified indirectly through both shipped implementations:
`UnavailableSpeechSynthesizerEngine` proves the honest-unavailable path, and
`SpeechSynthesizerEngine` proves availability reporting, session exclusivity leasing,
and the one-shot `SpeakAsync`/`SynthesizeAsync` convenience overloads. The `SynthesizedSpeech`
value type is a sealed record with its own constructor validation (null samples, non-positive
sample rate with non-empty samples, and negative silences are rejected), so it is verified
directly by `SynthesizedSpeechTests`, in addition to being observed through the segments
returned by `SynthesizeAsync`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner; no model, speakers, or native
  speech-inference runtime is required.

#### Acceptance Criteria

The contract is considered verified when the unavailable implementation reports `false`
availability and throws on every operational member (while `CreateSessionAsync` itself still
succeeds, returning an unavailable session), and when the real implementation reports itself
available, enforces single-session exclusivity (`SynthesisEngineBusyException` on a concurrent
lease request), allows a new session once a prior one is disposed, and its one-shot convenience
overloads create and dispose a session per call. `SynthesizedSpeech` is verified when valid values
are exposed unchanged, empty samples are accepted with any sample rate, and null samples,
a non-positive sample rate with non-empty samples, and negative silences are rejected.

#### Test Scenarios

##### Engine Contract Scenarios

See the SynthesisSubsystem-level scenarios "Engine: Session Exclusivity and Lease Lifecycle",
"Engine: One-Shot Convenience Overloads", and "Unavailable Fallback: Honest Degradation".

##### SynthesizedSpeech: Valid Values Are Exposed

**Test**: `SynthesizedSpeech_Constructor_ValidValues_ExposesValues`

Non-empty samples, a positive rate, and non-negative silences construct and expose the values.

##### SynthesizedSpeech: Empty Samples Accept Any Sample Rate

**Test**: `SynthesizedSpeech_Constructor_EmptySamples_AcceptsAnySampleRate`

A pure-pause segment with no samples is accepted even with a rate of zero.

##### SynthesizedSpeech: Null Samples Throw

**Test**: `SynthesizedSpeech_Constructor_NullSamples_ThrowsArgumentNullException`

Null samples throw `ArgumentNullException`.

##### SynthesizedSpeech: Non-Positive Sample Rate With Samples Throws

**Test**: `SynthesizedSpeech_Constructor_NonEmptySamplesWithNonPositiveSampleRate_ThrowsArgumentException`

Zero and negative sample rates with non-empty samples throw `ArgumentException`.

##### SynthesizedSpeech: Negative Silence Throws

**Test**: `SynthesizedSpeech_Constructor_NegativeSilence_ThrowsArgumentException`

A negative pre-silence or post-silence throws `ArgumentException`.
