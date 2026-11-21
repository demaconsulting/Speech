package SpeechDemo_MirrorOptionsFactory {
    private import SoftwareItemMetadata::*;

    part def MirrorOptionsFactory {
        doc /* The shared validator that builds a download mirror from raw strings for both launch-time and in-app
             * mirror configuration. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/MirrorOptionsFactory.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/AppLaunchOptionsTests.cs", "test/DemaConsulting.Speech.Demo.Tests/MirrorOptionsFactoryTests.cs");
            designFiles = ("docs/design/speech-demo/mirror-options-factory.md");
            verificationFiles = ("docs/verification/speech-demo/mirror-options-factory.md");
            requirementFiles = ("docs/reqstream/speech-demo/mirror-options-factory.yaml");
        }

        // Checks the raw mirror strings and builds the DownloadMirror used for downloads.
        dependency MirrorOptionsFactory to Speech_DownloadMirror::DownloadMirror;
    }
}
