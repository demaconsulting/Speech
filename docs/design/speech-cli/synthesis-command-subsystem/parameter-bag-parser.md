### ParameterBagParser

**Purpose**: Turn repeatable `key=value` flag tokens into the typed parameter bag a model loader
expects, validating each value against the parameters the resolved model declares. It serves
`--tts-param` for `speak` (and the synthesis side of `ask`) and `--stt-param` for `recognize` (and
the recognition side of `ask`).

**Data Model**: The unit is a stateless static helper. A raw token is a `(Key, Value)` pair of
strings; the result is a `Dictionary<string, object>` keyed by parameter id whose values are boxed
`double`, `string` or `bool` according to the declared parameter's kind.

**Key Methods**:

- **ParseToken(token, flagName)**: Splits a raw token at its first `=` into key and value. A token
  with no `=` or an empty key half is rejected; an empty value half is accepted as the empty
  string.
- **Resolve(rawValues, declaredParameters, flagName)**: Looks each key up in the declared
  parameters by exact (ordinal) id and resolves the value by the parameter's concrete kind:
  - `NumericParameter`: parsed as a `double` with invariant culture, checked against `Minimum` and
    `Maximum`, and, when the parameter is integer-only, checked to be a whole number.
  - `ChoiceParameter`: matched ordinally against the declared option values and returned as the
    string.
  - `BooleanParameter`: parsed case-insensitively as `true` or `false`.

  An empty token list yields an empty bag. A key the model does not declare is rejected with a
  message naming the key. This is deliberately stricter than the library loaders, which ignore (and
  log) an unrecognized key: parameter values supplied programmatically may target several engine
  versions, so ignoring is right there, but on the command line an unrecognized key is almost
  always an operator typo, and failing before any synthesis or recognition starts is better than
  silently running with a setting that was never applied.

**Error Handling**: Every rejection is an `ArgumentException` whose message names the invoking flag,
the key or parameter id, and the offending value. Both methods take the invoking flag name and echo
it verbatim, so the same code reports `--tts-param` for `speak` and `--stt-param` for `recognize`.
A null token list, parameter list or flag name is rejected with `ArgumentNullException`. A declared
parameter of a kind other than numeric, choice or boolean is rejected as an unrecognized kind.

**Dependencies**: The library's public `ISpeechModelParameter`, `NumericParameter`,
`ChoiceParameter` and `BooleanParameter` types.

**Callers**: `SpeakCommand` here, and the recognition and conversation command subsystems
(`RecognizeCommand` and `AskCommand`).
