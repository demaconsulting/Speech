using System.Text.Json;
using System.Text.RegularExpressions;
using DemaConsulting.Speech.ModelBundler.Tests.Fakes;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.NemotronStt.ModelManagementSubsystem;

namespace DemaConsulting.Speech.ModelBundler.Tests;

/// <summary>
///     Unit tests for <see cref="BundleDefinitions"/> and <see cref="BundleCatalog"/>: the shipped <c>bundles.json</c>
///     declares exactly the compiled bundles with fixed dates and license texts, the loader rejects malformed
///     definitions, and the compiled contents are the packages' own download pins.
/// </summary>
public sealed partial class BundleDefinitionsTests
{
    /// <summary>A valid definition document whose name, date and license file a test can replace.</summary>
    private const string Template =
        """
        {
          "bundles": [
            {
              "name": "NAME",
              "model": "fixture-model",
              "upstreamRelease": "r1",
              "quant": "int8",
              "bundleRevision": 1,
              "date": "DATE",
              "modelIds": [ "MODEL_ID_TOKEN" ],
              "upstream": { "repository": "https://example.invalid/fixture", "revision": "main" },
              "licenses": [ { "name": "MIT", "url": "https://opensource.org/license/mit", "appliesTo": "all", "textFile": "TEXTFILE" } ],
              "attribution": "Fixture.",
              "notice": [ "Fixture." ]
            }
          ]
        }
        """;

    /// <summary>Validates that the shipped definitions declare exactly the bundles the packages compile.</summary>
    [Fact]
    public void BundleDefinitions_Load_ShippedFile_DeclaresEveryCompiledBundle()
    {
        // Arrange / Act: load the shipped definitions and the compiled contents
        var definitions = BundleDefinitions.Load(BundleDefinitions.GetDefaultPath());
        var contents = BundleCatalog.GetContents();

        // Assert: the same seven bundle names
        Assert.Equal(7, definitions.Count);
        Assert.Equal(contents.Keys.Order(StringComparer.Ordinal), definitions.Select(definition => definition.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>Validates the shipped dates, upstream revisions and license texts.</summary>
    [Fact]
    public void BundleDefinitions_Load_ShippedFile_HasFixedDatesRevisionsAndLicenseTexts()
    {
        // Arrange / Act: load the shipped definitions
        var definitions = BundleDefinitions.Load(BundleDefinitions.GetDefaultPath()).ToDictionary(definition => definition.Name);

        // Assert: one fixed date, a pinned-snapshot revision and non-empty LF license texts everywhere
        Assert.All(definitions.Values, definition =>
        {
            Assert.Equal(new DateOnly(2026, 11, 22), definition.Date);
            Assert.Matches(PinnedRevisionPattern(), definition.UpstreamRevision);
            Assert.All(definition.Licenses, license => Assert.DoesNotContain('\r', license.Text));
        });

        // Assert: the per-model licenses
        Assert.Contains("Apache License", definitions["kokoro-en-v1.0-int8-b1"].Licenses[0].Text, StringComparison.Ordinal);
        Assert.Equal(
            ["OpenMDW-1.1", "MIT"],
            definitions["nemotron-3.5-asr-streaming-0.6b-20261011-int4-b1"].Licenses.Select(license => license.Name));
        Assert.Contains("Copyright (c) 2025 Resemble AI", definitions["chatterbox-turbo-lm-20261014-fp16-b1"].Licenses[0].Text, StringComparison.Ordinal);
    }

    /// <summary>Validates that a name that is not the composition of its parts is rejected.</summary>
    [Fact]
    public void BundleDefinitions_Load_NameDiffersFromParts_ThrowsInvalidDataException()
    {
        // Arrange: a definition named differently from its parts
        using var directory = new TemporaryDirectory();
        var path = WriteDefinitions(directory.Path, "other-model-r1-int8-b1", "2026-11-22", "MIT text\n");

        // Act / Assert: the definition is rejected
        Assert.Throws<InvalidDataException>(() => BundleDefinitions.Load(path));
    }

    /// <summary>Validates that a date that is not yyyy-MM-dd is rejected.</summary>
    [Fact]
    public void BundleDefinitions_Load_InvalidDate_ThrowsInvalidDataException()
    {
        // Arrange: a definition with a wall-clock style date
        using var directory = new TemporaryDirectory();
        var path = WriteDefinitions(directory.Path, TestBundles.Name, "22/11/2026", "MIT text\n");

        // Act / Assert: the definition is rejected
        Assert.Throws<InvalidDataException>(() => BundleDefinitions.Load(path));
    }

    /// <summary>Validates that a license text with CRLF line endings is normalized to LF with one trailing newline.</summary>
    [Fact]
    public void BundleDefinitions_Load_CrlfLicenseText_IsNormalizedToLf()
    {
        // Arrange: a license file checked out with CRLF line endings
        using var directory = new TemporaryDirectory();
        var path = WriteDefinitions(directory.Path, TestBundles.Name, "2026-11-22", "Line one\r\nLine two\r\n\r\n");

        // Act: load it
        var definition = Assert.Single(BundleDefinitions.Load(path));

        // Assert: LF only, one trailing newline
        Assert.Equal("Line one\nLine two\n", definition.Licenses[0].Text);
    }

    /// <summary>Validates that a license text path climbing out of the definitions directory is rejected.</summary>
    /// <param name="textFile">The traversing license text path.</param>
    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("licenses/../../secret.txt")]
    [InlineData("licenses\\..\\..\\secret.txt")]
    public void BundleDefinitions_Load_LicenseTextFileTraversesUp_ThrowsInvalidDataException(string textFile)
    {
        // Arrange: a readable file just outside the definitions directory
        using var directory = new TemporaryDirectory();
        File.WriteAllText(directory.Combine("secret.txt"), "Not a license.\n");
        Directory.CreateDirectory(directory.Combine("definitions", "licenses"));
        var path = WriteDefinitions(directory.Combine("definitions"), TestBundles.Name, "2026-11-22", "MIT text\n", textFile);

        // Act / Assert: the path is rejected as invalid data, never read
        var exception = Assert.Throws<InvalidDataException>(() => BundleDefinitions.Load(path));
        Assert.Contains(textFile, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Validates that a rooted license text path, which would replace the definitions directory, is rejected.</summary>
    [Fact]
    public void BundleDefinitions_Load_RootedLicenseTextFile_ThrowsInvalidDataException()
    {
        // Arrange: an existing file outside the definitions directory, named by its absolute path
        using var outside = new TemporaryDirectory();
        var secret = outside.Combine("secret.txt");
        File.WriteAllText(secret, "Not a license.\n");
        using var directory = new TemporaryDirectory();
        var path = WriteDefinitions(directory.Path, TestBundles.Name, "2026-11-22", "MIT text\n", secret);

        // Act / Assert: the path is rejected as invalid data, never read
        Assert.Throws<InvalidDataException>(() => BundleDefinitions.Load(path));
    }

    /// <summary>Validates that a license text in a subdirectory of the definitions directory is accepted.</summary>
    [Fact]
    public void BundleDefinitions_Load_LicenseTextFileInSubdirectory_LoadsText()
    {
        // Arrange: the license text under licenses/, as the shipped definitions declare
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(directory.Combine("licenses"));
        File.WriteAllText(directory.Combine("licenses", "MIT.txt"), "Nested text\n");
        var path = WriteDefinitions(directory.Path, TestBundles.Name, "2026-11-22", "MIT text\n", "licenses/MIT.txt");

        // Act: load it
        var definition = Assert.Single(BundleDefinitions.Load(path));

        // Assert: the nested text is read
        Assert.Equal("Nested text\n", definition.Licenses[0].Text);
    }

    /// <summary>Validates that a model id that is not one safe directory name is rejected.</summary>
    /// <param name="modelId">The unsafe model id.</param>
    [Theory]
    [InlineData("../outside")]
    [InlineData("..")]
    [InlineData("/absolute")]
    [InlineData("C:\\absolute")]
    [InlineData("nested/model")]
    [InlineData("Upper-Case")]
    [InlineData("double--separator")]
    public void BundleDefinitions_Load_UnsafeModelId_ThrowsInvalidDataException(string modelId)
    {
        // Arrange: a definition whose model id could escape the source store
        using var directory = new TemporaryDirectory();
        var path = WriteDefinitions(directory.Path, TestBundles.Name, "2026-11-22", "MIT text\n", modelId: modelId);

        // Act / Assert: the definition is rejected
        var exception = Assert.Throws<InvalidDataException>(() => BundleDefinitions.Load(path));
        Assert.Contains(modelId, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Validates that the model ids of the shipped catalog forms are accepted.</summary>
    /// <param name="modelId">A catalog-style model id.</param>
    [Theory]
    [InlineData("kokoro-onnx-v1_0-en")]
    [InlineData("nemotron-3.5-asr-streaming-0.6b-onnx-int4")]
    [InlineData("chatterbox-turbo-onnx-en-lm-fp32-aux-q4")]
    public void BundleDefinitions_Load_CatalogModelId_IsAccepted(string modelId)
    {
        // Arrange: a definition with a catalog-style model id
        using var directory = new TemporaryDirectory();
        var path = WriteDefinitions(directory.Path, TestBundles.Name, "2026-11-22", "MIT text\n", modelId: modelId);

        // Act: load it
        var definition = Assert.Single(BundleDefinitions.Load(path));

        // Assert: the id is kept
        Assert.Equal([modelId], definition.ModelIds);
    }

    /// <summary>Validates that Kokoro's and Nemotron's compiled bundle contents are their published bundles' contents.</summary>
    [Fact]
    public void BundleCatalog_GetContents_SingleBundleModels_EqualPublishedBundleContents()
    {
        // Arrange / Act: read the compiled contents
        var contents = BundleCatalog.GetContents();

        // Assert: the bundle files are exactly the files each model's published bundle installs (no duplicated pins)
        var kokoro = Assert.Single(OnnxKokoroEnglishSynthesisModel.Bundles);
        Assert.Equal(Describe(kokoro.Contents), Describe(contents[kokoro.Name]));
        Assert.Equal(30, contents["kokoro-en-v1.0-int8-b1"].Files.Count);
        var nemotron = Assert.Single(OnnxNemotronMultilingualRecognitionModel.Bundles);
        Assert.Equal(Describe(nemotron.Contents), Describe(contents[nemotron.Name]));
        Assert.Equal(11, contents["nemotron-3.5-asr-streaming-0.6b-20261011-int4-b1"].Files.Count);
    }

    /// <summary>Validates that every Chatterbox Turbo variant installs exactly its language-model and auxiliary bundles' files.</summary>
    /// <param name="languageModel">The language-model variant.</param>
    /// <param name="auxiliary">The auxiliary-model variant.</param>
    /// <param name="lmBundle">The expected language-model bundle.</param>
    /// <param name="auxBundle">The expected auxiliary bundle.</param>
    [Theory]
    [InlineData(ChatterboxTurboLanguageModelVariant.Fp16, ChatterboxTurboAuxiliaryModelVariant.Q4, "chatterbox-turbo-lm-20261014-fp16-b1", "chatterbox-turbo-aux-20261014-int4-b1")]
    [InlineData(ChatterboxTurboLanguageModelVariant.Fp32, ChatterboxTurboAuxiliaryModelVariant.Fp16, "chatterbox-turbo-lm-20261014-fp32-b1", "chatterbox-turbo-aux-20261014-fp16-b1")]
    [InlineData(ChatterboxTurboLanguageModelVariant.Fp16, ChatterboxTurboAuxiliaryModelVariant.Fp32, "chatterbox-turbo-lm-20261014-fp16-b1", "chatterbox-turbo-aux-20261014-fp32-b1")]
    public void BundleCatalog_GetContents_ChatterboxVariant_EqualsVariantVendorFiles(
        ChatterboxTurboLanguageModelVariant languageModel,
        ChatterboxTurboAuxiliaryModelVariant auxiliary,
        string lmBundle,
        string auxBundle)
    {
        // Arrange: the variant's model and the compiled contents
        var model = new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { LanguageModelVariant = languageModel, AuxiliaryModelVariant = auxiliary });
        var contents = BundleCatalog.GetContents();

        // Act: join the two component bundles
        var joined = contents[lmBundle].Files.Concat(contents[auxBundle].Files).ToList();

        // Assert: the variant installs exactly those eight files, in that order, whichever source it downloads from
        Assert.Equal(8, joined.Count);
        Assert.Equal(Describe(VendorFilesOf(model)), Describe(new SpeechModelDownloadDescriptor(joined)));
    }

    /// <summary>Validates that the published zip pins are exactly the packages' published bundles, so a rebuild reports MATCH.</summary>
    [Fact]
    public void BundleCatalog_GetPublishedChecksums_ListsEveryPublishedBundle()
    {
        // Arrange / Act: read the published pins
        var pins = BundleCatalog.GetPublishedChecksums();

        // Assert: the four published bundles carry their released zip SHA-256s, and no deferred bundle has a pin
        Assert.Equal(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["kokoro-en-v1.0-int8-b1"] = "c61bb8da1433020071782071fe7f2eb6c9d03d41b9b9672f6b1201b4afd61e9c",
                ["nemotron-3.5-asr-streaming-0.6b-20261011-int4-b1"] = "733375bb604712afac6c4adbd6a9149ebc414fc9a5bee39ed3bdfc3c104048b2",
                ["chatterbox-turbo-lm-20261014-fp16-b1"] = "2c2e05d62e18453fa8c726d57b8c970948f5d693400f303fc2e9dc97838a1584",
                ["chatterbox-turbo-aux-20261014-int4-b1"] = "1981e10e65bb4f2954b99dee6c9b9e74242428cd87ed0dadf085ca6f8f229581",
            }.OrderBy(entry => entry.Key, StringComparer.Ordinal),
            pins.OrderBy(entry => entry.Key, StringComparer.Ordinal));
        Assert.All(pins.Keys, name => Assert.Contains(name, BundleCatalog.GetContents().Keys));
    }

    /// <summary>Describes a descriptor as comparable strings (URL, pin and install path of each file, in order).</summary>
    /// <param name="descriptor">The descriptor.</param>
    /// <returns>One string per file.</returns>
    private static List<string> Describe(SpeechModelDownloadDescriptor descriptor) =>
        [.. descriptor.Files.Select(file => $"{file.Uri.AbsoluteUri}|{file.Sha256Checksum}|{file.RelativeInstallPath}")];

    /// <summary>
    ///     Gets the vendor files a Chatterbox Turbo variant installs, through its public API: the contents of the
    ///     published bundles its descriptor names, or the descriptor's own files for a variant downloading them
    ///     individually.
    /// </summary>
    /// <param name="model">The variant's model.</param>
    /// <returns>The installed vendor files, in descriptor order.</returns>
    private static SpeechModelDownloadDescriptor VendorFilesOf(OnnxChatterboxTurboEnglishSynthesisModel model)
    {
        var published = OnnxChatterboxTurboEnglishSynthesisModel.PublishedBundles
            .ToDictionary(bundle => bundle.FileName, StringComparer.Ordinal);
        var files = model.DownloadDescriptor.Files;
        return files.All(file => published.ContainsKey(file.RelativeInstallPath))
            ? new SpeechModelDownloadDescriptor([.. files.SelectMany(file => published[file.RelativeInstallPath].Contents.Files)])
            : model.DownloadDescriptor;
    }

    /// <summary>Writes a one-bundle definitions file and its license text.</summary>
    /// <param name="directory">The directory to write into (must exist).</param>
    /// <param name="name">The bundle name.</param>
    /// <param name="date">The bundle date text.</param>
    /// <param name="licenseText">The license file content.</param>
    /// <param name="textFile">The declared license text path (the written <c>license.txt</c> by default).</param>
    /// <param name="modelId">The declared model id.</param>
    /// <returns>The definitions file path.</returns>
    private static string WriteDefinitions(
        string directory,
        string name,
        string date,
        string licenseText,
        string textFile = "license.txt",
        string modelId = TestBundles.ModelId)
    {
        File.WriteAllText(Path.Combine(directory, "license.txt"), licenseText);
        var path = Path.Combine(directory, BundleDefinitions.FileName);
        File.WriteAllText(
            path,
            Template
                .Replace("NAME", name, StringComparison.Ordinal)
                .Replace("DATE", date, StringComparison.Ordinal)
                .Replace("TEXTFILE", JsonEncodedText.Encode(textFile).ToString(), StringComparison.Ordinal)
                .Replace("MODEL_ID_TOKEN", JsonEncodedText.Encode(modelId).ToString(), StringComparison.Ordinal));
        return path;
    }

    /// <summary>The upstream revision form: the branch plus the date the SHA-256 pins were first committed.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"^main \(snapshot pinned by SHA-256 on \d{4}-\d{2}-\d{2}\)$", RegexOptions.CultureInvariant)]
    private static partial Regex PinnedRevisionPattern();
}
