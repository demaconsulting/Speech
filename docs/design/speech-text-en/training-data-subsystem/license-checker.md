### LicenseChecker

**Purpose**: Keep the owner's licensing decisions true for every row and every file of the data folder: only open
licenses, a license that suits the source, no restricted or Misaki material, no evaluation-only corpus in the folder,
and the attribution that the licenses require. It does not recompute the license of a harvested row from the original
corpus tables, which the repository does not have; the closed per-source policy replaces that.

**Data Model**: `LicenseChecker` holds the data folder, the loaded `TextEnDataSet` and the `CheckReport`; its findings
use the check name `license`. Its fixed rules are:

- **Allow-list**: the license of a row is exactly `CC0-1.0`, `Public-Domain` or `CC-BY-3.0-US` (`LicenseIds`); any other
  value, including any share-alike, non-commercial or no-derivatives license, is a finding.
- **Source policy**: the allowed licenses of a source come from `SourceInfo`. A generated source (`synthetic`,
  `synthetic-v2`, `synthetic-v3`, `independent-holdout`) is `CC0-1.0`; `harvest-masc` and `harvest-masc-llm` are
  `CC-BY-3.0-US`; `harvest-open-train*` and `harvest-silver*` are `CC0-1.0` or `Public-Domain`; any other harvested
  source is `Public-Domain`. A row of a source that is not generated has an entry in the harvest provenance table. A row
  of an unknown source is not judged by the source policy (the schema check reports it).
- **Restricted terms**: the case-insensitive pattern
  `share-?alike|by-sa|by-nc|by-nd|non-?commercial|no-?derivat|wikipedia|misaki|creativecommons\.org/licenses/by-` is
  applied to every line of every `.tsv` file except the manifest, but for the four data files only to the `id`, `source`
  and `license` cells, and for the evaluation overlays `regression.tsv` and `generalization.tsv` to every cell except
  the sentence (column 3), so the sentence is never scanned.
- **Evaluation-only paths**: the case-insensitive pattern
  `wikipedia|whd|heteronym-bench|heteronymbench|llama|ewt|semcor|brown|misaki|\.conllu$` is applied to the relative path
  of every file under the folder, except exactly `whd-mapping.tsv` (which holds identifiers only); the local cache
  folders `external/` and `work/` are not enumerated.
- **Attribution**: when any row carries `CC-BY-3.0-US`, the file `ATTRIBUTION.md` exists and contains `MASC`.
- **License lines**: `README.md`, `prompts/README.md` and `audit/audit-summary.md` each exist and have a line beginning
  `License:` whose identifiers, ignoring `AND` and `OR`, are all in the allow-list.

**Key Methods**:

- **LicenseChecker(root, data, report)**: stores the arguments; a null argument throws `ArgumentNullException`.
- **Run()**: applies the rules above in that order (the rows, then each file's path and terms, then the attribution and
  the license lines) and adds an information line with the number of rows per license.

**Error Handling**: every violation is a finding; the method never throws for the content of the data. An unreadable
file propagates its input and output exception to `CheckDataCommand`.

**Dependencies**: `TextEnDataSet` (with `SourceInfo` and `LicenseIds`), `DataManifest` (to enumerate the files) and
`CheckReport`; regular expressions and file access of the base class library.

**Callers**: `CheckDataCommand` and the unit tests.
