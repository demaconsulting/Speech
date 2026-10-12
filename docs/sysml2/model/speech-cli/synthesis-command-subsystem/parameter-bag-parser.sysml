package SpeechCli {
    part def ParameterBagParser {
        doc /* Parses repeatable --tts-param/--stt-param key=value tokens (owned by SynthesisCommandSubsystem and shared,
             * unmodified, across speak, recognize, and ask) and validates each value
             * against a resolved model's declared NumericParameter/ChoiceParameter/
             * BooleanParameter set, rejecting an unrecognized key (deliberately stricter
             * than the library's own silent-ignore contract). */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/SynthesisCommandSubsystem/ParameterBagParser.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/SynthesisCommandSubsystem/ParameterBagParserTests.cs */
    }
}
