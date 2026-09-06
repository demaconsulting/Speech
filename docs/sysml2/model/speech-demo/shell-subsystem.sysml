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

    part def MainWindowViewModel {
        doc /* Navigation state for the application window: the ordered panel list and the
             * currently selected panel. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/ShellSubsystem/MainWindowViewModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/ShellSubsystem/MainWindowViewModelTests.cs */
        comment designRef /* Design: docs/design/speech-demo/shell-subsystem.md (documented as a subsystem unit) */
        comment verificationRef /* Verification: docs/verification/speech-demo/shell-subsystem.md (documented as a subsystem unit) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/shell-subsystem.yaml (consolidated at subsystem level) */
    }

    part def DemoPanelViewModel {
        doc /* One navigable panel entry: a display title paired with the panel view model
             * the shell was composed with. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/ShellSubsystem/DemoPanelViewModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/ShellSubsystem/DemoPanelViewModelTests.cs */
        comment designRef /* Design: docs/design/speech-demo/shell-subsystem.md (documented as a subsystem unit) */
        comment verificationRef /* Verification: docs/verification/speech-demo/shell-subsystem.md (documented as a subsystem unit) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/shell-subsystem.yaml (consolidated at subsystem level) */
    }
}
