using System.Diagnostics;
using DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Kokoro.SynthesisSubsystem;
using DemaConsulting.Speech.Onnx.OnnxRuntimeSubsystem;

// Throwaway CPU-vs-DirectML benchmark. See KokoroOnnxBenchmark.csproj's header comment for why
// this tool links source directly instead of referencing the real packages.
var modelsDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "KokoroSpike", "models");
modelsDir = Path.GetFullPath(modelsDir);

var modelPath = Path.Combine(modelsDir, "onnx", "model_fp16.onnx");
var voicePath = Path.Combine(modelsDir, "voices", "af_heart.bin");

if (!File.Exists(modelPath) || !File.Exists(voicePath))
{
    Console.Error.WriteLine($"Model files not found under '{modelsDir}'. Run the KokoroSpike download step first.");
    return 1;
}

// args[0]: "cpu" (default) or "dml" - selects which execution provider name this run attempts
// first. The actual available native support depends on which OrtProvider this project was
// *built* with (see the .csproj header comment) - this arg only controls what is attempted.
var useDml = args.Length > 0 && string.Equals(args[0], "dml", StringComparison.OrdinalIgnoreCase);
var preferredProviders = useDml ? new[] { "DmlExecutionProvider" } : null;

Console.WriteLine($"Model: {modelPath}");
Console.WriteLine($"Requested provider: {(useDml ? "DmlExecutionProvider (falls back to CPU if unavailable)" : "CPU only")}");

var stopwatchLoad = Stopwatch.StartNew();
var session = OnnxExecutionProviderSelector.Create(modelPath, preferredProviders, OnnxKokoroSynthesisEngine.RunProbeInference);
stopwatchLoad.Stop();
Console.WriteLine($"Session load time: {stopwatchLoad.ElapsedMilliseconds} ms");

var voiceBytes = File.ReadAllBytes(voicePath);
var voiceFloats = new float[voiceBytes.Length / sizeof(float)];
Buffer.BlockCopy(voiceBytes, 0, voiceFloats, 0, voiceBytes.Length);

var vocabulary = new KokoroPhonemeVocabulary();
var phonemizer = new KokoroLexiconPhonemizer();
using var engine = new OnnxKokoroSynthesisEngine(
    session,
    vocabulary,
    phonemizer,
    new Dictionary<int, float[]> { [0] = voiceFloats });

string[] sentences =
[
    "The quick brown fox jumps over the lazy dog.",
    "Artificial intelligence is transforming how we build and use software every single day.",
    "Local, offline speech synthesis lets an application run entirely without a network connection.",
    "In a village of La Mancha, the name of which I have no desire to call to mind, there lived not long since one of those gentlemen.",
];

const int warmupRuns = 2;
const int measuredRuns = 5;

// Warm-up: excludes first-call JIT/session-prep overhead from the measured numbers.
for (var i = 0; i < warmupRuns; i++)
{
    _ = engine.Generate(sentences[0], 1.0f, 0);
}

double totalAudioSeconds = 0;
double totalWallSeconds = 0;

foreach (var sentence in sentences)
{
    for (var i = 0; i < measuredRuns; i++)
    {
        var stopwatch = Stopwatch.StartNew();
        var audio = engine.Generate(sentence, 1.0f, 0);
        stopwatch.Stop();

        var audioSeconds = audio.Samples.Length / (double)audio.SampleRate;
        var wallSeconds = stopwatch.Elapsed.TotalSeconds;
        totalAudioSeconds += audioSeconds;
        totalWallSeconds += wallSeconds;

        Console.WriteLine(
            $"  [{sentence[..Math.Min(30, sentence.Length)],-30}] audio={audioSeconds,6:F2}s wall={wallSeconds,6:F3}s RTF={wallSeconds / audioSeconds,6:F3}");
    }
}

Console.WriteLine();
Console.WriteLine($"Total audio generated: {totalAudioSeconds:F2}s over {measuredRuns * sentences.Length} runs");
Console.WriteLine($"Total wall time:       {totalWallSeconds:F2}s");
Console.WriteLine($"Overall RTF (wall/audio, lower=faster): {totalWallSeconds / totalAudioSeconds:F4}");
Console.WriteLine($"Overall real-time multiple (audio/wall, higher=faster): {totalAudioSeconds / totalWallSeconds:F2}x");

return 0;
