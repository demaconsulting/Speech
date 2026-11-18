### DeterministicRandom Verification

#### Verification Approach

`DeterministicRandom` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/DeterministicRandomTests.cs`. The evidence is
reference output: the first values of the sequences of three seeds, and the order that a shuffle of a fixed list gives,
were computed independently of the unit (the 64-bit sequence for seed 0 is the published reference sequence of the
generator the design names) and are written into the tests as constants, so a change of the recurrence, the way a
bounded value is derived or the way a shuffle walks the list fails a test on any platform. No fake is needed.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test creates its own generator

#### Acceptance Criteria

The 64-bit sequences of seed 0 and of seed 1234567 equal their reference values; the bounded sequence of seed 42 equals
its reference values and stays in range; a bound of 1 always gives 0 and any bound is never reached (bounds 1, 2, 7 and
1,000,003); a bound of 0 or less throws `ArgumentOutOfRangeException`; over many draws every value below a small bound
is drawn about equally often; a shuffle with seed 42 gives the reference order; it keeps every item exactly once,
repeats for the same seed and differs for another; an empty list and a list of one item are unchanged.

#### Test Scenarios

##### Reference sequences

**Tests**: `DeterministicRandom_NextUInt64_SeedZero_MatchesReferenceSequence`,
`DeterministicRandom_NextUInt64_Seed1234567_MatchesReferenceSequence`,
`DeterministicRandom_NextInt_Seed42_MatchesReferenceSequence`

The first outputs of each seed equal the reference constants. A second seed besides zero shows that the recurrence, and
not a coincidence of the zero state, is what is right.

##### Bounded draws

**Tests**: `DeterministicRandom_NextInt_AnyBound_StaysBelowTheBound` (a theory over four bounds),
`DeterministicRandom_NextInt_ManyDraws_AreRoughlyUniform`

Many draws never reach the bound and are never negative. Every value below a small bound is drawn within a tolerance of
the expected share over many draws.

##### Bound validation

**Test**: `DeterministicRandom_NextInt_NonPositiveBound_ThrowsArgumentOutOfRangeException` (a theory over 0 and -5)

A bound of 0 and a bound of -5 each throw `ArgumentOutOfRangeException`.

##### Shuffle

**Tests**: `DeterministicRandom_Shuffle_Seed42_ProducesReferencePermutation`,
`DeterministicRandom_Shuffle_SameSeedRepeatsAndDifferentSeedDiffers`,
`DeterministicRandom_Shuffle_EmptyAndSingleItem_AreUnchanged`

A list of consecutive numbers shuffled with seed 42 equals the reference permutation. The shuffled list holds the same
items, the same seed repeats the order and another seed gives another order. Shuffling an empty list or a list of one
item changes nothing.
