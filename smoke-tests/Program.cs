using System;
using System.IO;
using System.IO.Compression;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
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
                CheckFirstEntryUppercasePng(Path.Combine(dir, "first-entry.apk"));
                CheckCallerStreamIsNotClosed();
                CheckNestedAndroidPackages(dir);
                if (AppPackageReader.getAppType("sample.APK") != AppPackageReader.AppType.AndroidApp ||
                    AppPackageReader.getAppType("sample.IPA") != AppPackageReader.AppType.iOSApp ||
                    AppPackageReader.getAppType("sample.APPX") != AppPackageReader.AppType.WindowsPhoneApp)
                    throw new Exception("Case-insensitive package detection failed");
                Console.WriteLine("PASS: APK stream lifetime, first-entry PNG, lazy resources and bundles");
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
                var resourceField = typeof(ApkReader).GetField("resources",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (resourceField == null || resourceField.GetValue(reader) != null)
                    throw new Exception("Resource table was loaded before it was requested");
            }
            using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
            }
        }

        private static void CheckFirstEntryUppercasePng(string path) {
            byte[] image;
            using (var buffer = new MemoryStream()) {
                using (var bitmap = new Bitmap(4, 4)) {
                    bitmap.SetPixel(0, 0, Color.Red);
                    bitmap.Save(buffer, ImageFormat.Png);
                }
                image = buffer.ToArray();
            }
            using (var archive = new ZipArchive(new FileStream(path, FileMode.Create, FileAccess.Write), ZipArchiveMode.Create)) {
                using (Stream destination = archive.CreateEntry("res/drawable/ICON.PNG").Open())
                    destination.Write(image, 0, image.Length);
                WriteEntry(archive, "androidmanifest.xml");
                WriteEntry(archive, "resources.arsc");
            }
            using (var reader = new ApkReader(path))
            using (Bitmap icon = reader.getImage("res/drawable/ICON.PNG", new Size(48, 48))) {
                if (icon == null || icon.Width != 48 || icon.Height != 48 ||
                    icon.GetPixel(0, 0).A == 0)
                    throw new Exception("Uppercase PNG in the first ZIP entry was not decoded");
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

        private static void CheckNestedAndroidPackages(string dir) {
            byte[] baseApk;
            using (var buffer = new MemoryStream()) {
                using (var inner = new ZipArchive(buffer, ZipArchiveMode.Create, true)) {
                    WriteEntry(inner, "androidmanifest.xml");
                    WriteEntry(inner, "resources.arsc");
                }
                baseApk = buffer.ToArray();
            }

            foreach (string ext in new[] { ".xapk", ".apks", ".apkm" }) {
                string path = Path.Combine(dir, "nested" + ext);
                using (var bundle = new ZipArchive(new FileStream(path, FileMode.Create, FileAccess.Write), ZipArchiveMode.Create)) {
                    // The first APK entry is an unusable split; prefer base.apk.
                    using (Stream split = bundle.CreateEntry("split_config.arm64_v8a.apk").Open())
                        split.Write(new byte[] { 0, 1, 2 }, 0, 3);
                    using (Stream nested = bundle.CreateEntry("base.apk").Open())
                        nested.Write(baseApk, 0, baseApk.Length);
                }
                using (AppPackageReader reader = AppPackageReader.Read(path)) {
                    if (!(reader is ApkReader) || reader.Type != AppPackageReader.AppType.AndroidApp ||
                        reader.FileName != path)
                        throw new Exception("Bundle reader did not preserve the original APK metadata API");
                }
                using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
                }
            }
        }

        private static void WriteEntry(ZipArchive archive, string name) {
            using (var writer = new StreamWriter(archive.CreateEntry(name).Open())) {
                writer.Write("fixture");
            }
        }
    }
}