param(
    [string]$BuildDirectory = (Join-Path $PSScriptRoot '..\ApkShellext2\bin\Release'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\dist')
)

$ErrorActionPreference = 'Stop'
$BuildDirectory = [IO.Path]::GetFullPath($BuildDirectory)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path (Join-Path $BuildDirectory 'ApkShellext2.dll'))) {
    throw 'Build Release | Any CPU before packaging.'
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

# Remove only obsolete Simplified-Chinese archive artifacts left by an older
# local packaging run. This locale is now included in the primary DLL.
foreach ($extension in @('.7z', '.zip')) {
    $obsolete = Join-Path $OutputDirectory ("zh-CN" + $extension)
    if (Test-Path -LiteralPath $obsolete)
        Remove-Item -LiteralPath $obsolete -Force
}

# Simplified Chinese belongs to the main assembly, never a zh-CN satellite.
# Catch accidental Visual Studio designer / MSBuild metadata regressions.
$chineseSatellite = Join-Path $BuildDirectory 'zh-CN\ApkShellext2.resources.dll'
if (Test-Path -LiteralPath $chineseSatellite) {
    throw 'Unexpected zh-CN satellite DLL: Simplified Chinese must be inside ApkShellext2.dll.'
}

$required = @('ApkShellext2.dll', 'install.bat', 'uninstall.bat', 'restart_explorer.bat')
foreach ($name in $required) {
    if (-not (Test-Path (Join-Path $BuildDirectory $name))) {
        throw "Missing release component: $name"
    }
}
# The original build copies legacy libwebp binaries. Refuse to package them
# until the pinned 1.6.0 DLLs have been built for both architectures.
& (Join-Path $PSScriptRoot 'verify-libwebp.ps1') -DllPath (Join-Path $BuildDirectory 'libwebp_x64.dll') -Architecture x64
$winPs32 = Join-Path $env:WINDIR 'SysWOW64\WindowsPowerShell\v1.0\powershell.exe'
if (-not (Test-Path $winPs32)) {
    throw '32-bit Windows PowerShell is needed to validate the x86 libwebp DLL.'
}
& $winPs32 -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'verify-libwebp.ps1') -DllPath (Join-Path $BuildDirectory 'libwebp_x86.dll') -Architecture x86
if ($LASTEXITCODE -ne 0) { throw 'The x86 libwebp DLL failed validation.' }

# The extension now references managed NuGet assemblies as well as native WebP.
# Ship all built DLL dependencies next to the COM entry assembly.
$payload = @($required)
$payload += @(Get-ChildItem -Path $BuildDirectory -Filter '*.dll' -File |
    Where-Object { $_.Name -notin $required } | ForEach-Object Name)
$payload = @($payload | Select-Object -Unique)

$command = Get-Command '7z.exe' -ErrorAction SilentlyContinue
$sevenZip = if ($command) { $command.Source } else { Join-Path $env:ProgramFiles '7-Zip\7z.exe' }
$useSevenZip = Test-Path $sevenZip

function Write-Package([string]$name, [string[]]$relativePaths) {
    $extension = if ($useSevenZip) { '.7z' } else { '.zip' }
    $destination = Join-Path $OutputDirectory ($name + $extension)
    if (Test-Path $destination) { Remove-Item -LiteralPath $destination -Force }
    if ($useSevenZip) {
        Push-Location $BuildDirectory
        try {
            & $sevenZip a '-t7z' '-y' $destination @relativePaths | Out-Host
            if ($LASTEXITCODE -ne 0) { throw "7-Zip packaging failed: $name" }
        } finally { Pop-Location }
    } else {
        $absolutePaths = @($relativePaths | ForEach-Object { Join-Path $BuildDirectory $_ })
        Compress-Archive -LiteralPath $absolutePaths -DestinationPath $destination
    }
    Write-Host "Created $destination"
}

Write-Package -name 'ApkShellext2' -relativePaths $payload

# Keep all other translations as separate optional language packs.
foreach ($culture in @(Get-ChildItem -Path $BuildDirectory -Directory |
        Where-Object { $_.Name -match '^[a-z]{2}-[A-Z]{2}$' -and
            $_.Name -ne 'zh-CN' })) {
    if (Test-Path (Join-Path $culture.FullName 'ApkShellext2.resources.dll')) {
        Write-Package -name $culture.Name -relativePaths @($culture.Name)
    }
}
