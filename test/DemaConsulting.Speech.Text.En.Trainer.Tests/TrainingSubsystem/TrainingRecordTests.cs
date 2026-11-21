using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingSubsystem;

/// <summary>
///     Unit tests for <see cref="TrainingRecord"/> and the settings records: the deterministic text form, its round
///     trip and the rejection of records that are malformed or out of range.
/// </summary>
public sealed class TrainingRecordTests
{
    /// <summary>A record with a value in every field.</summary>
    private static readonly TrainingRecord Sample = new(
        new TrainingSettings(7, 20, 64, 2, 3, 12, 1000, 20, RowVisitingOrder.Keyed),
        new EvaluationSettings([1UL, 2UL, 3UL], 5000, 95, 9, [new GateThreshold("holdout-natural", 9520), new GateThreshold("regression", 9603)]),
        new TrainingInputs("a1", "b2", "0.1.0-beta.5", 1, 2, 1, 100, 90, 4, 6, 50, 45, 2, 3),
        new TrainingResult(5, 422, 21.1f, 40, 42, 90, 100, 112887578, 12345, 99999, "c3"));

    /// <summary>Proves a record parses back to an equal record, including every setting and the temperature.</summary>
    [Fact]
    public void TrainingRecord_ToJsonThenParse_RoundTripsEveryField()
    {
        // Act
        var parsed = TrainingRecord.Parse(Sample.ToJson());

        // Assert
        Assert.Equal(Sample.Training, parsed.Training);
        Assert.Equal(Sample.Evaluation.Seeds, parsed.Evaluation.Seeds);
        Assert.True(Sample.Evaluation.SameValues(parsed.Evaluation));
        Assert.Equal(Sample.Evaluation.GateThresholds, parsed.Evaluation.GateThresholds);
        Assert.Equal(Sample.Inputs, parsed.Inputs);
        Assert.Equal(Sample.Result, parsed.Result);
    }

    /// <summary>Proves each row visiting order is written by name and read back to the same order.</summary>
    [Theory]
    [InlineData("positional")]
    [InlineData("keyed")]
    public void TrainingRecord_ToJsonThenParse_RowOrder_RoundTripsByName(string name)
    {
        // Arrange
        var order = name == "keyed" ? RowVisitingOrder.Keyed : RowVisitingOrder.Positional;
        var record = Sample with { Training = Sample.Training with { RowOrder = order } };

        // Act
        var json = record.ToJson();
        var parsed = TrainingRecord.Parse(json);

        // Assert
        Assert.Contains($"\"rowOrder\": \"{name}\"", json);
        Assert.Equal(order, parsed.Training.RowOrder);
    }

    /// <summary>Proves the text form is deterministic: the same record renders to the same text, with line feeds only and a final newline.</summary>
    [Fact]
    public void TrainingRecord_ToJson_IsDeterministicWithLineFeedsAndFinalNewline()
    {
        // Act
        var first = Sample.ToJson();
        var second = Sample.ToJson();

        // Assert
        Assert.Equal(first, second);
        Assert.DoesNotContain('\r', first);
        Assert.EndsWith("}\n", first, StringComparison.Ordinal);
    }

    /// <summary>Proves rendering does not depend on the current culture.</summary>
    [Fact]
    public void TrainingRecord_ToJson_UnderAnotherCulture_IsUnchanged()
    {
        // Arrange
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        var expected = Sample.ToJson();
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            // Act
            var actual = Sample.ToJson();

            // Assert
            Assert.Equal(expected, actual);
            Assert.Equal(Sample.Result.Temperature, TrainingRecord.Parse(actual).Result.Temperature);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>Proves text that is not a valid record is rejected with an InvalidDataException.</summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"schema\": 2}")]
    public void TrainingRecord_Parse_MalformedText_ThrowsInvalidDataException(string json)
    {
        // Act / Assert
        Assert.Throws<InvalidDataException>(() => TrainingRecord.Parse(json));
    }

    /// <summary>Proves a record with a missing property is rejected and the message names the property.</summary>
    [Fact]
    public void TrainingRecord_Parse_MissingProperty_ThrowsNamingTheProperty()
    {
        // Arrange
        var json = Sample.ToJson().Replace("\"imageSha256\"", "\"imageSha\"", StringComparison.Ordinal);

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => TrainingRecord.Parse(json));

        // Assert
        Assert.Contains("imageSha256", exception.Message);
    }

    /// <summary>Proves a setting outside its range is rejected when the record is parsed.</summary>
    [Theory]
    [InlineData("\"hashBits\": 20", "\"hashBits\": 99")]
    [InlineData("\"confidencePercent\": 95", "\"confidencePercent\": 5")]
    [InlineData("\"resamples\": 5000", "\"resamples\": 1")]
    [InlineData("\"rowOrder\": \"keyed\"", "\"rowOrder\": \"sorted\"")]
    [InlineData("\"minAccuracyBasisPoints\": 9520", "\"minAccuracyBasisPoints\": 0")]
    [InlineData("\"minAccuracyBasisPoints\": 9603", "\"minAccuracyBasisPoints\": 10001")]
    [InlineData("\"set\": \"regression\"", "\"set\": \"dev\"")]
    [InlineData("\"set\": \"regression\"", "\"set\": \"holdout-natural\"")]
    [InlineData("\"schema\": 2", "\"schema\": 1")]
    public void TrainingRecord_Parse_SettingOutOfRange_ThrowsInvalidDataException(string valid, string invalid)
    {
        // Arrange
        var json = Sample.ToJson().Replace(valid, invalid, StringComparison.Ordinal);
        Assert.NotEqual(Sample.ToJson(), json);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => TrainingRecord.Parse(json));
    }
}
