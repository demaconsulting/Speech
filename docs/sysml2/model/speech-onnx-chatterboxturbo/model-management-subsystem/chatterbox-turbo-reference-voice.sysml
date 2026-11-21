package SpeechOnnxChatterboxTurbo_ChatterboxTurboReferenceVoice {
    private import SoftwareItemMetadata::*;

    part def ChatterboxTurboReferenceVoice {
        doc /* A reference voice created from in-memory audio or a WAV or FLAC file, normalized to mono 24 kHz (via
             * the core AudioResampler) and bounded in length. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/ModelManagementSubsystem/ChatterboxTurboReferenceVoice.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/ModelManagementSubsystem/ChatterboxTurboReferenceVoiceTests.cs");
            designFiles = ("docs/design/speech-onnx-chatterboxturbo/model-management-subsystem/chatterbox-turbo-reference-voice.md");
            verificationFiles = ("docs/verification/speech-onnx-chatterboxturbo/model-management-subsystem/chatterbox-turbo-reference-voice.md");
            requirementFiles = ("docs/reqstream/speech-onnx-chatterboxturbo/model-management-subsystem/chatterbox-turbo-reference-voice.yaml");
        }

        // Reads WAV/FLAC files with the audio file reader and resamples them to mono 24 kHz.
        dependency ChatterboxTurboReferenceVoice to Speech_AudioFileReader::AudioFileReader;
        dependency ChatterboxTurboReferenceVoice to Speech_AudioResampler::AudioResampler;
    }
}
