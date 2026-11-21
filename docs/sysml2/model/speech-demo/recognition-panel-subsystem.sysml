package SpeechDemo_RecognitionPanelSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_CaptureDebugRecorder::*;
    private import SpeechDemo_IRecognizerSessionFactory::*;
    private import SpeechDemo_RecognitionPanelViewModel::*;
    private import SpeechDemo_RecognizerSessionFactory::*;

    part def RecognitionPanelSubsystem {
        doc /* The speech-to-text panel: a demo-owned session seam over the library's SpeechRecognizerFactory
             * narrowing a public model to the library's recognition role, and the Start/Stop streaming lifecycle with
             * progressive partial-then-final transcript rendering and honest reporting of every unavailable state. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-demo/recognition-panel-subsystem.md");
            verificationFiles = ("docs/verification/speech-demo/recognition-panel-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-demo/recognition-panel-subsystem.yaml");
        }

        part iRecognizerSessionFactory : IRecognizerSessionFactory;
        part recognizerSessionFactory : RecognizerSessionFactory;
        part recognitionPanelViewModel : RecognitionPanelViewModel;
        part captureDebugRecorder : CaptureDebugRecorder;
    }
}
