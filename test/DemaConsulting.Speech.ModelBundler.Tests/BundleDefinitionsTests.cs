using System.Text.Json;
using System.Text.RegularExpressions;
using DemaConsulting.Speech.ModelBundler.Tests.Fakes;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.NemotronStt.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;

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

        // Assert: the same five bundle names
        Assert.Equal(5, definitions.Count);
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
        var piper = definitions["piper-en-us-libritts-r-medium-20261122-fp32-b1"];
        Assert.Equal("CC-BY-4.0", Assert.Single(piper.Licenses).Name);
        Assert.Contains("Attribution 4.0 International", piper.Licenses[0].Text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the Piper bundle's attribution and NOTICE credit LibriTTS-R, the Piper project and the
    ///     Lessac voice with the research-only restriction of its Blizzard Challenge 2013 training data.
    /// </summary>
    [Fact]
    public void BundleDefinitions_Load_ShippedPiperBundle_CarriesFullLineageAttribution()
    {
        // Arrange / Act: load the shipped Piper definition
        var piper = BundleDefinitions.Load(BundleDefinitions.GetDefaultPath())
            .Single(definition => definition.Name == "piper-en-us-libritts-r-medium-20261122-fp32-b1");
        var notice = string.Join('\n', piper.NoticeLines);

        // Assert: both the attribution and the notice name every part of the lineage
        Assert.Equal(["piper-onnx-libritts-r-en-us-medium"], piper.ModelIds);
        Assert.All(new[] { piper.Attribution, notice }, text =>
        {
            Assert.Contains("LibriTTS-R", text, StringComparison.Ordinal);
            Assert.Contains("Koizumi", text, StringComparison.Ordinal);
            Assert.Contains("openslr.org/141", text, StringComparison.Ordinal);
            Assert.Contains("Piper project (Rhasspy", text, StringComparison.Ordinal);
            Assert.Contains("lessac", text, StringComparison.Ordinal);
            Assert.Contains("Blizzard Challenge 2013", text, StringComparison.Ordinal);
            Assert.Contains("non-commercial research use only", text, StringComparison.Ordinal);
            Assert.Contains("review the upstream terms", text, StringComparison.Ordinal);
        });
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
    [InlineData("chatterbox-turbo-onnx-en")]
    [InlineData("piper-onnx-libritts-r-en-us-medium")]
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

    /// <summary>Validates that Kokoro's, Nemotron's and Piper's compiled bundle contents are their published bundles' contents.</summary>
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
        var piper = Assert.Single(OnnxPiperLibriTtsEnSynthesisModel.Bundles);
        Assert.Equal(Describe(piper.Contents), Describe(contents[piper.Name]));
        Assert.Equal(3, contents["piper-en-us-libritts-r-medium-20261122-fp32-b1"].Files.Count);
    }

    /// <summary>Validates that Chatterbox Turbo installs exactly its language-model and auxiliary bundles' files.</summary>
    [Fact]
    public void BundleCatalog_GetContents_Chatterbox_EqualsModelVendorFiles()
    {
        // Arrange: the compiled contents
        var contents = BundleCatalog.GetContents();

        // Act: join the two component bundles
        var joined = contents["chatterbox-turbo-lm-20261014-fp16-b1"].Files
            .Concat(contents["chatterbox-turbo-aux-20261014-int4-b1"].Files)
            .ToList();

        // Assert: the model's bundles hold exactly those eight files, in that order
        Assert.Equal(8, joined.Count);
        Assert.Equal(
            Describe(new SpeechModelDownloadDescriptor([.. OnnxChatterboxTurboEnglishSynthesisModel.Bundles.SelectMany(bundle => bundle.Contents.Files)])),
            Describe(new SpeechModelDownloadDescriptor(joined)));
    }

    /// <summary>Validates that the published zip pins are exactly the packages' published bundles, so a rebuild reports MATCH.</summary>
    [Fact]
    public void BundleCatalog_GetPublishedChecksums_ListsEveryPublishedBundle()
    {
        // Arrange / Act: read the published pins
        var pins = BundleCatalog.GetPublishedChecksums();

        // Assert: every declared bundle is published and carries its released zip SHA-256
        Assert.Equal(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["kokoro-en-v1.0-int8-b1"] = "c61bb8da1433020071782071fe7f2eb6c9d03d41b9b9672f6b1201b4afd61e9c",
                ["nemotron-3.5-asr-streaming-0.6b-20261011-int4-b1"] = "733375bb604712afac6c4adbd6a9149ebc414fc9a5bee39ed3bdfc3c104048b2",
                ["chatterbox-turbo-lm-20261014-fp16-b1"] = "2c2e05d62e18453fa8c726d57b8c970948f5d693400f303fc2e9dc97838a1584",
                ["chatterbox-turbo-aux-20261014-int4-b1"] = "1981e10e65bb4f2954b99dee6c9b9e74242428cd87ed0dadf085ca6f8f229581",
                ["piper-en-us-libritts-r-medium-20261122-fp32-b1"] = "5196a8aa97f9053e0bdbbc2299007d48d55f37a74e88199869daebb710d8bd0f",
            }.OrderBy(entry => entry.Key, StringComparer.Ordinal),
            pins.OrderBy(entry => entry.Key, StringComparer.Ordinal));
        Assert.Equal(BundleCatalog.GetContents().Keys.Order(StringComparer.Ordinal), pins.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>Describes a descriptor as comparable strings (URL, pin and install path of each file, in order).</summary>
    /// <param name="descriptor">The descriptor.</param>
    /// <returns>One string per file.</returns>
    private static List<string> Describe(SpeechModelDownloadDescriptor descriptor) =>
        [.. descriptor.Files.Select(file => $"{file.Uri.AbsoluteUri}|{file.Sha256Checksum}|{file.RelativeInstallPath}")];

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
