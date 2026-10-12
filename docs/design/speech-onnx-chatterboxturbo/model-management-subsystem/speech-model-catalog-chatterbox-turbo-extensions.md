### SpeechModelCatalogChatterboxTurboExtensions

**Purpose**: Provide the opt-in registration seam through which a host composes this package with the
Speech library: one `AddChatterboxTurboModels()` call registers the package's single synthesis model into a
`SpeechModelCatalog`, so the Speech library never depends on this package.

**Data Model**: None. The class is a static holder of one extension method; it keeps no state.

**Key Methods**:

- **AddChatterboxTurboModels(catalog, options)**: construct one `OnnxChatterboxTurboEnglishSynthesisModel`
  from the options, add it with `SpeechModelCatalog.AddModels`, and return the same catalog for chaining.
  The reference-voice library used is `options?.ReferenceVoiceLibrary`, or when that is null a new
  `ReferenceVoiceLibrary` over `catalog.Store`, so the user folder and installed voice pack are offered by
  default. The library is passed to the model separately; the caller's options object is never modified.
  Voice files named by `ReferenceVoiceDirectory` are read when the method runs, so the voice list is fixed
  from then on.

**Error Handling**: `ArgumentNullException` when the catalog is null. Exceptions from the model constructor
(for example duplicate voice ids or a missing voice directory) propagate unchanged.

**Dependencies**: `SpeechModelCatalog` and `ReferenceVoiceLibrary` from the Speech library, and
`OnnxChatterboxTurboEnglishSynthesisModel` and `ChatterboxTurboModelOptions` from this package's
ModelManagementSubsystem.
