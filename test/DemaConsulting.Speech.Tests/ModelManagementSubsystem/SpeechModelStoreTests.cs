using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for the <see cref="SpeechModelStore"/> class.
/// </summary>
public sealed class SpeechModelStoreTests : IDisposable
{
    /// <summary>A scratch root directory unique to this test instance, cleaned up on dispose.</summary>
    private readonly string _testRoot;

    /// <summary>
    ///     Initializes a fresh, unique scratch directory for each test.
    /// </summary>
    public SpeechModelStoreTests()
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
    ///     Proves that a store constructed with no options resolves its root under the per-user
    ///     LocalApplicationData folder, per this library's default storage location.
    /// </summary>
    [Fact]
    public void SpeechModelStore_Constructor_NoOptions_ResolvesUnderLocalApplicationData()
    {
        // Arrange & Act
        var store = new SpeechModelStore();

        // Assert
        var expectedRoot = Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DemaConsulting.Speech",
            "Models");
        Assert.Equal(expectedRoot, store.RootPath);
    }

    /// <summary>
    ///     Proves that a host-supplied root path override is used verbatim instead of the default
    ///     LocalApplicationData location.
    /// </summary>
    [Fact]
    public void SpeechModelStore_Constructor_RootPathOverride_UsesOverrideVerbatim()
    {
        // Arrange & Act
        var store = new SpeechModelStore(new SpeechModelStoreOptions { RootPathOverride = _testRoot });

        // Assert
        Assert.Equal(_testRoot, store.RootPath);
    }

    /// <summary>
    ///     Proves that a model with no staged/installed content reports as not installed.
    /// </summary>
    [Fact]
    public void SpeechModelStore_IsInstalled_NoInstall_ReturnsFalse()
    {
        // Arrange
        var store = NewStore();

        // Act & Assert
        Assert.False(store.IsInstalled("model-a"));
    }

    /// <summary>
    ///     Proves that a first install swap creates current/ and a valid manifest without any
    ///     prior current/ to replace.
    /// </summary>
    [Fact]
    public void SpeechModelStore_CompleteInstall_FirstInstall_CreatesCurrentAndManifest()
    {
        // Arrange
        var store = NewStore();
        var (operationId, stagingDirectory) = BeginStagingWithFile(store, "model-a", "payload.bin", "hello");

        // Act
        store.CompleteInstall("model-a", operationId, stagingDirectory, 5);

        // Assert
        Assert.True(store.IsInstalled("model-a"));
        var installedFile = Path.Join(store.GetCurrentDirectory("model-a"), "payload.bin");
        Assert.Equal("hello", File.ReadAllText(installedFile));
    }

    /// <summary>
    ///     Proves that a completed install leaves no empty <c>.tmp</c> staging root behind.
    /// </summary>
    [Fact]
    public void SpeechModelStore_CompleteInstall_RemovesEmptyStagingRoot()
    {
        // Arrange
        var store = NewStore();
        var (operationId, stagingDirectory) = BeginStagingWithFile(store, "model-a", "payload.bin", "hello");

        // Act
        store.CompleteInstall("model-a", operationId, stagingDirectory, 5);

        // Assert
        Assert.False(Directory.Exists(Path.Join(store.GetModelDirectory("model-a"), ".tmp")));
        Assert.True(store.IsInstalled("model-a"));
    }

    /// <summary>
    ///     Proves that a staging root still holding another operation's directory is left alone.
    /// </summary>
    [Fact]
    public void SpeechModelStore_CompleteInstall_OtherOperationStaging_KeepsStagingRoot()
    {
        // Arrange
        var store = NewStore();
        var (otherOperation, otherStaging) = BeginStagingWithFile(store, "model-a", "other.bin", "x");
        var (operationId, stagingDirectory) = BeginStagingWithFile(store, "model-a", "payload.bin", "hello");

        // Act
        store.CompleteInstall("model-a", operationId, stagingDirectory, 5);

        // Assert
        Assert.NotEqual(otherOperation, operationId);
        Assert.True(Directory.Exists(otherStaging));
        Assert.True(store.IsInstalled("model-a"));
    }

    /// <summary>
    ///     Proves that installing over an existing current/ (a repair/re-download) replaces the
    ///     content and the old directory is cleaned up.
    /// </summary>
    [Fact]
    public void SpeechModelStore_CompleteInstall_ExistingCurrent_ReplacesContentAndCleansUpOldDirectory()
    {
        // Arrange: perform a first, successful install
        var store = NewStore();
        var (firstOperationId, firstStaging) = BeginStagingWithFile(store, "model-a", "payload.bin", "version-1");
        store.CompleteInstall("model-a", firstOperationId, firstStaging, 9);

        // Act: perform a second install (repair/re-download) over the existing current/
        var (secondOperationId, secondStaging) = BeginStagingWithFile(store, "model-a", "payload.bin", "version-2");
        store.CompleteInstall("model-a", secondOperationId, secondStaging, 9);

        // Assert: the new content is in place and no replaced-* leftover remains
        var installedFile = Path.Join(store.GetCurrentDirectory("model-a"), "payload.bin");
        Assert.Equal("version-2", File.ReadAllText(installedFile));
        Assert.True(store.IsInstalled("model-a"));
        Assert.Empty(Directory.EnumerateDirectories(store.GetModelDirectory("model-a"), "current.replaced-*"));
    }

    /// <summary>
    ///     Proves that staging a repair download leaves the installed current/ content and its
    ///     installed state untouched until the swap.
    /// </summary>
    [Fact]
    public void SpeechModelStore_BeginStaging_ExistingCurrent_LeavesInstalledContentUntouched()
    {
        // Arrange: perform a first, successful install
        var store = NewStore();
        var (firstOperationId, firstStaging) = BeginStagingWithFile(store, "model-a", "payload.bin", "version-1");
        store.CompleteInstall("model-a", firstOperationId, firstStaging, 9);

        // Act: stage a repair download without completing it
        BeginStagingWithFile(store, "model-a", "payload.bin", "version-2");

        // Assert: the installed content is unchanged and still reports as installed
        var installedFile = Path.Join(store.GetCurrentDirectory("model-a"), "payload.bin");
        Assert.Equal("version-1", File.ReadAllText(installedFile));
        Assert.True(store.IsInstalled("model-a"));
    }

    /// <summary>
    ///     Proves that current/ present without a valid manifest (simulating a crash between the
    ///     swap and the manifest write) is honestly reported as not installed rather than
    ///     falsely "installed".
    /// </summary>
    [Fact]
    public void SpeechModelStore_IsInstalled_CurrentWithoutManifest_ReturnsFalse()
    {
        // Arrange: create current/ directly, bypassing CompleteInstall, so no manifest exists
        var store = NewStore();
        Directory.CreateDirectory(store.GetCurrentDirectory("model-a"));

        // Act & Assert
        Assert.False(store.IsInstalled("model-a"));
    }

    /// <summary>
    ///     Proves that a manifest file containing malformed JSON is treated as "not installed"
    ///     rather than throwing, per <see cref="SpeechModelStore.TryReadManifest"/>'s documented
    ///     contract.
    /// </summary>
    [Fact]
    public void SpeechModelStore_IsInstalled_ManifestIsMalformedJson_ReturnsFalse()
    {
        // Arrange: create current/ and a manifest file that is not valid JSON
        var store = NewStore();
        Directory.CreateDirectory(store.GetCurrentDirectory("model-a"));
        var manifestPath = Path.Join(store.GetModelDirectory("model-a"), "install-manifest.json");
        File.WriteAllText(manifestPath, "not valid json");

        // Act & Assert
        Assert.False(store.IsInstalled("model-a"));
    }

    /// <summary>
    ///     Proves that a manifest file this process is denied read access to is treated as "not
    ///     installed" rather than letting <see cref="UnauthorizedAccessException"/> escape from
    ///     <see cref="SpeechModelStore.IsInstalled"/>.
    /// </summary>
    /// <remarks>
    ///     Denying read access via an explicit ACL deny-rule is only reliably reproducible on
    ///     Windows, so this test is skipped elsewhere rather than asserting platform-specific
    ///     behavior that would not hold.
    /// </remarks>
    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void SpeechModelStore_IsInstalled_ManifestReadAccessDenied_ReturnsFalse()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("ACL-based read-access denial is only reliably reproducible on Windows.");
        }

        // Arrange: create current/ and a manifest file this process is denied read access to
        var store = NewStore();
        Directory.CreateDirectory(store.GetCurrentDirectory("model-a"));
        var manifestPath = Path.Join(store.GetModelDirectory("model-a"), "install-manifest.json");
        File.WriteAllText(
            manifestPath,
            """{"TotalSizeBytes":5,"InstalledAtUtc":"2026-01-01T00:00:00+00:00"}""");

        var fileInfo = new FileInfo(manifestPath);
        var acl = fileInfo.GetAccessControl();
        var user = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
        var denyRule = new System.Security.AccessControl.FileSystemAccessRule(
            user, System.Security.AccessControl.FileSystemRights.Read, System.Security.AccessControl.AccessControlType.Deny);
        acl.SetAccessRule(denyRule);
        fileInfo.SetAccessControl(acl);
        try
        {
            // Act & Assert
            Assert.False(store.IsInstalled("model-a"));
        }
        finally
        {
            // Restore access so the scratch directory can be cleaned up on test disposal
            var restoreAcl = fileInfo.GetAccessControl();
            restoreAcl.RemoveAccessRule(denyRule);
            fileInfo.SetAccessControl(restoreAcl);
        }
    }

    /// <summary>
    ///     Proves that a leftover <c>.tmp/*</c> staging directory from a prior interrupted attempt
    ///     is removed by <see cref="SpeechModelStore.CleanUpLeftovers"/>.
    /// </summary>
    [Fact]
    public void SpeechModelStore_CleanUpLeftovers_LeftoverTmpDirectory_IsRemoved()
    {
        // Arrange
        var store = NewStore();
        var (_, stagingDirectory) = BeginStagingWithFile(store, "model-a", "payload.bin", "partial");
        Assert.True(Directory.Exists(stagingDirectory));

        // Act
        store.CleanUpLeftovers("model-a");

        // Assert
        Assert.False(Directory.Exists(stagingDirectory));
    }

    /// <summary>
    ///     Proves that beginning an install over an installed model stages elsewhere and leaves the
    ///     installed current/ content and manifest untouched until the swap.
    /// </summary>
    [Fact]
    public void SpeechModelStore_BeginInstall_ExistingInstall_LeavesCurrentUntouchedDuringStaging()
    {
        // Arrange
        var store = NewStore();
        var (firstOperationId, firstStaging) = BeginStagingWithFile(store, "model-a", "payload.bin", "version-1");
        store.CompleteInstall("model-a", firstOperationId, firstStaging, 9);

        // Act: stage a second version but do not complete it
        var (_, secondStaging) = BeginStagingWithFile(store, "model-a", "payload.bin", "version-2");

        // Assert
        Assert.NotEqual(store.GetCurrentDirectory("model-a"), secondStaging);
        Assert.True(store.IsInstalled("model-a"));
        Assert.Equal("version-1", File.ReadAllText(Path.Join(store.GetCurrentDirectory("model-a"), "payload.bin")));
    }

    /// <summary>
    ///     Proves that <see cref="SpeechModelStore.CleanUpLeftovers"/> removes a superseded
    ///     <c>current.replaced-*</c> directory while leaving the installed content intact.
    /// </summary>
    [Fact]
    public void SpeechModelStore_CleanUpLeftovers_ReplacedDirectory_IsRemovedAndCurrentKept()
    {
        // Arrange
        var store = NewStore();
        var (operationId, staging) = BeginStagingWithFile(store, "model-a", "payload.bin", "hello");
        store.CompleteInstall("model-a", operationId, staging, 5);
        var replaced = Path.Join(store.GetModelDirectory("model-a"), "current.replaced-old");
        Directory.CreateDirectory(replaced);
        File.WriteAllText(Path.Join(replaced, "stale.bin"), "stale");

        // Act
        store.CleanUpLeftovers("model-a");

        // Assert
        Assert.False(Directory.Exists(replaced));
        Assert.True(store.IsInstalled("model-a"));
    }

    /// <summary>
    ///     Proves that uninstalling an installed model removes current/ and the manifest, and
    ///     the model reports as not installed afterward.
    /// </summary>
    [Fact]
    public void SpeechModelStore_Uninstall_InstalledModel_RemovesCurrentAndManifest()
    {
        // Arrange
        var store = NewStore();
        var (operationId, stagingDirectory) = BeginStagingWithFile(store, "model-a", "payload.bin", "hello");
        store.CompleteInstall("model-a", operationId, stagingDirectory, 5);
        Assert.True(store.IsInstalled("model-a"));

        // Act
        store.Uninstall("model-a");

        // Assert
        Assert.False(store.IsInstalled("model-a"));
        Assert.False(Directory.Exists(store.GetCurrentDirectory("model-a")));
    }

    /// <summary>
    ///     Proves that uninstalling a model with no installed content is a harmless no-op.
    /// </summary>
    [Fact]
    public void SpeechModelStore_Uninstall_NotInstalled_DoesNotThrow()
    {
        // Arrange
        var store = NewStore();

        // Act & Assert
        var exception = Record.Exception(() => store.Uninstall("model-a"));
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that uninstalling a model whose current/ directory contains a file with an open,
    ///     exclusive handle throws <see cref="SpeechModelStoreException"/> rather than silently
    ///     succeeding or crashing with a raw I/O exception.
    /// </summary>
    /// <remarks>
    ///     Windows enforces exclusive-delete semantics for a file opened without
    ///     <see cref="FileShare.Delete"/>, so this scenario is only reliably reproducible on
    ///     Windows; POSIX platforms generally allow removal of open files, so the test is skipped
    ///     elsewhere rather than asserting platform-specific behavior that would not hold.
    /// </remarks>
    [Fact]
    public void SpeechModelStore_Uninstall_OpenFileHandleInCurrent_ThrowsSpeechModelStoreException()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Exclusive-delete blocking semantics are only reliably reproducible on Windows.");
        }

        // Arrange: install a model, then hold an exclusive handle open into current/
        var store = NewStore();
        var (operationId, stagingDirectory) = BeginStagingWithFile(store, "model-a", "payload.bin", "hello");
        store.CompleteInstall("model-a", operationId, stagingDirectory, 5);
        var lockedFile = Path.Join(store.GetCurrentDirectory("model-a"), "payload.bin");

        using var handle = new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Act
        Assert.Throws<SpeechModelStoreException>(() => store.Uninstall("model-a"));

        // Assert: the half-failed uninstall must not read as installed
        Assert.False(store.IsInstalled("model-a"));
    }

    /// <summary>
    ///     Proves that a failure deleting the install-manifest sidecar file (rather than
    ///     <c>current/</c>) is also wrapped in <see cref="SpeechModelStoreException"/>, per
    ///     <see cref="SpeechModelStore.Uninstall"/>'s documented contract, instead of letting a
    ///     raw <see cref="UnauthorizedAccessException"/> escape.
    /// </summary>
    /// <remarks>
    ///     Deleting a read-only file reliably throws <see cref="UnauthorizedAccessException"/> on
    ///     Windows; POSIX platforms govern delete permission through the containing directory
    ///     rather than the file's own read-only attribute, so this scenario is skipped elsewhere
    ///     rather than asserting platform-specific behavior that would not hold.
    /// </remarks>
    [Fact]
    public void SpeechModelStore_Uninstall_ManifestDeleteFails_ThrowsSpeechModelStoreException()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Read-only-file delete blocking semantics are only reliably reproducible on Windows.");
        }

        // Arrange: install a model, then mark its manifest file read-only so deleting it fails
        var store = NewStore();
        var (operationId, stagingDirectory) = BeginStagingWithFile(store, "model-a", "payload.bin", "hello");
        store.CompleteInstall("model-a", operationId, stagingDirectory, 5);
        var manifestPath = Path.Join(store.GetModelDirectory("model-a"), "install-manifest.json");
        File.SetAttributes(manifestPath, FileAttributes.ReadOnly);

        try
        {
            // Act & Assert
            Assert.Throws<SpeechModelStoreException>(() => store.Uninstall("model-a"));
        }
        finally
        {
            // Restore write access so the scratch directory can be cleaned up on test disposal
            File.SetAttributes(manifestPath, FileAttributes.Normal);
        }
    }

    /// <summary>
    ///     Proves that <c>"."</c> and <c>".."</c> model ids are rejected, even though neither
    ///     contains a character <see cref="Path.GetInvalidFileNameChars"/> itself flags - both
    ///     are reserved relative-path segments that <see cref="Path.Join(string,string)"/> would
    ///     otherwise silently normalize into escaping this model's own directory under
    ///     <see cref="SpeechModelStore.RootPath"/> entirely.
    /// </summary>
    /// <param name="modelId">The dot-segment model id to reject.</param>
    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public void SpeechModelStore_GetModelDirectory_DotSegmentModelId_ThrowsArgumentException(string modelId)
    {
        // Arrange
        var store = NewStore();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => store.GetModelDirectory(modelId));
    }

    /// <summary>
    ///     Proves that a field-less <c>{}</c> manifest, which deserializes to default values, is
    ///     treated as malformed and the model reported as not installed.
    /// </summary>
    [Fact]
    public void SpeechModelStore_IsInstalled_ManifestIsEmptyJsonObject_ReturnsFalse()
    {
        // Arrange
        var store = NewStore();
        Directory.CreateDirectory(store.GetCurrentDirectory("model-a"));
        var manifestPath = Path.Join(store.GetModelDirectory("model-a"), "install-manifest.json");
        File.WriteAllText(manifestPath, "{}");

        // Act & Assert
        Assert.False(store.IsInstalled("model-a"));
    }

    /// <summary>
    ///     Proves that when the second move of an install swap fails, the previously installed
    ///     content is restored to <c>current/</c> and the model stays installed.
    /// </summary>
    [Fact]
    public void SpeechModelStore_CompleteInstall_SecondMoveFails_RollsBackPreviousInstall()
    {
        // Arrange: a successful first install
        var store = NewStore();
        var (firstOperationId, firstStaging) = BeginStagingWithFile(store, "model-a", "payload.bin", "version-1");
        store.CompleteInstall("model-a", firstOperationId, firstStaging, 9);
        var missingStaging = Path.Join(store.GetModelDirectory("model-a"), ".tmp", "does-not-exist");

        // Act: a staging directory that does not exist makes the second Directory.Move fail
        Assert.ThrowsAny<IOException>(() =>
            store.CompleteInstall("model-a", "op-2", missingStaging, 9));

        // Assert: the original content is back in current/ and the model is still installed
        Assert.True(store.IsInstalled("model-a"));
        var installedFile = Path.Join(store.GetCurrentDirectory("model-a"), "payload.bin");
        Assert.Equal("version-1", File.ReadAllText(installedFile));
        Assert.Empty(Directory.EnumerateDirectories(store.GetModelDirectory("model-a"), "current.replaced-*"));
    }

    /// <summary>
    ///     Proves that a failed repair swap restores the previous manifest byte-for-byte along with the previous
    ///     content, because the manifest is removed before the swap begins.
    /// </summary>
    [Fact]
    public void SpeechModelStore_CompleteInstall_RepairSwapFails_RestoresPreviousManifest()
    {
        // Arrange
        var store = NewStore();
        var (firstOperationId, firstStaging) = BeginStagingWithFile(store, "model-a", "payload.bin", "version-1");
        store.CompleteInstall("model-a", firstOperationId, firstStaging, 9);
        var manifestPath = Path.Join(store.GetModelDirectory("model-a"), "install-manifest.json");
        var manifestBefore = File.ReadAllText(manifestPath);
        var missingStaging = Path.Join(store.GetModelDirectory("model-a"), ".tmp", "does-not-exist");

        // Act
        Assert.ThrowsAny<IOException>(() => store.CompleteInstall("model-a", "op-2", missingStaging, 123));

        // Assert
        Assert.Equal(manifestBefore, File.ReadAllText(manifestPath));
        Assert.True(store.IsInstalled("model-a"));
    }

    /// <summary>
    ///     Proves that a successful repair replaces the manifest with one describing the new install.
    /// </summary>
    [Fact]
    public void SpeechModelStore_CompleteInstall_Repair_WritesNewManifest()
    {
        // Arrange
        var store = NewStore();
        var (firstOperationId, firstStaging) = BeginStagingWithFile(store, "model-a", "payload.bin", "version-1");
        store.CompleteInstall("model-a", firstOperationId, firstStaging, 9);
        var (secondOperationId, secondStaging) = BeginStagingWithFile(store, "model-a", "payload.bin", "version-2");

        // Act
        store.CompleteInstall("model-a", secondOperationId, secondStaging, 77);

        // Assert
        Assert.Equal(77, store.TryReadManifest("model-a")!.TotalSizeBytes);
    }

    /// <summary>
    ///     Proves that uninstalling leaves another operation's live <c>.tmp</c> staging directory alone.
    /// </summary>
    [Fact]
    public void SpeechModelStore_Uninstall_LiveStagingDirectory_IsLeftAlone()
    {
        // Arrange
        var store = NewStore();
        var (firstOperationId, firstStaging) = BeginStagingWithFile(store, "model-a", "payload.bin", "version-1");
        store.CompleteInstall("model-a", firstOperationId, firstStaging, 9);
        var (_, liveStaging) = BeginStagingWithFile(store, "model-a", "other.bin", "x");

        // Act
        store.Uninstall("model-a");

        // Assert
        Assert.False(store.IsInstalled("model-a"));
        Assert.True(Directory.Exists(liveStaging));
    }

    /// <summary>
    ///     Proves that uninstalling removes superseded <c>current.replaced-*</c> directories.
    /// </summary>
    [Fact]
    public void SpeechModelStore_Uninstall_ReplacedDirectories_AreRemoved()
    {
        // Arrange
        var store = NewStore();
        var (firstOperationId, firstStaging) = BeginStagingWithFile(store, "model-a", "payload.bin", "version-1");
        store.CompleteInstall("model-a", firstOperationId, firstStaging, 9);
        var replaced = Path.Join(store.GetModelDirectory("model-a"), "current.replaced-old");
        Directory.CreateDirectory(replaced);

        // Act
        store.Uninstall("model-a");

        // Assert
        Assert.False(store.IsInstalled("model-a"));
        Assert.False(Directory.Exists(replaced));
    }

    /// <summary>
    ///     Proves that a failed manifest delete during uninstall leaves the model fully installed (nothing is half
    ///     removed), because the manifest is removed before <c>current/</c>.
    /// </summary>
    [Fact]
    public void SpeechModelStore_Uninstall_ManifestDeleteFails_LeavesModelInstalled()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Read-only-file delete blocking semantics are only reliably reproducible on Windows.");
        }

        // Arrange
        var store = NewStore();
        var (operationId, stagingDirectory) = BeginStagingWithFile(store, "model-a", "payload.bin", "hello");
        store.CompleteInstall("model-a", operationId, stagingDirectory, 5);
        var manifestPath = Path.Join(store.GetModelDirectory("model-a"), "install-manifest.json");
        File.SetAttributes(manifestPath, FileAttributes.ReadOnly);

        try
        {
            // Act
            Assert.Throws<SpeechModelStoreException>(() => store.Uninstall("model-a"));

            // Assert
            Assert.True(store.IsInstalled("model-a"));
            Assert.True(File.Exists(Path.Join(store.GetCurrentDirectory("model-a"), "payload.bin")));
        }
        finally
        {
            File.SetAttributes(manifestPath, FileAttributes.Normal);
        }
    }

    /// <summary>
    ///     Proves that a failed asynchronous uninstall reports its failure through the task and leaves the model
    ///     reading as not installed.
    /// </summary>
    [Fact]
    public async Task SpeechModelStore_UninstallAsync_OpenFileHandleInCurrent_FaultsWithSpeechModelStoreException()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Exclusive-delete blocking semantics are only reliably reproducible on Windows.");
        }

        // Arrange
        var store = NewStore();
        var (operationId, stagingDirectory) = BeginStagingWithFile(store, "model-a", "payload.bin", "hello");
        store.CompleteInstall("model-a", operationId, stagingDirectory, 5);
        var lockedFile = Path.Join(store.GetCurrentDirectory("model-a"), "payload.bin");
        using var handle = new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Act
        var task = store.UninstallAsync("model-a", TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<SpeechModelStoreException>(() => task);
        Assert.False(store.IsInstalled("model-a"));
    }

    /// <summary>
    ///     Proves that a leftover staging directory that cannot be removed does not make cleanup throw.
    /// </summary>
    [Fact]
    public void SpeechModelStore_CleanUpLeftovers_LockedLeftoverDirectory_DoesNotThrow()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Exclusive-delete blocking semantics are only reliably reproducible on Windows.");
        }

        // Arrange
        var store = NewStore();
        var (_, stagingDirectory) = BeginStagingWithFile(store, "model-a", "payload.bin", "partial");
        using var handle = new FileStream(
            Path.Join(stagingDirectory, "payload.bin"), FileMode.Open, FileAccess.Read, FileShare.Read);

        // Act
        var exception = Record.Exception(() => store.CleanUpLeftovers("model-a"));

        // Assert
        Assert.Null(exception);
        Assert.True(Directory.Exists(stagingDirectory));
    }

    /// <summary>
    ///     Proves that a manifest that cannot be written after the swap makes the install throw and leaves the model
    ///     reading as not installed, which shows the manifest is written only after the directory rename.
    /// </summary>
    [Fact]
    public void SpeechModelStore_CompleteInstall_ManifestWriteFails_ThrowsAndReadsAsNotInstalled()
    {
        // Arrange: a directory occupying the manifest path makes the manifest write fail
        var store = NewStore();
        var (operationId, stagingDirectory) = BeginStagingWithFile(store, "model-a", "payload.bin", "hello");
        Directory.CreateDirectory(Path.Join(store.GetModelDirectory("model-a"), "install-manifest.json"));

        // Act
        var exception = Record.Exception(() => store.CompleteInstall("model-a", operationId, stagingDirectory, 5));

        // Assert
        Assert.NotNull(exception);
        Assert.True(Directory.Exists(store.GetCurrentDirectory("model-a")));
        Assert.False(store.IsInstalled("model-a"));
    }

    /// <summary>
    ///     Proves that an invalid-file-name-character model id is rejected before anything is created on disk.
    /// </summary>
    [Fact]
    public void SpeechModelStore_GetModelDirectory_InvalidCharacterModelId_ThrowsAndCreatesNothing()
    {
        // Arrange
        var store = NewStore();
        var modelId = "a" + '\0' + "b";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => store.GetModelDirectory(modelId));
        Assert.Throws<ArgumentException>(() => store.BeginStaging(modelId));
        Assert.Throws<ArgumentException>(() => store.Uninstall(modelId));
        Assert.Empty(Directory.GetFileSystemEntries(_testRoot));
    }

    /// <summary>
    ///     Proves that an unsafe operation id or a staging directory outside the model's <c>.tmp</c> area is
    ///     rejected without touching installed content.
    /// </summary>
    [Fact]
    public void SpeechModelStore_CompleteInstall_UnsafeOperationIdOrForeignStaging_ThrowsArgumentException()
    {
        // Arrange
        var store = NewStore();
        var (firstOperationId, firstStaging) = BeginStagingWithFile(store, "model-a", "payload.bin", "version-1");
        store.CompleteInstall("model-a", firstOperationId, firstStaging, 9);
        var (operationId, stagingDirectory) = BeginStagingWithFile(store, "model-a", "payload.bin", "version-2");
        var foreign = Path.Join(_testRoot, "elsewhere");
        Directory.CreateDirectory(foreign);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => store.CompleteInstall("model-a", "..", stagingDirectory, 1));
        Assert.Throws<ArgumentException>(() => store.CompleteInstall("model-a", "a/b", stagingDirectory, 1));
        Assert.Throws<ArgumentException>(() => store.CompleteInstall("model-a", operationId, foreign, 1));
        Assert.Equal("version-1", File.ReadAllText(Path.Join(store.GetCurrentDirectory("model-a"), "payload.bin")));
        Assert.True(Directory.Exists(foreign));
    }

    /// <summary>
    ///     Proves that beginning staging creates a fresh, empty, unique staging directory inside the model's
    ///     <c>.tmp</c> area, including when several operations begin concurrently.
    /// </summary>
    [Fact]
    public async Task SpeechModelStore_BeginStaging_ConcurrentCalls_CreateUniqueEmptyDirectories()
    {
        // Arrange
        var store = NewStore();

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(
            () => store.BeginStaging("model-a"),
            TestContext.Current.CancellationToken)));

        // Assert
        Assert.Equal(16, results.Select(r => r.StagingDirectory).Distinct().Count());
        Assert.All(results, r =>
        {
            Assert.True(Directory.Exists(r.StagingDirectory));
            Assert.Empty(Directory.GetFileSystemEntries(r.StagingDirectory));
            Assert.StartsWith(Path.Join(store.GetModelDirectory("model-a"), ".tmp") + Path.DirectorySeparatorChar, r.StagingDirectory);
        });
    }

    /// <summary>
    ///     Proves that abandoning a staging directory deletes it and leaves installed content untouched.
    /// </summary>
    [Fact]
    public void SpeechModelStore_AbandonStaging_StagingDirectory_IsDeletedAndCurrentKept()
    {
        // Arrange
        var store = NewStore();
        var (firstOperationId, firstStaging) = BeginStagingWithFile(store, "model-a", "payload.bin", "version-1");
        store.CompleteInstall("model-a", firstOperationId, firstStaging, 9);
        var (_, staging) = BeginStagingWithFile(store, "model-a", "partial.bin", "x");

        // Act
        SpeechModelStore.AbandonStaging(staging);

        // Assert
        Assert.False(Directory.Exists(staging));
        Assert.True(store.IsInstalled("model-a"));
    }

    /// <summary>
    ///     Proves that a whitespace-only root path override falls back to the default root.
    /// </summary>
    [Fact]
    public void SpeechModelStore_Constructor_WhitespaceRootPathOverride_UsesDefaultRoot()
    {
        // Arrange & Act
        var store = new SpeechModelStore(new SpeechModelStoreOptions { RootPathOverride = "   " });

        // Assert
        Assert.Equal(new SpeechModelStore().RootPath, store.RootPath);
    }
    /// <summary>
    ///     Proves that an invalid model id is rejected by <see cref="SpeechModelStore.Uninstall"/>.
    /// </summary>
    /// <param name="modelId">The invalid model id.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("..")]
    public void SpeechModelStore_Uninstall_InvalidModelId_ThrowsArgumentException(string modelId)
    {
        // Arrange
        var store = NewStore();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => store.Uninstall(modelId));
    }

    /// <summary>
    ///     Proves that the asynchronous uninstall removes current/ and the manifest like the synchronous one.
    /// </summary>
    [Fact]
    public async Task SpeechModelStore_UninstallAsync_InstalledModel_RemovesCurrentAndManifest()
    {
        // Arrange
        var store = NewStore();
        var (operationId, stagingDirectory) = BeginStagingWithFile(store, "model-a", "payload.bin", "hello");
        store.CompleteInstall("model-a", operationId, stagingDirectory, 5);

        // Act
        await store.UninstallAsync("model-a", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(store.IsInstalled("model-a"));
        Assert.False(Directory.Exists(store.GetCurrentDirectory("model-a")));
    }

    /// <summary>
    ///     Proves that the asynchronous uninstall rejects an invalid id at the call site and honors a canceled token
    ///     without removing anything.
    /// </summary>
    [Fact]
    public async Task SpeechModelStore_UninstallAsync_InvalidIdAndCanceledToken_AreRejected()
    {
        // Arrange
        var store = NewStore();
        var (operationId, stagingDirectory) = BeginStagingWithFile(store, "model-a", "payload.bin", "hello");
        store.CompleteInstall("model-a", operationId, stagingDirectory, 5);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.UninstallAsync("..", TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.UninstallAsync("model-a", cts.Token));
        Assert.True(store.IsInstalled("model-a"));
    }

    /// <summary>
    ///     Proves that the asynchronous leftover cleanup honors a canceled token without deleting anything.
    /// </summary>
    [Fact]
    public async Task SpeechModelStore_CleanUpLeftoversAsync_CanceledToken_ThrowsWithoutDeleting()
    {
        // Arrange
        var store = NewStore();
        var (_, stagingDirectory) = BeginStagingWithFile(store, "model-a", "payload.bin", "partial");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.CleanUpLeftoversAsync("model-a", cts.Token));
        Assert.True(Directory.Exists(stagingDirectory));
    }

    /// <summary>
    ///     Proves that the asynchronous leftover cleanup removes a leftover staging directory and rejects an invalid id.
    /// </summary>
    [Fact]
    public async Task SpeechModelStore_CleanUpLeftoversAsync_LeftoverTmpDirectory_IsRemoved()
    {
        // Arrange
        var store = NewStore();
        var (_, stagingDirectory) = BeginStagingWithFile(store, "model-a", "payload.bin", "partial");

        // Act
        await store.CleanUpLeftoversAsync("model-a", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Directory.Exists(stagingDirectory));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.CleanUpLeftoversAsync("", TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that a null model id is rejected.
    /// </summary>
    [Fact]
    public void SpeechModelStore_GetModelDirectory_NullModelId_ThrowsArgumentNullException()
    {
        // Arrange
        var store = NewStore();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => store.GetModelDirectory(null!));
    }

    /// <summary>
    ///     Constructs a store rooted at this test's scratch directory.
    /// </summary>
    private SpeechModelStore NewStore() =>
        new(new SpeechModelStoreOptions { RootPathOverride = _testRoot });

    /// <summary>
    ///     Begins staging for a model and writes one text file into the staging directory,
    ///     returning the operation id and staging directory ready for
    ///     <see cref="SpeechModelStore.CompleteInstall"/>.
    /// </summary>
    private static (string OperationId, string StagingDirectory) BeginStagingWithFile(
        SpeechModelStore store,
        string modelId,
        string relativePath,
        string content)
    {
        var (operationId, stagingDirectory) = store.BeginStaging(modelId);
        File.WriteAllText(Path.Join(stagingDirectory, relativePath), content);
        return (operationId, stagingDirectory);
    }
}
