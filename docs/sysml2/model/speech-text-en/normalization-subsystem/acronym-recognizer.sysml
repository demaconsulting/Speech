package SpeechTextEn_AcronymRecognizer {
    private import SoftwareItemMetadata::*;

    part def AcronymRecognizer {
        doc /* Internal recognizer of acronyms and initialisms: loads the embedded, self-authored acronyms lexicon
             * with strict validation and decides, case-sensitively, whether a token is spoken letter by letter
             * (listed forms, dotted forms, ampersand forms and short all-capital tokens without a vowel), guarding
             * against all-capital shouting. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Text.En/NormalizationSubsystem/AcronymRecognizer.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/AcronymRecognizerTests.cs");
            designFiles = ("docs/design/speech-text-en/normalization-subsystem/acronym-recognizer.md");
            verificationFiles = ("docs/verification/speech-text-en/normalization-subsystem/acronym-recognizer.md");
            requirementFiles = ("docs/reqstream/speech-text-en/normalization-subsystem/acronym-recognizer.yaml");
        }
    }
}
