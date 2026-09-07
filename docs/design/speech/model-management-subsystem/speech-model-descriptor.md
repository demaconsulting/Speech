### SpeechModelDescriptor

**Purpose**: Provide the immutable catalog read-model returned by
`SpeechModelCatalog.Enumerate()`, pairing one `ISpeechModel` with its install state at the
moment the snapshot was built.

**Data Model**: `Model` (`ISpeechModel`), `State` (`SpeechModelState`), plus convenience
passthrough properties `Id`, `DisplayName`, `Role`, `LicenseName`, and `LicenseUrl` mirroring the
wrapped model's own properties.

**Key Methods**:

- **SpeechModelDescriptor(model, state)**: Validates `model` is not null; otherwise a pure,
  immutable record construction.

**Error Handling**: Throws `ArgumentNullException` for a null `model` at construction; otherwise
never throws (it performs no I/O or store queries itself).

**Dependencies**: `ISpeechModel`, `SpeechModelState`.

**Callers**: `SpeechModelCatalog.Enumerate()`; hosts binding a model-settings-page UI list.
