package SpeechCli_ICliPlaybackDeviceSource {
    private import SoftwareItemMetadata::*;

    part def ICliPlaybackDeviceSource {
        doc /* CLI-owned seam over real-playback-device resolution (PlaybackProbe, CreatePlaybackDevice), mirroring
             * ICliModelCatalog's "CLI-owned seam over a sealed library type" pattern. Exists because
             * AudioDeviceFactory's own hardware-detection resolves to the honestly unavailable fallback whenever no
             * real PortAudio hardware is present, regardless of any injected probe - making its public constructor
             * unusable for deterministic tests on headless CI, while its internal, hardware-injectable constructor is
             * deliberately not reachable from DemaConsulting.Speech.Cli.Tests. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/SynthesisCommandSubsystem/ICliPlaybackDeviceSource.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/SynthesisCommandSubsystem/SpeakCommandTests.cs");
            designFiles = ("docs/design/speech-cli/synthesis-command-subsystem/i-cli-playback-device-source.md");
            verificationFiles = ("docs/verification/speech-cli/synthesis-command-subsystem/i-cli-playback-device-source.md");
            requirementFiles = ("docs/reqstream/speech-cli/synthesis-command-subsystem/i-cli-playback-device-source.yaml");
        }

        // Exposes the playback probe and device creation by selection so commands can be tested without
        // hardware.
        dependency ICliPlaybackDeviceSource to Speech_AudioDeviceSelection::AudioDeviceSelection;
        dependency ICliPlaybackDeviceSource to Speech_IAudioPlaybackDevice::IAudioPlaybackDevice;
        dependency ICliPlaybackDeviceSource to Speech_IAudioPlaybackDeviceProbe::IAudioPlaybackDeviceProbe;
    }
}
