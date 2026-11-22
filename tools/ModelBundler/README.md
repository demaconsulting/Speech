# Model Bundler

A maintainer tool (never packed or shipped) that publishes the ONNX models on the project's `models` GitHub release.
It packs each model's unmodified upstream files into a deterministic, SHA-256-pinned bundle zip. Each zip also holds
`LICENSE`, `NOTICE`, `model.json` and `README.txt`. The tool can prove that a build reproduces byte for byte. It
uploads a build without ever replacing an existing asset.

The vendor files, their original URLs and their SHA-256 pins come from the model packages' internal `BundleContents`
members (Kokoro, NemotronStt and ChatterboxTurbo). The tool never declares a hash of its own. `bundles.json` declares
everything else about each bundle:

- the name parts and the catalog model ids that install the bundle (each id must be one safe directory name: lowercase
  letters and digits separated by single `.`, `_` or `-`)
- the upstream repository and the pinned snapshot
- the fixed bundle `date`, used in `README.txt` and for every zip timestamp (never the wall clock)
- the licenses and their texts in `licenses/` (a text path must be relative, without `..` segments, and resolve inside
  the tool's directory, so no other local file can be embedded in a bundle)
- the attribution and the `NOTICE` text

## Commands

Run from the repository root:

```pwsh
# Build bundles from the installed models (no network needed when they are installed)
dotnet run --project tools/ModelBundler -c Release -- build `
  --bundle kokoro-en-v1.0-int8-b1 --bundle nemotron-3.5-asr-streaming-0.6b-20261011-int4-b1 `
  --bundle chatterbox-turbo-lm-20261014-fp16-b1 --bundle chatterbox-turbo-aux-20261014-int4-b1 `
  --source-store "$env:LOCALAPPDATA\DemaConsulting.Speech\Models" --output artifacts/model-bundles

# Prove the build reproduces: rebuild every listed bundle and compare byte for byte
dotnet run --project tools/ModelBundler -c Release -- verify --input artifacts/model-bundles `
  --source-store "$env:LOCALAPPDATA\DemaConsulting.Speech\Models"

# Show what an upload would do, then upload (the release must exist; see below)
dotnet run --project tools/ModelBundler -c Release -- upload --input artifacts/model-bundles --dry-run
dotnet run --project tools/ModelBundler -c Release -- upload --input artifacts/model-bundles
```

`build (--all | --bundle <name>...) --output <dir> [--source-store <root>] [--cache <dir>] [--skip-unavailable]`:

- Finds each vendor file in this order:
  1. with `--source-store`, the installed copy `{root}/{modelId}/current/{path}`, trying each model id of the bundle
     (a path that would resolve outside `{root}` fails the build before any file is read)
  2. the SHA-keyed cache `{cache}/{sha256}` (default `artifacts/model-bundler/cache`)
  3. a download of the file's original URL into the cache
- Hashes every copy and uses it only when it equals the compiled pin. A copy that differs from its pin always fails the
  build; it never falls through to the next source.
- `--skip-unavailable` skips, and reports, a bundle whose files cannot be found or downloaded.
- Writes `{name}.zip`, `SHA256SUMS` and `release-notes.md` (the release body) to the output directory.
- Prints `sha256  size  name` and `MATCH` (the packages already pin this zip) or `UPDATE CATALOG` (they do not, yet).
- Refuses a zip of 2 GiB or more (the GitHub release asset limit).

`verify --input <dir> [--source-store <root>] [--cache <dir>]` rebuilds each bundle listed in `SHA256SUMS` into a
scratch folder. It compares each zip byte for byte, and `release-notes.md` as well. Exit code 1 means a difference.

`upload --input <dir> [--repo <owner/name>] [--dry-run]` (default repository `demaconsulting/Speech`):

1. Checks that every local zip still has its `SHA256SUMS` digest.
2. Lists the release (`gh release view models --json assets`).
3. Skips a zip that is already published with the same digest.
4. Refuses the whole upload, before uploading anything, when a name is published with a different digest.
5. Uploads the rest with `gh release upload`, never with `--clobber`.

The tool never deletes or replaces an asset.

Exit codes: 0 success, 1 failure (a pin mismatch, an unavailable file without `--skip-unavailable`, a difference, a
refused upload), 2 an invalid command line.

## The Bundle

The zip is written by the tool's own stored-zip writer, so the bytes do not depend on the platform or the .NET version:

- no compression, with a CRC-32 per entry
- one fixed timestamp (the bundle date at 00:00:00)
- fixed version fields, zero attributes
- no extra fields, comments, data descriptors or Zip64

The entries are always in this order:

1. `README.txt`
2. `LICENSE`
3. `NOTICE`
4. `model.json`
5. the vendor files, in ordinal path order

Text entries are UTF-8 without a byte order mark and use LF line endings. The runtime (`SpeechModelBundle.InstallAsync`)
checks every vendor file against both `model.json` and the compiled pin.

## Publishing Order

1. Build and verify (above). Paste the printed zip SHA-256s into the packages' bundle pins on the pull request branch.
2. Create the release once, as a pre-release that is never "latest":

   ```pwsh
   gh release create models --repo demaconsulting/Speech --target main --prerelease --latest=false `
     --title "Models" --notes-file artifacts/model-bundles/release-notes.md
   ```

3. Upload with `upload`. Check the published digests with `gh release view models --json assets`.
4. Only then merge the package change. Publish the assets **before** the catalog change merges, so `main` never points
   at a missing asset.

Rules:

- Never use `--clobber`, and never delete an asset that a released package version references.
- Any change to a bundle's bytes (vendor files, license texts, notices or this tool's output) needs a new bundle name.
  Use the next revision `bN`, or a new upstream release token.
- To add bundles later, upload them, then update the release notes with `gh release edit models --notes-file ...`.
