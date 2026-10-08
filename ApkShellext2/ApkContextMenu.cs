using ApkQuickReader;
using ApkShellext2.Properties;
using Microsoft.Win32;
using QRCoder;
using SharpShell.Attributes;
using SharpShell.Diagnostics;
using SharpShell.Extensions;
using SharpShell.ServerRegistration;
using SharpShell.SharpContextMenu;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Net;
using System.Web;
using System.Collections.Specialized;
using System.Globalization;
using System.Diagnostics;
using System.Xml;
using Microsoft.VisualBasic;

namespace ApkShellext2 {
    [Guid("dcb629fc-f86f-456f-8e24-98b9b2643a9b")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".apk")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".xapk")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".apks")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".apkm")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".ipa")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".appxbundle")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".appx")]
    public class ApkContextMenu : SharpContextMenu {
        /// <summary>
        /// Determines whether this instance can a shell context show menu, given the specified selected file list.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if this instance should show a shell context menu for the specified file list; otherwise, <c>false</c>.
        /// </returns>
        protected override bool CanShowMenu() {
            return SelectedItemPaths != null && SelectedItemPaths.Any();
        }

        /// <summary>
        /// Creates the context menu. This can be a single menu item or a tree of them.
        /// </summary>
        /// <returns>The context menu for the shell context menu.</returns>
        protected override ContextMenuStrip CreateMenu() {
            Utility.Localize();

            var menu = new ContextMenuStrip();

            string newVerAvai = "";
            if (Utility.NewVersionAvailible() && Utility.GetSetting("ShowNewVersion", "True") == "True")
                newVerAvai = " (" + string.Format(Resources.strNewVersionAvailible, Utility.GetSetting("LatestVersion")) + ")";
            var mainMenu = new ToolStripMenuItem {
                Text = Properties.Resources.menuMain + newVerAvai
            };

            int size = mainMenu.Height + 1;

            bool hasapk = false, hasipa = false, hasappx = false, hasappxbundle = false;
            foreach (var p in SelectedItemPaths) {
                if (p.EndsWith(AppPackageReader.extAPK, StringComparison.OrdinalIgnoreCase) ||
                    p.EndsWith(AppPackageReader.extXAPK, StringComparison.OrdinalIgnoreCase) ||
                    p.EndsWith(AppPackageReader.extAPKS, StringComparison.OrdinalIgnoreCase) ||
                    p.EndsWith(AppPackageReader.extAPKM, StringComparison.OrdinalIgnoreCase))
                    hasapk = true;
                if (p.EndsWith(AppPackageReader.extIPA, StringComparison.OrdinalIgnoreCase))
                    hasipa = true;
                if (p.EndsWith(AppPackageReader.extAPPX, StringComparison.OrdinalIgnoreCase))
                    hasappx = true;
                if (p.EndsWith(AppPackageReader.extAPPXBUNDLE, StringComparison.OrdinalIgnoreCase))
                    hasappxbundle = true;
            };

            bool singleSelected = SelectedItemPaths.Count() == 1;
            string appname = "";
            if (singleSelected) {
                try {
                    using (AppPackageReader reader = AppPackageReader.Read(SelectedItemPaths.ElementAt(0)))
                        appname = reader.AppName;
                } catch (Exception ex) {
                    // A broken download must not prevent Explorer from showing the menu.
                    Log("Cannot read package name for context menu: " + ex.Message);
                }
            }

            #region Rename Menu
            ToolStripMenuItem renameMenu = new ToolStripMenuItem() {
                Image = Utility.ResizeBitmap(Properties.NonLocalizeResources.iconRename, size)
            };

            if (singleSelected) {
                string newName = getNewFileName(SelectedItemPaths.ElementAt(0));
                if (newName != "") {
                    renameMenu.Text = string.Format(Resources.menuRenameAs, Path.GetFileName(newName));
                } else {
                    renameMenu.Text = Resources.strReadFileFailed;
                    renameMenu.Enabled = false;
                }
            } else {
                string renamePattern = Utility.GetSetting("RenamePattern", NonLocalizeResources.strRenamePatternDefault);
                renameMenu.Text = string.Format(Resources.menuRenameAs, renamePattern);
            }

            renameMenu.Click += (sender, args) => renameWithVersion();
            mainMenu.DropDownItems.Add(renameMenu);
            
            #endregion

            // Flat commands: do not require another submenu for two extract actions.
            if (hasapk) {
                var manifestMenu = new ToolStripMenuItem {
                    Text = Resources.menuDumpManifest,
                    Image = Utility.ResizeBitmap(NonLocalizeResources.output, size)
                };
                manifestMenu.Click += (sender, args) => dumpManifest();
                mainMenu.DropDownItems.Add(manifestMenu);

                var dumpOtherMenu = new ToolStripMenuItem {
                    Text = Resources.menuDumpOthers
                };
                dumpOtherMenu.Click += (sender, args) => dumpXML();
                mainMenu.DropDownItems.Add(dumpOtherMenu);
            }

            // The single-file details command is available for every supported package type.
            var detailsMenu = new ToolStripMenuItem {
                Text = Resources.menuMoreDetails,
                Enabled = singleSelected
            };
            detailsMenu.Click += (sender, args) => showDetails();
            mainMenu.DropDownItems.Add(detailsMenu);

            mainMenu.DropDownItems.Add("-");

            if (hasapk) {
                appname = singleSelected ? appname : AppPackageReader.extAPK;
                bool allowStore = singleSelected ||
                    Utility.GetSetting("ShowAppStoreWhenMultiSelected", "True") == "True";

                if (Utility.GetSetting("ShowGooglePlay", "True") == "True") {
                    var googleMenu = new ToolStripMenuItem {
                        Text = string.Format(Resources.menuGotoGooglePlay, appname),
                        Image = Utility.ResizeBitmap(NonLocalizeResources.iconGooglePlay, size),
                        Enabled = allowStore
                    };
                    googleMenu.Click += (sender, args) => gotoGooglePlay();
                    mainMenu.DropDownItems.Add(googleMenu);
                }

                if (Utility.GetSetting("ShowAmazonStore", "True") == "True") {
                    var amazonMenu = new ToolStripMenuItem {
                        Text = string.Format(Resources.menuGotoAmazonAppStore, appname),
                        Image = Utility.ResizeBitmap(NonLocalizeResources.iconAmazonStore, size),
                        Enabled = allowStore
                    };
                    amazonMenu.Click += (sender, args) => gotoAmazonAppStore();
                    mainMenu.DropDownItems.Add(amazonMenu);
                }

                if (Utility.GetSetting("ShowApkMirror", "False") == "True") {
                    var apkMirrorMenu = new ToolStripMenuItem {
                        Text = string.Format(Resources.menuGotoApkMirror, appname),
                        Image = Utility.ResizeBitmap(NonLocalizeResources.iconApkMirror, size),
                        Enabled = allowStore
                    };
                    apkMirrorMenu.Click += (sender, args) => gotoApkMirror();
                    mainMenu.DropDownItems.Add(apkMirrorMenu);
                }
            }
            if (hasipa && Utility.GetSetting("ShowAppleStore", "True") == "True") {
                #region AppleStore Menu
                appname = singleSelected ? appname : AppPackageReader.extIPA;
                var appleMenu = new ToolStripMenuItem {
                    Text = string.Format(Resources.menuGotoAppleAppStore, appname),
                    Image = Utility.ResizeBitmap(NonLocalizeResources.iconAppStore, size)
                };
                appleMenu.Click += (sender, args) => gotoAppleStore();
                mainMenu.DropDownItems.Add(appleMenu);
                appleMenu.Enabled = singleSelected ||
                Utility.GetSetting("ShowAppStoreWhenMultiSelected", "True") == "True";
                #endregion
            }

            if ((hasappx || hasappxbundle) && Utility.GetSetting("ShowMSStore", "True") == "True") {
                #region MS Store Menu
                appname = singleSelected ? appname :
                    (AppPackageReader.extAPPX + @"/" + AppPackageReader.extAPPXBUNDLE);
                var msMenu = new ToolStripMenuItem {
                    Text = string.Format(Resources.menuGotoMicrosoftStore, appname),
                    Image = Utility.ResizeBitmap(NonLocalizeResources.iconMSStore, size)
                };
                msMenu.Click += (sender, args) => gotoMicrosoftStore();
                mainMenu.DropDownItems.Add(msMenu);
                msMenu.Enabled = singleSelected ||
                Utility.GetSetting("ShowAppStoreWhenMultiSelected", "True") == "True";
                #endregion
            }

            #region Barcode
            /* Not enabled yet
            var barcodeMenu = new ToolStripMenuItem();
            barcodeMenu.Text = Resources.strScanToDownload;
            barcodeMenu.Image = Utility.ResizeBitmap(Resources.QR_Scan, size);
            barcodeMenu.Enabled = false;
            if (SelectedItemPaths.Count() == 1) {
                barcodeMenu.Enabled = true;
                var barcode = new ToolStripMenuItem();

                string md5;
                using (MD5 md5Hash = MD5.Create()) {
                    md5 = Utility.GetMd5Hash(md5Hash, SelectedItemPaths.First());
                }

                try {
                    // Notice service regarding the path
                    using (var client = new WebClient()) {
                        var values = new NameValueCollection();
                        client.QueryString.Add("md5", md5);
                        client.QueryString.Add("path", SelectedItemPaths.First());
                        client.BaseAddress = "http://localhost:42728";
                        client.UploadValues("", values);
                    }

                    string url = @"http://" + Utility.LocalIPAddress() + @":42728/" + md5;
                    QRCodeGenerator qrGenerator = new QRCodeGenerator();
                    QRCodeGenerator.QRCode qrCode = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
                    barcode.Image = qrCode.GetGraphic(5);

                    barcodeMenu.DropDownItems.Add(barcode);
                    barcode.Click += (sender,args) => gotoDownload();
                } catch (Exception ex) {
                    Log(ex.Message);
                    barcodeMenu.Enabled = false;
                }
            }
            mainMenu.DropDownItems.Add(barcodeMenu);
            */
            #endregion


            #region Preferences Menu
            var settingsMenu = new ToolStripMenuItem {
                Text = Resources.menuPreferences,
                Image = Utility.ResizeBitmap(Properties.NonLocalizeResources.logo, size)
            };
            if (mainMenu.DropDownItems.Count > 1) {
                mainMenu.DropDownItems.Add("-");
            }
            settingsMenu.Click += (sender, args) => showSettings();
            mainMenu.DropDownItems.Add(settingsMenu);
            #endregion

            #region Mainmenu Icon
            if (Utility.GetSetting("ShowMenuIcon","True")=="True") {
                // if choose multiple files, will create a icon with upto 3 icons together
                try { // draw multiple icons
                    int totalIconToDraw = (SelectedItemPaths.Count() > 3) ? 3 : SelectedItemPaths.Count();
                    Bitmap b = new Bitmap(size, size);
                    for (int i = 0; i < totalIconToDraw; i++) {
                        using (AppPackageReader reader = AppPackageReader.Read(SelectedItemPaths.ElementAt(totalIconToDraw - i - 1)))
                        using (Graphics g = Graphics.FromImage((Image)b)) {
                            reader.setFlag("ImageSize", size);
                            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                            g.DrawImage(reader.Icon, i * 3, i * 3, size - 2 * totalIconToDraw, size - 2 * totalIconToDraw);
                        }
                    }
                    mainMenu.Image = b;
                } catch (Exception ex) {
                    mainMenu.Image = Utility.ResizeBitmap(Properties.NonLocalizeResources.logo, size);
                    Log("Error happens during extracting icon " + ex.Message);
                }
            } else {
                mainMenu.Image = Utility.ResizeBitmap(Properties.NonLocalizeResources.logo, size);
            }
            #endregion

            menu.Items.Add(mainMenu);

            Utility.CheckUpdate();
            return menu;
        }

        //private void gotoDownload() {
        //    string md5;
        //    using (MD5 md5Hash = MD5.Create()) {
        //        md5 = Utility.GetMd5Hash(md5Hash, SelectedItemPaths.First());
        //    }
        //    System.Diagnostics.Process.Start(@"http://" + Utility.LocalIPAddress() + @":42728/" + md5);
        //}

        /// <summary>
        /// get the new filename for renaming function
        /// </summary>
        /// <returns></returns>
        private string getNewFileName(string path) {
            //bool key_RenameWithVersionCode = Settings.DefaultRenameWithVersionCode) == 1);
            string suffix = Path.GetExtension(path);
            string newFileName = "";
            string renamePattern = Utility.GetSetting("RenamePattern", NonLocalizeResources.strRenamePatternDefault);
            try {
                using (AppPackageReader reader = AppPackageReader.Read(path)) {                    
                    newFileName = ReplaceVariables(renamePattern,reader);
                }

                newFileName = Regex.Replace(newFileName, @"[\/:*?""<>|-]+", ""); // remove invalid chars
                if (Utility.GetSetting("ReplaceSpace") == "True") {
                    string replacement = Utility.GetSetting("ReplaceSpaceChar", "_");
                    newFileName = Regex.Replace(newFileName, @"\s+", match => replacement);
                }
                string oldFileName = Path.GetFileName(path);
                string folderPath = Path.GetDirectoryName(path);
                string tmpFileName = newFileName + suffix;

                int i = 0;
                while (File.Exists(folderPath + @"\" + tmpFileName) &&
                    (tmpFileName != oldFileName)) {
                    i++;
                    tmpFileName = newFileName + "_(" + i.ToString() + ")" + suffix;
                }
                newFileName = folderPath + @"\" + tmpFileName;
            } catch {
                newFileName = "";
            }
            return newFileName;
        }

        private void dumpXML() {
            string xml = Interaction.InputBox(Resources.strDumpXMLPrompt, Resources.strDumpXML);
            if (string.IsNullOrWhiteSpace(xml))
                return;
            foreach (var path in SelectedItemPaths.Where(IsAndroidPackage)) {
                dumpXML(path, xml);
            }
        }

        private void dumpXML(string apk, string xml) {
            try {
                using (ApkReader reader = (ApkReader)AppPackageReader.Read(apk)) {
                    XmlDocument doc = reader.ExtractCompressedXml(xml);
                    string filepath = apk + "." + Regex.Replace(xml, @"[\/:*?""<>|-]+", "_"); // remove invalid chars
                    XmlWriterSettings setting = new XmlWriterSettings();
                    setting.Indent = true;
                    XmlWriter writer = XmlWriter.Create(filepath,setting);
                    doc.WriteTo(writer);
                    writer.Close();
                }
            } catch (Exception ex) {
                Log(ex.Message + "\nError happens during extracting " + xml + " in " + apk);
            }
        }

        private void dumpManifest() {
            foreach (var path in SelectedItemPaths.Where(IsAndroidPackage))
                dumpXML(path, "androidmanifest.xml");
        }

        public static string ReplaceVariables(string ori, AppPackageReader reader) {
            string[] tokens = ori.Split(new char[] { '%' });
            string newstr = "";
            bool inpattern = false;
            foreach(string t in tokens) {
                if (!inpattern) {
                    newstr = newstr + t;
                } else {
                    string replacement = "";
                    if (t == NonLocalizeResources.varAppName)
                        replacement = reader.AppName;
                    else if (t == NonLocalizeResources.varPackage)
                        replacement = reader.PackageName;
                    else if (t == NonLocalizeResources.varPublisher)
                        replacement = reader.Publisher;
                    else if (t == NonLocalizeResources.varVersion)
                        replacement = reader.Version;
                    else if (t == NonLocalizeResources.varRevision)
                        replacement = reader.Revision;
                    else if (t == NonLocalizeResources.varFileSize)
                        replacement = Utility.getFileSize(reader.FileName);
                    else if (t == NonLocalizeResources.varOS)
                        replacement = (reader.Type == AppPackageReader.AppType.AndroidApp ? "Android"
                            : (reader.Type == AppPackageReader.AppType.iOSApp ? "iOS"
                            : "Windows"));
                    else if (t == NonLocalizeResources.varLastModify)
                        replacement = File.GetLastWriteTime(reader.FileName).ToString("dd/MM/yy HH:mm:ss");
                    else if (t == NonLocalizeResources.varDebuggable)
                        replacement = (reader.Type == AppPackageReader.AppType.AndroidApp) ?
                            (((ApkReader)reader).Debuggable ? "Debug" : "Release") : "";
                    else if (reader.Type == AppPackageReader.AppType.AndroidApp) {
                        try {
                            var apkreader = reader as ApkReader;
                            string regstr = NonLocalizeResources.varAttribute;
                            Regex rx = new Regex(regstr);
                            Match m = rx.Match(t);
                            if (m.Success) {
                                string tag = m.Groups[1].Value;
                                string attr = m.Groups[2].Value;
                                replacement = apkreader.QuickSearchManifestXml(tag, attr);
                            }
                        } catch (Exception ex) {
                            Utility.Log(null, "Replace variables", ex.Message + "\nError happens during replacing " + NonLocalizeResources.varAttribute);
                        }
                    }
                    newstr = newstr + replacement;
                }
                inpattern = !inpattern;
            }
            if (inpattern) {
                Utility.Log(null, "Replace variable", "% is not in pair.");
            }
            return newstr;
        }

        private void renameWithVersion() {
            foreach (var path in SelectedItemPaths) {
                try {
                    string newFilename = getNewFileName(path);
                    File.Move(path, newFilename);
                } catch (Exception e) {
                    Log("Exception happens during rename: " + e.Message);
                }
            }
        }

        private static bool IsAndroidPackage(string path) {
            string extension = Path.GetExtension(path);
            return extension.Equals(AppPackageReader.extAPK, StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(AppPackageReader.extXAPK, StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(AppPackageReader.extAPKS, StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(AppPackageReader.extAPKM, StringComparison.OrdinalIgnoreCase);
        }

        private void openAndroidStore(string urlTemplate) {
            foreach (var path in SelectedItemPaths.Where(IsAndroidPackage)) {
                try {
                    using (AppPackageReader reader = AppPackageReader.Read(path))
                        Process.Start(string.Format(urlTemplate, reader.PackageName));
                } catch (Exception ex) {
                    Log("Cannot open store for " + Path.GetFileName(path) + ": " + ex.Message);
                }
            }
        }

        private void gotoGooglePlay() {
            openAndroidStore(NonLocalizeResources.urlGooglePlay);
        }

        private void gotoAmazonAppStore() {
            openAndroidStore(NonLocalizeResources.urlAmazonAppStore);
        }

        private void gotoApkMirror() {
            openAndroidStore(NonLocalizeResources.urlApkMirror);
        }

        private void gotoAppleStore() {
            foreach (var p in SelectedItemPaths) {
                if (!p.EndsWith(AppPackageReader.extIPA, StringComparison.OrdinalIgnoreCase))
                    continue;
                try {
                    using (IpaReader reader = (IpaReader)AppPackageReader.Read(p)) {
                        string appId = reader.AppID;
                        if (!string.IsNullOrWhiteSpace(appId))
                            Process.Start(string.Format(NonLocalizeResources.urlAppleStore, appId));
                    }
                } catch (Exception ex) {
                    Log("Cannot open Apple App Store for " + Path.GetFileName(p) + ": " + ex.Message);
                }
            }
        }

        private void gotoMicrosoftStore() {
            foreach (var p in SelectedItemPaths) {
                if (!p.EndsWith(AppPackageReader.extAPPX, StringComparison.OrdinalIgnoreCase) &&
                    !p.EndsWith(AppPackageReader.extAPPXBUNDLE, StringComparison.OrdinalIgnoreCase))
                    continue;
                try {
                    using (AppPackageReader reader = AppPackageReader.Read(p))
                        Process.Start(string.Format(NonLocalizeResources.urlMicrosoftStore, reader.AppID));
                } catch (Exception ex) {
                    Log("Cannot open Microsoft Store for " + Path.GetFileName(p) + ": " + ex.Message);
                }
            }
        }

        private void showDetails() {
            string[] paths = SelectedItemPaths.ToArray();
            if (paths.Length != 1)
                return;

            try {
                using (var dialog = new AppDetailsDialog(paths[0]))
                    dialog.ShowDialog();
            } catch (Exception ex) {
                Log("Cannot show package details: " + ex.Message);
                MessageBox.Show(Resources.strReadFileFailed, Resources.detailsTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void showSettings() {
            using (var settings = new Preferences()) {
                if (SelectedItemPaths.Any())
                    settings.currentFile = SelectedItemPaths.First();
                settings.ShowDialog();
            }
        }

        [CustomRegisterFunction]
        public static void postDoRegister(Type type, RegistrationType registrationType) {
            Console.WriteLine("Registering " + type.FullName);

            #region Clean up older versions registry
            try {
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(@"\CLSID\" +
                    type.GUID.ToRegistryString() + @"\InprocServer32")) {
                    if (key != null && key.GetSubKeyNames().Count() != 0) {
                        Console.WriteLine("Found old version in registry, cleaning up ...");
                        foreach (var k in key.GetSubKeyNames()) {
                            if (k != type.Assembly.GetName().Version.ToString()) {
                                Registry.ClassesRoot.DeleteSubKeyTree(@"\CLSID\" +
                        type.GUID.ToRegistryString() + @"\InprocServer32\" + k);
                            }
                        }
                    }
                }
            } catch (Exception e) {
                Logging.Error("Cleaning up older version but see exception. "
                     + e.Message);
            }
            #endregion
        }

        protected override void Log(string message) {
            string s = (SelectedItemPaths.Count() >0) ?Path.GetFileName(SelectedItemPaths.ElementAt(0)): "";
            Utility.Log(this, s, message);
        }
    }
}
