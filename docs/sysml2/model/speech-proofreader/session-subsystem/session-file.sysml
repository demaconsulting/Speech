package SpeechProofreader_SessionFile {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_ProofreadSession::*;

    part def SessionFile {
        doc /* Reads and writes session JSON files (format version 1). */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Session/SessionFile.cs");
            testFiles = ("test/DemaConsulting.Speech.Proofreader.Tests/SessionFileTests.cs");
        }

        // Serializes and deserializes ProofreadSession objects as versioned JSON.
        dependency SessionFile to ProofreadSession;
    }
}
