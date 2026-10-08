namespace ApkShellext2 {
    partial class Preferences {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing) {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.GroupBox grpGeneral;
        private System.Windows.Forms.GroupBox grpIcon;
        private System.Windows.Forms.GroupBox grpContextMenu;
        private System.Windows.Forms.GroupBox grpRenaming;
        private System.Windows.Forms.GroupBox grpInfoTip;
        private System.Windows.Forms.Label lblLanguage;
        private System.Windows.Forms.Label lblCurrentVersion;
        private System.Windows.Forms.Label lblNewVer;
        private System.Windows.Forms.Label lblRenamePattern;
        private System.Windows.Forms.Label lblInfoTipPattern;
        private System.Windows.Forms.Label lblPatternVariablesHint;
        private System.Windows.Forms.Label lblInfoTipVariablesHint;
        private System.Windows.Forms.ComboBox combLanguage;
        private System.Windows.Forms.CheckBox ckShowIPA;
        private System.Windows.Forms.CheckBox ckShowAppxIcon;
        private System.Windows.Forms.CheckBox ckShowOverlay;
        private System.Windows.Forms.CheckBox ckEnableThumbnail;
        private System.Windows.Forms.CheckBox ckStretchThumbnail;
        private System.Windows.Forms.CheckBox ckAdaptiveIconSupport;
        private System.Windows.Forms.CheckBox ckShowMenuIcon;
        private System.Windows.Forms.CheckBox ckShowNewVersionInfo;
        private System.Windows.Forms.CheckBox ckAlwaysShowStore;
        private System.Windows.Forms.CheckBox ckShowGoogle;
        private System.Windows.Forms.CheckBox ckShowAmazon;
        private System.Windows.Forms.CheckBox ckShowApple;
        private System.Windows.Forms.CheckBox ckShowMS;
        private System.Windows.Forms.CheckBox ckShowAM;
        private System.Windows.Forms.CheckBox ckReplaceSpace;
        private System.Windows.Forms.TextBox txtRenamePattern;
        private System.Windows.Forms.TextBox txtReplaceWhiteSpace;
        private System.Windows.Forms.TextBox txtToolTipPattern;
        private System.Windows.Forms.Button btnUpdate;
        private System.Windows.Forms.Button btnClearCache;
        private System.Windows.Forms.Button btnResetRenamePattern;
        private System.Windows.Forms.Button btnResetInfoTipPattern;
        private System.Windows.Forms.Button btnOK;

        // The five existing settings groups share one scrolling page; no tree navigation.
        private void InitializeComponent() {
            components = new System.ComponentModel.Container();
            toolTip1 = new System.Windows.Forms.ToolTip(components) {
                IsBalloon = true,
                ShowAlways = true,
                ToolTipIcon = System.Windows.Forms.ToolTipIcon.Info
            };

            grpGeneral = NewGroup("General");
            grpIcon = NewGroup("Icons and thumbnails");
            grpContextMenu = NewGroup("Context menu");
            grpRenaming = NewGroup("Renaming");
            grpInfoTip = NewGroup("Info tip");

            lblLanguage = NewLabel("Language");
            lblCurrentVersion = NewLabel("Current version");
            lblNewVer = NewLabel("Latest version");
            lblRenamePattern = NewLabel("Rename pattern");
            lblInfoTipPattern = NewLabel("Info tip pattern");
            lblPatternVariablesHint = NewLabel("");
            lblInfoTipVariablesHint = NewLabel("");

            combLanguage = new System.Windows.Forms.ComboBox {
                Name = "combLanguage",
                Dock = System.Windows.Forms.DockStyle.Fill,
                DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
            };
            combLanguage.SelectedIndexChanged += combLanguage_SelectedIndexChanged;

            btnUpdate = NewButton("btnUpdate", btnUpdate_Click);
            btnClearCache = NewButton("btnClearCache", btnClearCache_Click);
            btnResetRenamePattern = NewButton("btnResetRenamePattern", btnResetRenamePattern_Click);
            btnResetInfoTipPattern = NewButton("btnResetInfoTipPattern", btnResetTooltipPattern_Click);
            btnOK = NewButton("btnOK", btnOK_Click);
            btnOK.Size = new System.Drawing.Size(90, 29);

            ckShowIPA = NewCheckBox("ckShowIPA", ckShowIPA_CheckedChanged);
            ckShowAppxIcon = NewCheckBox("ckShowAppxIcon", ckShowAppxIcon_CheckedChanged);
            ckShowOverlay = NewCheckBox("ckShowOverlay", ckShowOverlay_CheckedChanged);
            ckEnableThumbnail = NewCheckBox("ckEnableThumbnail", ckEnableThumbnail_CheckedChanged);
            ckStretchThumbnail = NewCheckBox("ckStretchThumbnail", ckStretchIcon_CheckedChanged);
            ckAdaptiveIconSupport = NewCheckBox("ckAdaptiveIconSupport", CkAdaptiveIconSupport_CheckedChanged);
            ckShowMenuIcon = NewCheckBox("ckShowMenuIcon", ckShowMenuIcon_CheckedChanged);
            ckShowNewVersionInfo = NewCheckBox("ckShowNewVersionInfo", CkShowNewVersionInfo_CheckedChanged);
            ckAlwaysShowStore = NewCheckBox("ckAlwaysShowStore", ckShowPlay_CheckedChanged);
            ckShowGoogle = NewCheckBox("ckShowGoogle", ckbShowGoogle_CheckedChanged);
            ckShowAmazon = NewCheckBox("ckShowAmazon", ckShowAmazon_CheckedChanged);
            ckShowApple = NewCheckBox("ckShowApple", ckShowApple_CheckedChanged);
            ckShowMS = NewCheckBox("ckShowMS", ckShowMS_CheckedChanged);
            ckShowAM = NewCheckBox("ckShowAM", ckShowAM_CheckedChanged);
            ckReplaceSpace = NewCheckBox("ckReplaceSpace", ckReplaceSpace_CheckedChanged);

            txtRenamePattern = new System.Windows.Forms.TextBox { Name = "txtRenamePattern", Dock = System.Windows.Forms.DockStyle.Fill };
            txtRenamePattern.TextChanged += txtRename_TextChanged;
            txtReplaceWhiteSpace = new System.Windows.Forms.TextBox { Name = "txtReplaceWhiteSpace", Dock = System.Windows.Forms.DockStyle.Fill, MinimumSize = new System.Drawing.Size(100, 0) };
            txtReplaceWhiteSpace.TextChanged += TxtReplaceWhiteSpace_TextChanged;
            txtToolTipPattern = new System.Windows.Forms.TextBox {
                Name = "txtToolTipPattern",
                Dock = System.Windows.Forms.DockStyle.Fill,
                Multiline = true,
                Height = 76,
                ScrollBars = System.Windows.Forms.ScrollBars.Vertical
            };
            txtToolTipPattern.TextChanged += txtToolTipPattern_TextChanged;

            var whitespaceRow = new System.Windows.Forms.TableLayoutPanel {
                AutoSize = true,
                AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Dock = System.Windows.Forms.DockStyle.Fill
            };
            whitespaceRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            whitespaceRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            whitespaceRow.Controls.Add(ckReplaceSpace, 0, 0);
            whitespaceRow.Controls.Add(txtReplaceWhiteSpace, 1, 0);

            AddRows(grpGeneral, lblLanguage, combLanguage, lblCurrentVersion, lblNewVer, btnUpdate);
            AddRows(grpIcon, ckShowIPA, ckShowAppxIcon, ckShowOverlay,
                ckEnableThumbnail, ckStretchThumbnail, ckAdaptiveIconSupport, btnClearCache);
            AddRows(grpContextMenu, ckShowMenuIcon, ckShowNewVersionInfo, ckAlwaysShowStore,
                ckShowGoogle, ckShowAmazon, ckShowApple, ckShowMS, ckShowAM);
            AddRows(grpRenaming, lblRenamePattern, txtRenamePattern, whitespaceRow,
                lblPatternVariablesHint, btnResetRenamePattern);
            AddRows(grpInfoTip, lblInfoTipPattern, txtToolTipPattern,
                lblInfoTipVariablesHint, btnResetInfoTipPattern);

            var sections = new System.Windows.Forms.TableLayoutPanel {
                AutoSize = true,
                AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                Dock = System.Windows.Forms.DockStyle.Top,
                Padding = new System.Windows.Forms.Padding(12, 8, 12, 16)
            };
            sections.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            foreach (System.Windows.Forms.GroupBox group in new[] {
                grpGeneral, grpIcon, grpContextMenu, grpRenaming, grpInfoTip
            }) {
                sections.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
                sections.Controls.Add(group, 0, sections.RowCount++);
            }

            var scroll = new System.Windows.Forms.Panel {
                Dock = System.Windows.Forms.DockStyle.Fill,
                AutoScroll = true
            };
            scroll.Controls.Add(sections);

            var buttons = new System.Windows.Forms.FlowLayoutPanel {
                Dock = System.Windows.Forms.DockStyle.Fill,
                FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new System.Windows.Forms.Padding(12, 8, 12, 4)
            };
            buttons.Controls.Add(btnOK);

            var root = new System.Windows.Forms.TableLayoutPanel {
                Dock = System.Windows.Forms.DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            root.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
            root.Controls.Add(scroll, 0, 0);
            root.Controls.Add(buttons, 0, 1);

            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(600, 590);
            this.MinimumSize = new System.Drawing.Size(470, 380);
            this.Controls.Add(root);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Name = "Preferences";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.AcceptButton = btnOK;
            this.FormClosed += Preferences_FormClosed;
            this.Load += Preferences_Load;
        }

        private static System.Windows.Forms.GroupBox NewGroup(string text) {
            return new System.Windows.Forms.GroupBox {
                Text = text,
                AutoSize = true,
                AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink,
                Dock = System.Windows.Forms.DockStyle.Top,
                Padding = new System.Windows.Forms.Padding(10, 10, 10, 8),
                Margin = new System.Windows.Forms.Padding(0, 4, 0, 8)
            };
        }

        private static System.Windows.Forms.Label NewLabel(string text) {
            return new System.Windows.Forms.Label {
                AutoSize = true,
                Dock = System.Windows.Forms.DockStyle.Fill,
                Text = text,
                Margin = new System.Windows.Forms.Padding(3, 5, 3, 4)
            };
        }

        private static System.Windows.Forms.CheckBox NewCheckBox(string name, System.EventHandler changed) {
            var check = new System.Windows.Forms.CheckBox {
                Name = name,
                AutoSize = true,
                Dock = System.Windows.Forms.DockStyle.Fill,
                UseVisualStyleBackColor = true
            };
            check.CheckedChanged += changed;
            return check;
        }

        private static System.Windows.Forms.Button NewButton(string name, System.EventHandler clicked) {
            var button = new System.Windows.Forms.Button {
                Name = name,
                AutoSize = true,
                UseVisualStyleBackColor = true,
                Anchor = System.Windows.Forms.AnchorStyles.Left,
                Margin = new System.Windows.Forms.Padding(3, 5, 3, 5)
            };
            button.Click += clicked;
            return button;
        }

        private static void AddRows(System.Windows.Forms.GroupBox group, params System.Windows.Forms.Control[] controls) {
            var layout = new System.Windows.Forms.TableLayoutPanel {
                AutoSize = true,
                AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                Dock = System.Windows.Forms.DockStyle.Top,
                Padding = new System.Windows.Forms.Padding(6, 8, 6, 4)
            };
            layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            foreach (var control in controls) {
                layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
                layout.Controls.Add(control, 0, layout.RowCount++);
            }
            group.Controls.Add(layout);
        }
    }
}
