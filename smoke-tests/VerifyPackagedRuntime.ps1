param(
    [Parameter(Mandatory = $true)][string]$ArchivePath
)

$ErrorActionPreference = 'Stop'
$ArchivePath = [IO.Path]::GetFullPath($ArchivePath)
if (-not (Test-Path -LiteralPath $ArchivePath)) {
    throw "Release archive does not exist: $ArchivePath"
}

$workDir = Join-Path ([IO.Path]::GetTempPath()) ('ApkShellextPackageSmoke_' + [Guid]::NewGuid().ToString('N'))
$installDir = Join-Path $workDir 'installed'
$fixtureDir = Join-Path $workDir 'fixture'
New-Item -ItemType Directory -Path $installDir, $fixtureDir -Force | Out-Null
try {
    if ($ArchivePath.EndsWith('.7z', [StringComparison]::OrdinalIgnoreCase)) {
        $command = Get-Command '7z.exe' -ErrorAction SilentlyContinue
        $sevenZip = if ($command) { $command.Source } else { Join-Path $env:ProgramW6432 '7-Zip\7z.exe' }
        if (-not (Test-Path -LiteralPath $sevenZip)) { throw '7-Zip is unavailable.' }
        & $sevenZip x '-y' "-o$installDir" $ArchivePath | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Cannot extract the release archive.' }
    } else {
        Expand-Archive -LiteralPath $ArchivePath -DestinationPath $installDir -Force
    }

    $mainDll = Join-Path $installDir 'ApkShellext2.dll'
    if (-not (Test-Path -LiteralPath $mainDll)) { throw 'Missing shell COM assembly in the extracted release.' }
    foreach ($name in @('Svg.dll', 'QRCoder.dll', 'libwebp_x64.dll', 'libwebp_x86.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $installDir $name))) {
            throw "Missing release dependency: $name"
        }
    }
    if (@(Get-ChildItem -LiteralPath $installDir -Filter '*SharpZipLib*.dll' -File).Count -ne 1) {
        throw 'Missing or duplicate SharpZipLib assembly in release.'
    }

    # Exercise loading from an isolated directory, not the build output.
    $assembly = [Reflection.Assembly]::LoadFrom($mainDll)
    [IO.File]::WriteAllText((Join-Path $fixtureDir 'androidmanifest.xml'), 'fixture')
    [IO.File]::WriteAllText((Join-Path $fixtureDir 'resources.arsc'), 'fixture')
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $apkPath = Join-Path $workDir 'fixture.apk'
    [IO.Compression.ZipFile]::CreateFromDirectory($fixtureDir, $apkPath)

    $apkType = $assembly.GetType('ApkQuickReader.ApkReader', $true)
    $constructor = $apkType.GetConstructor([Type[]]@([string], [string]))
    if ($null -eq $constructor) { throw 'The public ApkReader(string, string) constructor was not found.' }
    $reader = $constructor.Invoke([object[]]@($apkPath, [string]::Empty))
    try {
        if ($null -eq $reader.Type) { throw 'APK reader did not initialize.' }
    } finally {
        if ($null -ne $reader) { $reader.Dispose() }
    }

    # Android VectorDrawable rendering uses the new Svg NuGet assembly.
    $vectorType = $assembly.GetType('ApkShellext2.VectorDrawableRender', $true)
    $path = $vectorType.GetMethod('Convert2Path').Invoke($null, [object[]]@('M0,0 L8,8 L8,0 Z'))
    try {
        if ($null -eq $path -or $path.PointCount -lt 3) {
            throw 'VectorDrawable parsing is unavailable from the extracted release.'
        }
    } finally {
        if ($null -ne $path) { $path.Dispose() }
    }

    # Decode a real 2x2 lossless WebP using both 32-bit and 64-bit runtimes.
    $webPType = $assembly.GetType('WebPWrapper.WebP', $true)
    $webp = [Activator]::CreateInstance($webPType)
    try {
        $version = $webPType.GetMethod('GetVersion').Invoke($webp, $null)
        if ($version -ne '1.6.0') { throw "Unexpected packaged libwebp version: $version" }
        [byte[]]$webpBytes = [Convert]::FromBase64String('UklGRh4AAABXRUJQVlA4TBEAAAAvAUAAAAfQyd40uf+BiOh/AAA=')
        $method = $webPType.GetMethod('Decode', [Type[]]@([byte[]]))
        if ($null -eq $method) { throw 'WebP decoder method is unavailable.' }
        $args = New-Object 'object[]' 1
        $args[0] = $webpBytes
        $bitmap = $method.Invoke($webp, $args)
        try {
            if ($null -eq $bitmap -or $bitmap.Width -ne 2 -or $bitmap.Height -ne 2) {
                throw 'Packaged WebP decoder returned an invalid bitmap.'
            }
        } finally {
            if ($null -ne $bitmap) { $bitmap.Dispose() }
        }
    } finally {
        if ($null -ne $webp) { $webp.Dispose() }
    }

    Write-Host "PASS: extracted APK/ZIP, SVG and WebP runtime ($([IntPtr]::Size * 8)-bit)"
} finally {
    # Loaded framework assemblies may remain locked until process exit.
    Remove-Item -LiteralPath $workDir -Force -Recurse -ErrorAction SilentlyContinue
}
