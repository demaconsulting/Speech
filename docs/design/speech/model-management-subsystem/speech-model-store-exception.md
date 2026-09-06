### SpeechModelStoreException

**Purpose**: Signal that an explicit, user-invoked `SpeechModelStore` operation (currently, only
uninstall) could not complete.

**Data Model**: No additional fields beyond the standard `Exception` base members.

**Key Methods**: Standard three-constructor exception pattern (default message, custom message,
message with inner exception).

**Error Handling**: This type is itself the error-handling mechanism. Per this library's
"nothing throws at composition" decision, it is never thrown by construction, install-state
queries, or catalog enumeration - only by an explicit `Uninstall` call whose `current/` directory
cannot be removed, most commonly because another process still holds an open file handle into it
(most commonly seen on Windows).

**Dependencies**: `Exception`.

**Callers**: `SpeechModelStore.Uninstall(...)`.
