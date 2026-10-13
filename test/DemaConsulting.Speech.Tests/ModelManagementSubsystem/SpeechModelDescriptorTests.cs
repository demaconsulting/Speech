using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;
using NSubstitute;

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
        Assert.Equal(((ISpeechModel)model).LicenseName, descriptor.LicenseName);
        Assert.Equal(((ISpeechModel)model).LicenseUrl, descriptor.LicenseUrl);
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
    ///     Proves that the license passthrough properties mirror a model that declares a non-default license name and
    ///     URL, so a descriptor returning constants would fail.
    /// </summary>
    [Fact]
    public void SpeechModelDescriptor_Passthrough_NonDefaultLicense_MirrorsModel()
    {
        // Arrange
        var model = Substitute.For<ISpeechModel>();
        model.Id.Returns("licensed");
        model.LicenseName.Returns("MIT");
        model.LicenseUrl.Returns(new Uri("https://example.invalid/license"));

        // Act
        var descriptor = new SpeechModelDescriptor(model, SpeechModelState.Downloaded);

        // Assert
        Assert.Equal("MIT", descriptor.LicenseName);
        Assert.Equal(new Uri("https://example.invalid/license"), descriptor.LicenseUrl);
    }

    /// <summary>
    ///     Proves that the identity passthrough properties mirror a model with non-default values and are read live
    ///     from the wrapped model on every access (no caching).
    /// </summary>
    [Fact]
    public void SpeechModelDescriptor_Passthrough_NonDefaultIdentity_MirrorsModelLive()
    {
        // Arrange
        var model = Substitute.For<ISpeechModel>();
        model.Id.Returns("first-id");
        model.DisplayName.Returns("First Name");
        model.Role.Returns(SpeechModelRole.Synthesis);
        var descriptor = new SpeechModelDescriptor(model, SpeechModelState.NotDownloaded);

        // Act
        var before = (descriptor.Id, descriptor.DisplayName, descriptor.Role);
        model.Id.Returns("second-id");
        model.DisplayName.Returns("Second Name");
        model.Role.Returns(SpeechModelRole.Recognition);

        // Assert
        Assert.Equal(("first-id", "First Name", SpeechModelRole.Synthesis), before);
        Assert.Equal("second-id", descriptor.Id);
        Assert.Equal("Second Name", descriptor.DisplayName);
        Assert.Equal(SpeechModelRole.Recognition, descriptor.Role);
    }

    /// <summary>
    ///     Proves that two descriptors built with the same model instance and state are equal (reference equality on
    ///     <c>Model</c>, value equality on <c>State</c>).
    /// </summary>
    [Fact]
    public void SpeechModelDescriptor_SameModelAndState_AreEqual()
    {
        // Arrange
        var model = new FakeRecognitionModel("model-c");

        // Act
        var first = new SpeechModelDescriptor(model, SpeechModelState.Downloaded);
        var second = new SpeechModelDescriptor(model, SpeechModelState.Downloaded);

        // Assert
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    /// <summary>
    ///     Proves that a changed state (simulating a store-state transition between two <c>Enumerate()</c>
    ///     calls) produces an unequal descriptor, so state transitions are correctly reflected in a fresh snapshot.
    /// </summary>
    /// <param name="before">The state of the first descriptor.</param>
    /// <param name="after">The state of the second descriptor.</param>
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

    /// <summary>
    ///     Proves that two descriptors with the same state but different model instances are not equal, because
    ///     equality is defined on model identity.
    /// </summary>
    [Fact]
    public void SpeechModelDescriptor_DifferentModelInstance_SameState_AreNotEqual()
    {
        // Arrange
        var first = new SpeechModelDescriptor(new FakeRecognitionModel("model-d"), SpeechModelState.Downloaded);

        // Act
        var second = new SpeechModelDescriptor(new FakeRecognitionModel("model-d"), SpeechModelState.Downloaded);

        // Assert
        Assert.NotEqual(first, second);
    }

    /// <summary>
    ///     Proves that the descriptor offers no way to change its model or state after construction.
    /// </summary>
    [Fact]
    public void SpeechModelDescriptor_Snapshot_PublicProperties_HaveNoSetters()
    {
        // Arrange
        var properties = typeof(SpeechModelDescriptor).GetProperties();

        // Act
        var writable = properties.Where(property => property.GetSetMethod(nonPublic: false) is not null).ToArray();

        // Assert
        Assert.NotEmpty(properties);
        Assert.Empty(writable);
    }

    /// <summary>
    ///     Proves that the state a descriptor was built with stays the observed value, and the model reference
    ///     stays the same instance, even when the wrapped model's own values change afterwards.
    /// </summary>
    [Fact]
    public void SpeechModelDescriptor_Snapshot_ModelChangesLater_StateRemainsObservedValue()
    {
        // Arrange
        var model = Substitute.For<ISpeechModel>();
        model.Id.Returns("first-id");
        var descriptor = new SpeechModelDescriptor(model, SpeechModelState.Downloaded);

        // Act
        model.Id.Returns("second-id");

        // Assert
        Assert.Equal(SpeechModelState.Downloaded, descriptor.State);
        Assert.Same(model, descriptor.Model);
    }
}
