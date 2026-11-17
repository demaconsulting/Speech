### DeterministicMath Verification

#### Verification Approach

`DeterministicMath` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/DeterministicMathTests.cs`. The reference is the
framework's own `Math.Exp` and `Math.Log`: the tests sweep wide ranges of arguments and require the unit's results to
agree with them to a relative error of 1e-12, which is the accuracy that keeps the chosen calibration temperature the
right one. The tests cannot show that the unit's results are identical on every platform, because the framework's
functions that they compare with may themselves differ in the last bits; that property rests on the unit using only
operations whose results are specified exactly (it is the reason the unit exists) and is evidenced across operating
systems by the pinned model hash of the subsystem test, which the CI build matrix runs on every system. No fake is
needed.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: the unit is a pure function; no state is shared between tests

#### Acceptance Criteria

The exponential agrees with the framework's to a relative error of 1e-12 for every argument from -700.00 to 7.99 in
steps of 0.01; the logarithm agrees with the framework's to 1e-12 (relative to its result, or absolute when that result
is smaller than one) for arguments from 1e-300 to 1e300 and in a fine sweep around one; the logarithm of a subnormal
number agrees to 1e-9; the logarithm of the exponential of a number returns the number to 1e-11; the exponential of 0 is
1, of -1000 is 0, of 1000 is positive infinity and of not-a-number is not-a-number; the logarithm of 1 is 0, of 0 is
negative infinity, of -1 and of not-a-number is not-a-number and of positive infinity is positive infinity.

#### Test Scenarios

##### Exponential accuracy

**Tests**: `DeterministicMath_Exp_SweepOfArguments_MatchesFrameworkWithinTolerance`,
`DeterministicMath_Log_OfExp_ReturnsTheArgument`

A sweep of 70,800 arguments from far below any real score difference up to modest positive values is compared with the
framework's exponential. The logarithm of the exponential of seven numbers between -5.5 and 20 returns each number.

##### Logarithm accuracy

**Tests**: `DeterministicMath_Log_SweepOfArguments_MatchesFrameworkWithinTolerance`,
`DeterministicMath_Log_SubnormalArgument_MatchesFramework`

The logarithms of numbers over 600 orders of magnitude, and of numbers just below and above one, are compared with the
framework's. The logarithm of a subnormal number is compared with the framework's.

##### Special values

**Test**: `DeterministicMath_ExpAndLog_SpecialValues_AreDefined`

The special values listed in the acceptance criteria are returned exactly.
