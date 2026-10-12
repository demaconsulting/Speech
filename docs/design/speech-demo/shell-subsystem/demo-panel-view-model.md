### DemoPanelViewModel

**Purpose**: Pair a display title with the panel view model shown when that navigation entry is
selected.

**Data Model**:

| Member | Type | Purpose |
| --- | --- | --- |
| `Title` | `string` | The caption shown for the entry in the navigation shell |
| `Content` | `ObservableObject` | The panel view model rendered when the entry is selected |

**Key Methods**: The constructor is the only behavioral member; it stores the title and content
unchanged. Keeping the entry as a distinct type - rather than navigating over the panel view
models directly - is what lets the shell present a stable, titled navigation list without every
panel view model having to know it is being navigated to, and lets a new panel be added by
appending an entry with no change to the shell's own logic.

**Error Handling**: The constructor throws `ArgumentNullException` for a null title and
`ArgumentException` for an empty or whitespace title, and throws `ArgumentNullException` for a
null content, because a navigation entry with no title or no content is unusable rather than
merely degraded.

**Dependencies**: The Community Toolkit `ObservableObject` base type, as the common type of every
panel view model.

**Callers**: `MainWindowViewModel`, which builds one entry per injected panel view model.
