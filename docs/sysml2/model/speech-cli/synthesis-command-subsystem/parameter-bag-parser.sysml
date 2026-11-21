package SpeechCli_ParameterBagParser {
    private import SoftwareItemMetadata::*;

    part def ParameterBagParser {
        doc /* Parses repeatable --tts-param/--stt-param key=value tokens (owned by SynthesisCommandSubsystem and
             * shared, unmodified, across speak, recognize, and ask) and validates each value against a resolved
             * model's declared NumericParameter/ChoiceParameter/BooleanParameter set, rejecting an unrecognized key
             * (deliberately stricter than the library's own silent-ignore contract). Its `Resolve` and `ParseToken`
             * members are C# `internal` (visible only to the SpeechCli test project). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/SynthesisCommandSubsystem/ParameterBagParser.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/SynthesisCommandSubsystem/ParameterBagParserTests.cs");
            designFiles = ("docs/design/speech-cli/synthesis-command-subsystem/parameter-bag-parser.md");
            verificationFiles = ("docs/verification/speech-cli/synthesis-command-subsystem/parameter-bag-parser.md");
            requirementFiles = ("docs/reqstream/speech-cli/synthesis-command-subsystem/parameter-bag-parser.yaml");
        }

        // Parses key=value tokens and checks them against the model's declared SpeechModelParameters.
        dependency ParameterBagParser to Speech_SpeechModelParameters::SpeechModelParameters;
    }
}
