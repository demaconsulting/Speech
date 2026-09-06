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

**Requirement coverage**: `SpeechDemo-OTS-CommunityToolkitMvvm-ChangeNotification`.

#### CommunityToolkitMvvm_RelayCommand_Executed_InvokesAnnotatedMethod

**Scenario**: A generated synchronous command is executed the way a bound button does.

**Expected**: The command reports itself executable and the annotated method runs.

**Requirement coverage**: `SpeechDemo-OTS-CommunityToolkitMvvm-Commands`.

#### CommunityToolkitMvvm_AsyncRelayCommand_Executed_AwaitsAnnotatedMethod

**Scenario**: A generated asynchronous command is executed and awaited.

**Expected**: The command is an awaitable async command, and the awaited method completes and
updates the bound state. Cancellation support is provided by the generator (the command exposes
an awaitable `ExecutionTask` and a `CancelCommand`) but is not itself exercised by this scenario.

**Requirement coverage**: `SpeechDemo-OTS-CommunityToolkitMvvm-Commands`.

### Requirements Coverage

- **`SpeechDemo-OTS-CommunityToolkitMvvm-ChangeNotification`**:
  `CommunityToolkitMvvm_ObservableProperty_Assigned_RaisesPropertyChanged`
- **`SpeechDemo-OTS-CommunityToolkitMvvm-Commands`**:
  `CommunityToolkitMvvm_RelayCommand_Executed_InvokesAnnotatedMethod`,
  `CommunityToolkitMvvm_AsyncRelayCommand_Executed_AwaitsAnnotatedMethod`
