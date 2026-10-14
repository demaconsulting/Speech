package SpeechCli {
    part def SynthesisCommandSubsystem {
        doc /* The one text-to-speech subcommand (speak), plus the ParameterBagParser
             * --tts-param validation utility, extending the ModelCommandsSubsystem's
             * ICliModelCatalog seam with two further members (GetPreferredAudioFormat,
             * CreateSynthesizerEngineAsync) rather than introducing a second, competing
             * seam, and its own small ICliPlaybackDeviceSource seam over real
             * playback-device resolution, letting speak's device-dispatch logic be unit
             * tested deterministically without depending on real PortAudio hardware. */

        comment designRef /* Design: docs/design/speech-cli/synthesis-command-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/synthesis-command-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/synthesis-command-subsystem.yaml */

        part speakCommand : SpeakCommand;
        part parameterBagParser : ParameterBagParser;
        part cliPlaybackDeviceSource : ICliPlaybackDeviceSource;
        part audioDeviceFactoryPlaybackDeviceSource : AudioDeviceFactoryPlaybackDeviceSource;
    }
}
