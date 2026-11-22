package SpeechSttEvaluation_SttEvaluationGates {
    private import SoftwareItemMetadata::*;

    part def SttEvaluationGates {
        doc /* The single source of the accuracy gates used by --check and by the real-model system tests: the gated
             * model id (the ONNX Nemotron model) and the maximum corpus word error rates, 6% on the LibriSpeech subset
             * and 10% on the poem. Shared: compiled (linked) into the SpeechOnnxNemotronStt tests. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.SttEvaluation/SttEvaluationGates.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/SpeechOnnxNemotronSttTests.cs");
        }

        // Names the gated model by its catalog id.
        dependency SttEvaluationGates to SpeechOnnxNemotronStt_OnnxNemotronMultilingualRecognitionModel::OnnxNemotronMultilingualRecognitionModel;
    }
}