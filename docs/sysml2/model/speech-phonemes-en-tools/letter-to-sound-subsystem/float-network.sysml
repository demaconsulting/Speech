package SpeechPhonemesEnTools_FloatNetwork {
    private import SoftwareItemMetadata::*;

    part def FloatNetwork {
        doc /* Deterministic floating-point network training (training set, settings, seeded random source and Adam
             * optimizer state) used by the trainer before quantization. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/LetterToSoundSubsystem/FloatNetwork.cs");
        }
    }
}
