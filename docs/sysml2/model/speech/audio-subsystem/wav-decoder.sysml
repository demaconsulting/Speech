package Speech_WavDecoder {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioClip::*;

    part def WavDecoder {
        doc /* Internal RIFF/WAVE decoder for PCM 8/16/24/32-bit and float32 audio. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/WavDecoder.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/WavDecoderTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/wav-decoder.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/wav-decoder.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/wav-decoder.yaml");
        }

        // Decodes RIFF/WAVE data into AudioClip values for the file reader.
        dependency WavDecoder to AudioClip;
    }
}
