package SpeechTextEn {
    doc
    /*
     * SpeechTextEn is the DemaConsulting.Speech.Text.En library that supplies an English
     * implementation of the Speech library's ITextAnalyzer seam: part-of-speech annotation of the
     * words of a speech request and authoritative sentence boundaries, delivered to an
     * annotation-aware synthesis backend as language-neutral annotations. It is a preview. Speech
     * must never depend on it. It depends on the Speech library's public API and on the
     * DemaConsulting.PartOfSpeech package (an OTS item), and on no inference runtime.
     */

    part def SpeechTextEnSystem {
        doc /* The SpeechTextEn library as a whole. */

        comment designRef /* Design: docs/design/speech-text-en.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en.yaml */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/SpeechTextEnTests.cs */

        part analysisSubsystem : AnalysisSubsystem;
        part partOfSpeech : PartOfSpeech;
    }
}
