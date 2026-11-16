### EnglishTextAnalyzerOptions

**Purpose**: Carry the host's configuration of an `EnglishTextAnalyzer`: which annotation families to
produce, an optional counts-only diagnostics sink and an optional dictionary of pronunciation overrides. The
`TextEnAnnotationSelection` flags enumeration that expresses the selection is documented with this unit.

**Data Model**: `EnglishTextAnalyzerOptions` is a sealed record with three `init` properties. `Diagnostics`
is an `ISpeechDiagnostics?`, null by default. `Overrides` is a `PronunciationOverrides?`, null by default (no
overrides); the dictionary is immutable, so the record holds the reference and a `with` copy shares it.
`Annotations` is a `TextEnAnnotationSelection`, defaulting to
`PartOfSpeech | Normalization` (the analyzer does what the package is for unless the host narrows it).
`TextEnAnnotationSelection` is a `[Flags]` enumeration with `None` (0), `PartOfSpeech` (1) and
`Normalization` (2); later releases add families such as readings as further flags without changing these
values. `None` still yields the tokenizer's sentence breaks, unreconciled. Selecting `Normalization` alone
needs no tagger. The analyzer copies the values it needs when it is constructed, so a later `with` copy never
affects an existing analyzer. Overrides are applied by the normalization pass, so a non-empty dictionary
requires `Normalization`; the options record itself accepts any combination and the `EnglishTextAnalyzer`
constructor rejects this one.

**Key Methods**: the `Annotations` initializer validates its value on every initialization, including in a
`with` expression: any bit other than `PartOfSpeech` and `Normalization` throws
`ArgumentOutOfRangeException`, so a typo or a value from a newer release fails loudly rather than being
treated as "something selected". Every combination of the two defined flags, including each alone, is
accepted.

**Error Handling**: `ArgumentOutOfRangeException` for an undefined flag; nothing else.

**Dependencies**: the Speech library's `ISpeechDiagnostics` and the `PronunciationOverrides` unit.

**Callers**: `EnglishTextAnalyzer` (reads the values at construction) and the host application or model
package that builds the options.
