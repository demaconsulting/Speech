using System.IO.Compression;
using System.Text;
using System.Text.Json;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.Piper.Tests;

/// <summary>
///     Shared fixtures of the Piper tests: the trimmed voice configuration, a tiny ONNX graph with the Piper input
///     contract whose output echoes what the engine sent it, the published bundle pin and tampered bundle zips.
/// </summary>
/// <remarks>
///     The echo graph's output is <c>[ids (N values), noise scale, length scale, noise width, speaker, N]</c>, so a test
///     can observe exactly what the engine fed the model without the real, ~78 MB voice. The graph is serialized here
///     with a minimal ONNX protobuf writer (ModelProto, GraphProto, NodeProto, TensorProto, ValueInfoProto).
/// </remarks>
internal static class PiperTestFixtures
{
    /// <summary>The ONNX element type id for float.</summary>
    private const int Float = 1;

    /// <summary>The ONNX element type id for int64.</summary>
    private const int Int64 = 7;

    /// <summary>
    ///     The SHA-256 of the published <c>piper-en-us-libritts-r-medium-20261122-fp32-b1.zip</c> asset on the
    ///     <c>models</c> release, reproduced here (not read from the production class) so a changed pin fails a test.
    /// </summary>
    public const string BundleSha256 = "5196a8aa97f9053e0bdbbc2299007d48d55f37a74e88199869daebb710d8bd0f";

    /// <summary>Gets the path of the trimmed copy of the real voice configuration (test data, CC BY 4.0, attribution inside).</summary>
    public static string TrimmedConfigPath { get; } = Path.Combine(AppContext.BaseDirectory, "TestData", "en_US-libritts_r-medium.onnx.trimmed.json");

    /// <summary>Gets the text of <see cref="TrimmedConfigPath"/>.</summary>
    public static string TrimmedConfigJson => File.ReadAllText(TrimmedConfigPath, Encoding.UTF8);

    /// <summary>
    ///     Writes the echo graph and the trimmed configuration into <paramref name="installedDirectory"/> under the file
    ///     names of an installed voice.
    /// </summary>
    /// <param name="installedDirectory">The installed model directory (created when missing).</param>
    public static void WriteInstalledEchoVoice(string installedDirectory)
    {
        Directory.CreateDirectory(installedDirectory);
        File.WriteAllBytes(Path.Combine(installedDirectory, "en_US-libritts_r-medium.onnx"), BuildEchoModel());
        File.WriteAllText(Path.Combine(installedDirectory, "en_US-libritts_r-medium.onnx.json"), TrimmedConfigJson, Encoding.UTF8);
    }

    /// <summary>
    ///     Decodes the output of the echo graph: <c>[ids (N), noise scale, length scale, noise width, speaker, N]</c>.
    /// </summary>
    /// <param name="audio">The audio an engine produced from one run of the echo graph.</param>
    /// <returns>The phoneme ids the graph was fed, its three scales, the speaker and the length it was told.</returns>
    public static (long[] Ids, float[] Scales, long Speaker, long Length) DecodeEcho(BackendAudio audio)
    {
        var samples = audio.Samples;
        var idCount = samples.Length - 5;
        return (
            [.. samples.Take(idCount).Select(value => (long)value)],
            [.. samples.Skip(idCount).Take(3)],
            (long)samples[idCount + 3],
            (long)samples[idCount + 4]);
    }

    /// <summary>
    ///     Builds the echo graph.
    /// </summary>
    /// <returns>The ONNX model bytes.</returns>
    public static byte[] BuildEchoModel()
    {
        var graph = new List<byte>();
        AddNode(graph, "Cast", ["input"], ["input_f"], IntAttribute("to", Float));
        AddNode(graph, "Reshape", ["input_f", "flat_shape"], ["input_flat"]);
        AddNode(graph, "Cast", ["sid"], ["sid_f"], IntAttribute("to", Float));
        AddNode(graph, "Cast", ["input_lengths"], ["len_f"], IntAttribute("to", Float));
        AddNode(graph, "Concat", ["input_flat", "scales", "sid_f", "len_f"], ["output"], IntAttribute("axis", 0));
        WriteField(graph, 2, Encoding.UTF8.GetBytes("piper_echo"));

        // initializer: int64 [-1], the Reshape target shape
        var shape = new List<byte>();
        WriteVarIntField(shape, 1, 1);
        WriteVarIntField(shape, 2, Int64);
        WriteField(shape, 8, Encoding.UTF8.GetBytes("flat_shape"));
        WriteField(shape, 9, BitConverter.GetBytes(-1L));
        WriteField(graph, 5, shape);

        WriteField(graph, 11, ValueInfo("input", Int64, "1", "N"));
        WriteField(graph, 11, ValueInfo("input_lengths", Int64, "1"));
        WriteField(graph, 11, ValueInfo("scales", Float, "3"));
        WriteField(graph, 11, ValueInfo("sid", Int64, "1"));
        WriteField(graph, 12, ValueInfo("output", Float, "M"));

        var operatorSet = new List<byte>();
        WriteVarIntField(operatorSet, 2, 13);

        var model = new List<byte>();
        WriteVarIntField(model, 1, 8);
        WriteField(model, 8, operatorSet);
        WriteField(model, 7, graph);
        return [.. model];
    }

    /// <summary>
    ///     Stages a well-formed bundle zip for <paramref name="bundle"/> whose <c>model.json</c> declares every vendor
    ///     file at its compiled pin, but whose vendor entries hold other bytes, so only the extracted-bytes verification
    ///     can reject it.
    /// </summary>
    /// <param name="bundle">The bundle to imitate.</param>
    /// <param name="directory">The staging directory to write <c>{bundle.FileName}</c> into (created when missing).</param>
    public static void StageBundleWithWrongFiles(SpeechModelBundle bundle, string directory)
    {
        var wrongBytes = "these are not the vendor bytes"u8.ToArray();
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            bundle = bundle.Name,
            files = bundle.Contents.Files
                .Select(file => new { path = file.RelativeInstallPath, size = wrongBytes.Length, sha256 = file.Sha256Checksum })
                .ToArray(),
        });

        Directory.CreateDirectory(directory);
        using var archive = ZipFile.Open(Path.Join(directory, bundle.FileName), ZipArchiveMode.Create);
        WriteEntry(archive, "README.txt", "Test bundle."u8.ToArray());
        WriteEntry(archive, "LICENSE", "Test license."u8.ToArray());
        WriteEntry(archive, "model.json", manifest);
        foreach (var file in bundle.Contents.Files)
        {
            WriteEntry(archive, file.RelativeInstallPath, wrongBytes);
        }
    }

    /// <summary>Writes one stored entry into a zip being created.</summary>
    /// <param name="archive">The archive in create mode.</param>
    /// <param name="name">The entry name.</param>
    /// <param name="content">The entry bytes.</param>
    private static void WriteEntry(ZipArchive archive, string name, byte[] content)
    {
        using var stream = archive.CreateEntry(name, CompressionLevel.NoCompression).Open();
        stream.Write(content, 0, content.Length);
    }

    private static void AddNode(List<byte> graph, string op, string[] inputs, string[] outputs, List<byte>? attribute = null)
    {
        var node = new List<byte>();
        foreach (var input in inputs)
        {
            WriteField(node, 1, Encoding.UTF8.GetBytes(input));
        }

        foreach (var output in outputs)
        {
            WriteField(node, 2, Encoding.UTF8.GetBytes(output));
        }

        WriteField(node, 3, Encoding.UTF8.GetBytes(outputs[0] + "_node"));
        WriteField(node, 4, Encoding.UTF8.GetBytes(op));
        if (attribute is not null)
        {
            WriteField(node, 5, attribute);
        }

        WriteField(graph, 1, node);
    }

    private static List<byte> IntAttribute(string name, long value)
    {
        var attribute = new List<byte>();
        WriteField(attribute, 1, Encoding.UTF8.GetBytes(name));
        WriteVarIntField(attribute, 3, value);
        WriteVarIntField(attribute, 20, 2);
        return attribute;
    }

    private static List<byte> ValueInfo(string name, int elementType, params string[] dimensions)
    {
        var shape = new List<byte>();
        foreach (var dimension in dimensions)
        {
            var dim = new List<byte>();
            if (long.TryParse(dimension, out var value))
            {
                WriteVarIntField(dim, 1, value);
            }
            else
            {
                WriteField(dim, 2, Encoding.UTF8.GetBytes(dimension));
            }

            WriteField(shape, 1, dim);
        }

        var tensorType = new List<byte>();
        WriteVarIntField(tensorType, 1, elementType);
        WriteField(tensorType, 2, shape);

        var type = new List<byte>();
        WriteField(type, 1, tensorType);

        var info = new List<byte>();
        WriteField(info, 1, Encoding.UTF8.GetBytes(name));
        WriteField(info, 2, type);
        return info;
    }

    private static void WriteVarInt(List<byte> buffer, long value)
    {
        var v = (ulong)value;
        while (v >= 0x80)
        {
            buffer.Add((byte)(v | 0x80));
            v >>= 7;
        }

        buffer.Add((byte)v);
    }

    private static void WriteVarIntField(List<byte> buffer, int field, long value)
    {
        WriteVarInt(buffer, field << 3);
        WriteVarInt(buffer, value);
    }

    private static void WriteField(List<byte> buffer, int field, IReadOnlyCollection<byte> payload)
    {
        WriteVarInt(buffer, (field << 3) | 2);
        WriteVarInt(buffer, payload.Count);
        buffer.AddRange(payload);
    }
}
