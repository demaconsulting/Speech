using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for the <see cref="SpeechModelRole"/>, <see cref="SpeechModelState"/>, and
///     <see cref="SpeechModelAudioTagSupport"/> enums, proving each declares exactly the fixed
///     set of values architecture.md specifies.
/// </summary>
public class SpeechModelDescriptorEnumsTests
{
    /// <summary>
    ///     Proves that <see cref="SpeechModelRole"/> declares exactly Recognition and Synthesis.
    /// </summary>
    [Fact]
    public void SpeechModelRole_Values_DeclaresRecognitionAndSynthesisOnly()
    {
        // Arrange & Act
        var values = Enum.GetValues<SpeechModelRole>();

        // Assert
        Assert.Equal([SpeechModelRole.Recognition, SpeechModelRole.Synthesis], values);
    }

    /// <summary>
    ///     Proves that <see cref="SpeechModelState"/> declares exactly the fixed, small state set
    ///     architecture.md defines, with no "update available" state.
    /// </summary>
    [Fact]
    public void SpeechModelState_Values_DeclaresFixedStateSet()
    {
        // Arrange & Act
        var values = Enum.GetValues<SpeechModelState>();

        // Assert
        Assert.Equal(
        [
            SpeechModelState.NotDownloaded,
            SpeechModelState.Downloading,
            SpeechModelState.Downloaded,
            SpeechModelState.FailedOrCorrupt,
        ],
            values);
    }

    /// <summary>
    ///     Proves that <see cref="SpeechModelAudioTagSupport"/> declares exactly the three
    ///     declaration-shape values (rendering logic itself is deferred to Phase 4).
    /// </summary>
    [Fact]
    public void SpeechModelAudioTagSupport_Values_DeclaresNoneParameterMappedAndNative()
    {
        // Arrange & Act
        var values = Enum.GetValues<SpeechModelAudioTagSupport>();

        // Assert
        Assert.Equal(
            [SpeechModelAudioTagSupport.None, SpeechModelAudioTagSupport.ParameterMapped, SpeechModelAudioTagSupport.Native],
            values);
    }
}
