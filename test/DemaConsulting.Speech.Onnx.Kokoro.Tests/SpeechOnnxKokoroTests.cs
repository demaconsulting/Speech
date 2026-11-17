using System.Xml.Linq;

namespace DemaConsulting.Speech.Onnx.Kokoro.Tests;

/// <summary>
///     System-level tests for the SpeechOnnxKokoro package license files: the combined
///     <c>PACKAGE-LICENSE.md</c> that the package names as its license, and the
///     <c>THIRD-PARTY-NOTICES.md</c> that attributes the embedded lexicon and tokenizer data to
///     its sources. The tests read the repository files directly; the packed package itself is
///     asserted by the <c>Kokoro_Package_LicenseFilesPackaged</c> FileAssert test.
/// </summary>
public sealed class SpeechOnnxKokoroTests
{
    /// <summary>The license file the package names as its license.</summary>
    private const string PackageLicenseFileName = "PACKAGE-LICENSE.md";

    /// <summary>The third-party notices file shipped next to the license file.</summary>
    private const string ThirdPartyNoticesFileName = "THIRD-PARTY-NOTICES.md";

    /// <summary>The package project folder, relative to the repository root.</summary>
    private const string PackageFolder = "src/DemaConsulting.Speech.Onnx.Kokoro";

    /// <summary>The SPDX expression every license the package ships is listed in.</summary>
    private const string SpdxHeader = "SPDX-License-Identifier: MIT AND Apache-2.0 AND BSD-2-Clause";

    /// <summary>Proves the project file names the combined license file and packs both files at the package root.</summary>
    [Fact]
    public void SpeechOnnxKokoro_PackageLicense_ProjectFile_NamesLicenseFileAtPackageRoot()
    {
        // Arrange
        var projectPath = FindRepositoryFile($"{PackageFolder}/DemaConsulting.Speech.Onnx.Kokoro.csproj");

        // Act
        var project = XDocument.Load(projectPath);
        var licenseFiles = project.Descendants("PackageLicenseFile").Select(element => element.Value.Trim()).ToList();
        var licenseExpressions = project.Descendants("PackageLicenseExpression").ToList();
        var packedItems = project.Descendants("None")
            .Where(element => (string?)element.Attribute("Pack") == "true")
            .ToDictionary(
                element => (string?)element.Attribute("Include") ?? string.Empty,
                element => (string?)element.Attribute("PackagePath"),
                StringComparer.Ordinal);

        // Assert
        Assert.Equal([PackageLicenseFileName], licenseFiles);
        Assert.Empty(licenseExpressions);
        foreach (var fileName in new[] { PackageLicenseFileName, ThirdPartyNoticesFileName })
        {
            Assert.True(packedItems.TryGetValue(fileName, out var packagePath), $"{fileName} is not packed");
            Assert.Equal("\\", packagePath);
            Assert.True(File.Exists(FindRepositoryFile($"{PackageFolder}/{fileName}")), $"{fileName} does not exist");
        }
    }

    /// <summary>Proves the license file starts with the package's SPDX expression and covers the code and both embedded data files.</summary>
    [Fact]
    public void SpeechOnnxKokoro_PackageLicense_File_CoversCodeAndEmbeddedData()
    {
        // Arrange
        var licensePath = FindRepositoryFile($"{PackageFolder}/{PackageLicenseFileName}");

        // Act
        var firstLine = File.ReadLines(licensePath).First().TrimEnd();
        var text = File.ReadAllText(licensePath);

        // Assert
        Assert.Equal(SpdxHeader, firstLine);
        Assert.Contains("MIT License", text, StringComparison.Ordinal);
        Assert.Contains("kokoro-en-lexicon.tsv.gz", text, StringComparison.Ordinal);
        Assert.Contains("kokoro-v1.0-tokenizer.json", text, StringComparison.Ordinal);
        Assert.Contains("CMUdict", text, StringComparison.Ordinal);
        Assert.Contains("Apache", text, StringComparison.Ordinal);
        Assert.Contains(ThirdPartyNoticesFileName, text, StringComparison.Ordinal);
    }

    /// <summary>Proves the third-party notices attribute the embedded data to its sources with their license texts, state the misaki provenance limits without over-claiming, and claim no public-domain status.</summary>
    [Fact]
    public void SpeechOnnxKokoro_ThirdPartyNotices_File_AttributesEmbeddedData()
    {
        // Arrange
        var noticesPath = FindRepositoryFile($"{PackageFolder}/{ThirdPartyNoticesFileName}");

        // Act
        var text = File.ReadAllText(noticesPath);

        // Assert
        Assert.Contains("Carnegie Mellon University", text, StringComparison.Ordinal);
        Assert.Contains("Redistribution and use in source and binary forms", text, StringComparison.Ordinal);
        Assert.Contains("Apache License", text, StringComparison.Ordinal);
        Assert.Contains("Version 2.0, January 2004", text, StringComparison.Ordinal);
        Assert.Contains("hexgrad/misaki", text, StringComparison.Ordinal);
        Assert.Contains("cmusphinx/cmudict", text, StringComparison.Ordinal);
        Assert.DoesNotContain("public domain", text, StringComparison.OrdinalIgnoreCase);

        // The misaki provenance statement: version, undocumented upstream data, unverified author statements, planned replacement
        var flat = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        Assert.Contains("version 0.7.4", flat, StringComparison.Ordinal);
        Assert.Contains("does not document the upstream provenance", flat, StringComparison.Ordinal);
        Assert.Contains("We have not independently verified these statements or the licensing of the upstream data", flat, StringComparison.Ordinal);
        Assert.Contains("scheduled to be replaced by one generated solely from the CMU Pronouncing Dictionary", flat, StringComparison.Ordinal);
    }

    /// <summary>Finds a file in the repository by walking up from the test output folder to the repository root.</summary>
    /// <param name="relativePath">The path of the file relative to the repository root, using forward slashes.</param>
    /// <returns>The full path of the file.</returns>
    /// <exception cref="InvalidOperationException">The repository root cannot be found above the test output folder.</exception>
    private static string FindRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Speech.slnx")))
        {
            directory = directory.Parent;
        }

        return directory == null
            ? throw new InvalidOperationException("The repository root (the folder containing Speech.slnx) was not found.")
            : Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}
