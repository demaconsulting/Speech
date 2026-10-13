## CommunityToolkit.Mvvm Verification

This document provides the verification evidence for the CommunityToolkit.Mvvm OTS software item.
Requirements for this OTS item are defined in the CommunityToolkit.Mvvm OTS Software Requirements
document.

### Required Functionality

CommunityToolkit.Mvvm provides the MVVM source generators the SpeechDemo view models are built
on: `ObservableObject`, the `[ObservableProperty]` generator, and the `[RelayCommand]` generator
for both synchronous and cancellable asynchronous commands.

### Verification Approach

Because these members are generated at compile time rather than written by hand, they are
verified as ordinary public API: the tests assign generated properties and assert the resulting
change notification, and execute the generated commands and assert their effects. A
source-generator regression therefore surfaces as a test failure rather than as a silently
unbound user interface.

### Test Scenarios

#### CommunityToolkitMvvm_ObservableProperty_Assigned_RaisesPropertyChanged

**Scenario**: A generated observable property is assigned on a demo view model.

**Expected**: The value is stored and the corresponding change notification is raised.

#### CommunityToolkitMvvm_RelayCommand_Executed_InvokesAnnotatedMethod

**Scenario**: A generated synchronous command (`RecognitionPanelViewModel.RefreshCommand`, over a
`void` annotated method) is executed the way a bound button does.

**Expected**: The command is not an async command, reports itself executable, and the annotated
method runs to completion (the model list is re-read).

#### CommunityToolkitMvvm_RelayCommand_TestLocalPartialClass_GeneratesSynchronousCommand

**Scenario**: A test-local `partial` class deriving from `ObservableObject` declares a `void`
method annotated with `[RelayCommand]`.

**Expected**: The generator emits a synchronous command that runs the annotated method once,
demonstrating the generator prerequisites independently of any demo view model.

#### CommunityToolkitMvvm_AsyncRelayCommand_Executed_AwaitsAnnotatedMethod

**Scenario**: A generated asynchronous command is executed and awaited.

**Expected**: The command is an awaitable async command, and the awaited method completes and
updates the bound state.

#### CommunityToolkitMvvm_AsyncRelayCommand_Canceled_SignalsTokenToAnnotatedMethod

**Scenario**: The generated download command is started over a download that only ends when its
`CancellationToken` is canceled, and the command is then canceled.

**Expected**: The command reports it can be canceled, the annotated method observes the
cancellation through its token, and the row returns to not-downloaded with a cancellation message.

#### CommunityToolkitMvvm_NotifyPropertyChangedFor_StateAssigned_RaisesDependentPropertyChanged

**Scenario**: The generated `State` property of a model row is assigned.

**Expected**: Change notification is raised for the dependent computed properties
(`StatusText`, `CanDownload`) as well as for `State`.

### Test Location

The OTS tests live in `test/DemaConsulting.Speech.Demo.Tests/Ots/` rather than in a repository-level
`test/OtsSoftwareTests/` project. This is a documented deviation: the tests exercise the demo's view
models, which are internal to the demo and only visible to the demo test project.
