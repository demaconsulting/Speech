using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;
using NSubstitute;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for <see cref="SpeechModelParameterDiagnostics"/>, proving the deliberate split
///     between an unrecognized parameter id (reported, never thrown) and an invalid value for a
///     recognized parameter (thrown as <see cref="ArgumentException"/>).
/// </summary>
public sealed class SpeechModelParameterDiagnosticsTests
{
    private const string ModelId = "fake-model";
    private const string Category = "TestCategory";

    /// <summary>
    ///     Proves that a <see langword="null"/> supplied bag is a complete no-op: no diagnostic
    ///     is reported and nothing throws.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_NullSuppliedValues_DoesNothing()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameters = new List<ISpeechModelParameter> { NumericTempoParameter() };

        // Act
        SpeechModelParameterDiagnostics.ValidateAndReport(ModelId, parameters, null, diagnostics, Category);

        // Assert
        diagnostics.DidNotReceiveWithAnyArgs().Report(default, default!, default!);
    }

    /// <summary>
    ///     Proves that an empty supplied bag is a complete no-op.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_EmptySuppliedValues_DoesNothing()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameters = new List<ISpeechModelParameter> { NumericTempoParameter() };

        // Act
        SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, new Dictionary<string, object>(), diagnostics, Category);

        // Assert
        diagnostics.DidNotReceiveWithAnyArgs().Report(default, default!, default!);
    }

    /// <summary>
    ///     Proves that an unrecognized parameter id is reported at
    ///     <see cref="SpeechDiagnosticLevel.Info"/> and never throws, preserving the deliberate
    ///     cross-model-compatibility contract.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_UnrecognizedParameterId_ReportsInfoAndDoesNotThrow()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameters = new List<ISpeechModelParameter> { NumericTempoParameter() };
        var suppliedValues = new Dictionary<string, object> { ["unknown-id"] = 1.0 };

        // Act
        var exception = Record.Exception(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Null(exception);
        diagnostics.Received(1).Report(
            SpeechDiagnosticLevel.Info,
            Category,
            "Parameter 'unknown-id' is not declared by this model and was ignored.");
    }

    /// <summary>
    ///     Proves that a valid recognized value neither throws nor reports any diagnostic.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_ValidRecognizedNumericValue_DoesNothing()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameters = new List<ISpeechModelParameter> { NumericTempoParameter() };
        var suppliedValues = new Dictionary<string, object> { ["tempo"] = 1.5 };

        // Act
        SpeechModelParameterDiagnostics.ValidateAndReport(ModelId, parameters, suppliedValues, diagnostics, Category);

        // Assert
        diagnostics.DidNotReceiveWithAnyArgs().Report(default, default!, default!);
    }

    /// <summary>
    ///     Proves that a wrong CLR type for a <see cref="NumericParameter"/> throws
    ///     <see cref="ArgumentException"/> naming the parameter, the supplied type, and the model.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_NumericParameterWrongType_Throws()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameters = new List<ISpeechModelParameter> { NumericTempoParameter() };
        var suppliedValues = new Dictionary<string, object> { ["tempo"] = "fast" };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Contains("tempo", exception.Message, StringComparison.Ordinal);
        Assert.Contains("System.String", exception.Message, StringComparison.Ordinal);
        Assert.Contains(ModelId, exception.Message, StringComparison.Ordinal);
        Assert.Equal("parameterValues", exception.ParamName);
    }

    /// <summary>
    ///     Proves that an out-of-range numeric value throws <see cref="ArgumentException"/>
    ///     naming the value and the declared range, rather than silently clamping.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_NumericParameterOutOfRange_Throws()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameter = new NumericParameter(
            "volume", "Volume", "Playback volume.", new NumericParameterBounds(0, 100, 1, 50));
        var parameters = new List<ISpeechModelParameter> { parameter };
        var suppliedValues = new Dictionary<string, object> { ["volume"] = 150.0 };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Contains("volume", exception.Message, StringComparison.Ordinal);
        Assert.Contains("[0, 100]", exception.Message, StringComparison.Ordinal);
        Assert.Contains(ModelId, exception.Message, StringComparison.Ordinal);
        Assert.Equal("parameterValues", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a non-integral value for an integer-only <see cref="NumericParameter"/>
    ///     throws <see cref="ArgumentException"/> rather than being silently rounded.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_IntegerParameterFractionalValue_Throws()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameter = new NumericParameter(
            "pitch", "Pitch", "Discrete pitch step.", new NumericParameterBounds(0, 24, 1, 0), isInteger: true);
        var parameters = new List<ISpeechModelParameter> { parameter };
        var suppliedValues = new Dictionary<string, object> { ["pitch"] = 12.4 };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Contains("pitch", exception.Message, StringComparison.Ordinal);
        Assert.Contains("12.4", exception.Message, StringComparison.Ordinal);
        Assert.Contains("whole number", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a whole-number value (including one supplied as a boxed
    ///     <see langword="double"/> with no fractional part) is accepted for an integer-only
    ///     parameter, and does not throw.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_IntegerParameterWholeNumberValue_DoesNotThrow()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameter = new NumericParameter(
            "pitch", "Pitch", "Discrete pitch step.", new NumericParameterBounds(0, 24, 1, 0), isInteger: true);
        var parameters = new List<ISpeechModelParameter> { parameter };
        var suppliedValues = new Dictionary<string, object> { ["pitch"] = 12.0 };

        // Act
        var exception = Record.Exception(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that a value not matching any declared <see cref="ChoiceParameterOption.Value"/>
    ///     throws <see cref="ArgumentException"/> naming the expected options.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_ChoiceParameterInvalidOption_Throws()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameter = new ChoiceParameter(
            "speed",
            "Speed",
            "Speaking speed.",
            [
                new ChoiceParameterOption("slow", "Slow"),
                new ChoiceParameterOption("normal", "Normal"),
                new ChoiceParameterOption("fast", "Fast"),
            ],
            "normal");
        var parameters = new List<ISpeechModelParameter> { parameter };
        var suppliedValues = new Dictionary<string, object> { ["speed"] = "ludicrous" };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Contains("speed", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ludicrous", exception.Message, StringComparison.Ordinal);
        Assert.Contains("slow, normal, fast", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a non-string value supplied for a <see cref="ChoiceParameter"/> throws
    ///     <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_ChoiceParameterWrongType_Throws()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameter = new ChoiceParameter(
            "speed",
            "Speed",
            "Speaking speed.",
            [new ChoiceParameterOption("normal", "Normal")],
            "normal");
        var parameters = new List<ISpeechModelParameter> { parameter };
        var suppliedValues = new Dictionary<string, object> { ["speed"] = 1.5 };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Contains("speed", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a valid choice value neither throws nor reports.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_ChoiceParameterValidOption_DoesNotThrow()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameter = new ChoiceParameter(
            "speed",
            "Speed",
            "Speaking speed.",
            [
                new ChoiceParameterOption("slow", "Slow"),
                new ChoiceParameterOption("fast", "Fast"),
            ],
            "slow");
        var parameters = new List<ISpeechModelParameter> { parameter };
        var suppliedValues = new Dictionary<string, object> { ["speed"] = "fast" };

        // Act
        var exception = Record.Exception(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that a non-<see langword="bool"/> value for a <see cref="BooleanParameter"/>
    ///     throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_BooleanParameterWrongType_Throws()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameter = new BooleanParameter("denoise", "Denoise", "Denoise input audio.", false);
        var parameters = new List<ISpeechModelParameter> { parameter };
        var suppliedValues = new Dictionary<string, object> { ["denoise"] = "yes" };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Contains("denoise", exception.Message, StringComparison.Ordinal);
        Assert.Contains(ModelId, exception.Message, StringComparison.Ordinal);
        Assert.Equal("parameterValues", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a valid boolean value neither throws nor reports.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_BooleanParameterValidValue_DoesNotThrow()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameter = new BooleanParameter("denoise", "Denoise", "Denoise input audio.", false);
        var parameters = new List<ISpeechModelParameter> { parameter };
        var suppliedValues = new Dictionary<string, object> { ["denoise"] = true };

        // Act
        var exception = Record.Exception(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that an invalid recognized value is detected (and throws) even alongside an
    ///     unrelated unrecognized key in the same supplied bag, and that the recognized-value
    ///     check runs before the unrecognized-id report, per the declared-order determinism
    ///     contract.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_InvalidRecognizedValueAndUnrecognizedId_ThrowsForRecognizedValue()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameters = new List<ISpeechModelParameter> { NumericTempoParameter() };
        var suppliedValues = new Dictionary<string, object>
        {
            ["unknown-id"] = "whatever",
            ["tempo"] = "not-a-number",
        };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Contains("tempo", exception.Message, StringComparison.Ordinal);
        Assert.Contains(ModelId, exception.Message, StringComparison.Ordinal);
        Assert.Equal("parameterValues", exception.ParamName);
        diagnostics.DidNotReceiveWithAnyArgs().Report(default, default!, default!);
    }

    /// <summary>
    ///     Proves that a <see langword="null"/> value supplied for a recognized
    ///     <see cref="NumericParameter"/> throws <see cref="ArgumentException"/> (not
    ///     <see cref="NullReferenceException"/>), describing the received value as "null" rather
    ///     than calling <see cref="object.GetType"/> on it.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_NumericParameterNullValue_ThrowsArgumentException()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameters = new List<ISpeechModelParameter> { NumericTempoParameter() };
        var suppliedValues = new Dictionary<string, object> { ["tempo"] = null! };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Contains("tempo", exception.Message, StringComparison.Ordinal);
        Assert.Contains("null", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a <see langword="null"/> value supplied for a recognized
    ///     <see cref="ChoiceParameter"/> throws <see cref="ArgumentException"/> rather than
    ///     <see cref="NullReferenceException"/>.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_ChoiceParameterNullValue_ThrowsArgumentException()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameter = new ChoiceParameter(
            "speed",
            "Speed",
            "Speaking speed.",
            [new ChoiceParameterOption("normal", "Normal")],
            "normal");
        var parameters = new List<ISpeechModelParameter> { parameter };
        var suppliedValues = new Dictionary<string, object> { ["speed"] = null! };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Contains("speed", exception.Message, StringComparison.Ordinal);
        Assert.Contains("null", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a <see langword="null"/> value supplied for a recognized
    ///     <see cref="BooleanParameter"/> throws <see cref="ArgumentException"/> rather than
    ///     <see cref="NullReferenceException"/>.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_BooleanParameterNullValue_ThrowsArgumentException()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameter = new BooleanParameter("denoise", "Denoise", "Denoise input audio.", false);
        var parameters = new List<ISpeechModelParameter> { parameter };
        var suppliedValues = new Dictionary<string, object> { ["denoise"] = null! };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Contains("denoise", exception.Message, StringComparison.Ordinal);
        Assert.Contains("null", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a value supplied for a recognized parameter whose descriptor is neither
    ///     <see cref="NumericParameter"/>, <see cref="ChoiceParameter"/>, nor
    ///     <see cref="BooleanParameter"/> throws <see cref="ArgumentException"/> rather than
    ///     silently bypassing validation - <see cref="ISpeechModelParameter"/> is public, so a
    ///     custom model could otherwise supply an unrecognized descriptor subtype.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_UnsupportedParameterDescriptorType_Throws()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameter = new FakeSpeechModelParameter("custom");
        var parameters = new List<ISpeechModelParameter> { parameter };
        var suppliedValues = new Dictionary<string, object> { ["custom"] = "anything" };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Contains("custom", exception.Message, StringComparison.Ordinal);
        Assert.Contains(ModelId, exception.Message, StringComparison.Ordinal);
        Assert.Equal("parameterValues", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a NaN or infinite numeric value is rejected with a message that reports
    ///     it as non-finite rather than as a wrong type.
    /// </summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_NumericParameterNonFiniteValue_ThrowsFiniteMessage(double value)
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameters = new List<ISpeechModelParameter> { NumericTempoParameter() };
        var suppliedValues = new Dictionary<string, object> { ["tempo"] = value };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Contains("finite", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Double", exception.Message, StringComparison.Ordinal);
        Assert.Equal("parameterValues", exception.ParamName);
    }

    /// <summary>
    ///     Proves that <see langword="int"/> and <see langword="float"/> values are accepted for
    ///     a <see cref="NumericParameter"/>.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_NumericParameterIntAndFloatValues_DoNotThrow()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameter = new NumericParameter(
            "volume", "Volume", "Playback volume.", new NumericParameterBounds(0, 100, 1, 50));
        var parameters = new List<ISpeechModelParameter> { parameter };

        // Act
        var intException = Record.Exception(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, new Dictionary<string, object> { ["volume"] = 10 }, diagnostics, Category));
        var floatException = Record.Exception(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, new Dictionary<string, object> { ["volume"] = 10.5f }, diagnostics, Category));

        // Assert
        Assert.Null(intException);
        Assert.Null(floatException);
        diagnostics.DidNotReceiveWithAnyArgs().Report(default, default!, default!);
    }

    /// <summary>
    ///     Proves that values exactly at Minimum and Maximum are accepted (inclusive range).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(100.0)]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_NumericParameterBoundaryValue_DoesNotThrow(double value)
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameter = new NumericParameter(
            "volume", "Volume", "Playback volume.", new NumericParameterBounds(0, 100, 1, 50));
        var parameters = new List<ISpeechModelParameter> { parameter };
        var suppliedValues = new Dictionary<string, object> { ["volume"] = value };

        // Act
        var exception = Record.Exception(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that with several invalid values the first one in declared-parameter order is
    ///     the one reported, regardless of the supplied bag's order.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_MultipleInvalidValues_ThrowsForFirstDeclaredParameter()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameters = new List<ISpeechModelParameter>
        {
            new BooleanParameter("first", "First", "First flag.", false),
            new BooleanParameter("second", "Second", "Second flag.", false),
        };
        var suppliedValues = new Dictionary<string, object>
        {
            ["second"] = "bad",
            ["first"] = "bad",
        };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Contains("'first'", exception.Message, StringComparison.Ordinal);
        Assert.Contains(ModelId, exception.Message, StringComparison.Ordinal);
        Assert.Equal("parameterValues", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a wrong-type choice value message names the received type.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_ChoiceParameterWrongType_MessageNamesReceivedType()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameter = new ChoiceParameter(
            "speed", "Speed", "Speaking speed.", [new ChoiceParameterOption("normal", "Normal")], "normal");
        var parameters = new List<ISpeechModelParameter> { parameter };
        var suppliedValues = new Dictionary<string, object> { ["speed"] = 1.5 };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Contains("System.Double", exception.Message, StringComparison.Ordinal);
        Assert.Equal("parameterValues", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a numeric value below the declared minimum throws <see cref="ArgumentException"/>
    ///     naming the declared range, rather than being clamped.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_NumericParameterBelowMinimum_Throws()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameters = new List<ISpeechModelParameter> { NumericTempoParameter() };
        var suppliedValues = new Dictionary<string, object> { ["tempo"] = 0.1 };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Contains("tempo", exception.Message, StringComparison.Ordinal);
        Assert.Contains("[0.5, 2]", exception.Message, StringComparison.Ordinal);
        Assert.Equal("parameterValues", exception.ParamName);
    }

    /// <summary>
    ///     Proves that every unrecognized key is reported as its own Info diagnostic and none throws.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_MultipleUnrecognizedParameterIds_ReportsInfoForEach()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameters = new List<ISpeechModelParameter> { NumericTempoParameter() };
        var suppliedValues = new Dictionary<string, object> { ["unknown-a"] = 1.0, ["unknown-b"] = "x" };

        // Act
        SpeechModelParameterDiagnostics.ValidateAndReport(ModelId, parameters, suppliedValues, diagnostics, Category);

        // Assert
        diagnostics.Received(1).Report(
            SpeechDiagnosticLevel.Info, Category, "Parameter 'unknown-a' is not declared by this model and was ignored.");
        diagnostics.Received(1).Report(
            SpeechDiagnosticLevel.Info, Category, "Parameter 'unknown-b' is not declared by this model and was ignored.");
    }

    /// <summary>
    ///     Proves that a null element in the declared-parameter list is reported as an
    ///     <see cref="ArgumentException"/> naming <c>declaredParameters</c>, not a <see cref="NullReferenceException"/>.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_NullDeclaredParameterElement_ThrowsArgumentException()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameters = new List<ISpeechModelParameter> { NumericTempoParameter(), null! };
        var suppliedValues = new Dictionary<string, object> { ["tempo"] = 1.0 };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, suppliedValues, diagnostics, Category));

        // Assert
        Assert.Equal("declaredParameters", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a null model id throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_NullModelId_ThrowsArgumentNullException()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameters = new List<ISpeechModelParameter> { NumericTempoParameter() };

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            null!, parameters, null, diagnostics, Category));

        // Assert
        Assert.Equal("modelId", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a null declared-parameter list throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_NullDeclaredParameters_ThrowsArgumentNullException()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, null!, null, diagnostics, Category));

        // Assert
        Assert.Equal("declaredParameters", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a null diagnostics sink throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_NullDiagnostics_ThrowsArgumentNullException()
    {
        // Arrange
        var parameters = new List<ISpeechModelParameter> { NumericTempoParameter() };

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, null, null!, Category));

        // Assert
        Assert.Equal("diagnostics", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a null category throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void SpeechModelParameterDiagnostics_ValidateAndReport_NullCategory_ThrowsArgumentNullException()
    {
        // Arrange
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var parameters = new List<ISpeechModelParameter> { NumericTempoParameter() };

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => SpeechModelParameterDiagnostics.ValidateAndReport(
            ModelId, parameters, null, diagnostics, null!));

        // Assert
        Assert.Equal("category", exception.ParamName);
    }

    /// <summary>Builds a small, reusable declared "tempo" <see cref="NumericParameter"/>.</summary>
    private static NumericParameter NumericTempoParameter() =>
        new("tempo", "Tempo", "Speaking rate multiplier.", new NumericParameterBounds(0.5, 2.0, 0.05, 1.0), "x");

    /// <summary>
    ///     A minimal <see cref="ISpeechModelParameter"/> implementation representing a
    ///     hypothetical descriptor subtype not known to <see cref="SpeechModelParameterDiagnostics"/>.
    /// </summary>
    private sealed class FakeSpeechModelParameter(string id) : ISpeechModelParameter
    {
        public string Id { get; } = id;

        public string DisplayName => "Custom";

        public string Description => string.Empty;
    }
}
