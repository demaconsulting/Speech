namespace DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

/// <summary>
///     The exponential and natural logarithm, computed with only addition, subtraction, multiplication and
///     division on <see cref="double"/> in a fixed order, so the trainer's model selection never depends on a
///     platform's math library.
/// </summary>
/// <remarks>
///     <para>
///     The platform implementations of <see cref="Math.Exp(double)"/> and <see cref="Math.Log(double)"/> may differ in
///     the last bits between operating systems. Those differences could, in principle, flip the choice of the
///     calibration temperature (a minimum over many nearly equal values) and so change a trained model. These
///     functions use range reduction, a fixed-degree polynomial and exact power-of-two scaling, so the same input
///     gives the same bits on every platform; they agree with the library functions to a relative error far below
///     <c>1e-12</c> over the domain the trainer uses.
///     </para>
///     <para>
///     The type is static and thread-safe.
///     </para>
/// </remarks>
internal static class DeterministicMath
{
    /// <summary>The high part of the natural logarithm of two (its low bits are zero, so a product with a small integer is exact).</summary>
    private const double Ln2High = 6.93147180369123816490e-01;

    /// <summary>The low part of the natural logarithm of two, so that high plus low is the logarithm to double precision.</summary>
    private const double Ln2Low = 1.90821492927058770002e-10;

    /// <summary>The reciprocal of the natural logarithm of two.</summary>
    private const double InverseLn2 = 1.44269504088896338700e+00;

    /// <summary>The number of Taylor terms of the exponential of the reduced argument.</summary>
    private const int ExpTerms = 16;

    /// <summary>The number of odd-power terms of the logarithm series.</summary>
    private const int LogTerms = 14;

    /// <summary>The argument below which the exponential underflows to zero.</summary>
    private const double ExpUnderflow = -745.0;

    /// <summary>The argument above which the exponential overflows to infinity.</summary>
    private const double ExpOverflow = 709.0;

    /// <summary>The square root of two, the upper edge of the logarithm's reduced interval.</summary>
    private const double SquareRootTwo = 1.41421356237309504880;

    /// <summary>The number of explicit mantissa bits of a double.</summary>
    private const int MantissaBits = 52;

    /// <summary>The exponent bias of a double.</summary>
    private const int ExponentBias = 1023;

    /// <summary>
    ///     Computes <c>e</c> raised to a power.
    /// </summary>
    /// <param name="x">The exponent.</param>
    /// <returns>
    ///     The exponential; <c>0</c> for an argument below about <c>-745</c>, positive infinity above about <c>709</c>,
    ///     and NaN for NaN.
    /// </returns>
    public static double Exp(double x)
    {
        if (double.IsNaN(x))
        {
            return double.NaN;
        }

        if (x < ExpUnderflow)
        {
            return 0.0;
        }

        if (x > ExpOverflow)
        {
            return double.PositiveInfinity;
        }

        // x = k ln2 + r with |r| <= ln2 / 2, so exp(x) = 2^k exp(r). Floor is exact in IEEE arithmetic.
        var k = Math.Floor((x * InverseLn2) + 0.5);
        var r = (x - (k * Ln2High)) - (k * Ln2Low);

        // exp(r) by Horner evaluation of the Taylor series, from the highest term down.
        var sum = 1.0;
        for (var n = ExpTerms; n >= 1; n--)
        {
            sum = 1.0 + (r * sum / n);
        }

        return ScaleByPowerOfTwo(sum, (int)k);
    }

    /// <summary>
    ///     Computes the natural logarithm.
    /// </summary>
    /// <param name="x">The argument.</param>
    /// <returns>The logarithm; negative infinity for zero, NaN for a negative number or NaN, positive infinity for positive infinity.</returns>
    public static double Log(double x)
    {
        if (double.IsNaN(x) || x < 0.0)
        {
            return double.NaN;
        }

        if (x == 0.0)
        {
            return double.NegativeInfinity;
        }

        if (double.IsPositiveInfinity(x))
        {
            return x;
        }

        // A subnormal number is scaled into the normal range first (an exact multiplication by 2^54).
        var exponent = 0;
        if (x < 2.2250738585072014e-308)
        {
            x *= 18014398509481984.0;
            exponent = -54;
        }

        // x = m 2^e with m in [1, 2), then m in [sqrt(2)/2, sqrt(2)) by one exact halving.
        var bits = BitConverter.DoubleToInt64Bits(x);
        exponent += (int)((bits >> MantissaBits) & 0x7FF) - ExponentBias;
        var m = BitConverter.Int64BitsToDouble((bits & ((1L << MantissaBits) - 1)) | ((long)ExponentBias << MantissaBits));
        if (m > SquareRootTwo)
        {
            m /= 2.0;
            exponent++;
        }

        // log(m) = 2 atanh(s) with s = (m - 1) / (m + 1), summed from the smallest term up.
        var s = (m - 1.0) / (m + 1.0);
        var s2 = s * s;
        var series = 0.0;
        for (var n = LogTerms; n >= 0; n--)
        {
            series = (1.0 / ((2 * n) + 1)) + (s2 * series);
        }

        var logM = 2.0 * s * series;
        return ((exponent * Ln2High) + logM) + (exponent * Ln2Low);
    }

    /// <summary>
    ///     Multiplies a finite positive number by two raised to an integer power, exactly.
    /// </summary>
    /// <param name="value">The value, in the range of a reduced exponential (about 0.7 to 1.5).</param>
    /// <param name="power">The power of two, about -1075 to 1024.</param>
    /// <returns>The scaled value; a result below the normal range is produced in two exact steps.</returns>
    private static double ScaleByPowerOfTwo(double value, int power)
    {
        // Scale in two halves so neither factor over- or underflows on its own.
        var half = power / 2;
        return value * PowerOfTwo(half) * PowerOfTwo(power - half);
    }

    /// <summary>
    ///     Builds two raised to an integer power from the bits of a double.
    /// </summary>
    /// <param name="power">The power, -1022 to 1023.</param>
    /// <returns>The power of two.</returns>
    private static double PowerOfTwo(int power) => BitConverter.Int64BitsToDouble((long)(power + ExponentBias) << MantissaBits);
}
