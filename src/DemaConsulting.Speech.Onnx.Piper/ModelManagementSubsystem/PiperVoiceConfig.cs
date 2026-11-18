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
    ///     non-positive <c>audio.sample_rate</c>, a phoneme type other than <c>espeak</c>, or a map without the
    ///     <c>^</c>, <c>_</c> or <c>$</c> symbol. The message names what is wrong.
    /// </exception>
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

            if (root.TryGetProperty("phoneme_type", out var phonemeType) &&
                phonemeType.ValueKind == JsonValueKind.String &&
                !string.Equals(phonemeType.GetString(), "espeak", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"The Piper voice uses phoneme type '{phonemeType.GetString()}'; only espeak-style phonemes are supported.");
            }

            var idMap = new Dictionary<string, long[]>(StringComparer.Ordinal);
            foreach (var entry in mapElement.EnumerateObject())
            {
                idMap[entry.Name] = [.. entry.Value.EnumerateArray().Select(id => id.GetInt64())];
            }

            foreach (var required in new[] { BeginSymbol, PadSymbol, EndSymbol })
            {
                if (!idMap.TryGetValue(required, out var ids) || ids.Length == 0)
                {
                    throw new InvalidDataException($"The Piper phoneme_id_map has no entry for the required symbol '{required}'.");
                }
            }

            return new PiperVoiceConfig(idMap)
            {
                SampleRate = ReadSampleRate(root),
                NumSpeakers = root.TryGetProperty("num_speakers", out var speakers) ? speakers.GetInt32() : 1,
                NoiseScale = ReadScale(root, "noise_scale", DefaultNoiseScale),
                LengthScale = ReadScale(root, "length_scale", DefaultLengthScale),
                NoiseW = ReadScale(root, "noise_w", DefaultNoiseW),
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

    /// <summary>Reads <c>audio.sample_rate</c>, which every usable configuration has.</summary>
    private static int ReadSampleRate(JsonElement root)
    {
        if (!root.TryGetProperty("audio", out var audio) ||
            audio.ValueKind != JsonValueKind.Object ||
            !audio.TryGetProperty("sample_rate", out var rate) ||
            rate.GetInt32() <= 0)
        {
            throw new InvalidDataException("The Piper voice configuration has no positive 'audio.sample_rate'.");
        }

        return rate.GetInt32();
    }

    /// <summary>Reads one of the <c>inference</c> scales, falling back to <paramref name="fallback"/> when it is absent.</summary>
    private static float ReadScale(JsonElement root, string name, float fallback) =>
        root.TryGetProperty("inference", out var inference) &&
        inference.ValueKind == JsonValueKind.Object &&
        inference.TryGetProperty(name, out var value)
            ? value.GetSingle()
            : fallback;

    /// <summary>Reads <c>espeak.voice</c>, or an empty string when it is absent.</summary>
    private static string ReadEspeakVoice(JsonElement root) =>
        root.TryGetProperty("espeak", out var espeak) &&
        espeak.ValueKind == JsonValueKind.Object &&
        espeak.TryGetProperty("voice", out var voice) &&
        voice.ValueKind == JsonValueKind.String
            ? voice.GetString() ?? string.Empty
            : string.Empty;
}
