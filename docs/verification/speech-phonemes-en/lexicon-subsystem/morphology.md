### Morphology Verification

#### Verification Approach

`Morphology` is verified by unit tests in
`test/DemaConsulting.Speech.Phonemes.En.Tests/LexiconSubsystemTests.Morphology.cs` (the `Morphology_*` tests) against
the real lexicon for the derivation, and with pronunciations written in the test for the sound rules, which need no
lexicon. The golden derivations are written out in the tests as theories; the statistical agreement with the dictionary
is the subsystem test (see *LexiconSubsystem Verification*).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: no files or network; the rules are pure functions

#### Acceptance Criteria

Seventeen regular forms are derived to the expected pronunciation (plurals `cats dogs boxes cries`, pasts
`walked wanted stopped cried`, `-ing` forms `making running`, `slowly`, `accidentally`, and the possessives and
contractions `cat's cats' they'll it'll isn't`); four words that no stem explains (`xyzzy`, `qwxzs`, the empty string
and `s`) give no derivation; the ending of a stem is chosen by its last phone (`K AE1 T` plural `K AE1 T S`, `D AO1 G`
plural `D AO1 G Z`, `B AA1 K S` plural `B AA1 K S IH0 Z`, and the past tense forms `T`, `D` and `IH0 D`); and a clitic
is attached by the last phone of its host (`AY1` + `'ll` is `AY1 L`, `IH1 T` + `'ll` is `IH1 T AH0 L`). A superlative
after a stem that ends in a stressed `NG` takes the velar stop (`S T R AO1 NG` + `-est` is `S T R AO1 NG G AH0 S T`, but
`W IH1 N IH0 NG` + `-est` does not) and a derived comparative does not; the dictionary variants of `stronger`, `longer`
and `younger` start with the one that ends in `NG G ER0`, and those of `singer`, `hanger`, `linger` and `cat` keep their
dictionary order.

#### Test Scenarios

##### Derivation from a known stem

**Tests**: `Morphology_TryDerive_RegularForm_DerivesFromStem` (a seventeen-row theory),
`Morphology_TryDerive_NoKnownStem_ReturnsFalse` (a four-row theory)

##### Sound of the ending

**Tests**: `Morphology_Apply_EndingByLastPhone` (a twelve-row theory), `Morphology_AppendClitic_ByHostLastPhone` (a
five-row theory)

##### Order of the dictionary variants

**Test**: `Morphology_OrderVariants_ComparativeInNg_StartsWithTheVelarStop` (a seven-row theory)
