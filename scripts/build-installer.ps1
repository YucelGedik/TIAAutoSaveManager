param([switch]$SkipAppBuild, [switch]$UpdateOnly)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $projectRoot
try {
    [xml]$properties = Get-Content -LiteralPath 'Directory.Build.props'
    $version = [string]$properties.Project.PropertyGroup.Version
    $kind = if ($UpdateOnly) { 'Update' } else { 'Setup' }
    $payload = Join-Path $projectRoot "artifacts/TIAAutoSaveManager-v$version-setup-payload"
    $output = Join-Path $projectRoot 'artifacts/setup'
    $cache = Join-Path $projectRoot 'artifacts/installer-cache'
    $toolchain = Join-Path $projectRoot 'artifacts/installer-toolchain/inno-6.7.3'
    $dependencies = Get-Content -LiteralPath 'installer/dependencies.json' -Raw | ConvertFrom-Json
    New-Item -ItemType Directory -Path $output, $cache -Force | Out-Null

    # Trust only the pinned official binaries, even when using the download cache.
    foreach ($file in $dependencies.files) {
        $path = Join-Path $cache $file.name
        if (-not (Test-Path -LiteralPath $path)) { Invoke-WebRequest -Uri $file.url -OutFile $path }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.sha256) { throw "SHA256 mismatch: $($file.name)" }
        if ($file.sha512 -and (Get-FileHash -LiteralPath $path -Algorithm SHA512).Hash -ne $file.sha512) { throw "SHA512 mismatch: $($file.name)" }
        $signature = Get-AuthenticodeSignature -LiteralPath $path
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notlike "*O=$($file.publisher),*") { throw "Publisher signature invalid: $($file.name)" }
    }

    $compiler = Join-Path $toolchain 'ISCC.exe'
    if (-not (Test-Path -LiteralPath $compiler)) {
        # Official portable mode: workspace-only compiler, no machine installation.
        $compilerSetup = Join-Path $cache 'innosetup-6.7.3.exe'
        $compilerLog = Join-Path $output 'compiler-portable.log'
        $arguments = "/PORTABLE=1 /CURRENTUSER /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR=`"$toolchain`" /LOG=`"$compilerLog`""
        $process = Start-Process -FilePath $compilerSetup -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
        if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $compiler)) { throw 'Portable compiler preparation failed.' }
    }
    if (-not $SkipAppBuild) {
        & "$PSScriptRoot/build.ps1" -SkipRestore -Publish -PublishDirectory "artifacts/TIAAutoSaveManager-v$version-setup-payload"
    }
    foreach ($required in @('TIAAutoSaveManager.App.exe','host/TIAAutoSaveManager.OpennessHost.exe','KULLANIM.md','THIRD-PARTY-NOTICES.md')) {
        if (-not (Test-Path -LiteralPath (Join-Path $payload $required))) { throw "Missing payload: $required" }
    }
    if (-not ([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $payload 'TIAAutoSaveManager.App.dll')).ProductVersion.StartsWith($version))) { throw 'Payload version mismatch.' }
    if (Get-ChildItem -LiteralPath $payload -Recurse -Filter 'Siemens.Engineering*.dll') { throw 'Siemens DLL must not be distributed.' }
    $desktop = $dependencies.files | Where-Object { $_.name -like 'windowsdesktop*' }
    $framework = $dependencies.files | Where-Object { $_.name -like 'ndp48*' }
    $defines = @("/DAppVersion=$version", "/DPackageKind=$kind", "/DIncludePrerequisites=$(if ($UpdateOnly) { 0 } else { 1 })", "/DPayloadDirectory=$payload", "/DOutputDirectory=$output", "/DCacheDirectory=$cache",
        "/DDesktopVersion=$($dependencies.desktopVersion)", "/DDesktopMinPatch=$($dependencies.desktopMinPatch)",
        "/DDesktopSHA256=$($desktop.sha256)", "/DFrameworkSHA256=$($framework.sha256)")
    & $compiler @defines 'installer/PrerequisiteTests.iss'
    if ($LASTEXITCODE -ne 0) { throw 'Installer rule tests did not compile.' }
    $testExe = Join-Path $output 'InstallerRuleTests.exe'
    $report = Join-Path $output 'installer-rule-tests.txt'
    if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
    $closeTestExe = Join-Path $output 'InstallerCloseTestTarget.exe'
    $csharpCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    $closeTestSource = Join-Path $projectRoot 'installer\CloseTestTarget.cs'
    & $csharpCompiler /nologo /target:winexe /reference:System.Windows.Forms.dll "/out:$closeTestExe" $closeTestSource
    if ($LASTEXITCODE -ne 0) { throw 'Close test target did not compile.' }
    foreach ($mode in @('unowned','owned')) {
        $readyFile = Join-Path $output "close-test-$mode-ready.txt"
        $closedFile = Join-Path $output "close-test-$mode-closed.txt"
        foreach ($marker in @($readyFile,$closedFile,$report)) { if (Test-Path -LiteralPath $marker) { Remove-Item -LiteralPath $marker } }
        $closeTarget = Start-Process -FilePath $closeTestExe -ArgumentList "`"$readyFile`" `"$closedFile`" $mode" -WindowStyle Hidden -PassThru
        for ($attempt = 0; $attempt -lt 50 -and -not (Test-Path -LiteralPath $readyFile); $attempt++) { Start-Sleep -Milliseconds 100 }
        if (-not (Test-Path -LiteralPath $readyFile)) { throw 'Invisible test target did not become ready.' }
        # No production installer is executed. Test only this child process, never real Manager/TIA processes.
        $testProcess = Start-Process -FilePath $testExe -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /Report=`"$report`" /CloseTargetPid=$($closeTarget.Id) /CloseMarker=`"$closedFile`"" -WindowStyle Hidden -Wait -PassThru
        if (-not (Test-Path -LiteralPath $report) -or (Get-Content -LiteralPath $report -Raw) -ne 'PASSED: 31 checks') { throw "Installer rule tests failed ($mode); inspect $report" }
        if (-not $closeTarget.WaitForExit(5000)) { throw 'Close target did not exit normally.' }
    }
    & $compiler @defines 'installer/TIAAutoSaveManager.iss'
    if ($LASTEXITCODE -ne 0) { throw 'Setup compilation failed.' }
    $setup = Join-Path $output "TIAAutoSaveManager-v$version-$kind-win-x64.exe"
    if ($UpdateOnly -and (Get-Item -LiteralPath $setup).Length -gt 10MB) { throw 'Small update unexpectedly includes large prerequisite payloads.' }
    [pscustomobject]@{
        Version = $version
        Setup = $setup
        Bytes = (Get-Item -LiteralPath $setup).Length
        SHA256 = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash
        PrerequisiteTests = 31
        CloseTestModes = @('unowned main window', 'owned duplicate-instance dialog')
        PrerequisitesBundled = -not $UpdateOnly
        Dependencies = $dependencies
        Acceptance = 'Compiled and rule-tested; actual installation on a clean Windows machine is pending.'
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output "v$version-$kind-manifest.json") -Encoding UTF8
    Get-Item -LiteralPath $setup | Select-Object FullName,Length
}
finally { Pop-Location }
