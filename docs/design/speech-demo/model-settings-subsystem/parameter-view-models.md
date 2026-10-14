### ParameterViewModels

**Purpose**:

The parameter presenters turn one library parameter descriptor into the presentation state a
bound control needs. `ParameterViewModelBase` carries the state shared by every parameter kind;
`NumericParameterViewModel`, `ChoiceParameterViewModel` and `BooleanParameterViewModel` each add
the state and constraint of one kind. The four types are documented as one unit because the
abstract base is only observable through its concrete presenters.

**Data Model**:

| Member | Type | Purpose |
| --- | --- | --- |
| `Id` | `string` | The stable key used in the untyped key-value bag |
| `DisplayName` | `string` | The short label a host UI shows |
| `Description` | `string` | The longer explanation shown as a tooltip or help text |
| `BoxedValue` | `abstract object` | The parameter's current value, boxed for the value bag |

`NumericParameterViewModel` adds `Value`, `Minimum`, `Maximum`, `Step`, `Unit` and `IsInteger`;
`ChoiceParameterViewModel` adds `Options` and `SelectedOption`; `BooleanParameterViewModel` adds
`Value`.

| Presenter | Rendered as | Starting value | Constraint enforced |
| --- | --- | --- | --- |
| `NumericParameterViewModel` | Slider/`NumericUpDown` | Declared default | Clamped; whole if integer; NaN/inf ignored |
| `ChoiceParameterViewModel` | Dropdown | Option matching default | Declared options only; null/non-member ignored |
| `BooleanParameterViewModel` | Checkbox / toggle | Declared default | None beyond the two boolean states |

**Key Methods**:

- **Numeric `Value` setter**: clamps on every write specifically so a bound control that allows a
  wider range than declared — for example, free-text entry — or a test setting the value directly
  can never push the parameter outside the range the model declared it could honor. When the
  underlying `NumericParameter.IsInteger` is `true` (for example a multi-speaker
  model's `speaker` parameter, a discrete index with no fractional meaning), `Value` additionally
  rounds every write to the nearest whole number (`MidpointRounding.AwayFromZero`) after clamping.
  A continuous parameter keeps fractional values. A non-finite write (`NaN`, `+Infinity`,
  `-Infinity`) cannot be clamped meaningfully and is ignored: `Value` keeps its previous value and
  no notification is raised.
- **Control selection**: `NumericParameterViewModel.IsInteger` mirrors the library descriptor's
  flag; this is the presenter's only contribution (it reports the flag and offers no control).
  The separate, view-level outcome is realized by `ModelSettingsView.axaml`'s `DataTemplate`, which renders
  Avalonia's built-in `NumericUpDown` bound to `Value`/`Minimum`/`Maximum`/`Step` with `FormatString="0"` — for an
  integer parameter
  instead of the `Slider` used for a continuous one, via `IsInteger`/`!IsInteger` visibility
  bindings, so a fractional value can never be selected in the first place. That view-level outcome is
  verified at subsystem level by a structural test over the view markup.
- **Choice presenter**: exposes the declared options and preselects the option whose value matches
  the declared default, resolved by value rather than by position. Changing `SelectedOption`
  raises a property-changed notification and updates `BoxedValue`. A `null` or non-member option
  assigned to `SelectedOption` is ignored: the previous selection is kept, so `BoxedValue` is
  never `null` and only declared option values ever reach the value bag.
- **Boolean presenter**: starts at the declared default; toggling `Value` updates `BoxedValue`.

**Error Handling**:

- Each concrete presenter's constructor rejects a null library descriptor with
  `ArgumentNullException`, because a presenter built with no descriptor has nothing to display.
- The library validates a choice parameter's default (it must be one of the declared options) and
  that numeric bounds are finite when the descriptor is constructed. The presenters therefore need
  no guards for those cases: `ChoiceParameterViewModel` selects the option matching the default
  with a lookup that always succeeds for a library-constructed descriptor.
- Runtime writes are guarded instead: `ChoiceParameterViewModel.SelectedOption` ignores `null` and
  options not in `Options`, and `NumericParameterViewModel.Value` ignores `NaN` and infinities,
  each keeping the previous value rather than throwing.

**Dependencies**:

- The library's `NumericParameter`, `ChoiceParameter` and `BooleanParameter` descriptors

**Callers**:

- `ModelSettingsViewModel`, which constructs one presenter per recognized declared parameter
- `ModelSettingsView.axaml`, whose `DataTemplate`s bind to the presenters' members
