package SpeechTextEn_PronunciationOverrides {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_NormalizationAlignment::*;
    private import SpeechTextEn_ReadingInventory::*;

    enum def OverrideAction {
        enum Skip;
        enum Say;
        enum SpellOut;
        enum Pronunciation;
        enum Reading;
    }

    enum def PronunciationScheme {
        enum Ipa;
        enum Arpabet;
    }

    part def PronunciationOverrides {
        doc /* The immutable, thread-safe dictionary of host-supplied pronunciation overrides (with the
             * PronunciationOverrideEntry record, the OverrideAction and PronunciationScheme enumerations and the
             * internal OverrideSyntax grammars). It is built from entries or from a tab-separated file with
             * validation and size limits that report a position and a fixed reason, never content, and it resolves
             * the tokens of a text to token directives (speak as written, replace with plain words, spell by letter
             * name, attach a pronunciation) by whole-word, case-sensitive-then-case-insensitive matching with no
             * culture dependency. A reading override fixes the reading of a word by reading id and is validated
             * strictly against the ReadingInventory (the id must exist, be active and be a candidate of the word). It
             * rewrites nothing and decides nothing about spans: the NormalizationSubsystem applies the directives
             * before its built-in rules. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Text.En/AnalysisSubsystem/PronunciationOverrides.cs", "src/DemaConsulting.Speech.Text.En/AnalysisSubsystem/PronunciationOverrideEntry.cs", "src/DemaConsulting.Speech.Text.En/AnalysisSubsystem/OverrideSyntax.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/PronunciationOverridesTests.cs");
            designFiles = ("docs/design/speech-text-en/analysis-subsystem/pronunciation-overrides.md");
            verificationFiles = ("docs/verification/speech-text-en/analysis-subsystem/pronunciation-overrides.md");
            requirementFiles = ("docs/reqstream/speech-text-en/analysis-subsystem/pronunciation-overrides.yaml");
        }

        // Checks override readings against the inventory and uses alignment kinds to mark override
        // replacements.
        dependency PronunciationOverrides to NormalizationAlignment;
        dependency PronunciationOverrides to ReadingInventory;
    }
}
