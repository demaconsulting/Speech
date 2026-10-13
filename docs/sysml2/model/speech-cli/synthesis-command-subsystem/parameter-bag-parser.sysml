package SpeechCli {
    part def ParameterBagParser {
        doc /* Parses repeatable --tts-param/--stt-param key=value tokens (owned by SynthesisCommandSubsystem and shared,
             * unmodified, across speak, recognize, and ask) and validates each value
             * against a resolved model's declared NumericParameter/ChoiceParameter/
             * BooleanParameter set, rejecting an unrecognized key (deliberately stricter
             * than the library's own silent-ignore contract). */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/SynthesisCommandSubsystem/ParameterBagParser.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/SynthesisCommandSubsystem/ParameterBagParserTests.cs */
        comment designRef /* Design: docs/design/speech-cli/synthesis-command-subsystem.md (documented within the subsystem doc; no dedicated unit doc) */
        comment verificationRef /* Verification: docs/verification/speech-cli/synthesis-command-subsystem.md (verified within the subsystem doc; no dedicated unit doc) */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/synthesis-command-subsystem.yaml (requirements held in the subsystem file; no dedicated unit file) */
    }
}
