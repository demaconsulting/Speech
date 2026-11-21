package Speech_AudioFileReader {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioClip::*;
    private import Speech_FlacDecoder::*;
    private import Speech_WavDecoder::*;

    part def AudioFileReader {
        doc /* Decodes WAV and FLAC files or streams into AudioClip values with managed code only. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/AudioFileReader.cs", "src/DemaConsulting.Speech/AudioSubsystem/AudioFileReadOptions.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/AudioFileReaderTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/audio-file-reader.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/audio-file-reader.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/audio-file-reader.yaml");
        }

        // Routes files to the WAV or FLAC decoder and returns the decoded audio as an AudioClip.
        dependency AudioFileReader to AudioClip;
        dependency AudioFileReader to FlacDecoder;
        dependency AudioFileReader to WavDecoder;
    }
}
