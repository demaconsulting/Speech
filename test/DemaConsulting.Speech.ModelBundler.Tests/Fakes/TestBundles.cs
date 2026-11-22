using System.Security.Cryptography;
using System.Text;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.ModelBundler.Tests.Fakes;

/// <summary>
///     A small, fixed bundle used across the tests: a two-file "model" with known bytes, pins and definition, plus
///     helpers to lay out a source store and create a builder over a fake HTTP server.
/// </summary>
internal static class TestBundles
{
    /// <summary>The fixture bundle name (it follows the bundle naming rule).</summary>
    public const string Name = "fixture-model-r1-int8-b1";

    /// <summary>The first model id that installs the fixture bundle.</summary>
    public const string ModelId = "fixture-model-id";

    /// <summary>The second model id that installs the fixture bundle.</summary>
    public const string AlternateModelId = "fixture-model-id-alt";

    /// <summary>The original URL of the fixture's weights file.</summary>
    public static readonly Uri WeightsUri = new("https://example.invalid/fixture/resolve/main/onnx/weights.bin");

    /// <summary>The original URL of the fixture's configuration file.</summary>
    public static readonly Uri ConfigUri = new("https://example.invalid/fixture/resolve/main/config.json");

    /// <summary>The bytes of the fixture's weights file.</summary>
    public static readonly byte[] WeightsBytes = [.. Enumerable.Range(0, 4096).Select(index => (byte)(index * 7 % 251))];

    /// <summary>The bytes of the fixture's configuration file.</summary>
    public static readonly byte[] ConfigBytes = Encoding.UTF8.GetBytes("{\"fixture\": true}\n");

    /// <summary>Computes a lowercase SHA-256.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The lowercase hexadecimal digest.</returns>
    public static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>Creates the fixture's compiled contents: the two files with their original URLs and pins.</summary>
    /// <returns>The descriptor, weights declared first (so the builder must sort).</returns>
    public static SpeechModelDownloadDescriptor CreateContents() =>
        new(
        [
            new SpeechModelDownloadFile(WeightsUri, Sha256(WeightsBytes), "onnx/weights.bin"),
            new SpeechModelDownloadFile(ConfigUri, Sha256(ConfigBytes), "config.json"),
        ]);

    /// <summary>Creates the fixture's catalog of compiled contents.</summary>
    /// <returns>The contents keyed by bundle name.</returns>
    public static IReadOnlyDictionary<string, SpeechModelDownloadDescriptor> CreateCatalog() =>
        new Dictionary<string, SpeechModelDownloadDescriptor>(StringComparer.Ordinal) { [Name] = CreateContents() };

    /// <summary>Creates the fixture's definition, with fixed texts and date.</summary>
    /// <returns>The definition.</returns>
    public static BundleDefinition CreateDefinition() =>
        new(
            Name,
            "fixture-model",
            "r1",
            "int8",
            1,
            new DateOnly(2026, 11, 22),
            [ModelId, AlternateModelId],
            "https://example.invalid/fixture",
            "main (snapshot pinned by SHA-256 on 2026-10-10)",
            [new BundleLicense("MIT", "https://opensource.org/license/mit", "fixture weights", "Fixture license text.\n")],
            "Fixture model by Example Corp.",
            ["Fixture model by Example Corp.", "Copyright (c) 2026 Example Corp."]);

    /// <summary>Writes the fixture's files into a model store as an installed copy of one model.</summary>
    /// <param name="storeRoot">The store root.</param>
    /// <param name="modelId">The model whose <c>current</c> directory receives the files.</param>
    /// <param name="weights">The weights bytes to write (the pinned bytes unless a test tampers with them).</param>
    public static void WriteStore(string storeRoot, string modelId, byte[]? weights = null)
    {
        var current = Path.Combine(storeRoot, modelId, "current");
        Directory.CreateDirectory(Path.Combine(current, "onnx"));
        File.WriteAllBytes(Path.Combine(current, "onnx", "weights.bin"), weights ?? WeightsBytes);
        File.WriteAllBytes(Path.Combine(current, "config.json"), ConfigBytes);
    }

    /// <summary>Creates a builder over the fixture catalog.</summary>
    /// <param name="storeRoot">The source store root, or <see langword="null"/>.</param>
    /// <param name="cacheDirectory">The cache directory.</param>
    /// <param name="handler">The fake HTTP server.</param>
    /// <param name="log">The log writer.</param>
    /// <param name="published">The published zip pins, or <see langword="null"/> for none.</param>
    /// <returns>The builder (its HTTP client is owned by the test's handler lifetime).</returns>
    public static BundleBuilder CreateBuilder(
        string? storeRoot,
        string cacheDirectory,
        FakeHttpMessageHandler handler,
        TextWriter log,
        IReadOnlyDictionary<string, string>? published = null) =>
        new(
            CreateCatalog(),
            published ?? new Dictionary<string, string>(StringComparer.Ordinal),
            new VendorFileSource(storeRoot, cacheDirectory, new HttpClient(handler, disposeHandler: false)),
            log);
}
