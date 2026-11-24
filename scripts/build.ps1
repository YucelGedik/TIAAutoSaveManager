param(
    [switch]$SkipRestore,
    [switch]$ReadOnlyV21,
    [switch]$Publish,
    [string]$PublishDirectory = 'artifacts/TIAAutoSaveManager'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $projectRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.build-cli'
    $env:NUGET_PACKAGES = Join-Path $projectRoot '.packages'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'

    if (-not $SkipRestore) {
        dotnet restore TIAAutoSaveManager.sln --disable-parallel -m:1 /nodeReuse:false
        if ($LASTEXITCODE -ne 0) { throw 'NuGet restore failed.' }
    }
    dotnet build TIAAutoSaveManager.sln -c Release --no-restore -m:1 /nodeReuse:false
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet test tests/TIAAutoSaveManager.Tests/TIAAutoSaveManager.Tests.csproj -c Release --no-build --no-restore -m:1 /nodeReuse:false --filter 'TestCategory!=ReadOnlyV21' --logger 'trx;LogFileName=unit-tests.trx' --results-directory artifacts/test-results
    if ($LASTEXITCODE -ne 0) { throw 'Unit tests failed.' }
    dotnet test tests/TIAAutoSaveManager.UiTests/TIAAutoSaveManager.UiTests.csproj -c Release --no-build --no-restore -m:1 /nodeReuse:false --logger 'trx;LogFileName=ui-tests.trx' --results-directory artifacts/test-results
    if ($LASTEXITCODE -ne 0) { throw 'WPF layout/theme tests failed.' }

    if ($ReadOnlyV21) {
        $env:TIAAUTOSAVE_DATA_DIR = Join-Path $projectRoot 'artifacts\read-only-v21-data'
        $env:TIAAUTOSAVE_TEST_HOST = Join-Path $projectRoot 'src\TIAAutoSaveManager.App\bin\Release\net8.0-windows\host\TIAAutoSaveManager.OpennessHost.exe'
        dotnet test tests/TIAAutoSaveManager.Tests/TIAAutoSaveManager.Tests.csproj -c Release --no-build --no-restore -m:1 /nodeReuse:false --filter 'TestCategory=ReadOnlyV21' --logger 'trx;LogFileName=read-only-v21.trx' --results-directory artifacts/test-results
        if ($LASTEXITCODE -ne 0) { throw 'Real V21 read-only discovery failed.' }
        Remove-Item Env:TIAAUTOSAVE_TEST_HOST
        Remove-Item Env:TIAAUTOSAVE_DATA_DIR
    }

    if ($Publish) {
        $publishPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $PublishDirectory))
        if (-not $publishPath.StartsWith($projectRoot.TrimEnd('\') + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Publish directory must stay inside this workspace.' }
        dotnet publish src/TIAAutoSaveManager.App/TIAAutoSaveManager.App.csproj -c Release --no-restore --self-contained false -m:1 /nodeReuse:false -o $publishPath
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
        Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\KULLANIM.md') -Destination (Join-Path $publishPath 'KULLANIM.md')
        Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\THIRD-PARTY-NOTICES.md') -Destination (Join-Path $publishPath 'THIRD-PARTY-NOTICES.md')
        $licensePath = Join-Path $publishPath 'licenses'
        New-Item -ItemType Directory -Path $licensePath -Force | Out-Null
        Get-ChildItem -LiteralPath (Join-Path $projectRoot 'docs\licenses') -File | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $licensePath -Force
        }
        $unexpectedSiemens = Get-ChildItem -LiteralPath $publishPath -Recurse -Filter 'Siemens.Engineering*.dll'
        if ($unexpectedSiemens) { throw 'Siemens DLL must not be bundled.' }
    }
}
finally { Pop-Location }
