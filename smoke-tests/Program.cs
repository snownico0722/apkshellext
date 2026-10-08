using System;
using System.IO;
using System.IO.Compression;
using ApkQuickReader;
using ApkShellext2;

namespace ApkShellextSmokeTests {
    internal static class Program {
        private static int Main() {
            string dir = Path.Combine(Path.GetTempPath(), "ApkShellextSmoke_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try {
                CheckBrokenFileDoesNotRemainOpen(Path.Combine(dir, "broken.apk"));
                CheckSuccessfulReadDoesNotRemainOpen(Path.Combine(dir, "valid.apk"));
                CheckCallerStreamIsNotClosed();
                if (AppPackageReader.getAppType("sample.APK") != AppPackageReader.AppType.AndroidApp ||
                    AppPackageReader.getAppType("sample.IPA") != AppPackageReader.AppType.iOSApp ||
                    AppPackageReader.getAppType("sample.APPX") != AppPackageReader.AppType.WindowsPhoneApp)
                    throw new Exception("Case-insensitive package detection failed");
                Console.WriteLine("PASS: invalid/valid APK stream lifetime and uppercase extensions");
                return 0;
            } catch (Exception ex) {
                Console.Error.WriteLine("FAIL: " + ex);
                return 1;
            } finally {
                Directory.Delete(dir, true);
            }
        }

        private static void CheckBrokenFileDoesNotRemainOpen(string path) {
            File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4, 5 });
            bool rejected = false;
            try {
                using (var reader = new ApkReader(path)) {
                }
            } catch (Exception) {
                rejected = true;
            }
            if (!rejected)
                throw new Exception("Malformed APK was unexpectedly accepted");
            using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
            }
        }

        private static void CheckSuccessfulReadDoesNotRemainOpen(string path) {
            using (var archive = new ZipArchive(new FileStream(path, FileMode.Create, FileAccess.Write), ZipArchiveMode.Create)) {
                WriteEntry(archive, "androidmanifest.xml");
                WriteEntry(archive, "resources.arsc");
            }
            using (var reader = new ApkReader(path)) {
                if (reader == null)
                    throw new Exception("Unable to open ZIP-based APK");
            }
            using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
            }
        }

        private static void CheckCallerStreamIsNotClosed() {
            using (var supplied = new MemoryStream(new byte[] { 1, 2, 3, 4 })) {
                try {
                    using (var reader = new ApkReader(supplied)) {
                    }
                } catch (Exception) {
                    // Malformed content is expected; caller ownership is what matters.
                }
                if (!supplied.CanRead)
                    throw new Exception("The APK parser closed its caller-owned stream");
            }
        }

        private static void WriteEntry(ZipArchive archive, string name) {
            using (var writer = new StreamWriter(archive.CreateEntry(name).Open())) {
                writer.Write("fixture");
            }
        }
    }
}