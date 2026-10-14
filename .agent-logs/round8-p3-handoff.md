# Round 8 P3 (ModelManagement) handoff for P7

`docs/reqstream/speech.yaml` needs `children:` additions (P3 did not edit it). Current parents:

- `Speech-Models-DownloadFailureReporting` (lists `Speech-Models-RicherFailureClassification`): add
  `Speech-Models-FailureErrorPopulated`, `Speech-Models-CallerCancellationNotAFailure`.
- `Speech-Models-DownloadMirror` (lists `Speech-Models-MirrorRedirect`): add `Speech-Models-MirrorAuthentication`
  (existing orphan), `Speech-Models-MirrorAuthenticationRedirectStripping`,
  `Speech-Models-MirrorAuthenticationTransportPolicy`.
- `Speech-Models-CatalogAndContract` (lists `Speech-Models-ContractDeclaration`, `-TunableParameters`): add
  `Speech-Models-ContractParameterDeclaration`, `Speech-Models-ContractAudioTagAndDownloadDeclaration`,
  `Speech-Models-ContractLicenseDeclaration`, `Speech-Models-ContractHooks`, `Speech-Models-ContractRoleMembers`,
  `Speech-Models-TunableParameterDescriptorValidation`, `Speech-Models-TunableParameterArgumentValidation`.
- `Speech-Models-InstallStateCatalog` (lists `Speech-Models-CatalogEnumeration`): add
  `Speech-Models-CatalogStateReporting`.
- Install/store parent (wherever `Speech-Models-AtomicInstall` is listed): add `Speech-Models-LeftoverCleanup`
  if not yet present.

Other notes:

- No files were added, moved or deleted, so `.reviewmark.yaml` needs no change.
- Requirement IDs `...AtomicSwap` / `...AtomicSwapReplacesExisting` (unit level) and
  `Speech-Models-AtomicInstall` are kept for stability; their text was weakened per decision 1.
- `docs/sysml2` has no references to the renamed tests (`ValidateAndReport_*` now prefixed with
  `SpeechModelParameterDiagnostics_`).
