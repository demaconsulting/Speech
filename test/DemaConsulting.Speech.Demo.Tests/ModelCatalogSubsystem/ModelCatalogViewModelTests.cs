using DemaConsulting.Speech.Demo.ModelCatalogSubsystem;
using DemaConsulting.Speech.Demo.Tests.Fakes;
using DemaConsulting.Speech.ModelManagementSubsystem;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace DemaConsulting.Speech.Demo.Tests.ModelCatalogSubsystem;

/// <summary>
///     Unit tests for <see cref="ModelCatalogViewModel"/>.
/// </summary>
public class ModelCatalogViewModelTests
{
    /// <summary>
    ///     Builds a faked catalog service reporting the supplied descriptors.
    /// </summary>
    /// <param name="descriptors">The descriptors the service reports.</param>
    /// <returns>The configured fake service.</returns>
    private static IModelCatalogService Service(params SpeechModelDescriptor[] descriptors)
    {
        var service = Substitute.For<IModelCatalogService>();
        service.Enumerate().Returns(descriptors);
        return service;
    }

    /// <summary>
    ///     Builds a faked catalog service reporting one set of descriptors on its first
    ///     enumeration and another on every later enumeration, standing in for the library's view
    ///     of the catalog changing between refreshes.
    /// </summary>
    /// <param name="first">The descriptors reported by the first enumeration.</param>
    /// <param name="later">The descriptors reported by later enumerations.</param>
    /// <returns>The configured fake service.</returns>
    private static IModelCatalogService SequencedService(
        IReadOnlyList<SpeechModelDescriptor> first,
        IReadOnlyList<SpeechModelDescriptor> later)
    {
        var service = Substitute.For<IModelCatalogService>();
        service.Enumerate().Returns(first, later);
        return service;
    }

    /// <summary>
    ///     Proves that the panel rejects a missing catalog service rather than presenting a
    ///     permanently broken list.
    /// </summary>
    [Fact]
    public void ModelCatalogViewModel_Constructor_NullService_ThrowsArgumentNullException()
    {
        // Act & Assert: composing without a catalog service is a programming error
        Assert.Throws<ArgumentNullException>(() => new ModelCatalogViewModel(null!));
    }

    /// <summary>
    ///     Proves that a catalog service reporting no models renders as an explicit, honest
    ///     explanation rather than a blank list a user would read as a bug.
    /// </summary>
    [Fact]
    public void ModelCatalogViewModel_Constructor_EmptyCatalog_ReportsHonestEmptyState()
    {
        // Arrange & Act: compose the panel over a catalog reporting no models at all
        var viewModel = new ModelCatalogViewModel(Service());

        // Assert: the panel reports emptiness explicitly and selects nothing
        Assert.Empty(viewModel.Models);
        Assert.False(viewModel.HasModels);
        Assert.True(viewModel.IsCatalogEmpty);
        Assert.Null(viewModel.SelectedModel);
    }

    /// <summary>
    ///     Proves that the empty-state message explains why the catalog is empty rather than
    ///     implying a failure.
    /// </summary>
    [Fact]
    public void ModelCatalogViewModel_EmptyCatalogMessage_Read_ExplainsCatalogHasNoKnownModels()
    {
        // Act: read the message the view shows when the catalog is empty
        var message = ModelCatalogViewModel.EmptyCatalogMessage;

        // Assert: it states the honest reason without claiming the library ships no models
        Assert.Contains("no speech models are currently known to this catalog", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ships no downloadable speech models", message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a populated catalog produces one row per known model, carrying each
    ///     model's identity and state as the library reported them.
    /// </summary>
    [Fact]
    public void ModelCatalogViewModel_Constructor_PopulatedCatalog_BuildsOneRowPerModel()
    {
        // Arrange: a catalog reporting two models in different states
        var viewModel = new ModelCatalogViewModel(Service(
            FakeSpeechModel.Descriptor("model-a", SpeechModelState.NotDownloaded, "Model A"),
            FakeSpeechModel.Descriptor("model-b", SpeechModelState.Downloaded, "Model B", SpeechModelRole.Synthesis)));

        // Act: read the rows built from the reported descriptors
        var rows = viewModel.Models;

        // Assert: identity, role, and state are carried through faithfully
        Assert.Equal(2, rows.Count);
        Assert.False(viewModel.IsCatalogEmpty);
        Assert.True(viewModel.HasModels);
        Assert.Equal("model-a", rows[0].Id);
        Assert.Equal("Model A", rows[0].DisplayName);
        Assert.Equal(SpeechModelState.NotDownloaded, rows[0].State);
        Assert.Equal(SpeechModelRole.Synthesis, rows[1].Role);
        Assert.Equal(SpeechModelState.Downloaded, rows[1].State);
    }

    /// <summary>
    ///     Proves that the first model is pre-selected so the panel never opens with a populated
    ///     list and no selection.
    /// </summary>
    [Fact]
    public void ModelCatalogViewModel_Constructor_PopulatedCatalog_SelectsFirstModel()
    {
        // Arrange & Act: compose the panel over a catalog reporting two models
        var viewModel = new ModelCatalogViewModel(Service(
            FakeSpeechModel.Descriptor("model-a"),
            FakeSpeechModel.Descriptor("model-b")));

        // Assert: the first row is selected
        Assert.Equal("model-a", viewModel.SelectedModel?.Id);
    }

    /// <summary>
    ///     Proves that refreshing rebuilds the rows from the library's current view while keeping
    ///     the user's selected model selected.
    /// </summary>
    [Fact]
    public void ModelCatalogViewModel_Refresh_StateChanged_RebuildsRowsAndKeepsSelection()
    {
        // Arrange: a catalog reporting one model as not downloaded, then as installed
        var service = SequencedService(
            [FakeSpeechModel.Descriptor("model-a"), FakeSpeechModel.Descriptor("model-b")],
            [
                FakeSpeechModel.Descriptor("model-a"),
                FakeSpeechModel.Descriptor("model-b", SpeechModelState.Downloaded)
            ]);
        var viewModel = new ModelCatalogViewModel(service);
        viewModel.SelectedModel = viewModel.Models[1];

        // Act: re-read the catalog after the second model was installed
        viewModel.Refresh();

        // Assert: the row reflects the new state and the same model stays selected
        Assert.Equal(SpeechModelState.Downloaded, viewModel.Models[1].State);
        Assert.Equal("model-b", viewModel.SelectedModel?.Id);
    }

    /// <summary>
    ///     Proves that a catalog which loses a model no longer shows it, rather than keeping a
    ///     stale row alive across a refresh.
    /// </summary>
    [Fact]
    public void ModelCatalogViewModel_Refresh_ModelNoLongerKnown_DropsRowAndFallsBack()
    {
        // Arrange: a catalog reporting two models, then only the first
        var service = SequencedService(
            [FakeSpeechModel.Descriptor("model-a"), FakeSpeechModel.Descriptor("model-b")],
            [FakeSpeechModel.Descriptor("model-a")]);
        var viewModel = new ModelCatalogViewModel(service);
        viewModel.SelectedModel = viewModel.Models[1];

        // Act: re-read the catalog after the second model disappeared
        viewModel.Refresh();

        // Assert: the stale row is gone and the selection falls back to a real row
        Assert.Single(viewModel.Models);
        Assert.Equal("model-a", viewModel.SelectedModel?.Id);
    }

    /// <summary>
    ///     Proves that the refresh command exposed to the view performs the same re-read as the
    ///     method, so the button in the UI is genuinely wired to the behavior under test.
    /// </summary>
    [Fact]
    public void ModelCatalogViewModel_RefreshCommand_Executed_ReReadsCatalog()
    {
        // Arrange: a catalog reporting nothing, then one model
        var service = SequencedService([], [FakeSpeechModel.Descriptor("model-a")]);
        var viewModel = new ModelCatalogViewModel(service);

        // Act: invoke the command the view's button binds to
        viewModel.RefreshCommand.Execute(null);

        // Assert: the newly known model is now listed
        Assert.Single(viewModel.Models);
    }

    /// <summary>
    ///     Proves that a successful download marks the model installed and completes its progress,
    ///     so the row stops offering a download it no longer needs.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_DownloadAsync_Installed_MarksModelDownloaded()
    {
        // Arrange: a catalog whose download succeeds
        var service = Service(FakeSpeechModel.Descriptor("model-a"));
        service
            .DownloadAsync("model-a", Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new SpeechModelDownloadResult(SpeechModelDownloadOutcome.Installed));
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.Models[0];

        // Act: download the model
        await viewModel.DownloadAsync(row, TestContext.Current.CancellationToken);

        // Assert: the row reports the model installed and no longer downloadable
        Assert.Equal(SpeechModelState.Downloaded, row.State);
        Assert.Equal(1.0, row.ProgressFraction);
        Assert.False(row.CanDownload);
        Assert.False(row.HasFailureMessage);
    }

    /// <summary>
    ///     Proves that progress reported by the library while a download runs is reflected in the
    ///     row, which is what drives the visible progress bar.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_DownloadAsync_ProgressReported_UpdatesRowProgress()
    {
        // Arrange: an inline synchronization context so progress callbacks are deterministic,
        // and a catalog that reports partial progress before completing
        using var scope = InlineSynchronizationContext.Install();
        var observed = new List<double?>();
        var service = Service(FakeSpeechModel.Descriptor("model-a"));
        service
            .DownloadAsync("model-a", Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var progress = callInfo.Arg<IProgress<SpeechModelDownloadProgress>?>();
                progress?.Report(new SpeechModelDownloadProgress(0, 1, 25, 100));
                progress?.Report(new SpeechModelDownloadProgress(0, 1, 50, 100));
                return Task.FromResult(new SpeechModelDownloadResult(SpeechModelDownloadOutcome.Installed));
            });
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.Models[0];
        row.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ModelListItemViewModel.ProgressFraction))
            {
                observed.Add(row.ProgressFraction);
            }
        };

        // Act: download the model, observing every progress update
        await viewModel.DownloadAsync(row, TestContext.Current.CancellationToken);

        // Assert: the reported fractions reached the row in order, ending at completion
        Assert.Contains(0.25, observed);
        Assert.Contains(0.5, observed);
        Assert.Equal(1.0, row.ProgressFraction);
    }

    /// <summary>
    ///     Proves that a checksum mismatch is surfaced in the row as an explained failure rather
    ///     than being reported as a successful install.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_DownloadAsync_ChecksumMismatch_ReportsExplainedFailure()
    {
        // Arrange: a catalog whose download completes with a checksum mismatch
        var service = Service(FakeSpeechModel.Descriptor("model-a"));
        service
            .DownloadAsync("model-a", Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new SpeechModelDownloadResult(SpeechModelDownloadOutcome.ChecksumMismatch));
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.Models[0];

        // Act: download the model
        await viewModel.DownloadAsync(row, TestContext.Current.CancellationToken);

        // Assert: the row explains the mismatch and offers a retry
        Assert.Equal(SpeechModelState.FailedOrCorrupt, row.State);
        Assert.Null(row.ProgressFraction);
        Assert.True(row.HasFailureMessage);
        Assert.Contains("checksums", row.FailureMessage!, StringComparison.Ordinal);
        Assert.True(row.CanDownload);
    }

    /// <summary>
    ///     Proves that a transport failure surfaces the library's own error message, so the user
    ///     sees why the download failed rather than a generic message.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_DownloadAsync_Failed_ReportsLibraryErrorMessage()
    {
        // Arrange: a catalog whose download fails with a reported error
        var service = Service(FakeSpeechModel.Descriptor("model-a"));
        service
            .DownloadAsync("model-a", Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new SpeechModelDownloadResult(
                SpeechModelDownloadOutcome.Failed,
                new InvalidOperationException("The server refused the connection.")));
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.Models[0];

        // Act: download the model
        await viewModel.DownloadAsync(row, TestContext.Current.CancellationToken);

        // Assert: the row carries the library's own explanation
        Assert.Equal(SpeechModelState.FailedOrCorrupt, row.State);
        Assert.Equal("The server refused the connection.", row.FailureMessage);
    }

    /// <summary>
    ///     Proves that an unexpected exception from the catalog seam is contained in the row
    ///     rather than escaping and crashing the demo.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_DownloadAsync_SeamThrows_ContainsFaultInRow()
    {
        // Arrange: a catalog whose download throws instead of returning an outcome
        var service = Service(FakeSpeechModel.Descriptor("model-a"));
        service
            .DownloadAsync("model-a", Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Catalog exploded."));
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.Models[0];

        // Act: download the model, capturing any escaping exception
        var exception = await Record.ExceptionAsync(
            () => viewModel.DownloadAsync(row, TestContext.Current.CancellationToken));

        // Assert: nothing escaped and the row explains the fault
        Assert.Null(exception);
        Assert.Equal(SpeechModelState.FailedOrCorrupt, row.State);
        Assert.Equal("Catalog exploded.", row.FailureMessage);
    }

    /// <summary>
    ///     Proves that a canceled download leaves the model reported as simply not downloaded,
    ///     matching the library's guarantee that nothing was installed.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_DownloadAsync_Canceled_ReportsNotDownloaded()
    {
        // Arrange: a catalog whose download observes cancellation
        var service = Service(FakeSpeechModel.Descriptor("model-a"));
        service
            .DownloadAsync("model-a", Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.Models[0];

        // Act: download the model and let it be canceled
        await viewModel.DownloadAsync(row, TestContext.Current.CancellationToken);

        // Assert: the model is offered again rather than marked corrupt
        Assert.Equal(SpeechModelState.NotDownloaded, row.State);
        Assert.Equal("Download canceled.", row.FailureMessage);
        Assert.True(row.CanDownload);
    }

    /// <summary>
    ///     Proves that an already-installed model is never re-downloaded, so a user cannot
    ///     disturb working installed content by clicking twice.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_DownloadAsync_AlreadyInstalledModel_DoesNotDownload()
    {
        // Arrange: a catalog whose single model is already installed
        var service = Service(FakeSpeechModel.Descriptor("model-a", SpeechModelState.Downloaded));
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.Models[0];

        // Act: attempt to download the installed model
        await viewModel.DownloadAsync(row, TestContext.Current.CancellationToken);

        // Assert: the seam was never asked to download and the state is untouched
        await service.DidNotReceive().DownloadAsync(
            Arg.Any<string>(), Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>());
        Assert.Equal(SpeechModelState.Downloaded, row.State);
    }

    /// <summary>
    ///     Proves that invoking the download command with no row selected does nothing, so an
    ///     empty catalog's UI cannot fault.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_DownloadAsync_NullModel_DoesNothing()
    {
        // Arrange: a panel over an empty catalog
        var service = Service();
        var viewModel = new ModelCatalogViewModel(service);

        // Act: attempt a download with no row at all
        var exception = await Record.ExceptionAsync(
            () => viewModel.DownloadAsync(null, TestContext.Current.CancellationToken));

        // Assert: nothing happened and nothing threw
        Assert.Null(exception);
        await service.DidNotReceive().DownloadAsync(
            Arg.Any<string>(), Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Proves that a retry after a failure clears the previous failure explanation, so a stale
    ///     error is never shown beside an in-flight download.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_DownloadAsync_RetryAfterFailure_ClearsPreviousFailureMessage()
    {
        // Arrange: a catalog that fails once and then succeeds
        var service = Service(FakeSpeechModel.Descriptor("model-a"));
        service
            .DownloadAsync("model-a", Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(
                new SpeechModelDownloadResult(SpeechModelDownloadOutcome.ChecksumMismatch),
                new SpeechModelDownloadResult(SpeechModelDownloadOutcome.Installed));
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.Models[0];

        // Act: download once (failing), then retry
        await viewModel.DownloadAsync(row, TestContext.Current.CancellationToken);
        await viewModel.DownloadAsync(row, TestContext.Current.CancellationToken);

        // Assert: the successful retry left no stale failure text behind
        Assert.Equal(SpeechModelState.Downloaded, row.State);
        Assert.Null(row.FailureMessage);
        Assert.False(row.HasFailureMessage);
    }
}
