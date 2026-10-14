### SpeechModelCatalogChatterboxTurboExtensions

**Purpose**: Provide the opt-in registration seam through which a host composes this package with the
Speech library: one `AddChatterboxTurboModels()` call registers the package's single synthesis model into a
`SpeechModelCatalog`, so the Speech library never depends on this package.

**Data Model**: None. The class is a static holder of one extension method; it keeps no state.

**Key Methods**:

- **AddChatterboxTurboModels(this SpeechModelCatalog catalog, ChatterboxTurboModelOptions? options = null)**:
  returns `SpeechModelCatalog`. Constructs one `OnnxChatterboxTurboEnglishSynthesisModel` from the options, adds
  it with `SpeechModelCatalog.AddModels`, and returns the same catalog for chaining.
  The reference-voice library used is `options?.ReferenceVoiceLibrary`, or when that is null a new
  `ReferenceVoiceLibrary` over `catalog.Store`, so the user folder and installed voice pack are offered by
  default. The library is passed to the model separately; the caller's options object is never modified.
  Voice files named by `ReferenceVoiceDirectory` are read when the method runs, so the voice list is fixed
  from then on.
  - *Preconditions*: `catalog` is not null; `options` is optional (null selects the defaults); a named voice
    directory exists and its voice ids are unique.
  - *Postconditions*: the returned catalog is the same instance that was supplied; exactly one Chatterbox Turbo
    model has been registered in it; the supplied options object is unchanged.

**Error Handling**: `ArgumentNullException` when the catalog is null. Exceptions from the model constructor
(for example duplicate voice ids or a missing voice directory) propagate unchanged.

**Callers**: Host applications that build a `SpeechModelCatalog` - the Speech command-line tool (through its model
catalog adapter) and the Speech demo application (at startup) - call this method once per catalog to opt in to
the Chatterbox Turbo model.

**Dependencies**: `SpeechModelCatalog` and `ReferenceVoiceLibrary` from the Speech library, and
`OnnxChatterboxTurboEnglishSynthesisModel` and `ChatterboxTurboModelOptions` from this package's
ModelManagementSubsystem.
