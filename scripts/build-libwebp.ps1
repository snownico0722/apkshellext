param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\ApkShellext2\bin\Release')
)

$ErrorActionPreference = 'Stop'

# Build reproducible Windows x86/x64 libwebp DLLs for the existing WebPWrapper.
# The original submodule embeds older DLLs; release packaging must replace them.
$version = '1.6.0'
$sourceCommit = '4fa21912338357f89e4fd51cf2368325b59e9bd9'
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$tempRoot = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [IO.Path]::GetTempPath() }
$work = Join-Path $tempRoot ('apkshellext-libwebp-' + [Guid]::NewGuid().ToString('N'))
$source = Join-Path $work 'src'
New-Item -ItemType Directory -Path $work -Force | Out-Null

try {
    & git clone --quiet --depth 1 --branch "v$version" 'https://github.com/webmproject/libwebp.git' $source
    if ($LASTEXITCODE -ne 0) { throw 'Cannot fetch the pinned libwebp source.' }
    $actualCommit = (& git -C $source rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $actualCommit -ne $sourceCommit) {
        throw "libwebp source tag has changed: $actualCommit"
    }

    # Upstream shared webp normally depends on a separate sharpyuv DLL. Link
    # sharpyuv statically so each architecture stays a single, self-contained
    # native DLL with the filenames WebPWrapper already imports.
    $cmakePath = Join-Path $source 'CMakeLists.txt'
    $cmakeText = [IO.File]::ReadAllText($cmakePath)
    $original = 'add_library(sharpyuv ${WEBP_SHARPYUV_SRCS})'
    $patched = 'add_library(sharpyuv STATIC ${WEBP_SHARPYUV_SRCS})'
    if (-not $cmakeText.Contains($original)) {
        throw 'The pinned libwebp CMake target no longer matches the expected source.'
    }
    [IO.File]::WriteAllText($cmakePath, $cmakeText.Replace($original, $patched),
        [Text.UTF8Encoding]::new($false))

    foreach ($target in @(
        @{ Platform = 'x64'; Suffix = 'x64'; Machine = 0x8664 },
        @{ Platform = 'Win32'; Suffix = 'x86'; Machine = 0x014c }
    )) {
        $build = Join-Path $work ('build-' + $target.Suffix)
        $cmakeOptions = @(
            '-G', 'Visual Studio 17 2022', '-A', $target.Platform,
            '-DBUILD_SHARED_LIBS=ON',
            '-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded',
            '-DWEBP_BUILD_CWEBP=OFF',
            '-DWEBP_BUILD_DWEBP=OFF',
            '-DWEBP_BUILD_GIF2WEBP=OFF',
            '-DWEBP_BUILD_IMG2WEBP=OFF',
            '-DWEBP_BUILD_VWEBP=OFF',
            '-DWEBP_BUILD_WEBPINFO=OFF',
            '-DWEBP_BUILD_LIBWEBPMUX=OFF',
            '-DWEBP_BUILD_WEBPMUX=OFF',
            '-DWEBP_BUILD_ANIM_UTILS=OFF',
            '-DWEBP_BUILD_EXTRAS=OFF'
        )
        & cmake -S $source -B $build @cmakeOptions
        if ($LASTEXITCODE -ne 0) { throw "CMake configure failed for $($target.Platform)." }
        & cmake --build $build --config Release --target webp --parallel 4
        if ($LASTEXITCODE -ne 0) { throw "libwebp build failed for $($target.Platform)." }

        $outputDll = Get-ChildItem -Path $build -Recurse -File -Filter 'libwebp.dll'
        if (@($outputDll).Count -ne 1) {
            throw "Expected one libwebp.dll for $($target.Platform), found $(@($outputDll).Count)."
        }
        $destination = Join-Path $OutputDirectory ("libwebp_$($target.Suffix).dll")
        Copy-Item -LiteralPath $outputDll[0].FullName -Destination $destination -Force

        # Verify the PE architecture, not just the filename.
        $bytes = [IO.File]::ReadAllBytes($destination)
        $peOffset = [BitConverter]::ToInt32($bytes, 0x3c)
        $machine = [BitConverter]::ToUInt16($bytes, $peOffset + 4)
        if ($machine -ne $target.Machine) {
            throw "Incorrect PE architecture in $destination ($machine)."
        }
        Write-Host "Built libwebp $version for $($target.Suffix): $destination"
    }
} finally {
    if (Test-Path -LiteralPath $work) {
        Remove-Item -LiteralPath $work -Force -Recurse
    }
}
