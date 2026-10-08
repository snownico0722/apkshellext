# Building and contributing

This fork follows the upstream **ApkShellext2** branch. Work is incremental: **keep all existing features** (APK/IPA/APPX icon handlers, thumbnails, info tips, context menu, batch rename, stores, translations, settings, and version check). Changes must not silently remove or change them.

## Windows development environment

- Windows 10/11 and Visual Studio 2022 with the **.NET desktop development** workload and .NET Framework 4.8 targeting pack.
- Clone with submodules: `git clone --recurse-submodules https://github.com/snownico0722/apkshellext.git`. For an existing clone run `git submodule update --init --recursive`.
- Open `ApkShellext2.sln` and build **Release | Any CPU**, or execute `msbuild ApkShellext2.sln /restore /m /p:Configuration=Release` from a Visual Studio Developer Command Prompt.
- The normal build copies the existing installer/uninstaller scripts and native WebP DLLs to `ApkShellext2/bin/Release/`. It does **not** install a Shell extension, restart Explorer, or require a locally installed 7-Zip. Use `pwsh -File scripts/package-release.ps1` to package a Release build (7z when available, otherwise built-in ZIP), including optional language packs.
- Before packaging, run `pwsh -File scripts/build-libwebp.ps1` to replace the old WebP submodule DLLs with pinned upstream libwebp **1.6.0** (both x86 and x64). Packaging now checks both DLL versions and rejects the old binaries. The DLLs are built from pinned official source and use a statically linked `sharpyuv` to avoid extra native runtime dependencies.
- The service project remains in the repository; it can be built explicitly with `msbuild ApkShellextService/apkShellextService.csproj /p:Configuration=Release`. Normal builds do not install/start the service.

SharpZipLib 1.4.2 is restored as a managed NuGet dependency; release archives include the resolved runtime DLLs alongside the Shell assembly. Use `/restore` on Windows builds.

## Built-in Simplified Chinese

The `Properties/Resources.zh-CN.resx` translation is compiled as a **neutral embedded resource inside `ApkShellext2.dll`**, with a dedicated `EmbeddedChineseResourceManager`. It does not produce or require `zh-CN/ApkShellext2.resources.dll`. On first run the default interface language follows the Windows display language when its translation is available (Simplified Chinese for `zh-CN` / `zh-SG`, otherwise English or an installed optional language). Explicit language choices in Settings take precedence.

The main release archive includes Chinese automatically. Other languages remain standard optional satellite assemblies and separate language archives. `smoke-tests/Program.cs` verifies the manifest resource and language selection, and `smoke-tests/VerifyPackagedRuntime.ps1` checks both x86/x64 unpacked release behavior. `scripts/package-release.ps1` refuses unexpected Chinese satellite DLL output.

The original installer/uninstaller scripts remain available. They now run both 32-bit and 64-bit `RegAsm.exe` when present, and fail clearly if registration cannot complete. Install from a permanent extracted folder; uninstall before moving it. Installing alongside an existing copy of ApkShellext2 is not supported because they register the same COM handler IDs.

The original project referenced an untracked PFX and stale Google.Protobuf packages. This fork carries a dedicated strong-name `.snk` file for reproducible COM registration; **strong naming does not mean Authenticode publisher signing**. The unused Protobuf project references were removed, without deleting parser functionality.

## Work sequence

1. Reproducible build with original runtime features unchanged.
2. Fix verified handle leaks, exception paths, and settings bugs with focused patches.
3. Improve existing Android resource/image resolution without changing supported packages.
4. Add XAPK/APKS/APKM handlers independently; never paste code from other forks.
5. Measure thumbnail/icon performance before adding caches; make installation/uninstallation reversible.

Please keep patches small and reviewable. The Windows CI builds and tests the parser, compiles libwebp x86/x64, unpacks the release archive, and **installs/uninstalls the actual COM handlers**. It then tests x64 and x86 Shell icon, thumbnail, info-tip and right-click interfaces on Windows Server 2022 and 2025, using deterministic fixtures for APK, XAPK/APKS/APKM, IPA, APPX, APPXBUNDLE, corrupted packages, in-progress downloads, GDI handle reuse and file deletion.

Targeted regression cases now cover Android binary boolean attributes, malformed resource chunk lengths, nested VectorDrawable groups, desktop APPX without `PhoneIdentity`, default thumbnail and menu settings, actual broken-file menu commands, and first-run update version parsing. The optional legacy HTTP service is built and separately tested on Windows; it only shares files staged under `%ProgramData%\\ApkShellext2\\Share` through randomly generated download tokens, and the normal installer does not start or install that service.

The COM integration test entrypoint is `smoke-tests/Invoke-ShellIntegration.ps1`. It registers Shell extensions system-wide and should only run with administrator privileges on a disposable Windows VM. Each architecture and scenario runs in a separate STA process with a timeout. CI still does **not** automate the interactive Explorer/Chrome user interfaces, Windows 11 client UI, or validate arbitrary modern real-world app packages.
