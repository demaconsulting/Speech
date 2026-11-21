package Speech_AudioTagDescriptor {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    enum def NaturalLanguageAudioTag {
        enum Excited;
        enum Serious;
        enum Sarcastic;
        enum Panicked;
        enum Bored;
        enum Sad;
        enum Slow;
        enum VerySlow;
        enum Fast;
        enum VeryFast;
        enum ShortPause;
        enum LongPause;
        enum Emphasis;
        enum Whispers;
        enum Soft;
        enum Loud;
        enum Breathy;
        enum Laughs;
        enum Sighs;
        enum Gasp;
        enum ClearsThroat;
        enum Snorts;
        enum Angry;
        enum Calm;
        enum Happy;
        enum Enthused;
        enum Surprised;
        enum Worried;
        enum Confused;
        enum Frustrated;
        enum Neutral;
    }

    enum def NaturalLanguageAudioTagKind {
        enum Emotion;
        enum Pace;
        enum DeliveryVolume;
        enum NonVerbal;
        enum Pause;
        enum Emphasis;
    }

    part def AudioTagDescriptor {
        doc /* Supporting record pairing one NaturalLanguageAudioTag with its kind and canonical display information,
             * as reported by AudioTagCatalog.Tags. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/SynthesisSubsystem/AudioTagDescriptor.cs", "src/DemaConsulting.Speech/SynthesisSubsystem/AudioTagCatalog.cs", "src/DemaConsulting.Speech/SynthesisSubsystem/NaturalLanguageAudioTag.cs", "src/DemaConsulting.Speech/SynthesisSubsystem/NaturalLanguageAudioTagKind.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/SynthesisSubsystem/AudioTagCatalogTests.cs");
            designFiles = ("docs/design/speech/synthesis-subsystem/audio-tag-parser.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem/audio-tag-parser.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem/audio-tag-parser.yaml");
        }

        attribute tag : NaturalLanguageAudioTag;
        attribute kind : NaturalLanguageAudioTagKind;
        attribute aliases : String [0..*];
    }
}
