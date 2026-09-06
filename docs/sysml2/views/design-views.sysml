// Views rendered into docs/design/generated/ for the Design document.
// Reopens the Speech package so each 'expose' statement can reference
// its target part def directly. Per SysML2Tools syntax, 'expose' is only valid
// inside a named 'view' usage (not a 'view def' definition), and it - not
// 'render' - is what scopes the rendered diagram content.
package Speech {
    view SoftwareStructureView {
        expose Speech;
        expose SpeechDemo;
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
