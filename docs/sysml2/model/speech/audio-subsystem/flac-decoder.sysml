package Speech_FlacDecoder {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioClip::*;

    part def FlacDecoder {
        doc /* Internal managed FLAC decoder (stream, frame, bit reader and CRC helpers) for AudioFileReader. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/FlacDecoder.cs", "src/DemaConsulting.Speech/AudioSubsystem/FlacFrameDecoder.cs", "src/DemaConsulting.Speech/AudioSubsystem/FlacBitReader.cs", "src/DemaConsulting.Speech/AudioSubsystem/FlacCrc.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/FlacDecoderTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/flac-decoder.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/flac-decoder.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/flac-decoder.yaml");
        }

        // Decodes FLAC streams into AudioClip values for the file reader.
        dependency FlacDecoder to AudioClip;
    }
}
