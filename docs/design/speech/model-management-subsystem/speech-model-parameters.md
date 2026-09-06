### SpeechModelParameters (ISpeechModelParameter, NumericParameter, ChoiceParameter, BooleanParameter)

**Purpose**: Provide a typed, self-describing tunable-parameter descriptor set so a host can
render an appropriate generic control (slider, dropdown, checkbox) for any model's declared
parameters without knowing that model in advance.

**Data Model**:

- `ISpeechModelParameter`: common contract - `Id`, `DisplayName`, `Description`.
- `NumericParameter`: `Minimum`, `Maximum`, `Step`, `Default`, optional `Unit`, `IsInteger`
  (defaults to `false`).
- `ChoiceParameter`: ordered `Options` (`IReadOnlyList<ChoiceParameterOption>`, each a
  `Value`/`Label` pair) and `Default`.
- `BooleanParameter`: `Default`.

**Key Methods**:

- **NumericParameter(id, displayName, description, minimum, maximum, step, default, unit?,
  isInteger?)**: Validates all bounds are finite, `minimum <= maximum`, `step > 0`, and
  `minimum <= default <= maximum`. When `isInteger` is `true` (default `false`), additionally
  validates that `minimum`, `maximum`, `step`, and `default` are all whole numbers - an
  inherently whole-number value (for example a discrete speaker index) has no fractional
  meaning, so a host renders it with a numeric up-down rather than a continuous slider,
  guaranteeing no fractional value can be selected in the first place.
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
