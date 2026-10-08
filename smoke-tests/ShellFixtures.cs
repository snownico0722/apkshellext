using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ApkShellextIntegration {
    internal static class ShellFixtures {
        internal static readonly Color IconColor = Color.FromArgb(255, 241, 12, 173);
        internal const string AppName = "Shell Integration Test";
        internal const string PackageName = "org.apkshellext.ci";

        internal static string PathFor(string directory, string filename) {
            return Path.Combine(directory, filename);
        }

        internal static void Create(string directory) {
            Directory.CreateDirectory(directory);
            byte[] png = CreatePng();
            byte[] apk = CreateApk(png);

            File.WriteAllBytes(PathFor(directory, "sample.apk"), apk);
            File.WriteAllBytes(PathFor(directory, "SAMPLE.APK"), apk);
            File.WriteAllBytes(PathFor(directory, "zero.apk"), new byte[0]);
            File.WriteAllBytes(PathFor(directory, "incomplete.apk"), Slice(apk, Math.Min(48, apk.Length)));
            File.WriteAllBytes(PathFor(directory, "broken.apk"), new byte[] { 1, 2, 3, 4, 5 });
            File.WriteAllBytes(PathFor(directory, "bad-manifest.apk"), CreateApkWithInvalidManifest(png));

            foreach (string ext in new[] { ".xapk", ".apks", ".apkm" }) {
                using (var destination = new FileStream(PathFor(directory, "sample" + ext), FileMode.Create, FileAccess.Write))
                using (var zip = new ZipArchive(destination, ZipArchiveMode.Create)) {
                    Add(zip, "split_config.arm64_v8a.apk", new byte[] { 0, 1, 2, 3 });
                    Add(zip, "base.apk", apk);
                }
            }

            File.WriteAllBytes(PathFor(directory, "sample.ipa"), CreateIpa(png));
            File.WriteAllBytes(PathFor(directory, "sample.appx"), CreateAppx(png));
            File.WriteAllBytes(PathFor(directory, "desktop.appx"), CreateDesktopAppx(png));
            File.WriteAllBytes(PathFor(directory, "sample.appxbundle"), CreateAppxBundle(CreateAppx(png)));
            foreach (string ext in new[] { ".ipa", ".appx", ".appxbundle" })
                File.WriteAllBytes(PathFor(directory, "invalid" + ext), CreateUnrelatedZip());
        }

        private static byte[] Slice(byte[] bytes, int count) {
            byte[] result = new byte[count];
            Buffer.BlockCopy(bytes, 0, result, 0, count);
            return result;
        }

        private static byte[] CreatePng() {
            using (var image = new Bitmap(64, 64))
            using (var stream = new MemoryStream()) {
                using (Graphics graphics = Graphics.FromImage(image))
                    graphics.Clear(IconColor);
                image.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }

        private static byte[] CreateApk(byte[] png) {
            using (var stream = new MemoryStream()) {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) {
                    // First ZIP member also checks the original index-zero PNG regression.
                    Add(zip, "res/drawable/ICON.PNG", png);
                    Add(zip, "AndroidManifest.xml", CreateManifest());
                    Add(zip, "resources.arsc", Encoding.ASCII.GetBytes("fixture"));
                }
                return stream.ToArray();
            }
        }

        private static byte[] CreateApkWithInvalidManifest(byte[] png) {
            byte[] manifest = new byte[16];
            manifest[0] = 3;  // RES_XML_TYPE
            manifest[2] = 8;  // outer header size
            manifest[4] = 16; // total size, with an invalid zero-length child
            using (var stream = new MemoryStream()) {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) {
                    Add(zip, "res/drawable/ICON.PNG", png);
                    Add(zip, "AndroidManifest.xml", manifest);
                    Add(zip, "resources.arsc", Encoding.ASCII.GetBytes("fixture"));
                }
                return stream.ToArray();
            }
        }

        // A minimal valid Android binary XML (AXML) manifest. All attributes use
        // TYPE_STRING, so the APK does not need a compiled resource table.
        private static byte[] CreateManifest() {
            string[] strings = {
                "manifest", "application", "package", PackageName,
                "label", AppName, "icon", "res/drawable/ICON.PNG",
                "versionName", "1.0", "versionCode", "1"
            };
            var ids = new Dictionary<string, uint>(StringComparer.Ordinal);
            for (int i = 0; i < strings.Length; i++)
                ids[strings[i]] = (uint)i;

            using (var payload = new MemoryStream())
            using (var writer = new BinaryWriter(payload, Encoding.UTF8, true)) {
                WriteStringPool(writer, strings);
                WriteStart(writer, ids["manifest"], new[] {
                    new KeyValuePair<uint,uint>(ids["package"], ids[PackageName]),
                    new KeyValuePair<uint,uint>(ids["versionName"], ids["1.0"]),
                    new KeyValuePair<uint,uint>(ids["versionCode"], ids["1"])
                });
                WriteStart(writer, ids["application"], new[] {
                    new KeyValuePair<uint,uint>(ids["label"], ids[AppName]),
                    new KeyValuePair<uint,uint>(ids["icon"], ids["res/drawable/ICON.PNG"])
                });
                WriteEnd(writer, ids["application"]);
                WriteEnd(writer, ids["manifest"]);
                writer.Flush();

                using (var result = new MemoryStream())
                using (var outer = new BinaryWriter(result)) {
                    outer.Write((ushort)0x0003); // RES_XML_TYPE
                    outer.Write((ushort)8);
                    outer.Write(checked((uint)payload.Length + 8));
                    outer.Write(payload.ToArray());
                    return result.ToArray();
                }
            }
        }

        private static void WriteStringPool(BinaryWriter writer, string[] strings) {
            var bytes = new MemoryStream();
            var positions = new List<uint>();
            foreach (string s in strings) {
                positions.Add(checked((uint)bytes.Position));
                byte[] encoded = Encoding.UTF8.GetBytes(s);
                if (encoded.Length > 127 || s.Length > 127)
                    throw new InvalidOperationException("Fixture string too long for one-byte AXML length.");
                bytes.WriteByte((byte)s.Length);
                bytes.WriteByte((byte)encoded.Length);
                bytes.Write(encoded, 0, encoded.Length);
                bytes.WriteByte(0);
            }
            int dataSize = checked((int)bytes.Length);
            int start = 28 + 4 * strings.Length;
            int chunkSize = (start + dataSize + 3) & ~3;
            writer.Write((ushort)0x0001); // RES_STRING_POOL_TYPE
            writer.Write((ushort)28);
            writer.Write(chunkSize);
            writer.Write(strings.Length);
            writer.Write(0); // style count
            writer.Write(0x100); // UTF-8 string pool
            writer.Write(start);
            writer.Write(0); // style start
            foreach (uint offset in positions)
                writer.Write(offset);
            writer.Write(bytes.ToArray());
            for (int i = start + dataSize; i < chunkSize; i++)
                writer.Write((byte)0);
        }

        private static void WriteStart(BinaryWriter writer, uint element,
            KeyValuePair<uint, uint>[] attributes) {
            writer.Write((ushort)0x0102); // RES_XML_START_ELEMENT_TYPE
            writer.Write((ushort)16);
            writer.Write(36 + 20 * attributes.Length);
            writer.Write(0); // line number
            writer.Write(uint.MaxValue); // comment
            writer.Write(uint.MaxValue); // namespace
            writer.Write(element);
            writer.Write((ushort)20); // attributeStart
            writer.Write((ushort)20); // attributeSize
            writer.Write((ushort)attributes.Length);
            writer.Write((ushort)0); // idIndex
            writer.Write((ushort)0); // classIndex
            writer.Write((ushort)0); // styleIndex
            foreach (var pair in attributes) {
                writer.Write(uint.MaxValue); // namespace
                writer.Write(pair.Key); // name
                writer.Write(pair.Value); // rawValue
                writer.Write((ushort)8);
                writer.Write((byte)0);
                writer.Write((byte)0x03); // TYPE_STRING
                writer.Write(pair.Value); // string pool value index
            }
        }

        private static void WriteEnd(BinaryWriter writer, uint element) {
            writer.Write((ushort)0x0103); // RES_XML_END_ELEMENT_TYPE
            writer.Write((ushort)16);
            writer.Write(24);
            writer.Write(0);
            writer.Write(uint.MaxValue);
            writer.Write(uint.MaxValue);
            writer.Write(element);
        }

        private static byte[] CreateIpa(byte[] png) {
            const string plist = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">" +
                "<plist version=\"1.0\"><dict>" +
                "<key>CFBundleDisplayName</key><string>Shell iOS Test</string>" +
                "<key>CFBundleIdentifier</key><string>org.apkshellext.ios</string>" +
                "<key>CFBundleShortVersionString</key><string>1.0</string>" +
                "<key>CFBundleIconFile</key><string>AppIcon</string>" +
                "</dict></plist>";
            using (var ms = new MemoryStream()) {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true)) {
                    Add(zip, "Payload/CI.app/Info.plist", Encoding.UTF8.GetBytes(plist));
                    Add(zip, "Payload/CI.app/AppIcon.png", png);
                }
                return ms.ToArray();
            }
        }

        private static byte[] CreateAppx(byte[] png) {
            const string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<Package xmlns:mp=\"urn:apkshellext:mobile\">" +
                "<Identity Name=\"org.apkshellext.appx\" Version=\"1.0.0.0\" Publisher=\"CN=CI\" />" +
                "<Properties><DisplayName>Shell Windows Test</DisplayName><Logo>logo.png</Logo></Properties>" +
                "<mp:PhoneIdentity PhoneProductId=\"CI-TEST\" /></Package>";
            using (var ms = new MemoryStream()) {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true)) {
                    Add(zip, "AppxManifest.xml", Encoding.UTF8.GetBytes(xml));
                    Add(zip, "logo.png", png);
                }
                return ms.ToArray();
            }
        }

        private static byte[] CreateDesktopAppx(byte[] png) {
            // A legal desktop APPX does not have a mobile PhoneIdentity node.
            const string xml = "<Package><Identity Name='org.apkshellext.desktop' " +
                "Version='1.0.0.0' Publisher='CN=CI' />" +
                "<Properties><DisplayName>Shell Windows Test</DisplayName>" +
                "<Logo>logo.png</Logo></Properties></Package>";
            using (var ms = new MemoryStream()) {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true)) {
                    Add(zip, "AppxManifest.xml", Encoding.UTF8.GetBytes(xml));
                    Add(zip, "logo.png", png);
                }
                return ms.ToArray();
            }
        }

        private static byte[] CreateAppxBundle(byte[] appx) {
            const string xml = "<Bundle><Identity Name=\"CI\" />" +
                "<Packages><Package Type=\"application\" FileName=\"main.appx\" /></Packages></Bundle>";
            using (var ms = new MemoryStream()) {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true)) {
                    Add(zip, "AppxMetadata/AppxBundleManifest.xml", Encoding.UTF8.GetBytes(xml));
                    Add(zip, "main.appx", appx);
                }
                return ms.ToArray();
            }
        }

        private static byte[] CreateUnrelatedZip() {
            using (var ms = new MemoryStream()) {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                    Add(zip, "unrelated.txt", Encoding.ASCII.GetBytes("test"));
                return ms.ToArray();
            }
        }

        private static void Add(ZipArchive archive, string filename, byte[] bytes) {
            using (Stream stream = archive.CreateEntry(filename).Open())
                stream.Write(bytes, 0, bytes.Length);
        }
    }
}
