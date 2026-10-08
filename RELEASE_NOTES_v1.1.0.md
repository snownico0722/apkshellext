# ApkShellExt v1.1.0

This release simplifies package file actions and preferences while keeping the existing Windows Shell extension and its original package-format support.

## What's new

- **Single-page settings:** all settings are grouped in one scrollable page, without the old left-hand navigation tree. Sponsorship, translation-help and external Wiki links are no longer shown in settings; pattern variables are explained in-place.
- **Simpler right-click menu:** Android XML extraction and app-store actions are directly available inside the ApkShellExt menu instead of additional submenus.
- **More details / 更多:** right-click one supported APK, XAPK, APKS, APKM, IPA, APPX or APPXBUNDLE and open a read-only metadata window. Depending on the format, it shows the application name, package ID, version/build, app ID, file size and path, and Android SDK attributes. It supports copying all displayed information.
- **Correct metadata labeling:** Android package namespace prefixes are no longer mislabeled as a verified app publisher. Publisher information is shown for formats whose reader actually provides it.

## Fixes

- Malformed IPA files no longer cause an unhandled exception when the Apple App Store command is selected.
- Mixed selections skip non-Android packages for Android manifest export and store links.
- Opening preferences does not accidentally save default checkbox states or refresh Explorer; changing language updates labels without reloading the form.
- Rename, InfoTip and whitespace replacement patterns now save on change, consistent with the other settings. Closing by the X button no longer discards these edits.
- The thumbnail-cache cleanup prompt appears only once, including when prompted after closing preferences.
- The inline variable reference includes %Debuggable% and Android manifest %Attr(tag,attribute)% expressions.
- APKMirror search intentionally uses the **full package name**, not a truncated package namespace prefix.

## Upgrade from v1.0.0

1. Extract `ApkShellext2.7z` (or the ZIP variant) into a permanent folder.
2. Run the previous installation's `uninstall.bat`, then install this release with its `install.bat` as administrator. Follow the included instructions if an Explorer restart is needed.
3. Keep the extracted installation directory in place after installation.

Windows 10/11 and **.NET Framework 4.8** are required. The main package includes built-in Simplified Chinese and English, both x86 and x64 Shell components, and the required runtime dependencies. Other languages remain separately downloadable if available.

## Checks

GitHub Actions builds and packages the software, tests parser and metadata behavior, and exercises the registered Shell COM handlers on Windows Server 2022/2025 for 64- and 32-bit systems. Automated tests do not validate arbitrary real-world APK packages or visually inspect Windows 11 Explorer's interactive layout.

The previous v1.0.0 GitHub Release is retired **only after** the v1.1.0 release and its assets have been successfully published and verified. Git repository history is preserved.
