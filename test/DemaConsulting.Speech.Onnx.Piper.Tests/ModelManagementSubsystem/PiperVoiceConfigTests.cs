using System.Text.Json.Nodes;
using DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Onnx.Piper.Tests.ModelManagementSubsystem;

/// <summary>
///     Behavior tests for <see cref="PiperVoiceConfig"/> against a trimmed copy of the real configuration of the
///     <c>en_US-libritts_r-medium</c> voice (<c>TestData/en_US-libritts_r-medium.onnx.trimmed.json</c>, CC BY 4.0, the
///     attribution is inside the file): what is read from it, and that an unusable configuration is rejected with a
///     message that says what is wrong.
/// </summary>
public sealed class PiperVoiceConfigTests
{
    /// <summary>Proves the real configuration's settings are read: rate, speakers, the voice's own scales, the espeak voice and the symbol table with its framing symbols.</summary>
    [Fact]
    public void PiperVoiceConfig_Parse_RealVoiceConfiguration_ReadsSettingsAndSymbolTable()
    {
        // Act
        var config = PiperVoiceConfig.Parse(PiperTestFixtures.TrimmedConfigJson);

        // Assert
        Assert.Equal(22050, config.SampleRate);
        Assert.Equal(904, config.NumSpeakers);
        Assert.Equal(0.333f, config.NoiseScale);
        Assert.Equal(1.0f, config.LengthScale);
        Assert.Equal(0.333f, config.NoiseW);
        Assert.Equal("en-us", config.EspeakVoice);
        Assert.Equal(159, config.SymbolCount);
        Assert.Equal([1L], config.BeginIds);
        Assert.Equal([0L], config.PadIds);
        Assert.Equal([2L], config.EndIds);
    }

    /// <summary>Proves symbols are looked up ordinally and case-sensitively, including the stress marks, and an unknown symbol is reported as absent.</summary>
    [Fact]
    public void PiperVoiceConfig_TryGetIds_Symbols_AreOrdinalAndCaseSensitive()
    {
        // Arrange
        var config = PiperVoiceConfig.Parse(PiperTestFixtures.TrimmedConfigJson);

        // Act
        var hasStress = config.TryGetIds("ˈ", out var stressIds);
        var hasLower = config.TryGetIds("x", out var lowerIds);
        var hasUpper = config.TryGetIds("X", out var upperIds);
        var hasUnknown = config.TryGetIds("\u4E2D", out var unknownIds);

        // Assert
        Assert.True(hasStress);
        Assert.Single(stressIds);
        Assert.True(hasLower);
        Assert.True(hasUpper);
        Assert.NotEqual(lowerIds, upperIds);
        Assert.False(hasUnknown);
        Assert.Empty(unknownIds);
    }

    /// <summary>Proves a configuration without an <c>inference</c> block or speaker count gets Piper's own defaults.</summary>
    [Fact]
    public void PiperVoiceConfig_Parse_MinimalConfiguration_UsesDefaults()
    {
        // Arrange
        const string json = """
            { "audio": { "sample_rate": 16000 }, "phoneme_id_map": { "^": [1], "_": [0], "$": [2], "a": [5] } }
            """;

        // Act
        var config = PiperVoiceConfig.Parse(json);

        // Assert
        Assert.Equal(16000, config.SampleRate);
        Assert.Equal(1, config.NumSpeakers);
        Assert.Equal(0.667f, config.NoiseScale);
        Assert.Equal(1.0f, config.LengthScale);
        Assert.Equal(0.8f, config.NoiseW);
        Assert.Equal(string.Empty, config.EspeakVoice);
    }

    /// <summary>Proves a configuration lacking any of the framing symbols <c>^ _ $</c> is rejected with a message naming the symbol.</summary>
    [Theory]
    [InlineData("^")]
    [InlineData("_")]
    [InlineData("$")]
    public void PiperVoiceConfig_Parse_MissingFramingSymbol_ThrowsNamingTheSymbol(string symbol)
    {
        // Arrange
        var document = JsonNode.Parse(PiperTestFixtures.TrimmedConfigJson)!;
        document["phoneme_id_map"]!.AsObject().Remove(symbol);

        // Act
        var error = Assert.Throws<InvalidDataException>(() => PiperVoiceConfig.Parse(document.ToJsonString()));

        // Assert
        Assert.Contains($"'{symbol}'", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves other unusable configurations (not JSON, no map, no sample rate, a non-espeak phoneme type) are rejected.</summary>
    [Theory]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("""{ "audio": { "sample_rate": 22050 } }""")]
    [InlineData("""{ "phoneme_id_map": { "^": [1], "_": [0], "$": [2] } }""")]
    [InlineData("""{ "audio": { "sample_rate": 0 }, "phoneme_id_map": { "^": [1], "_": [0], "$": [2] } }""")]
    [InlineData("""{ "audio": { "sample_rate": 22050 }, "phoneme_type": "text", "phoneme_id_map": { "^": [1], "_": [0], "$": [2] } }""")]
    [InlineData("""{ "audio": { "sample_rate": 22050 }, "phoneme_id_map": { "^": [1], "_": [0], "$": ["x"] } }""")]
    public void PiperVoiceConfig_Parse_UnusableConfiguration_ThrowsInvalidData(string json)
    {
        // Act & Assert
        Assert.Throws<InvalidDataException>(() => PiperVoiceConfig.Parse(json));
    }

    /// <summary>
    ///     Proves every field that is present must have exactly its documented JSON type and range: a wrong-typed or
    ///     out-of-range value (number, string, boolean, array, object or null where another type is required) is
    ///     rejected with an error that names the field, rather than being coerced or silently ignored.
    /// </summary>
    [Theory]
    [InlineData("phoneme_type", "1")]
    [InlineData("phoneme_type", "true")]
    [InlineData("phoneme_type", """["espeak"]""")]
    [InlineData("phoneme_type", """{ "name": "espeak" }""")]
    [InlineData("phoneme_type", "null")]
    [InlineData("phoneme_type", "\"ESPEAK\"")]
    [InlineData("audio", "5")]
    [InlineData("audio.sample_rate", "\"22050\"")]
    [InlineData("audio.sample_rate", "22050.5")]
    [InlineData("audio.sample_rate", "true")]
    [InlineData("audio.sample_rate", "[22050]")]
    [InlineData("audio.sample_rate", "{}")]
    [InlineData("audio.sample_rate", "null")]
    [InlineData("audio.sample_rate", "-22050")]
    [InlineData("num_speakers", "\"904\"")]
    [InlineData("num_speakers", "904.5")]
    [InlineData("num_speakers", "true")]
    [InlineData("num_speakers", "[904]")]
    [InlineData("num_speakers", "{}")]
    [InlineData("num_speakers", "null")]
    [InlineData("num_speakers", "0")]
    [InlineData("num_speakers", "-3")]
    [InlineData("inference", "[]")]
    [InlineData("inference.noise_scale", "\"0.3\"")]
    [InlineData("inference.noise_scale", "true")]
    [InlineData("inference.noise_scale", "[0.3]")]
    [InlineData("inference.noise_scale", "{}")]
    [InlineData("inference.noise_scale", "null")]
    [InlineData("inference.noise_scale", "-0.1")]
    [InlineData("inference.length_scale", "\"1\"")]
    [InlineData("inference.length_scale", "null")]
    [InlineData("inference.length_scale", "0")]
    [InlineData("inference.length_scale", "-1")]
    [InlineData("inference.noise_w", "\"0.3\"")]
    [InlineData("inference.noise_w", "false")]
    [InlineData("inference.noise_w", "[]")]
    [InlineData("espeak", "\"en-us\"")]
    [InlineData("espeak.voice", "1")]
    [InlineData("espeak.voice", "true")]
    [InlineData("espeak.voice", """["en-us"]""")]
    [InlineData("espeak.voice", "{}")]
    [InlineData("espeak.voice", "null")]
    [InlineData("phoneme_id_map.a", "5")]
    [InlineData("phoneme_id_map.a", "\"5\"")]
    [InlineData("phoneme_id_map.a", "null")]
    [InlineData("phoneme_id_map.a", "{}")]
    [InlineData("phoneme_id_map.a", "[]")]
    [InlineData("phoneme_id_map.a", """["5"]""")]
    [InlineData("phoneme_id_map.a", "[1.5]")]
    [InlineData("phoneme_id_map.a", "[-1]")]
    [InlineData("phoneme_id_map.a", "[null]")]
    [InlineData("phoneme_id_map.a", "[true]")]
    [InlineData("phoneme_id_map.a", "[[5]]")]
    public void PiperVoiceConfig_Parse_FieldOfTheWrongTypeOrRange_ThrowsNamingTheField(string field, string valueJson)
    {
        // Arrange
        var document = JsonNode.Parse(PiperTestFixtures.TrimmedConfigJson)!;
        var path = field.Split('.');
        var parent = document;
        foreach (var segment in path[..^1])
        {
            parent = parent[segment]!;
        }

        parent[path[^1]] = JsonNode.Parse(valueJson);

        // Act
        var error = Assert.Throws<InvalidDataException>(() => PiperVoiceConfig.Parse(document.ToJsonString()));

        // Assert
        Assert.Contains(path[^1], error.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves a symbol table with an empty symbol or a repeated symbol is rejected rather than silently keeping one of the entries.</summary>
    [Theory]
    [InlineData("""{ "audio": { "sample_rate": 22050 }, "phoneme_id_map": { "^": [1], "_": [0], "$": [2], "": [7] } }""", "empty symbol")]
    [InlineData("""{ "audio": { "sample_rate": 22050 }, "phoneme_id_map": { "^": [1], "_": [0], "$": [2], "a": [5], "a": [6] } }""", "more than once")]
    public void PiperVoiceConfig_Parse_EmptyOrRepeatedSymbol_ThrowsNamingTheProblem(string json, string expectedMessage)
    {
        // Act
        var error = Assert.Throws<InvalidDataException>(() => PiperVoiceConfig.Parse(json));

        // Assert
        Assert.Contains(expectedMessage, error.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves a configuration file is read from its path.</summary>
    [Fact]
    public void PiperVoiceConfig_Load_ConfigurationFile_ReadsIt()
    {
        // Act
        var config = PiperVoiceConfig.Load(PiperTestFixtures.TrimmedConfigPath);

        // Assert
        Assert.Equal(22050, config.SampleRate);
        Assert.Equal(904, config.NumSpeakers);
    }

    /// <summary>Proves null or empty arguments are rejected.</summary>
    [Fact]
    public void PiperVoiceConfig_ParseAndLoad_InvalidArguments_Throw()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => PiperVoiceConfig.Parse(null!));
        Assert.Throws<ArgumentException>(() => PiperVoiceConfig.Load(string.Empty));
    }
}
