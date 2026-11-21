// Off-the-shelf (OTS) items used by the systems of this repository, one part def per item,
// matching docs/design/ots/{ots-name}.md. OTS items have no local integration code, so they
// carry design, verification and requirements locations only.
package OtsDependencies {
    private import SoftwareItemMetadata::*;

    doc /* Off-the-shelf (OTS) dependencies used by the Speech repository's systems. */

    comment buildToolNote
    /*
     * The build-tool OTS parts below (BuildMark, FileAssert, Pandoc, ReqStream, ReviewMark, SarifMark, SonarMark,
     * SysML2Tools, VersionMark, WeasyPrint and xUnit) are repository-level process tooling shared by every package;
     * they are not linked into any shipped assembly, so they have no part usage inside a software-system part def.
     * Their requirements are grouped under the Quality-* requirements in docs/reqstream/quality.yaml.
     */

    part def PortAudioSharp2 {
        doc /* Managed PortAudio binding and carrier for the supported transitive native runtime packages consumed by
             * the Speech PortAudio child subsystem. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/port-audio-sharp2.md");
            verificationFiles = ("docs/verification/ots/port-audio-sharp2.md");
            requirementFiles = ("docs/reqstream/ots/port-audio-sharp2.yaml");
        }
    }

    part def SystemNumericsTensors {
        doc /* Maintained vectorized numeric primitives (TensorPrimitives) used by the Speech AudioSubsystem and
             * SynthesisSubsystem for bulk sample math. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/system-numerics-tensors.md");
            verificationFiles = ("docs/verification/ots/system-numerics-tensors.md");
            requirementFiles = ("docs/reqstream/ots/system-numerics-tensors.yaml");
        }
    }

    part def BuildMark {
        doc /* Build-tool OTS item: generates build-notes documentation from GitHub Actions workflow-run metadata. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/buildmark.md");
            verificationFiles = ("docs/verification/ots/buildmark.md");
            requirementFiles = ("docs/reqstream/ots/buildmark.yaml");
        }
    }

    part def FileAssert {
        doc /* Build-tool OTS item: asserts that generated HTML and PDF documents exist and contain the expected
             * content. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/fileassert.md");
            verificationFiles = ("docs/verification/ots/fileassert.md");
            requirementFiles = ("docs/reqstream/ots/fileassert.yaml");
        }
    }

    part def Pandoc {
        doc /* Build-tool OTS item: converts Markdown documentation to HTML in the documentation build pipeline. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/pandoc.md");
            verificationFiles = ("docs/verification/ots/pandoc.md");
            requirementFiles = ("docs/reqstream/ots/pandoc.yaml");
        }
    }

    part def ReqStream {
        doc /* Build-tool OTS item: lints requirements files and enforces that every requirement is linked to passing
             * test evidence. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/reqstream.md");
            verificationFiles = ("docs/verification/ots/reqstream.md");
            requirementFiles = ("docs/reqstream/ots/reqstream.yaml");
        }
    }

    part def ReviewMark {
        doc /* Build-tool OTS item: generates review plans and reports and enforces review currency for the
             * repository's review-sets. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/reviewmark.md");
            verificationFiles = ("docs/verification/ots/reviewmark.md");
            requirementFiles = ("docs/reqstream/ots/reviewmark.yaml");
        }
    }

    part def SarifMark {
        doc /* Build-tool OTS item: converts CodeQL SARIF scan results into a markdown report. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/sarifmark.md");
            verificationFiles = ("docs/verification/ots/sarifmark.md");
            requirementFiles = ("docs/reqstream/ots/sarifmark.yaml");
        }
    }

    part def SonarMark {
        doc /* Build-tool OTS item: retrieves SonarCloud quality-gate data and renders it as a markdown report. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/sonarmark.md");
            verificationFiles = ("docs/verification/ots/sonarmark.md");
            requirementFiles = ("docs/reqstream/ots/sonarmark.yaml");
        }
    }

    part def SysML2Tools {
        doc /* Build-tool OTS item: lints and renders the repository's SysML2 architecture model. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/sysml2tools.md");
            verificationFiles = ("docs/verification/ots/sysml2tools.md");
            requirementFiles = ("docs/reqstream/ots/sysml2tools.yaml");
        }
    }

    part def VersionMark {
        doc /* Build-tool OTS item: captures and publishes the versions of the dotnet tools used in the pipeline. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/versionmark.md");
            verificationFiles = ("docs/verification/ots/versionmark.md");
            requirementFiles = ("docs/reqstream/ots/versionmark.yaml");
        }
    }

    part def WeasyPrint {
        doc /* Build-tool OTS item: converts generated HTML documents to PDF. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/weasyprint.md");
            verificationFiles = ("docs/verification/ots/weasyprint.md");
            requirementFiles = ("docs/reqstream/ots/weasyprint.yaml");
        }
    }

    part def XUnit {
        doc /* Build-tool OTS item: the xUnit v3 unit-testing framework and Visual Studio runner used by the test
             * projects. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/xunit.md");
            verificationFiles = ("docs/verification/ots/xunit.md");
            requirementFiles = ("docs/reqstream/ots/xunit.yaml");
        }
    }

    part def MicrosoftMlOnnxRuntime {
        doc /* Managed ONNX Runtime inference API consumed by the SpeechOnnx, SpeechOnnxKokoro, SpeechOnnxNemotronStt,
             * and SpeechOnnxChatterboxTurbo libraries. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/microsoft-ml-onnxruntime.md");
            verificationFiles = ("docs/verification/ots/microsoft-ml-onnxruntime.md");
            requirementFiles = ("docs/reqstream/ots/microsoft-ml-onnxruntime.yaml");
        }
    }

    part def TestResults {
        doc /* Maintained TRX and JUnit serializer (DemaConsulting.TestResults) used by the SpeechCli Validation unit
             * to write self-validation results. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/test-results.md");
            verificationFiles = ("docs/verification/ots/test-results.md");
            requirementFiles = ("docs/reqstream/ots/test-results.yaml");
        }
    }

    part def PartOfSpeech {
        doc /* Managed English tokenizer and Universal Dependencies part-of-speech tagger
             * (DemaConsulting.PartOfSpeech, a pre-release package with its own embedded model and lexicon) consumed
             * only by the SpeechTextEn PartOfSpeechAdapter unit. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/part-of-speech.md");
            verificationFiles = ("docs/verification/ots/part-of-speech.md");
            requirementFiles = ("docs/reqstream/ots/part-of-speech.yaml");
        }
    }

    part def Avalonia {
        doc /* Cross-platform XAML desktop UI framework providing the application host, styling, and view layer for
             * the SpeechDemo application. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/avalonia.md");
            verificationFiles = ("docs/verification/ots/avalonia.md");
            requirementFiles = ("docs/reqstream/ots/avalonia.yaml");
        }
    }

    part def CommunityToolkitMvvm {
        doc /* MVVM source generators supplying the observable-property change notification and the bindable
             * synchronous/asynchronous commands used by the SpeechDemo view models. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/community-toolkit-mvvm.md");
            verificationFiles = ("docs/verification/ots/community-toolkit-mvvm.md");
            requirementFiles = ("docs/reqstream/ots/community-toolkit-mvvm.yaml");
        }
    }

    part def SherpaOnnx {
        doc /* Managed local speech-inference API and carrier for its per-platform native runtime packages, consumed
             * by the SpeechSherpa recognition and synthesis engines behind Speech's public backend seams. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/sherpa-onnx.md");
            verificationFiles = ("docs/verification/ots/sherpa-onnx.md");
            requirementFiles = ("docs/reqstream/ots/sherpa-onnx.yaml");
        }
    }

    part def SharpCompress {
        doc /* Permissively-licensed managed archive library providing the BZip2 tar (.tar.bz2) decoder the BCL lacks,
             * consumed by the SpeechSherpa ModelManagementSubsystem's TarBz2ArchiveExtractor to unpack downloaded
             * sherpa-onnx model archives. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/sharp-compress.md");
            verificationFiles = ("docs/verification/ots/sharp-compress.md");
            requirementFiles = ("docs/reqstream/ots/sharp-compress.yaml");
        }
    }
}
