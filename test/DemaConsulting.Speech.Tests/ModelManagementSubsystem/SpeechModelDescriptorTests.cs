using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for the <see cref="SpeechModelDescriptor"/> record.
/// </summary>
public class SpeechModelDescriptorTests
{
    /// <summary>
    ///     Proves that construction exposes the supplied model and state, and that the
    ///     convenience passthrough properties reflect the wrapped model.
    /// </summary>
    [Fact]
    public void SpeechModelDescriptor_Constructor_ValidModel_ExposesModelAndPassthroughProperties()
    {
        // Arrange
        var model = new FakeRecognitionModel("model-a");

        // Act
        var descriptor = new SpeechModelDescriptor(model, SpeechModelState.Downloaded);

        // Assert
        Assert.Same(model, descriptor.Model);
        Assert.Equal(SpeechModelState.Downloaded, descriptor.State);
        Assert.Equal(model.Id, descriptor.Id);
        Assert.Equal(model.DisplayName, descriptor.DisplayName);
        Assert.Equal(model.Role, descriptor.Role);
    }

    /// <summary>
    ///     Proves that a null model is rejected, since a descriptor with no underlying model
    ///     would have nothing to describe.
    /// </summary>
    [Fact]
    public void SpeechModelDescriptor_Constructor_NullModel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SpeechModelDescriptor(null!, SpeechModelState.NotDownloaded));
    }

    /// <summary>
    ///     Proves that two descriptors built with the same model and state are equal, and that a
    ///     changed state (simulating a store-state transition between two <c>Enumerate()</c>
    ///     calls) produces an unequal descriptor - since <see cref="SpeechModelDescriptor"/> is a
    ///     record, this proves state transitions are correctly reflected in a fresh snapshot.
    /// </summary>
    [Theory]
    [InlineData(SpeechModelState.NotDownloaded, SpeechModelState.Downloading)]
    [InlineData(SpeechModelState.Downloading, SpeechModelState.Downloaded)]
    [InlineData(SpeechModelState.Downloaded, SpeechModelState.FailedOrCorrupt)]
    public void SpeechModelDescriptor_StateTransition_DifferentState_ProducesUnequalDescriptor(
        SpeechModelState before,
        SpeechModelState after)
    {
        // Arrange
        var model = new FakeRecognitionModel("model-b");
        var beforeDescriptor = new SpeechModelDescriptor(model, before);

        // Act
        var afterDescriptor = new SpeechModelDescriptor(model, after);

        // Assert
        Assert.NotEqual(beforeDescriptor, afterDescriptor);
        Assert.Equal(before, beforeDescriptor.State);
        Assert.Equal(after, afterDescriptor.State);
    }
}
