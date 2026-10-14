# build.ps1
#
# PURPOSE:
#   Unified cross-platform build script (replaces build.bat and build.sh).
#   Builds the solution in Release configuration and runs all unit tests.
#
# EXTENSION POINTS:
#   Search for "[PROJECT-SPECIFIC]" comments to find the designated locations
#   for adding project-specific build or test operations.
#
# MODIFICATION POLICY:
#   Only modify this file to add project-specific operations at the designated
#   [PROJECT-SPECIFIC] extension points.

$ErrorActionPreference = 'Stop'

Write-Host "Building project..."
dotnet build --configuration Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# [PROJECT-SPECIFIC] Add additional build steps here.

# Pack verification: mirrors the CI "Create Library Package" step (pack the already-built Release
# output) into a temporary directory, then asserts the packages are well formed: no NuGet (NU*)
# warnings, a .nupkg for every packable src/ project, and a global-tool package for the CLI that
# exposes the `speech-cli` command.
Write-Host "Verifying NuGet packaging..."
$packDir = Join-Path ([System.IO.Path]::GetTempPath()) ("speech-pack-" + [System.Guid]::NewGuid().ToString('N'))
try {
    $packOutput = dotnet pack --no-build --no-restore --configuration Release `
        --property:PackageVersion=0.0.1-test -o $packDir 2>&1 | ForEach-Object { "$_" }
    $packExit = $LASTEXITCODE
    $packOutput | ForEach-Object { Write-Host $_ }
    if ($packExit -ne 0) { exit $packExit }

    $nugetWarnings = @($packOutput | Where-Object { $_ -match 'warning NU\d+' })
    if ($nugetWarnings.Count -gt 0) {
        Write-Error "Pack verification failed: NuGet warnings were reported."
        exit 1
    }

    # Every packable project under src/ (IsPackable not false) must yield a .nupkg
    $packable = Get-ChildItem -Path src -Recurse -Filter *.csproj |
        Where-Object { (Get-Content $_.FullName -Raw) -notmatch '<IsPackable>\s*false\s*</IsPackable>' }
    foreach ($project in $packable) {
        $id = [System.IO.Path]::GetFileNameWithoutExtension($project.Name)
        if (-not (Test-Path (Join-Path $packDir "$id.0.0.1-test.nupkg"))) {
            Write-Error "Pack verification failed: $id.0.0.1-test.nupkg was not produced."
            exit 1
        }
    }

    # The CLI package must be a .NET global tool exposing the speech-cli command
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $cliPackage = Join-Path $packDir "DemaConsulting.Speech.Cli.0.0.1-test.nupkg"
    $archive = [System.IO.Compression.ZipFile]::OpenRead($cliPackage)
    try {
        $settings = $archive.Entries | Where-Object { $_.Name -eq 'DotnetToolSettings.xml' } | Select-Object -First 1
        if (-not $settings) {
            Write-Error "Pack verification failed: DemaConsulting.Speech.Cli has no DotnetToolSettings.xml (not a global tool)."
            exit 1
        }
        $reader = New-Object System.IO.StreamReader($settings.Open())
        try { $settingsXml = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($settingsXml -notmatch 'Name="speech-cli"') {
            Write-Error "Pack verification failed: the CLI tool command name is not 'speech-cli'."
            exit 1
        }
    }
    finally { $archive.Dispose() }
}
finally {
    if (Test-Path $packDir) { Remove-Item -Recurse -Force $packDir }
}

Write-Host "Running unit tests..."
dotnet test --configuration Release --report-trx
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# [PROJECT-SPECIFIC] Add additional test or post-build steps here.

Write-Host "Build and tests completed successfully!"
