using ApkQuickReader;
using ApkShellext2.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ApkShellext2 {
    // Opened on demand from the context menu, never during Explorer menu creation.
    internal sealed class AppDetailsDialog : Form {
        private readonly ListView detailsList;
        private readonly List<KeyValuePair<string, string>> details;

        internal AppDetailsDialog(string path) {
            Utility.Localize();
            Text = Resources.detailsTitle;
            ClientSize = new System.Drawing.Size(660, 420);
            MinimumSize = new System.Drawing.Size(460, 290);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            ShowIcon = false;
            ShowInTaskbar = false;

            details = ReadDetails(path);
            detailsList = new ListView {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HideSelection = false,
                MultiSelect = false
            };
            detailsList.Columns.Add(Resources.detailsField, 155);
            detailsList.Columns.Add(Resources.detailsValue, 470);
            foreach (var pair in details) {
                detailsList.Items.Add(new ListViewItem(new[] { pair.Key, pair.Value }));
            }
            detailsList.Resize += (sender, args) =>
                detailsList.Columns[1].Width = Math.Max(120,
                    detailsList.ClientSize.Width - detailsList.Columns[0].Width - 6);

            var copy = new Button { Text = Resources.detailsCopyAll, AutoSize = true };
            copy.Click += (sender, args) => {
                var text = new StringBuilder();
                foreach (var pair in details)
                    text.Append(pair.Key).Append(": ").AppendLine(pair.Value);
                if (text.Length > 0)
                    Clipboard.SetText(text.ToString());
            };
            var close = new Button { Text = Resources.detailsClose, AutoSize = true, DialogResult = DialogResult.OK };
            AcceptButton = close;
            CancelButton = close;

            var footer = new FlowLayoutPanel {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(10, 5, 10, 4)
            };
            footer.Controls.Add(close);
            footer.Controls.Add(copy);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            root.Controls.Add(detailsList, 0, 0);
            root.Controls.Add(footer, 0, 1);
            Controls.Add(root);
        }

        // Metadata extraction is separate so tests can exercise valid and damaged packages
        // without opening an interactive window inside the Explorer COM host.
        internal static List<KeyValuePair<string, string>> ReadDetails(string path) {
            var rows = new List<KeyValuePair<string, string>>();
            Add(rows, Resources.detailsFileName, Path.GetFileName(path));
            Add(rows, Resources.detailsFilePath, path);
            Add(rows, Resources.detailsFormat, Path.GetExtension(path).TrimStart('.').ToUpperInvariant());

            try {
                var file = new FileInfo(path);
                if (file.Exists) {
                    Add(rows, Resources.detailsFileSize, Utility.getFileSize(path));
                    Add(rows, Resources.detailsModified, file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
                }
                int metadataStart = rows.Count;
                using (AppPackageReader reader = AppPackageReader.Read(path)) {
                    TryAdd(rows, Resources.detailsAppName, () => reader.AppName);
                    TryAdd(rows, Resources.detailsPackageName, () => reader.PackageName);
                    TryAdd(rows, Resources.detailsVersion, () => reader.Version);
                    TryAdd(rows, Resources.detailsRevision, () => reader.Revision);
                    // Android's reader derives Publisher from the package namespace, not
                    // the app's verified developer or certificate. Do not mislabel that as a publisher.
                    if (reader.Type != AppPackageReader.AppType.AndroidApp)
                        TryAdd(rows, Resources.detailsPublisher, () => reader.Publisher);
                    TryAdd(rows, Resources.detailsAppId, () => reader.AppID);

                    var android = reader as ApkReader;
                    if (android != null) {
                        TryAdd(rows, Resources.detailsMinSdk,
                            () => android.getAttribute("manifest/uses-sdk", "minSdkVersion"));
                        TryAdd(rows, Resources.detailsTargetSdk,
                            () => android.getAttribute("manifest/uses-sdk", "targetSdkVersion"));
                        TryAdd(rows, Resources.detailsDebuggable,
                            () => android.getAttribute("manifest/application", "debuggable"));
                    }
                }
                if (rows.Count == metadataStart)
                    Add(rows, Resources.detailsReadError, Resources.strReadFileFailed);
            } catch (Exception ex) {
                Add(rows, Resources.detailsReadError, ex.Message);
            }
            return rows;
        }

        private static void Add(List<KeyValuePair<string, string>> rows, string key, string value) {
            if (!string.IsNullOrWhiteSpace(value))
                rows.Add(new KeyValuePair<string, string>(key, value));
        }

        private static void TryAdd(List<KeyValuePair<string, string>> rows, string key, Func<string> value) {
            try {
                Add(rows, key, value());
            } catch {
                // A missing optional field must not hide other successfully parsed fields.
            }
        }
    }
}
