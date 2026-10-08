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

    # The main archive must not rely on a separately installed Chinese pack.
    if (Test-Path -LiteralPath (Join-Path $installDir 'zh-CN')) {
        throw 'Simplified Chinese must not be shipped as a satellite language directory.'
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

    # Exercise the assembly from an isolated directory, not the build output.
    [IO.File]::WriteAllText((Join-Path $fixtureDir 'androidmanifest.xml'), 'fixture')
    [IO.File]::WriteAllText((Join-Path $fixtureDir 'resources.arsc'), 'fixture')
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $apkPath = Join-Path $workDir 'fixture.apk'
    [IO.Compression.ZipFile]::CreateFromDirectory($fixtureDir, $apkPath)

    # Run strongly typed reflection from .NET Framework, avoiding PowerShell
    # PSObject wrappers being passed to constructors expecting System.String.
    $probeSource = @'
using System;
using System.Reflection;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Resources;
using System.Globalization;

public static class ApkShellextPackageProbe {
    public static void Verify(string mainDll, string apkFile) {
        Assembly assembly = Assembly.LoadFrom(mainDll);
        const string embedded = "ApkShellext2.Properties.Resources_zh_CN_embedded.resources";
        if (Array.IndexOf(assembly.GetManifestResourceNames(), embedded) < 0)
            throw new Exception("The release assembly does not embed Simplified Chinese.");

        Type uiResources = assembly.GetType("ApkShellext2.Properties.Resources", true);
        PropertyInfo property = uiResources.GetProperty("ResourceManager",
            BindingFlags.Static | BindingFlags.NonPublic);
        ResourceManager labels = (ResourceManager)property.GetValue(null, null);
        if (labels.GetType().Name != "EmbeddedChineseResourceManager")
            throw new Exception("The release DLL uses the old satellite-only resource manager.");
        if (labels.GetString("menuMain", CultureInfo.GetCultureInfo("zh-CN")) !=
            "APK文件助手" ||
            labels.GetString("menuMain", CultureInfo.GetCultureInfo("en-US")) !=
            "APK Shell Extension")
            throw new Exception("The extracted release cannot switch between Chinese and English.");

        Type apkType = assembly.GetType("ApkQuickReader.ApkReader", true);
        using (IDisposable reader = (IDisposable)Activator.CreateInstance(
            apkType, new object[] { apkFile, "" })) {
            if (apkType.GetProperty("Type").GetValue(reader, null) == null)
                throw new Exception("The standalone APK reader failed to initialize.");
        }

        Type vectorType = assembly.GetType("ApkShellext2.VectorDrawableRender", true);
        using (GraphicsPath path = (GraphicsPath)vectorType.GetMethod(
            "Convert2Path").Invoke(null, new object[] { "M0,0 L8,8 L8,0 Z" })) {
            if (path == null || path.PointCount < 3)
                throw new Exception("Standalone SVG path parsing failed.");
        }

        Type webpType = assembly.GetType("WebPWrapper.WebP", true);
        using (IDisposable webp = (IDisposable)Activator.CreateInstance(webpType)) {
            string version = (string)webpType.GetMethod("GetVersion").Invoke(webp, null);
            if (version != "1.6.0")
                throw new Exception("Unexpected standalone libwebp version: " + version);

            // 2x2 lossless WebP image, exercising the decode P/Invoke path.
            byte[] webpBytes = Convert.FromBase64String(
                "UklGRh4AAABXRUJQVlA4TBEAAAAvAUAAAAfQyd40uf+BiOh/AAA=");
            MethodInfo decode = webpType.GetMethod("Decode", new Type[] { typeof(byte[]) });
            if (decode == null)
                throw new Exception("WebP decoder entrypoint is unavailable.");
            using (Bitmap bitmap = (Bitmap)decode.Invoke(webp, new object[] { webpBytes })) {
                if (bitmap == null || bitmap.Width != 2 || bitmap.Height != 2)
                    throw new Exception("Standalone WebP decoder returned invalid pixels.");
            }
        }
    }
}
'@
    Add-Type -TypeDefinition $probeSource -ReferencedAssemblies 'System.Drawing.dll' -ErrorAction Stop
    [ApkShellextPackageProbe]::Verify([string]$mainDll, [string]$apkPath)

    Write-Host "PASS: embedded zh-CN / English UI and APK/ZIP, SVG and WebP runtime ($([IntPtr]::Size * 8)-bit)"
} finally {
    # Loaded framework assemblies may remain locked until process exit.
    Remove-Item -LiteralPath $workDir -Force -Recurse -ErrorAction SilentlyContinue
}
