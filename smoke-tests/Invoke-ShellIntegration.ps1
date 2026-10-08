param(
    [Parameter(Mandatory = $true)][string]$ArchivePath,
    [Parameter(Mandatory = $true)][string]$TestAssembly,
    [int]$TimeoutSeconds = 40
)

$ErrorActionPreference = 'Stop'
$ArchivePath = [IO.Path]::GetFullPath($ArchivePath)
$TestAssembly = [IO.Path]::GetFullPath($TestAssembly)
$workRoot = Join-Path ([IO.Path]::GetTempPath()) ('ApkShellext_COM_' + [Guid]::NewGuid().ToString('N'))
$installed = Join-Path $workRoot 'installed'
New-Item -ItemType Directory -Path $installed -Force | Out-Null
$didAttemptInstallation = $false
$classes = @(
    '1F869CEE-4FDA-35D9-896F-43975A87D1F6',
    'd747c5a7-2f66-4b7d-8301-8531838e4ed3',
    '946435a5-fe96-416d-99db-e94ee9fb46c8',
    'dcb629fc-f86f-456f-8e24-98b9b2643a9b'
)

function Invoke-Installer([string]$batFile) {
    if (-not (Test-Path $batFile)) { throw "Missing installer: $batFile" }
    # The existing BAT ends in PAUSE. Redirect stdin so CI can exercise the
    # original script rather than reimplementing its RegAsm commands.
    $line = 'call "' + $batFile + '" <NUL'
    & $env:ComSpec /D /S /C $line
    if ($LASTEXITCODE -ne 0) {
        throw "Installation command exited with code $LASTEXITCODE : $batFile"
    }
}

function Assert-ComRegistration([bool]$expected) {
    foreach ($view in @([Microsoft.Win32.RegistryView]::Registry64,
                        [Microsoft.Win32.RegistryView]::Registry32)) {
        $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
            [Microsoft.Win32.RegistryHive]::ClassesRoot, $view)
        try {
            foreach ($clsid in $classes) {
                $subkey = $root.OpenSubKey("CLSID\{$clsid}\InprocServer32")
                try {
                    if ($expected) {
                        if ($null -eq $subkey) {
                            throw "Missing $view COM class registration: $clsid"
                        }
                        $codebase = [string]$subkey.GetValue('CodeBase', '')
                        if ($codebase -notlike '*ApkShellext2.dll') {
                            throw "Wrong $view COM CodeBase for $clsid : $codebase"
                        }
                        if ([string]::IsNullOrEmpty([string]$subkey.GetValue('Class',''))) {
                            throw "Missing $view COM managed Class metadata: $clsid"
                        }
                    } elseif ($null -ne $subkey) {
                        throw "COM class remained after uninstall in $view : $clsid"
                    }
                } finally {
                    if ($null -ne $subkey) { $subkey.Dispose() }
                }
            }
        } finally { $root.Dispose() }
    }
    Write-Host "PASS: COM registry $(if($expected){'installed'}else{'uninstalled'}) in both architectures"
}

function Invoke-Case([string]$architecture, [string]$suite) {
    $powershell = if ($architecture -eq 'x64') {
        Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    } else {
        Join-Path $env:WINDIR 'SysWOW64\WindowsPowerShell\v1.0\powershell.exe'
    }
    if (-not (Test-Path $powershell)) { throw "Missing host: $powershell" }
    $runner = Join-Path $PSScriptRoot 'Run-ShellCase.ps1'
    $caseDirectory = Join-Path $workRoot ("case-" + $architecture + "-" + $suite)
    New-Item -ItemType Directory -Path $caseDirectory -Force | Out-Null
    $standardOutput = Join-Path $workRoot ($architecture + '-' + $suite + '.stdout.txt')
    $standardError = Join-Path $workRoot ($architecture + '-' + $suite + '.stderr.txt')
    $arguments = @(
        '-NoProfile', '-NonInteractive', '-STA', '-ExecutionPolicy', 'Bypass',
        '-File', ('"' + $runner + '"'),
        '-TestAssembly', ('"' + $TestAssembly + '"'),
        '-Workspace', ('"' + $caseDirectory + '"'),
        '-Suite', $suite
    )
    $process = Start-Process -FilePath $powershell -ArgumentList $arguments -PassThru `
        -RedirectStandardOutput $standardOutput -RedirectStandardError $standardError
    try {
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill()
            throw "Windows Shell $architecture/$suite exceeded $TimeoutSeconds seconds."
        }
        $out = Get-Content -LiteralPath $standardOutput -Raw -ErrorAction SilentlyContinue
        $err = Get-Content -LiteralPath $standardError -Raw -ErrorAction SilentlyContinue
        if ($out) { Write-Host $out }
        if ($err) { Write-Host $err }
        if ($process.ExitCode -ne 0) {
            throw "Windows Shell $architecture/$suite failed with exit code $($process.ExitCode)."
        }
    } finally { $process.Dispose() }
}

try {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'COM registration test requires the elevated ephemeral Windows runner.'
    }

    if ($ArchivePath.EndsWith('.7z', [StringComparison]::OrdinalIgnoreCase)) {
        $command = Get-Command '7z.exe' -ErrorAction SilentlyContinue
        $sevenZip = if ($command) { $command.Source } else { Join-Path $env:ProgramFiles '7-Zip\7z.exe' }
        if (-not (Test-Path $sevenZip)) { throw '7-Zip not available.' }
        & $sevenZip x '-y' "-o$installed" $ArchivePath | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Cannot extract release.' }
    } else {
        Expand-Archive -LiteralPath $ArchivePath -DestinationPath $installed -Force
    }
    if (-not (Test-Path $TestAssembly)) { throw "Missing compiled Shell integration tests: $TestAssembly" }

    $didAttemptInstallation = $true
    Invoke-Installer (Join-Path $installed 'install.bat')
    Assert-ComRegistration $true

    foreach ($architecture in @('x64', 'x86')) {
        foreach ($suite in @('registration', 'valid', 'invalid', 'stress', 'downloading', 'settings')) {
            Invoke-Case $architecture $suite
        }
    }
    Write-Host 'PASS: installed COM Shell integration across x64 and x86'
} finally {
    try {
        if ($didAttemptInstallation) {
            Invoke-Installer (Join-Path $installed 'uninstall.bat')
            Assert-ComRegistration $false
        }
    } finally {
        # The GitHub runner is an ephemeral VM. Best effort cleanup; the
        # 32-bit shell host can keep DLLs mapped until process shutdown.
        Remove-Item -LiteralPath $workRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
