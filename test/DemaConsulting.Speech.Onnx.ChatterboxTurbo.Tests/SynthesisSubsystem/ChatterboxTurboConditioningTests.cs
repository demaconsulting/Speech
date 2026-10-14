using DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="ChatterboxTurboConditioning"/>.
/// </summary>
public sealed class ChatterboxTurboConditioningTests
{
    /// <summary>Proves every supplied value and count is preserved and exposed unchanged.</summary>
    [Fact]
    public void ChatterboxTurboConditioning_Constructor_Values_ArePreserved()
    {
        // Arrange
        float[] embeddings = [0.5f, 0.25f];
        long[] prompt = [7, 8, 9];
        float[] speaker = [1f, 2f];
        float[] features = [3f, 4f, 5f, 6f];

        // Act
        var conditioning = new ChatterboxTurboConditioning(embeddings, 2, prompt, speaker, features, 2);

        // Assert
        Assert.Equal(embeddings, conditioning.ConditioningEmbeddings.ToArray());
        Assert.Equal(2, conditioning.ConditioningLength);
        Assert.Equal(prompt, conditioning.PromptTokens.ToArray());
        Assert.Equal(speaker, conditioning.SpeakerEmbeddings.ToArray());
        Assert.Equal(features, conditioning.SpeakerFeatures.ToArray());
        Assert.Equal(2, conditioning.SpeakerFeatureFrames);
    }

    /// <summary>Proves changing a source array after construction does not change the conditioning.</summary>
    [Fact]
    public void ChatterboxTurboConditioning_Constructor_SourceArraysMutatedAfterwards_ConditioningUnchanged()
    {
        // Arrange
        float[] embeddings = [0.5f, 0.25f];
        long[] prompt = [7, 8];
        float[] speaker = [1f, 2f];
        float[] features = [3f, 4f];
        var conditioning = new ChatterboxTurboConditioning(embeddings, 1, prompt, speaker, features, 1);

        // Act
        embeddings[0] = 99f;
        prompt[0] = 99;
        speaker[0] = 99f;
        features[0] = 99f;

        // Assert
        Assert.Equal([0.5f, 0.25f], conditioning.ConditioningEmbeddings.ToArray());
        Assert.Equal([7L, 8L], conditioning.PromptTokens.ToArray());
        Assert.Equal([1f, 2f], conditioning.SpeakerEmbeddings.ToArray());
        Assert.Equal([3f, 4f], conditioning.SpeakerFeatures.ToArray());
    }

    /// <summary>Proves the exposed properties are read-only views that cannot be written through.</summary>
    [Fact]
    public void ChatterboxTurboConditioning_Properties_AreReadOnlyViewsOfUnchangedData()
    {
        // Arrange
        var conditioning = new ChatterboxTurboConditioning([1f], 1, [2], [3f], [4f], 1);

        // Act: a copy taken from the view is changed
        var copy = conditioning.SpeakerEmbeddings.ToArray();
        copy[0] = 99f;

        // Assert: the conditioning itself still holds the original value on every read
        Assert.Equal(3f, conditioning.SpeakerEmbeddings.Span[0]);
        Assert.Equal(3f, conditioning.SpeakerEmbeddings.Span[0]);
        Assert.IsType<ReadOnlyMemory<float>>(conditioning.SpeakerEmbeddings);
    }

    /// <summary>Proves a null array argument is rejected.</summary>
    [Fact]
    public void ChatterboxTurboConditioning_Constructor_NullArray_ThrowsArgumentNullException()
    {
        // Arrange
        // (no setup needed)

        // Act
        var exceptions = new[]
        {
            Record.Exception(() => new ChatterboxTurboConditioning(null!, 1, [1], [1f], [1f], 1)),
            Record.Exception(() => new ChatterboxTurboConditioning([1f], 1, null!, [1f], [1f], 1)),
            Record.Exception(() => new ChatterboxTurboConditioning([1f], 1, [1], null!, [1f], 1)),
            Record.Exception(() => new ChatterboxTurboConditioning([1f], 1, [1], [1f], null!, 1)),
        };

        // Assert
        Assert.All(exceptions, exception => Assert.IsType<ArgumentNullException>(exception));
    }
}
