using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for the public <see cref="SpeechModelBundle"/> declaration type: construction rules, the download
///     descriptor it produces, and installing a set of bundles.
/// </summary>
public sealed class SpeechModelBundleTests : IDisposable
{
    /// <summary>A placeholder bundle checksum (construction never dereferences it).</summary>
    private const string Checksum = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>A scratch directory unique to this test instance.</summary>
    private readonly string _root = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Tests", Guid.NewGuid().ToString("N"));

    /// <summary>Deletes the scratch directory.</summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup only.
        }
    }

    /// <summary>Creates a bundle on the models release with the given file name and vendor paths.</summary>
    private static SpeechModelBundle Bundle(string fileName, params string[] paths) =>
        new(
            new Uri(SpeechModelBundle.ModelsReleaseBaseUri, fileName),
            Checksum,
            ModelBundleTestZips.ContentsFor(paths.Select(path => (path, "x"u8.ToArray()))));

    /// <summary>
    ///     Proves that the models release base URI is the project's long-lived <c>models</c> release.
    /// </summary>
    [Fact]
    public void SpeechModelBundle_ModelsReleaseBaseUri_IsProjectModelsRelease()
    {
        // Act
        var baseUri = SpeechModelBundle.ModelsReleaseBaseUri;

        // Assert
        Assert.Equal("https://github.com/demaconsulting/Speech/releases/download/models/", baseUri.AbsoluteUri);
    }

    /// <summary>
    ///     Proves that a valid bundle exposes its file name, name, contents and a download file that fetches the zip
    ///     under its file name with the zip pin.
    /// </summary>
    [Fact]
    public void SpeechModelBundle_Constructor_ValidArguments_ExposesNameFileNameAndDownloadFile()
    {
        // Arrange
        var contents = ModelBundleTestZips.ContentsFor(ModelBundleTestZips.VendorFiles);
        var uri = new Uri(SpeechModelBundle.ModelsReleaseBaseUri, "kokoro-en-v1.0-int8-b1.zip");

        // Act
        var bundle = new SpeechModelBundle(uri, Checksum, contents);

        // Assert
        Assert.Equal(uri, bundle.Uri);
        Assert.Equal(Checksum, bundle.Sha256Checksum);
        Assert.Equal("kokoro-en-v1.0-int8-b1.zip", bundle.FileName);
        Assert.Equal("kokoro-en-v1.0-int8-b1", bundle.Name);
        Assert.Same(contents, bundle.Contents);
        Assert.Equal(uri, bundle.DownloadFile.Uri);
        Assert.Equal(Checksum, bundle.DownloadFile.Sha256Checksum);
        Assert.Equal("kokoro-en-v1.0-int8-b1.zip", bundle.DownloadFile.RelativeInstallPath);
    }

    /// <summary>
    ///     Proves that file names following the bundle naming rule are accepted, including the planned names.
    /// </summary>
    /// <param name="fileName">A conforming bundle file name.</param>
    [Theory]
    [InlineData("kokoro-en-v1.0-int8-b1.zip")]
    [InlineData("nemotron-3.5-asr-streaming-0.6b-20261011-int4-b1.zip")]
    [InlineData("chatterbox-turbo-lm-20261014-fp16-b1.zip")]
    [InlineData("chatterbox-turbo-aux-20261014-fp32-b12.zip")]
    public void SpeechModelBundle_Constructor_FileNameFollowsNamingRule_IsAccepted(string fileName)
    {
        // Act
        var bundle = Bundle(fileName, "model.onnx");

        // Assert
        Assert.Equal(fileName, bundle.FileName);
    }

    /// <summary>
    ///     Proves that file names breaking the bundle naming rule are rejected: uppercase, an unknown quantization, a
    ///     missing or zero revision, a leading-zero revision, another extension, and an empty token.
    /// </summary>
    /// <param name="fileName">A non-conforming file name.</param>
    [Theory]
    [InlineData("Kokoro-en-v1.0-int8-b1.zip")]
    [InlineData("kokoro-en-v1.0-q4-b1.zip")]
    [InlineData("kokoro-en-v1.0-int8.zip")]
    [InlineData("kokoro-en-v1.0-int8-b0.zip")]
    [InlineData("kokoro-en-v1.0-int8-b01.zip")]
    [InlineData("kokoro-en-v1.0-int8-b1.tar.gz")]
    [InlineData("kokoro--en-int8-b1.zip")]
    [InlineData("kokoro_en-int8-b1.zip")]
    public void SpeechModelBundle_Constructor_FileNameBreaksNamingRule_ThrowsArgumentException(string fileName)
    {
        // Act / Assert
        var exception = Assert.Throws<ArgumentException>(() => Bundle(fileName, "model.onnx"));
        Assert.Equal("uri", exception.ParamName);
    }

    /// <summary>
    ///     Proves that a non-HTTPS URI, or one with a query or fragment, is rejected.
    /// </summary>
    /// <param name="uri">The rejected URI.</param>
    [Theory]
    [InlineData("http://example.invalid/kokoro-en-v1.0-int8-b1.zip")]
    [InlineData("https://example.invalid/kokoro-en-v1.0-int8-b1.zip?x=1")]
    [InlineData("https://example.invalid/kokoro-en-v1.0-int8-b1.zip#frag")]
    public void SpeechModelBundle_Constructor_NonHttpsOrQueryUri_ThrowsArgumentException(string uri)
    {
        // Arrange
        var contents = ModelBundleTestZips.ContentsFor(ModelBundleTestZips.VendorFiles);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => new SpeechModelBundle(new Uri(uri), Checksum, contents));
    }

    /// <summary>
    ///     Proves that a malformed zip checksum is rejected.
    /// </summary>
    [Fact]
    public void SpeechModelBundle_Constructor_InvalidChecksum_ThrowsArgumentException()
    {
        // Arrange
        var contents = ModelBundleTestZips.ContentsFor(ModelBundleTestZips.VendorFiles);
        var uri = new Uri(SpeechModelBundle.ModelsReleaseBaseUri, "kokoro-en-v1.0-int8-b1.zip");

        // Act / Assert
        Assert.Throws<ArgumentException>(() => new SpeechModelBundle(uri, "not-a-sha", contents));
    }

    /// <summary>
    ///     Proves that null arguments are rejected.
    /// </summary>
    [Fact]
    public void SpeechModelBundle_Constructor_NullArgument_ThrowsArgumentNullException()
    {
        // Arrange
        var contents = ModelBundleTestZips.ContentsFor(ModelBundleTestZips.VendorFiles);
        var uri = new Uri(SpeechModelBundle.ModelsReleaseBaseUri, "kokoro-en-v1.0-int8-b1.zip");

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new SpeechModelBundle(null!, Checksum, contents));
        Assert.Throws<ArgumentNullException>(() => new SpeechModelBundle(uri, null!, contents));
        Assert.Throws<ArgumentNullException>(() => new SpeechModelBundle(uri, Checksum, null!));
    }

    /// <summary>
    ///     Proves that vendor paths reserved by the bundle itself, colliding within the bundle, or not portable are
    ///     rejected: the licenses folder, a metadata entry name, the zip's own file name, a case-only duplicate, a
    ///     file that is another file's parent directory, and a reserved device name.
    /// </summary>
    /// <param name="first">The first vendor path.</param>
    /// <param name="second">The second vendor path.</param>
    [Theory]
    [InlineData("licenses/x.txt", "model.onnx")]
    [InlineData("LICENSES/x.txt", "model.onnx")]
    [InlineData("model.json", "model.onnx")]
    [InlineData("notice", "model.onnx")]
    [InlineData("kokoro-en-v1.0-int8-b1.zip", "model.onnx")]
    [InlineData("Model.onnx", "model.onnx")]
    [InlineData("onnx", "onnx/model.onnx")]
    [InlineData("voices/con.bin", "model.onnx")]
    public void SpeechModelBundle_Constructor_ReservedOrCollidingContentsPath_ThrowsArgumentException(string first, string second)
    {
        // Act / Assert
        var exception = Assert.Throws<ArgumentException>(() => Bundle("kokoro-en-v1.0-int8-b1.zip", first, second));
        Assert.Equal("contents", exception.ParamName);
    }

    /// <summary>
    ///     Proves that the descriptor of a multi-bundle model lists each bundle's zip, in order, as the download files.
    /// </summary>
    [Fact]
    public void SpeechModelBundle_CreateDescriptor_TwoBundles_ListsEachZip()
    {
        // Arrange
        var lm = Bundle("chatterbox-turbo-lm-20261014-fp16-b1.zip", "onnx/language_model_fp16.onnx");
        var aux = Bundle("chatterbox-turbo-aux-20261014-int4-b1.zip", "onnx/embed_tokens_q4.onnx");

        // Act
        var descriptor = SpeechModelBundle.CreateDescriptor([lm, aux]);

        // Assert
        Assert.Equal([lm.DownloadFile, aux.DownloadFile], descriptor.Files);
    }

    /// <summary>
    ///     Proves that bundles whose vendor paths collide, which share a name, or whose zip name equals another
    ///     bundle's vendor path are rejected.
    /// </summary>
    [Fact]
    public void SpeechModelBundle_CreateDescriptor_CollidingBundles_ThrowsArgumentException()
    {
        // Arrange
        var a = Bundle("a-v1-int8-b1.zip", "onnx/model.onnx");
        var sameFile = Bundle("b-v1-int8-b1.zip", "ONNX/model.onnx");
        var sameName = Bundle("a-v1-int8-b1.zip", "voices/x.bin");
        var zipNamedLikeFile = Bundle("c-v1-int8-b1.zip", "a-v1-int8-b1.zip");

        // Act / Assert
        Assert.Throws<ArgumentException>(() => SpeechModelBundle.CreateDescriptor([a, sameFile]));
        Assert.Throws<ArgumentException>(() => SpeechModelBundle.CreateDescriptor([a, sameName]));
        Assert.Throws<ArgumentException>(() => SpeechModelBundle.CreateDescriptor([a, zipNamedLikeFile]));
    }

    /// <summary>
    ///     Proves that a null, empty, or null-containing bundle list is rejected.
    /// </summary>
    [Fact]
    public void SpeechModelBundle_CreateDescriptor_NullOrEmptyBundles_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => SpeechModelBundle.CreateDescriptor(null!));
        Assert.Throws<ArgumentException>(() => SpeechModelBundle.CreateDescriptor([]));
        Assert.Throws<ArgumentException>(() => SpeechModelBundle.CreateDescriptor([null!]));
    }

    /// <summary>
    ///     Proves that installing a two-bundle model extracts both bundles' vendor files side by side, keeps each
    ///     bundle's metadata in its own <c>licenses/{name}/</c> folder, and deletes both zips.
    /// </summary>
    [Fact]
    public async Task SpeechModelBundle_InstallAsync_TwoBundles_InstallsBothWithSeparateLicenseFolders()
    {
        // Arrange: two component bundles staged as the downloader would stage them
        var staging = Path.Join(_root, "staging");
        IReadOnlyList<(string Path, byte[] Content)> lmFiles = [("onnx/language_model.onnx", "lm"u8.ToArray())];
        IReadOnlyList<(string Path, byte[] Content)> auxFiles = [("onnx/embed_tokens.onnx", "aux"u8.ToArray())];
        var lmZip = ModelBundleTestZips.BuildZip(ModelBundleTestZips.BundleEntries(lmFiles, "test-lm-v1-fp16-b1"));
        var auxZip = ModelBundleTestZips.BuildZip(ModelBundleTestZips.BundleEntries(auxFiles, "test-aux-v1-int4-b1", includeNotice: false));
        var lm = ModelBundleTestZips.BundleFor(lmZip, lmFiles, "test-lm-v1-fp16-b1.zip");
        var aux = ModelBundleTestZips.BundleFor(auxZip, auxFiles, "test-aux-v1-int4-b1.zip");
        ModelBundleTestZips.Stage(staging, lmZip, lm.FileName);
        ModelBundleTestZips.Stage(staging, auxZip, aux.FileName);

        // Act
        await SpeechModelBundle.InstallAsync([lm, aux], staging, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                "licenses/test-aux-v1-int4-b1/LICENSE",
                "licenses/test-aux-v1-int4-b1/README.txt",
                "licenses/test-aux-v1-int4-b1/model.json",
                "licenses/test-lm-v1-fp16-b1/LICENSE",
                "licenses/test-lm-v1-fp16-b1/NOTICE",
                "licenses/test-lm-v1-fp16-b1/README.txt",
                "licenses/test-lm-v1-fp16-b1/model.json",
                "onnx/embed_tokens.onnx",
                "onnx/language_model.onnx",
            ],
            ModelBundleTestZips.ListFiles(staging));
    }

    /// <summary>
    ///     Proves that an empty staging directory argument is rejected.
    /// </summary>
    [Fact]
    public async Task SpeechModelBundle_InstallAsync_EmptyStagingDirectory_ThrowsArgumentException()
    {
        // Arrange
        var bundle = Bundle("kokoro-en-v1.0-int8-b1.zip", "model.onnx");

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            SpeechModelBundle.InstallAsync([bundle], string.Empty, TestContext.Current.CancellationToken));
    }
}
