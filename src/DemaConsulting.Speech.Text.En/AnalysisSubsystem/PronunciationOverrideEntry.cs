using DemaConsulting.Speech.Text.En.HeteronymSubsystem;

namespace DemaConsulting.Speech.Text.En.AnalysisSubsystem;

/// <summary>
///     What a <see cref="PronunciationOverrideEntry"/> does to the word it matches.
/// </summary>
/// <remarks>
///     The numeric values are part of the contract and never change.
/// </remarks>
public enum OverrideAction
{
    /// <summary>
    ///     Speak the word as written: every built-in abbreviation, acronym, symbol and number rule
    ///     is bypassed for it and no annotation is produced.
    /// </summary>
    Skip = 1,

    /// <summary>
    ///     Speak the entry's payload (plain words) instead of the word: one
    ///     <see cref="TextEnAnnotations.Normalized"/> annotation.
    /// </summary>
    Say = 2,

    /// <summary>
    ///     Speak the word's letters one by one by letter name: one
    ///     <see cref="TextEnAnnotations.SpellOut"/> annotation.
    /// </summary>
    SpellOut = 3,

    /// <summary>
    ///     Hand the entry's payload (<c>scheme:phonemes</c>) to the backend for the word: one
    ///     <see cref="TextEnAnnotations.Pronunciation"/> annotation; the word itself is not replaced.
    /// </summary>
    Pronunciation = 4,

    /// <summary>
    ///     Fix the heteronym reading of the word to the entry's payload (a reading id such as
    ///     <c>record.noun</c>): one <see cref="TextEnAnnotations.Reading"/> annotation with confidence
    ///     <c>1.000</c>, and the word is spoken as written otherwise. It beats the classifier and needs no
    ///     model. The id must exist in the <see cref="ReadingInventory"/> and be a
    ///     candidate for the entry's word.
    /// </summary>
    Reading = 5,
}

/// <summary>
///     The phoneme notation of an <see cref="TextEnAnnotations.Pronunciation"/> annotation. The set
///     is closed: a new notation is added by a new release, never by the host.
/// </summary>
/// <remarks>
///     This library checks only that the phonemes are written in the notation's syntax; whether a
///     symbol belongs to a particular phoneme inventory is the backend's concern. A backend that
///     cannot use a scheme speaks the word as it would without the annotation.
/// </remarks>
public enum PronunciationScheme
{
    /// <summary>
    ///     International Phonetic Alphabet; wire name <c>ipa</c>. The phonemes are Unicode text
    ///     without whitespace, such as <c>ˈpoʊlɪʃ</c>.
    /// </summary>
    Ipa = 1,

    /// <summary>
    ///     ARPAbet as used by the CMU Pronouncing Dictionary; wire name <c>arpabet</c>. The phonemes
    ///     are upper-case phones separated by single spaces, each with an optional stress digit
    ///     (0, 1 or 2), such as <c>P OW1 L IH0 SH</c>.
    /// </summary>
    Arpabet = 2,
}

/// <summary>
///     One pronunciation override: a whole word, how its letters are compared, what to do with it
///     and the action's payload.
/// </summary>
/// <param name="Word">
///     The word to match: one token of the analyzed text, compared in full. See
///     <see cref="PronunciationOverrides"/> for the exact key grammar.
/// </param>
/// <param name="CaseSensitive">
///     <see langword="true"/> to compare the exact UTF-16 characters; <see langword="false"/> to
///     compare with <see cref="StringComparison.OrdinalIgnoreCase"/> (invariant simple case
///     mapping; the current culture is never consulted).
/// </param>
/// <param name="Action">What to do with the word.</param>
/// <param name="Payload">
///     The action's payload: the spoken words for <see cref="OverrideAction.Say"/>, the
///     <c>scheme:phonemes</c> text for <see cref="OverrideAction.Pronunciation"/>, the reading id for
///     <see cref="OverrideAction.Reading"/>, and <see langword="null"/> for
///     <see cref="OverrideAction.Skip"/> and <see cref="OverrideAction.SpellOut"/>.
/// </param>
/// <remarks>
///     An entry is a plain immutable value: nothing is checked when it is created (except by the
///     factory methods' argument checks). It is validated when it is added to a
///     <see cref="PronunciationOverrides"/> dictionary by
///     <see cref="PronunciationOverrides.Create"/> or
///     <see cref="PronunciationOverrides.FromTsv"/>, which report the position of a bad entry and
///     never its content.
/// </remarks>
public sealed record PronunciationOverrideEntry(string Word, bool CaseSensitive, OverrideAction Action, string? Payload = null)
{
    /// <summary>
    ///     Creates an entry that makes the word spoken as written, bypassing every built-in rule.
    /// </summary>
    /// <param name="word">The word. Must not be null.</param>
    /// <param name="caseSensitive">Whether the comparison is case sensitive; <see langword="false"/> by default.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="word"/> is null.</exception>
    public static PronunciationOverrideEntry Skip(string word, bool caseSensitive = false)
    {
        ArgumentNullException.ThrowIfNull(word);
        return new PronunciationOverrideEntry(word, caseSensitive, OverrideAction.Skip);
    }

    /// <summary>
    ///     Creates an entry that makes the word spoken as the given plain words.
    /// </summary>
    /// <param name="word">The word. Must not be null.</param>
    /// <param name="spokenText">The words to speak instead. Must not be null.</param>
    /// <param name="caseSensitive">Whether the comparison is case sensitive; <see langword="false"/> by default.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="word"/> or <paramref name="spokenText"/> is null.</exception>
    public static PronunciationOverrideEntry Say(string word, string spokenText, bool caseSensitive = false)
    {
        ArgumentNullException.ThrowIfNull(word);
        ArgumentNullException.ThrowIfNull(spokenText);
        return new PronunciationOverrideEntry(word, caseSensitive, OverrideAction.Say, spokenText);
    }

    /// <summary>
    ///     Creates an entry that makes the word spoken letter by letter.
    /// </summary>
    /// <param name="word">The word, ASCII letters only. Must not be null.</param>
    /// <param name="caseSensitive">Whether the comparison is case sensitive; <see langword="false"/> by default.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="word"/> is null.</exception>
    public static PronunciationOverrideEntry SpellOut(string word, bool caseSensitive = false)
    {
        ArgumentNullException.ThrowIfNull(word);
        return new PronunciationOverrideEntry(word, caseSensitive, OverrideAction.SpellOut);
    }

    /// <summary>
    ///     Creates an entry that hands a phoneme string to the backend for the word.
    /// </summary>
    /// <param name="word">The word. Must not be null.</param>
    /// <param name="scheme">The notation of <paramref name="phonemes"/>.</param>
    /// <param name="phonemes">The phonemes in the notation's syntax. Must not be null.</param>
    /// <param name="caseSensitive">Whether the comparison is case sensitive; <see langword="false"/> by default.</param>
    /// <returns>The entry; its payload is <c>scheme:phonemes</c> with the scheme's lower-case wire name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="word"/> or <paramref name="phonemes"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scheme"/> is not a defined scheme.</exception>
    public static PronunciationOverrideEntry Pronounce(string word, PronunciationScheme scheme, string phonemes, bool caseSensitive = false)
    {
        ArgumentNullException.ThrowIfNull(word);
        ArgumentNullException.ThrowIfNull(phonemes);
        var name = OverrideSyntax.SchemeName(scheme)
            ?? throw new ArgumentOutOfRangeException(nameof(scheme), scheme, "The pronunciation scheme is not defined.");
        return new PronunciationOverrideEntry(word, caseSensitive, OverrideAction.Pronunciation, name + ":" + phonemes);
    }

    /// <summary>
    ///     Creates an entry that fixes the heteronym reading of the word.
    /// </summary>
    /// <param name="word">The word; its ASCII-folded spelling must be a form of the reading. Must not be null.</param>
    /// <param name="readingId">The reading id, for example <c>record.noun</c>. Must not be null.</param>
    /// <param name="caseSensitive">Whether the comparison is case sensitive; <see langword="false"/> by default.</param>
    /// <returns>The entry; the id is checked against the <see cref="ReadingInventory"/> when the entry is added to a dictionary.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="word"/> or <paramref name="readingId"/> is null.</exception>
    public static PronunciationOverrideEntry Reading(string word, string readingId, bool caseSensitive = false)
    {
        ArgumentNullException.ThrowIfNull(word);
        ArgumentNullException.ThrowIfNull(readingId);
        return new PronunciationOverrideEntry(word, caseSensitive, OverrideAction.Reading, readingId);
    }
}
