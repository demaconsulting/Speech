package SpeechPhonemesEnTools_DataManifest {
    private import SoftwareItemMetadata::*;

    part def DataManifest {
        doc /* Reader and verifier of data/phonemes-en/MANIFEST.tsv: the SHA-256 pins of the dictionary input and of
             * the two derived images. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/LexiconSubsystem/DataManifest.cs");
        }
    }
}
