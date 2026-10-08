# ApkShellExt v1.0.0

The first stable release of the maintained [ApkShellExt2 fork](https://github.com/snownico0722/apkshellext), preserving the original shell-extension features while updating the dependencies and covering installation/runtime behavior with Windows CI.

## What's included

- Windows File Explorer application icons and package details for **APK, XAPK, APKS, APKM, IPA, APPX and APPXBUNDLE**.
- APK thumbnail generation, information tips and the existing right-click commands, including rename, app-store links, settings and Android manifest export.
- **Simplified Chinese and English built into `ApkShellext2.dll`**: on a Simplified Chinese Windows display language the interface defaults to Chinese; no separate zh-CN DLL or language download is needed. Other translations remain optional separate language packs. Explicit language choices remain available in Preferences.
- Updated dependencies, including **SharpShell 2.7.2, SharpZipLib 1.4.2, Svg 3.4.8, QRCoder 1.8.0 and libwebp 1.6.0**.
- Bug fixes for broken and partially downloaded packages; binary Android XML booleans and zero-length resource chunks; nested VectorDrawable SVG shapes; compressed APPXBUNDLE entries and optional desktop APPX metadata; IPA image lifetime; settings defaults, rename patterns, thumbnail overlay consistency and version checks.

## Install / upgrade

1. Download **`ApkShellext2.7z`** (or the ZIP variant if provided), extract to a **permanent folder**.
2. If an older ApkShellExt2 is installed, run its `uninstall.bat` first, then restart Explorer as needed.
3. Run the new `install.bat` as administrator from the extracted folder. It registers **both 64-bit and 32-bit** COM Shell handlers when the .NET Framework tools are available.
4. Leave the extracted program files in place. To remove the extension, run `uninstall.bat` before deleting the folder.

Requires **Windows 10/11** with **.NET Framework 4.8**. The main archive already contains the native WebP DLLs and managed runtime dependencies; end users do not need a NuGet download or network connection during normal installation.

## Languages

Simplified Chinese is **included in the main archive's DLL**. Optional language archives for Arabic, German, Greek, Spanish, Persian, French, Hebrew, Italian, Japanese, Korean, Portuguese, Russian, Turkish and Traditional Chinese can be extracted alongside the main program if desired.

## Verification

Windows CI compiles and packages this release and verifies the real in-process COM handlers on **Windows Server 2022 and 2025** in both **x86 and x64**. Coverage includes COM registration/unregistration, icons, thumbnails, information tips, context menus, malformed and downloading APKs, GDI resources, file unlock/rename/delete, Chinese/English language switching and extracted release dependencies.

Interactive Windows 10/11 Explorer/Chrome GUI behavior is not a part of the headless GitHub Actions tests.

This is a fork with separate maintenance and release history; the original project and contributors retain their attribution.
