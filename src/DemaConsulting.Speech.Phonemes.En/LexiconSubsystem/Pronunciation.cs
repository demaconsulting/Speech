using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

/// <summary>
///     An immutable sequence of ARPAbet phones: the pronunciation of one word, or of a word part.
/// </summary>
/// <remarks>
///     The class wraps its phone array and never exposes it, so a pronunciation shared between a lexicon
///     lookup and its callers cannot be changed. Equality is by phone sequence. The text form is the CMUdict
///     one: upper-case symbols separated by single spaces (<c>K AE1 T</c>). Thread-safe because immutable.
/// </remarks>
internal sealed class Pronunciation : IEquatable<Pronunciation>
{
    /// <summary>The phones.</summary>
    private readonly Phone[] _phones;

    /// <summary>
    ///     Initializes a pronunciation from phones.
    /// </summary>
    /// <param name="phones">The phones; copied.</param>
    public Pronunciation(ReadOnlySpan<Phone> phones) => _phones = phones.ToArray();

    /// <summary>Gets the empty pronunciation.</summary>
    public static Pronunciation Empty { get; } = new([]);

    /// <summary>Gets the number of phones.</summary>
    public int Count => _phones.Length;

    /// <summary>Gets the phones as a read-only span; valid as long as the pronunciation is referenced.</summary>
    public ReadOnlySpan<Phone> Phones => _phones;

    /// <summary>Gets the phone at an index.</summary>
    /// <param name="index">The zero-based index.</param>
    /// <returns>The phone.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when <paramref name="index"/> is outside the pronunciation.</exception>
    public Phone this[int index] => _phones[index];

    /// <summary>
    ///     Parses the CMUdict text form, such as <c>K AE1 T</c>.
    /// </summary>
    /// <param name="text">Upper-case ARPAbet symbols separated by spaces.</param>
    /// <returns>The pronunciation.</returns>
    /// <exception cref="FormatException">Thrown when a symbol is not an ARPAbet phone.</exception>
    public static Pronunciation Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var symbols = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var phones = new Phone[symbols.Length];
        for (var i = 0; i < symbols.Length; i++)
        {
            if (!Phone.TryParse(symbols[i], out phones[i]))
            {
                throw new FormatException("'" + symbols[i] + "' is not an ARPAbet phone.");
            }
        }

        return new Pronunciation(phones);
    }

    /// <summary>
    ///     Returns this pronunciation followed by more phones.
    /// </summary>
    /// <param name="suffix">The phones to append.</param>
    /// <returns>A new pronunciation.</returns>
    public Pronunciation Append(ReadOnlySpan<Phone> suffix)
    {
        var phones = new Phone[_phones.Length + suffix.Length];
        _phones.CopyTo(phones, 0);
        suffix.CopyTo(phones.AsSpan(_phones.Length));
        return new Pronunciation(phones);
    }

    /// <summary>
    ///     Returns this pronunciation without its last phones.
    /// </summary>
    /// <param name="count">The number of phones to drop; at most <see cref="Count"/>.</param>
    /// <returns>A new, shorter pronunciation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative or larger than <see cref="Count"/>.</exception>
    public Pronunciation DropLast(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, _phones.Length);
        return new Pronunciation(_phones.AsSpan(0, _phones.Length - count));
    }

    /// <summary>
    ///     Tests whether this pronunciation starts with another one.
    /// </summary>
    /// <param name="prefix">The candidate prefix.</param>
    /// <returns><see langword="true"/> when the first phones of this pronunciation are exactly <paramref name="prefix"/>.</returns>
    public bool StartsWith(Pronunciation prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return _phones.AsSpan().StartsWith(prefix._phones.AsSpan());
    }

    /// <inheritdoc />
    public bool Equals([NotNullWhen(true)] Pronunciation? other) => other is not null && _phones.AsSpan().SequenceEqual(other._phones);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => Equals(obj as Pronunciation);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var phone in _phones)
        {
            hash.Add(phone.Code);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    ///     Returns the CMUdict text form.
    /// </summary>
    /// <returns>Symbols separated by single spaces; empty for the empty pronunciation.</returns>
    public override string ToString()
    {
        var builder = new StringBuilder();
        foreach (var phone in _phones)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(phone.Symbol);
        }

        return builder.ToString();
    }
}
