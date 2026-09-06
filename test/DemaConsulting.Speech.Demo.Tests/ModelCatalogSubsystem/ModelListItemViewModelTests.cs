using DemaConsulting.Speech.Demo.ModelCatalogSubsystem;
using DemaConsulting.Speech.Demo.Tests.Fakes;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo.Tests.ModelCatalogSubsystem;

/// <summary>
///     Unit tests for <see cref="ModelListItemViewModel"/>.
/// </summary>
public class ModelListItemViewModelTests
{
    /// <summary>
    ///     Proves that a row rejects a missing descriptor rather than presenting an unidentified
    ///     model.
    /// </summary>
    [Fact]
    public void ModelListItemViewModel_Constructor_NullDescriptor_ThrowsArgumentNullException()
    {
        // Act & Assert: building a row without a descriptor is a programming error
        Assert.Throws<ArgumentNullException>(() => new ModelListItemViewModel(null!));
    }

    /// <summary>
    ///     Proves that a row carries the descriptor's identity, role, and state unchanged.
    /// </summary>
    [Fact]
    public void ModelListItemViewModel_Constructor_Descriptor_CarriesDescriptorIdentityAndState()
    {
        // Arrange & Act: build a row from a descriptor for an installed synthesis model
        var row = new ModelListItemViewModel(FakeSpeechModel.Descriptor(
            "model-a", SpeechModelState.Downloaded, "Model A", SpeechModelRole.Synthesis));

        // Assert: every displayed value came from the descriptor
        Assert.Equal("model-a", row.Id);
        Assert.Equal("Model A", row.DisplayName);
        Assert.Equal(SpeechModelRole.Synthesis, row.Role);
        Assert.Equal(SpeechModelState.Downloaded, row.State);
    }

    /// <summary>
    ///     Proves that each install state maps to a human-readable status caption.
    /// </summary>
    /// <param name="state">The install state under test.</param>
    /// <param name="expected">The caption the row must display for that state.</param>
    [Theory]
    [InlineData(SpeechModelState.NotDownloaded, "Not downloaded")]
    [InlineData(SpeechModelState.Downloading, "Downloading")]
    [InlineData(SpeechModelState.Downloaded, "Installed")]
    [InlineData(SpeechModelState.FailedOrCorrupt, "Failed or corrupt")]
    public void ModelListItemViewModel_StatusText_EachState_ReturnsReadableCaption(
        SpeechModelState state,
        string expected)
    {
        // Arrange: a row in the state under test
        var row = new ModelListItemViewModel(FakeSpeechModel.Descriptor("model-a", state));

        // Act: read the caption the list displays
        var actual = row.StatusText;

        // Assert: the caption is the documented human-readable form
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Proves that only a model which is neither installed nor already downloading offers a
    ///     download, so working installed content cannot be disturbed and a retry after failure
    ///     remains possible.
    /// </summary>
    /// <param name="state">The install state under test.</param>
    /// <param name="expected">Whether a download may be started in that state.</param>
    [Theory]
    [InlineData(SpeechModelState.NotDownloaded, true)]
    [InlineData(SpeechModelState.FailedOrCorrupt, true)]
    [InlineData(SpeechModelState.Downloading, false)]
    [InlineData(SpeechModelState.Downloaded, false)]
    public void ModelListItemViewModel_CanDownload_EachState_ReflectsRetryPolicy(
        SpeechModelState state,
        bool expected)
    {
        // Arrange: a row in the state under test
        var row = new ModelListItemViewModel(FakeSpeechModel.Descriptor("model-a", state));

        // Act: read whether the download button is enabled
        var actual = row.CanDownload;

        // Assert: only retryable states offer a download
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Proves that only the downloading state shows the progress bar.
    /// </summary>
    [Fact]
    public void ModelListItemViewModel_IsDownloading_DownloadingState_ReturnsTrue()
    {
        // Arrange: one downloading row and one idle row
        var downloading = new ModelListItemViewModel(
            FakeSpeechModel.Descriptor("model-a", SpeechModelState.Downloading));
        var idle = new ModelListItemViewModel(FakeSpeechModel.Descriptor("model-b"));

        // Act & Assert: only the downloading row reports an in-flight download
        Assert.True(downloading.IsDownloading);
        Assert.False(idle.IsDownloading);
    }

    /// <summary>
    ///     Proves that a failure explanation is only reported as present when there is one to
    ///     show, so an empty message never renders as blank red text.
    /// </summary>
    [Fact]
    public void ModelListItemViewModel_HasFailureMessage_ReflectsPresenceOfMessage()
    {
        // Arrange: a row with no failure recorded
        var row = new ModelListItemViewModel(FakeSpeechModel.Descriptor("model-a"));

        // Act & Assert: absent, then present after a failure is recorded
        Assert.False(row.HasFailureMessage);
        row.FailureMessage = "Something went wrong.";
        Assert.True(row.HasFailureMessage);
    }

    /// <summary>
    ///     Proves that changing the install state announces every derived value a bound row
    ///     displays, so the caption, progress bar, and button never go stale.
    /// </summary>
    [Fact]
    public void ModelListItemViewModel_State_Changed_NotifiesDerivedProperties()
    {
        // Arrange: a row with a recorded notification list
        var row = new ModelListItemViewModel(FakeSpeechModel.Descriptor("model-a"));
        var changed = new List<string?>();
        row.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        // Act: move the row into the downloading state
        row.State = SpeechModelState.Downloading;

        // Assert: every derived display value was announced
        Assert.Contains(nameof(ModelListItemViewModel.StatusText), changed);
        Assert.Contains(nameof(ModelListItemViewModel.CanDownload), changed);
        Assert.Contains(nameof(ModelListItemViewModel.IsDownloading), changed);
    }
}
