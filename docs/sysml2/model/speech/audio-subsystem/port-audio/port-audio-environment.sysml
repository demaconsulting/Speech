package Speech_PortAudioEnvironment {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioDeviceInUseException::*;
    private import Speech_IPortAudioApi::*;
    private import Speech_PortAudioApi::*;
    private import Speech_PortAudioHostApiInfo::*;
    private import Speech_PortAudioHostApiType::*;

    part def PortAudioEnvironment {
        doc /* Shared production environment and policy helper that caches one PortAudio initialization attempt,
             * resolves the current platform's preferred host API, and tracks currently active capture/playback
             * streams so an on-demand device-table refresh can safely reinitialize the runtime or refuse to do so
             * while a stream is active. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/PortAudio/PortAudioEnvironment.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudio/PortAudioEnvironmentTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/port-audio/port-audio-environment.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/port-audio/port-audio-environment.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/port-audio/port-audio-environment.yaml");
        }

        // Initializes PortAudio once through the API seam, picks the preferred host API and flags devices
        // in use.
        dependency PortAudioEnvironment to AudioDeviceInUseException;
        dependency PortAudioEnvironment to IPortAudioApi;
        dependency PortAudioEnvironment to PortAudioApi;
        dependency PortAudioEnvironment to PortAudioHostApiInfo;
        dependency PortAudioEnvironment to PortAudioHostApiType;
    }
}
