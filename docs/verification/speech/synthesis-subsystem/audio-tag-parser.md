### AudioTagParser

#### Verification Approach

`AudioTagParser` and its `AudioTagCatalog` alias table are verified directly through pure unit
tests over plain strings; no test double is required since neither type depends on a model,
engine, audio device, or native runtime.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup: every test is a pure function call with no shared or persisted state.

#### Acceptance Criteria

This unit is considered verified when every canonical tag is reachable through at least one
unambiguous alias, alias resolution is case-insensitive and whitespace-tolerant, `Parse` produces
a correctly ordered span sequence for plain text, tags, and mixtures of the two (including edge
placement and adjacency), and every unrecognized or malformed bracket form passes through as
literal text rather than throwing or being discarded.

#### Test Scenarios

See the SynthesisSubsystem-level scenarios "Vocabulary: Completeness and Alias Resolution",
"Vocabulary: Normalization, Case-Insensitivity, and Whitespace Tolerance", "Vocabulary:
Unresolvable Content", "Parsing: Span Sequence and Ordering", and "Parsing: Malformed and
Unrecognized Bracket Passthrough".
