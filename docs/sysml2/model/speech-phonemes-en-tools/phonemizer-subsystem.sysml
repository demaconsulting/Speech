package SpeechPhonemesEnTools_PhonemizerSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEnTools_GoldenCorpus::*;
    private import SpeechPhonemesEnTools_RegenerateGoldensCommand::*;
    private import SpeechPhonemesEnTools_SymbolInventory::*;

    part def PhonemizerSubsystem {
        doc /* The regenerate-goldens command: regenerates the generated columns of the phonemizer golden corpora and
             * reports how well the real text analysis labels the readings corpus. */

        metadata info : SoftwareInfo {
        }

        part regenerateGoldensCommand : RegenerateGoldensCommand;
        part goldenCorpus : GoldenCorpus;
        part symbolInventory : SymbolInventory;
    }
}
