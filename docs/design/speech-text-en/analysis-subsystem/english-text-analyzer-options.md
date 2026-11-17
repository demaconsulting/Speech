### EnglishTextAnalyzerOptions

**Purpose**: Carry the host's configuration of an `EnglishTextAnalyzer`: which annotation families to
produce, an optional counts-only diagnostics sink, an optional dictionary of pronunciation overrides and the
confidence below which a heteronym reading counts as low confidence. The
`TextEnAnnotationSelection` flags enumeration that expresses the selection is documented with this unit.

**Data Model**: `EnglishTextAnalyzerOptions` is a sealed record with four `init` properties. `Diagnostics`
is an `ISpeechDiagnostics?`, null by default. `Overrides` is a `PronunciationOverrides?`, null by default (no
overrides); the dictionary is immutable, so the record holds the reference and a `with` copy shares it.
`Annotations` is a `TextEnAnnotationSelection`, defaulting to
`PartOfSpeech | Normalization | Reading` (the analyzer does what the package is for unless the host narrows it).
`LowConfidenceThreshold` is a `double`, 0.60 by default. `TextEnAnnotationSelection` is a `[Flags]` enumeration
with `None` (0), `PartOfSpeech` (1), `Normalization` (2) and `Reading` (4); later releases add further families as
further flags without changing these values. `Reading` covers the `en.reading` and `en.reading-confidence`
annotations together, there is no separate confidence flag, and the family is effective only where a heteronym model
exists (a `Reading` override is honored without one, see *EnglishTextAnalyzer Design*), so the default selection
changes nothing for a host while no model is embedded. `None` still yields the tokenizer's sentence breaks,
unreconciled. Selecting `Normalization` alone needs no tagger. The analyzer copies the values it needs when it is
constructed, so a later `with` copy never affects an existing analyzer. Overrides are applied by the normalization
pass, so a non-empty dictionary requires `Normalization`; the options record itself accepts any combination and the
`EnglishTextAnalyzer` constructor rejects this one.

**Key Methods**: the `Annotations` initializer validates its value on every initialization, including in a
`with` expression: any bit other than `PartOfSpeech`, `Normalization` and `Reading` throws
`ArgumentOutOfRangeException`, so a typo or a value from a newer release fails loudly rather than being
treated as "something selected" (the value 4 is the defined `Reading` flag and 8 is the first undefined one). Every
combination of the three defined flags, including each alone, is accepted. The `LowConfidenceThreshold` initializer
validates on every initialization, including in a `with` expression: a value that is NaN, infinite, negative or above 1
throws `ArgumentOutOfRangeException`, and every finite value from 0 to 1 is accepted and copied. The threshold only
counts and flags classified readings (the heteronym diagnostics and `AnalyzedToken.IsLowConfidenceReading`); it never
removes a reading.

**Error Handling**: `ArgumentOutOfRangeException` for an undefined flag or an out-of-range threshold; nothing else.

**Dependencies**: the Speech library's `ISpeechDiagnostics` and the `PronunciationOverrides` unit.

**Callers**: `EnglishTextAnalyzer` (reads the values at construction) and the host application or model
package that builds the options.
