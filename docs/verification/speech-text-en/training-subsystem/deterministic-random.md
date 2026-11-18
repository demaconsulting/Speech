### DeterministicRandom Verification

#### Verification Approach

`DeterministicRandom` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/DeterministicRandomTests.cs`. The evidence is
reference output: the first values of the sequence of seed 0 (the published reference sequence of the generator the
design names) and of the bounded draws of seed 42, and the order that a shuffle of a fixed list gives, were computed
independently of the unit and are written into the tests as constants, so a change of the recurrence, the way a bounded
value is derived or the way a shuffle walks the list fails a test on any platform. No fake is needed.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test creates its own generator

#### Acceptance Criteria

The 64-bit sequence of seed 0 and the bounded sequence of seed 42 equal their reference values; a bound of 1 always
gives 0 and no bound is ever reached (bounds 1, 7 and 1,000,003); a bound of 0 or less throws
`ArgumentOutOfRangeException`; a shuffle with seed 42 gives the reference order, keeps every item exactly once, repeats
for the same seed and differs for another.

#### Test Scenarios

##### Reproducible sequences and shuffle

**Tests**: `DeterministicRandom_NextUInt64_SeedZero_MatchesReferenceSequence`,
`DeterministicRandom_NextInt_Seed42_MatchesReferenceSequence`,
`DeterministicRandom_NextInt_AnyBound_StaysBelowTheBound` (a theory over three bounds),
`DeterministicRandom_Shuffle_Seed42_ProducesReferencePermutation`,
`DeterministicRandom_Shuffle_SameSeedRepeatsAndDifferentSeedDiffers`

The first outputs of seed 0 and the first bounded draws of seed 42 equal the reference constants. Many draws never reach
the bound and are never negative. A list of consecutive numbers shuffled with seed 42 equals the reference permutation;
a shuffled list of 100 numbers holds the same items, the same seed repeats the order and another seed gives another
order.

##### Bound validation

**Test**: `DeterministicRandom_NextInt_NonPositiveBound_ThrowsArgumentOutOfRangeException` (a theory over 0 and -5)

A bound of 0 and a bound of -5 each throw `ArgumentOutOfRangeException`.
