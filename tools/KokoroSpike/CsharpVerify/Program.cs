using DemaConsulting.Speech.Onnx.ModelManagementSubsystem;

var phonemizer = new KokoroLexiconPhonemizer();
var vocab = new KokoroPhonemeVocabulary();

var text = "Life is like a box of chocolates. You never know what you are " +
           "gonna get. This is a test of a text to speech model running " +
           "through a local inference engine.";

var (phonemes, unknown) = phonemizer.Phonemize(text);
Console.WriteLine($"Phonemes: {phonemes}");
Console.WriteLine($"Unknown words: {string.Join(", ", unknown)}");
var ids = vocab.ToTokenIds(phonemes);
Console.WriteLine($"Token ids ({ids.Count}): {string.Join(",", ids)}");

var installDir = @"C:\work\DemaConsulting\Libraries\Speech\tools\KokoroSpike\installed";
var model = new OnnxKokoroEnglishSynthesisModel();

var backend = model.CreateBackend(installDir);
Console.WriteLine($"SampleRate: {backend.SampleRate}");

var sw = System.Diagnostics.Stopwatch.StartNew();
var audio = backend.Generate(text, 1.0f, 0); // speaker 0 = af_heart
sw.Stop();

var duration = audio.Samples.Length / (double)audio.SampleRate;
Console.WriteLine($"Samples: {audio.Samples.Length}, Duration: {duration:F3}s, Inference: {sw.Elapsed.TotalSeconds:F3}s, RTF: {sw.Elapsed.TotalSeconds / duration:F4}");

// Write WAV file for manual listening comparison against python_output.wav
WriteWav(@"C:\work\DemaConsulting\Libraries\Speech\tools\KokoroSpike\csharp_output.wav", audio.Samples, audio.SampleRate);
Console.WriteLine("Wrote csharp_output.wav");

static void WriteWav(string path, float[] samples, int sampleRate)
{
    using var fs = new FileStream(path, FileMode.Create);
    using var bw = new BinaryWriter(fs);

    var byteCount = samples.Length * 2; // 16-bit PCM
    bw.Write("RIFF"u8.ToArray());
    bw.Write(36 + byteCount);
    bw.Write("WAVE"u8.ToArray());
    bw.Write("fmt "u8.ToArray());
    bw.Write(16);
    bw.Write((short)1); // PCM
    bw.Write((short)1); // mono
    bw.Write(sampleRate);
    bw.Write(sampleRate * 2);
    bw.Write((short)2);
    bw.Write((short)16);
    bw.Write("data"u8.ToArray());
    bw.Write(byteCount);
    foreach (var sample in samples)
    {
        var clamped = Math.Clamp(sample, -1.0f, 1.0f);
        bw.Write((short)(clamped * short.MaxValue));
    }
}
