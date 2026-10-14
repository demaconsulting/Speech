### ListVoicesCommand

**Purpose**: Implement `list-voices`: list every available reference voice (the `--voices-dir` folder, the
per-user folder and installed packs) as an aligned table or as JSON.

**Data Model**: A private row record carries the JSON fields `Id`, `DisplayName`, `Source`, `PackId`,
`FilePath`, `LicenseName` and `Moods` (the names of the voice's emotion variants). The fixed texts the command
prints are private constants: `ToolName` (the executable name `speech-cli`, used in the install hint),
`NoVoicesMessage`, `MoodsNote` and `UsageHint`. The pack id in the install hint is not a literal; it comes from
`ReferenceVoicePack.VoiceZero10.Id`.

**Key Methods**:

- **Run(context)**: Creates the catalog through `CliModelCatalogFactory` and delegates to the overload below.
- **Run(context, catalog)**: Parses the optional `--format table|json` flag and prints
  `catalog.EnumerateVoices()`. The table has the columns Id, Source, Moods and Display Name, where Moods is the
  number of emotion variants the voice has, or `-`. Below the table it prints the moods note, the usage hint
  (`speak --tts-model <id> --tts-param voice=<Id>`) and the reference-voice consent note
  (`ReferenceVoiceLibrary.ConsentNotice`: only clone voices you have permission to use). When there are no
  voices it prints the no-voices message and how to obtain some (`speech-cli voices install <packId>`, the
  per-user folder `catalog.UserVoicesDirectory`, or `--voices-dir`), followed by the consent note. JSON output is
  an indented array of the row records, without the notes.

**Error Handling**: An unsupported argument, a `--format` with no value, or a `--format` value other than
`table` or `json` is an `ArgumentException` with parameter name `args`. A `null` context or catalog is an
`ArgumentNullException`.

**Dependencies**: `ICliModelCatalog`, `CliModelCatalogFactory`, `Context`, the library's `ReferenceVoiceInfo`,
`ReferenceVoicePack` and `ReferenceVoiceLibrary`, and `System.Text.Json`.

**Callers**: `CommandDispatch`, through the `Run(Context)` entry point.
