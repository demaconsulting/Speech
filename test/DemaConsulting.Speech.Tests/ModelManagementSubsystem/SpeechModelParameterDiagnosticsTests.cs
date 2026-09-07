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
    public void ValidateAndReport_NullSuppliedValues_DoesNothing()
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
    public void ValidateAndReport_EmptySuppliedValues_DoesNothing()
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
    public void ValidateAndReport_UnrecognizedParameterId_ReportsInfoAndDoesNotThrow()
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
    public void ValidateAndReport_ValidRecognizedNumericValue_DoesNothing()
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
    public void ValidateAndReport_NumericParameterWrongType_Throws()
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
    }

    /// <summary>
    ///     Proves that an out-of-range numeric value throws <see cref="ArgumentException"/>
    ///     naming the value and the declared range, rather than silently clamping.
    /// </summary>
    [Fact]
    public void ValidateAndReport_NumericParameterOutOfRange_Throws()
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
    }

    /// <summary>
    ///     Proves that a non-integral value for an integer-only <see cref="NumericParameter"/>
    ///     throws <see cref="ArgumentException"/> rather than being silently rounded.
    /// </summary>
    [Fact]
    public void ValidateAndReport_IntegerParameterFractionalValue_Throws()
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
    public void ValidateAndReport_IntegerParameterWholeNumberValue_DoesNotThrow()
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
    public void ValidateAndReport_ChoiceParameterInvalidOption_Throws()
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
    public void ValidateAndReport_ChoiceParameterWrongType_Throws()
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
    public void ValidateAndReport_ChoiceParameterValidOption_DoesNotThrow()
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
    public void ValidateAndReport_BooleanParameterWrongType_Throws()
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
    }

    /// <summary>
    ///     Proves that a valid boolean value neither throws nor reports.
    /// </summary>
    [Fact]
    public void ValidateAndReport_BooleanParameterValidValue_DoesNotThrow()
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
    public void ValidateAndReport_InvalidRecognizedValueAndUnrecognizedId_ThrowsForRecognizedValue()
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
        diagnostics.DidNotReceiveWithAnyArgs().Report(default, default!, default!);
    }

    /// <summary>Builds a small, reusable declared "tempo" <see cref="NumericParameter"/>.</summary>
    private static NumericParameter NumericTempoParameter() =>
        new("tempo", "Tempo", "Speaking rate multiplier.", new NumericParameterBounds(0.5, 2.0, 0.05, 1.0), "x");
}
