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

using DemaConsulting.Speech.Cli.Cli;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.SynthesisCommandSubsystem;

/// <summary>
///     Unit tests for <see cref="ParameterBagParser"/>: per-type parsing/validation of
///     <c>--param key=value</c> tokens against a model's declared parameters.
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
        var (key, value) = ParameterBagParser.ParseToken("rate=1.2");

        Assert.Equal("rate", key);
        Assert.Equal("1.2", value);
    }

    /// <summary>Test that a token with no '=' throws.</summary>
    [Fact]
    public void ParameterBagParser_ParseToken_NoSeparator_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ParameterBagParser.ParseToken("rate"));
    }

    /// <summary>Test that a token with an empty key throws.</summary>
    [Fact]
    public void ParameterBagParser_ParseToken_EmptyKey_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ParameterBagParser.ParseToken("=1.2"));
    }

    /// <summary>Test that a value with an empty half (trailing '=') is accepted as an empty value string.</summary>
    [Fact]
    public void ParameterBagParser_ParseToken_EmptyValueHalf_ReturnsEmptyValue()
    {
        var (key, value) = ParameterBagParser.ParseToken("rate=");

        Assert.Equal("rate", key);
        Assert.Equal(string.Empty, value);
    }

    /// <summary>Test that an in-range numeric value resolves to a boxed double.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_NumericInRange_ResolvesBoxedDouble()
    {
        var result = ParameterBagParser.Resolve([("rate", "1.5")], [RateParameter]);

        Assert.Equal(1.5, Assert.IsType<double>(result["rate"]));
    }

    /// <summary>Test that an out-of-range numeric value throws.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_NumericOutOfRange_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ParameterBagParser.Resolve([("rate", "5.0")], [RateParameter]));
    }

    /// <summary>Test that a malformed numeric value throws.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_NumericMalformed_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ParameterBagParser.Resolve([("rate", "not-a-number")], [RateParameter]));
    }

    /// <summary>Test that a fractional value for an integer-only parameter throws.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_IntegerParameterFractionalValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            ParameterBagParser.Resolve([("speakerIndex", "1.5")], [SpeakerIndexParameter]));
    }

    /// <summary>Test that a whole-number value for an integer-only parameter resolves.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_IntegerParameterWholeValue_ResolvesBoxedDouble()
    {
        var result = ParameterBagParser.Resolve([("speakerIndex", "3")], [SpeakerIndexParameter]);

        Assert.Equal(3.0, Assert.IsType<double>(result["speakerIndex"]));
    }

    /// <summary>Test that a valid choice value resolves to a boxed string.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_ValidChoiceValue_ResolvesBoxedString()
    {
        var result = ParameterBagParser.Resolve([("voice", "bob")], [VoiceParameter]);

        Assert.Equal("bob", Assert.IsType<string>(result["voice"]));
    }

    /// <summary>Test that an invalid choice value throws.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_InvalidChoiceValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ParameterBagParser.Resolve([("voice", "carol")], [VoiceParameter]));
    }

    /// <summary>Test that a valid boolean value resolves to a boxed bool.</summary>
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("True", true)]
    public void ParameterBagParser_Resolve_ValidBooleanValue_ResolvesBoxedBool(string input, bool expected)
    {
        var result = ParameterBagParser.Resolve([("denoise", input)], [DenoiseParameter]);

        Assert.Equal(expected, Assert.IsType<bool>(result["denoise"]));
    }

    /// <summary>Test that an invalid boolean value throws.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_InvalidBooleanValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ParameterBagParser.Resolve([("denoise", "maybe")], [DenoiseParameter]));
    }

    /// <summary>Test that an unrecognized key throws a clean ArgumentException naming the key.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_UnrecognizedKey_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            ParameterBagParser.Resolve([("bogus", "1")], [RateParameter]));

        Assert.Contains("bogus", exception.Message);
    }

    /// <summary>Test that an empty raw values list against declared parameters resolves an empty bag.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_NoRawValues_ReturnsEmptyBag()
    {
        var result = ParameterBagParser.Resolve([], [RateParameter]);

        Assert.Empty(result);
    }

    /// <summary>Test that a null raw values list is rejected.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_NullRawValues_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ParameterBagParser.Resolve(null!, [RateParameter]));
    }

    /// <summary>Test that a null declared parameters list is rejected.</summary>
    [Fact]
    public void ParameterBagParser_Resolve_NullDeclaredParameters_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ParameterBagParser.Resolve([], null!));
    }
}
