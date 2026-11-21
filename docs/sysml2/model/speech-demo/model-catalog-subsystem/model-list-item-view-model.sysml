package SpeechDemo_ModelListItemViewModel {
    private import SoftwareItemMetadata::*;

    part def ModelListItemViewModel {
        doc /* One catalog row: a model's identity and role, its install state, its download progress, and its failure
             * explanation, plus the derived captions and enablement values the row displays. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/ModelCatalogSubsystem/ModelListItemViewModel.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/ModelCatalogSubsystem/ModelListItemViewModelTests.cs");
            designFiles = ("docs/design/speech-demo/model-catalog-subsystem/model-list-item-view-model.md");
            verificationFiles = ("docs/verification/speech-demo/model-catalog-subsystem/model-list-item-view-model.md");
            requirementFiles = ("docs/reqstream/speech-demo/model-catalog-subsystem/model-list-item-view-model.yaml");
        }

        // Shows a model descriptor's identity, role and install state as an observable MVVM row.
        dependency ModelListItemViewModel to Speech_SpeechModelDescriptor::SpeechModelDescriptor;
        dependency ModelListItemViewModel to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency ModelListItemViewModel to OtsDependencies::CommunityToolkitMvvm;
    }
}
