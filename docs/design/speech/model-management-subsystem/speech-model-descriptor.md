### SpeechModelDescriptor

**Purpose**: Provide the immutable catalog read-model returned by
`SpeechModelCatalog.Enumerate()`, pairing one `ISpeechModel` with its install state at the
moment the snapshot was built.

**Data Model**: `Model` (`ISpeechModel`), `State` (`SpeechModelState`), plus convenience
passthrough properties `Id`, `DisplayName`, `Role`, `LicenseName`, and `LicenseUrl` (`Uri?`, `null`
when unknown) that delegate live to the wrapped model's own properties (no caching). The type is a
sealed record whose immutability is shallow: `Model` is a reference to a model that
is not itself copied, so the descriptor guarantees only that its own `Model` and `State` never change. Two descriptors
are equal when they wrap the same `Model` instance (reference
equality, as the model type does not override equality) and have the same `State`.

**Key Methods**:

- **SpeechModelDescriptor(model, state)**: Validates `model` is not null; otherwise a pure,
  immutable record construction.
- **Passthrough properties** (`Id`, `DisplayName`, `Role`, `LicenseName`, `LicenseUrl`): each reads the
  wrapped model's property on every access.

**Error Handling**: Throws `ArgumentNullException` for a null `model` at construction; otherwise
never throws (it performs no I/O or store queries itself).

**Dependencies**: `ISpeechModel`, `SpeechModelState`.

**Callers**: `SpeechModelCatalog.Enumerate()`; hosts binding a model-settings-page UI list.
