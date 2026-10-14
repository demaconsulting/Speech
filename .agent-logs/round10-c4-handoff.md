# Round 10 C4 Handoff (shared files owned by F)

## docs/reqstream/speech-cli.yaml (system level) - stale test names and new child links

Renamed or split Context tests (old name -> new names):

- `Context_Create_ResultsOption_ParsesPathAndRejectsMissingValue` ->
  `Context_Create_ResultsOption_ParsesPath`, `Context_Create_ResultAlias_ParsesPath`,
  `Context_Create_ResultsOptionWithoutValue_ThrowsArgumentException`
- `Context_Create_WithBadVoicesDir_ThrowsArgumentException` ->
  `Context_Create_WithMissingVoicesDir_ThrowsArgumentException`,
  `Context_Create_VoicesDirWithoutValue_ThrowsArgumentException`
- `Context_CreateMirror_CredentialOverPlainHttp_RejectedUnlessLoopback` ->
  `Context_CreateMirror_BasicCredentialsOverPlainHttp_ThrowsArgumentException`,
  `Context_CreateMirror_BearerTokenOverPlainHttp_ThrowsArgumentException`,
  `Context_CreateMirror_BearerTokenOverPlainHttpLoopback_ReturnsMirror`
- `Context_Create_NoEmotionsOnNonVoicesCommand_IsRecognizedAsGlobalOption` is now accompanied by
  `..._NoEmotionsBeforeCommand_...` and `Context_Create_NoEmotionsAbsent_DefaultsToOff`

Stale references: `docs/reqstream/speech-cli.yaml` (~lines 99, 162, 308, 585) and
`docs/verification/speech-cli.md` (~126, 134, 159, 215).

New Context requirement IDs (in `docs/reqstream/speech-cli/context.yaml`, parent linking is F's):
`SpeechCli-Context-GlobalOptionPlacementVerbose`, `-GlobalOptionPlacementNoEmotions`,
`-MirrorBearerToken`, `-MirrorUrlShapeValidation`, `-HeadingDepthMissingValue`,
`-ResultsOptionMissingValue`. First IDs kept stable and narrowed.

## Recognition

- New requirement `SpeechCli-RecognitionCommands-SessionDefaultClockOperation` (silence-timeout
  yaml): add as child of the parent that lists `SessionDefaultClock`. `SessionDefaultClock` is now
  creation-only.

## docs/reqstream/ots/test-results.yaml (MAJOR-01)

Remove coupling to the Validation unit: wording "that the SpeechCli Validation unit writes"
(~lines 13-14, 24-25) should read "serialize results as TRX / JUnit XML".

## .reviewmark.yaml

RecognitionCommandSubsystem set should include `RecognizeCommandTests.cs` (cross-level test
linking: deferred per plan).
