### DeterministicRandom

**Purpose**: Supply a pseudo-random sequence that depends only on its seed, never on the runtime, the operating system
or the framework version, so that the order in which training rows are visited and the clusters a bootstrap resample
draws are reproducible everywhere. The framework's own generator is deliberately not used: its algorithm is not part of
its contract and may change between runtime versions.

**Data Model**: `DeterministicRandom` is an internal sealed class with one 64-bit state, set from the seed. The
algorithm is SplitMix64, a fixed integer recurrence: the state advances by the constant `0x9E3779B97F4A7C15` and the
output is the state mixed by two xor-shift and multiply rounds (the multipliers `0xBF58476D1CE4E5B9` and
`0x94D049BB133111EB`) and a final xor-shift. Every seed is valid, including zero. An instance belongs to one sequence of
draws and is not thread-safe.

**Key Methods**:

- **NextUInt64()**: the next 64-bit value; the arithmetic is unchecked and wraps by design
- **NextInt(bound)**: a value in `[0, bound)` that is exactly uniform. A draw that falls in the short interval at the
  bottom of the 64-bit range, the one that would make some residues likelier than others (`2^64 mod bound` values), is
  rejected and redrawn, so there is no modulo bias. A bound that is not positive throws
  `ArgumentOutOfRangeException`
- **Shuffle(items)**: the Fisher-Yates permutation in place, from the last position down, each step swapping with a
  position drawn by `NextInt`. Every permutation is equally likely and the same seed and length always give the same
  permutation

**Error Handling**: the only failure is the non-positive bound of `NextInt`, a defect of the caller that throws
`ArgumentOutOfRangeException`. There is no I/O.

**Dependencies**: none (the framework's integer arithmetic only).

**Callers**: `AveragedPerceptronTrainer` (the visiting order of the rows of each epoch), `HeteronymEvaluator` (the
cluster draws of the bootstrap) and the unit tests.
