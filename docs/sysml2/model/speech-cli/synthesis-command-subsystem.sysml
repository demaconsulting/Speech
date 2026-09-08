package SpeechCli {
    part def SynthesisCommandSubsystem {
        doc /* The one text-to-speech subcommand (speak), plus the ParameterBagParser
             * --param validation utility, extending the ModelCommandsSubsystem's
             * ICliModelCatalog seam with two further members (GetPreferredAudioFormat,
             * CreateSynthesizer) rather than introducing a second, competing seam. */

        comment designRef /* Design: docs/design/speech-cli/synthesis-command-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/synthesis-command-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/synthesis-command-subsystem.yaml */

        part speakCommand : SpeakCommand;
        part parameterBagParser : ParameterBagParser;
    }

    part def SpeakCommand {
        doc /* Implements speak: resolves text from --text/--file/stdin (mutually
             * exclusive), resolves and validates the requested synthesis model, validates
             * --param values, strips audio tags when --no-tags is given, dispatches to
             * either a WAV file (--output) or a real playback device (--device/default),
             * and disposes the synthesizer before the playback device. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/SynthesisCommandSubsystem/SpeakCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/SynthesisCommandSubsystem/SpeakCommandTests.cs */
    }

    part def ParameterBagParser {
        doc /* Parses repeatable --param key=value tokens and validates each value against
             * a resolved model's declared NumericParameter/ChoiceParameter/BooleanParameter
             * set, rejecting an unrecognized key (deliberately stricter than the library's
             * own silent-ignore contract). */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Cli/ParameterBagParser.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/SynthesisCommandSubsystem/ParameterBagParserTests.cs */
    }
}
