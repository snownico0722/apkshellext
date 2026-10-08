# ApkShellExt v0.3.0

First release of the `snownico0722/apkshellext` maintenance fork, based on ApkShellExt2.

## Changes from the upstream version

- Reproducible .NET Framework 4.8 / Windows build and portable archives.
- Fixes for APK stream lifetime, icon handle release, resource loading and uppercase embedded PNG paths.
- Existing Android APK support plus base-APK icons/metadata for XAPK, APKS and APKM.
- Fixes for independent store-setting checkboxes.
- Updated **libwebp to 1.6.0** in both the x86 and x64 release binaries; native versions verified in CI.

## Architecture / requirements

- Main Shell extension: **AnyCPU** managed DLL targeting **.NET Framework 4.8**.
- Installation script registers both **64-bit and 32-bit** Shell COM handlers when available.
- Package includes both `libwebp_x64.dll` and `libwebp_x86.dll`.
- Extract the main archive to a permanent folder; run `install.bat` as administrator. Uninstall before moving or removing the folder.
- Separate optional language packs can be extracted alongside the main archive.

## Scope and verification

Windows CI builds the original app and service, runs APK parser smoke tests, and builds/tests both native WebP DLL architectures. COM/Explorer behavior, Chrome and Edge download integration, and Windows 11 settings UI **have not been verified in a real installed Explorer session**.

Open PRs #6 and #7 are **not included** in this release. SharpZipLib and SharpShell remain on the original source-backed integration pending a separate compatibility upgrade.
