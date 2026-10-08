# ApkShellExt v1.1.1

This is a stable maintenance release for the Windows Explorer package-file extension.

## Changes

- **Right-click details placement:** the **View more information / 查看更多信息** command is now immediately below **Extract XML / 提取XML文件** in the first group of the ApkShellExt menu.
- **Cleaner menu separators:** there are no adjacent separators when every optional app-store link is switched off.
- **Faster settings window:** consolidated registry reads, reduced redundant layout passes, simplified the docked footer, and reused the supported-language list when switching languages. All existing settings and immediate-save behavior are retained.
- **Reduced startup flicker:** settings and localized labels are populated before first display; first-show handling is now covered by Windows integration tests. Actual cold-start time depends on the PC and shell environment.

## Compatibility and validation

- Windows 10/11 and **.NET Framework 4.8**; includes the 32-bit and 64-bit shell components and the original installation/uninstallation scripts.
- English and Simplified Chinese are included in the main DLL. Other translations are optional language packs.
- The Windows CI build compiles, packages and runs APK reader tests plus registered Shell COM integration checks in x86 and x64 on Windows Server 2022 and 2025.
- Release archives include `SHA256SUMS.txt` for verifying downloaded files.

## Upgrade from v1.1.0

1. Run the previous installation's `uninstall.bat`, following its administrator prompts.
2. Extract `ApkShellext2.7z` to a **permanent folder**, then run its `install.bat` as administrator.
3. Restart Explorer if the shell menu or file icons have not refreshed. Do not delete the installed folder.

The previous v1.1.0 release remains available for rollback. This release does not upgrade the .NET Framework requirement or change the supported package formats.
