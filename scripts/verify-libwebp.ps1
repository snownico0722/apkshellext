param(
    [Parameter(Mandatory = $true)][string]$DllPath,
    [Parameter(Mandatory = $true)][ValidateSet('x86', 'x64')][string]$Architecture
)

$ErrorActionPreference = 'Stop'
$DllPath = [IO.Path]::GetFullPath($DllPath)
if (-not (Test-Path -LiteralPath $DllPath)) {
    throw "Missing WebP DLL: $DllPath"
}
$expectedPointerSize = if ($Architecture -eq 'x64') { 8 } else { 4 }
if ([IntPtr]::Size -ne $expectedPointerSize) {
    throw "Probe $Architecture from a matching PowerShell process."
}

# Use an absolute P/Invoke library path to avoid loading another copy from
# PATH or from the (possibly older) dependencies bundled with this repository.
$escapedPath = $DllPath.Replace('"', '""')
$source = @"
using System.Runtime.InteropServices;
public static class ApkShellLibWebPVersionProbe {
    [DllImport(@"$escapedPath", CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "WebPGetDecoderVersion")]
    public static extern int GetVersion();
}
"@
Add-Type -TypeDefinition $source -ErrorAction Stop
$actual = [ApkShellLibWebPVersionProbe]::GetVersion()
if ($actual -ne 0x010600) {
    throw "Expected libwebp 1.6.0; found native version 0x$($actual.ToString('X6')) at $DllPath"
}
Write-Host "PASS: libwebp 1.6.0 ($Architecture): $DllPath"
