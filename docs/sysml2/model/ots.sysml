package Speech {
    comment buildToolNote /* The build-tool OTS parts below (BuildMark, FileAssert, Pandoc, ReqStream,
        * ReviewMark, SarifMark, SonarMark, SysML2Tools, VersionMark, WeasyPrint and xUnit) are
        * repository-level process tooling shared by every package; they are not linked into any
        * shipped assembly, so they have no part usage inside a software-system part def. Their
        * requirements are grouped under the Quality-* requirements in docs/reqstream/quality.yaml. */

    part def PortAudioSharp2 {
        doc /* Managed PortAudio binding and carrier for the supported transitive native runtime
             * packages consumed by the Speech PortAudio child subsystem. */

        comment designRef /* Design: docs/design/ots/port-audio-sharp2.md */
        comment verificationRef /* Verification: docs/verification/ots/port-audio-sharp2.md */
        comment reqRef /* Requirements: docs/reqstream/ots/port-audio-sharp2.yaml */
    }

    part def SystemNumericsTensors {
        doc /* Maintained vectorized numeric primitives (TensorPrimitives) used by the Speech
             * AudioSubsystem and SynthesisSubsystem for bulk sample math. */

        comment designRef /* Design: docs/design/ots/system-numerics-tensors.md */
        comment verificationRef /* Verification: docs/verification/ots/system-numerics-tensors.md */
        comment reqRef /* Requirements: docs/reqstream/ots/system-numerics-tensors.yaml */
    }

    part def BuildMark {
        doc /* Build-tool OTS item: generates build-notes documentation from GitHub Actions
             * workflow-run metadata. */

        comment designRef /* Design: docs/design/ots/buildmark.md */
        comment verificationRef /* Verification: docs/verification/ots/buildmark.md */
        comment reqRef /* Requirements: docs/reqstream/ots/buildmark.yaml */
    }

    part def FileAssert {
        doc /* Build-tool OTS item: asserts that generated HTML and PDF documents exist and
             * contain the expected content. */

        comment designRef /* Design: docs/design/ots/fileassert.md */
        comment verificationRef /* Verification: docs/verification/ots/fileassert.md */
        comment reqRef /* Requirements: docs/reqstream/ots/fileassert.yaml */
    }

    part def Pandoc {
        doc /* Build-tool OTS item: converts Markdown documentation to HTML in the
             * documentation build pipeline. */

        comment designRef /* Design: docs/design/ots/pandoc.md */
        comment verificationRef /* Verification: docs/verification/ots/pandoc.md */
        comment reqRef /* Requirements: docs/reqstream/ots/pandoc.yaml */
    }

    part def ReqStream {
        doc /* Build-tool OTS item: lints requirements files and enforces that every
             * requirement is linked to passing test evidence. */

        comment designRef /* Design: docs/design/ots/reqstream.md */
        comment verificationRef /* Verification: docs/verification/ots/reqstream.md */
        comment reqRef /* Requirements: docs/reqstream/ots/reqstream.yaml */
    }

    part def ReviewMark {
        doc /* Build-tool OTS item: generates review plans and reports and enforces review
             * currency for the repository's review-sets. */

        comment designRef /* Design: docs/design/ots/reviewmark.md */
        comment verificationRef /* Verification: docs/verification/ots/reviewmark.md */
        comment reqRef /* Requirements: docs/reqstream/ots/reviewmark.yaml */
    }

    part def SarifMark {
        doc /* Build-tool OTS item: converts CodeQL SARIF scan results into a markdown report. */

        comment designRef /* Design: docs/design/ots/sarifmark.md */
        comment verificationRef /* Verification: docs/verification/ots/sarifmark.md */
        comment reqRef /* Requirements: docs/reqstream/ots/sarifmark.yaml */
    }

    part def SonarMark {
        doc /* Build-tool OTS item: retrieves SonarCloud quality-gate data and renders it as a
             * markdown report. */

        comment designRef /* Design: docs/design/ots/sonarmark.md */
        comment verificationRef /* Verification: docs/verification/ots/sonarmark.md */
        comment reqRef /* Requirements: docs/reqstream/ots/sonarmark.yaml */
    }

    part def SysML2Tools {
        doc /* Build-tool OTS item: lints and renders the repository's SysML2 architecture
             * model. */

        comment designRef /* Design: docs/design/ots/sysml2tools.md */
        comment verificationRef /* Verification: docs/verification/ots/sysml2tools.md */
        comment reqRef /* Requirements: docs/reqstream/ots/sysml2tools.yaml */
    }

    part def VersionMark {
        doc /* Build-tool OTS item: captures and publishes the versions of the dotnet tools
             * used in the pipeline. */

        comment designRef /* Design: docs/design/ots/versionmark.md */
        comment verificationRef /* Verification: docs/verification/ots/versionmark.md */
        comment reqRef /* Requirements: docs/reqstream/ots/versionmark.yaml */
    }

    part def WeasyPrint {
        doc /* Build-tool OTS item: converts generated HTML documents to PDF. */

        comment designRef /* Design: docs/design/ots/weasyprint.md */
        comment verificationRef /* Verification: docs/verification/ots/weasyprint.md */
        comment reqRef /* Requirements: docs/reqstream/ots/weasyprint.yaml */
    }

    part def XUnit {
        doc /* Build-tool OTS item: the xUnit v3 unit-testing framework and Visual Studio
             * runner used by the test projects. */

        comment designRef /* Design: docs/design/ots/xunit.md */
        comment verificationRef /* Verification: docs/verification/ots/xunit.md */
        comment reqRef /* Requirements: docs/reqstream/ots/xunit.yaml */
    }
}

package SpeechOnnx {
    part def MicrosoftMlOnnxRuntime {
        doc /* Managed ONNX Runtime inference API consumed by the SpeechOnnx, SpeechOnnxKokoro,
             * SpeechOnnxNemotronStt, and SpeechOnnxChatterboxTurbo libraries. */

        comment designRef /* Design: docs/design/ots/microsoft-ml-onnxruntime.md */
        comment verificationRef /* Verification: docs/verification/ots/microsoft-ml-onnxruntime.md */
        comment reqRef /* Requirements: docs/reqstream/ots/microsoft-ml-onnxruntime.yaml */
    }
}

package SpeechDemo {
    part def Avalonia {
        doc /* Cross-platform XAML desktop UI framework providing the application host,
             * styling, and view layer for the SpeechDemo application. */

        comment designRef /* Design: docs/design/ots/avalonia.md */
        comment verificationRef /* Verification: docs/verification/ots/avalonia.md */
        comment reqRef /* Requirements: docs/reqstream/ots/avalonia.yaml */
    }

    part def CommunityToolkitMvvm {
        doc /* MVVM source generators supplying the observable-property change notification
             * and the bindable synchronous/asynchronous commands used by the SpeechDemo
             * view models. */

        comment designRef /* Design: docs/design/ots/community-toolkit-mvvm.md */
        comment verificationRef /* Verification: docs/verification/ots/community-toolkit-mvvm.md */
        comment reqRef /* Requirements: docs/reqstream/ots/community-toolkit-mvvm.yaml */
    }
}

package SpeechSherpa {
    part def SherpaOnnx {
        doc /* Managed local speech-inference API and carrier for its per-platform native runtime
             * packages, consumed by the SpeechSherpa recognition and synthesis engines behind
             * Speech's public backend seams. */

        comment designRef /* Design: docs/design/ots/sherpa-onnx.md */
        comment verificationRef /* Verification: docs/verification/ots/sherpa-onnx.md */
        comment reqRef /* Requirements: docs/reqstream/ots/sherpa-onnx.yaml */
    }

    part def SharpCompress {
        doc /* Permissively-licensed managed archive library providing the BZip2 tar
             * (.tar.bz2) decoder the BCL lacks, consumed by the SpeechSherpa
             * ModelManagementSubsystem's TarBz2ArchiveExtractor to unpack downloaded
             * sherpa-onnx model archives. */

        comment designRef /* Design: docs/design/ots/sharp-compress.md */
        comment verificationRef /* Verification: docs/verification/ots/sharp-compress.md */
        comment reqRef /* Requirements: docs/reqstream/ots/sharp-compress.yaml */
    }
}
