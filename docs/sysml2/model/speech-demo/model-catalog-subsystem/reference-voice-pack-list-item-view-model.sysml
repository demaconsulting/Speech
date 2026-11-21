package SpeechDemo_ReferenceVoicePackListItemViewModel {
    private import SoftwareItemMetadata::*;

    part def ReferenceVoicePackListItemViewModel {
        doc /* One reference voice pack row: the pack identity, size, license and attribution, its install state,
             * progress and failure explanation, the include-emotions option and installed-size caption, plus the
             * derived captions and enablement values the row displays. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/ModelCatalogSubsystem/ReferenceVoicePackListItemViewModel.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/ModelCatalogSubsystem/ReferenceVoicePackListItemViewModelTests.cs");
            designFiles = ("docs/design/speech-demo/model-catalog-subsystem/reference-voice-pack-list-item-view-model.md");
            verificationFiles = ("docs/verification/speech-demo/model-catalog-subsystem/reference-voice-pack-list-item-view-model.md");
            requirementFiles = ("docs/reqstream/speech-demo/model-catalog-subsystem/reference-voice-pack-list-item-view-model.yaml");
        }

        // Shows a voice pack and its install state from the voice library as an observable MVVM row.
        dependency ReferenceVoicePackListItemViewModel to Speech_ReferenceVoiceLibrary::ReferenceVoiceLibrary;
        dependency ReferenceVoicePackListItemViewModel to Speech_ReferenceVoicePack::ReferenceVoicePack;
        dependency ReferenceVoicePackListItemViewModel to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency ReferenceVoicePackListItemViewModel to OtsDependencies::CommunityToolkitMvvm;
    }
}
