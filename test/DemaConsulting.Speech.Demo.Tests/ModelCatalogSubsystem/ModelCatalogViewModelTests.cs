using DemaConsulting.Speech.Demo;
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
    ///     Proves that initial mirror values (for example from launch-time arguments) pre-populate
    ///     the mirror-settings panel rather than always starting blank.
    /// </summary>
    [Fact]
    public void ModelCatalogViewModel_Constructor_InitialMirrorValues_PrePopulatesFields()
    {
        // Act: compose the panel with launch-time mirror values supplied
        var viewModel = new ModelCatalogViewModel(
            Service(), "https://mirror.example.com", "alice", "secret", "token-value");

        // Assert: every field reflects the supplied initial value
        Assert.Equal("https://mirror.example.com", viewModel.MirrorUrl);
        Assert.Equal("alice", viewModel.MirrorUser);
        Assert.Equal("secret", viewModel.MirrorPassword);
        Assert.Equal("token-value", viewModel.MirrorBearerToken);
    }

    /// <summary>
    ///     Proves that applying a valid mirror forwards a correctly configured
    ///     <see cref="DownloadMirror"/> to the catalog service.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_ApplyMirrorAsync_ValidMirror_ForwardsConfiguredMirror()
    {
        // Arrange
        var service = Service();
        var viewModel = new ModelCatalogViewModel(service)
        {
            MirrorUrl = "https://mirror.example.com",
            MirrorUser = "alice",
            MirrorPassword = "secret",
        };

        // Act
        await viewModel.ApplyMirrorCommand.ExecuteAsync(null);

        // Assert: the service received a mirror built from the typed fields
        service.Received(1).ApplyMirror(Arg.Is<DownloadMirror?>(
            mirror => mirror != null
                      && mirror.BaseUri == new Uri("https://mirror.example.com")
                      && mirror.Credentials!.UserName == "alice"
                      && mirror.Credentials.Password == "secret"));
    }

    /// <summary>
    ///     Proves that applying a valid mirror reports the applied mirror in the panel's confirmation message.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_ApplyMirrorAsync_ValidMirror_ReportsSuccess()
    {
        // Arrange
        var viewModel = new ModelCatalogViewModel(Service()) { MirrorUrl = "https://mirror.example.com" };

        // Act
        await viewModel.ApplyMirrorCommand.ExecuteAsync(null);

        // Assert: a success confirmation naming the mirror is shown
        Assert.False(viewModel.MirrorHasError);
        Assert.True(viewModel.HasMirrorStatusMessage);
        Assert.True(viewModel.MirrorAppliedSuccessfully);
        Assert.Contains("mirror.example.com", viewModel.MirrorStatusMessage, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that applying a valid mirror re-reads the catalog, so the rows reflect whatever the
    ///     reconfigured catalog reports.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_ApplyMirrorAsync_ValidMirror_ReReadsCatalog()
    {
        // Arrange: the reconfigured catalog reports a different state on its next enumeration
        var service = SequencedService(
            [FakeSpeechModel.Descriptor("model-a", SpeechModelState.NotDownloaded)],
            [FakeSpeechModel.Descriptor("model-a", SpeechModelState.Downloaded)]);
        var viewModel = new ModelCatalogViewModel(service) { MirrorUrl = "https://mirror.example.com" };

        // Act
        await viewModel.ApplyMirrorCommand.ExecuteAsync(null);

        // Assert: the catalog was enumerated again and the row shows the new state
        service.Received(2).Enumerate();
        Assert.Equal(SpeechModelState.Downloaded, Assert.Single(viewModel.Models).State);
    }

    /// <summary>
    ///     Proves that an unexpected exception from the service's mirror reconfiguration is reported
    ///     in the panel rather than escaping the command.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_ApplyMirrorAsync_UnexpectedFault_ReportedInPanel()
    {
        // Arrange
        var service = Service();
        service
            .When(s => s.ApplyMirror(Arg.Any<DownloadMirror?>()))
            .Do(_ => throw new IOException("catalog unreadable"));

        // Act & Assert
        await AssertMirrorFaultReportedAsync(service);
    }

    /// <summary>
    ///     Proves that a failure to re-read the catalog after a successful mirror reconfiguration is
    ///     reported in the panel rather than escaping the command.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_ApplyMirrorAsync_ReloadFault_ReportedInPanel()
    {
        // Arrange
        var service = Service();
        service.Enumerate().Returns(_ => [], _ => throw new IOException("catalog unreadable"));

        // Act & Assert
        await AssertMirrorFaultReportedAsync(service);
    }

    /// <summary>
    ///     Applies a valid mirror through a view model over <paramref name="service"/> and asserts the
    ///     fault is contained and reported.
    /// </summary>
    /// <param name="service">The service configured to fault.</param>
    private static async Task AssertMirrorFaultReportedAsync(IModelCatalogService service)
    {
        var viewModel = new ModelCatalogViewModel(service) { MirrorUrl = "https://mirror.example.com" };

        // Act
        var exception = await Record.ExceptionAsync(() => viewModel.ApplyMirrorCommand.ExecuteAsync(null));

        // Assert: nothing escaped and the panel reports an error naming the cause
        Assert.Null(exception);
        Assert.True(viewModel.MirrorHasError);
        Assert.False(viewModel.MirrorAppliedSuccessfully);
        Assert.Contains("catalog unreadable", viewModel.MirrorStatusMessage, StringComparison.Ordinal);
    }
    /// <summary>
    ///     Proves that a typed bearer token is forwarded to the catalog service with the mirror.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_ApplyMirrorAsync_BearerToken_ForwardsToken()
    {
        // Arrange
        var service = Service();
        var viewModel = new ModelCatalogViewModel(service)
        {
            MirrorUrl = "https://mirror.example.com",
            MirrorBearerToken = "token-value",
        };

        // Act
        await viewModel.ApplyMirrorCommand.ExecuteAsync(null);

        // Assert
        service.Received(1).ApplyMirror(Arg.Is<DownloadMirror?>(
            mirror => mirror != null && mirror.BearerToken == "token-value"));
    }

    /// <summary>
    ///     Proves that clearing the mirror URL and applying reverts the service to no mirror
    ///     rather than requiring a separate "clear" action.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_ApplyMirrorAsync_BlankUrl_AppliesNullMirror()
    {
        // Arrange
        var service = Service();
        var viewModel = new ModelCatalogViewModel(service) { MirrorUrl = null };

        // Act
        await viewModel.ApplyMirrorCommand.ExecuteAsync(null);

        // Assert
        service.Received(1).ApplyMirror(null);
        Assert.False(viewModel.MirrorHasError);
        Assert.True(viewModel.MirrorAppliedSuccessfully);
    }

    /// <summary>
    ///     Proves that an invalid mirror URL is reported in the panel rather than thrown at the
    ///     user, consistent with every other seam failure this panel reports in place.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_ApplyMirrorAsync_InvalidUrl_ReportsErrorWithoutCallingService()
    {
        // Arrange
        var service = Service();
        var viewModel = new ModelCatalogViewModel(service) { MirrorUrl = "not a url" };

        // Act
        var exception = await Record.ExceptionAsync(() => viewModel.ApplyMirrorCommand.ExecuteAsync(null));

        // Assert: nothing escaped, the service was never asked to change its mirror, and the
        // panel reports the failure as an error rather than a success
        Assert.Null(exception);
        service.DidNotReceive().ApplyMirror(Arg.Any<DownloadMirror?>());
        Assert.True(viewModel.MirrorHasError);
        Assert.True(viewModel.HasMirrorStatusMessage);
        Assert.False(viewModel.MirrorAppliedSuccessfully);
    }

    /// <summary>
    ///     Proves that supplying a bearer token (or user/password) with no mirror URL is reported
    ///     as a panel error rather than being silently discarded, matching
    ///     <see cref="MirrorOptionsFactory.Create"/>'s own documented rejection of stray
    ///     credentials supplied without a URL.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_ApplyMirrorAsync_CredentialsWithoutUrl_ReportsErrorWithoutCallingService()
    {
        // Arrange
        var service = Service();
        var viewModel = new ModelCatalogViewModel(service) { MirrorBearerToken = "token-value" };

        // Act
        var exception = await Record.ExceptionAsync(() => viewModel.ApplyMirrorCommand.ExecuteAsync(null));

        // Assert: nothing escaped, the service was never asked to change its mirror, and the
        // panel reports the failure as an error rather than a success
        Assert.Null(exception);
        service.DidNotReceive().ApplyMirror(Arg.Any<DownloadMirror?>());
        Assert.True(viewModel.MirrorHasError);
        Assert.True(viewModel.HasMirrorStatusMessage);
        Assert.False(viewModel.MirrorAppliedSuccessfully);
    }

    /// <summary>
    ///     Proves that a catalog service unable to support mirror reconfiguration reports its
    ///     <see cref="InvalidOperationException"/> in the panel rather than crashing the demo.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_ApplyMirrorAsync_ServiceRejectsReconfiguration_ReportsError()
    {
        // Arrange
        var service = Service();
        service
            .When(s => s.ApplyMirror(Arg.Any<DownloadMirror?>()))
            .Do(_ => throw new InvalidOperationException("This service cannot change its mirror."));
        var viewModel = new ModelCatalogViewModel(service) { MirrorUrl = "https://mirror.example.com" };

        // Act
        await viewModel.ApplyMirrorCommand.ExecuteAsync(null);

        // Assert
        Assert.True(viewModel.MirrorHasError);
        Assert.Equal("This service cannot change its mirror.", viewModel.MirrorStatusMessage);
    }

    /// <summary>
    ///     Proves that applying a mirror while a row is downloading is refused outright - without
    ///     ever calling the catalog service - because the service disposes the catalog a download
    ///     in flight depends on, which would otherwise abort that download.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_ApplyMirrorAsync_DownloadInProgress_RefusesAndReportsError()
    {
        // Arrange: one row already in the Downloading state
        var service = Service(FakeSpeechModel.Descriptor("model-a", SpeechModelState.Downloading));
        var viewModel = new ModelCatalogViewModel(service) { MirrorUrl = "https://mirror.example.com" };

        // Act
        await viewModel.ApplyMirrorCommand.ExecuteAsync(null);

        // Assert: refused before ever reaching the service, and reported as an explained error
        service.DidNotReceive().ApplyMirror(Arg.Any<DownloadMirror?>());
        Assert.True(viewModel.MirrorHasError);
        Assert.True(viewModel.HasMirrorStatusMessage);
        Assert.False(viewModel.MirrorAppliedSuccessfully);
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
    public async Task ModelCatalogViewModel_RefreshAsync_StateChanged_RebuildsRowsAndKeepsSelection()
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
        await viewModel.RefreshAsync();

        // Assert: the row reflects the new state and the same model stays selected
        Assert.Equal(SpeechModelState.Downloaded, viewModel.Models[1].State);
        Assert.Equal("model-b", viewModel.SelectedModel?.Id);
    }

    /// <summary>
    ///     Proves that a catalog which loses a model no longer shows it, rather than keeping a
    ///     stale row alive across a refresh.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_RefreshAsync_ModelNoLongerKnown_DropsRowAndFallsBack()
    {
        // Arrange: a catalog reporting two models, then only the first
        var service = SequencedService(
            [FakeSpeechModel.Descriptor("model-a"), FakeSpeechModel.Descriptor("model-b")],
            [FakeSpeechModel.Descriptor("model-a")]);
        var viewModel = new ModelCatalogViewModel(service);
        viewModel.SelectedModel = viewModel.Models[1];

        // Act: re-read the catalog after the second model disappeared
        await viewModel.RefreshAsync();

        // Assert: the stale row is gone and the selection falls back to a real row
        Assert.Single(viewModel.Models);
        Assert.Equal("model-a", viewModel.SelectedModel?.Id);
    }

    /// <summary>
    ///     Proves that the refresh command exposed to the view performs the same re-read as the
    ///     method, so the button in the UI is genuinely wired to the behavior under test.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_RefreshCommand_Executed_ReReadsCatalog()
    {
        // Arrange: a catalog reporting nothing, then one model
        var service = SequencedService([], [FakeSpeechModel.Descriptor("model-a")]);
        var viewModel = new ModelCatalogViewModel(service);

        // Act: invoke the command the view's button binds to
        await viewModel.RefreshCommand.ExecuteAsync(null);

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
    ///     Proves that a row shows the downloading state while its download is still running.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_DownloadAsync_InFlight_ShowsDownloadingState()
    {
        // Arrange: a catalog whose download stays pending until released
        var release = new TaskCompletionSource<SpeechModelDownloadResult>();
        var service = Service(FakeSpeechModel.Descriptor("model-a"));
        service
            .DownloadAsync("model-a", Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(release.Task);
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.Models[0];

        // Act: start the download without completing it
        var download = viewModel.DownloadAsync(row, TestContext.Current.CancellationToken);

        // Assert: the row is downloading, then settles once the download completes
        Assert.Equal(SpeechModelState.Downloading, row.State);
        Assert.True(row.IsDownloading);
        release.SetResult(new SpeechModelDownloadResult(SpeechModelDownloadOutcome.Installed));
        await download;
    }

    /// <summary>
    ///     Proves that refreshing while a download is in flight leaves the downloading row in place and does not
    ///     re-read the catalog, so the transfer's progress and outcome are not discarded.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_RefreshAsync_DownloadInFlight_KeepsRowsAndDoesNotRereadCatalog()
    {
        // Arrange: a catalog whose download stays pending until released
        var release = new TaskCompletionSource<SpeechModelDownloadResult>();
        var service = Service(FakeSpeechModel.Descriptor("model-a"));
        service
            .DownloadAsync("model-a", Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(release.Task);
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.Models[0];
        var download = viewModel.DownloadAsync(row, TestContext.Current.CancellationToken);
        service.ClearReceivedCalls();

        // Act: refresh while the download is running
        await viewModel.RefreshAsync();

        // Assert: the same row is still downloading and the catalog was not enumerated again
        Assert.Same(row, viewModel.Models[0]);
        Assert.Equal(SpeechModelState.Downloading, row.State);
        service.DidNotReceive().Enumerate();

        release.SetResult(new SpeechModelDownloadResult(SpeechModelDownloadOutcome.Installed));
        await download;
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
    ///     Proves that a model whose download is already running is not downloaded a second time.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_DownloadAsync_AlreadyDownloadingModel_DoesNotDownload()
    {
        // Arrange: a catalog whose single model is already downloading
        var service = Service(FakeSpeechModel.Descriptor("model-a", SpeechModelState.Downloading));
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.Models[0];

        // Act
        await viewModel.DownloadAsync(row, TestContext.Current.CancellationToken);

        // Assert
        await service.DidNotReceive().DownloadAsync(
            Arg.Any<string>(), Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>());
        Assert.Equal(SpeechModelState.Downloading, row.State);
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

    /// <summary>
    ///     Builds a catalog service fake offering the built-in voice pack in the given state.
    /// </summary>
    /// <param name="state">The pack's reported state.</param>
    /// <param name="descriptors">The model descriptors to report.</param>
    /// <returns>The configured fake.</returns>
    private static IModelCatalogService VoiceService(SpeechModelState state, params SpeechModelDescriptor[] descriptors)
    {
        var service = Service(descriptors);
        service.ReferenceVoicePacks.Returns([ReferenceVoicePack.VoiceZero10]);
        service.GetReferenceVoicePackState(ReferenceVoicePack.VoiceZero10.Id).Returns(state);
        service.UserVoicesDirectory.Returns("C:\\voices\\user");
        return service;
    }

    /// <summary>
    ///     Proves that the panel lists the reference-voice packs with their state.
    /// </summary>
    [Fact]
    public void ModelCatalogViewModel_Constructor_ListsVoicePacks()
    {
        // Arrange & Act
        var viewModel = new ModelCatalogViewModel(VoiceService(SpeechModelState.NotDownloaded));

        // Assert
        var row = Assert.Single(viewModel.VoicePacks);
        Assert.True(viewModel.HasVoicePacks);
        Assert.Equal("voice-zero-1.0", row.Id);
        Assert.True(row.CanInstall);
        Assert.False(row.CanRemove);
    }

    /// <summary>
    ///     Proves that the panel shows the user voices folder the service reports.
    /// </summary>
    [Fact]
    public void ModelCatalogViewModel_Constructor_VoicePacks_ShowsUserVoicesFolder()
    {
        // Arrange & Act
        var viewModel = new ModelCatalogViewModel(VoiceService(SpeechModelState.NotDownloaded));

        // Assert
        Assert.Equal("C:\\voices\\user", viewModel.UserVoicesDirectory);
    }

    /// <summary>
    ///     Proves that the panel shows the voice-cloning consent note.
    /// </summary>
    [Fact]
    public void ModelCatalogViewModel_Constructor_VoicePacks_ShowsConsentNote()
    {
        // Arrange & Act
        var viewModel = new ModelCatalogViewModel(VoiceService(SpeechModelState.NotDownloaded));

        // Assert
        Assert.Equal(ReferenceVoiceLibrary.ConsentNotice, viewModel.VoiceConsentNotice);
    }

    /// <summary>
    ///     Proves that refreshing reads the emotion-variant flag for an installed pack from the
    ///     service, so the pack row reports whether the install includes emotions.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ModelCatalogViewModel_RefreshAsync_InstalledPack_ReadsHasEmotionsFromService(bool hasEmotions)
    {
        // Arrange: a pack the service reports as installed, with or without emotion variants
        var service = VoiceService(SpeechModelState.Downloaded);
        service.ReferenceVoicePackHasEmotionsAsync("voice-zero-1.0", Arg.Any<CancellationToken>()).Returns(hasEmotions);

        // Act
        var viewModel = new ModelCatalogViewModel(service);
        await viewModel.InitialLoadTask;
        await viewModel.RefreshAsync();

        // Assert
        var row = Assert.Single(viewModel.VoicePacks);
        Assert.Equal(SpeechModelState.Downloaded, row.State);
        Assert.Equal(hasEmotions, row.HasEmotions);
        Assert.Equal(!hasEmotions, row.CanUpgradeToEmotions);
    }

    /// <summary>
    ///     Proves that a service without voice support yields an empty, hidden voices section.
    /// </summary>
    [Fact]
    public void ModelCatalogViewModel_Constructor_NoVoicePacks_HidesSection()
    {
        var viewModel = new ModelCatalogViewModel(Service());
        Assert.False(viewModel.HasVoicePacks);
        Assert.Empty(viewModel.VoicePacks);
    }

    /// <summary>
    ///     Proves that a successful pack install goes through the service with progress and then
    ///     refreshes the rows from the (rebuilt) catalog.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_InstallVoicePackAsync_Installed_RefreshesRows()
    {
        // Arrange: not installed at first, installed once the service has done its work
        var service = VoiceService(SpeechModelState.NotDownloaded);
        service.InstallReferenceVoicePackAsync(
                "voice-zero-1.0", Arg.Any<bool>(), Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                service.GetReferenceVoicePackState("voice-zero-1.0").Returns(SpeechModelState.Downloaded);
                service.ReferenceVoicePackHasEmotionsAsync("voice-zero-1.0", Arg.Any<CancellationToken>()).Returns(true);
                return Task.FromResult(new SpeechModelDownloadResult(SpeechModelDownloadOutcome.Installed));
            });
        var viewModel = new ModelCatalogViewModel(service);

        // Act
        await viewModel.InstallVoicePackAsync(viewModel.VoicePacks[0], TestContext.Current.CancellationToken);

        // Assert
        var row = Assert.Single(viewModel.VoicePacks);
        Assert.Equal(SpeechModelState.Downloaded, row.State);
        Assert.True(row.CanRemove);
        Assert.False(row.CanInstall);
    }

    /// <summary>
    ///     Proves that installing a pack asks the catalog service to install exactly that pack, passing a progress
    ///     receiver so the row can show progress.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ModelCatalogViewModel_InstallVoicePackAsync_Command_InstallsRequestedPackWithProgress()
    {
        // Arrange
        var service = VoiceService(SpeechModelState.NotDownloaded);
        service.InstallReferenceVoicePackAsync(
                Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new SpeechModelDownloadResult(SpeechModelDownloadOutcome.Installed));
        var viewModel = new ModelCatalogViewModel(service);

        // Act
        await viewModel.InstallVoicePackAsync(viewModel.VoicePacks[0], TestContext.Current.CancellationToken);

        // Assert
        await service.Received(1).InstallReferenceVoicePackAsync(
            "voice-zero-1.0", Arg.Any<bool>(), Arg.Is<IProgress<SpeechModelDownloadProgress>?>(p => p != null), Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Proves that the emotion choice is passed to the service when installing a pack.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_InstallVoicePackAsync_PassesEmotionChoice()
    {
        // Arrange
        var service = VoiceService(SpeechModelState.NotDownloaded);
        service.InstallReferenceVoicePackAsync(
                "voice-zero-1.0", Arg.Any<bool>(), Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new SpeechModelDownloadResult(SpeechModelDownloadOutcome.ChecksumMismatch)));
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.VoicePacks[0];
        row.IncludeEmotions = false;

        // Act
        await viewModel.InstallVoicePackAsync(row, TestContext.Current.CancellationToken);

        // Assert
        await service.Received(1).InstallReferenceVoicePackAsync("voice-zero-1.0", false, Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Proves that "Add emotions" on a voices-only install always installs the emotion variants.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_InstallVoicePackAsync_AddEmotions_AlwaysIncludesEmotions()
    {
        // Arrange
        var service = VoiceService(SpeechModelState.NotDownloaded);
        service.InstallReferenceVoicePackAsync(
                "voice-zero-1.0", Arg.Any<bool>(), Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new SpeechModelDownloadResult(SpeechModelDownloadOutcome.ChecksumMismatch)));
        var viewModel = new ModelCatalogViewModel(service);
        var upgradeRow = new ReferenceVoicePackListItemViewModel(ReferenceVoicePack.VoiceZero10, SpeechModelState.Downloaded, hasEmotions: false);
        upgradeRow.IncludeEmotions = false;

        // Act
        await viewModel.InstallVoicePackAsync(upgradeRow, TestContext.Current.CancellationToken);

        // Assert
        await service.Received(1).InstallReferenceVoicePackAsync("voice-zero-1.0", true, Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Proves that a failed, canceled or faulted pack install is reported in the row.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_InstallVoicePackAsync_FailureModes_ReportedInRow()
    {
        // Arrange: three consecutive attempts - checksum mismatch, cancellation, fault
        var service = VoiceService(SpeechModelState.NotDownloaded);
        service.InstallReferenceVoicePackAsync(
                "voice-zero-1.0", Arg.Any<bool>(), Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromResult(new SpeechModelDownloadResult(SpeechModelDownloadOutcome.ChecksumMismatch)),
                _ => Task.FromException<SpeechModelDownloadResult>(new OperationCanceledException()),
                _ => Task.FromException<SpeechModelDownloadResult>(new InvalidOperationException("boom")));
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.VoicePacks[0];

        // Act & Assert
        await viewModel.InstallVoicePackAsync(row, TestContext.Current.CancellationToken);
        Assert.Equal(SpeechModelState.FailedOrCorrupt, row.State);
        Assert.Contains("checksums", row.FailureMessage, StringComparison.Ordinal);

        await viewModel.InstallVoicePackAsync(row, TestContext.Current.CancellationToken);
        Assert.Equal(SpeechModelState.NotDownloaded, row.State);
        Assert.Equal("Install canceled.", row.FailureMessage);

        await viewModel.InstallVoicePackAsync(row, TestContext.Current.CancellationToken);
        Assert.Equal(SpeechModelState.FailedOrCorrupt, row.State);
        Assert.Equal("boom", row.FailureMessage);
    }

    /// <summary>
    ///     Proves that a pack install is refused while a model download is in flight, and that a
    ///     null or already-installed row is ignored.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_InstallVoicePackAsync_ModelDownloading_RefusedWithoutCallingService()
    {
        // Arrange
        var service = VoiceService(
            SpeechModelState.NotDownloaded,
            FakeSpeechModel.Descriptor("m1", SpeechModelState.Downloading));
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.VoicePacks[0];

        // Act
        await viewModel.InstallVoicePackAsync(row, TestContext.Current.CancellationToken);
        await viewModel.InstallVoicePackAsync(null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ModelCatalogViewModel.VoiceBusyMessage, row.FailureMessage);
        Assert.Equal(SpeechModelState.NotDownloaded, row.State);
        await service.DidNotReceiveWithAnyArgs().InstallReferenceVoicePackAsync(default!, default, default, TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Proves that a model download is refused while a pack install is in flight, because the
    ///     install's catalog rebuild would abort it.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_DownloadAsync_PackInstalling_RefusedWithoutCallingService()
    {
        // Arrange
        var service = VoiceService(SpeechModelState.NotDownloaded, FakeSpeechModel.Descriptor("m1"));
        var viewModel = new ModelCatalogViewModel(service);
        viewModel.VoicePacks[0].State = SpeechModelState.Downloading;
        var model = viewModel.Models[0];

        // Act
        await viewModel.DownloadAsync(model, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ModelCatalogViewModel.VoiceBusyMessage, model.FailureMessage);
        await service.DidNotReceiveWithAnyArgs().DownloadAsync(default!, default, TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Proves that applying a mirror is refused while a pack install is in flight.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_ApplyMirrorAsync_PackInstalling_IsRefused()
    {
        // Arrange
        var service = VoiceService(SpeechModelState.NotDownloaded);
        var viewModel = new ModelCatalogViewModel(service);
        viewModel.VoicePacks[0].State = SpeechModelState.Downloading;

        // Act
        await viewModel.ApplyMirrorCommand.ExecuteAsync(null);

        // Assert
        Assert.True(viewModel.MirrorHasError);
        service.DidNotReceiveWithAnyArgs().ApplyMirror(default);
    }

    /// <summary>
    ///     Proves that refreshing is refused while a pack install is in flight, leaving the installing row in place.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_RefreshAsync_PackInstalling_KeepsRowsAndDoesNotRereadCatalog()
    {
        // Arrange
        var service = VoiceService(SpeechModelState.NotDownloaded);
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.VoicePacks[0];
        row.State = SpeechModelState.Downloading;
        service.ClearReceivedCalls();

        // Act
        await viewModel.RefreshAsync();

        // Assert
        Assert.Same(row, viewModel.VoicePacks[0]);
        service.DidNotReceive().Enumerate();
    }

    /// <summary>
    ///     Proves that installing or removing a pack is refused while a different pack is installing.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_VoicePackCommands_AnotherPackInstalling_RefusedWithoutCallingService()
    {
        // Arrange: one pack row is installing; the rows under test are separate rows
        var service = VoiceService(SpeechModelState.NotDownloaded);
        var viewModel = new ModelCatalogViewModel(service);
        viewModel.VoicePacks[0].State = SpeechModelState.Downloading;
        var installable = new ReferenceVoicePackListItemViewModel(ReferenceVoicePack.VoiceZero10, SpeechModelState.NotDownloaded);
        var removable = new ReferenceVoicePackListItemViewModel(ReferenceVoicePack.VoiceZero10, SpeechModelState.Downloaded);

        // Act
        await viewModel.InstallVoicePackAsync(installable, TestContext.Current.CancellationToken);
        await viewModel.RemoveVoicePackAsync(removable, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ModelCatalogViewModel.VoiceBusyMessage, installable.FailureMessage);
        Assert.Equal(ModelCatalogViewModel.VoiceBusyMessage, removable.FailureMessage);
        await service.DidNotReceiveWithAnyArgs().InstallReferenceVoicePackAsync(default!, default, default, TestContext.Current.CancellationToken);
        await service.DidNotReceiveWithAnyArgs().UninstallReferenceVoicePackAsync(default!, TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Proves that a fault while reading an installed pack's emotion flag at construction is contained: the
    ///     initial load task still completes and the flag stays false.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_Constructor_EmotionFlagReadFaults_InitialLoadCompletesWithFlagFalse()
    {
        // Arrange
        var service = VoiceService(SpeechModelState.Downloaded);
        service.ReferenceVoicePackHasEmotionsAsync("voice-zero-1.0", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<bool>(new IOException("unreadable")));

        // Act
        var viewModel = new ModelCatalogViewModel(service);
        await viewModel.InitialLoadTask;

        // Assert
        Assert.False(viewModel.VoicePacks[0].HasEmotions);
    }

    /// <summary>
    ///     Proves that removing a voice pack is refused while a model download is in flight.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_RemoveVoicePackAsync_ModelDownloading_RefusedWithoutCallingService()
    {
        // Arrange
        var service = VoiceService(
            SpeechModelState.Downloaded,
            FakeSpeechModel.Descriptor("m1", SpeechModelState.Downloading));
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.VoicePacks[0];

        // Act
        await viewModel.RemoveVoicePackAsync(row, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ModelCatalogViewModel.VoiceBusyMessage, row.FailureMessage);
        Assert.Equal(SpeechModelState.Downloaded, row.State);
        await service.DidNotReceiveWithAnyArgs().UninstallReferenceVoicePackAsync(default!, TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Proves that removing an installed pack uninstalls it through the service and refreshes
    ///     the rows, and that a non-installed or null row is ignored.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_RemoveVoicePackAsync_Installed_UninstallsAndRefreshesRows()
    {
        // Arrange
        var service = VoiceService(SpeechModelState.Downloaded);
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.VoicePacks[0];

        // Act: remove succeeds and the pack then reports as not installed
        service.UninstallReferenceVoicePackAsync("voice-zero-1.0", Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                service.GetReferenceVoicePackState("voice-zero-1.0").Returns(SpeechModelState.NotDownloaded);
                return Task.CompletedTask;
            });
        await viewModel.RemoveVoicePackAsync(row, TestContext.Current.CancellationToken);

        // Assert
        await service.Received(1).UninstallReferenceVoicePackAsync("voice-zero-1.0", Arg.Any<CancellationToken>());
        service.Received(2).Enumerate();
        Assert.Equal(SpeechModelState.NotDownloaded, viewModel.VoicePacks[0].State);

        // Act & Assert: ignored when not installed, or null
        await viewModel.RemoveVoicePackAsync(viewModel.VoicePacks[0], TestContext.Current.CancellationToken);
        await viewModel.RemoveVoicePackAsync(null, TestContext.Current.CancellationToken);
        await service.Received(1).UninstallReferenceVoicePackAsync("voice-zero-1.0", Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Proves that removing a pack asks the catalog service to uninstall exactly that pack.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ModelCatalogViewModel_RemoveVoicePackAsync_Command_UninstallsRequestedPack()
    {
        // Arrange
        var service = VoiceService(SpeechModelState.Downloaded);
        var viewModel = new ModelCatalogViewModel(service);

        // Act
        await viewModel.RemoveVoicePackAsync(viewModel.VoicePacks[0], TestContext.Current.CancellationToken);

        // Assert
        await service.Received(1).UninstallReferenceVoicePackAsync("voice-zero-1.0", Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Proves that an unexpected fault while removing a voice pack is reported on the pack's row, with the
    ///     fault's message, and the pack stays installed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ModelCatalogViewModel_RemoveVoicePackAsync_Fault_ReportsMessageInRow()
    {
        // Arrange
        var faulty = VoiceService(SpeechModelState.Downloaded);
        faulty.UninstallReferenceVoicePackAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new IOException("locked")));
        var faultyViewModel = new ModelCatalogViewModel(faulty);

        // Act
        await faultyViewModel.RemoveVoicePackAsync(faultyViewModel.VoicePacks[0], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("locked", faultyViewModel.VoicePacks[0].FailureMessage);
        Assert.Equal(SpeechModelState.Downloaded, faultyViewModel.VoicePacks[0].State);
    }

    /// <summary>
    ///     Proves that after a failed, canceled or faulted "Add emotions" upgrade the row is refreshed from the service:
    ///     the library rolls back to the previous voices-only install, so the pack stays installed and removable while
    ///     the failure message is preserved.
    /// </summary>
    [Theory]
    [InlineData("failed")]
    [InlineData("mismatch")]
    [InlineData("canceled")]
    [InlineData("fault")]
    public async Task ModelCatalogViewModel_InstallVoicePackAsync_UpgradeFailsAndRollsBack_KeepsPackInstalledWithMessage(string mode)
    {
        // Arrange: voices-only install; the upgrade attempt fails but the service still reports it Downloaded
        var service = VoiceService(SpeechModelState.Downloaded);
        service.ReferenceVoicePackHasEmotionsAsync("voice-zero-1.0", Arg.Any<CancellationToken>()).Returns(false);
        service.InstallReferenceVoicePackAsync(
                "voice-zero-1.0", Arg.Any<bool>(), Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(_ => mode switch
            {
                "failed" => Task.FromResult(new SpeechModelDownloadResult(SpeechModelDownloadOutcome.Failed, new IOException("disk full"))),
                "mismatch" => Task.FromResult(new SpeechModelDownloadResult(SpeechModelDownloadOutcome.ChecksumMismatch)),
                "canceled" => Task.FromException<SpeechModelDownloadResult>(new OperationCanceledException()),
                _ => Task.FromException<SpeechModelDownloadResult>(new InvalidOperationException("boom")),
            });
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.VoicePacks[0];
        Assert.True(row.CanUpgradeToEmotions);

        // Act
        await viewModel.InstallVoicePackAsync(row, TestContext.Current.CancellationToken);

        // Assert: still installed (voices only), removable, upgradable again, and the message is kept
        Assert.Equal(SpeechModelState.Downloaded, row.State);
        Assert.False(row.HasEmotions);
        Assert.True(row.CanRemove);
        Assert.True(row.CanUpgradeToEmotions);
        Assert.True(row.HasFailureMessage);
        Assert.Null(row.ProgressFraction);
        var expected = mode switch
        {
            "failed" => "disk full",
            "canceled" => "Install canceled.",
            "fault" => "boom",
            _ => "The downloaded files did not match their expected checksums and were discarded.",
        };
        Assert.Equal(expected, row.FailureMessage);
    }

    /// <summary>
    ///     Proves that when the service cannot report the pack's state after a failed install, the row falls back
    ///     to the attempt's own outcome and still shows the message.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_InstallVoicePackAsync_StateReadFails_FallsBackToOutcome()
    {
        // Arrange
        var service = VoiceService(SpeechModelState.NotDownloaded);
        service.InstallReferenceVoicePackAsync(
                "voice-zero-1.0", Arg.Any<bool>(), Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                service.GetReferenceVoicePackState("voice-zero-1.0").Returns(_ => throw new InvalidOperationException("gone"));
                return Task.FromException<SpeechModelDownloadResult>(new IOException("boom"));
            });
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.VoicePacks[0];

        // Act
        await viewModel.InstallVoicePackAsync(row, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelState.FailedOrCorrupt, row.State);
        Assert.Equal("boom", row.FailureMessage);
    }

    /// <summary>
    ///     Proves that a removal the library rejects with a store exception (files still in use) shows the message in
    ///     the row and leaves the pack installed instead of crashing the panel.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_RemoveVoicePackAsync_StoreException_ShowsMessageAndKeepsPack()
    {
        // Arrange
        var service = VoiceService(SpeechModelState.Downloaded);
        const string message = "Failed to uninstall model 'voice-zero-1.0': its installed directory could not be removed.";
        service.UninstallReferenceVoicePackAsync("voice-zero-1.0", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new SpeechModelStoreException(message)));
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.VoicePacks[0];

        // Act
        await viewModel.RemoveVoicePackAsync(row, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(row, viewModel.VoicePacks[0]);
        Assert.Equal(message, row.FailureMessage);
        Assert.Equal(SpeechModelState.Downloaded, row.State);
        Assert.True(row.CanRemove);
    }

    /// <summary>
    ///     Proves that a canceled removal reports "Removal canceled." in the row and keeps the pack installed.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_RemoveVoicePackAsync_Canceled_ShowsCanceledMessage()
    {
        // Arrange
        var service = VoiceService(SpeechModelState.Downloaded);
        service.UninstallReferenceVoicePackAsync("voice-zero-1.0", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new OperationCanceledException()));
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.VoicePacks[0];

        // Act
        await viewModel.RemoveVoicePackAsync(row, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Removal canceled.", row.FailureMessage);
        Assert.Equal(SpeechModelState.Downloaded, row.State);
    }

    /// <summary>
    ///     Proves that credentials cleared in the panel (empty or whitespace text rather than null) are treated as
    ///     not supplied, both when a mirror is applied and when a blank URL reverts to public downloads.
    /// </summary>
    /// <param name="url">The typed mirror URL.</param>
    /// <param name="blank">The blank value typed into every credential field.</param>
    [Theory]
    [InlineData("https://mirror.example.com", "")]
    [InlineData("https://mirror.example.com", "  ")]
    [InlineData("", "")]
    public async Task ModelCatalogViewModel_ApplyMirrorAsync_BlankCredentials_TreatedAsNotSupplied(string url, string blank)
    {
        // Arrange
        var service = Service();
        var viewModel = new ModelCatalogViewModel(service)
        {
            MirrorUrl = url,
            MirrorUser = blank,
            MirrorPassword = blank,
            MirrorBearerToken = blank,
        };

        // Act
        await viewModel.ApplyMirrorCommand.ExecuteAsync(null);

        // Assert: no error, and the service got a credential-free mirror (or none)
        Assert.False(viewModel.MirrorHasError);
        Assert.True(viewModel.MirrorAppliedSuccessfully);
        service.Received(1).ApplyMirror(Arg.Is<DownloadMirror?>(
            mirror => url.Length == 0
                ? mirror == null
                : mirror != null && mirror.Credentials == null && mirror.BearerToken == null));
    }

    /// <summary>
    ///     Proves that the applied-mirror confirmation never contains the typed credentials.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_ApplyMirrorAsync_WithCredentials_MessageDoesNotEchoCredentials()
    {
        // Arrange
        var viewModel = new ModelCatalogViewModel(Service())
        {
            MirrorUrl = "https://mirror.example.com",
            MirrorUser = "alice",
            MirrorPassword = "pw-value-123",
        };

        // Act
        await viewModel.ApplyMirrorCommand.ExecuteAsync(null);

        // Assert
        Assert.True(viewModel.MirrorAppliedSuccessfully);
        Assert.DoesNotContain("pw-value-123", viewModel.MirrorStatusMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("alice", viewModel.MirrorStatusMessage, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a refresh whose catalog read faults is reported in the panel instead of escaping, and that
    ///     the rows already shown are left untouched; a later successful refresh clears the report.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_RefreshAsync_SeamFault_ReportedAndRowsKept()
    {
        // Arrange: the first read succeeds, the second faults, the third succeeds
        var service = Substitute.For<IModelCatalogService>();
        var failNext = false;
        service.Enumerate().Returns(_ => failNext
            ? throw new IOException("catalog unreadable")
            : new[] { FakeSpeechModel.Descriptor("model-a") });
        var viewModel = new ModelCatalogViewModel(service);

        // Act: fault
        failNext = true;
        var exception = await Record.ExceptionAsync(() => viewModel.RefreshCommand.ExecuteAsync(null));

        // Assert
        Assert.Null(exception);
        Assert.True(viewModel.HasRefreshErrorMessage);
        Assert.Contains("catalog unreadable", viewModel.RefreshErrorMessage, StringComparison.Ordinal);
        Assert.Equal("model-a", Assert.Single(viewModel.Models).Id);

        // Act: recover
        failNext = false;
        await viewModel.RefreshCommand.ExecuteAsync(null);

        // Assert
        Assert.False(viewModel.HasRefreshErrorMessage);
    }

    /// <summary>
    ///     Proves that a catalog re-read failing after a successful pack install is reported as a re-read failure,
    ///     not as a failed install.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_InstallVoicePackAsync_ReloadFaults_ReportedAsReReadFailure()
    {
        // Arrange
        var service = VoiceService(SpeechModelState.NotDownloaded);
        service.InstallReferenceVoicePackAsync(
                "voice-zero-1.0", Arg.Any<bool>(), Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                service.Enumerate().Returns(_ => throw new IOException("catalog unreadable"));
                return Task.FromResult(new SpeechModelDownloadResult(SpeechModelDownloadOutcome.Installed));
            });
        var viewModel = new ModelCatalogViewModel(service);
        var row = viewModel.VoicePacks[0];

        // Act
        var exception = await Record.ExceptionAsync(() => viewModel.InstallVoicePackAsync(row, TestContext.Current.CancellationToken));

        // Assert: nothing escaped, the row carries no install-failure text, and the re-read failure is reported
        Assert.Null(exception);
        Assert.Null(row.FailureMessage);
        Assert.Contains("installed, but the catalog could not be re-read", viewModel.RefreshErrorMessage, StringComparison.Ordinal);
        Assert.Contains("catalog unreadable", viewModel.RefreshErrorMessage, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a catalog re-read failing after a successful pack removal is reported with context.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_RemoveVoicePackAsync_ReloadFaults_ReportedAsReReadFailure()
    {
        // Arrange
        var service = VoiceService(SpeechModelState.Downloaded);
        service.UninstallReferenceVoicePackAsync("voice-zero-1.0", Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                service.Enumerate().Returns(_ => throw new IOException("catalog unreadable"));
                return Task.CompletedTask;
            });
        var viewModel = new ModelCatalogViewModel(service);

        // Act
        var exception = await Record.ExceptionAsync(
            () => viewModel.RemoveVoicePackAsync(viewModel.VoicePacks[0], TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(exception);
        Assert.Contains("removed, but the catalog could not be re-read", viewModel.RefreshErrorMessage, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a refresh selects the first row when nothing was selected before it.
    /// </summary>
    [Fact]
    public async Task ModelCatalogViewModel_RefreshAsync_NoPreviousSelection_SelectsFirstRow()
    {
        // Arrange: an empty catalog first (so nothing is selected), then one with two models
        var service = SequencedService([], [FakeSpeechModel.Descriptor("model-a"), FakeSpeechModel.Descriptor("model-b")]);
        var viewModel = new ModelCatalogViewModel(service);
        Assert.Null(viewModel.SelectedModel);

        // Act
        await viewModel.RefreshAsync();

        // Assert
        Assert.Equal("model-a", viewModel.SelectedModel?.Id);
    }
}
