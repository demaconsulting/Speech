using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;

/// <summary>
///     Rewrites arbitrary user text into plain, speakable English words before phonemization:
///     strips Markdown and code decoration, expands numbers, currency, times, ordinals and common
///     abbreviations into words, reads URLs and e-mail addresses aloud, and removes diacritics.
/// </summary>
/// <remarks>
///     The lexicon only knows alphabetic words, so everything else in a sentence (digits, symbols,
///     markup) would otherwise be silently dropped - turning "The year is 2024" into "The year
///     is". Spelled-out single letters (for example the "P M" of "5 p.m.") are emitted with the
///     <see cref="LetterMarker"/> prefix so the phonemizer reads them by letter name rather than
///     looking them up as words.
/// </remarks>
internal static class KokoroTextNormalizer
{
    /// <summary>A private-use character that prefixes a letter to be read by its letter name.</summary>
    internal const char LetterMarker = '\uE001';

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private static readonly string[] Ones =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen",
    ];

    private static readonly string[] Tens =
        ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    private static readonly string[] Scales = ["", "thousand", "million", "billion", "trillion"];

    private static readonly Dictionary<string, string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dr"] = "Doctor",
        ["Mr"] = "Mister",
        ["Mrs"] = "Missus",
        ["Ms"] = "Miss",
        ["Prof"] = "Professor",
        ["Jr"] = "Junior",
        ["Sr"] = "Senior",
        ["vs"] = "versus",
        ["etc"] = "et cetera",
        ["Mt"] = "Mount",
        ["Ave"] = "Avenue",
        ["Rd"] = "Road",
        ["Blvd"] = "Boulevard",
    };

    private static readonly Regex EmailPattern = new(
        @"[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)+", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex UrlPattern = new(
        @"(?:https?://|www\.)[^\s]+", RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout);

    private static readonly Regex LinkPattern = new(
        @"!?\[([^\]]*)\]\([^)]*\)", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex CurrencyScalePattern = new(
        @"([$£€])(\d+(?:\.\d+)?)\s*(thousand|million|billion|trillion)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout);

    private static readonly Regex CurrencyPattern = new(
        @"([$£€])(\d{1,3}(?:,\d{3})+|\d+)(?:\.(\d{2}))?(?!\d)", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex TimeMeridiemPattern = new(
        @"(?<!\d)(\d{1,2}):(\d{2})(?::\d{2})?\s*([AaPp])\.?[Mm]\b\.?",
        RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex TimePattern = new(
        @"(?<![\d:])(\d{1,2}):(\d{2})(?![\d:])", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex HourMeridiemPattern = new(
        @"(?<!\d)(\d{1,2})\s*([AaPp])\.?[Mm]\b\.?", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex PercentPattern = new(
        @"(\d)\s*%", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex OrdinalPattern = new(
        @"(?<!\d)(\d+)(?:st|nd|rd|th)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout);

    private static readonly Regex DigitGroupsPattern = new(
        @"(?<!\d)\d+(?:[-\u2013]\d+)+(?!\d)", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex ThousandsPattern = new(
        @"(?<!\d)\d{1,3}(?:,\d{3})+(?!\d)", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex DecimalPattern = new(
        @"(?<!\d)(\d+)\.(\d+)(?!\d)", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex YearPattern = new(
        @"(?<![\d,.])(1[1-9]\d{2}|20\d{2})(?![\d,]|\.\d)", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex IntegerPattern = new(
        @"(?<!\d)\d+(?!\d)", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex NegativePattern = new(
        @"(?<![\w)])-(?=\d)", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex AbbreviationPattern = new(
        @"\b(Dr|Mr|Mrs|Ms|Prof|Jr|Sr|vs|etc|Mt|Ave|Rd|Blvd)\.", RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout);

    private static readonly Regex SaintStreetPattern = new(
        @"(?<before>\b[A-Za-z]+\s+)?\bSt\.(?<after>\s+[A-Z])?", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex DottedAbbreviationPattern = new(
        @"\b(e\.g\.|i\.e\.)", RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout);

    private static readonly Regex ListMarkerPattern = new(
        @"^\s*(?:[-*+]|\d+[.)])\s+", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex HeadingPattern = new(
        @"^\s*#{1,6}\s+", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex QuotePattern = new(
        @"^\s*>+\s?", RegexOptions.Compiled, RegexTimeout);

    /// <summary>Normalizes <paramref name="text"/> into plain speakable words and punctuation.</summary>
    /// <param name="text">The raw text. Must not be null.</param>
    /// <returns>The normalized text.</returns>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        text = StripDiacritics(text);
        text = text.Replace('\u2018', '\'').Replace('\u2019', '\'');
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        text = text.Replace("<3", " ", StringComparison.Ordinal);
        text = StripMarkdown(text);
        text = ReadAddresses(text);
        text = ExpandAbbreviations(text);
        text = ExpandNumbers(text);

        return text
            .Replace("&", " and ", StringComparison.Ordinal)
            .Replace("+", " plus ", StringComparison.Ordinal)
            .Replace("=", " equals ", StringComparison.Ordinal);
    }

    /// <summary>Converts a non-negative integer to English words (for example 2024 to "two thousand twenty four").</summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The words, space separated.</returns>
    internal static string IntegerToWords(long value)
    {
        if (value < 0)
        {
            return "minus " + IntegerToWords(-value);
        }

        if (value < 20)
        {
            return Ones[value];
        }

        if (value < 100)
        {
            return value % 10 == 0 ? Tens[value / 10] : $"{Tens[value / 10]} {Ones[value % 10]}";
        }

        if (value < 1000)
        {
            var rest = value % 100;
            return rest == 0 ? $"{Ones[value / 100]} hundred" : $"{Ones[value / 100]} hundred {IntegerToWords(rest)}";
        }

        var parts = new List<string>();
        var scale = 0;
        while (value > 0)
        {
            var group = value % 1000;
            if (group > 0)
            {
                var words = IntegerToWords(group);
                parts.Insert(0, Scales[scale].Length == 0 ? words : $"{words} {Scales[scale]}");
            }

            value /= 1000;
            scale++;
        }

        return string.Join(' ', parts);
    }

    private static string StripDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string StripMarkdown(string text)
    {
        text = LinkPattern.Replace(text, "$1");

        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var structural = false;

            foreach (var pattern in new[] { HeadingPattern, ListMarkerPattern, QuotePattern })
            {
                var stripped = pattern.Replace(line, string.Empty, 1);
                if (stripped.Length != line.Length)
                {
                    structural = true;
                    line = stripped;
                }
            }

            line = line.Replace("`", string.Empty, StringComparison.Ordinal)
                .Replace("*", string.Empty, StringComparison.Ordinal)
                .Replace("~~", string.Empty, StringComparison.Ordinal)
                .Replace('_', ' ');

            // A heading or list item is its own spoken unit; end it with a full stop so it does
            // not run into the next line.
            var trimmed = line.TrimEnd();
            if (structural && trimmed.Length > 0 && ".!?\u2026:;,".IndexOf(trimmed[^1]) < 0)
            {
                line = trimmed + ".";
            }

            lines[i] = line;
        }

        return string.Join('\n', lines);
    }

    private static string ReadAddresses(string text)
    {
        text = EmailPattern.Replace(text, match => " " + match.Value
            .Replace("@", " at ", StringComparison.Ordinal)
            .Replace(".", " dot ", StringComparison.Ordinal)
            .Replace("_", " underscore ", StringComparison.Ordinal)
            .Replace("-", " dash ", StringComparison.Ordinal) + " ");

        return UrlPattern.Replace(text, match =>
        {
            var url = match.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')');
            var trailing = match.Value[url.Length..];
            var www = url.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
            url = Regex.Replace(url, @"^https?://", string.Empty, RegexOptions.IgnoreCase, RegexTimeout);
            if (www)
            {
                url = url[4..];
            }

            var spoken = url
                .Replace(".", " dot ", StringComparison.Ordinal)
                .Replace("/", " slash ", StringComparison.Ordinal)
                .Replace("_", " underscore ", StringComparison.Ordinal)
                .Replace("-", " dash ", StringComparison.Ordinal)
                .Replace("?", " question mark ", StringComparison.Ordinal)
                .Replace("=", " equals ", StringComparison.Ordinal)
                .Replace("&", " and ", StringComparison.Ordinal);
            return (www ? " " + SpellLetters("www") + " dot " : " ") + spoken + " " + trailing;
        });
    }

    private static string ExpandAbbreviations(string text)
    {
        text = DottedAbbreviationPattern.Replace(text, match =>
            string.Equals(match.Value, "e.g.", StringComparison.OrdinalIgnoreCase) ? "for example," : "that is,");

        text = AbbreviationPattern.Replace(text, match => Abbreviations[match.Groups[1].Value] + " ");

        return SaintStreetPattern.Replace(text, match =>
        {
            var before = match.Groups["before"];
            var after = match.Groups["after"];

            // "St. Louis" after a lowercase word (or at the start) is Saint; "Main St." after a
            // capitalized word is Street.
            var precededByCapital = before.Success && char.IsUpper(before.Value.TrimStart()[0]);
            var word = after.Success && !precededByCapital ? "Saint" : "Street";
            return before.Value + word + after.Value;
        });
    }

    private static string ExpandNumbers(string text)
    {
        // Must run first: later passes consume the digits that identify a minus sign.
        text = NegativePattern.Replace(text, "minus ");
        text = CurrencyScalePattern.Replace(text, match =>
            $" {DecimalToWords(match.Groups[2].Value)} {match.Groups[3].Value.ToLowerInvariant()} {CurrencyUnit(match.Groups[1].Value, plural: true)} ");

        text = CurrencyPattern.Replace(text, ExpandCurrency);
        text = TimeMeridiemPattern.Replace(text, match =>
            $" {ClockToWords(match.Groups[1].Value, match.Groups[2].Value, includeOClock: false)} {SpellLetters(match.Groups[3].Value + "m")} ");
        text = HourMeridiemPattern.Replace(text, match =>
            $" {IntegerToWords(long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))} {SpellLetters(match.Groups[2].Value + "m")} ");
        text = TimePattern.Replace(text, match =>
            $" {ClockToWords(match.Groups[1].Value, match.Groups[2].Value, includeOClock: true)} ");
        text = PercentPattern.Replace(text, "$1 percent ");
        text = OrdinalPattern.Replace(text, match => $" {OrdinalToWords(match.Groups[1].Value)} ");
        text = DigitGroupsPattern.Replace(text, match =>
            match.Value.Count(char.IsAsciiDigit) >= 6 ? " " + SpellDigitGroups(match.Value) + " " : match.Value);
        text = ThousandsPattern.Replace(text, match => $" {DigitsToWords(match.Value.Replace(",", string.Empty, StringComparison.Ordinal))} ");
        text = DecimalPattern.Replace(text, match => $" {DecimalToWords(match.Value)} ");
        text = YearPattern.Replace(text, match => $" {YearToWords(int.Parse(match.Value, CultureInfo.InvariantCulture))} ");
        return IntegerPattern.Replace(text, match => $" {DigitsToWords(match.Value)} ");
    }

    private static string ExpandCurrency(Match match)
    {
        var symbol = match.Groups[1].Value;
        var whole = match.Groups[2].Value.Replace(",", string.Empty, StringComparison.Ordinal);
        var result = $" {DigitsToWords(whole)} {CurrencyUnit(symbol, whole.TrimStart('0') != "1")}";

        if (match.Groups[3].Success)
        {
            var cents = long.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            if (cents > 0)
            {
                result += $" and {IntegerToWords(cents)} {CurrencySubunit(symbol, cents != 1)}";
            }
        }

        return result + " ";
    }

    private static string CurrencyUnit(string symbol, bool plural) => symbol switch
    {
        "$" => plural ? "dollars" : "dollar",
        "\u00A3" => plural ? "pounds" : "pound",
        _ => plural ? "euros" : "euro",
    };

    private static string CurrencySubunit(string symbol, bool plural) => symbol switch
    {
        "\u00A3" => plural ? "pence" : "penny",
        _ => plural ? "cents" : "cent",
    };

    private static string ClockToWords(string hour, string minute, bool includeOClock)
    {
        var hours = IntegerToWords(long.Parse(hour, CultureInfo.InvariantCulture));
        var minutes = int.Parse(minute, CultureInfo.InvariantCulture);
        if (minutes == 0)
        {
            return includeOClock ? hours + " o'clock" : hours;
        }

        return minutes < 10 ? $"{hours} oh {Ones[minutes]}" : $"{hours} {IntegerToWords(minutes)}";
    }

    private static string DigitsToWords(string digits) =>
        digits.Length > 15 ? SpellDigits(digits) : IntegerToWords(long.Parse(digits, CultureInfo.InvariantCulture));

    private static string DecimalToWords(string number)
    {
        var dot = number.IndexOf('.', StringComparison.Ordinal);
        return dot < 0
            ? DigitsToWords(number)
            : $"{DigitsToWords(number[..dot])} point {SpellDigits(number[(dot + 1)..])}";
    }

    private static string SpellDigits(string digits) =>
        string.Join(' ', digits.Where(char.IsAsciiDigit).Select(d => Ones[d - '0']));

    private static string SpellDigitGroups(string value) =>
        string.Join(", ", value.Replace('\u2013', '-').Split('-').Select(SpellDigits));

    private static string YearToWords(int year)
    {
        if (year is >= 2000 and <= 2009)
        {
            return year == 2000 ? "two thousand" : $"two thousand {Ones[year - 2000]}";
        }

        var high = year / 100;
        var low = year % 100;
        return low switch
        {
            0 => $"{IntegerToWords(high)} hundred",
            < 10 => $"{IntegerToWords(high)} oh {Ones[low]}",
            _ => $"{IntegerToWords(high)} {IntegerToWords(low)}",
        };
    }

    private static string OrdinalToWords(string digits)
    {
        var words = DigitsToWords(digits).Split(' ');
        var last = words[^1];
        words[^1] = last switch
        {
            "one" => "first",
            "two" => "second",
            "three" => "third",
            "five" => "fifth",
            "eight" => "eighth",
            "nine" => "ninth",
            "twelve" => "twelfth",
            _ when last.EndsWith('y') => last[..^1] + "ieth",
            _ => last + "th",
        };

        return string.Join(' ', words);
    }

    /// <summary>Returns <paramref name="letters"/> as space-separated <see cref="LetterMarker"/>-prefixed letters.</summary>
    internal static string SpellLetters(string letters) =>
        string.Join(' ', letters.Where(char.IsAsciiLetter).Select(c => LetterMarker.ToString() + char.ToLowerInvariant(c)));
}
