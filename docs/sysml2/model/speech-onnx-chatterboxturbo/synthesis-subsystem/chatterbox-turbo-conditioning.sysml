package SpeechOnnxChatterboxTurbo_ChatterboxTurboConditioning {
    private import SoftwareItemMetadata::*;

    part def ChatterboxTurboConditioning {
        doc /* Supporting record holding the reference-voice conditioning embeddings produced once per (voice, mood)
             * by the speech encoder and reused for every utterance spoken with that clip. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/SynthesisSubsystem/ChatterboxTurboConditioning.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/SynthesisSubsystem/OnnxChatterboxTurboNetworksTests.cs", "test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/SynthesisSubsystem/ChatterboxTurboConditioningTests.cs");
            designFiles = ("docs/design/speech-onnx-chatterboxturbo/synthesis-subsystem/chatterbox-turbo-conditioning.md");
            verificationFiles = ("docs/verification/speech-onnx-chatterboxturbo/synthesis-subsystem/chatterbox-turbo-conditioning.md");
            requirementFiles = ("docs/reqstream/speech-onnx-chatterboxturbo/synthesis-subsystem/chatterbox-turbo-conditioning.yaml");
        }
    }
}
