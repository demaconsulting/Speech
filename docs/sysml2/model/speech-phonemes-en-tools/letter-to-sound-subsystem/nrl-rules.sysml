package SpeechPhonemesEnTools_NrlRules {
    private import SoftwareItemMetadata::*;

    part def NrlRules {
        doc /* The rules baseline of the benchmark: context-sensitive letter-to-phone rules in the classic public NRL
             * report 7948 format followed by stress heuristics. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/LetterToSoundSubsystem/NrlRules.cs");
        }

        // Emits Phone sequences from NRL letter rules so the baseline is comparable to the models.
        dependency NrlRules to SpeechPhonemesEn_Phone::Phone;
    }
}
