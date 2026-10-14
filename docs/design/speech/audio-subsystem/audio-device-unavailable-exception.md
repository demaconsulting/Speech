### AudioDeviceUnavailableException

**Purpose**: Signal that an operational member of an unavailable audio device was invoked.

**Data Model**: No additional fields beyond the standard `Exception` base members.

**Key Methods**: Standard three-constructor exception pattern (default, message, message with
inner exception).

**Error Handling**: This type is itself the error-handling mechanism.

**Dependencies**: `Exception`.

**Callers**: The unavailable fallback devices and real PortAudio devices when first-use native
failures occur.
