### AudioDeviceFactoryCaptureDeviceSource

**Purpose**: Provide the production `ICliCaptureDeviceSource`, resolving capture devices through the real library
`AudioDeviceFactory` exactly as the factory does on its own.

**Data Model**: One private field holds the composed `AudioDeviceFactory` that every call is forwarded to. The
class owns no other state, does not own the factory's lifetime, and disposes nothing.

**Key Methods**: The constructor stores the factory. `CaptureProbe` returns the factory's own capture probe.
`CreateCaptureDevice(selection)` returns whatever the factory's `CreateCaptureDevice` returns for the same
selection. Every call is a pure pass-through, including the factory's hardware detection, so real capture-device
resolution for `ask` and `recognize --mic` is identical to using the factory directly.

**Error Handling**: The constructor throws `ArgumentNullException` for a null factory. No other member adds
behavior, so any outcome of the factory, including an unavailable device, is returned unchanged.

**Dependencies**: The library's `AudioDeviceFactory`, plus the `ICliCaptureDeviceSource` contract it implements.

**Callers**: The production entry points of `AskCommand` and `RecognizeCommand`, which wrap the real
`AudioDeviceFactory` in this adapter.
