package SpeechDemo_CaptureDebugRecorder {
    private import SoftwareItemMetadata::*;

    part def CaptureDebugRecorder {
        doc /* Opt-in, off-by-default diagnostic instrumentation that records the live-microphone capture and
             * recognition event stream to help investigate dropped words after a long pause. It is a supporting
             * diagnostic type, not a user-facing feature. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/RecognitionPanelSubsystem/CaptureDebugRecorder.cs", "src/DemaConsulting.Speech.Demo/RecognitionPanelSubsystem/WavFileWriter.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/RecognitionPanelSubsystem/CaptureDebugRecorderTests.cs", "test/DemaConsulting.Speech.Demo.Tests/RecognitionPanelSubsystem/WavFileWriterTests.cs");
            designFiles = ("docs/design/speech-demo/recognition-panel-subsystem.md");
            verificationFiles = ("docs/verification/speech-demo/recognition-panel-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-demo/recognition-panel-subsystem.yaml");
        }

        // Records audio from an IAudioCaptureDevice to help debug lost words in live recognition.
        dependency CaptureDebugRecorder to Speech_IAudioCaptureDevice::IAudioCaptureDevice;
    }
}
