namespace DemaConsulting.Speech.Text.En.NormalizationSubsystem;

/// <summary>
///     A bounded, append-only buffer of spoken words over caller-supplied memory, so building the
///     spoken form of one number allocates nothing until the finished text is read.
/// </summary>
/// <remarks>
///     Words are separated by exactly one space; the writer never inserts a leading or trailing
///     space. A writer is a stack-only value: it cannot be stored in a field of a class or
///     shared between threads. The capacity is fixed by the caller (the documented
///     <see cref="NumberVerbalizer.MaxSpokenLength"/>); exceeding it is a defect of the caller's
///     input caps and throws rather than truncating.
/// </remarks>
internal ref struct SpokenWriter
{
    /// <summary>The memory the words are written to.</summary>
    private readonly Span<char> _buffer;

    /// <summary>The number of characters written so far.</summary>
    private int _length;

    /// <summary>
    ///     Initializes a new writer over <paramref name="buffer"/>.
    /// </summary>
    /// <param name="buffer">The memory to write to; its length is the capacity.</param>
    internal SpokenWriter(Span<char> buffer)
    {
        _buffer = buffer;
        _length = 0;
    }

    /// <summary>
    ///     Gets the number of characters written so far.
    /// </summary>
    internal readonly int Length => _length;

    /// <summary>
    ///     Appends one word (or several words that already contain their own single spaces),
    ///     preceded by a separating space unless the writer is empty.
    /// </summary>
    /// <param name="words">The words. Must not be null or empty.</param>
    /// <exception cref="InvalidOperationException">The capacity would be exceeded.</exception>
    internal void Append(string words)
    {
        if (_length > 0)
        {
            Reserve(1 + words.Length);
            _buffer[_length++] = ' ';
        }
        else
        {
            Reserve(words.Length);
        }

        words.AsSpan().CopyTo(_buffer[_length..]);
        _length += words.Length;
    }

    /// <summary>
    ///     Appends characters directly after the last word, without a separating space (an
    ///     inflection such as the plural <c>s</c>).
    /// </summary>
    /// <param name="suffix">The characters to append.</param>
    /// <exception cref="InvalidOperationException">The capacity would be exceeded.</exception>
    internal void AppendJoined(string suffix)
    {
        Reserve(suffix.Length);
        suffix.AsSpan().CopyTo(_buffer[_length..]);
        _length += suffix.Length;
    }

    /// <summary>
    ///     Replaces the last word written (everything after the last space) by another word.
    /// </summary>
    /// <param name="replacement">The replacement word. Must not be null or empty.</param>
    /// <exception cref="InvalidOperationException">The capacity would be exceeded.</exception>
    internal void ReplaceLastWord(string replacement)
    {
        var lastSpace = _buffer[.._length].LastIndexOf(' ');
        _length = lastSpace + 1;
        Reserve(replacement.Length);
        replacement.AsSpan().CopyTo(_buffer[_length..]);
        _length += replacement.Length;
    }

    /// <summary>
    ///     Gets the last word written (everything after the last space).
    /// </summary>
    /// <returns>The last word, or an empty string when nothing was written.</returns>
    internal readonly string LastWord()
    {
        var span = _buffer[.._length];
        return span[(span.LastIndexOf(' ') + 1)..].ToString();
    }

    /// <summary>
    ///     Returns the words written so far as one string.
    /// </summary>
    /// <returns>The spoken text; empty when nothing was written.</returns>
    public override readonly string ToString() => _buffer[.._length].ToString();

    /// <summary>
    ///     Checks that <paramref name="count"/> more characters fit.
    /// </summary>
    /// <param name="count">The number of characters about to be written.</param>
    /// <exception cref="InvalidOperationException">The capacity would be exceeded.</exception>
    private readonly void Reserve(int count)
    {
        if (count > _buffer.Length - _length)
        {
            throw new InvalidOperationException("The spoken text exceeds the documented size bound.");
        }
    }
}

/// <summary>
///     Builds the English words for numbers: cardinals, ordinals, years, decades, decimals,
///     digit sequences, fractions, clock times and calendar dates.
/// </summary>
/// <remarks>
///     <para>
///     Every method reads ASCII digits from a span and appends lowercase words (capitals only in
///     month names) to a <see cref="SpokenWriter"/>: separated by single spaces, with no hyphens,
///     no commas and no digits, so a phoneme backend sees only plain words (<c>twenty four</c>,
///     not <c>twenty-four</c>). Only <c>char</c> arithmetic is used; there is no
///     culture-sensitive parsing, formatting or casing, so the result never depends on the
///     current culture.
///     </para>
///     <para>
///     Documented limits: a cardinal or ordinal takes at most
///     <see cref="MaxCardinalDigits"/> significant digits (999 trillion), a digit-by-digit
///     reading at most <see cref="MaxSpelledDigits"/>, the fraction digits of a decimal at most
///     <see cref="MaxFractionDigits"/>, and no spoken value exceeds
///     <see cref="MaxSpokenLength"/> characters (the recognizer enforces the caps before it
///     calls; a call outside them throws). The class is stateless and thread-safe.
///     </para>
/// </remarks>
internal static class NumberVerbalizer
{
    /// <summary>The largest number of significant digits a cardinal, ordinal or year-like reading accepts.</summary>
    internal const int MaxCardinalDigits = 15;

    /// <summary>The largest number of digits that is read digit by digit.</summary>
    internal const int MaxSpelledDigits = 64;

    /// <summary>The largest number of digits after a decimal point that is read.</summary>
    internal const int MaxFractionDigits = 32;

    /// <summary>The documented upper bound of the length of any spoken value, in characters.</summary>
    internal const int MaxSpokenLength = 1024;

    /// <summary>The words of the numbers 0 to 19.</summary>
    private static readonly string[] Ones =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen",
    ];

    /// <summary>The words of the multiples of ten, indexed by the tens digit (the first two entries are unused).</summary>
    private static readonly string[] Tens =
    [
        string.Empty, string.Empty, "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety",
    ];

    /// <summary>The scale words, indexed by the number of groups of three digits below them.</summary>
    private static readonly string[] Scales = [string.Empty, "thousand", "million", "billion", "trillion"];

    /// <summary>The plural decade words, indexed by the tens digit (the first entry is unused).</summary>
    private static readonly string[] Decades =
    [
        string.Empty, "tens", "twenties", "thirties", "forties", "fifties", "sixties", "seventies", "eighties", "nineties",
    ];

    /// <summary>The month names, January first.</summary>
    private static readonly string[] Months =
    [
        "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December",
    ];

    /// <summary>
    ///     Gets the English name of a month.
    /// </summary>
    /// <param name="month">The month number, 1 (January) to 12 (December).</param>
    /// <returns>The capitalized month name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="month"/> is outside 1 to 12.</exception>
    internal static string MonthName(int month)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, 12);
        return Months[month - 1];
    }

    /// <summary>
    ///     Returns the cardinal words of a number written in ASCII digits.
    /// </summary>
    /// <param name="digits">The digits; leading zeros are ignored.</param>
    /// <returns>For example <c>one thousand two hundred thirty four</c>; <c>zero</c> for no value.</returns>
    /// <exception cref="ArgumentException"><paramref name="digits"/> is empty, holds a non-ASCII-digit, or has more than <see cref="MaxCardinalDigits"/> significant digits.</exception>
    internal static string Cardinal(ReadOnlySpan<char> digits)
    {
        var writer = new SpokenWriter(stackalloc char[MaxSpokenLength]);
        AppendCardinal(ref writer, digits);
        return writer.ToString();
    }

    /// <summary>
    ///     Returns the ordinal words of a number written in ASCII digits.
    /// </summary>
    /// <param name="digits">The digits; leading zeros are ignored.</param>
    /// <returns>For example <c>twenty first</c> or <c>one hundredth</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="digits"/> is empty, holds a non-ASCII-digit, or has more than <see cref="MaxCardinalDigits"/> significant digits.</exception>
    internal static string Ordinal(ReadOnlySpan<char> digits)
    {
        var writer = new SpokenWriter(stackalloc char[MaxSpokenLength]);
        AppendOrdinal(ref writer, digits);
        return writer.ToString();
    }

    /// <summary>
    ///     Returns the year-style words of a four-digit number: <c>nineteen ninety nine</c>,
    ///     <c>twenty twenty four</c>; the plain cardinal for a value outside 1000 to 2099.
    /// </summary>
    /// <param name="digits">Exactly four ASCII digits.</param>
    /// <returns>The words.</returns>
    /// <exception cref="ArgumentException"><paramref name="digits"/> is not exactly four ASCII digits.</exception>
    internal static string Year(ReadOnlySpan<char> digits)
    {
        var writer = new SpokenWriter(stackalloc char[MaxSpokenLength]);
        AppendYear(ref writer, digits);
        return writer.ToString();
    }

    /// <summary>
    ///     Returns the words of a decade written as digits, without its <c>s</c>.
    /// </summary>
    /// <param name="digits">Two digits (<c>90</c>) or four digits (<c>1990</c>) ending in zero.</param>
    /// <returns>For example <c>nineties</c>, <c>nineteen nineties</c>, <c>two thousands</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="digits"/> is not a two- or four-digit decade.</exception>
    internal static string Decade(ReadOnlySpan<char> digits)
    {
        var writer = new SpokenWriter(stackalloc char[MaxSpokenLength]);
        AppendDecade(ref writer, digits);
        return writer.ToString();
    }

    /// <summary>
    ///     Returns the digits one by one.
    /// </summary>
    /// <param name="digits">The digits.</param>
    /// <returns>For example <c>zero zero seven</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="digits"/> is empty, holds a non-ASCII-digit, or is longer than <see cref="MaxSpelledDigits"/>.</exception>
    internal static string Digits(ReadOnlySpan<char> digits)
    {
        var writer = new SpokenWriter(stackalloc char[MaxSpokenLength]);
        AppendDigits(ref writer, digits);
        return writer.ToString();
    }

    /// <summary>
    ///     Returns the words of a decimal number.
    /// </summary>
    /// <param name="integerDigits">The digits before the point; may be empty (<c>.5</c>).</param>
    /// <param name="fractionDigits">The digits after the point; at most <see cref="MaxFractionDigits"/>.</param>
    /// <returns>For example <c>three point five</c> or <c>point five</c>.</returns>
    /// <exception cref="ArgumentException">A part holds a non-ASCII-digit, the fraction is empty or too long, or the integer part exceeds its limits.</exception>
    internal static string Decimal(ReadOnlySpan<char> integerDigits, ReadOnlySpan<char> fractionDigits)
    {
        var writer = new SpokenWriter(stackalloc char[MaxSpokenLength]);
        AppendDecimal(ref writer, integerDigits, fractionDigits);
        return writer.ToString();
    }

    /// <summary>
    ///     Returns the words of a simple fraction.
    /// </summary>
    /// <param name="numerator">The numerator, at least 1.</param>
    /// <param name="denominator">The denominator, greater than the numerator.</param>
    /// <returns>For example <c>one half</c>, <c>three quarters</c>, <c>seven sixteenths</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The numerator is below 1 or the denominator is not above it, or is above 100.</exception>
    internal static string Fraction(int numerator, int denominator)
    {
        var writer = new SpokenWriter(stackalloc char[MaxSpokenLength]);
        AppendFraction(ref writer, numerator, denominator);
        return writer.ToString();
    }

    /// <summary>
    ///     Returns the words of a clock time.
    /// </summary>
    /// <param name="hour">One or two ASCII digits, 0 to 23; a leading zero is read as <c>oh</c>.</param>
    /// <param name="minute">Exactly two ASCII digits, 00 to 59.</param>
    /// <param name="omitZeroMinute">Whether minute 00 is left out (a meridiem follows).</param>
    /// <returns>For example <c>five thirty</c>, <c>five oh five</c>, <c>five o'clock</c>, <c>seventeen hundred</c>.</returns>
    /// <exception cref="ArgumentException">The hour or minute is malformed or out of range.</exception>
    internal static string Clock(ReadOnlySpan<char> hour, ReadOnlySpan<char> minute, bool omitZeroMinute)
    {
        var writer = new SpokenWriter(stackalloc char[MaxSpokenLength]);
        AppendClock(ref writer, hour, minute, omitZeroMinute);
        return writer.ToString();
    }

    /// <summary>
    ///     Returns the ordinal words of a day of the month.
    /// </summary>
    /// <param name="day">The day, 1 to 31.</param>
    /// <returns>For example <c>twenty fifth</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="day"/> is outside 1 to 31.</exception>
    internal static string DayOrdinal(int day)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(day, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(day, 31);
        var writer = new SpokenWriter(stackalloc char[MaxSpokenLength]);
        AppendUnderThousand(ref writer, day);
        writer.ReplaceLastWord(OrdinalWord(writer.LastWord()));
        return writer.ToString();
    }

    /// <summary>
    ///     Appends the cardinal words of a number written in ASCII digits.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="digits">The digits; leading zeros are ignored.</param>
    /// <exception cref="ArgumentException"><paramref name="digits"/> is empty, holds a non-ASCII-digit, or has more than <see cref="MaxCardinalDigits"/> significant digits.</exception>
    internal static void AppendCardinal(ref SpokenWriter writer, ReadOnlySpan<char> digits)
    {
        RequireDigits(digits);
        var significant = digits.TrimStart('0');
        if (significant.Length == 0)
        {
            writer.Append("zero");
            return;
        }

        if (significant.Length > MaxCardinalDigits)
        {
            throw new ArgumentException("A cardinal has at most 15 significant digits.", nameof(digits));
        }

        // The number is read in groups of three digits from the left; the first group holds the
        // remainder so that every later group is exactly three digits.
        var groups = (significant.Length + 2) / 3;
        var position = 0;
        for (var group = groups - 1; group >= 0; group--)
        {
            var size = group == groups - 1 ? significant.Length - ((groups - 1) * 3) : 3;
            var value = 0;
            for (var i = 0; i < size; i++)
            {
                value = (value * 10) + (significant[position + i] - '0');
            }

            position += size;
            if (value != 0)
            {
                AppendUnderThousand(ref writer, value);
                if (group > 0)
                {
                    writer.Append(Scales[group]);
                }
            }
        }
    }

    /// <summary>
    ///     Appends the ordinal words of a number written in ASCII digits.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="digits">The digits; leading zeros are ignored.</param>
    /// <exception cref="ArgumentException"><paramref name="digits"/> is empty, holds a non-ASCII-digit, or has more than <see cref="MaxCardinalDigits"/> significant digits.</exception>
    internal static void AppendOrdinal(ref SpokenWriter writer, ReadOnlySpan<char> digits)
    {
        // Only the last word of a cardinal changes in the ordinal: twenty one becomes twenty first.
        var start = writer.Length;
        AppendCardinal(ref writer, digits);
        if (writer.Length > start)
        {
            writer.ReplaceLastWord(OrdinalWord(writer.LastWord()));
        }
    }

    /// <summary>
    ///     Appends the words of an integer as it is read in running text: a number with a leading
    ///     zero (and more than one digit) is an identifier and is read digit by digit, any other
    ///     as a cardinal.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="digits">The digits.</param>
    /// <exception cref="ArgumentException">The digits are empty, malformed or beyond the limits of the chosen reading.</exception>
    internal static void AppendInteger(ref SpokenWriter writer, ReadOnlySpan<char> digits)
    {
        RequireDigits(digits);
        if (digits.Length > 1 && digits[0] == '0')
        {
            AppendDigits(ref writer, digits);
        }
        else
        {
            AppendCardinal(ref writer, digits);
        }
    }

    /// <summary>
    ///     Appends the digits one by one.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="digits">The digits.</param>
    /// <exception cref="ArgumentException"><paramref name="digits"/> is empty, holds a non-ASCII-digit, or is longer than <see cref="MaxSpelledDigits"/>.</exception>
    internal static void AppendDigits(ref SpokenWriter writer, ReadOnlySpan<char> digits)
    {
        RequireDigits(digits);
        if (digits.Length > MaxSpelledDigits)
        {
            throw new ArgumentException("At most 64 digits are read digit by digit.", nameof(digits));
        }

        foreach (var digit in digits)
        {
            writer.Append(Ones[digit - '0']);
        }
    }

    /// <summary>
    ///     Appends the words of a decimal number: the integer part as <see cref="AppendInteger"/>
    ///     reads it, <c>point</c>, and the fraction digits one by one (zeros kept).
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="integerDigits">The digits before the point; may be empty.</param>
    /// <param name="fractionDigits">The digits after the point; one to <see cref="MaxFractionDigits"/> digits.</param>
    /// <exception cref="ArgumentException">A part holds a non-ASCII-digit, the fraction is empty or too long, or the integer part exceeds its limits.</exception>
    internal static void AppendDecimal(ref SpokenWriter writer, ReadOnlySpan<char> integerDigits, ReadOnlySpan<char> fractionDigits)
    {
        if (fractionDigits.Length is 0 or > MaxFractionDigits)
        {
            throw new ArgumentException("A decimal has one to 32 fraction digits.", nameof(fractionDigits));
        }

        if (integerDigits.Length > 0)
        {
            AppendInteger(ref writer, integerDigits);
        }

        writer.Append("point");
        AppendDigits(ref writer, fractionDigits);
    }

    /// <summary>
    ///     Appends the year-style words of a four-digit number.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="digits">Exactly four ASCII digits.</param>
    /// <exception cref="ArgumentException"><paramref name="digits"/> is not exactly four ASCII digits.</exception>
    internal static void AppendYear(ref SpokenWriter writer, ReadOnlySpan<char> digits)
    {
        RequireDigits(digits);
        if (digits.Length != 4)
        {
            throw new ArgumentException("A year has exactly four digits.", nameof(digits));
        }

        var value = ParseSmall(digits);
        if (!IsYearReadingDifferent(value))
        {
            AppendCardinal(ref writer, digits);
            return;
        }

        AppendUnderThousand(ref writer, value / 100);
        var low = value % 100;
        if (low == 0)
        {
            writer.Append("hundred");
        }
        else if (low < 10)
        {
            writer.Append("oh");
            writer.Append(Ones[low]);
        }
        else
        {
            AppendUnderThousand(ref writer, low);
        }
    }

    /// <summary>
    ///     Tells whether the year-style words of a four-digit value differ from its cardinal words.
    /// </summary>
    /// <param name="value">The value, 0 to 9999.</param>
    /// <returns>
    ///     <see langword="false"/> for values outside 1000 to 2099 and for the values both readings
    ///     speak alike (<c>1000</c>, <c>2000</c> and <c>2001</c> to <c>2009</c>); otherwise <see langword="true"/>.
    /// </returns>
    internal static bool IsYearReadingDifferent(int value) =>
        value is >= 1000 and <= 2099 && value % 1000 != 0 && value is not (>= 2001 and <= 2009);

    /// <summary>
    ///     Appends the words of a decade written as digits, without its <c>s</c>.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="digits">Two digits (<c>90</c>, tens digit 1 to 9) or four digits (<c>1990</c>, value 1000 to 2099) ending in zero.</param>
    /// <exception cref="ArgumentException"><paramref name="digits"/> is not a two- or four-digit decade.</exception>
    internal static void AppendDecade(ref SpokenWriter writer, ReadOnlySpan<char> digits)
    {
        RequireDigits(digits);
        if (digits.Length == 2 && digits[0] != '0' && digits[1] == '0')
        {
            writer.Append(Decades[digits[0] - '0']);
            return;
        }

        if (digits.Length != 4 || digits[3] != '0')
        {
            throw new ArgumentException("A decade is two digits or four digits ending in zero.", nameof(digits));
        }

        var value = ParseSmall(digits);
        if (value is < 1000 or > 2099)
        {
            throw new ArgumentException("A four-digit decade is between 1000 and 2099.", nameof(digits));
        }

        if (value % 1000 == 0)
        {
            AppendUnderThousand(ref writer, value / 1000);
            writer.Append("thousands");
            return;
        }

        AppendUnderThousand(ref writer, value / 100);
        writer.Append(value % 100 == 0 ? "hundreds" : Decades[value % 100 / 10]);
    }

    /// <summary>
    ///     Appends the words of a simple fraction.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="numerator">The numerator, at least 1.</param>
    /// <param name="denominator">The denominator, greater than the numerator and at most 100.</param>
    /// <exception cref="ArgumentOutOfRangeException">The numerator is below 1 or the denominator is not above it, or is above 100.</exception>
    internal static void AppendFraction(ref SpokenWriter writer, int numerator, int denominator)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(numerator, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(denominator, numerator);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(denominator, 100);

        AppendUnderThousand(ref writer, numerator);
        var plural = numerator != 1;
        switch (denominator)
        {
            case 2:
                writer.Append(plural ? "halves" : "half");
                break;
            case 4:
                writer.Append(plural ? "quarters" : "quarter");
                break;
            case 100:
                writer.Append(plural ? "hundredths" : "hundredth");
                break;
            default:
                // The denominator is its ordinal ("sixteenth"), pluralized for more than one part.
                AppendUnderThousand(ref writer, denominator);
                writer.ReplaceLastWord(OrdinalWord(writer.LastWord()));
                if (plural)
                {
                    writer.AppendJoined("s");
                }

                break;
        }
    }

    /// <summary>
    ///     Appends the words of a clock time.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="hour">One or two ASCII digits, 0 to 23; a leading zero is read as <c>oh</c>.</param>
    /// <param name="minute">Exactly two ASCII digits, 00 to 59.</param>
    /// <param name="omitZeroMinute">Whether minute 00 is left out (a meridiem follows).</param>
    /// <exception cref="ArgumentException">The hour or minute is malformed or out of range.</exception>
    internal static void AppendClock(ref SpokenWriter writer, ReadOnlySpan<char> hour, ReadOnlySpan<char> minute, bool omitZeroMinute)
    {
        RequireDigits(hour);
        RequireDigits(minute);
        if (hour.Length > 2 || minute.Length != 2)
        {
            throw new ArgumentException("A time has a one- or two-digit hour and a two-digit minute.", nameof(hour));
        }

        var hourValue = ParseSmall(hour);
        var minuteValue = ParseSmall(minute);
        if (hourValue > 23 || minuteValue > 59)
        {
            throw new ArgumentException("The hour must be 0 to 23 and the minute 0 to 59.", nameof(hour));
        }

        // A written leading zero ("07:05") is read as "oh seven"; hour 00 as "zero".
        var leadingZero = hour.Length == 2 && hour[0] == '0';
        if (leadingZero && hourValue != 0)
        {
            writer.Append("oh");
            writer.Append(Ones[hourValue]);
        }
        else
        {
            AppendUnderThousand(ref writer, hourValue);
        }

        if (minuteValue == 0)
        {
            if (!omitZeroMinute)
            {
                writer.Append(!leadingZero && hourValue is >= 1 and <= 12 ? "o'clock" : "hundred");
            }
        }
        else if (minuteValue < 10)
        {
            writer.Append("oh");
            writer.Append(Ones[minuteValue]);
        }
        else
        {
            AppendUnderThousand(ref writer, minuteValue);
        }
    }

    /// <summary>
    ///     Appends the words of a calendar date: the month name, the day as an ordinal, and the year.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="month">The month, 1 to 12.</param>
    /// <param name="day">The day, 1 to 31.</param>
    /// <param name="yearDigits">Two or four ASCII digits; a four-digit year in 1000 to 2099 is read as a year, a two-digit year as a cardinal (<c>oh five</c> with a leading zero).</param>
    /// <exception cref="ArgumentOutOfRangeException">The month or day is out of range.</exception>
    /// <exception cref="ArgumentException">The year is not two or four ASCII digits.</exception>
    internal static void AppendDate(ref SpokenWriter writer, int month, int day, ReadOnlySpan<char> yearDigits)
    {
        writer.Append(MonthName(month));
        ArgumentOutOfRangeException.ThrowIfLessThan(day, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(day, 31);
        AppendUnderThousand(ref writer, day);
        writer.ReplaceLastWord(OrdinalWord(writer.LastWord()));

        RequireDigits(yearDigits);
        if (yearDigits.Length == 4)
        {
            AppendYear(ref writer, yearDigits);
        }
        else if (yearDigits.Length == 2)
        {
            if (yearDigits[0] == '0')
            {
                writer.Append("oh");
                writer.Append(Ones[yearDigits[1] - '0']);
            }
            else
            {
                AppendCardinal(ref writer, yearDigits);
            }
        }
        else
        {
            throw new ArgumentException("A year has two or four digits.", nameof(yearDigits));
        }
    }

    /// <summary>
    ///     Appends the cardinal words of a small number (0 to 999).
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value, 0 to 999.</param>
    private static void AppendUnderThousand(ref SpokenWriter writer, int value)
    {
        if (value == 0)
        {
            writer.Append("zero");
            return;
        }

        var hundreds = value / 100;
        if (hundreds > 0)
        {
            writer.Append(Ones[hundreds]);
            writer.Append("hundred");
        }

        var rest = value % 100;
        if (rest == 0)
        {
            return;
        }

        if (rest < 20)
        {
            writer.Append(Ones[rest]);
            return;
        }

        writer.Append(Tens[rest / 10]);
        if (rest % 10 != 0)
        {
            writer.Append(Ones[rest % 10]);
        }
    }

    /// <summary>
    ///     Converts the last word of a cardinal to its ordinal form.
    /// </summary>
    /// <param name="cardinalWord">The cardinal word (<c>one</c>, <c>twenty</c>, <c>hundred</c>, ...).</param>
    /// <returns>The ordinal word (<c>first</c>, <c>twentieth</c>, <c>hundredth</c>, ...).</returns>
    private static string OrdinalWord(string cardinalWord) => cardinalWord switch
    {
        "one" => "first",
        "two" => "second",
        "three" => "third",
        "five" => "fifth",
        "eight" => "eighth",
        "nine" => "ninth",
        "twelve" => "twelfth",
        _ => cardinalWord[^1] == 'y' ? string.Concat(cardinalWord.AsSpan(0, cardinalWord.Length - 1), "ieth") : cardinalWord + "th",
    };

    /// <summary>
    ///     Parses up to four ASCII digits that were already validated.
    /// </summary>
    /// <param name="digits">The digits.</param>
    /// <returns>The value.</returns>
    private static int ParseSmall(ReadOnlySpan<char> digits)
    {
        var value = 0;
        foreach (var digit in digits)
        {
            value = (value * 10) + (digit - '0');
        }

        return value;
    }

    /// <summary>
    ///     Checks that a span is non-empty and made of ASCII digits only (never <c>char.IsDigit</c>,
    ///     which accepts other scripts).
    /// </summary>
    /// <param name="digits">The span.</param>
    /// <exception cref="ArgumentException">The span is empty or holds another character.</exception>
    private static void RequireDigits(ReadOnlySpan<char> digits)
    {
        if (digits.Length == 0)
        {
            throw new ArgumentException("The digits must not be empty.", nameof(digits));
        }

        foreach (var character in digits)
        {
            if (character is < '0' or > '9')
            {
                throw new ArgumentException("Only the ASCII digits 0 to 9 are accepted.", nameof(digits));
            }
        }
    }
}
