### ModelSettingsViewModel

**Purpose**:

`ModelSettingsViewModel` is the settings panel's presentation state. It presents one presenter per
parameter the selected model declares, explains an empty panel honestly, assembles the current
values into the untyped value bag a host forwards to synthesis, and restores a previously captured
bag onto a replacement model's presenters.

**Data Model**:

| Member | Type | Purpose |
| --- | --- | --- |
| `NoModelMessage` | `const string` | The explanation shown when no model is selected |
| `NoParametersMessage` | `const string` | The explanation shown for a model with no declared parameters |
| `Parameters` | `ObservableCollection<ParameterViewModelBase>` | The rendered presenters, in declaration order |
| `Model` | `ISpeechModel?` | The model whose parameters are presented |
| `HasModel` / `HasParameters` / `IsEmpty` | `bool` | The current empty-state classification |
| `EmptyMessage` | `string` | The message matching the current empty state |

**Key Methods**:

- **Rebuild (assigning `Model`)**: assigning `Model` — including assigning `null` — clears
  `Parameters` and rebuilds it by pattern-matching each of the model's declared parameters to its
  concrete presenter type, skipping any parameter kind the view model does not recognize.
  Presenters are rebuilt rather than merged because a newly selected model's parameter set is
  unrelated to the previous one's; keeping a stale presenter alive across a model change would let
  the panel offer a control for a parameter the new model does not declare. The empty-state
  members are re-notified on every assignment.
- **Empty states**: `IsEmpty` is true whenever there is no model selected or the selected model
  declares no parameters; `EmptyMessage` distinguishes the two cases. This mirrors the honest
  empty-state precedent the model catalog panel established for an empty `SpeechModelCatalog`: an
  unexplained blank panel would read to a user as a broken application rather than an expected
  state.
- **`BuildValueBag()`**: assembles every presented parameter's current `BoxedValue` into a
  dictionary keyed by `Id`, and returns an empty bag when no model is selected. If two presenters
  carry the same `Id`, the later presenter's value wins.
- **`RestoreValues(values)`**: applies a previously captured bag onto the current presenters:
  choices by option value, numerics as `double`, booleans as `bool`. Unknown keys, wrongly typed
  values and options the model no longer offers are ignored, leaving the default.
  `SynthesisPanelViewModel` captures the bag before a catalog rebuild (a voice pack install or
  removal) and restores it afterwards so the user keeps their chosen voice and other settings.
  Because the replacement model instance is presented before the restore, options that only the
  replacement declares (for example newly installed reference voices) are offered immediately, and
  a restored selection that the replacement still offers stays selected.

**Error Handling**:

- `RestoreValues` throws `ArgumentNullException` for a null bag; every other stale or malformed
  entry in the bag is ignored rather than failing the refresh that triggered the restore.
- `BuildValueBag` tolerates duplicate parameter identifiers: each presenter writes its value with
  an indexer assignment, so the later presenter's value replaces the earlier one's instead of
  throwing. Duplicate identifiers are not expected from a well-formed model, so the view model
  does not reject them.
- The library validates a choice parameter's default and the finiteness of numeric bounds when the
  descriptor is constructed, so the presenters need no defensive guards for those cases (see
  _ParameterViewModels Design_).
- A parameter kind the view model does not recognize is skipped, so a model carrying a custom
  `ISpeechModelParameter` implementation still presents every parameter that is recognized.

**Dependencies**:

- The library's `ISpeechModel` and `ISpeechModelParameter` descriptors
- The presenters of the _ParameterViewModels_ unit

**Callers**:

- `SynthesisPanelViewModel`, which embeds the view model, assigns `Model`, and calls
  `BuildValueBag()` and `RestoreValues()`
- `ModelSettingsView.axaml`, which binds to `Parameters`, `IsEmpty`, and `EmptyMessage`
