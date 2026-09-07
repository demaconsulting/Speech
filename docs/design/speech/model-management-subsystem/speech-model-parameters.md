### SpeechModelParameters (ISpeechModelParameter, NumericParameter, ChoiceParameter, BooleanParameter)

**Purpose**: Provide a typed, self-describing tunable-parameter descriptor set so a host can
render an appropriate generic control (slider, dropdown, checkbox) for any model's declared
parameters without knowing that model in advance.

**Data Model**:

- `ISpeechModelParameter`: common contract - `Id`, `DisplayName`, `Description`.
- `NumericParameter`: `Minimum`, `Maximum`, `Step`, `Default`, optional `Unit`, `IsInteger`
  (defaults to `false`).
- `NumericParameterBounds`: a small `readonly record struct` bundling `Minimum`, `Maximum`,
  `Step`, and `Default` into a single constructor argument, keeping `NumericParameter`'s own
  constructor parameter count small. It has no behavior of its own; `NumericParameter` still
  validates and exposes the four values as its own individual properties.
- `ChoiceParameter`: ordered `Options` (`IReadOnlyList<ChoiceParameterOption>`, each a
  `Value`/`Label` pair) and `Default`.
- `BooleanParameter`: `Default`.

**Key Methods**:

- **NumericParameter(id, displayName, description, bounds, unit?, isInteger?)**: Validates all
  four values in `bounds` (`Minimum`, `Maximum`, `Step`, `Default`) are finite,
  `Minimum <= Maximum`, `Step > 0`, and `Minimum <= Default <= Maximum`. When `isInteger` is
  `true` (default `false`), additionally validates that every value in `bounds` is a whole
  number - an inherently whole-number value (for example a discrete speaker index) has no
  fractional meaning, so a host renders it with a numeric up-down rather than a continuous
  slider, guaranteeing no fractional value can be selected in the first place.
- **ChoiceParameter(id, displayName, description, options, default)**: Validates `options` is
  non-empty, contains no duplicate `Value`s, and that `default` matches one option's `Value`
  exactly (ordinal comparison).
- **ChoiceParameterOption(value, label)**: Validates `value` is non-empty/non-whitespace.
- **BooleanParameter(id, displayName, description, default)**: Validates only the common fields.

**Error Handling**: Every constructor validates eagerly and throws `ArgumentException`/
`ArgumentNullException` for an invalid or internally inconsistent descriptor at construction
time - never silently accepted and discovered later when a host tries to render it.

**Dependencies**: None beyond each other (`ChoiceParameter` depends on `ChoiceParameterOption`).

**Callers**: `ISpeechModel.Parameters`; a model's own backing class declares instances of these
types; a host UI renders controls from them and supplies values back in an untyped key-value bag
keyed by `Id`.
