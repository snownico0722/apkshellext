param(
    [Parameter(Mandatory = $true)][string]$TestAssembly,
    [Parameter(Mandatory = $true)][string]$Workspace,
    [Parameter(Mandatory = $true)][ValidateSet('registration', 'valid', 'invalid', 'stress', 'downloading', 'settings')][string]$Suite
)
$ErrorActionPreference = 'Stop'
try {
    Add-Type -Path ([IO.Path]::GetFullPath($TestAssembly)) -ErrorAction Stop
    exit [ApkShellextIntegration.ShellComSmoke]::Run([string]$Workspace, [string]$Suite)
} catch {
    Write-Error $_
    exit 1
}
