using System.Text;
using System.Text.Json;

namespace DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;

/// <summary>
///     The settings of a Piper voice, read from the voice's <c>.onnx.json</c> file: the symbol table that turns phoneme
///     symbols into the integer ids the graph takes, the output sample rate, the speaker count and the voice's own
///     recommended inference scales.
/// </summary>
/// <remarks>
///     <para>
///     The map's keys are single Unicode symbols and are compared ordinally: the map holds symbols that differ only by
///     case. Each symbol maps to one or more ids. Three symbols frame every utterance and are required:
///     <see cref="BeginSymbol"/> (<c>^</c>), <see cref="PadSymbol"/> (<c>_</c>, placed after each symbol) and
///     <see cref="EndSymbol"/> (<c>$</c>); a configuration without them cannot be used and is rejected with an error that
///     names the missing symbol rather than producing silent or garbled audio.
///     </para>
///     Instances are immutable and thread-safe.
/// </remarks>
internal sealed class PiperVoiceConfig
{
    /// <summary>The symbol that starts every utterance.</summary>
    public const string BeginSymbol = "^";

    /// <summary>The symbol that follows every phoneme symbol.</summary>
    public const string PadSymbol = "_";

    /// <summary>The symbol that ends every utterance.</summary>
    public const string EndSymbol = "$";

    /// <summary>The inference scales Piper's own default configuration uses when the file declares none.</summary>
    private const float DefaultNoiseScale = 0.667f;
    private const float DefaultLengthScale = 1.0f;
    private const float DefaultNoiseW = 0.8f;

    private readonly Dictionary<string, long[]> _idMap;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PiperVoiceConfig"/> class.
    /// </summary>
    private PiperVoiceConfig(Dictionary<string, long[]> idMap)
    {
        _idMap = idMap;
        BeginIds = idMap[BeginSymbol];
        PadIds = idMap[PadSymbol];
        EndIds = idMap[EndSymbol];
    }

    /// <summary>Gets the output sample rate in Hz (<c>audio.sample_rate</c>).</summary>
    public int SampleRate { get; private init; }

    /// <summary>Gets the number of speakers the voice has (<c>num_speakers</c>; 1 when the file declares none).</summary>
    public int NumSpeakers { get; private init; } = 1;

    /// <summary>Gets the voice's recommended noise scale (<c>inference.noise_scale</c>).</summary>
    public float NoiseScale { get; private init; } = DefaultNoiseScale;

    /// <summary>Gets the voice's recommended length scale (<c>inference.length_scale</c>); larger is slower.</summary>
    public float LengthScale { get; private init; } = DefaultLengthScale;

    /// <summary>Gets the voice's recommended noise-width scale (<c>inference.noise_w</c>).</summary>
    public float NoiseW { get; private init; } = DefaultNoiseW;

    /// <summary>Gets the name of the espeak-ng voice the symbol table was made for (<c>espeak.voice</c>; empty when absent).</summary>
    public string EspeakVoice { get; private init; } = string.Empty;

    /// <summary>Gets the number of symbols in the map, framing symbols included.</summary>
    public int SymbolCount => _idMap.Count;

    /// <summary>Gets the ids of the symbol that starts an utterance.</summary>
    public IReadOnlyList<long> BeginIds { get; }

    /// <summary>Gets the ids of the pad symbol.</summary>
    public IReadOnlyList<long> PadIds { get; }

    /// <summary>Gets the ids of the symbol that ends an utterance.</summary>
    public IReadOnlyList<long> EndIds { get; }

    /// <summary>
    ///     Parses the text of a voice configuration file.
    /// </summary>
    /// <param name="json">The JSON text. Must not be null.</param>
    /// <returns>The configuration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the text is not a usable voice configuration: not JSON, no <c>phoneme_id_map</c>, a missing or
    ///     non-positive <c>audio.sample_rate</c>, a phoneme type other than the string <c>espeak</c>, a map without the
    ///     <c>^</c>, <c>_</c> or <c>$</c> symbol, or any field present with a JSON type other than the one documented
    ///     for it (see the remarks). The message names what is wrong.
    /// </exception>
    /// <remarks>
    ///     Every field that is present must have exactly its documented JSON type and range, so a configuration that
    ///     was damaged or hand-edited fails at load time rather than being coerced: <c>phoneme_type</c> a string equal
    ///     to <c>espeak</c>; <c>audio.sample_rate</c> and <c>num_speakers</c> positive JSON integers;
    ///     <c>inference.noise_scale</c>, <c>inference.length_scale</c> and <c>inference.noise_w</c> finite JSON numbers
    ///     (the length scale positive, the others not negative); <c>espeak.voice</c> a string; <c>audio</c>,
    ///     <c>inference</c> and <c>espeak</c> objects; each <c>phoneme_id_map</c> entry a non-empty key mapped to a
    ///     non-empty array of non-negative JSON integers, with no duplicated key. Absent optional fields take their
    ///     defaults.
    /// </remarks>
    public static PiperVoiceConfig Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("phoneme_id_map", out var mapElement) ||
                mapElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("The Piper voice configuration has no 'phoneme_id_map' object.");
            }

            // Only the string "espeak" is accepted; any other value, of any JSON type, is a different phoneme scheme
            // (or a damaged file) that the phonemizer cannot serve.
            if (root.TryGetProperty("phoneme_type", out var phonemeType) &&
                !(phonemeType.ValueKind == JsonValueKind.String &&
                  string.Equals(phonemeType.GetString(), "espeak", StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    $"The Piper voice configuration has phoneme_type {Describe(phonemeType)}; only the string 'espeak' is supported.");
            }

            var idMap = ReadIdMap(mapElement);
            var missingSymbol = new[] { BeginSymbol, PadSymbol, EndSymbol }.FirstOrDefault(required => !idMap.ContainsKey(required));
            if (missingSymbol is not null)
            {
                throw new InvalidDataException($"The Piper phoneme_id_map has no entry for the required symbol '{missingSymbol}'.");
            }

            return new PiperVoiceConfig(idMap)
            {
                SampleRate = ReadSampleRate(root),
                NumSpeakers = ReadNumSpeakers(root),
                NoiseScale = ReadScale(root, "noise_scale", DefaultNoiseScale, allowZero: true),
                LengthScale = ReadScale(root, "length_scale", DefaultLengthScale, allowZero: false),
                NoiseW = ReadScale(root, "noise_w", DefaultNoiseW, allowZero: true),
                EspeakVoice = ReadEspeakVoice(root),
            };
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException("The Piper voice configuration is not valid.", ex);
        }
    }

    /// <summary>
    ///     Reads and parses a voice configuration file.
    /// </summary>
    /// <param name="path">The path of the <c>.onnx.json</c> file. Must not be null or empty.</param>
    /// <returns>The configuration.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is not a usable voice configuration.</exception>
    public static PiperVoiceConfig Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Parse(File.ReadAllText(path, Encoding.UTF8));
    }

    /// <summary>
    ///     Looks up the ids of a symbol.
    /// </summary>
    /// <param name="symbol">The symbol (one Unicode scalar value). Must not be null.</param>
    /// <param name="ids">The symbol's ids, when it is in the map.</param>
    /// <returns><see langword="true"/> when the map has the symbol.</returns>
    public bool TryGetIds(string symbol, out IReadOnlyList<long> ids)
    {
        if (_idMap.TryGetValue(symbol, out var found))
        {
            ids = found;
            return true;
        }

        ids = [];
        return false;
    }

    /// <summary>
    ///     Reads <c>phoneme_id_map</c>: every key a non-empty symbol, every value a non-empty array of non-negative
    ///     integers, no key repeated.
    /// </summary>
    private static Dictionary<string, long[]> ReadIdMap(JsonElement mapElement)
    {
        var idMap = new Dictionary<string, long[]>(StringComparer.Ordinal);
        foreach (var entry in mapElement.EnumerateObject())
        {
            if (entry.Name.Length == 0)
            {
                throw new InvalidDataException("The Piper phoneme_id_map has an entry with an empty symbol.");
            }

            if (entry.Value.ValueKind != JsonValueKind.Array || entry.Value.GetArrayLength() == 0)
            {
                throw new InvalidDataException(
                    $"The Piper phoneme_id_map entry for '{entry.Name}' is {Describe(entry.Value)}; expected a non-empty array of integer ids.");
            }

            var ids = new List<long>();
            foreach (var id in entry.Value.EnumerateArray())
            {
                if (id.ValueKind != JsonValueKind.Number || !id.TryGetInt64(out var value) || value < 0)
                {
                    throw new InvalidDataException(
                        $"The Piper phoneme_id_map entry for '{entry.Name}' has the id {Describe(id)}; expected a non-negative integer.");
                }

                ids.Add(value);
            }

            if (!idMap.TryAdd(entry.Name, [.. ids]))
            {
                throw new InvalidDataException($"The Piper phoneme_id_map lists the symbol '{entry.Name}' more than once.");
            }
        }

        return idMap;
    }

    /// <summary>Reads <c>audio.sample_rate</c>, which every usable configuration has, as a positive integer.</summary>
    private static int ReadSampleRate(JsonElement root)
    {
        if (!root.TryGetProperty("audio", out var audio))
        {
            throw new InvalidDataException("The Piper voice configuration has no positive 'audio.sample_rate'.");
        }

        if (audio.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"The Piper voice configuration has 'audio' as {Describe(audio)}; expected an object.");
        }

        if (!audio.TryGetProperty("sample_rate", out var rate))
        {
            throw new InvalidDataException("The Piper voice configuration has no positive 'audio.sample_rate'.");
        }

        return ReadPositiveInteger(rate, "audio.sample_rate");
    }

    /// <summary>Reads <c>num_speakers</c> as a positive integer, or 1 when it is absent.</summary>
    private static int ReadNumSpeakers(JsonElement root) =>
        root.TryGetProperty("num_speakers", out var speakers) ? ReadPositiveInteger(speakers, "num_speakers") : 1;

    /// <summary>Reads <paramref name="value"/> as a positive JSON integer that fits <see cref="int"/>.</summary>
    private static int ReadPositiveInteger(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number) || number <= 0)
        {
            throw new InvalidDataException(
                $"The Piper voice configuration has '{name}' as {Describe(value)}; expected a positive integer.");
        }

        return number;
    }

    /// <summary>
    ///     Reads one of the <c>inference</c> scales as a finite JSON number (positive, or also zero when
    ///     <paramref name="allowZero"/>), falling back to <paramref name="fallback"/> when it is absent.
    /// </summary>
    private static float ReadScale(JsonElement root, string name, float fallback, bool allowZero)
    {
        if (!root.TryGetProperty("inference", out var inference))
        {
            return fallback;
        }

        if (inference.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"The Piper voice configuration has 'inference' as {Describe(inference)}; expected an object.");
        }

        if (!inference.TryGetProperty(name, out var value))
        {
            return fallback;
        }

        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetSingle(out var scale) ||
            !float.IsFinite(scale) ||
            scale < 0f ||
            (scale == 0f && !allowZero))
        {
            throw new InvalidDataException(
                $"The Piper voice configuration has 'inference.{name}' as {Describe(value)}; expected a finite " +
                (allowZero ? "non-negative" : "positive") + " number.");
        }

        return scale;
    }

    /// <summary>Reads <c>espeak.voice</c> as a string, or an empty string when it is absent.</summary>
    private static string ReadEspeakVoice(JsonElement root)
    {
        if (!root.TryGetProperty("espeak", out var espeak))
        {
            return string.Empty;
        }

        if (espeak.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"The Piper voice configuration has 'espeak' as {Describe(espeak)}; expected an object.");
        }

        if (!espeak.TryGetProperty("voice", out var voice))
        {
            return string.Empty;
        }

        if (voice.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"The Piper voice configuration has 'espeak.voice' as {Describe(voice)}; expected a string.");
        }

        return voice.GetString() ?? string.Empty;
    }

    /// <summary>
    ///     Describes <paramref name="value"/> for an error message by its JSON type and, for a short scalar, its text.
    /// </summary>
    private static string Describe(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => $"the string '{value.GetString()}'",
        JsonValueKind.Number => $"the number {value.GetRawText()}",
        JsonValueKind.True or JsonValueKind.False => $"the boolean {value.GetRawText()}",
        JsonValueKind.Null => "null",
        JsonValueKind.Array => "an array",
        JsonValueKind.Object => "an object",
        _ => "of an unexpected type",
    };
}
