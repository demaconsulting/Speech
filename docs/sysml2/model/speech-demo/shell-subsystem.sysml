package SpeechDemo {
    part def ShellSubsystem {
        doc /* The application's manual composition root, window shell, and capability
             * navigation: it builds the library entry points and demo service adapters,
             * injects them into the panel view models, and presents one titled panel per
             * demonstrated capability. */

        comment designRef /* Design: docs/design/speech-demo/shell-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/shell-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/shell-subsystem.yaml */

        part mainWindowViewModel : MainWindowViewModel;
        part demoPanelViewModel : DemoPanelViewModel;
    }
}
