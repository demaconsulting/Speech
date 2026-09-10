using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;
using NSubstitute;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for the <see cref="SpeechModelDownloader"/> class, using a fake
///     <see cref="IModelDownloadClient"/> so every scenario runs deterministically with no real
///     network access.
/// </summary>
public sealed class SpeechModelDownloaderTests : IDisposable
{
    /// <summary>A scratch root directory unique to this test instance, cleaned up on dispose.</summary>
    private readonly string _testRoot;

    /// <summary>
    ///     Initializes a fresh, unique scratch directory for each test.
    /// </summary>
    public SpeechModelDownloaderTests()
    {
        _testRoot = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    /// <summary>
    ///     Deletes the scratch directory tree created for this test instance.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup only; a leftover temp directory does not fail the test.
        }
    }

    /// <summary>
    ///     Proves that a successful download verifies the checksum, installs the model, and
    ///     reports monotonically increasing progress.
    /// </summary>
    [Fact]
    public async Task SpeechModelDownloader_DownloadAsync_ValidPayload_InstallsModelAndReportsProgress()
    {
        // Arrange
        var payload = "hello, model!"u8.ToArray();
        var descriptor = SingleFileDescriptor(payload, "model.bin");
        var store = NewStore();
        var client = new FakeModelDownloadClient(payload, chunkSize: 4);
        var downloader = new SpeechModelDownloader(store, client);
        var reports = new List<SpeechModelDownloadProgress>();
        var progress = new SynchronousProgress<SpeechModelDownloadProgress>(reports.Add);

        // Act
        var result = await downloader.DownloadAsync(
            "model-a", descriptor, progress, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelDownloadOutcome.Installed, result.Outcome);
        Assert.True(store.IsInstalled("model-a"));
        var installedBytes = await File.ReadAllBytesAsync(
            Path.Join(store.GetCurrentDirectory("model-a"), "model.bin"), TestContext.Current.CancellationToken);
        Assert.Equal(payload, installedBytes);
        Assert.NotEmpty(reports);
        Assert.All(reports, r => Assert.Equal(0, r.FileIndex));
        Assert.All(reports, r => Assert.Equal(1, r.FileCount));

        // Bytes transferred must never decrease across successive reports.
        for (var i = 1; i < reports.Count; i++)
        {
            Assert.True(reports[i].BytesTransferred >= reports[i - 1].BytesTransferred);
        }
    }

    /// <summary>
    ///     Proves that a checksum mismatch reports <see cref="SpeechModelDownloadOutcome.ChecksumMismatch"/>
    ///     and installs nothing.
    /// </summary>
    [Fact]
    public async Task SpeechModelDownloader_DownloadAsync_ChecksumMismatch_ReportsChecksumMismatchAndInstallsNothing()
    {
        // Arrange: declare a checksum that does not match the payload the fake client returns
        var payload = "hello, model!"u8.ToArray();
        var wrongChecksum = new string('0', 64);
        var file = new SpeechModelDownloadFile(new Uri("https://example.test/model.bin"), wrongChecksum, "model.bin");
        var descriptor = new SpeechModelDownloadDescriptor([file]);
        var store = NewStore();
        var client = new FakeModelDownloadClient(payload, chunkSize: 1024);
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var downloader = new SpeechModelDownloader(store, client, diagnostics);

        // Act
        var result = await downloader.DownloadAsync(
            "model-a", descriptor, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelDownloadOutcome.ChecksumMismatch, result.Outcome);
        Assert.False(store.IsInstalled("model-a"));
        Assert.NotNull(result.Error);
        Assert.Contains("model-a", result.Error.Message, StringComparison.Ordinal);
        diagnostics.Received(1).Report(SpeechDiagnosticLevel.Error, "ModelManagementSubsystem", Arg.Any<string>());
    }

    /// <summary>
    ///     Proves that <see cref="SpeechModelDownloader.DownloadAsync(string,SpeechModelDownloadDescriptor,IProgress{SpeechModelDownloadProgress}?,CancellationToken)"/>
    ///     is a genuine no-op for an already-installed model: it reports
    ///     <see cref="SpeechModelDownloadOutcome.Installed"/> without ever invoking the download
    ///     client, and reports an <see cref="SpeechDiagnosticLevel.Info"/> diagnostic.
    /// </summary>
    [Fact]
    public async Task SpeechModelDownloader_DownloadAsync_AlreadyInstalled_ReturnsInstalledWithoutFetchingOrStaging()
    {
        // Arrange: install the model for real once, then swap in a client that must never be called
        var payload = "hello, model!"u8.ToArray();
        var descriptor = SingleFileDescriptor(payload, "model.bin");
        var store = NewStore();
        var firstDownloader = new SpeechModelDownloader(store, new FakeModelDownloadClient(payload, 1024));
        var firstResult = await firstDownloader.DownloadAsync(
            "model-a", descriptor, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(SpeechModelDownloadOutcome.Installed, firstResult.Outcome);

        var client = Substitute.For<IModelDownloadClient>();
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var secondDownloader = new SpeechModelDownloader(store, client, diagnostics);

        // Act
        var result = await secondDownloader.DownloadAsync(
            "model-a", descriptor, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: reported installed, without ever touching the network client
        Assert.Equal(SpeechModelDownloadOutcome.Installed, result.Outcome);
        await client.DidNotReceive().DownloadAsync(
            Arg.Any<Uri>(), Arg.Any<Stream>(), Arg.Any<IProgress<SpeechModelDownloadProgress>?>(), Arg.Any<CancellationToken>());
        diagnostics.Received(1).Report(SpeechDiagnosticLevel.Info, "ModelManagementSubsystem", Arg.Any<string>());
    }

    /// <summary>
    ///     Proves that the already-installed fast path still opportunistically cleans up a
    ///     leftover staging directory from a prior interrupted attempt, rather than leaving it
    ///     in place indefinitely on every future launch's no-op <c>DownloadAsync</c> call.
    /// </summary>
    [Fact]
    public async Task SpeechModelDownloader_DownloadAsync_AlreadyInstalledWithLeftoverStaging_CleansUpLeftover()
    {
        // Arrange: install the model for real, then leave behind an abandoned staging directory
        // as if a prior repair attempt had been interrupted mid-flight.
        var payload = "hello, model!"u8.ToArray();
        var descriptor = SingleFileDescriptor(payload, "model.bin");
        var store = NewStore();
        var firstDownloader = new SpeechModelDownloader(store, new FakeModelDownloadClient(payload, 1024));
        var firstResult = await firstDownloader.DownloadAsync(
            "model-a", descriptor, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(SpeechModelDownloadOutcome.Installed, firstResult.Outcome);

        var (_, leftoverStagingDirectory) = store.BeginStaging("model-a");
        Assert.True(Directory.Exists(leftoverStagingDirectory));

        var secondDownloader = new SpeechModelDownloader(store, Substitute.For<IModelDownloadClient>());

        // Act: a subsequent no-op call for the already-installed model
        var result = await secondDownloader.DownloadAsync(
            "model-a", descriptor, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: still reports the fast-path outcome, but the leftover is gone
        Assert.Equal(SpeechModelDownloadOutcome.Installed, result.Outcome);
        Assert.False(Directory.Exists(leftoverStagingDirectory));
    }

    /// <summary>
    ///     Proves that a second <c>DownloadAsync</c> call for an already-installed model takes
    ///     the Issue 3 fast path - returning <see cref="SpeechModelDownloadOutcome.Installed"/>
    ///     without ever inspecting the second call's descriptor/checksum at all - leaving the
    ///     prior successful install completely untouched. This supersedes this library's earlier
    ///     "second call genuinely re-verifies and can report ChecksumMismatch" behavior: once
    ///     installed, <c>DownloadAsync</c> is a genuine no-op regardless of what a second caller
    ///     supplies, so there is no longer any way to trigger a real repair attempt through this
    ///     API for an already-installed model id.
    /// </summary>
    [Fact]
    public async Task SpeechModelDownloader_DownloadAsync_SecondCallAfterPriorSuccess_TakesFastPathAndLeavesPriorInstallIntact()
    {
        // Arrange: a successful first install
        var goodPayload = "good payload"u8.ToArray();
        var goodDescriptor = SingleFileDescriptor(goodPayload, "model.bin");
        var store = NewStore();
        var firstDownloader = new SpeechModelDownloader(store, new FakeModelDownloadClient(goodPayload, 1024));
        var firstResult = await firstDownloader.DownloadAsync(
            "model-a", goodDescriptor, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(SpeechModelDownloadOutcome.Installed, firstResult.Outcome);

        // Act: a second call whose descriptor's checksum would not match its payload, were it
        // ever actually re-verified
        var badPayload = "bad payload!!"u8.ToArray();
        var wrongChecksumFile = new SpeechModelDownloadFile(
            new Uri("https://example.test/model.bin"),
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            "model.bin");
        var badDescriptor = new SpeechModelDownloadDescriptor([wrongChecksumFile]);
        var secondDownloader = new SpeechModelDownloader(store, new FakeModelDownloadClient(badPayload, 1024));
        var secondResult = await secondDownloader.DownloadAsync(
            "model-a", badDescriptor, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the fast path took over - Installed, not ChecksumMismatch - and the prior good
        // install is untouched
        Assert.Equal(SpeechModelDownloadOutcome.Installed, secondResult.Outcome);
        Assert.True(store.IsInstalled("model-a"));
        var installedBytes = await File.ReadAllBytesAsync(
            Path.Join(store.GetCurrentDirectory("model-a"), "model.bin"), TestContext.Current.CancellationToken);
        Assert.Equal(goodPayload, installedBytes);
    }

    /// <summary>
    ///     Proves that canceling mid-download propagates <see cref="OperationCanceledException"/>
    ///     and installs nothing.
    /// </summary>
    [Fact]
    public async Task SpeechModelDownloader_DownloadAsync_CanceledMidDownload_ThrowsAndInstallsNothing()
    {
        // Arrange
        var payload = new byte[1024];
        var descriptor = SingleFileDescriptor(payload, "model.bin");
        var store = NewStore();
        using var cts = new CancellationTokenSource();
        var client = new FakeModelDownloadClient(payload, chunkSize: 64, cancelAfterChunks: 2, cancellationSource: cts);
        var downloader = new SpeechModelDownloader(store, client);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => downloader.DownloadAsync("model-a", descriptor, cancellationToken: cts.Token));
        Assert.False(store.IsInstalled("model-a"));
    }

    /// <summary>
    ///     Proves that two concurrent download requests for two <em>different</em> model ids run
    ///     fully concurrently, with no pool limit or global gate serializing them.
    /// </summary>
    /// <remarks>
    ///     Uses a deterministic barrier (rather than a timing-based delay) that requires both
    ///     calls to have genuinely reached the download client at the same time before either is
    ///     allowed to proceed: if a regression reintroduced a global single-flight lock, the
    ///     second call would never reach the client while the first held it, and this test would
    ///     fail with a bounded timeout rather than hang or pass by coincidence.
    /// </remarks>
    [Fact]
    public async Task SpeechModelDownloader_DownloadAsync_TwoConcurrentRequestsForDifferentModels_RunConcurrently()
    {
        // Arrange
        var payloadA = "payload-a"u8.ToArray();
        var payloadB = "payload-b"u8.ToArray();
        var descriptorA = SingleFileDescriptorFor(payloadA, "a.bin");
        var descriptorB = SingleFileDescriptorFor(payloadB, "b.bin");
        var store = NewStore();
        var barrierClient = new BarrierModelDownloadClient(
            new Dictionary<Uri, byte[]>
            {
                [descriptorA.Files[0].Uri] = payloadA,
                [descriptorB.Files[0].Uri] = payloadB
            },
            expectedConcurrentCalls: 2);
        var downloader = new SpeechModelDownloader(store, barrierClient);

        // Act: issue two downloads for two different models concurrently
        var taskA = downloader.DownloadAsync("model-a", descriptorA, cancellationToken: TestContext.Current.CancellationToken);
        var taskB = downloader.DownloadAsync("model-b", descriptorB, cancellationToken: TestContext.Current.CancellationToken);
        await Task.WhenAll(taskA, taskB);

        // Assert: both downloads reached the client and both completed successfully
        Assert.True(store.IsInstalled("model-a"));
        Assert.True(store.IsInstalled("model-b"));
    }

    /// <summary>
    ///     Proves that two concurrent download requests for the <em>same</em> model id are still
    ///     serialized, guarding against the same-model-id race in <c>SpeechModelStore</c>'s
    ///     atomic-swap install (two concurrent <c>Directory.Move</c> calls for the same model's
    ///     <c>current/</c> directory are not otherwise safe).
    /// </summary>
    [Fact]
    public async Task SpeechModelDownloader_DownloadAsync_TwoConcurrentRequestsForSameModelId_AreSerialized()
    {
        // Arrange
        var payloadA = "payload-a"u8.ToArray();
        var descriptorA = SingleFileDescriptorFor(payloadA, "a.bin");
        var store = NewStore();
        var orderTrackingClient = new OrderTrackingModelDownloadClient(
            new Dictionary<Uri, byte[]> { [descriptorA.Files[0].Uri] = payloadA });
        var downloader = new SpeechModelDownloader(store, orderTrackingClient);

        // Act: start a first download for "model-a" and wait until it has genuinely reached the
        // client (and is held open there), then start a second download for the same model id.
        var taskA = downloader.DownloadAsync("model-a", descriptorA, cancellationToken: TestContext.Current.CancellationToken);
        await orderTrackingClient.FirstCallEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        var taskB = downloader.DownloadAsync("model-a", descriptorA, cancellationToken: TestContext.Current.CancellationToken);

        // Give the second call a brief window to run - since the first call still holds the
        // per-model-id lock open, the second call must still be queued behind it and so must not
        // yet have reached the client.
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(1, orderTrackingClient.CallCount);

        // Release the first call so it can complete and install the model. The second call was
        // genuinely serialized behind the first (never raced its atomic-swap install); once it
        // acquires the lock in turn, it now observes the already-installed fast path added for
        // Issue 3 and short-circuits to Installed without ever reaching the client a second time
        // - proving both that serialization held (only entry order [1] was ever recorded) and
        // that the fast path itself is race-safe when checked while holding the same lock a
        // concurrent first-time install just released.
        orderTrackingClient.ReleaseFirstCall();
        var resultA = await taskA;
        var resultB = await taskB;

        Assert.Equal([1], orderTrackingClient.EntryOrder);
        Assert.Equal(SpeechModelDownloadOutcome.Installed, resultA.Outcome);
        Assert.Equal(SpeechModelDownloadOutcome.Installed, resultB.Outcome);
        Assert.True(store.IsInstalled("model-a"));
    }

    /// <summary>
    ///     Proves that the <see cref="ISpeechModel"/>-aware overload invokes the model's own
    ///     <see cref="ISpeechModel.InstallAsync"/> hook after checksum verification but before
    ///     <c>current/</c> exists, passing it the staging directory containing the verified file.
    /// </summary>
    [Fact]
    public async Task SpeechModelDownloader_DownloadAsync_WithModel_InvokesInstallAsyncBeforeCompleteInstall()
    {
        // Arrange
        var payload = "hello, model!"u8.ToArray();
        var descriptor = SingleFileDescriptor(payload, "model.bin");
        var store = NewStore();
        var client = new FakeModelDownloadClient(payload, chunkSize: 1024);
        var downloader = new SpeechModelDownloader(store, client);
        var model = new RecordingInstallHookModel("model-a", descriptor);

        // Act
        var result = await downloader.DownloadAsync(model, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelDownloadOutcome.Installed, result.Outcome);
        Assert.True(model.InstallAsyncCalled);
        Assert.True(model.StagingDirectoryContainedVerifiedFileWhenInstallAsyncRan);
        Assert.False(model.CurrentDirectoryExistedWhenInstallAsyncRan);
        Assert.True(store.IsInstalled("model-a"));
    }

    /// <summary>
    ///     Proves that a zip-archive-payload model, downloaded end to end through the
    ///     <see cref="ISpeechModel"/>-aware overload, results in the store's <c>current/</c>
    ///     directory containing the archive's extracted entry rather than the archive itself.
    /// </summary>
    [Fact]
    public async Task SpeechModelDownloader_DownloadAsync_WithZipArchiveModel_CurrentContainsExtractedFilesNotTheArchive()
    {
        // Arrange
        var model = new FakeRecognitionModel("model-zip", useZipArchivePayload: true);
        var store = NewStore();
        var client = new FakeModelDownloadClient(FakeModelDescriptors.ZipArchiveBytes, chunkSize: 1024);
        var downloader = new SpeechModelDownloader(store, client);

        // Act
        var result = await downloader.DownloadAsync(model, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelDownloadOutcome.Installed, result.Outcome);
        var currentDirectory = store.GetCurrentDirectory("model-zip");
        var extractedPath = Path.Join(currentDirectory, FakeModelDescriptors.ZipArchiveEntryName);
        var archivePath = Path.Join(currentDirectory, FakeModelDescriptors.ZipArchiveRelativeInstallPath);
        Assert.True(File.Exists(extractedPath));
        var extractedBytes = await File.ReadAllBytesAsync(extractedPath, TestContext.Current.CancellationToken);
        Assert.Equal(FakeModelDescriptors.ZipArchiveEntryContent, extractedBytes);
        Assert.False(File.Exists(archivePath));
    }

    /// <summary>
    ///     Proves that a second <c>DownloadAsync</c> call for an already-installed model id takes
    ///     the Issue 3 fast path even when supplied a model whose
    ///     <see cref="ISpeechModel.InstallAsync"/> hook would throw - the hook is never invoked
    ///     at all, since the fast path returns before any staging/install work begins - leaving
    ///     the prior successful install completely untouched. This supersedes this library's
    ///     earlier "second call genuinely re-runs InstallAsync and can report Failed" behavior for
    ///     an already-installed model id.
    /// </summary>
    [Fact]
    public async Task SpeechModelDownloader_DownloadAsync_WithModel_SecondCallAfterPriorSuccess_TakesFastPathAndNeverInvokesInstallHook()
    {
        // Arrange: a successful first install using the bare (modelId, descriptor, ...) overload
        var goodPayload = "good payload"u8.ToArray();
        var goodDescriptor = SingleFileDescriptor(goodPayload, "model.bin");
        var store = NewStore();
        var firstDownloader = new SpeechModelDownloader(store, new FakeModelDownloadClient(goodPayload, 1024));
        var firstResult = await firstDownloader.DownloadAsync(
            "model-a", goodDescriptor, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(SpeechModelDownloadOutcome.Installed, firstResult.Outcome);

        // Act: a second call whose model's InstallAsync hook would throw if it were ever invoked
        var repairPayload = "repair payload"u8.ToArray();
        var repairDescriptor = SingleFileDescriptor(repairPayload, "model.bin");
        var secondDownloader = new SpeechModelDownloader(store, new FakeModelDownloadClient(repairPayload, 1024));
        var throwingModel = new ThrowingInstallHookModel("model-a", repairDescriptor);
        var secondResult = await secondDownloader.DownloadAsync(
            throwingModel, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the fast path took over - Installed, not Failed - the hook never ran, and the
        // prior good install is untouched
        Assert.Equal(SpeechModelDownloadOutcome.Installed, secondResult.Outcome);
        Assert.Null(secondResult.Error);
        Assert.True(store.IsInstalled("model-a"));
        var installedBytes = await File.ReadAllBytesAsync(
            Path.Join(store.GetCurrentDirectory("model-a"), "model.bin"), TestContext.Current.CancellationToken);
        Assert.Equal(goodPayload, installedBytes);
    }

    /// <summary>
    ///     Proves that an <see cref="ISpeechModel.InstallAsync"/> failure of an exception type
    ///     outside the previously narrow allow-list (here <see cref="InvalidOperationException"/>)
    ///     is still converted into a <see cref="SpeechModelDownloadOutcome.Failed"/> result rather
    ///     than escaping <see cref="SpeechModelDownloader.DownloadAsync(ISpeechModel,IProgress{SpeechModelDownloadProgress}?,CancellationToken)"/>,
    ///     per <see cref="ISpeechModel.InstallAsync"/>'s documented "handled identically to a
    ///     download failure" contract for any exception it throws.
    /// </summary>
    [Fact]
    public async Task SpeechModelDownloader_DownloadAsync_WithModel_InstallAsyncThrowsUnlistedExceptionType_ReportsFailed()
    {
        // Arrange: a model whose InstallAsync hook throws an exception type not in the old
        // narrow allow-list (IOException/HttpRequestException/UnauthorizedAccessException/InvalidDataException)
        var payload = "payload"u8.ToArray();
        var descriptor = SingleFileDescriptor(payload, "model.bin");
        var store = NewStore();
        var downloader = new SpeechModelDownloader(store, new FakeModelDownloadClient(payload, 1024));
        var throwingModel = new ThrowingInstallHookModel(
            "model-a", descriptor, () => new InvalidOperationException("Simulated unlisted install-hook failure."));

        // Act
        var result = await downloader.DownloadAsync(throwingModel, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: reported as an honest failure rather than throwing out of DownloadAsync
        Assert.Equal(SpeechModelDownloadOutcome.Failed, result.Outcome);
        Assert.IsType<InvalidOperationException>(result.Error);
        Assert.False(store.IsInstalled("model-a"));
    }

    /// <summary>
    ///     Constructs a store rooted at this test's scratch directory.
    /// </summary>
    private SpeechModelStore NewStore() =>
        new(new SpeechModelStoreOptions { RootPathOverride = _testRoot });

    /// <summary>
    ///     Builds a single-file descriptor whose declared checksum genuinely matches the given
    ///     payload's SHA-256 hash, using a fixed local placeholder URI (never actually
    ///     dereferenced, since the fake client ignores it).
    /// </summary>
    private static SpeechModelDownloadDescriptor SingleFileDescriptor(byte[] payload, string relativeInstallPath) =>
        SingleFileDescriptorFor(payload, relativeInstallPath);

    /// <summary>See <see cref="SingleFileDescriptor"/>.</summary>
    private static SpeechModelDownloadDescriptor SingleFileDescriptorFor(byte[] payload, string relativeInstallPath)
    {
        var checksum = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(payload));
        var file = new SpeechModelDownloadFile(
            new Uri($"https://example.test/{relativeInstallPath}"),
            checksum,
            relativeInstallPath);
        return new SpeechModelDownloadDescriptor([file]);
    }

    /// <summary>
    ///     Fake <see cref="IModelDownloadClient"/> that serves a fixed in-memory payload in
    ///     controlled chunk sizes, optionally canceling mid-transfer to exercise cancellation.
    /// </summary>
    private sealed class FakeModelDownloadClient(
        byte[] payload,
        int chunkSize,
        int? cancelAfterChunks = null,
        CancellationTokenSource? cancellationSource = null) : IModelDownloadClient
    {
        /// <inheritdoc/>
        public async Task DownloadAsync(
            Uri sourceUri,
            Stream destination,
            IProgress<SpeechModelDownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            var offset = 0;
            var chunkNumber = 0;
            while (offset < payload.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();

                chunkNumber++;
                if (cancelAfterChunks is not null && chunkNumber > cancelAfterChunks)
                {
                    if (cancellationSource is not null)
                    {
                        await cancellationSource.CancelAsync();
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var length = Math.Min(chunkSize, payload.Length - offset);
                await destination.WriteAsync(payload.AsMemory(offset, length), cancellationToken);
                offset += length;
                progress?.Report(new SpeechModelDownloadProgress(0, 1, offset, payload.Length));
            }
        }
    }

    /// <summary>
    ///     Fake <see cref="IModelDownloadClient"/> that requires a fixed number of calls to have
    ///     genuinely arrived concurrently before any of them may proceed, used to deterministically
    ///     prove the downloader's cross-model concurrency policy without relying on timing. Serves
    ///     a fixed payload per requested URI so its output still matches each descriptor's
    ///     declared checksum.
    /// </summary>
    private sealed class BarrierModelDownloadClient(IReadOnlyDictionary<Uri, byte[]> payloadsByUri, int expectedConcurrentCalls)
        : IModelDownloadClient
    {
        private readonly TaskCompletionSource _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivedCount;

        /// <inheritdoc/>
        public async Task DownloadAsync(
            Uri sourceUri,
            Stream destination,
            IProgress<SpeechModelDownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrivedCount) == expectedConcurrentCalls)
            {
                _allArrived.TrySetResult();
            }

            // If a regression reintroduced a global single-flight lock, the other expected
            // call(s) would never reach this point while this one is blocked earlier waiting for
            // the (now-global) lock, so this would time out rather than silently pass.
            await _allArrived.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

            var payload = payloadsByUri[sourceUri];
            await destination.WriteAsync(payload, cancellationToken);
            progress?.Report(new SpeechModelDownloadProgress(0, 1, payload.Length, payload.Length));
        }
    }

    /// <summary>
    ///     Fake <see cref="IModelDownloadClient"/> that holds its first call open until the test
    ///     explicitly releases it and records the order in which subsequent calls reach it, used
    ///     to deterministically prove that two concurrent downloads for the same model id are
    ///     serialized rather than run in parallel.
    /// </summary>
    private sealed class OrderTrackingModelDownloadClient(IReadOnlyDictionary<Uri, byte[]> payloadsByUri)
        : IModelDownloadClient
    {
        private readonly TaskCompletionSource _releaseFirstCallGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<int> _entryOrder = [];
        private int _callCount;

        /// <summary>Completes once the first call has reached <see cref="DownloadAsync"/> and is holding it open.</summary>
        public TaskCompletionSource FirstCallEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the number of calls that have reached <see cref="DownloadAsync"/> so far.</summary>
        public int CallCount => Volatile.Read(ref _callCount);

        /// <summary>Gets the order (by 1-based call index) in which calls reached <see cref="DownloadAsync"/>.</summary>
        public IReadOnlyList<int> EntryOrder
        {
            get
            {
                lock (_entryOrder)
                {
                    return [.. _entryOrder];
                }
            }
        }

        /// <summary>Releases the first call, allowing it (and any queued behind it) to proceed.</summary>
        public void ReleaseFirstCall() => _releaseFirstCallGate.TrySetResult();

        /// <inheritdoc/>
        public async Task DownloadAsync(
            Uri sourceUri,
            Stream destination,
            IProgress<SpeechModelDownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            var callIndex = Interlocked.Increment(ref _callCount);
            lock (_entryOrder)
            {
                _entryOrder.Add(callIndex);
            }

            if (callIndex == 1)
            {
                FirstCallEntered.TrySetResult();
                await _releaseFirstCallGate.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }

            var payload = payloadsByUri[sourceUri];
            await destination.WriteAsync(payload, cancellationToken);
            progress?.Report(new SpeechModelDownloadProgress(0, 1, payload.Length, payload.Length));
        }
    }

    /// <summary>
    ///     A trivial <see cref="IProgress{T}"/> implementation that invokes its callback
    ///     synchronously and in-order on the reporting thread.
    /// </summary>
    /// <remarks>
    ///     Unlike <see cref="Progress{T}"/>, which posts each report independently (via a
    ///     captured <see cref="SynchronizationContext"/> or the thread pool) with no ordering
    ///     guarantee across successive calls, this adapter forwards deterministically in the
    ///     exact order <c>Report</c> is invoked - required for this test suite's assertions about
    ///     monotonically increasing byte counts across successive reports.
    /// </remarks>
    private sealed class SynchronousProgress<T>(Action<T> callback) : IProgress<T>
    {
        /// <inheritdoc/>
        public void Report(T value) => callback(value);
    }

    /// <summary>
    ///     Test-only <see cref="ISpeechModel"/> stub recording whether/how its
    ///     <see cref="InstallAsync"/> hook was invoked, used to prove
    ///     <see cref="SpeechModelDownloader"/> invokes it at the correct point in the
    ///     fetch/verify/install sequence.
    /// </summary>
    private sealed class RecordingInstallHookModel(string id, SpeechModelDownloadDescriptor downloadDescriptor)
        : ISpeechModel
    {
        /// <summary>Gets whether <see cref="InstallAsync"/> was ever invoked.</summary>
        public bool InstallAsyncCalled { get; private set; }

        /// <summary>
        ///     Gets whether the staging directory already contained the verified downloaded
        ///     file(s) when <see cref="InstallAsync"/> was invoked.
        /// </summary>
        public bool StagingDirectoryContainedVerifiedFileWhenInstallAsyncRan { get; private set; }

        /// <summary>
        ///     Gets whether a sibling <c>current/</c> directory already existed (it must not)
        ///     when <see cref="InstallAsync"/> was invoked.
        /// </summary>
        public bool CurrentDirectoryExistedWhenInstallAsyncRan { get; private set; }

        /// <inheritdoc/>
        public string Id { get; } = id;

        /// <inheritdoc/>
        public string DisplayName => "Recording Install Hook Model";

        /// <inheritdoc/>
        public SpeechModelRole Role => SpeechModelRole.Recognition;

        /// <inheritdoc/>
        public IReadOnlyList<ISpeechModelParameter> Parameters { get; } = [];

        /// <inheritdoc/>
        public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.None;

        /// <inheritdoc/>
        public SpeechModelDownloadDescriptor DownloadDescriptor { get; } = downloadDescriptor;

        /// <inheritdoc/>
        public Task InstallAsync(string stagedFilesDirectory, CancellationToken cancellationToken = default)
        {
            InstallAsyncCalled = true;
            StagingDirectoryContainedVerifiedFileWhenInstallAsyncRan =
                DownloadDescriptor.Files.All(file => File.Exists(Path.Join(stagedFilesDirectory, file.RelativeInstallPath)));

            // The staging directory's own parent's "current" sibling must not exist yet.
            var currentDirectory = Path.Join(Path.GetDirectoryName(Path.GetDirectoryName(stagedFilesDirectory))!, "current");
            CurrentDirectoryExistedWhenInstallAsyncRan = Directory.Exists(currentDirectory);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    ///     Test-only <see cref="ISpeechModel"/> stub whose <see cref="InstallAsync"/> hook always
    ///     throws, used to prove an install-hook failure is handled identically to a download
    ///     failure - nothing is installed, and a prior successful install is left untouched.
    /// </summary>
    /// <param name="id">This model's stable identifier.</param>
    /// <param name="downloadDescriptor">The files this model declares for download.</param>
    /// <param name="exceptionFactory">
    ///     Produces the exception <see cref="InstallAsync"/> throws, or <see langword="null"/> to
    ///     default to throwing <see cref="IOException"/>. Lets tests prove that
    ///     <see cref="SpeechModelDownloader"/>'s safe-failure handling is not limited to a narrow
    ///     allow-list of exception types.
    /// </param>
    private sealed class ThrowingInstallHookModel(
        string id,
        SpeechModelDownloadDescriptor downloadDescriptor,
        Func<Exception>? exceptionFactory = null)
        : ISpeechModel
    {
        /// <inheritdoc/>
        public string Id { get; } = id;

        /// <inheritdoc/>
        public string DisplayName => "Throwing Install Hook Model";

        /// <inheritdoc/>
        public SpeechModelRole Role => SpeechModelRole.Recognition;

        /// <inheritdoc/>
        public IReadOnlyList<ISpeechModelParameter> Parameters { get; } = [];

        /// <inheritdoc/>
        public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.None;

        /// <inheritdoc/>
        public SpeechModelDownloadDescriptor DownloadDescriptor { get; } = downloadDescriptor;

        /// <inheritdoc/>
        public Task InstallAsync(string stagedFilesDirectory, CancellationToken cancellationToken = default) =>
            throw (exceptionFactory?.Invoke() ?? new IOException("Simulated install-hook failure."));
    }
}
