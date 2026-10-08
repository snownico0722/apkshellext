using System;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;

namespace ApkQuickReader {
    // Extract only the launchable base APK from a compound Android package.
    // The existing APK parser handles all manifest/resource/image decoding.
    public sealed class ApkBundleReader : ApkReader {
        public ApkBundleReader(string filename) : base(ExtractFromFile(filename), true) {
            FileName = filename;
        }

        public ApkBundleReader(Stream packageStream) : base(ExtractFromStream(packageStream), true) {
        }

        private static Stream ExtractFromFile(string path) {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete)) {
                return ExtractFromStream(stream);
            }
        }

        private static Stream ExtractFromStream(Stream packageStream) {
            if (packageStream == null || !packageStream.CanSeek)
                throw new InvalidDataException("The bundle must be a seekable ZIP stream");

            long originalPosition = packageStream.Position;
            try {
                packageStream.Position = 0;
                // The underlying file/COM stream remains owned by its caller,
                // including when the ZIP constructor throws.
                using (var borrowed = new BorrowedStream(packageStream))
                using (var zip = new ZipFile(borrowed)) {
                    zip.IsStreamOwner = false;
                    ZipEntry mainEntry = FindBaseApk(zip);
                    if (mainEntry == null)
                        throw new InvalidDataException("No APK file was found inside the bundle");

                    string path = Path.Combine(Path.GetTempPath(),
                        "ApkShellext_" + Guid.NewGuid().ToString("N") + ".apk");
                    // DeleteOnClose is bounded-memory and leaves no temporary file
                    // after the owned stream is released by ApkReader.
                    FileStream extracted = new FileStream(path, FileMode.CreateNew,
                        FileAccess.ReadWrite, FileShare.Read, 81920, FileOptions.DeleteOnClose);
                    try {
                        using (Stream nestedApk = zip.GetInputStream(mainEntry))
                            nestedApk.CopyTo(extracted);
                        extracted.Position = 0;
                        return extracted;
                    } catch {
                        extracted.Dispose();
                        throw;
                    }
                }
            } finally {
                packageStream.Position = originalPosition;
            }
        }

        private static ZipEntry FindBaseApk(ZipFile zip) {
            ZipEntry chosen = null;
            int chosenPriority = -1;
            foreach (ZipEntry entry in zip) {
                if (!entry.IsFile)
                    continue;
                string normalized = entry.Name.Replace('\\', '/');
                if (!normalized.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                    continue;
                string fileName = normalized.Substring(normalized.LastIndexOf('/') + 1);
                int priority = 20;
                if (fileName.Equals("base.apk", StringComparison.OrdinalIgnoreCase))
                    priority = 100;
                else if (fileName.Equals("base-master.apk", StringComparison.OrdinalIgnoreCase))
                    priority = 90;
                else if (fileName.Equals("standalone.apk", StringComparison.OrdinalIgnoreCase))
                    priority = 80;
                else if (fileName.Equals("main.apk", StringComparison.OrdinalIgnoreCase))
                    priority = 70;
                else if (fileName.StartsWith("split_", StringComparison.OrdinalIgnoreCase) ||
                         fileName.StartsWith("config.", StringComparison.OrdinalIgnoreCase))
                    priority = 0;

                if (priority > chosenPriority) {
                    chosen = entry;
                    chosenPriority = priority;
                }
            }
            return chosen;
        }
    }
}
