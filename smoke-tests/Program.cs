using System;
using System.IO;
using System.IO.Compression;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
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
                CheckBooleanAndResourceGuards(Path.Combine(dir, "resources.apk"));
                CheckInvalidBinaryManifestTerminates(Path.Combine(dir, "bad-manifest.apk"));
                CheckFirstEntryUppercasePng(Path.Combine(dir, "first-entry.apk"));
                CheckVectorPathRendering();
                CheckNestedVectorGroups(Path.Combine(dir, "valid.apk"));
                CheckUpdateVersionParsing();
                CheckCallerStreamIsNotClosed();
                CheckNestedAndroidPackages(dir);
                CheckMalformedLegacyPackages(dir);
                if (AppPackageReader.getAppType("sample.APK") != AppPackageReader.AppType.AndroidApp ||
                    AppPackageReader.getAppType("sample.IPA") != AppPackageReader.AppType.iOSApp ||
                    AppPackageReader.getAppType("sample.APPX") != AppPackageReader.AppType.WindowsPhoneApp)
                    throw new Exception("Case-insensitive package detection failed");
                Console.WriteLine("PASS: APK streams, binary XML, PNG icons, bundles and malformed-package cleanup");
                return 0;
            } catch (Exception ex) {
                Console.Error.WriteLine("FAIL: " + ex);
                return 1;
            } finally {
                Directory.Delete(dir, true);
            }
        }

        private static void ExpectInvalidChunk(Action action, string label) {
            try {
                action();
            } catch (TargetInvocationException e) {
                if (e.InnerException is InvalidDataException)
                    return;
                throw new Exception("Unexpected error in " + label, e.InnerException);
            }
            throw new Exception("Invalid Android chunk was accepted: " + label);
        }

        private static void CheckBooleanAndResourceGuards(string path) {
            byte[] badXml = new byte[16];
            badXml[0] = 3;
            badXml[2] = 8;
            badXml[4] = 16;
            badXml[10] = 8; // second chunk has a valid header but zero size

            // Minimal resources.arsc with one package and a zero-size type
            // chunk. Without the cursor guard QuickSearchResource hangs.
            byte[] badResources = new byte[64];
            using (var stream = new MemoryStream(badResources))
            using (var writer = new BinaryWriter(stream)) {
                writer.Write((ushort)2);
                writer.Write((ushort)12);
                writer.Write(64);
                writer.Write(1); // packageCount
                writer.Write((ushort)1);
                writer.Write((ushort)8);
                writer.Write(8); // global string pool size
                writer.Write((ushort)0x0200);
                writer.Write((ushort)20);
                writer.Write(44); // packageSize
                writer.Write(1); // packageId
                writer.Seek(40, SeekOrigin.Begin);
                for (int i = 0; i < 2; i++) {
                    writer.Write((ushort)1);
                    writer.Write((ushort)8);
                    writer.Write(8);
                }
                writer.Write((ushort)0x0201);
                writer.Write((ushort)8);
                writer.Write(0); // invalid resource subchunk
            }

            using (var archive = new ZipArchive(
                new FileStream(path, FileMode.Create, FileAccess.Write), ZipArchiveMode.Create)) {
                WriteEntry(archive, "androidmanifest.xml");
                using (Stream entry = archive.CreateEntry("resources.arsc").Open())
                    entry.Write(badResources, 0, badResources.Length);
            }
            using (var reader = new ApkReader(path)) {
                MethodInfo convert = typeof(ApkReader).GetMethod("convertData",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (!"false".Equals(convert.Invoke(reader,
                    new object[] { badXml, DATA_TYPE.TYPE_INT_BOOLEAN, 0u })) ||
                    !"true".Equals(convert.Invoke(reader,
                    new object[] { badXml, DATA_TYPE.TYPE_INT_BOOLEAN, 1u })))
                    throw new Exception("Android binary boolean true/false were reversed");

                MethodInfo strings = typeof(ApkReader).GetMethod("QuickSearchStringPool",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo map = typeof(ApkReader).GetMethod("QuickSearchCompressedXMlResMap",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo resource = typeof(ApkReader).GetMethod("QuickSearchResource",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                ExpectInvalidChunk(() => strings.Invoke(reader,
                    new object[] { badXml, (uint)0 }), "Android string pool");
                ExpectInvalidChunk(() => map.Invoke(reader,
                    new object[] { badXml, (uint)0 }), "Android resource map");
                ExpectInvalidChunk(() => resource.Invoke(reader,
                    new object[] { 0x01010000u }), "Android resource table");

                FieldInfo stack = typeof(ApkReader).GetField("searchstack",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var pending = (System.Collections.ICollection)stack.GetValue(reader);
                if (pending.Count != 0)
                    throw new Exception("Failed resource lookup left stale recursion state");
            }
            using (var exclusive = new FileStream(path, FileMode.Open,
                FileAccess.ReadWrite, FileShare.None)) {
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

        private static void CheckInvalidBinaryManifestTerminates(string path) {
            // Binary XML header is 16 bytes total; the second chunk claims
            // zero length. This used to loop forever because Seek did not advance.
            byte[] manifest = new byte[16];
            manifest[0] = 3;
            manifest[2] = 8;
            manifest[4] = 16;
            using (var archive = new ZipArchive(
                new FileStream(path, FileMode.Create, FileAccess.Write), ZipArchiveMode.Create)) {
                using (Stream entry = archive.CreateEntry("androidmanifest.xml").Open())
                    entry.Write(manifest, 0, manifest.Length);
                WriteEntry(archive, "resources.arsc");
            }

            using (var reader = new ApkReader(path)) {
                bool attributeRejected = false;
                try {
                    reader.getAttribute("manifest/application", "label");
                } catch (InvalidDataException) {
                    attributeRejected = true;
                }
                if (!attributeRejected)
                    throw new Exception("Malformed Android manifest attribute loop did not stop");

                bool dumpRejected = false;
                try {
                    reader.ExtractCompressedXml("androidmanifest.xml");
                } catch (Exception ex) {
                    dumpRejected = ex.Message.Contains("Invalid binary Android XML chunk size");
                }
                if (!dumpRejected)
                    throw new Exception("Malformed Android manifest dump loop did not stop");
            }

            using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
            }
        }

        private static void CheckVectorPathRendering() {
            // Verify the updated SVG parser still supplies path geometry to
            // Android VectorDrawable rendering (not just that it compiles).
            using (GraphicsPath path = VectorDrawableRender.Convert2Path("M0,0 L8,8 L8,0 Z")) {
                if (path == null || path.PointCount < 3)
                    throw new Exception("SVG vector path was not rendered after the Svg upgrade");
            }
        }

        private static void CheckNestedVectorGroups(string apk) {
            var doc = new System.Xml.XmlDocument();
            doc.LoadXml("<vector viewportWidth='32' viewportHeight='16'>" +
                "<group><path pathData='M0,0 L8,0 L8,8 L0,8 Z' fillColor='#FFFF0000'/></group>" +
                "<group translateX='16'><group>" +
                "<path pathData='M0,0 L8,0 L8,8 L0,8 Z' fillColor='#FF00FF00'/>" +
                "</group></group></vector>");
            using (var reader = new ApkReader(apk)) {
                MethodInfo render = typeof(ApkReader).GetMethod("parseVectorDrawable",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                using (Bitmap bitmap = (Bitmap)render.Invoke(reader,
                    new object[] { doc, new Size(32, 16) })) {
                    Color left = bitmap.GetPixel(4, 4);
                    Color right = bitmap.GetPixel(20, 4);
                    if (left.R < 200 || left.G > 70 || right.G < 200 || right.R > 70) {
                        using (GraphicsPath debugPath = VectorDrawableRender.Convert2Path(
                            "M0,0 L8,0 L8,8 L0,8 Z")) {
                            int opaque = 0;
                            for (int x = 0; x < bitmap.Width; x++)
                                for (int y = 0; y < bitmap.Height; y++)
                                    if (bitmap.GetPixel(x, y).A != 0) opaque++;
                            Color controlPixel;
                            using (Bitmap control = new Bitmap(32, 16)) {
                                using (Graphics g = Graphics.FromImage(control))
                                    g.FillPath(Brushes.Red, debugPath);
                                controlPixel = control.GetPixel(4, 4);
                            }
                            throw new Exception("Nested VectorDrawable colors/transform wrong: left=" +
                                left + ", right=" + right + ", opaque=" + opaque +
                                ", pathPoints=" + debugPath.PointCount +
                                ", pathBounds=" + debugPath.GetBounds() +
                                ", directFillPixel=" + controlPixel);
                        }
                    }
                }
            }
        }

        private static void CheckUpdateVersionParsing() {
            MethodInfo parse = typeof(Utility).GetMethod("TryParseLatestVersion",
                BindingFlags.NonPublic | BindingFlags.Static);
            object[] args = { "{\"tag_name\":\"v0.4.1\"}", null };
            if (!(bool)parse.Invoke(null, args) ||
                !((Version)args[1]).Equals(new Version(0, 4, 1, 0)))
                throw new Exception("GitHub release JSON version parsing failed");

            args = new object[] { "corrupted.version", null };
            if ((bool)parse.Invoke(null, args))
                throw new Exception("Malformed release version was accepted");
            string previous = Utility.GetSetting("LatestVersion", "");
            string previousCheck = Utility.GetSetting("LastCheckUpdateTime", "");
            try {
                Utility.SaveSetting("LatestVersion", "corrupted.version");
                if (Utility.NewVersionAvailible())
                    throw new Exception("Invalid cached version caused a false update signal");

                Utility.SaveSetting("LatestVersion", "0.4.1.0");
                if (!Utility.NewVersionAvailible())
                    throw new Exception("Valid newer release was not detected");

                Utility.SaveSetting("LastCheckUpdateTime", "invalid-date");
                Utility.CheckUpdate();
                DateTime parsed;
                if (!DateTime.TryParse(Utility.GetSetting("LastCheckUpdateTime", ""),
                    out parsed) || parsed.Date != DateTime.Today)
                    throw new Exception("First-run update check was not scheduled");
            } finally {
                Utility.SaveSetting("LatestVersion", previous);
                Utility.SaveSetting("LastCheckUpdateTime", previousCheck);
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

        private static void CheckMalformedLegacyPackages(string dir) {
            foreach (string extension in new[] { ".ipa", ".appx", ".appxbundle" }) {
                string path = Path.Combine(dir, "invalid" + extension);
                using (var archive = new ZipArchive(
                    new FileStream(path, FileMode.Create, FileAccess.Write), ZipArchiveMode.Create)) {
                    if (extension == ".appxbundle") {
                        // A valid bundle manifest without an application must terminate,
                        // not loop forever while scanning past the final Package element.
                        using (var writer = new StreamWriter(
                            archive.CreateEntry("AppxMetadata/AppxBundleManifest.xml").Open())) {
                            writer.Write("<Bundle><Identity Name=\"test\" />" +
                                "<Package Type=\"resource\" FileName=\"resource.appx\" /></Bundle>");
                        }
                    } else {
                        WriteEntry(archive, "unrelated.txt");
                    }
                }

                bool rejected = false;
                try {
                    using (AppPackageReader reader = AppPackageReader.Read(path)) {
                    }
                } catch (Exception) {
                    rejected = true;
                }
                if (!rejected)
                    throw new Exception("Invalid package was accepted: " + extension);

                using (var exclusive = new FileStream(
                    path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
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