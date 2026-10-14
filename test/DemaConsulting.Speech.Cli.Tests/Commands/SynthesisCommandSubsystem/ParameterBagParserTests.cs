// Copyright (c) DEMA Consulting
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using DemaConsulting.Speech.Cli.Commands.SynthesisCommandSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.SynthesisCommandSubsystem;

/// <summary>
///     Unit tests for <see cref="ParameterBagParser"/>: per-type parsing/validation of
///     <c>--tts-param key=value</c>/<c>--stt-param key=value</c> tokens against a model's declared parameters.
/// </summary>
[Collection("Sequential")]
public sealed class ParameterBagParserTests
{
    private static readonly NumericParameter RateParameter = new(
        "rate", "Rate", "Speaking rate", new NumericParameterBounds(0.5, 2.0, 0.1, 1.0));

    private static readonly NumericParameter SpeakerIndexParameter = new(
        "speakerIndex", "Speaker index", "Discrete speaker index",
        new NumericParameterBounds(0, 10, 1, 0), isInteger: true);

    private static readonly ChoiceParameter VoiceParameter = new(
        "voice", "Voice", "Voice selection",
        [new ChoiceParameterOption("alice", "Alice"), new ChoiceParameterOption("bob", "Bob")],
        "alice");

    private static readonly BooleanParameter DenoiseParameter = new(
        "denoise", "Denoise", "Denoise input", false);

    /// <summary>Test that a well-formed token splits into key and value.</summary>
    [Fact]
    public void ParameterBagParser_ParseToken_WellFormedToken_SplitsKeyAndValue()
    {
        // Arrange
        const string token = "rate=1.2";

        // Act
        var (key, value) = ParameterBagParser.ParseToken(token, "--stt-param");

        // Assert
        Assert.Equal("rate", key);
        Assert.Equal("1.2", value);
    }

    /// <summary>Test that a token with no '=' throws.</summary>
    [Fact]
    public void ParameterBagParser_ParseToken_NoSeparator_ThrowsArgumentException()
    {
        // Arrange
        const string token = "rate";

        // Act
        var exception = Assert.Throws<ArgumentException>(() => ParameterBagParser.ParseToken(token, "--stt-param"));

        // Assert
        Assert.Contains("--stt-param", exception.Message);
        Assert.Contains("'rate'", exception.Message);
    }

    /// <summary>Test that a null token is rejected.</summary>
    [Fact]
    public void ParameterBagParser_ParseToken_NullToken_ThrowsArgumentNullException()
    {
        // Arrange
        string token = null!;

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ParameterBagParser.ParseToken(token, "--stt-param"));
    }

    /// <summary>Test that a null flag name is rejected.</summary>
    [Fact]
    public void ParameterBagParser_ParseToken_NullFlagName_ThrowsArgumentNullException()
    {
        // Arrange
        string flagName = null!;

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ParameterBagParser.ParseToken("rate=1.2", flagName));
    }

    /// <summary>Test that a token with an empty key throws.</summary>
    [Fact]
    public void ParameterBagParser_ParseToken_EmptyKey_ThrowsArgumentException()
    {
        // Arrange
        const string token = "=1.2";

        // Act
        var exception = Assert.Throws<ArgumentException>(() => ParameterBagParser.ParseToken(token, "--tts-param"));

        // Assert
        Assert.Contains("--tts-param", exception.Message);
        Assert.Contains("'=1.2'", exception.Message);
    }

    /// <summary>Test that a value with an empty half (trailing '=') is accepted as an empty value string.</summary>
    [Fact]
    public void ParameterBagParser_ParseToken_EmptyValueHalf_ReturnsEmptyValue()
    {
        // Arrange
        const string token = "rate=";

        // Act
        var (key, value) = ParameterBagParser.ParseToken(token, "--stt-param");

        // Assert
        Assert.Equal("rate", key);
        Assert.Equal(string.Empty, value);
    }

    /// <summary>Test that an in-range numeric value resolves to a boxed double.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_NumericInRange_ResolvesBoxedDouble()
    {
        // Arrange
        var raw = new[] { ("rate", "1.5") };

        // Act
        var result = ParameterBagParser.Resolve(raw, [RateParameter], "--stt-param");

        // Assert
        Assert.Equal(1.5, Assert.IsType<double>(result["rate"]));
    }

    /// <summary>Test that an out-of-range numeric value throws.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_NumericOutOfRange_ThrowsArgumentException()
    {
        // Arrange
        var raw = new[] { ("rate", "5.0") };

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            ParameterBagParser.Resolve(raw, [RateParameter], "--stt-param"));

        // Assert
        Assert.Contains("--stt-param", exception.Message);
        Assert.Contains("rate", exception.Message);
        Assert.Contains("'5.0'", exception.Message);
    }

    /// <summary>Test that a malformed numeric value throws.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_NumericMalformed_ThrowsArgumentException()
    {
        // Arrange
        var raw = new[] { ("rate", "not-a-number") };

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            ParameterBagParser.Resolve(raw, [RateParameter], "--stt-param"));

        // Assert
        Assert.Contains("not a valid number", exception.Message);
        Assert.Contains("'not-a-number'", exception.Message);
    }

    /// <summary>
    ///     Regression test: non-finite numbers (NaN, Infinity, -Infinity) must be rejected as
    ///     malformed numbers rather than slipping through the range comparison (NaN compares false
    ///     against every bound) or the integer check.
    /// </summary>
    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void ParameterBagParser_Resolve_NumericNonFinite_ThrowsArgumentException(string value)
    {
        // Arrange
        var raw = new[] { ("rate", value) };

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            ParameterBagParser.Resolve(raw, [RateParameter], "--tts-param"));

        // Assert
        Assert.Contains("--tts-param", exception.Message);
        Assert.Contains($"'{value}'", exception.Message);
    }

    /// <summary>Regression test: non-finite numbers are also rejected for integer-only parameters.</summary>
    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void ParameterBagParser_Resolve_IntegerParameterNonFinite_ThrowsArgumentException(string value)
    {
        // Arrange
        var raw = new[] { ("speakerIndex", value) };

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            ParameterBagParser.Resolve(raw, [SpeakerIndexParameter], "--stt-param"));

        // Assert
        Assert.Contains("not a valid number", exception.Message);
    }

    /// <summary>Test that a fractional value for an integer-only parameter throws.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_IntegerParameterFractionalValue_ThrowsArgumentException()
    {
        // Arrange
        var raw = new[] { ("speakerIndex", "1.5") };

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            ParameterBagParser.Resolve(raw, [SpeakerIndexParameter], "--stt-param"));

        // Assert
        Assert.Contains("whole number", exception.Message);
        Assert.Contains("'1.5'", exception.Message);
    }

    /// <summary>Test that a whole-number value for an integer-only parameter resolves.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_IntegerParameterWholeValue_ResolvesBoxedDouble()
    {
        // Arrange
        var raw = new[] { ("speakerIndex", "3") };

        // Act
        var result = ParameterBagParser.Resolve(raw, [SpeakerIndexParameter], "--stt-param");

        // Assert
        Assert.Equal(3.0, Assert.IsType<double>(result["speakerIndex"]));
    }

    /// <summary>Test that a valid choice value resolves to a boxed string.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_ValidChoiceValue_ResolvesBoxedString()
    {
        // Arrange
        var raw = new[] { ("voice", "bob") };

        // Act
        var result = ParameterBagParser.Resolve(raw, [VoiceParameter], "--stt-param");

        // Assert
        Assert.Equal("bob", Assert.IsType<string>(result["voice"]));
    }

    /// <summary>Test that an invalid choice value throws.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_InvalidChoiceValue_ThrowsArgumentException()
    {
        // Arrange
        var raw = new[] { ("voice", "carol") };

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            ParameterBagParser.Resolve(raw, [VoiceParameter], "--stt-param"));

        // Assert
        Assert.Contains("does not match any declared option", exception.Message);
        Assert.Contains("'carol'", exception.Message);
    }

    /// <summary>Test that a valid boolean value resolves to a boxed bool.</summary>
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("True", true)]
    public void ParameterBagParser_Resolve_ValidBooleanValue_ResolvesBoxedBool(string input, bool expected)
    {
        // Arrange
        var raw = new[] { ("denoise", input) };

        // Act
        var result = ParameterBagParser.Resolve(raw, [DenoiseParameter], "--stt-param");

        // Assert
        Assert.Equal(expected, Assert.IsType<bool>(result["denoise"]));
    }

    /// <summary>Test that an invalid boolean value throws.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_InvalidBooleanValue_ThrowsArgumentException()
    {
        // Arrange
        var raw = new[] { ("denoise", "maybe") };

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            ParameterBagParser.Resolve(raw, [DenoiseParameter], "--stt-param"));

        // Assert
        Assert.Contains("--stt-param", exception.Message);
        Assert.Contains("'maybe'", exception.Message);
    }

    /// <summary>Test that a declared parameter of an unsupported kind is rejected, naming flag, id and value.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_UnrecognizedParameterKind_ThrowsArgumentExceptionNamingValue()
    {
        // Arrange
        var raw = new[] { ("custom", "xyz") };
        var declared = new ISpeechModelParameter[] { new UnsupportedKindParameter() };

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            ParameterBagParser.Resolve(raw, declared, "--tts-param"));

        // Assert
        Assert.Contains("--tts-param", exception.Message);
        Assert.Contains("'custom'", exception.Message);
        Assert.Contains("'xyz'", exception.Message);
    }

    /// <summary>Test that an unrecognized key throws a clean ArgumentException naming the key.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_UnrecognizedKey_ThrowsArgumentException()
    {
        // Arrange
        var raw = new[] { ("bogus", "1") };

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            ParameterBagParser.Resolve(raw, [RateParameter], "--stt-param"));

        // Assert
        Assert.Contains("bogus", exception.Message);
        Assert.Contains("--stt-param", exception.Message);
    }

    /// <summary>Test that an empty raw values list against declared parameters resolves an empty bag.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_NoRawValues_ReturnsEmptyBag()
    {
        // Arrange
        var raw = Array.Empty<(string Key, string Value)>();

        // Act
        var result = ParameterBagParser.Resolve(raw, [RateParameter], "--stt-param");

        // Assert
        Assert.Empty(result);
    }

    /// <summary>Test that a null raw values list is rejected.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_NullRawValues_ThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<(string Key, string Value)> raw = null!;

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ParameterBagParser.Resolve(raw, [RateParameter], "--stt-param"));
    }

    /// <summary>Test that a null declared parameters list is rejected.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_NullDeclaredParameters_ThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<ISpeechModelParameter> declared = null!;

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ParameterBagParser.Resolve([], declared, "--stt-param"));
    }

    /// <summary>Test that a null flag name is rejected.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_NullFlagName_ThrowsArgumentNullException()
    {
        // Arrange
        string flagName = null!;

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ParameterBagParser.Resolve([], [RateParameter], flagName));
    }

    /// <summary>A parameter of a kind the parser does not support.</summary>
    private sealed class UnsupportedKindParameter : ISpeechModelParameter
    {
        /// <inheritdoc/>
        public string Id => "custom";

        /// <inheritdoc/>
        public string DisplayName => "Custom";

        /// <inheritdoc/>
        public string Description => "Unsupported kind";
    }
}
