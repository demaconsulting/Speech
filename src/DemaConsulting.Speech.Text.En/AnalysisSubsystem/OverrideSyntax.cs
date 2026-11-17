using System.Buffers;
using System.Globalization;
using System.Text;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;

namespace DemaConsulting.Speech.Text.En.AnalysisSubsystem;

/// <summary>
///     The grammars of the pronunciation-override vocabulary: dictionary keys, <c>say</c> payloads
///     and the <c>scheme:phonemes</c> value of an <c>en.pron</c> annotation. The dictionary
///     builders and the typed annotation readers both use these functions, so what is accepted
///     when an entry is added and what is accepted when an annotation is read cannot drift apart.
/// </summary>
/// <remarks>
///     Every validator returns a fixed-text reason (or <see langword="null"/> for valid input);
///     a reason never contains the offending text, so it can be placed in an exception message
///     without leaking dictionary content. The type is static and stateless.
/// </remarks>
internal static class OverrideSyntax
{
    /// <summary>The wire name of <see cref="PronunciationScheme.Ipa"/>.</summary>
    internal const string IpaName = "ipa";

    /// <summary>The wire name of <see cref="PronunciationScheme.Arpabet"/>.</summary>
    internal const string ArpabetName = "arpabet";

    /// <summary>The most UTF-16 code units of the phonemes of an <c>ipa</c> payload.</summary>
    internal const int MaxIpaLength = 128;

    /// <summary>The most phones of an <c>arpabet</c> payload.</summary>
    internal const int MaxArpabetPhones = 32;

    /// <summary>The most letters of an ARPAbet phone, without the stress digit.</summary>
    private const int MaxPhoneLetters = 3;

    /// <summary>The scheme separator of a pronunciation value.</summary>
    private const char SchemeSeparator = ':';

    /// <summary>The typographic apostrophe a key may use instead of the plain one.</summary>
    internal const char TypographicApostrophe = '\u2019';

    /// <summary>
    ///     Gets the wire name of a scheme.
    /// </summary>
    /// <param name="scheme">The scheme.</param>
    /// <returns>The lower-case name, or <see langword="null"/> for an undefined value.</returns>
    internal static string? SchemeName(PronunciationScheme scheme) => scheme switch
    {
        PronunciationScheme.Ipa => IpaName,
        PronunciationScheme.Arpabet => ArpabetName,
        _ => null,
    };

    /// <summary>
    ///     Maps the typographic apostrophe of a key to the plain one, so that the two spellings
    ///     are one key when duplicates are detected.
    /// </summary>
    /// <param name="key">A validated key.</param>
    /// <returns>The key with every U+2019 replaced by U+0027.</returns>
    internal static string CanonicalKey(string key) => key.Replace(TypographicApostrophe, '\'');

    /// <summary>
    ///     Validates the word of an entry.
    /// </summary>
    /// <param name="word">The word; may be null.</param>
    /// <returns>
    ///     <see langword="null"/> when valid; otherwise the reason. A valid word is 1 to
    ///     <see cref="PronunciationOverrides.MaxWordLength"/> UTF-16 code units of well-formed text;
    ///     starts with a letter and ends with a letter or a decimal digit; consists of letters,
    ///     combining marks, decimal digits and the characters <c>' ’ - . &amp; / _</c>; and uses
    ///     only one of the two apostrophe styles.
    /// </returns>
    internal static string? ValidateKey(string? word)
    {
        if (word is null || word.Length == 0)
        {
            return "the word is missing or empty";
        }

        if (word.Length > PronunciationOverrides.MaxWordLength)
        {
            return "the word is too long";
        }

        var plainApostrophe = false;
        var typographicApostrophe = false;
        var lastIsLetterOrDigit = false;
        var position = 0;
        while (position < word.Length)
        {
            if (Rune.DecodeFromUtf16(word.AsSpan(position), out var rune, out var consumed) != OperationStatus.Done)
            {
                return "the word is not well-formed UTF-16 text";
            }

            var isLetter = Rune.IsLetter(rune);
            if (position == 0 && !isLetter)
            {
                return "the word must start with a letter";
            }

            var isDigit = IsDecimalDigit(rune);
            if (!(isLetter || isDigit || IsMark(rune) || IsKeyPunctuation(rune)))
            {
                return "the word contains a character that is not allowed";
            }

            plainApostrophe |= rune.Value == '\'';
            typographicApostrophe |= rune.Value == TypographicApostrophe;
            lastIsLetterOrDigit = isLetter || isDigit;
            position += consumed;
        }

        if (!lastIsLetterOrDigit)
        {
            return "the word must end with a letter or a digit";
        }

        return plainApostrophe && typographicApostrophe ? "the word mixes the two apostrophe styles" : null;
    }

    /// <summary>
    ///     Validates the word of a spell-out entry.
    /// </summary>
    /// <param name="word">A word that already passed <see cref="ValidateKey"/>.</param>
    /// <returns><see langword="null"/> for 1 to <see cref="PronunciationOverrides.MaxSpellOutLetters"/> ASCII letters; otherwise the reason.</returns>
    internal static string? ValidateSpellKey(string word)
    {
        if (word.Length > PronunciationOverrides.MaxSpellOutLetters)
        {
            return "the word has too many letters to spell out";
        }

        return word.Any(static character => character is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z'))
            ? "a word to spell out must consist of ASCII letters only"
            : null;
    }

    /// <summary>
    ///     Validates the payload of a <c>say</c> entry: plain words.
    /// </summary>
    /// <param name="payload">The payload; may be null.</param>
    /// <returns>
    ///     <see langword="null"/> when valid; otherwise the reason. A valid payload is 1 to
    ///     <see cref="PronunciationOverrides.MaxPayloadLength"/> UTF-16 code units of letters,
    ///     combining marks, apostrophes (<c>'</c>), hyphens and single ASCII spaces between
    ///     words (never leading, trailing or doubled), with at least one letter; no digits and no
    ///     punctuation (a number is written in words).
    /// </returns>
    internal static string? ValidateSayPayload(string? payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return "a spoken text is required for this action";
        }

        if (payload.Length > PronunciationOverrides.MaxPayloadLength)
        {
            return "the spoken text is too long";
        }

        var hasLetter = false;
        var previousWasSpace = true;
        var position = 0;
        while (position < payload.Length)
        {
            if (Rune.DecodeFromUtf16(payload.AsSpan(position), out var rune, out var consumed) != OperationStatus.Done)
            {
                return "the spoken text is not well-formed UTF-16 text";
            }

            if (rune.Value == ' ')
            {
                if (previousWasSpace)
                {
                    return "the spoken text has a leading or doubled space";
                }

                previousWasSpace = true;
            }
            else if (Rune.IsLetter(rune))
            {
                hasLetter = true;
                previousWasSpace = false;
            }
            else if (IsMark(rune) || rune.Value is '\'' or '-')
            {
                previousWasSpace = false;
            }
            else
            {
                return "the spoken text may contain only letters, apostrophes, hyphens and single spaces";
            }

            position += consumed;
        }

        if (previousWasSpace)
        {
            return "the spoken text has a trailing space";
        }

        return hasLetter ? null : "the spoken text must contain a letter";
    }

    /// <summary>
    ///     Validates a reading entry: the payload is a well-formed id that exists and is active in the
    ///     inventory, and the entry's word is one of the forms that can carry it.
    /// </summary>
    /// <param name="word">A word that already passed <see cref="ValidateKey"/>.</param>
    /// <param name="payload">The reading id; may be null.</param>
    /// <returns>
    ///     <see langword="null"/> when valid; otherwise the fixed-text reason, which never contains the word
    ///     or the id. The inventory is loaded only when the id is well-formed, so a dictionary without
    ///     reading entries never touches it.
    /// </returns>
    internal static string? ValidateReading(string word, string? payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return "a reading id is required for this action";
        }

        if (!ReadingIdSyntax.IsValid(payload))
        {
            return "the reading id is not well-formed";
        }

        var inventory = ReadingInventory.Default;
        if (!inventory.TryGetReadingIndex(payload, out var readingIndex))
        {
            return "the reading id is not in the reading inventory";
        }

        if (!inventory.IsActive(readingIndex))
        {
            return "the reading id is not active in the reading inventory";
        }

        // The word must be a form of the inventory whose candidates include this reading.
        return inventory.TryFindForm(word, out var formIndex) && inventory.GetCandidates(formIndex).Contains(readingIndex)
            ? null
            : "the word is not a form of this reading";
    }

    /// <summary>
    ///     Validates and splits a <c>scheme:phonemes</c> value.
    /// </summary>
    /// <param name="value">The value; may be null.</param>
    /// <param name="scheme">The scheme when the method returns <see langword="null"/>; otherwise undefined.</param>
    /// <param name="phonemes">The phonemes after the first colon when the method returns <see langword="null"/>; otherwise empty.</param>
    /// <returns>
    ///     <see langword="null"/> when valid; otherwise the reason. The scheme (lower-case, from the
    ///     closed list) is everything before the first colon; <c>ipa</c> phonemes are 1 to
    ///     <see cref="MaxIpaLength"/> UTF-16 code units of non-whitespace, non-control text;
    ///     <c>arpabet</c> phonemes are 1 to <see cref="MaxArpabetPhones"/> phones separated by
    ///     single spaces, each one to three capital letters and an optional stress digit 0 to 2;
    ///     the whole value is at most <see cref="PronunciationOverrides.MaxPayloadLength"/> UTF-16
    ///     code units.
    /// </returns>
    internal static string? ParsePronunciation(string? value, out PronunciationScheme scheme, out string phonemes)
    {
        scheme = default;
        phonemes = string.Empty;
        if (string.IsNullOrEmpty(value))
        {
            return "a pronunciation of the form scheme:phonemes is required for this action";
        }

        if (value.Length > PronunciationOverrides.MaxPayloadLength)
        {
            return "the pronunciation is too long";
        }

        var separator = value.IndexOf(SchemeSeparator);
        if (separator < 0)
        {
            return "the pronunciation must have the form scheme:phonemes";
        }

        var name = value.AsSpan(0, separator);
        PronunciationScheme parsed;
        if (name.SequenceEqual(IpaName))
        {
            parsed = PronunciationScheme.Ipa;
        }
        else if (name.SequenceEqual(ArpabetName))
        {
            parsed = PronunciationScheme.Arpabet;
        }
        else
        {
            return "the pronunciation scheme is not supported";
        }

        var body = value.AsSpan(separator + 1);
        var reason = parsed == PronunciationScheme.Ipa ? ValidateIpa(body) : ValidateArpabet(body);
        if (reason is not null)
        {
            return reason;
        }

        scheme = parsed;
        phonemes = body.ToString();
        return null;
    }

    /// <summary>
    ///     Validates the phonemes of an <c>ipa</c> payload.
    /// </summary>
    /// <param name="phonemes">The text after <c>ipa:</c>.</param>
    /// <returns><see langword="null"/> when valid; otherwise the reason.</returns>
    private static string? ValidateIpa(ReadOnlySpan<char> phonemes)
    {
        if (phonemes.IsEmpty || phonemes.Length > MaxIpaLength)
        {
            return "the ipa phonemes are empty or too long";
        }

        var position = 0;
        while (position < phonemes.Length)
        {
            if (Rune.DecodeFromUtf16(phonemes[position..], out var rune, out var consumed) != OperationStatus.Done)
            {
                return "the ipa phonemes are not well-formed UTF-16 text";
            }

            if (rune.Value is 0xFFFC or 0xFFFD || Rune.IsWhiteSpace(rune) || IsExcludedCategory(Rune.GetUnicodeCategory(rune)))
            {
                return "the ipa phonemes contain a character that is not allowed";
            }

            position += consumed;
        }

        return null;
    }

    /// <summary>
    ///     Validates the phones of an <c>arpabet</c> payload.
    /// </summary>
    /// <param name="phonemes">The text after <c>arpabet:</c>.</param>
    /// <returns><see langword="null"/> when valid; otherwise the reason.</returns>
    private static string? ValidateArpabet(ReadOnlySpan<char> phonemes)
    {
        var phones = 0;
        var position = 0;
        while (position <= phonemes.Length)
        {
            // One phone: up to the next space or the end.
            var length = 0;
            while (position + length < phonemes.Length && phonemes[position + length] != ' ')
            {
                length++;
            }

            if (!IsArpabetPhone(phonemes.Slice(position, length)))
            {
                return "the arpabet phonemes must be capital-letter phones with an optional stress digit, separated by single spaces";
            }

            phones++;
            if (phones > MaxArpabetPhones)
            {
                return "the arpabet phonemes have too many phones";
            }

            position += length + 1;
        }

        return null;
    }

    /// <summary>
    ///     Tells whether text is one ARPAbet phone: one to three ASCII capitals and an optional digit 0 to 2.
    /// </summary>
    /// <param name="phone">The candidate.</param>
    /// <returns><see langword="true"/> for a phone.</returns>
    private static bool IsArpabetPhone(ReadOnlySpan<char> phone)
    {
        var letters = phone.Length > 0 && phone[^1] is >= '0' and <= '2' ? phone.Length - 1 : phone.Length;
        if (letters is < 1 or > MaxPhoneLetters)
        {
            return false;
        }

        for (var i = 0; i < letters; i++)
        {
            if (phone[i] is < 'A' or > 'Z')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Tells whether a character is one of the punctuation characters a key may contain.
    /// </summary>
    /// <param name="rune">The character.</param>
    /// <returns><see langword="true"/> for <c>' ’ - . &amp; / _</c>.</returns>
    private static bool IsKeyPunctuation(Rune rune) =>
        rune.Value is '\'' or TypographicApostrophe or '-' or '.' or '&' or '/' or '_';

    /// <summary>
    ///     Tells whether a character is a decimal digit (category Nd, any script).
    /// </summary>
    /// <param name="rune">The character.</param>
    /// <returns><see langword="true"/> for a decimal digit.</returns>
    internal static bool IsDecimalDigit(Rune rune) =>
        Rune.GetUnicodeCategory(rune) == UnicodeCategory.DecimalDigitNumber;

    /// <summary>
    ///     Tells whether a character is a combining mark.
    /// </summary>
    /// <param name="rune">The character.</param>
    /// <returns><see langword="true"/> for the categories Mn, Mc and Me.</returns>
    internal static bool IsMark(Rune rune) =>
        Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;

    /// <summary>
    ///     Tells whether a category is excluded from ipa phonemes.
    /// </summary>
    /// <param name="category">The category.</param>
    /// <returns><see langword="true"/> for control, format, surrogate, private-use, unassigned and separator characters.</returns>
    private static bool IsExcludedCategory(UnicodeCategory category) =>
        category is UnicodeCategory.Control
            or UnicodeCategory.Format
            or UnicodeCategory.Surrogate
            or UnicodeCategory.PrivateUse
            or UnicodeCategory.OtherNotAssigned
            or UnicodeCategory.SpaceSeparator
            or UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator;
}
