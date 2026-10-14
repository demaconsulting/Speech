using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="DefaultSynthesisBackendFactory"/>.
/// </summary>
public class DefaultSynthesisBackendFactoryTests
{
    /// <summary>
    ///     Proves that <see cref="DefaultSynthesisBackendFactory.Create"/> returns the backend the
    ///     model itself constructs.
    /// </summary>
    [Fact]
    public void DefaultSynthesisBackendFactory_Create_ValidArguments_ReturnsModelCreatedBackend()
    {
        // Arrange
        var factory = new DefaultSynthesisBackendFactory();

        // Act
        using var backend = factory.Create(new FakeSynthesisModel(), "model-directory");

        // Assert
        Assert.NotNull(backend);
        Assert.True(backend.SampleRate > 0);
    }

    /// <summary>
    ///     Proves that a null model and a null or empty directory are rejected.
    /// </summary>
    [Fact]
    public void DefaultSynthesisBackendFactory_Create_InvalidArguments_Throws()
    {
        // Arrange
        var factory = new DefaultSynthesisBackendFactory();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => factory.Create(null!, "model-directory"));
        Assert.Throws<ArgumentNullException>(() => factory.Create(new FakeSynthesisModel(), null!));
        Assert.Throws<ArgumentException>(() => factory.Create(new FakeSynthesisModel(), string.Empty));
    }
}
