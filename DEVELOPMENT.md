# Building and contributing

This fork follows the upstream **ApkShellext2** branch. Work is incremental: **keep all existing features** (APK/IPA/APPX icon handlers, thumbnails, info tips, context menu, batch rename, stores, translations, settings, and version check). Changes must not silently remove or change them.

## Windows development environment

- Windows 10/11 and Visual Studio 2022 with the **.NET desktop development** workload and .NET Framework 4.8 targeting pack.
- Clone with submodules: `git clone --recurse-submodules https://github.com/snownico0722/apkshellext.git`. For an existing clone run `git submodule update --init --recursive`.
- Open `ApkShellext2.sln` and build **Release | Any CPU**, or execute `msbuild ApkShellext2.sln /m /p:Configuration=Release` from a Visual Studio Developer Command Prompt.
- The normal build copies the existing installer/uninstaller scripts and native WebP DLLs to `ApkShellext2/bin/Release/`. It does **not** install a Shell extension, restart Explorer, or require a locally installed 7-Zip. Release archives can be packaged separately.
- The service project remains in the repository; it can be built explicitly with `msbuild ApkShellextService/apkShellextService.csproj /p:Configuration=Release`. Normal builds do not install/start the service.

The original project referenced an untracked PFX and stale Google.Protobuf packages. This fork carries a dedicated strong-name `.snk` file for reproducible COM registration; **strong naming does not mean Authenticode publisher signing**. The unused Protobuf project references were removed, without deleting parser functionality.

## Work sequence

1. Reproducible build with original runtime features unchanged.
2. Fix verified handle leaks, exception paths, and settings bugs with focused patches.
3. Improve existing Android resource/image resolution without changing supported packages.
4. Add XAPK/APKS/APKM handlers independently; never paste code from other forks.
5. Measure thumbnail/icon performance before adding caches; make installation/uninstallation reversible.

Please keep patches small and reviewable. For shell behavior validate on Windows with representative APK, IPA and APPX samples, file renames/deletes after failed reads, and install/uninstall. GitHub CI only compiles; it cannot prove Explorer stability.
