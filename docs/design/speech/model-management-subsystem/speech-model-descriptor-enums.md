### SpeechModelDescriptorEnums (SpeechModelRole, SpeechModelState, SpeechModelAudioTagSupport)

**Purpose**: Declare the fixed, small value sets a model and `SpeechModelCatalog` are built
around: which capability a model provides, its install lifecycle state, and how (if at all) it
can honor inline Natural Language Audio Tags.

**Data Model**: Three plain enums with no behavior:

- `SpeechModelRole`: `Recognition`, `Synthesis`.
- `SpeechModelState`: `NotDownloaded`, `Downloading`, `Downloaded`, `FailedOrCorrupt` - no
  "update available" state.
- `SpeechModelAudioTagSupport`: `None`, `ParameterMapped`, `Native` - a declaration shape only;
  the Layer 2 rendering logic that actually honors a tag is implemented per-model in Phase 4.

**Key Methods**: None; these are declaration-only enums.

**Error Handling**: N/A.

**Dependencies**: None (BCL only).

**Callers**: `ISpeechModel` (`Role`, `AudioTagSupport`), `SpeechModelDescriptor`/
`SpeechModelCatalog` (`SpeechModelState`), and every model's own backing class in Phase 3/4.
