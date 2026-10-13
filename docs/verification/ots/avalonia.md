## Avalonia Verification

This document provides the verification evidence for the Avalonia OTS software item.
Requirements for this OTS item are defined in the Avalonia OTS Software Requirements document.

### Required Functionality

Avalonia provides the cross-platform desktop application host, styling, and view layer for the
SpeechDemo application. The demo relies on it for application configuration and start-up, XAML
views with compiled bindings, and the Fluent theme with the bundled Inter font.

### Verification Approach

Automated verification for this OTS item is intentionally limited to deterministic behaviors that
do not require a display:

- The demo's Avalonia application host can be configured through the application builder against the
  referenced packages without throwing, producing a builder bound to the demo's application type
- The demo's application type derives from the Avalonia application base class (a smoke test of the
  type wiring; it cannot distinguish a demo regression from an Avalonia one)

Automated tests do **not** start a windowing lifetime, open a window, or drive the user
interface. A CI runner has no display, so doing so would be fragile, and it would add no evidence
the view-model tests do not already provide: the views contain no application logic and all
bindings are compiled, so a mistyped binding fails the build rather than failing silently at
runtime. Visual appearance and interactive behavior remain manual/local verification activities.

The OTS tests live in `test/DemaConsulting.Speech.Demo.Tests/Ots/` rather than in a repository-level
`test/OtsSoftwareTests/` project. This is a documented deviation: the tests need `InternalsVisibleTo`
access to the demo's `Program` and `App` types, which a separate project would not have.

### Test Scenarios

#### Avalonia_BuildAvaloniaApp_Invoked_ReturnsConfiguredApplicationBuilder

**Scenario**: The application host is configured exactly as the process entry point does.

**Expected**: Avalonia returns a builder bound to the demo's own application type without throwing.
Platform backend selection and font loading happen when the builder is started and are not
exercised here.

#### Avalonia_DemoApplication_Inspected_DerivesFromAvaloniaApplication

**Scenario**: The demo's application object is created without starting any lifetime.

**Expected**: It is an Avalonia application (smoke test), which is what lets Avalonia own the demo's
styling, resources, and desktop lifetime.
