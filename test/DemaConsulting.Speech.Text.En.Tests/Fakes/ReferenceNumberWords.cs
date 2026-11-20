namespace DemaConsulting.Speech.Text.En.Tests.Fakes;

/// <summary>
///     An independent reference for the English words of numbers, years, clock times and days,
///     used to check <c>NumberVerbalizer</c>. It is deliberately a different algorithm from the
///     production one: a recursive decomposition over <see cref="ulong"/> values that always
///     splits at the largest unit not above the value, and an explicit table for every ordinal
///     word, instead of the production group-by-group scan and suffix rule. It also owns the
///     closed vocabulary every numeric spoken value must stay inside.
/// </summary>
internal static class ReferenceNumberWords
{
    /// <summary>The words below twenty, by value.</summary>
    private static readonly Dictionary<ulong, string> Irregular = new()
    {
        [0] = "zero",
        [1] = "one",
        [2] = "two",
        [3] = "three",
        [4] = "four",
        [5] = "five",
        [6] = "six",
        [7] = "seven",
        [8] = "eight",
        [9] = "nine",
        [10] = "ten",
        [11] = "eleven",
        [12] = "twelve",
        [13] = "thirteen",
        [14] = "fourteen",
        [15] = "fifteen",
        [16] = "sixteen",
        [17] = "seventeen",
        [18] = "eighteen",
        [19] = "nineteen",
    };

    /// <summary>The multiples of ten from twenty, by value.</summary>
    private static readonly Dictionary<ulong, string> TensWords = new()
    {
        [20] = "twenty",
        [30] = "thirty",
        [40] = "forty",
        [50] = "fifty",
        [60] = "sixty",
        [70] = "seventy",
        [80] = "eighty",
        [90] = "ninety",
    };

    /// <summary>The units from the largest to the smallest, each with its word.</summary>
    private static readonly (ulong Value, string Word)[] Units =
    [
        (1_000_000_000_000UL, "trillion"), (1_000_000_000UL, "billion"), (1_000_000UL, "million"), (1_000UL, "thousand"), (100UL, "hundred"),
    ];

    /// <summary>The ordinal of every word that can end a cardinal, written out in full.</summary>
    private static readonly Dictionary<string, string> OrdinalOf = new()
    {
        ["zero"] = "zeroth",
        ["one"] = "first",
        ["two"] = "second",
        ["three"] = "third",
        ["four"] = "fourth",
        ["five"] = "fifth",
        ["six"] = "sixth",
        ["seven"] = "seventh",
        ["eight"] = "eighth",
        ["nine"] = "ninth",
        ["ten"] = "tenth",
        ["eleven"] = "eleventh",
        ["twelve"] = "twelfth",
        ["thirteen"] = "thirteenth",
        ["fourteen"] = "fourteenth",
        ["fifteen"] = "fifteenth",
        ["sixteen"] = "sixteenth",
        ["seventeen"] = "seventeenth",
        ["eighteen"] = "eighteenth",
        ["nineteen"] = "nineteenth",
        ["twenty"] = "twentieth",
        ["thirty"] = "thirtieth",
        ["forty"] = "fortieth",
        ["fifty"] = "fiftieth",
        ["sixty"] = "sixtieth",
        ["seventy"] = "seventieth",
        ["eighty"] = "eightieth",
        ["ninety"] = "ninetieth",
        ["hundred"] = "hundredth",
        ["thousand"] = "thousandth",
        ["million"] = "millionth",
        ["billion"] = "billionth",
        ["trillion"] = "trillionth",
    };

    /// <summary>The month names.</summary>
    private static readonly string[] MonthWords =
    [
        "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December",
    ];

    /// <summary>The words other than numbers that a numeric spoken value may contain.</summary>
    private static readonly string[] ExtraWords =
    [
        "oh", "point", "minus", "plus", "percent", "number", "and", "to", "o'clock", "P", "M", "A",
        "half", "halves", "quarter", "quarters",
        "tens", "twenties", "thirties", "forties", "fifties", "sixties", "seventies", "eighties", "nineties", "hundreds", "thousands",
        "dollar", "dollars", "cent", "cents", "euro", "euros", "pound", "pounds", "penny", "pence", "yen",
        "millimeter", "millimeters", "centimeter", "centimeters", "kilometer", "kilometers", "foot", "feet", "inch", "inches", "yard", "yards",
        "mile", "miles", "per", "hour", "hours", "milligram", "milligrams", "kilogram", "kilograms", "ounce", "ounces",
        "milliliter", "milliliters", "kilobyte", "kilobytes", "megabyte", "megabytes", "gigabyte", "gigabytes",
        "terabyte", "terabytes", "hertz", "kilohertz", "megahertz", "gigahertz", "millisecond", "milliseconds",
        "minute", "minutes", "degree", "degrees", "Celsius", "Fahrenheit",
        "approximately", "by", "meter", "meters", "gram", "grams", "second", "seconds", "liter", "liters", "volt", "volts",
        "joule", "joules", "nanometer", "nanometers", "micrometer", "micrometers", "revolution", "revolutions",
        "microgram", "micrograms", "centiliter", "centiliters", "deciliter", "deciliters", "pascal", "pascals",
        "hectopascal", "hectopascals", "kilopascal", "kilopascals", "megapascal", "megapascals", "gigapascal",
        "gigapascals", "square", "milliwatt", "milliwatts", "kilowatt", "kilowatts", "megawatt", "megawatts", "gigawatt",
        "gigawatts", "watt", "millivolt", "millivolts", "kilovolt", "kilovolts", "milliampere", "milliamperes",
        "kilojoule", "kilojoules", "megajoule", "megajoules", "kilonewton", "kilonewtons", "kilocalorie", "kilocalories",
        "decibel", "decibels", "microsecond", "microseconds", "nanosecond", "nanoseconds", "petabyte", "petabytes",
        "megabit", "megabits", "gigabit", "gigabits",
    ];

    /// <summary>
    ///     Gets the closed vocabulary of numeric spoken values: every cardinal and ordinal word,
    ///     the plural ordinals used for fractions, the month names and the fixed words.
    /// </summary>
    internal static IReadOnlySet<string> Vocabulary { get; } = BuildVocabulary();

    /// <summary>
    ///     Gets the cardinal words of a value.
    /// </summary>
    /// <param name="value">The value, below 10^15.</param>
    /// <returns>For example <c>one thousand two hundred thirty four</c>.</returns>
    internal static string Cardinal(ulong value)
    {
        if (value < 20)
        {
            return Irregular[value];
        }

        if (value < 100)
        {
            var tens = value - (value % 10);
            return value % 10 == 0 ? TensWords[tens] : TensWords[tens] + " " + Irregular[value % 10];
        }

        // Split at the largest unit that does not exceed the value.
        foreach (var (unit, word) in Units)
        {
            if (value >= unit)
            {
                var head = Cardinal(value / unit) + " " + word;
                return value % unit == 0 ? head : head + " " + Cardinal(value % unit);
            }
        }

        throw new InvalidOperationException("Unreachable: every value of 100 or more has a unit.");
    }

    /// <summary>
    ///     Gets the ordinal words of a value.
    /// </summary>
    /// <param name="value">The value, below 10^15.</param>
    /// <returns>For example <c>twenty first</c>.</returns>
    internal static string Ordinal(ulong value)
    {
        var words = Cardinal(value);
        var cut = words.LastIndexOf(' ') + 1;
        return words[..cut] + OrdinalOf[words[cut..]];
    }

    /// <summary>
    ///     Gets the year-style words of a four-digit value from 1000 to 2099.
    /// </summary>
    /// <param name="value">The year.</param>
    /// <returns>For example <c>nineteen oh five</c>; the cardinal for 1000, 2000 and 2001 to 2009.</returns>
    internal static string Year(ulong value)
    {
        if (value % 1000 == 0 || (value >= 2001 && value <= 2009))
        {
            return Cardinal(value);
        }

        var head = Cardinal(value / 100);
        var tail = value % 100;
        if (tail == 0)
        {
            return head + " hundred";
        }

        return tail < 10 ? head + " oh " + Cardinal(tail) : head + " " + Cardinal(tail);
    }

    /// <summary>
    ///     Gets the words of a clock time.
    /// </summary>
    /// <param name="hour">The hour, 0 to 23.</param>
    /// <param name="minute">The minute, 0 to 59.</param>
    /// <param name="hourLeadingZero">Whether the hour was written with a leading zero.</param>
    /// <param name="meridiem">Whether a meridiem follows (minute zero is then left out).</param>
    /// <returns>For example <c>five oh five</c> or <c>seventeen hundred</c>.</returns>
    internal static string Clock(ulong hour, ulong minute, bool hourLeadingZero, bool meridiem)
    {
        var words = hourLeadingZero && hour != 0 ? "oh " + Cardinal(hour) : Cardinal(hour);
        if (minute == 0)
        {
            if (meridiem)
            {
                return words;
            }

            return words + (!hourLeadingZero && hour >= 1 && hour <= 12 ? " o'clock" : " hundred");
        }

        return minute < 10 ? words + " oh " + Cardinal(minute) : words + " " + Cardinal(minute);
    }

    /// <summary>
    ///     Gets the English name of a month.
    /// </summary>
    /// <param name="month">The month, 1 to 12.</param>
    /// <returns>The capitalized name.</returns>
    internal static string Month(int month) => MonthWords[month - 1];

    /// <summary>
    ///     Tells whether every word of a spoken value belongs to the closed vocabulary.
    /// </summary>
    /// <param name="spoken">The spoken value.</param>
    /// <returns><see langword="true"/> when no word is outside the vocabulary.</returns>
    internal static bool IsInVocabulary(string spoken) =>
        spoken.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(word => Vocabulary.Contains(word));

    /// <summary>
    ///     Builds the vocabulary.
    /// </summary>
    /// <returns>The set of allowed words.</returns>
    private static IReadOnlySet<string> BuildVocabulary()
    {
        var words = new HashSet<string>(StringComparer.Ordinal);
        words.UnionWith(Irregular.Values);
        words.UnionWith(TensWords.Values);
        words.UnionWith(Units.Select(unit => unit.Word));
        words.UnionWith(OrdinalOf.Values);

        // Plural ordinals name the parts of a fraction ("thirds", "sixteenths", "hundredths").
        words.UnionWith(OrdinalOf.Values.Select(ordinal => ordinal + "s"));
        words.UnionWith(MonthWords);
        words.UnionWith(ExtraWords);
        return words;
    }
}
