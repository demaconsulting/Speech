using System.Text;

namespace DemaConsulting.Speech.Phonemes.En.RenderingSubsystem;

/// <summary>
///     Translates the IPA pronunciation a host supplied for a word (the <c>ipa:</c> pronunciation override) into the
///     symbols of one of the two voices, and proves that every symbol it emits is one the voice knows.
/// </summary>
/// <remarks>
///     <para>
///     The accepted input is the IPA of General American English as espeak-ng writes it (the form the Piper voice was
///     trained on): the diphthongs and affricates may be written as two symbols (<c>aɪ aʊ eɪ oʊ ɔɪ tʃ dʒ</c>) or as the
///     ligatures <c>ʧ ʤ</c>, the r-colored vowels as <c>ɚ ɝ ɜː</c>, and length as <c>ː</c>. For Kokoro the diphthongs
///     become its single capital letters (<c>I W A O Y</c>), the affricates <c>ʧ ʤ</c>, <c>ɝ</c> and <c>ɜː</c> become
///     <c>ɜɹ</c>, <c>ɚ</c> becomes <c>əɹ</c> and length marks are dropped, exactly as the renderer writes the same
///     sounds from ARPAbet; for Piper <c>ɝ</c> becomes <c>ɜː</c> and the ligatures become two symbols.
///     </para>
///     <para>
///     Every other symbol passes through unchanged, but only when the voice's own inventory contains it (the character
///     vocabulary of the Kokoro tokenizer and the <c>phoneme_id_map</c> of the Piper voice, kept here as the
///     <see cref="KokoroVocabulary"/> and <see cref="PiperSymbols"/> strings and proven equal to the shipped files by
///     the tests). The letters Kokoro reserves for its diphthongs (<c>A I O W Y Q S T</c>) and the control symbols of
///     the models are not IPA and are never accepted. A pronunciation that cannot be translated completely is
///     <i>not</i> approximated: the translation returns <see langword="null"/> and the renderer speaks the word's
///     normal pronunciation instead.
///     </para>
///     <para>The class is stateless and thread-safe.</para>
/// </remarks>
internal static class IpaSymbols
{
    /// <summary>The single-character symbols of the vocabulary of Kokoro's tokenizer.</summary>
    public const string KokoroVocabulary = " !\"$(),.:;?AIOQSTWYabcdefhijklmnopqrstuvwxyzæçðøŋœɐɑɒɔɕɖəɚɛɜɟɡɣɤɥɨɪɯɰɲɳɴɸɹɻɽɾʁʂʃʈʊʋʌʎʒʔʝʣʤʥʦʧʨʰʲˈˌː\u0303βθχᵊᵝᵻ—“”…→↓↗↘ꭧ";

    /// <summary>The single-character symbols of the <c>phoneme_id_map</c> of the Piper voice.</summary>
    public const string PiperSymbols = " !\"#$'(),-.0123456789:;?X^_abcdefghijklmnopqrstuvwxyzæçðøħŋœǀǁǂǃɐɑɒɓɔɕɖɗɘəɚɛɜɞɟɠɡɢɣɤɥɦɧɨɪɫɬɭɮɯɰɱɲɳɴɵɶɸɹɺɻɽɾʀʁʂʃʄʈʉʊʋʌʍʎʏʐʑʒʔʕʘʙʛʜʝʟʡʢʦʰʲˈˌːˑ˞ˤ\u0303\u030A\u031D\u0327\u0329\u032A\u032F\u033A\u033Bβεθχᵻ↑↓ⱱ";

    /// <summary>The symbols of Kokoro's vocabulary that are not IPA: its diphthong capitals and the padding symbol.</summary>
    private static readonly string KokoroReserved = new(['A', 'I', 'O', 'Q', 'S', 'T', 'W', 'Y', '$']);

    /// <summary>The symbols of the Piper map that are not IPA: the padding, start and end symbols and two espeak markers.</summary>
    private const string PiperReserved = "^_$#X";

    /// <summary>
    ///     Translates an IPA pronunciation into Kokoro's symbols.
    /// </summary>
    /// <param name="ipa">The IPA, espeak style.</param>
    /// <returns>The symbols, all in <see cref="KokoroVocabulary"/>, or <see langword="null"/> when the pronunciation cannot be represented completely.</returns>
    public static string? ToKokoro(string ipa) => Translate(ipa, kokoro: true, strict: true);

    /// <summary>
    ///     Translates an IPA pronunciation into Kokoro's symbols, dropping the symbols the voice does not know.
    /// </summary>
    /// <param name="ipa">The IPA, espeak style.</param>
    /// <returns>The symbols, all in <see cref="KokoroVocabulary"/>.</returns>
    public static string ToKokoroLossy(string ipa) => Translate(ipa, kokoro: true, strict: false)!;

    /// <summary>
    ///     Translates an IPA pronunciation into the symbols of the Piper voice.
    /// </summary>
    /// <param name="ipa">The IPA, espeak style.</param>
    /// <returns>The symbols, all in <see cref="PiperSymbols"/>, or <see langword="null"/> when the pronunciation cannot be represented completely.</returns>
    public static string? ToPiper(string ipa) => Translate(ipa, kokoro: false, strict: true);

    /// <summary>
    ///     Translates an IPA pronunciation into the symbols of the Piper voice, dropping the symbols the voice does not know.
    /// </summary>
    /// <param name="ipa">The IPA, espeak style.</param>
    /// <returns>The symbols, all in <see cref="PiperSymbols"/>.</returns>
    public static string ToPiperLossy(string ipa) => Translate(ipa, kokoro: false, strict: false)!;

    /// <summary>
    ///     Translates an IPA string symbol by symbol.
    /// </summary>
    /// <param name="ipa">The IPA.</param>
    /// <param name="kokoro"><see langword="true"/> for Kokoro, <see langword="false"/> for Piper.</param>
    /// <param name="strict">Whether an unrepresentable symbol fails the translation (otherwise it is dropped).</param>
    /// <returns>The symbols, or <see langword="null"/> when strict and a symbol cannot be represented.</returns>
    private static string? Translate(string ipa, bool kokoro, bool strict)
    {
        ArgumentNullException.ThrowIfNull(ipa);
        var builder = new StringBuilder(ipa.Length + 4);
        var index = 0;
        while (index < ipa.Length)
        {
            var (text, consumed) = kokoro ? ToKokoroAt(ipa, index) : ToPiperAt(ipa, index);
            if (text is null)
            {
                // Passes through only when the voice has the symbol
                text = IsPassThrough(ipa[index], kokoro) ? ipa[index].ToString() : null;
                consumed = 1;
            }

            if (text is null && strict)
            {
                return null;
            }

            builder.Append(text);
            index += consumed;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Tests whether a symbol may pass through to a voice unchanged.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="kokoro"><see langword="true"/> for Kokoro, <see langword="false"/> for Piper.</param>
    /// <returns><see langword="true"/> when it is in the voice's inventory and is an IPA symbol.</returns>
    private static bool IsPassThrough(char symbol, bool kokoro) => kokoro
        ? KokoroVocabulary.Contains(symbol) && !KokoroReserved.Contains(symbol)
        : PiperSymbols.Contains(symbol) && !PiperReserved.Contains(symbol);

    /// <summary>
    ///     Translates the symbol or symbol pair at an index into Kokoro's symbols.
    /// </summary>
    /// <param name="ipa">The IPA.</param>
    /// <param name="index">The index.</param>
    /// <returns>The translation and the number of characters it consumed, or <see langword="null"/> text when the symbol has no special rule.</returns>
    private static (string? Text, int Consumed) ToKokoroAt(string ipa, int index)
    {
        var pair = Next(ipa, index);
        switch (pair)
        {
            case "aɪ":
                return ("I", 2);
            case "aʊ":
                return ("W", 2);
            case "eɪ":
                return ("A", 2);
            case "oʊ":
                return ("O", 2);
            case "ɔɪ":
                return ("Y", 2);
            case "tʃ":
                return ("ʧ", 2);
            case "dʒ":
                return ("ʤ", 2);
            case "ɜː":
                return ("ɜɹ", 2 + LinkingR(ipa, index + 2));
        }

        return ipa[index] switch
        {
            'ɝ' => ("ɜɹ", 1 + LinkingR(ipa, index + 1)),
            'ɚ' => ("əɹ", 1 + LinkingR(ipa, index + 1)),
            'ː' => (string.Empty, 1),
            'g' => ("ɡ", 1),
            'ɫ' => ("l", 1),
            _ => (null, 1),
        };
    }

    /// <summary>
    ///     Translates the symbol or symbol pair at an index into the symbols of the Piper voice.
    /// </summary>
    /// <param name="ipa">The IPA.</param>
    /// <param name="index">The index.</param>
    /// <returns>The translation and the number of characters it consumed, or <see langword="null"/> text when the symbol has no special rule.</returns>
    private static (string? Text, int Consumed) ToPiperAt(string ipa, int index) => ipa[index] switch
    {
        'ɝ' => ("ɜː", 1 + LinkingR(ipa, index + 1)),
        'ʧ' => ("tʃ", 1),
        'ʤ' => ("dʒ", 1),
        _ => (null, 1),
    };

    /// <summary>
    ///     Gets the symbol pair at an index.
    /// </summary>
    /// <param name="ipa">The IPA.</param>
    /// <param name="index">The index.</param>
    /// <returns>The two characters, or an empty string at the end.</returns>
    private static string Next(string ipa, int index) => index + 1 < ipa.Length ? ipa.Substring(index, 2) : string.Empty;

    /// <summary>
    ///     Counts the explicit <c>ɹ</c> after an r-colored vowel, which the r-colored symbol already contains.
    /// </summary>
    /// <param name="ipa">The IPA.</param>
    /// <param name="index">The index after the vowel.</param>
    /// <returns><c>1</c> when an <c>ɹ</c> follows (it is consumed), otherwise <c>0</c>.</returns>
    private static int LinkingR(string ipa, int index) => index < ipa.Length && ipa[index] == 'ɹ' ? 1 : 0;
}
