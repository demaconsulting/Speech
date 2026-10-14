### SynthesisSessionFaultedException

#### Verification Approach

The exception is verified in two ways. Its three constructors are verified directly
(`SynthesisSessionFaultedExceptionTests`), asserting that the message and inner exception supplied
to each are preserved and that the default constructor yields a non-empty message. Its behavioral
role - being thrown on every operational call once the owning session is `Faulted`, wrapping the
original fault - is verified through `SynthesisSession`, driven by the deterministic fake
`ISynthesisBackend` made to fault on its first call, so no downloaded model, native
speech-inference runtime, or audio hardware is needed.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

The unit is considered verified when a `SynthesizeAsync` call made on a faulted session throws
`SynthesisSessionFaultedException`, and each constructor exposes the message and inner exception
supplied to it.

#### Test Scenarios

##### A faulted session rejects further operations

**Test**: `SynthesisSession_SynthesizeAsync_AfterFault_ThrowsSynthesisSessionFaultedException`

##### Constructors preserve the message and inner exception

**Test**: `SynthesisSessionFaultedException_Constructors_PreserveMessageAndInnerException`
