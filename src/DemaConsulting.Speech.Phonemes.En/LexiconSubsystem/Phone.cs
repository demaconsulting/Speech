using System.Diagnostics.CodeAnalysis;

namespace DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

/// <summary>
///     One ARPAbet phone: a consonant, or a vowel together with its lexical stress digit (<c>AH0</c>,
///     <c>EY1</c>, <c>IH2</c>).
/// </summary>
/// <remarks>
///     <para>
///     The phone is a one-byte value, not a string, so a pronunciation is a compact array, equality is a byte
///     comparison, and the lexicon image can store a phone as one byte. The code space is fixed by the lexicon
///     image format: <c>0</c> is never a phone (the image uses it as a terminator), the 24 consonants are
///     <c>1</c> to <c>24</c> in alphabetical order, and each of the 15 vowels owns three consecutive codes
///     (stress <c>0</c>, <c>1</c>, <c>2</c>) from <c>25</c> to <c>69</c>. A renderer for a particular voice maps
///     the 69 codes to that voice's symbols.
///     </para>
///     <para>
///     The type is immutable and therefore thread-safe. The default value is not a valid phone.
///     </para>
/// </remarks>
internal readonly struct Phone : IEquatable<Phone>
{
    /// <summary>The 24 consonant symbols in code order (code = index + 1).</summary>
    private static readonly string[] Consonants =
    [
        "B", "CH", "D", "DH", "F", "G", "HH", "JH", "K", "L", "M", "N",
        "NG", "P", "R", "S", "SH", "T", "TH", "V", "W", "Y", "Z", "ZH"
    ];

    /// <summary>The 15 vowel base symbols; vowel <c>i</c> owns the codes <c>25 + 3 * i</c> to <c>27 + 3 * i</c>.</summary>
    private static readonly string[] Vowels =
    [
        "AA", "AE", "AH", "AO", "AW", "AY", "EH", "ER", "EY", "IH", "IY", "OW", "OY", "UH", "UW"
    ];

    /// <summary>The code of the first vowel phone (<c>AA0</c>); all smaller codes are consonants.</summary>
    private const int FirstVowelCode = 25;

    /// <summary>The largest valid phone code (<c>UW2</c>).</summary>
    public const int MaxCode = FirstVowelCode + (15 * 3) - 1;

    /// <summary>The symbol of every valid code, indexed by code (index 0 is unused).</summary>
    private static readonly string[] Symbols = BuildSymbols();

    /// <summary>
    ///     Initializes a phone from its code.
    /// </summary>
    /// <param name="code">A code from <c>1</c> to <see cref="MaxCode"/>.</param>
    private Phone(byte code) => Code = code;

    /// <summary>Gets the one-byte code of the phone, from <c>1</c> to <see cref="MaxCode"/>; <c>0</c> for the invalid default value.</summary>
    public byte Code { get; }

    /// <summary>Gets a value indicating whether the phone is a vowel (it then carries a stress digit).</summary>
    public bool IsVowel => Code >= FirstVowelCode;

    /// <summary>Gets the lexical stress digit of a vowel (<c>0</c> none, <c>1</c> primary, <c>2</c> secondary), or <c>-1</c> for a consonant.</summary>
    public int Stress => IsVowel ? (Code - FirstVowelCode) % 3 : -1;

    /// <summary>Gets the symbol with its stress digit, such as <c>AH0</c> or <c>K</c>.</summary>
    public string Symbol => Symbols[Code];

    /// <summary>Gets the symbol without the stress digit, such as <c>AH</c> or <c>K</c>.</summary>
    public string BaseSymbol => IsVowel ? Vowels[(Code - FirstVowelCode) / 3] : Symbols[Code];

    /// <summary>Gets a value indicating whether the phone is a sibilant consonant (S, Z, SH, ZH, CH or JH), which takes <c>IH Z</c> as the plural and third-person ending.</summary>
    public bool IsSibilant => IsConsonant("S", "Z", "SH", "ZH", "CH", "JH");

    /// <summary>Gets a value indicating whether the phone is a voiceless consonant (P, T, K, F, TH, S, SH, CH or HH).</summary>
    public bool IsVoiceless => IsConsonant("P", "T", "K", "F", "TH", "S", "SH", "CH", "HH");

    /// <summary>
    ///     Creates a phone from its one-byte code.
    /// </summary>
    /// <param name="code">A code from <c>1</c> to <see cref="MaxCode"/>.</param>
    /// <returns>The phone.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="code"/> is not a valid phone code.</exception>
    public static Phone FromCode(byte code)
    {
        if (code is 0 or > MaxCode)
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "The phone code must be from 1 to " + MaxCode + ".");
        }

        return new Phone(code);
    }

    /// <summary>
    ///     Creates the phone with the given base symbol and stress.
    /// </summary>
    /// <param name="baseSymbol">A base symbol without a stress digit, such as <c>AH</c> or <c>K</c>.</param>
    /// <param name="stress">The stress digit of a vowel (0 to 2); ignored for a consonant.</param>
    /// <returns>The phone.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="baseSymbol"/> is not an ARPAbet base symbol.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a vowel's <paramref name="stress"/> is not 0, 1 or 2.</exception>
    public static Phone Create(string baseSymbol, int stress = 0)
    {
        ArgumentNullException.ThrowIfNull(baseSymbol);
        var consonant = Array.IndexOf(Consonants, baseSymbol);
        if (consonant >= 0)
        {
            return new Phone((byte)(consonant + 1));
        }

        var vowel = Array.IndexOf(Vowels, baseSymbol);
        if (vowel < 0)
        {
            throw new ArgumentException("'" + baseSymbol + "' is not an ARPAbet phone.", nameof(baseSymbol));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(stress);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(stress, 2);
        return new Phone((byte)(FirstVowelCode + (vowel * 3) + stress));
    }

    /// <summary>
    ///     Parses an ARPAbet symbol such as <c>AH0</c>, <c>EY1</c> or <c>K</c> (upper case).
    /// </summary>
    /// <param name="symbol">The symbol; a vowel must carry its stress digit and a consonant must not.</param>
    /// <param name="phone">Receives the phone, or the default value when parsing fails.</param>
    /// <returns><see langword="true"/> when the symbol is valid.</returns>
    public static bool TryParse(ReadOnlySpan<char> symbol, out Phone phone)
    {
        phone = default;
        var text = symbol.ToString();

        // A consonant is the whole symbol
        var consonant = Array.IndexOf(Consonants, text);
        if (consonant >= 0)
        {
            phone = new Phone((byte)(consonant + 1));
            return true;
        }

        // A vowel is a two-letter base symbol and one stress digit
        if (text.Length != 3 || text[2] is < '0' or > '2')
        {
            return false;
        }

        var vowel = Array.IndexOf(Vowels, text[..2]);
        if (vowel < 0)
        {
            return false;
        }

        phone = new Phone((byte)(FirstVowelCode + (vowel * 3) + (text[2] - '0')));
        return true;
    }

    /// <summary>
    ///     Returns the same vowel with another stress digit; a consonant is returned unchanged.
    /// </summary>
    /// <param name="stress">The new stress digit (0 to 2).</param>
    /// <returns>The phone with the stress digit.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="stress"/> is not 0, 1 or 2.</exception>
    public Phone WithStress(int stress)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stress);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(stress, 2);
        return IsVowel ? new Phone((byte)(Code - Stress + stress)) : this;
    }

    /// <inheritdoc />
    public bool Equals(Phone other) => Code == other.Code;

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Phone other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Code;

    /// <inheritdoc />
    public override string ToString() => Code == 0 ? string.Empty : Symbol;

    /// <summary>Compares two phones for equality.</summary>
    /// <param name="left">The first phone.</param>
    /// <param name="right">The second phone.</param>
    /// <returns><see langword="true"/> when the phones are the same.</returns>
    public static bool operator ==(Phone left, Phone right) => left.Equals(right);

    /// <summary>Compares two phones for inequality.</summary>
    /// <param name="left">The first phone.</param>
    /// <param name="right">The second phone.</param>
    /// <returns><see langword="true"/> when the phones differ.</returns>
    public static bool operator !=(Phone left, Phone right) => !left.Equals(right);

    /// <summary>
    ///     Builds the symbol table indexed by code.
    /// </summary>
    /// <returns>The symbols; index 0 is the empty string.</returns>
    private static string[] BuildSymbols()
    {
        var symbols = new string[MaxCode + 1];
        symbols[0] = string.Empty;
        for (var i = 0; i < Consonants.Length; i++)
        {
            symbols[i + 1] = Consonants[i];
        }

        for (var i = 0; i < Vowels.Length; i++)
        {
            for (var stress = 0; stress < 3; stress++)
            {
                symbols[FirstVowelCode + (i * 3) + stress] = Vowels[i] + (char)('0' + stress);
            }
        }

        return symbols;
    }

    /// <summary>
    ///     Tests whether this phone is a consonant with one of the given symbols.
    /// </summary>
    /// <param name="symbols">The consonant symbols.</param>
    /// <returns><see langword="true"/> when the phone is a consonant named in <paramref name="symbols"/>.</returns>
    private bool IsConsonant(params string[] symbols) => Code is not 0 && !IsVowel && Array.IndexOf(symbols, Symbols[Code]) >= 0;
}
