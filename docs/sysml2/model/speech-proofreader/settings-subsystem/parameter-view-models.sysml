package SpeechProofreader_ParameterViewModels {
    private import SoftwareItemMetadata::*;

    part def ParameterViewModels {
        doc /* Presenters for the library's numeric, choice and boolean parameter kinds, chosen by concrete type. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Settings/ParameterViewModels.cs");
        }

        // Wraps each SpeechModelParameters kind in an MVVM editor.
        dependency ParameterViewModels to Speech_SpeechModelParameters::SpeechModelParameters;
        dependency ParameterViewModels to OtsDependencies::CommunityToolkitMvvm;
    }
}
