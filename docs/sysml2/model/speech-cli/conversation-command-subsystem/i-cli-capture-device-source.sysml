package SpeechCli_ICliCaptureDeviceSource {
    private import SoftwareItemMetadata::*;

    part def ICliCaptureDeviceSource {
        doc /* CLI-owned seam over real-capture-device resolution (CaptureProbe, CreateCaptureDevice), mirroring
             * SynthesisCommandSubsystem's ICliPlaybackDeviceSource. Exists because AudioDeviceFactory's own hardware
             * detection resolves to the honestly unavailable fallback whenever no real PortAudio hardware is present,
             * regardless of any injected probe - making it unusable for deterministic microphone-mode happy-path
             * tests on a headless build agent. Both AskCommand and RecognizeCommand resolve their capture devices
             * through it. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/ConversationCommandSubsystem/ICliCaptureDeviceSource.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/ConversationCommandSubsystem/AudioDeviceFactoryCaptureDeviceSourceTests.cs");
            designFiles = ("docs/design/speech-cli/conversation-command-subsystem/i-cli-capture-device-source.md");
            verificationFiles = ("docs/verification/speech-cli/conversation-command-subsystem/i-cli-capture-device-source.md");
            requirementFiles = ("docs/reqstream/speech-cli/conversation-command-subsystem/i-cli-capture-device-source.yaml");
        }

        // Exposes the capture probe and device creation by selection so commands can be tested without
        // hardware.
        dependency ICliCaptureDeviceSource to Speech_AudioDeviceSelection::AudioDeviceSelection;
        dependency ICliCaptureDeviceSource to Speech_IAudioCaptureDevice::IAudioCaptureDevice;
        dependency ICliCaptureDeviceSource to Speech_IAudioCaptureDeviceProbe::IAudioCaptureDeviceProbe;
    }
}
