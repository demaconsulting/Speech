using System.Text;
using System.Text.Json;
using DemaConsulting.Speech.ModelBundler.Tests.Fakes;

namespace DemaConsulting.Speech.ModelBundler.Tests;

/// <summary>
///     Unit tests for <see cref="BundleMetadataWriter"/>: the required <c>README.txt</c> statements, the fixed
///     <c>model.json</c> layout, the <c>NOTICE</c> and the <c>LICENSE</c> composition.
/// </summary>
public sealed class BundleMetadataWriterTests
{
    /// <summary>The fixture's vendor files as resolved, sorted by entry name.</summary>
    private static readonly IReadOnlyList<ResolvedVendorFile> Files =
    [
        .. TestBundles.CreateContents().Files
            .Select(file => new ResolvedVendorFile(
                file,
                VendorFileSource.GetEntryName(file),
                "unused",
                file.RelativeInstallPath == "config.json" ? TestBundles.ConfigBytes.Length : TestBundles.WeightsBytes.Length,
                VendorFileOrigin.Cache))
            .OrderBy(file => file.EntryName, StringComparer.Ordinal),
    ];

    /// <summary>Validates that README.txt states who repackaged the bundle, its name, revision and date.</summary>
    [Fact]
    public void BundleMetadataWriter_CreateReadme_FixtureBundle_StatesRepackagingIdentity()
    {
        // Arrange / Act: create the readme
        var readme = BundleMetadataWriter.CreateReadme(TestBundles.CreateDefinition(), Files);

        // Assert: the repackaging statement, name, revision and date are present, with LF line endings only
        Assert.Contains("This bundle was repackaged by DEMA Consulting for DemaConsulting.Speech.", readme, StringComparison.Ordinal);
        Assert.Contains($"Bundle:            {TestBundles.Name}", readme, StringComparison.Ordinal);
        Assert.Contains("Bundle revision:   b1", readme, StringComparison.Ordinal);
        Assert.Contains("Date:              2026-11-22", readme, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', readme);
    }

    /// <summary>Validates that README.txt states the vendor files are unmodified, with each original URL and SHA-256.</summary>
    [Fact]
    public void BundleMetadataWriter_CreateReadme_FixtureBundle_ListsUnmodifiedFilesWithUrlAndSha256()
    {
        // Arrange / Act: create the readme
        var readme = BundleMetadataWriter.CreateReadme(TestBundles.CreateDefinition(), Files);

        // Assert: the unmodified statement and every file's original URL and pin
        Assert.Contains("UNMODIFIED copies of the original upstream files", readme, StringComparison.Ordinal);
        foreach (var file in Files)
        {
            Assert.Contains($"Original URL: {file.File.Uri.AbsoluteUri}", readme, StringComparison.Ordinal);
            Assert.Contains($"SHA-256:      {file.File.Sha256Checksum}", readme, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Validates that README.txt lists the added files, the license names and URLs, the attribution and the issues
    ///     link.
    /// </summary>
    [Fact]
    public void BundleMetadataWriter_CreateReadme_FixtureBundle_ListsAddedFilesLicenseAttributionAndIssues()
    {
        // Arrange / Act: create the readme
        var definition = TestBundles.CreateDefinition();
        var readme = BundleMetadataWriter.CreateReadme(definition, Files);

        // Assert: the added files, the license, the attribution and where to report issues
        Assert.Contains("DEMA Consulting added only these files", readme, StringComparison.Ordinal);
        foreach (var added in new[] { "  LICENSE ", "  NOTICE ", "  model.json ", "  README.txt " })
        {
            Assert.Contains(added, readme, StringComparison.Ordinal);
        }

        Assert.Contains("MIT (https://opensource.org/license/mit)", readme, StringComparison.Ordinal);
        Assert.Contains(definition.Attribution, readme, StringComparison.Ordinal);
        Assert.Contains("https://github.com/demaconsulting/Speech/issues", readme, StringComparison.Ordinal);
    }

    /// <summary>Validates the fixed key order, two-space indent, LF endings and no byte order mark of model.json.</summary>
    [Fact]
    public void BundleMetadataWriter_CreateModelJson_FixtureBundle_UsesFixedKeyOrderAndFormat()
    {
        // Arrange / Act: create model.json
        var bytes = BundleMetadataWriter.CreateModelJson(TestBundles.CreateDefinition(), Files);
        var text = Encoding.UTF8.GetString(bytes);
        using var document = JsonDocument.Parse(bytes);

        // Assert: key order, formatting and the fixed values
        Assert.Equal(
            ["schemaVersion", "bundle", "model", "upstreamRelease", "quant", "bundleRevision", "date", "repackagedBy", "upstream", "licenses", "modelIds", "files"],
            document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            ["path", "size", "sha256", "sourceUrl"],
            document.RootElement.GetProperty("files")[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            ["name", "url", "appliesTo"],
            document.RootElement.GetProperty("licenses")[0].EnumerateObject().Select(property => property.Name));
        Assert.StartsWith("{\n  \"schemaVersion\": 1,\n  \"bundle\": \"fixture-model-r1-int8-b1\",\n", text, StringComparison.Ordinal);
        Assert.EndsWith("}\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', text);
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Equal("DEMA Consulting for DemaConsulting.Speech", document.RootElement.GetProperty("repackagedBy").GetString());
        Assert.Equal("2026-11-22", document.RootElement.GetProperty("date").GetString());
    }

    /// <summary>Validates that model.json lists exactly the compiled files, sorted by path, with sizes, pins and URLs.</summary>
    [Fact]
    public void BundleMetadataWriter_CreateModelJson_FixtureBundle_ListsCompiledFilesSorted()
    {
        // Arrange / Act: create model.json from the files in reverse order of declaration
        using var document = JsonDocument.Parse(BundleMetadataWriter.CreateModelJson(TestBundles.CreateDefinition(), Files));
        var files = document.RootElement.GetProperty("files").EnumerateArray().ToList();

        // Assert: sorted ordinal, each with the measured size, the pin and the original URL
        Assert.Equal(["config.json", "onnx/weights.bin"], files.Select(file => file.GetProperty("path").GetString()));
        Assert.Equal(TestBundles.ConfigBytes.Length, files[0].GetProperty("size").GetInt64());
        Assert.Equal(TestBundles.Sha256(TestBundles.ConfigBytes), files[0].GetProperty("sha256").GetString());
        Assert.Equal(TestBundles.ConfigUri.AbsoluteUri, files[0].GetProperty("sourceUrl").GetString());
        Assert.Equal(TestBundles.Sha256(TestBundles.WeightsBytes), files[1].GetProperty("sha256").GetString());
    }

    /// <summary>Validates that NOTICE carries the declared notice lines and states the files are unmodified.</summary>
    [Fact]
    public void BundleMetadataWriter_CreateNotice_FixtureBundle_HasNoticeLinesAndUnmodifiedStatement()
    {
        // Arrange / Act: create the notice
        var definition = TestBundles.CreateDefinition();
        var notice = BundleMetadataWriter.CreateNotice(definition);

        // Assert: the declared lines come first, then the fixed unmodified statement
        Assert.StartsWith(string.Join('\n', definition.NoticeLines) + "\n\n", notice, StringComparison.Ordinal);
        Assert.Contains("unmodified copies of the original upstream files", notice, StringComparison.Ordinal);
    }

    /// <summary>Validates that a single-license bundle's LICENSE is the license text verbatim.</summary>
    [Fact]
    public void BundleMetadataWriter_CreateLicense_SingleLicense_IsTextVerbatim()
    {
        // Arrange / Act: create the license of the single-license fixture
        var license = BundleMetadataWriter.CreateLicense(TestBundles.CreateDefinition());

        // Assert: exactly the license text
        Assert.Equal("Fixture license text.\n", license);
    }

    /// <summary>Validates that a multi-license bundle's LICENSE holds every text under a header naming its scope.</summary>
    [Fact]
    public void BundleMetadataWriter_CreateLicense_TwoLicenses_HoldsEachTextUnderHeader()
    {
        // Arrange: a definition with two licenses
        var definition = TestBundles.CreateDefinition() with
        {
            Licenses =
            [
                new BundleLicense("OpenMDW-1.1", "https://openmdw.ai/license/1-1/", "model weights", "Weights license.\n"),
                new BundleLicense("MIT", "https://opensource.org/license/mit", "conversion", "Conversion license.\n"),
            ],
        };

        // Act: create the license
        var license = BundleMetadataWriter.CreateLicense(definition);

        // Assert: both headers and texts, in order
        var weights = license.IndexOf("OpenMDW-1.1 - applies to: model weights", StringComparison.Ordinal);
        var conversion = license.IndexOf("MIT - applies to: conversion", StringComparison.Ordinal);
        Assert.True(weights >= 0 && conversion > weights);
        Assert.Contains("Weights license.\n", license, StringComparison.Ordinal);
        Assert.EndsWith("Conversion license.\n", license, StringComparison.Ordinal);
    }
}
