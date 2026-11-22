package SpeechSttEvaluation_SttDataSet {
    private import SoftwareItemMetadata::*;

    part def SttDataSet {
        doc /* Loads and verifies the evaluation data in data/stt-en: reads MANIFEST.tsv (SttDataItem rows), refuses
             * any audio file whose size or SHA-256 differs from the manifest, groups the items by set (LibriSpeech
             * subset, poem), and decodes the audio with the library's file reader, resampling when the rate differs.
             * Shared: compiled (linked) into the SpeechOnnxNemotronStt tests. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.SttEvaluation/SttDataSet.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/Evaluation/SttDataSetTests.cs");
        }

        // Decodes and resamples the verified recordings with the library's audio file pipeline.
        dependency SttDataSet to Speech_AudioFileReader::AudioFileReader;
        dependency SttDataSet to Speech_AudioResampler::AudioResampler;
    }
}