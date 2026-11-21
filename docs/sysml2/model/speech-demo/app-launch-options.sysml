package SpeechDemo_AppLaunchOptions {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_MirrorOptionsFactory::*;
    private import ScalarValues::*;

    part def AppLaunchOptions {
        doc /* The parser and holder of the demo's launch-time command-line options. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/AppLaunchOptions.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/AppLaunchOptionsTests.cs");
            designFiles = ("docs/design/speech-demo/app-launch-options.md");
            verificationFiles = ("docs/verification/speech-demo/app-launch-options.md");
            requirementFiles = ("docs/reqstream/speech-demo/app-launch-options.yaml");
        }

        attribute modelsDir : String [0..1];
        attribute voicesDir : String [0..1];
        attribute mirrorUrl : String [0..1];
        attribute mirrorUser : String [0..1];
        attribute mirrorPassword : String [0..1];
        attribute mirrorBearerToken : String [0..1];
        attribute help : Boolean;

        // Parses launch options into store and downloader options, using the factory to check mirror
        // settings.
        dependency AppLaunchOptions to MirrorOptionsFactory;
        dependency AppLaunchOptions to Speech_SpeechModelDownloaderOptions::SpeechModelDownloaderOptions;
        dependency AppLaunchOptions to Speech_SpeechModelStoreOptions::SpeechModelStoreOptions;
    }
}
