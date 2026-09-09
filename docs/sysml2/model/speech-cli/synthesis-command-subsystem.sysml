package SpeechCli {
    part def SynthesisCommandSubsystem {
        doc /* The one text-to-speech subcommand (speak), plus the ParameterBagParser
             * --tts-param validation utility, extending the ModelCommandsSubsystem's
             * ICliModelCatalog seam with two further members (GetPreferredAudioFormat,
             * CreateSynthesizer) rather than introducing a second, competing seam, and
             * its own small ICliPlaybackDeviceSource seam over real playback-device
             * resolution, letting speak's device-dispatch logic be unit tested
             * deterministically without depending on real PortAudio hardware. */

        comment designRef /* Design: docs/design/speech-cli/synthesis-command-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/synthesis-command-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/synthesis-command-subsystem.yaml */

        part speakCommand : SpeakCommand;
        part parameterBagParser : ParameterBagParser;
        part cliPlaybackDeviceSource : ICliPlaybackDeviceSource;
        part audioDeviceFactoryPlaybackDeviceSource : AudioDeviceFactoryPlaybackDeviceSource;
    }

    part def SpeakCommand {
        doc /* Implements speak: resolves text from --text/--file/stdin (mutually
             * exclusive), resolves and validates the requested synthesis model, validates
             * --tts-param values, strips audio tags when --no-tags is given, dispatches to
             * either a WAV file (--output-audio) or a real playback device
             * (--playback-device/default), and disposes the synthesizer before the
             * playback device. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/SynthesisCommandSubsystem/SpeakCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/SynthesisCommandSubsystem/SpeakCommandTests.cs */
    }

    part def ParameterBagParser {
        doc /* Parses repeatable --tts-param/--stt-param key=value tokens (shared,
             * unmodified, across speak, recognize, and ask) and validates each value
             * against a resolved model's declared NumericParameter/ChoiceParameter/
             * BooleanParameter set, rejecting an unrecognized key (deliberately stricter
             * than the library's own silent-ignore contract). */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Cli/ParameterBagParser.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/SynthesisCommandSubsystem/ParameterBagParserTests.cs */
    }

    part def ICliPlaybackDeviceSource {
        doc /* CLI-owned seam over real-playback-device resolution (PlaybackProbe,
             * CreatePlaybackDevice), mirroring ICliModelCatalog's "CLI-owned seam over a
             * sealed library type" pattern. Exists because AudioDeviceFactory's own
             * hardware-detection resolves to the honestly unavailable fallback whenever
             * no real PortAudio hardware is present, regardless of any injected probe -
             * making its public constructor unusable for deterministic tests on headless
             * CI, while its internal, hardware-injectable constructor is deliberately not
             * reachable from DemaConsulting.Speech.Cli.Tests. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/SynthesisCommandSubsystem/ICliPlaybackDeviceSource.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/SynthesisCommandSubsystem/SpeakCommandTests.cs */
    }

    part def AudioDeviceFactoryPlaybackDeviceSource {
        doc /* Production ICliPlaybackDeviceSource implementation, forwarding every call
             * unchanged to a real, composed AudioDeviceFactory - including its
             * hardware-detection behavior - so SpeakCommand's real playback-device
             * resolution is exactly what it was before this seam was introduced. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/SynthesisCommandSubsystem/AudioDeviceFactoryPlaybackDeviceSource.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/SynthesisCommandSubsystem/AudioDeviceFactoryPlaybackDeviceSourceTests.cs */
    }
}
