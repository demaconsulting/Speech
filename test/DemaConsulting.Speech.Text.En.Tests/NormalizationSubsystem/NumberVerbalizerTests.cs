using System.Globalization;
using DemaConsulting.Speech.Text.En.NormalizationSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests.NormalizationSubsystem;

/// <summary>
///     Unit tests for <see cref="NumberVerbalizer"/>: the words of cardinals, ordinals, years,
///     decades, decimals, digit sequences, fractions, clock times and calendar dates, compared
///     against the independent <see cref="ReferenceNumberWords"/> implementation.
/// </summary>
public sealed class NumberVerbalizerTests
{
    /// <summary>Proves every value from 0 to 9999 is spoken exactly as the independent reference speaks it.</summary>
    [Fact]
    public void NumberVerbalizer_Cardinal_EveryValueFrom0To9999_MatchesReference()
    {
        // Act / Assert: with and without leading zeros
        for (var value = 0UL; value <= 9999; value++)
        {
            var expected = ReferenceNumberWords.Cardinal(value);
            Assert.True(expected == NumberVerbalizer.Cardinal(value.ToString(CultureInfo.InvariantCulture)), $"value {value}");
            Assert.True(expected == NumberVerbalizer.Cardinal("000" + value.ToString(CultureInfo.InvariantCulture)), $"padded value {value}");
        }
    }

    /// <summary>Proves ten thousand seeded random values of up to 15 digits are spoken as the reference speaks them.</summary>
    [Fact]
    public void NumberVerbalizer_Cardinal_RandomValuesUpTo15Digits_MatchesReference()
    {
        // Arrange
        var random = new Random(20260801);

        // Act / Assert: the magnitude is drawn first so every length from 1 to 15 digits is well represented
        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            var digits = random.Next(1, 16);
            var value = (ulong)(random.NextInt64(0, 1_000_000_000_000_000L) % (long)Math.Pow(10, digits));
            Assert.True(
                ReferenceNumberWords.Cardinal(value) == NumberVerbalizer.Cardinal(value.ToString(CultureInfo.InvariantCulture)),
                $"value {value}");
        }
    }

    /// <summary>Proves the values on both sides of every scale boundary use the right scale words and skip empty groups.</summary>
    [Fact]
    public void NumberVerbalizer_Cardinal_ScaleBoundaries_UseCorrectScaleWords()
    {
        // Arrange
        (string Digits, string Spoken)[] cases =
        [
            ("999", "nine hundred ninety nine"),
            ("1000", "one thousand"),
            ("1001", "one thousand one"),
            ("999999", "nine hundred ninety nine thousand nine hundred ninety nine"),
            ("1000000", "one million"),
            ("1000001", "one million one"),
            ("1001000", "one million one thousand"),
            ("999999999", "nine hundred ninety nine million nine hundred ninety nine thousand nine hundred ninety nine"),
            ("1000000000", "one billion"),
            ("1000000000000", "one trillion"),
            ("999999999999999", "nine hundred ninety nine trillion nine hundred ninety nine billion nine hundred ninety nine million nine hundred ninety nine thousand nine hundred ninety nine"),
            ("000000", "zero"),
        ];

        // Act / Assert
        foreach (var (digits, spoken) in cases)
        {
            Assert.Equal(spoken, NumberVerbalizer.Cardinal(digits));
        }
    }

    /// <summary>Proves every ordinal from 0 to 9999 is spoken as the independent reference speaks it.</summary>
    [Fact]
    public void NumberVerbalizer_Ordinal_EveryValueFrom0To9999_MatchesReference()
    {
        // Act / Assert
        for (var value = 0UL; value <= 9999; value++)
        {
            Assert.True(
                ReferenceNumberWords.Ordinal(value) == NumberVerbalizer.Ordinal(value.ToString(CultureInfo.InvariantCulture)),
                $"value {value}");
        }
    }

    /// <summary>Proves the ordinal word ends the way the written suffix (st, nd, rd, th) says, for 1 to 1000.</summary>
    [Fact]
    public void NumberVerbalizer_OrdinalSuffix_MatchesNumber_For1To1000()
    {
        // Act / Assert
        for (var value = 1; value <= 1000; value++)
        {
            var suffix = (value % 100) is >= 11 and <= 13
                ? "th"
                : (value % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            var last = NumberVerbalizer.Ordinal(value.ToString(CultureInfo.InvariantCulture)).Split(' ')[^1];
            var wordSuffix = last switch { "first" => "st", "second" => "nd", "third" => "rd", _ => "th" };
            Assert.True(suffix == wordSuffix, $"value {value}: written suffix {suffix}, ordinal word {last}");
        }
    }

    /// <summary>Proves every year from 1000 to 2099 is spoken as the independent reference speaks it, and a year outside the range is a cardinal.</summary>
    [Fact]
    public void NumberVerbalizer_Year_EveryYearFrom1000To2099_MatchesReference()
    {
        // Act / Assert
        for (var year = 1000UL; year <= 2099; year++)
        {
            Assert.True(ReferenceNumberWords.Year(year) == NumberVerbalizer.Year(year.ToString(CultureInfo.InvariantCulture)), $"year {year}");
        }

        Assert.Equal("nine thousand nine hundred ninety nine", NumberVerbalizer.Year("9999"));
        Assert.Equal("two thousand one hundred", NumberVerbalizer.Year("2100"));
        Assert.Equal("nine hundred ninety nine", NumberVerbalizer.Year("0999"));
        Assert.True(NumberVerbalizer.IsYearReadingDifferent(1999));
        Assert.False(NumberVerbalizer.IsYearReadingDifferent(2000));
        Assert.False(NumberVerbalizer.IsYearReadingDifferent(1000));
        Assert.False(NumberVerbalizer.IsYearReadingDifferent(2005));
        Assert.False(NumberVerbalizer.IsYearReadingDifferent(2100));
    }

    /// <summary>Proves the decade forms: two-digit decades, century decades, tens and the thousands.</summary>
    [Fact]
    public void NumberVerbalizer_Decade_Forms_AreSpoken()
    {
        // Arrange
        (string Digits, string Spoken)[] cases =
        [
            ("10", "tens"), ("20", "twenties"), ("30", "thirties"), ("40", "forties"), ("50", "fifties"),
            ("60", "sixties"), ("70", "seventies"), ("80", "eighties"), ("90", "nineties"),
            ("1990", "nineteen nineties"), ("1900", "nineteen hundreds"), ("1800", "eighteen hundreds"),
            ("2000", "two thousands"), ("2010", "twenty tens"), ("2090", "twenty nineties"), ("1000", "one thousands"),
            ("1060", "ten sixties"), ("1100", "eleven hundreds"),
        ];

        // Act / Assert
        foreach (var (digits, spoken) in cases)
        {
            Assert.Equal(spoken, NumberVerbalizer.Decade(digits));
        }
    }

    /// <summary>Proves the fraction digits of a decimal are read one by one, zeros included, and an empty integer part is just the point.</summary>
    [Fact]
    public void NumberVerbalizer_Decimal_FractionDigitsAreSpelledIncludingZeros()
    {
        // Act / Assert
        Assert.Equal("three point one four", NumberVerbalizer.Decimal("3", "14"));
        Assert.Equal("five point zero", NumberVerbalizer.Decimal("5", "0"));
        Assert.Equal("zero point zero five", NumberVerbalizer.Decimal("0", "05"));
        Assert.Equal("point five", NumberVerbalizer.Decimal("", "5"));
        Assert.Equal("one thousand two hundred point two", NumberVerbalizer.Decimal("1200", "2"));
        Assert.Equal("zero zero seven point five", NumberVerbalizer.Decimal("007", "5"));
        Assert.Equal(
            "zero point " + string.Join(' ', Enumerable.Repeat("one", 32)),
            NumberVerbalizer.Decimal("0", new string('1', 32)));
    }

    /// <summary>Proves every digit is spelled, in order, with single spaces.</summary>
    [Fact]
    public void NumberVerbalizer_Digits_SpellsEveryDigit()
    {
        // Act / Assert
        Assert.Equal("zero one two three four five six seven eight nine", NumberVerbalizer.Digits("0123456789"));
        Assert.Equal("seven", NumberVerbalizer.Digits("7"));
        Assert.Equal(64, NumberVerbalizer.Digits(new string('0', 64)).Split(' ').Length);
    }

    /// <summary>Proves fractions: singular for one part, plural otherwise, halves and quarters, and every denominator the recognizer allows.</summary>
    [Fact]
    public void NumberVerbalizer_Fraction_SingularPluralAndSpecialDenominators()
    {
        // Arrange
        (int Numerator, int Denominator, string Spoken)[] cases =
        [
            (1, 2, "one half"), (1, 3, "one third"), (2, 3, "two thirds"), (1, 4, "one quarter"), (3, 4, "three quarters"),
            (1, 5, "one fifth"), (4, 5, "four fifths"), (1, 6, "one sixth"), (5, 6, "five sixths"), (1, 7, "one seventh"),
            (1, 8, "one eighth"), (7, 8, "seven eighths"), (1, 9, "one ninth"), (8, 9, "eight ninths"), (1, 10, "one tenth"),
            (9, 10, "nine tenths"), (1, 16, "one sixteenth"), (15, 16, "fifteen sixteenths"), (1, 32, "one thirty second"),
            (3, 32, "three thirty seconds"), (1, 64, "one sixty fourth"), (63, 64, "sixty three sixty fourths"),
            (1, 100, "one hundredth"), (37, 100, "thirty seven hundredths"), (99, 100, "ninety nine hundredths"),
        ];

        // Act / Assert
        foreach (var (numerator, denominator, spoken) in cases)
        {
            Assert.Equal(spoken, NumberVerbalizer.Fraction(numerator, denominator));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => NumberVerbalizer.Fraction(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => NumberVerbalizer.Fraction(2, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => NumberVerbalizer.Fraction(1, 101));
    }

    /// <summary>Proves all 24 hours times 60 minutes are spoken as the reference speaks them, with and without a leading zero and a meridiem.</summary>
    [Fact]
    public void NumberVerbalizer_Clock_AllHoursAndMinutes_MatchReference()
    {
        // Act / Assert
        for (var hour = 0; hour < 24; hour++)
        {
            for (var minute = 0; minute < 60; minute++)
            {
                var minuteText = minute.ToString("00", CultureInfo.InvariantCulture);
                var plain = hour.ToString(CultureInfo.InvariantCulture);
                var padded = hour.ToString("00", CultureInfo.InvariantCulture);
                Assert.True(
                    ReferenceNumberWords.Clock((ulong)hour, (ulong)minute, false, false) == NumberVerbalizer.Clock(plain, minuteText, false),
                    $"{plain}:{minuteText}");

                // A leading zero exists only for the hours 0 to 9 written with two digits.
                if (hour < 10)
                {
                    Assert.True(
                        ReferenceNumberWords.Clock((ulong)hour, (ulong)minute, true, false) == NumberVerbalizer.Clock(padded, minuteText, false),
                        $"{padded}:{minuteText}");
                }
                else
                {
                    Assert.True(
                        ReferenceNumberWords.Clock((ulong)hour, (ulong)minute, false, false) == NumberVerbalizer.Clock(padded, minuteText, false),
                        $"{padded}:{minuteText} without a leading zero");
                }
                Assert.True(
                    ReferenceNumberWords.Clock((ulong)hour, (ulong)minute, false, true) == NumberVerbalizer.Clock(plain, minuteText, true),
                    $"{plain}:{minuteText} with a meridiem");
            }
        }
    }

    /// <summary>Proves the named clock forms: o'clock, hundred, oh, a leading zero and midnight.</summary>
    [Fact]
    public void NumberVerbalizer_Clock_OClockHundredAndLeadingZeroForms()
    {
        // Act / Assert
        Assert.Equal("five o'clock", NumberVerbalizer.Clock("5", "00", false));
        Assert.Equal("twelve o'clock", NumberVerbalizer.Clock("12", "00", false));
        Assert.Equal("thirteen hundred", NumberVerbalizer.Clock("13", "00", false));
        Assert.Equal("seventeen hundred", NumberVerbalizer.Clock("17", "00", false));
        Assert.Equal("oh seven hundred", NumberVerbalizer.Clock("07", "00", false));
        Assert.Equal("oh seven oh five", NumberVerbalizer.Clock("07", "05", false));
        Assert.Equal("zero hundred", NumberVerbalizer.Clock("00", "00", false));
        Assert.Equal("zero hundred", NumberVerbalizer.Clock("0", "00", false));
        Assert.Equal("zero thirty", NumberVerbalizer.Clock("0", "30", false));
        Assert.Equal("five", NumberVerbalizer.Clock("5", "00", true));
        Assert.Equal("five thirty", NumberVerbalizer.Clock("5", "30", true));
    }

    /// <summary>Proves the month names and the day ordinals 1 to 31, and the date words built from them.</summary>
    [Fact]
    public void NumberVerbalizer_Calendar_MonthNamesAndDayOrdinals_AreSpoken()
    {
        // Act / Assert: month names
        for (var month = 1; month <= 12; month++)
        {
            Assert.Equal(ReferenceNumberWords.Month(month), NumberVerbalizer.MonthName(month));
        }

        // Act / Assert: every day ordinal
        for (var day = 1; day <= 31; day++)
        {
            Assert.Equal(ReferenceNumberWords.Ordinal((ulong)day), NumberVerbalizer.DayOrdinal(day));
        }

        // Act / Assert: whole dates
        var writer = new SpokenWriter(stackalloc char[NumberVerbalizer.MaxSpokenLength]);
        NumberVerbalizer.AppendDate(ref writer, 12, 25, "2024");
        Assert.Equal("December twenty fifth twenty twenty four", writer.ToString());
        var shortYear = new SpokenWriter(stackalloc char[NumberVerbalizer.MaxSpokenLength]);
        NumberVerbalizer.AppendDate(ref shortYear, 1, 2, "05");
        Assert.Equal("January second oh five", shortYear.ToString());
        var twoDigit = new SpokenWriter(stackalloc char[NumberVerbalizer.MaxSpokenLength]);
        NumberVerbalizer.AppendDate(ref twoDigit, 3, 4, "24");
        Assert.Equal("March fourth twenty four", twoDigit.ToString());
    }

    /// <summary>Proves the documented worst-case inputs of every reading stay inside the 1,024-character output bound.</summary>
    [Fact]
    public void NumberVerbalizer_WorstCaseInputs_StayWithinOutputBound()
    {
        // Arrange: the longest words repeated at every cap
        var spelled = new string('7', NumberVerbalizer.MaxSpelledDigits);
        var fraction = new string('7', NumberVerbalizer.MaxFractionDigits);
        var longestCardinal = "877877877877877";

        // Act
        var results = new List<string>
        {
            NumberVerbalizer.Cardinal(longestCardinal),
            NumberVerbalizer.Ordinal(longestCardinal),
            NumberVerbalizer.Digits(spelled),
            NumberVerbalizer.Decimal(longestCardinal, fraction),
            NumberVerbalizer.Decimal("0" + spelled[..63], fraction),
            NumberVerbalizer.Year("1888"),
            NumberVerbalizer.Clock("07", "37", false),
        };
        var writer = new SpokenWriter(stackalloc char[NumberVerbalizer.MaxSpokenLength]);
        NumberVerbalizer.AppendDate(ref writer, 9, 23, "1877");
        results.Add(writer.ToString());

        // Assert
        Assert.All(results, result => Assert.True(result.Length <= NumberVerbalizer.MaxSpokenLength, $"{result.Length} characters"));
        Assert.Throws<InvalidOperationException>(() =>
        {
            var tiny = new SpokenWriter(stackalloc char[8]);
            tiny.Append("overflowing words");
        });
    }

    /// <summary>Proves no output of any reading contains a digit, and every word is in the independent reference's closed vocabulary.</summary>
    [Fact]
    public void NumberVerbalizer_AnyOutput_HasNoDigitAndOnlyReferenceVocabulary()
    {
        // Arrange
        var outputs = new List<string>();
        for (var value = 0; value <= 2200; value++)
        {
            var digits = value.ToString(CultureInfo.InvariantCulture);
            outputs.Add(NumberVerbalizer.Cardinal(digits));
            outputs.Add(NumberVerbalizer.Ordinal(digits));
            outputs.Add(NumberVerbalizer.Digits(digits));
            outputs.Add(NumberVerbalizer.Decimal(digits, "05"));
            if (digits.Length == 4)
            {
                outputs.Add(NumberVerbalizer.Year(digits));
            }
        }

        for (var denominator = 2; denominator <= 100; denominator++)
        {
            outputs.Add(NumberVerbalizer.Fraction(1, denominator));
            outputs.Add(NumberVerbalizer.Fraction(denominator - 1, denominator));
        }

        for (var hour = 0; hour < 24; hour++)
        {
            outputs.Add(NumberVerbalizer.Clock(hour.ToString(CultureInfo.InvariantCulture), "07", false));
            outputs.Add(NumberVerbalizer.Clock(hour.ToString("00", CultureInfo.InvariantCulture), "00", false));
        }

        foreach (var decade in new[] { "10", "20", "90", "1990", "2000", "1900" })
        {
            outputs.Add(NumberVerbalizer.Decade(decade));
        }

        // Act / Assert
        foreach (var output in outputs)
        {
            Assert.DoesNotContain(output, character => character is >= '0' and <= '9');
            Assert.True(ReferenceNumberWords.IsInVocabulary(output), $"'{output}' leaves the closed vocabulary");
            Assert.Equal(output.Trim(), output);
            Assert.DoesNotContain("  ", output, StringComparison.Ordinal);
        }
    }

    /// <summary>Proves the words never depend on the current culture, including cultures with other digits, other casing rules and other calendars.</summary>
    [Fact]
    public void NumberVerbalizer_UnderForeignCultures_GivesIdenticalWords()
    {
        // Arrange
        string Sample() => string.Join(
            "|",
            NumberVerbalizer.Cardinal("1234567"),
            NumberVerbalizer.Ordinal("21"),
            NumberVerbalizer.Year("1999"),
            NumberVerbalizer.Decade("1990"),
            NumberVerbalizer.Decimal("3", "14"),
            NumberVerbalizer.Fraction(3, 16),
            NumberVerbalizer.Clock("17", "05", false),
            NumberVerbalizer.MonthName(9),
            NumberVerbalizer.DayOrdinal(1),
            NumberVerbalizer.Digits("0123456789"));
        var expected = Sample();
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            // Act / Assert: Turkish (capital and lowercase I), Arabic, German, Thai and Persian
            foreach (var name in new[] { "tr-TR", "ar-SA", "de-DE", "th-TH", "fa-IR" })
            {
                CultureInfo.CurrentCulture = new CultureInfo(name);
                CultureInfo.CurrentUICulture = new CultureInfo(name);
                Assert.Equal(expected, Sample());
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    /// <summary>Proves anything that is not ASCII digits, an empty or oversized input, and out-of-range arguments are rejected with an argument exception.</summary>
    [Fact]
    public void NumberVerbalizer_InvalidDigits_ThrowArgumentException()
    {
        // Assert: not ASCII digits (letters, signs, spaces, other scripts, full-width and mathematical digits)
        foreach (var bad in new[] { string.Empty, "12a", "-5", " 5", "5 ", "1,2", "\u0663", "\uFF15", "5\u00B2", "\uD835\uDFD9" })
        {
            Assert.ThrowsAny<ArgumentException>(() => NumberVerbalizer.Cardinal(bad));
            Assert.ThrowsAny<ArgumentException>(() => NumberVerbalizer.Ordinal(bad));
            Assert.ThrowsAny<ArgumentException>(() => NumberVerbalizer.Digits(bad));
        }

        // Assert: beyond the documented limits
        Assert.Throws<ArgumentException>(() => NumberVerbalizer.Cardinal("1000000000000000"));
        Assert.Throws<ArgumentException>(() => NumberVerbalizer.Digits(new string('1', 65)));
        Assert.Throws<ArgumentException>(() => NumberVerbalizer.Decimal("1", string.Empty));
        Assert.Throws<ArgumentException>(() => NumberVerbalizer.Decimal("1", new string('1', 33)));
        Assert.Throws<ArgumentException>(() => NumberVerbalizer.Decimal("1", "x"));

        // Assert: shapes of the other readings
        Assert.Throws<ArgumentException>(() => NumberVerbalizer.Year("199"));
        Assert.Throws<ArgumentException>(() => NumberVerbalizer.Year("19999"));
        Assert.Throws<ArgumentException>(() => NumberVerbalizer.Decade("95"));
        Assert.Throws<ArgumentException>(() => NumberVerbalizer.Decade("00"));
        Assert.Throws<ArgumentException>(() => NumberVerbalizer.Decade("2100"));
        Assert.Throws<ArgumentException>(() => NumberVerbalizer.Clock("24", "00", false));
        Assert.Throws<ArgumentException>(() => NumberVerbalizer.Clock("5", "60", false));
        Assert.Throws<ArgumentException>(() => NumberVerbalizer.Clock("5", "3", false));
        Assert.Throws<ArgumentException>(() => NumberVerbalizer.Clock("123", "30", false));
        Assert.Throws<ArgumentOutOfRangeException>(() => NumberVerbalizer.DayOrdinal(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NumberVerbalizer.DayOrdinal(32));
        Assert.Throws<ArgumentOutOfRangeException>(() => NumberVerbalizer.MonthName(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NumberVerbalizer.MonthName(13));
    }
}
