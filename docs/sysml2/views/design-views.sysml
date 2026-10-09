// Views rendered into docs/design/generated/ for the Design document.
// Reopens the Speech package so each 'expose' statement can reference
// its target part def directly. Per SysML2Tools syntax, 'expose' is only valid
// inside a named 'view' usage (not a 'view def' definition), and it - not
// 'render' - is what scopes the rendered diagram content.
package Speech {
    view SoftwareStructureView {
        expose Speech;
        expose SpeechDemo;
        expose SpeechCli;
        expose SpeechSherpa;
        expose SpeechOnnx;
        expose SpeechOnnxKokoro;
    }

    view SpeechView {
        expose SpeechSystem;
    }

    view DiagnosticsView {
        expose Diagnostics;
    }

    view AudioSubsystemView {
        expose AudioSubsystem;
    }

    view PortAudioView {
        expose PortAudio;
    }

    view ModelManagementSubsystemView {
        expose ModelManagementSubsystem;
    }

    view RecognitionSubsystemView {
        expose RecognitionSubsystem;
    }

    view SynthesisSubsystemView {
        expose SynthesisSubsystem;
    }
}

// SpeechDemo views. Declared in their own package so each 'expose' statement can reference
// its target part def directly, exactly as the Speech views above do.
package SpeechDemo {
    view SpeechDemoView {
        expose SpeechDemoSystem;
    }

    view ShellSubsystemView {
        expose ShellSubsystem;
    }

    view DeviceSelectionSubsystemView {
        expose DeviceSelectionSubsystem;
    }

    view ModelCatalogSubsystemView {
        expose ModelCatalogSubsystem;
    }

    view ModelSettingsSubsystemView {
        expose ModelSettingsSubsystem;
    }

    view SynthesisPanelSubsystemView {
        expose SynthesisPanelSubsystem;
    }

    view RecognitionPanelSubsystemView {
        expose RecognitionPanelSubsystem;
    }
}

// SpeechCli views. Declared in their own package so each 'expose' statement can reference
// its target part def directly, exactly as the Speech views above do.
package SpeechCli {
    view SpeechCliView {
        expose SpeechCliSystem;
    }
}

// SpeechSherpa views. Declared in their own package so each 'expose' statement can reference
// its target part def directly, exactly as the Speech views above do. The subsystem views carry
// a 'Sherpa' prefix because rendered SVG filenames are keyed by bare view name, so reusing the
// Speech subsystem view names would overwrite the Speech package's own diagrams.
package SpeechSherpa {
    view SpeechSherpaView {
        expose SpeechSherpaSystem;
    }

    view SherpaModelManagementSubsystemView {
        expose ModelManagementSubsystem;
    }

    view SherpaRecognitionSubsystemView {
        expose RecognitionSubsystem;
    }

    view SherpaSynthesisSubsystemView {
        expose SynthesisSubsystem;
    }
}

// SpeechOnnx views. Declared in their own package so each 'expose' statement can reference
// its target part def directly, exactly as the Speech views above do.
package SpeechOnnx {
    view SpeechOnnxView {
        expose SpeechOnnxSystem;
    }

    view OnnxRuntimeSubsystemView {
        expose OnnxRuntimeSubsystem;
    }
}

// SpeechOnnxKokoro views. Declared in their own package so each 'expose' statement can reference
// its target part def directly, exactly as the Speech views above do. The subsystem views carry
// a 'Kokoro' prefix because rendered SVG filenames are keyed by bare view name, so reusing the
// Speech/SpeechSherpa subsystem view names would overwrite their own diagrams.
package SpeechOnnxKokoro {
    view SpeechOnnxKokoroView {
        expose SpeechOnnxKokoroSystem;
    }

    view KokoroModelManagementSubsystemView {
        expose ModelManagementSubsystem;
    }

    view KokoroSynthesisSubsystemView {
        expose SynthesisSubsystem;
    }
}
