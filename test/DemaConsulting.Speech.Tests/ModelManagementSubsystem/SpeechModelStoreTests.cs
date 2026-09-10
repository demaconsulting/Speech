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
        File.WriteAllText(manifestPath, "{}");

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

        // Act & Assert
        Assert.Throws<SpeechModelStoreException>(() => store.Uninstall("model-a"));
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
