### SpeechModelParameters (ISpeechModelParameter, NumericParameter, ChoiceParameter, BooleanParameter, SpeechModelParameterDiagnostics)

**Purpose**: Provide a typed, self-describing tunable-parameter descriptor set so a host can
render an appropriate generic control (slider, dropdown, checkbox) for any model's declared
parameters without knowing that model in advance, and a shared validation helper so both
composition factories (`SpeechRecognizerFactory`, `SpeechSynthesizerFactory`) apply identical
rules for what counts as a valid supplied value.

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
- `SpeechModelParameterDiagnostics`: an internal, stateless static helper with a single
  `ValidateAndReport(modelId, declaredParameters, parameterValues, diagnostics, category)` entry
  point; carries no data of its own.

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
- **SpeechModelParameterDiagnostics.ValidateAndReport(modelId, declaredParameters,
  parameterValues, diagnostics, category)**: Called once, up front, by both composition factories'
  innermost `Create` overloads (before either factory does any other work). Iterates
  `declaredParameters` in their declared order; for each one present as a key in
  `parameterValues`, validates the supplied value against that parameter's own rules - a
  `NumericParameter` value must be a `double`/`int`/`float` within `[Minimum, Maximum]`, and
  additionally a whole number when `IsInteger` is `true` (a fractional value is rejected outright,
  never silently rounded); a `ChoiceParameter` value must be a `string` matching one declared
  `ChoiceParameterOption.Value` exactly; a `BooleanParameter` value must be a `bool`. The first
  invalid recognized value found (in declared-parameter order, so behavior is deterministic)
  throws `ArgumentException` immediately, naming the parameter id, the model id, and the specific
  reason the value is invalid; the exception's `ParamName` is always `"parameterValues"` - the
  actual public parameter name a caller passed the offending bag through as - rather than the
  internal parameter id, consistent with this codebase's convention of every `ArgumentException`
  populating `ParamName`. Only once every declared, supplied parameter validates does it
  report an `Info` diagnostic for each key in `parameterValues` that names no parameter declared
  by this model ("Parameter '{id}' is not declared by this model and was ignored.") - this case
  never throws, since a host reusing one settings bag across different models must not break just
  because one model doesn't declare a parameter another model had. A `null` or empty
  `parameterValues` bag is a no-op.

**Error Handling**: Every parameter-descriptor constructor validates eagerly and throws
`ArgumentException`/`ArgumentNullException` for an invalid or internally inconsistent descriptor
at construction time - never silently accepted and discovered later when a host tries to render
it. `SpeechModelParameterDiagnostics.ValidateAndReport` throws `ArgumentException` for an invalid
value supplied for a parameter this model *does* declare (a caller bug - the host explicitly
targeted this parameter on this model), but never throws for a supplied key naming a parameter
this model does not declare (a legitimate cross-model compatibility gap, only reported as an
`Info` diagnostic).

**Dependencies**: `ISpeechModelParameter`/`NumericParameter`/`ChoiceParameter`/`BooleanParameter`
depend on none beyond each other (`ChoiceParameter` depends on `ChoiceParameterOption`).
`SpeechModelParameterDiagnostics` depends on all four descriptor types plus `ISpeechDiagnostics`.

**Callers**: `ISpeechModel.Parameters`; a model's own backing class declares instances of these
types; a host UI renders controls from them and supplies values back in an untyped key-value bag
keyed by `Id`. `SpeechModelParameterDiagnostics.ValidateAndReport` is called exclusively by
`SpeechRecognizerFactory.Create` and `SpeechSynthesizerFactory.Create`'s innermost overloads.
