using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ApkShellextIntegration {
    public static class ShellComSmoke {
        public static int Run(string workspace, string suite) {
            try {
                Console.WriteLine("Shell COM integration: " + suite + ", " +
                    (IntPtr.Size * 8) + "-bit, Windows " + Environment.OSVersion.Version);
                if (string.IsNullOrWhiteSpace(workspace)) throw new ArgumentNullException("workspace");
                ShellFixtures.Create(workspace);
                // The context-menu handler launches a version-check thread on first use.
                // Keep this CI test deterministic and offline.
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ApkShellext2"))
                    key.SetValue("LastCheckUpdateTime", DateTime.Today.ToString());

                switch (suite) {
                    case "registration": CheckComActivation(); break;
                    case "valid": CheckValidPackages(workspace); break;
                    case "invalid": CheckInvalidPackages(workspace); break;
                    case "stress": CheckRepeatedRequests(workspace); break;
                    default: throw new ArgumentException("Unknown test suite: " + suite);
                }
                Console.WriteLine("PASS: " + suite + " (" + IntPtr.Size * 8 + "-bit)");
                return 0;
            } catch (Exception ex) {
                Console.Error.WriteLine("FAIL: " + suite + " (" + IntPtr.Size * 8 + "-bit)");
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void Require(bool value, string message) {
            if (!value) throw new Exception(message);
        }

        private static void CheckComActivation() {
            var classes = new[] {
                Native.Icon, Native.Thumbnail, Native.Tip, Native.Context
            };
            var interfaces = new[] {
                typeof(IExtractIconW), typeof(IThumbnailProvider),
                typeof(IQueryInfo), typeof(IContextMenu)
            };
            for (int i = 0; i < classes.Length; i++) {
                object instance = Native.Create(classes[i]);
                try {
                    Require(Native.SupportsInterface(instance, interfaces[i].GUID),
                        "COM class is not exposing the expected Shell interface: " + classes[i]);
                } finally { Native.Release(instance); }
            }
            Console.WriteLine("PASS: four registered CLSIDs and Shell interfaces");
        }

        private static void CheckValidPackages(string dir) {
            CheckComActivation();
            string[] names = {
                "sample.apk", "SAMPLE.APK", "sample.xapk", "sample.apks",
                "sample.apkm", "sample.ipa", "sample.appx", "sample.appxbundle"
            };
            foreach (string filename in names) {
                string path = ShellFixtures.PathFor(dir, filename);
                Require(File.Exists(path), "Missing fixture: " + path);
                bool android = filename.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) ||
                    filename.EndsWith(".xapk", StringComparison.OrdinalIgnoreCase) ||
                    filename.EndsWith(".apks", StringComparison.OrdinalIgnoreCase) ||
                    filename.EndsWith(".apkm", StringComparison.OrdinalIgnoreCase);

                CheckInfoTip(path, android ? ShellFixtures.AppName : null);
                // Only Android paths have a distinct, explicit expected icon color.
                CheckExtractIcon(path, android);
                CheckContextMenu(new[] { path });
                if (android) CheckThumbnail(path, true);
                AssertFileIsWritable(path);
                Console.WriteLine("PASS: icon, info tip, context menu" +
                    (android ? ", thumbnail" : "") + " for " + filename);
            }

            CheckContextMenu(new[] {
                ShellFixtures.PathFor(dir, "sample.apk"),
                ShellFixtures.PathFor(dir, "sample.ipa"),
                ShellFixtures.PathFor(dir, "sample.appx")
            });
            CheckShellLookup(ShellFixtures.PathFor(dir, "sample.apk"));
        }

        private static void CheckInvalidPackages(string dir) {
            string[] invalid = {
                "zero.apk", "incomplete.apk", "broken.apk", "bad-manifest.apk",
                "invalid.ipa", "invalid.appx", "invalid.appxbundle"
            };
            foreach (string filename in invalid) {
                string path = ShellFixtures.PathFor(dir, filename);
                CheckInfoTip(path, null);
                CheckExtractIcon(path, false);
                CheckContextMenu(new[] { path });
                if (filename.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                    CheckThumbnail(path, false);
                AssertFileIsWritable(path);
                Console.WriteLine("PASS: invalid package does not crash or lock: " + filename);
            }
        }

        private static void CheckRepeatedRequests(string dir) {
            string path = ShellFixtures.PathFor(dir, "sample.apk");
            int startGdi = GetGdiCount();
            for (int i = 0; i < 32; i++) {
                CheckExtractIcon(path, true);
                CheckInfoTip(path, ShellFixtures.AppName);
                if (i % 4 == 0) CheckThumbnail(path, true);
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            int finalGdi = GetGdiCount();
            if (startGdi != 0 && finalGdi != 0)
                Require(finalGdi <= startGdi + 60,
                    "GDI handles increased unexpectedly: " + startGdi + " -> " + finalGdi);

            string renamed = ShellFixtures.PathFor(dir, "moved.apk");
            File.Move(path, renamed);
            File.Delete(renamed);
            Require(!File.Exists(renamed), "Previously inspected APK remained in use.");
            Console.WriteLine("PASS: 32 icon + info tip requests, thumbnails, GDI handles, rename/delete");
        }

        private static int GetGdiCount() {
            using (Process p = Process.GetCurrentProcess())
                return GetGuiResources(p.Handle, 0);
        }

        [DllImport("user32.dll")]
        private static extern int GetGuiResources(IntPtr process, int flags);

        private static void CheckExtractIcon(string path, bool matchFixtureColor) {
            object instance = Native.Create(Native.Icon);
            IntPtr large = IntPtr.Zero, small = IntPtr.Zero;
            try {
                ((IPersistFile)instance).Load(path, 0);
                var icon = Native.AsInterface<IExtractIconW>(instance);
                int index;
                uint flags;
                Native.Check(icon.GetIconLocation(0, new StringBuilder(260), 260,
                    out index, out flags), "IExtractIconW.GetIconLocation");
                Require((flags & 0x0008) != 0, "Icon extraction should use an HICON, not a file index.");

                int hr = icon.Extract("", (uint)index, out large, out small,
                    (uint)(48 | (16 << 16)));
                Native.Check(hr, "IExtractIconW.Extract");
                Require(large != IntPtr.Zero && small != IntPtr.Zero,
                    "Shell handler failed to return both HICON sizes: " + Path.GetFileName(path));
                CheckIconBitmap(large, matchFixtureColor);
            } finally {
                if (small != IntPtr.Zero) Native.DestroyIcon(small);
                if (large != IntPtr.Zero) Native.DestroyIcon(large);
                Native.Release(instance);
            }
        }

        private static void CheckIconBitmap(IntPtr iconHandle, bool matchFixtureColor) {
            using (Icon icon = Icon.FromHandle(iconHandle))
            using (Bitmap bitmap = icon.ToBitmap()) {
                Require(bitmap.Width >= 16 && bitmap.Height >= 16,
                    "Shell icon dimensions are unexpectedly small.");
                if (matchFixtureColor) CheckPixel(bitmap.GetPixel(bitmap.Width / 2,
                    bitmap.Height / 2), "HICON");
            }
        }

        private static void CheckPixel(Color color, string source) {
            Color expected = ShellFixtures.IconColor;
            const int tolerance = 35;
            Require(Math.Abs(color.R - expected.R) <= tolerance &&
                Math.Abs(color.G - expected.G) <= tolerance &&
                Math.Abs(color.B - expected.B) <= tolerance,
                source + " did not come from the APK's embedded icon: " + color);
        }

        private static void CheckInfoTip(string path, string expectedName) {
            object instance = Native.Create(Native.Tip);
            try {
                ((IPersistFile)instance).Load(path, 0);
                string text;
                Native.Check(Native.AsInterface<IQueryInfo>(instance).GetInfoTip(0, out text),
                    "IQueryInfo.GetInfoTip");
                Require(!string.IsNullOrWhiteSpace(text),
                    "No tooltip text was returned: " + Path.GetFileName(path));
                if (expectedName != null)
                    Require(text.Contains(expectedName), "Tooltip lost the APK app name: " + text);
                int flags;
                Native.Check(Native.AsInterface<IQueryInfo>(instance).GetInfoFlags(out flags),
                    "IQueryInfo.GetInfoFlags");
            } finally { Native.Release(instance); }
        }

        private static void CheckThumbnail(string path, bool shouldExist) {
            IStream input;
            Native.Check(Native.SHCreateStreamOnFileEx(path, 0x40, 0, false,
                IntPtr.Zero, out input), "SHCreateStreamOnFileEx");
            object instance = Native.Create(Native.Thumbnail);
            IntPtr bitmapHandle = IntPtr.Zero;
            try {
                Native.Check(Native.AsInterface<IInitializeWithStream>(instance).Initialize(input, 0),
                    "IInitializeWithStream.Initialize");
                int alpha;
                int hr = Native.AsInterface<IThumbnailProvider>(instance).GetThumbnail(64, out bitmapHandle, out alpha);
                if (shouldExist) {
                    Native.Check(hr, "IThumbnailProvider.GetThumbnail");
                    Require(bitmapHandle != IntPtr.Zero, "Thumbnail handler returned no HBITMAP.");
                    Require(alpha == 2, "Thumbnail did not declare ARGB content.");
                    using (Bitmap bitmap = Image.FromHbitmap(bitmapHandle))
                        CheckPixel(bitmap.GetPixel(bitmap.Width / 2,
                            bitmap.Height / 2), "IThumbnailProvider");
                } else {
                    Require(hr < 0 && bitmapHandle == IntPtr.Zero,
                        "Malformed APK must fail thumbnail generation cleanly.");
                }
            } finally {
                if (bitmapHandle != IntPtr.Zero) Native.DeleteObject(bitmapHandle);
                Native.Release(instance);
                if (input != null && Marshal.IsComObject(input))
                    Marshal.FinalReleaseComObject(input);
            }
        }

        private static void CheckContextMenu(string[] files) {
            object instance = Native.Create(Native.Context);
            IntPtr dataPointer = IntPtr.Zero;
            IntPtr nativeMenu = IntPtr.Zero;
            try {
                var data = new DataObject();
                var filePaths = new StringCollection();
                filePaths.AddRange(files);
                data.SetFileDropList(filePaths);
                dataPointer = Marshal.GetComInterfaceForObject(data,
                    typeof(System.Runtime.InteropServices.ComTypes.IDataObject));
                Native.AsInterface<IShellExtInit>(instance).Initialize(IntPtr.Zero, dataPointer, IntPtr.Zero);

                nativeMenu = Native.CreatePopupMenu();
                Require(nativeMenu != IntPtr.Zero, "CreatePopupMenu failed.");
                int result = Native.AsInterface<IContextMenu>(instance).QueryContextMenu(nativeMenu,
                    0, 1, 0x7FFF, 0);
                Native.Check(result, "IContextMenu.QueryContextMenu");
                Require(Native.GetMenuItemCount(nativeMenu) > 0,
                    "Shell handler did not insert any context-menu entries.");
                var label = new StringBuilder(260);
                Require(Native.GetMenuString(nativeMenu, 0, label,
                    label.Capacity, 0x400) > 0, "Shell context-menu root text is missing.");
            } finally {
                if (nativeMenu != IntPtr.Zero) Native.DestroyMenu(nativeMenu);
                if (dataPointer != IntPtr.Zero) Marshal.Release(dataPointer);
                Native.Release(instance);
            }
        }

        private static void CheckShellLookup(string path) {
            var data = new Native.SHFILEINFO();
            IntPtr got = Native.SHGetFileInfo(path, 0, ref data,
                (uint)Marshal.SizeOf(typeof(Native.SHFILEINFO)), 0x100);
            try {
                Require(got != IntPtr.Zero && data.hIcon != IntPtr.Zero,
                    "SHGetFileInfo could not retrieve an icon for the registered APK file.");
            } finally {
                if (data.hIcon != IntPtr.Zero) Native.DestroyIcon(data.hIcon);
            }
        }

        private static void AssertFileIsWritable(string path) {
            using (var stream = new FileStream(path, FileMode.Open,
                FileAccess.ReadWrite, FileShare.None)) {
                Require(stream.CanWrite, "Shell request left the file locked: " + path);
            }
        }
    }
}
