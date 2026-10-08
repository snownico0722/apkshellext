using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
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
                    {
                        key.SetValue("LastCheckUpdateTime", DateTime.Today.ToString());
                        key.SetValue("EnableThumbnail", "True");
                        key.SetValue("StretchThumbnail", "True");
                        key.SetValue("ShowOverlayIcon", "False");
                        key.SetValue("ShowOverLayIcon", "False");
                        key.SetValue("ShowIpaIcon", "True");
                        key.SetValue("ShowAppxIcon", "True");
                    }

                switch (suite) {
                    case "registration": CheckComActivation(); break;
                    case "valid": CheckValidPackages(workspace); break;
                    case "invalid": CheckInvalidPackages(workspace); break;
                    case "stress": CheckRepeatedRequests(workspace); break;
                    case "downloading": CheckDownloadInProgress(workspace); break;
                    case "settings": CheckSettingsAndMenuActions(workspace); break;
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
                "sample.apkm", "sample.ipa", "sample.appx", "desktop.appx", "sample.appxbundle"
            };
            foreach (string filename in names) {
                string path = ShellFixtures.PathFor(dir, filename);
                Require(File.Exists(path), "Missing fixture: " + path);
                bool android = filename.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) ||
                    filename.EndsWith(".xapk", StringComparison.OrdinalIgnoreCase) ||
                    filename.EndsWith(".apks", StringComparison.OrdinalIgnoreCase) ||
                    filename.EndsWith(".apkm", StringComparison.OrdinalIgnoreCase);

                string expectedName = android ? ShellFixtures.AppName :
                    filename.EndsWith(".ipa", StringComparison.OrdinalIgnoreCase)
                        ? "Shell iOS Test" : "Shell Windows Test";
                CheckInfoTip(path, expectedName);
                CheckExtractIcon(path, true);
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

        private static void CheckDownloadInProgress(string dir) {
            byte[] full = File.ReadAllBytes(ShellFixtures.PathFor(dir, "sample.apk"));
            string path = ShellFixtures.PathFor(dir, "downloading.apk");
            int[] stages = {
                0, 1, 8, 32, 64, 128, full.Length / 2, full.Length - 1
            };
            using (var writer = new FileStream(path, FileMode.Create,
                FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete)) {
                foreach (int size in stages) {
                    writer.SetLength(0);
                    writer.Write(full, 0, size);
                    writer.Flush(true);
                    // The shell may inspect the same file while a browser or
                    // download manager is still writing it.
                    CheckExtractIcon(path, false);
                    CheckInfoTip(path, null);
                    CheckContextMenu(new[] { path });
                    CheckThumbnail(path, false);
                }
                writer.SetLength(0);
                writer.Write(full, 0, full.Length);
                writer.Flush(true);
            }
            CheckExtractIcon(path, true);
            CheckInfoTip(path, ShellFixtures.AppName);
            CheckThumbnail(path, true);
            AssertFileIsWritable(path);
            File.Delete(path);
            Console.WriteLine("PASS: inspected APK during 8 partial-download stages and after completion");
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
                object[] location = { 0u, new StringBuilder(260), 260, 0, 0u };
                Native.Check(Native.Invoke<IExtractIconW>(instance, "GetIconLocation", location),
                    "IExtractIconW.GetIconLocation");
                int index = Convert.ToInt32(location[3]);
                uint flags = Convert.ToUInt32(location[4]);
                Require((flags & 0x0008) != 0, "Icon extraction should use an HICON, not a file index.");

                object[] extracted = { "", (uint)index, IntPtr.Zero, IntPtr.Zero,
                    (uint)(48 | (16 << 16)) };
                int hr = Native.Invoke<IExtractIconW>(instance, "Extract", extracted);
                large = (IntPtr)extracted[2];
                small = (IntPtr)extracted[3];
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
                object[] tip = { 0u, null };
                Native.Check(Native.Invoke<IQueryInfo>(instance, "GetInfoTip", tip),
                    "IQueryInfo.GetInfoTip");
                string text = tip[1] as string;
                Require(!string.IsNullOrWhiteSpace(text),
                    "No tooltip text was returned: " + Path.GetFileName(path));
                if (expectedName != null)
                    Require(text.Contains(expectedName), "Tooltip lost the APK app name: " + text);
                object[] flags = { 0 };
                Native.Check(Native.Invoke<IQueryInfo>(instance, "GetInfoFlags", flags),
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
                Native.Check(Native.Invoke<IInitializeWithStream>(instance, "Initialize",
                    new object[] { input, 0u }), "IInitializeWithStream.Initialize");
                object[] thumb = { 64u, IntPtr.Zero, 0 };
                int hr = Native.Invoke<IThumbnailProvider>(instance, "GetThumbnail", thumb);
                bitmapHandle = (IntPtr)thumb[1];
                int alpha = Convert.ToInt32(thumb[2]);
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

        private static void CheckSettingsAndMenuActions(string dir) {
            string path = ShellFixtures.PathFor(dir, "sample.apk");
            const string settingsKey = @"SOFTWARE\ApkShellext2";
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(settingsKey)) {
                string oldThumbnail = (string)key.GetValue("EnableThumbnail", null);
                string oldPattern = (string)key.GetValue("RenamePattern", null);
                string oldReplace = (string)key.GetValue("ReplaceSpace", null);
                string oldReplaceChar = (string)key.GetValue("ReplaceSpaceChar", null);
                string oldInfoTip = (string)key.GetValue("ToolTipPattern", null);
                try {
                    // Fresh installs must behave like the settings checkbox,
                    // which defaults to True, without the value ever being saved.
                    key.DeleteValue("EnableThumbnail", false);
                    CheckThumbnail(path, true);
                    key.SetValue("EnableThumbnail", "False");
                    CheckThumbnail(path, false);
                    key.SetValue("EnableThumbnail", "True");

                    key.SetValue("ReplaceSpace", "True");
                    key.SetValue("ReplaceSpaceChar", "--");
                    key.SetValue("RenamePattern", "%AppName%");
                    object menu = Native.Create(Native.Context);
                    Assembly shellAssembly = menu.GetType().Assembly;
                    IntPtr dataPointer = IntPtr.Zero;
                    try {
                        var data = new DataObject();
                        var paths = new StringCollection();
                        paths.Add(path);
                        data.SetFileDropList(paths);
                        dataPointer = Marshal.GetComInterfaceForObject(data,
                            typeof(System.Runtime.InteropServices.ComTypes.IDataObject));
                        Native.Invoke<IShellExtInit>(menu, "Initialize",
                            new object[] { IntPtr.Zero, dataPointer, IntPtr.Zero });
                        Type type = menu.GetType();
                        MethodInfo name = type.GetMethod("getNewFileName",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        string suggested = (string)name.Invoke(menu, new object[] { path });
                        Require(Path.GetFileName(suggested) == "Shell--Integration--Test.apk",
                            "Rename ignored the custom whitespace replacement: " + suggested);
                        // The previous exception handler started after opening
                        // the broken APK, letting the menu command crash.
                        MethodInfo dump = type.GetMethod("dumpXML",
                            BindingFlags.NonPublic | BindingFlags.Instance, null,
                            new[] { typeof(string), typeof(string) }, null);
                        dump.Invoke(menu, new object[] {
                            ShellFixtures.PathFor(dir, "broken.apk"), "AndroidManifest.xml"
                        });
                        // Context commands must be flat, including both extract options
                        // and the store links, with one single-file details entry.
                        MethodInfo createMenu = type.GetMethod("CreateMenu",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        using (var strip = (ContextMenuStrip)createMenu.Invoke(menu, null)) {
                            Require(strip.Items.Count == 1, "Expected one extension root menu");
                            var root = strip.Items[0] as ToolStripMenuItem;
                            Require(root != null, "Context menu root must be a menu item");
                            Type menuResources = shellAssembly.GetType(
                                "ApkShellext2.Properties.Resources", true);
                            string xmlText = (string)menuResources.GetProperty("menuDumpOthers",
                                BindingFlags.Static | BindingFlags.NonPublic).GetValue(null, null);
                            string detailsText = (string)menuResources.GetProperty("menuMoreDetails",
                                BindingFlags.Static | BindingFlags.NonPublic).GetValue(null, null);
                            int xmlIndex = -1, detailsIndex = -1, separatorIndex = -1;
                            for (int i = 0; i < root.DropDownItems.Count; i++) {
                                ToolStripItem item = root.DropDownItems[i];
                                if (item is ToolStripSeparator && separatorIndex < 0)
                                    separatorIndex = i;
                                var command = item as ToolStripMenuItem;
                                if (command == null) continue;
                                Require(command.DropDownItems.Count == 0,
                                    "Context menu has an unexpected nested submenu: " + command.Text);
                                if (command.Text == xmlText) xmlIndex = i;
                                if (command.Text == detailsText) {
                                    Require(command.Enabled, "Single-file details action should be enabled");
                                    detailsIndex = i;
                                }
                            }
                            Require(xmlIndex >= 0 && detailsIndex == xmlIndex + 1 &&
                                detailsIndex < separatorIndex,
                                "Details must appear immediately below Extract XML in the first menu group");
                        }
                    } finally {
                        if (dataPointer != IntPtr.Zero) Marshal.Release(dataPointer);
                        Native.Release(menu);
                    }

                    string savedLanguage = (string)key.GetValue("Language", null);
                    var originalCulture = Thread.CurrentThread.CurrentCulture;
                    var originalUICulture = Thread.CurrentThread.CurrentUICulture;
                    try {
                        Type utility = shellAssembly.GetType("ApkShellext2.Utility", true);
                        MethodInfo localize = utility.GetMethod("Localize");
                        Type resourceType = shellAssembly.GetType(
                            "ApkShellext2.Properties.Resources", true);
                        PropertyInfo menuLabel = resourceType.GetProperty("menuMain",
                            BindingFlags.Static | BindingFlags.NonPublic);
                        Require(menuLabel != null, "Missing translated menu label");

                        key.SetValue("Language", "zh-CN");
                        localize.Invoke(null, null);
                        Require(Thread.CurrentThread.CurrentUICulture.Name == "zh-CN" &&
                            (string)menuLabel.GetValue(null, null) == "APK文件助手",
                            "The installed COM extension did not use embedded Chinese UI");
                        PropertyInfo chineseDetailLabel = resourceType.GetProperty("menuMoreDetails",
                            BindingFlags.Static | BindingFlags.NonPublic);
                        Require(chineseDetailLabel != null &&
                            (string)chineseDetailLabel.GetValue(null, null) == "查看更多信息",
                            "The Chinese details action label is incorrect");

                        key.SetValue("Language", "en-US");
                        localize.Invoke(null, null);
                        Require(Thread.CurrentThread.CurrentUICulture.Name == "en-US" &&
                            (string)menuLabel.GetValue(null, null) == "APK Shell Extension",
                            "The installed COM extension could not return to English UI");
                        PropertyInfo detailLabel = resourceType.GetProperty("menuMoreDetails",
                            BindingFlags.Static | BindingFlags.NonPublic);
                        Require(detailLabel != null &&
                            (string)detailLabel.GetValue(null, null) == "View more information",
                            "The details action has an unexpected English label");
                    } finally {
                        RestoreValue(key, "Language", savedLanguage);
                        Thread.CurrentThread.CurrentCulture = originalCulture;
                        Thread.CurrentThread.CurrentUICulture = originalUICulture;
                    }

                    Type detailsType = shellAssembly.GetType("ApkShellext2.AppDetailsDialog", true);
                    MethodInfo readDetails = detailsType.GetMethod("ReadDetails",
                        BindingFlags.NonPublic | BindingFlags.Static);
                    var rows = (IEnumerable<KeyValuePair<string, string>>)readDetails.Invoke(
                        null, new object[] { path });
                    bool hasPackage = false;
                    foreach (var row in rows)
                        if (row.Value == ShellFixtures.PackageName) hasPackage = true;
                    Require(hasPackage, "Details did not include the Android package name");
                    foreach (var row in rows) {
                        Require(row.Key != "Publisher" && row.Key != "发布者",
                            "Android package namespace must not be reported as a verified publisher");
                    }

                    var brokenRows = (IEnumerable<KeyValuePair<string, string>>)readDetails.Invoke(
                        null, new object[] { ShellFixtures.PathFor(dir, "broken.apk") });
                    bool hasError = false;
                    foreach (var row in brokenRows)
                        if (row.Value.Contains("End of Central Directory") ||
                            row.Key == "Failed to read package metadata" ||
                            row.Key == "包内信息读取失败") hasError = true;
                    Require(hasError, "Damaged APK details must report the read failure");

                    Type preferencesType = shellAssembly.GetType("ApkShellext2.Preferences", true);
                    MeasureSettingsStartup(preferencesType);
                    using (Form preferences = (Form)Activator.CreateInstance(preferencesType)) {
                        Require(!ContainsControl(preferences, typeof(TreeView)) &&
                            !ContainsControl(preferences, typeof(LinkLabel)),
                            "Settings still contains tree or wiki/translation navigation");
                        Require(ContainsScrollingPanel(preferences),
                            "Unified settings page must scroll");

                        // Controls must already be populated before the form first becomes visible.
                        FieldInfo field = preferencesType.GetField("txtRenamePattern",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        TextBox textbox = (TextBox)field.GetValue(preferences);
                        Require(textbox.Text == "%AppName%" &&
                            (string)key.GetValue("RenamePattern", "") == "%AppName%",
                            "Settings were not initialized before the dialog was shown");
                        textbox.Text = "custom";
                        Require((string)key.GetValue("RenamePattern", "") == "custom",
                            "Rename pattern did not auto-save");
                        preferencesType.GetMethod("btnResetRenamePattern_Click",
                            BindingFlags.NonPublic | BindingFlags.Instance).Invoke(
                                preferences, new object[] { preferences, EventArgs.Empty });
                        Require(textbox.Text == "%AppName%_%Version%" &&
                            (string)key.GetValue("RenamePattern", "") == textbox.Text,
                            "Reset rename pattern did not persist its default");

                        var toolTip = (TextBox)preferencesType.GetField("txtToolTipPattern",
                            BindingFlags.NonPublic | BindingFlags.Instance).GetValue(preferences);
                        toolTip.Text = "Version: %Version%";
                        Require((string)key.GetValue("ToolTipPattern", "") == toolTip.Text,
                            "Info tip pattern did not auto-save");

                        var replacement = (TextBox)preferencesType.GetField("txtReplaceWhiteSpace",
                            BindingFlags.NonPublic | BindingFlags.Instance).GetValue(preferences);
                        replacement.Text = "__";
                        Require((string)key.GetValue("ReplaceSpaceChar", "") == "__",
                            "Whitespace replacement did not auto-save");
                    }

                    CheckInvalidIpaStoreCommand(dir);
                } finally {
                    RestoreValue(key, "EnableThumbnail", oldThumbnail);
                    RestoreValue(key, "RenamePattern", oldPattern);
                    RestoreValue(key, "ReplaceSpace", oldReplace);
                    RestoreValue(key, "ReplaceSpaceChar", oldReplaceChar);
                    RestoreValue(key, "ToolTipPattern", oldInfoTip);
                }
            }
            Console.WriteLine("PASS: embedded Chinese/English COM UI, thumbnail defaults, rename settings and broken-APK menu command");
        }

        // Observational timing only; no machine-dependent millisecond assertion.
        // The Show/DoEvents segment covers handle creation, layout and first paint.
        private static void MeasureSettingsStartup(Type preferencesType) {
            for (int i = 0; i < 3; i++) {
                var stopwatch = Stopwatch.StartNew();
                using (Form form = (Form)Activator.CreateInstance(preferencesType)) {
                    long constructorMs = stopwatch.ElapsedMilliseconds;
                    bool shown = false;
                    form.Shown += (sender, args) => shown = true;
                    form.Show();
                    Application.DoEvents();
                    Require(shown && form.IsHandleCreated,
                        "Settings form did not reach the first Shown event");
                    Console.WriteLine("METRIC settings-open sample=" + i +
                        " constructor_ms=" + constructorMs +
                        " show_paint_ms=" + (stopwatch.ElapsedMilliseconds - constructorMs));
                    form.Close();
                }
            }
        }

        // Opening the store for a damaged IPA must not throw out of the Explorer
        // context-menu callback. Previously the IpaReader constructor was outside try.
        private static void CheckInvalidIpaStoreCommand(string dir) {
            object menu = Native.Create(Native.Context);
            IntPtr dataPointer = IntPtr.Zero;
            try {
                var data = new DataObject();
                var paths = new StringCollection();
                paths.Add(ShellFixtures.PathFor(dir, "invalid.ipa"));
                data.SetFileDropList(paths);
                dataPointer = Marshal.GetComInterfaceForObject(data,
                    typeof(System.Runtime.InteropServices.ComTypes.IDataObject));
                Native.Invoke<IShellExtInit>(menu, "Initialize",
                    new object[] { IntPtr.Zero, dataPointer, IntPtr.Zero });
                MethodInfo apple = menu.GetType().GetMethod("gotoAppleStore",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Require(apple != null, "Apple Store command no longer exists");
                apple.Invoke(menu, null);
            } finally {
                if (dataPointer != IntPtr.Zero) Marshal.Release(dataPointer);
                Native.Release(menu);
            }
        }

        private static void RestoreValue(RegistryKey key, string name, string original) {
            if (original == null)
                key.DeleteValue(name, false);
            else
                key.SetValue(name, original);
        }

        private static bool ContainsControl(Control parent, Type kind) {
            foreach (Control child in parent.Controls) {
                if (kind.IsInstanceOfType(child) || ContainsControl(child, kind))
                    return true;
            }
            return false;
        }

        private static bool ContainsScrollingPanel(Control parent) {
            foreach (Control child in parent.Controls) {
                var panel = child as Panel;
                if (panel != null && panel.AutoScroll) return true;
                if (ContainsScrollingPanel(child)) return true;
            }
            return false;
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
                Native.Invoke<IShellExtInit>(instance, "Initialize",
                    new object[] { IntPtr.Zero, dataPointer, IntPtr.Zero });

                nativeMenu = Native.CreatePopupMenu();
                Require(nativeMenu != IntPtr.Zero, "CreatePopupMenu failed.");
                int result = Native.Invoke<IContextMenu>(instance, "QueryContextMenu",
                    new object[] { nativeMenu, 0u, 1, 0x7FFF, 0u });
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
