package SpeechPhonemesEn_Morphology {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEn_CmuLexicon::*;
    private import SpeechPhonemesEn_Phone::*;

    enum def SuffixKind {
        enum PluralS;
        enum PastEd;
        enum Ing;
        enum PossessiveS;
        enum PluralPossessive;
        enum ComparativeEr;
        enum SuperlativeEst;
        enum AdverbLy;
        enum ContractionD;
        enum ContractionLl;
        enum ContractionVe;
        enum ContractionRe;
        enum ContractionM;
        enum ContractionNt;
    }

    part def Morphology {
        doc /* Internal sound rules for regular word endings: chooses the sound of a plural, past tense, -ing,
             * comparative, superlative, -ly, possessive or contraction ending by the last phone of the stem, attaches
             * clitics to a host pronunciation, and derives the pronunciation of a word that is a known stem plus a
             * regular ending (SuffixKind and Derivation describe the endings and the result). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Phonemes.En/LexiconSubsystem/Morphology.cs");
            testFiles = ("test/DemaConsulting.Speech.Phonemes.En.Tests/LexiconSubsystemTests.Morphology.cs");
            designFiles = ("docs/design/speech-phonemes-en/lexicon-subsystem/morphology.md");
            verificationFiles = ("docs/verification/speech-phonemes-en/lexicon-subsystem/morphology.md");
            requirementFiles = ("docs/reqstream/speech-phonemes-en/lexicon-subsystem/morphology.yaml");
        }

        // Builds phones for regular word endings from the base word's CMU lexicon pronunciation.
        dependency Morphology to CmuLexicon;
        dependency Morphology to Phone;
    }
}
