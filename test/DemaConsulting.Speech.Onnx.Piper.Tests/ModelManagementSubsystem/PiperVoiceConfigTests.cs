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
    public void Parse_RealVoiceConfiguration_ReadsSettingsAndSymbolTable()
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
    public void TryGetIds_Symbols_AreOrdinalAndCaseSensitive()
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
    public void Parse_MinimalConfiguration_UsesDefaults()
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
    public void Parse_MissingFramingSymbol_ThrowsNamingTheSymbol(string symbol)
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
    public void Parse_UnusableConfiguration_ThrowsInvalidData(string json)
    {
        // Act & Assert
        Assert.Throws<InvalidDataException>(() => PiperVoiceConfig.Parse(json));
    }

    /// <summary>Proves null arguments are rejected.</summary>
    [Fact]
    public void ParseAndLoad_InvalidArguments_Throw()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => PiperVoiceConfig.Parse(null!));
        Assert.Throws<ArgumentException>(() => PiperVoiceConfig.Load(string.Empty));
        Assert.Equal(22050, PiperVoiceConfig.Load(PiperTestFixtures.TrimmedConfigPath).SampleRate);
    }
}
