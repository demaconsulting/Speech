### DeterministicMath

**Purpose**: Compute the exponential and the natural logarithm with only addition, subtraction, multiplication and
division of `double` in a fixed order, so that the trainer's model selection never depends on a platform's math
library. The platform implementations of `Math.Exp` and `Math.Log` may differ in the last bits between operating
systems and runtimes, which could in principle flip the choice of the calibration temperature and so change a trained
model.

**Data Model**: `DeterministicMath` is an internal static class with no state and only constants: the two parts of the
natural logarithm of two (the high part has low bits that are zero, so a product with a small integer is exact), the
reciprocal of it, the term counts of the two series (16 Taylor terms for the exponential, 14 odd-power terms for the
logarithm) and the argument limits of the exponential (below -745 the result is 0, above 709 it is infinity).

**Key Methods**:

- **Exp(x)**: not a number gives not a number, an argument out of the limits gives 0 or positive infinity. Otherwise
  the argument is reduced to `x = k ln2 + r` with `|r|` at most half of `ln2` (the floor is exact in IEEE arithmetic),
  `exp(r)` is evaluated by Horner's rule over the Taylor series from the highest term down, and the result is scaled by
  `2^k` exactly, in two halves so that neither factor overflows or underflows alone (the powers of two are built from
  the bits of a double)
- **Log(x)**: not a number or a negative number gives not a number, zero gives negative infinity, positive infinity
  gives itself. A subnormal number is first scaled into the normal range by an exact multiplication by `2^54`. Then
  `x = m 2^e` is split from the bits, `m` is brought into `[sqrt(2)/2, sqrt(2))` by one exact halving, and `log(m)` is
  `2 atanh(s)` with `s = (m - 1) / (m + 1)` summed from the smallest term up; the result adds the exponent times the two
  parts of `ln2`

The functions agree with the library functions to a relative error far below `1e-12` over the domain the trainer uses;
the point is that the same input gives the same bits on every platform.

**Error Handling**: none is needed or raised: every input has a defined result, including the non-finite ones above.

**Dependencies**: the framework only (`Math.Floor` and `BitConverter`, which are exact).

**Callers**: `ModelSelection` (the loss of the temperature grid) and the unit tests. The runtime classifier does not use
this unit: its confidence uses `Math.Exp`, which never affects a decision or a model byte.
