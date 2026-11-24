# build.ps1
#
# PURPOSE:
#   Builds the solution in Release configuration, verifies the NuGet packaging, and runs the unit tests,
#   either all of them (-All, the full local build and test run) or only the test projects a change affects
#   (-Changed / -Project).
#
# USAGE:
#   pwsh ./build.ps1 -Help                     Print the usage text and exit 0
#   pwsh ./build.ps1 -All                      Full local build, pack verification, and all tests
#   pwsh ./build.ps1 -Changed [-Base <ref>]    Only the test projects affected by the changes since <ref>
#                                              (default origin/main, else main) plus uncommitted changes
#   pwsh ./build.ps1 -Project Onnx.Kokoro,Cli  Only the test projects affected by the named projects (full or short
#                                              names: Kokoro finds DemaConsulting.Speech.Onnx.Kokoro, Core is
#                                              DemaConsulting.Speech; -Help lists this repository's names)
#   Add -DryRun to any scope to print the selection without building anything.
#   With no arguments the usage text is printed and the script exits 1 (a scope must be chosen).
#
#   A changed project selects every project that references it, directly or transitively, from the
#   ProjectReference graph, and files linked into another project (any item or file property that
#   points outside the project folder) select that project too. Changes that affect the whole
#   build (solution, project, props/targets, global.json, .editorconfig, nuget.config, tool manifest,
#   this script, scripts/ProjectScripts.psm1) fall back to the full build; repository files tests read
#   at run time select their test projects through $RuntimeReads; other changes the build does not
#   read select nothing. A scoped run skips the NuGet pack verification, which belongs to the full build.
#
#   The test platform is detected from global.json: "test": { "runner": "Microsoft.Testing.Platform" } runs
#   'dotnet test --project <path> --report-trx', anything else runs VSTest 'dotnet test <path> --logger trx';
#   results go to artifacts/tests either way. Do not customize the arguments per repository.
#
# EXTENSION POINTS:
#   Search for "[PROJECT-SPECIFIC]" comments to find the designated locations
#   for adding project-specific build or test operations.
#
# CUSTOMIZATION:
#   This script belongs to the project: extend it as the project grows. Add steps at the
#   [PROJECT-SPECIFIC] points and add optional parameters that focus a run. The reusable scoping
#   logic lives in scripts/ProjectScripts.psm1, which is delivered by the Agents release zip, is
#   shared across repositories, and must never be edited here. Slow or environment-dependent test
#   tiers (GUI automation, integration) may be opt-in switches.
#   With no arguments the script prints the usage text and exits 1; -All must still build and run
#   the standard test suite (agents rely on that), and never remove or weaken a test step to make
#   it pass.

param(
    # Full local build and all tests.
    [switch]$All,

    # Build and test only the projects affected by the changed files.
    [switch]$Changed,

    # Git reference to compare against for -Changed (default: origin/main, else main).
    [string]$Base,

    # Build and test only the projects affected by these projects (full or short names, e.g. Core, Onnx.Kokoro).
    [string[]]$Project,

    # Print the selection and exit without building.
    [switch]$DryRun,

    # Print the usage text and exit 0.
    [switch]$Help
)

Import-Module (Join-Path $PSScriptRoot 'scripts/ProjectScripts.psm1') -Force

# [PROJECT-SPECIFIC] Repository files that tests read at run time (through the repository root, not through a
# project item), mapped to the test project that reads them. Keep in step with the tests. Files that a project
# links in (<None Include="..\..\data\..."> and the like) need no entry: the project graph already sees them.
$RuntimeReads = [ordered]@{
    # The CLI integration tests locate the recording in data/stt-en by walking up to Speech.slnx
    '^data/stt-en/'             = 'DemaConsulting.Speech.Cli.Tests'
    # The Text.En trainer tests read the training data, the committed external-evaluation records, and every
    # file under test/ (the regression overlay's references) from the repository root
    '^data/text-en/'            = 'DemaConsulting.Speech.Text.En.Trainer.Tests'
    '^docs/evaluation/text-en/' = 'DemaConsulting.Speech.Text.En.Trainer.Tests'
    '^test/'                    = 'DemaConsulting.Speech.Text.En.Trainer.Tests'
}

# [PROJECT-SPECIFIC] Short -Project names that no resolution rule can derive (exact name, alias, unique dot-segment
# suffix, Core), as data: alias = project name. Empty: every name this repository uses already resolves.
$ProjectAliases = @{}

# [PROJECT-SPECIFIC] Folders scanned for projects. List every root once here when projects live outside src/, test/,
# and tools/; leave out folders of fixture projects that are test inputs.
$ProjectRoots = @('src', 'test', 'tools')

# Test platform: global.json selects Microsoft.Testing.Platform with "test": { "runner": "Microsoft.Testing.Platform" };
# otherwise VSTest is used. The platform decides the 'dotnet test' arguments (see Invoke-DotnetTest).
$UseTestingPlatform = $false
if (Test-Path global.json) {
    try { $UseTestingPlatform = (Get-Content -Raw global.json | ConvertFrom-Json).test.runner -eq 'Microsoft.Testing.Platform' }
    catch { Write-Host "Warning: global.json could not be read; assuming VSTest." }
}

function Invoke-DotnetTest {
    # Runs the tests of one project, or of the whole solution when no path is given, against the existing build
    param([string]$Path)

    # @(...) keeps a one-element list an array, so the splat passes the path whole and not character by character
    if ($UseTestingPlatform) {
        $target = @(if ($Path) { '--project'; $Path })
        dotnet test @target --no-build --configuration Release --report-trx --results-directory artifacts/tests
    } else {
        $target = @(if ($Path) { $Path })
        dotnet test @target --no-build --configuration Release --logger trx --results-directory artifacts/tests
    }
}

# ==============================================================================
# USAGE AND SCOPE VALIDATION
# ==============================================================================

function Show-Usage {
    Write-ScriptUsage -Script 'build.ps1' -IterateWith '-Changed (or -Project)' `
        -Purpose 'build the solution (Release), verify the NuGet packaging, and run the unit tests.' `
        -Options ([ordered]@{
            '-All'             = 'Full local build, pack verification, and all tests; cannot be combined with another scope.'
            '-Changed'         = 'Only the test projects affected by files changed since the base, plus uncommitted files.'
            '-Base <ref>'      = 'Base reference for -Changed (default origin/main, else main).'
            '-Project <names>' = 'Only the test projects affected by these projects (full or short names, e.g. Core,Onnx.Kokoro).'
            '-DryRun'          = 'Print what would be built, tested, and skipped, then exit 0 without running anything.'
            '-Help'            = 'Print this text and exit 0.'
        }) `
        -ChecksLabel 'Projects and aliases (-Project names; short names such as Core or Kokoro also work)' `
        -Checks @(try { Get-ProjectNameList (Get-ProjectGraph -Root $ProjectRoots) $ProjectAliases } catch { }) `
        -Examples @('-Changed', '-Project Onnx.Kokoro,Cli', '-Changed -DryRun', '-All')
}

if ($Help) { Show-Usage; exit 0 }

$scopeProblems = Test-ScopeArguments -Base $Base -Scopes ([ordered]@{
        All     = $All.IsPresent
        Changed = $Changed.IsPresent
        Project = [bool]$Project
    })
if ($scopeProblems.Count -gt 0) {
    $scopeProblems | ForEach-Object { Write-Host "Error: $_" }
    Write-Host ""
    Show-Usage
    exit 1
}

$buildError = $false

# ==============================================================================
# PACK VERIFICATION (full build)
# Mirrors the CI "Create Library Package" step (pack the already-built Release output) into a
# temporary directory, then asserts the packages are well formed: no NuGet (NU*) warnings, a .nupkg
# for every packable src/ project, and a global-tool package for the CLI that exposes the
# `speech-cli` command. Returns $true when the packaging is well formed.
# ==============================================================================

function Test-NuGetPackaging {
    $ErrorActionPreference = 'Stop'
    $packDir = Join-Path ([System.IO.Path]::GetTempPath()) ("speech-pack-" + [System.Guid]::NewGuid().ToString('N'))
    try {
        $packOutput = dotnet pack --no-build --no-restore --configuration Release `
            --property:PackageVersion=0.0.1-test -o $packDir 2>&1 | ForEach-Object { "$_" }
        $packExit = $LASTEXITCODE
        $packOutput | ForEach-Object { Write-Host $_ }
        if ($packExit -ne 0) { return $false }

        $nugetWarnings = @($packOutput | Where-Object { $_ -match 'warning NU\d+' })
        if ($nugetWarnings.Count -gt 0) {
            Write-Host "Error: Pack verification failed: NuGet warnings were reported."
            return $false
        }

        # Every effectively packable project under src/ must yield a .nupkg. Packability is evaluated by
        # MSBuild with the same PackageVersion used to pack above (not by text search), because it can be
        # conditional (e.g. Text.En is packable only for a prerelease version).
        $packable = @()
        foreach ($project in (Get-ChildItem -Path src -Recurse -Filter *.csproj)) {
            $isPackable = (dotnet msbuild $project.FullName -getProperty:IsPackable `
                --property:Configuration=Release --property:PackageVersion=0.0.1-test | Out-String).Trim()
            if ($LASTEXITCODE -ne 0) {
                Write-Host "Error: Pack verification failed: could not evaluate IsPackable for $($project.Name)."
                return $false
            }
            if ($isPackable -ne 'false') { $packable += $project }
        }
        foreach ($project in $packable) {
            $id = [System.IO.Path]::GetFileNameWithoutExtension($project.Name)
            if (-not (Test-Path (Join-Path $packDir "$id.0.0.1-test.nupkg"))) {
                Write-Host "Error: Pack verification failed: $id.0.0.1-test.nupkg was not produced."
                return $false
            }
        }

        # The CLI package must be a .NET global tool exposing the speech-cli command
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $cliPackage = Join-Path $packDir "DemaConsulting.Speech.Cli.0.0.1-test.nupkg"
        $archive = [System.IO.Compression.ZipFile]::OpenRead($cliPackage)
        try {
            $settings = $archive.Entries | Where-Object { $_.Name -eq 'DotnetToolSettings.xml' } | Select-Object -First 1
            if (-not $settings) {
                Write-Host "Error: Pack verification failed: DemaConsulting.Speech.Cli has no DotnetToolSettings.xml (not a global tool)."
                return $false
            }
            $reader = New-Object System.IO.StreamReader($settings.Open())
            try { $settingsXml = $reader.ReadToEnd() } finally { $reader.Dispose() }
            if ($settingsXml -notmatch 'Name="speech-cli"') {
                Write-Host "Error: Pack verification failed: the CLI tool command name is not 'speech-cli'."
                return $false
            }
        }
        finally { $archive.Dispose() }
        return $true
    }
    catch {
        Write-Host "Error: Pack verification failed: $($_.Exception.Message)"
        return $false
    }
    finally {
        if (Test-Path $packDir) { Remove-Item -Recurse -Force $packDir }
    }
}

# ==============================================================================
# SCOPED BUILD (-Changed / -Project)
# Selects the affected test projects; falls back to the full build when needed.
# ==============================================================================

$scoped = $false
if (-not $All) {
    $graph = Get-ProjectGraph -Root $ProjectRoots
    $selected = [System.Collections.Generic.HashSet[string]]::new()
    $fullReason = $null

    $names = @($Project | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    $known = (Get-ProjectNameList $graph $ProjectAliases) -join ', '
    if ($Project -and $names.Count -eq 0) {
        Write-Host "Error: no project named in '$($Project -join ' ')'. Known projects: $known"
        exit 1
    }
    foreach ($single in $names) {
        try { $resolved = Resolve-ProjectName $graph $single $ProjectAliases }
        catch { Write-Host "Error: $($_.Exception.Message)"; exit 1 }
        if (-not $resolved) {
            Write-Host "Unknown project '$single'. Known projects: $known"
            exit 1
        }
        [void]$selected.Add($resolved)
    }

    if ($Changed) {
        $changedFiles = Get-ChangedFiles -Base $Base
        if ($null -eq $changedFiles) {
            $fullReason = 'the changed files could not be determined'
        } else {
            Write-Host "Changed files: $($changedFiles.Count)"
            $fullReason = Add-ChangedProjects -Graph $graph -Files $changedFiles -Selected $selected -RuntimeReads $RuntimeReads
        }
    }

    if ($fullReason) {
        Write-Host "Full build required: $fullReason."
    } else {
        Expand-ReferencingProjects -Graph $graph -Selected $selected

        # Test projects are built and tested; other leaf projects (tools/) are built only
        $tests = @($graph.Values | Where-Object { $selected.Contains($_.Name) -and $_.Directory -like 'test/*' })
        $tools = @($graph.Values | Where-Object { $selected.Contains($_.Name) -and $_.Directory -like 'tools/*' })
        $skipped = @($graph.Values | Where-Object { $_.Directory -like 'test/*' -and -not $selected.Contains($_.Name) })
        if ($tests.Count -eq 0 -and $tools.Count -eq 0) {
            Write-Host "Nothing to build: no changed file is read by the build."
            exit 0
        }

        Write-Host "Affected test projects (built and tested):"
        $tests | ForEach-Object { Write-Host "  $($_.Name)" }
        if ($tools.Count -gt 0) {
            Write-Host "Affected tool projects (build only):"
            $tools | ForEach-Object { Write-Host "  $($_.Name)" }
        }
        if ($DryRun) {
            Write-Host "Skipped test projects ($($skipped.Count)): $(($skipped | ForEach-Object { $_.Name }) -join ', ')"
            Write-Host "Dry run: nothing was built or tested."
            exit 0
        }

        $scoped = $true
        foreach ($entry in $tests + $tools) {
            Write-Host "Building $($entry.Name)..."
            dotnet build $entry.Path --configuration Release
            if ($LASTEXITCODE -ne 0) { $buildError = $true }
        }

        if (-not $buildError) {
            foreach ($entry in $tests) {
                Write-Host "Running unit tests: $($entry.Name)..."
                Invoke-DotnetTest $entry.Path
                if ($LASTEXITCODE -ne 0) { $buildError = $true }
            }
        } else {
            Write-Host "Skipping tests: the build failed."
        }
    }
}

if ($DryRun -and -not $scoped) { Write-Host "Dry run: would run the full build and all tests."; exit 0 }

# ==============================================================================
# FULL BUILD (-All, or a scoped run that needs everything)
# ==============================================================================

if (-not $scoped) {
    # Each step runs only if the steps before it succeeded, so tests never run against stale binaries
    $failedStep = $null   # the step that failed first, so the skip message names the real cause
    Write-Host "Restoring dependencies..."
    dotnet restore
    if ($LASTEXITCODE -ne 0) { $buildError = $true; $failedStep = 'restore' }

    if (-not $buildError) {
        Write-Host "Building project..."
        dotnet build --no-restore --configuration Release
        if ($LASTEXITCODE -ne 0) { $buildError = $true; $failedStep = 'build' }
    }

    # [PROJECT-SPECIFIC] Pack verification: the packages must be well formed (see Test-NuGetPackaging)
    if (-not $buildError) {
        Write-Host "Verifying NuGet packaging..."
        if (-not (Test-NuGetPackaging)) { $buildError = $true; $failedStep = 'pack' }
    }

    if (-not $buildError) {
        Write-Host "Running unit tests..."
        Invoke-DotnetTest
        if ($LASTEXITCODE -ne 0) { $buildError = $true }
    } elseif ($failedStep -eq 'restore') {
        Write-Host "Skipping build and tests: restore failed."
    } else {
        Write-Host "Skipping tests: the build or pack verification failed."
    }

    # [PROJECT-SPECIFIC] Add additional test or post-build steps here.
    # Guard steps that depend on the build with: if (-not $buildError) { ... }
}

if (-not $buildError) {
    Write-Host "Build and tests completed successfully!"
}

exit ($buildError ? 1 : 0)
