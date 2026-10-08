using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Globalization;
using ApkShellext2.Properties;

namespace ApkShellext2 {
    public partial class Preferences : Form {
        private bool formLoaded;
        private bool needClearThumbnailCache;

        public string currentFile = "";

        public Preferences() {
            InitializeComponent();
            // Build the populated page before ShowDialog creates a visible form.
            SuspendLayoutTree(this);
            try {
                InitializeSettings();
            } finally {
                ResumeLayoutTree(this);
            }
            DoubleBuffered = true;
        }

        private void InitializeSettings() {
            Utility.Localize();
            var settings = Utility.GetSettingsSnapshot(new Dictionary<string, string> {
                { "ShowOverLayIcon", "False" },
                { "ShowIpaIcon", "True" },
                { "ShowAppxIcon", "False" },
                { "StretchThumbnail", "True" },
                { "EnableThumbnail", "True" },
                { "SupportAdaptiveIcon", "False" },
                { "ShowAppStoreWhenMultiSelected", "True" },
                { "ShowMenuIcon", "True" },
                { "ShowNewVersion", "True" },
                { "ShowGooglePlay", "True" },
                { "ShowAmazonStore", "True" },
                { "ShowAppleStore", "True" },
                { "ShowMSStore", "True" },
                { "ShowApkMirror", "False" },
                { "RenamePattern", NonLocalizeResources.strRenamePatternDefault },
                { "ReplaceSpace", "False" },
                { "ReplaceSpaceChar", "_" },
                { "ToolTipPattern", NonLocalizeResources.strInfoTipDefault }
            });

            CultureInfo[] languages = Utility.getSupportedLanguages();
            int selectedLanguage = 0;
            for (int i = 0; i < languages.Length; i++) {
                combLanguage.Items.Add(languages[i].NativeName);
                if (languages[i].Name == Thread.CurrentThread.CurrentUICulture.Name)
                    selectedLanguage = i;
            }
            if (combLanguage.Items.Count > 0)
                combLanguage.SelectedIndex = selectedLanguage;

            ckShowOverlay.Checked = settings["ShowOverLayIcon"] == "True";
            ckShowIPA.Checked = settings["ShowIpaIcon"] == "True";
            ckShowAppxIcon.Checked = settings["ShowAppxIcon"] == "True";
            ckStretchThumbnail.Checked = settings["StretchThumbnail"] == "True";
            ckEnableThumbnail.Checked = settings["EnableThumbnail"] == "True";
            ckAdaptiveIconSupport.Checked = settings["SupportAdaptiveIcon"] == "True";

            ckAlwaysShowStore.Checked = settings["ShowAppStoreWhenMultiSelected"] == "True";
            ckShowMenuIcon.Checked = settings["ShowMenuIcon"] == "True";
            ckShowNewVersionInfo.Checked = settings["ShowNewVersion"] == "True";
            ckShowGoogle.Checked = settings["ShowGooglePlay"] == "True";
            ckShowAmazon.Checked = settings["ShowAmazonStore"] == "True";
            ckShowApple.Checked = settings["ShowAppleStore"] == "True";
            ckShowMS.Checked = settings["ShowMSStore"] == "True";
            ckShowAM.Checked = settings["ShowApkMirror"] == "True";

            txtRenamePattern.Text = settings["RenamePattern"];
            ckReplaceSpace.Checked = settings["ReplaceSpace"] == "True";
            txtReplaceWhiteSpace.Text = settings["ReplaceSpaceChar"];
            txtReplaceWhiteSpace.Enabled = ckReplaceSpace.Checked;
            txtToolTipPattern.Text = settings["ToolTipPattern"];

            btnUpdate.Image = Utility.ResizeBitmap(
                Utility.NewVersionAvailible() ? NonLocalizeResources.iconUpdate : NonLocalizeResources.iconGitHub, 16);
            btnUpdate.TextImageRelation = TextImageRelation.ImageBeforeText;

            RefreshLocalizedText();
            formLoaded = true;
        }

        // Changing the language updates labels without resetting the current field values.
        private void RefreshLocalizedText() {
            SuspendLayoutTree(this);
            try {
                Text = Resources.strPreferencesCaption;
                grpGeneral.Text = Resources.twGeneral;
                grpIcon.Text = Resources.twIcon;
                grpContextMenu.Text = Resources.twContextMenu;
                grpRenaming.Text = Resources.twRename;
                grpInfoTip.Text = Resources.twInfotip;

                lblLanguage.Text = Resources.strLanguages;
                lblCurrentVersion.Text = string.Format(Resources.strCurrVersion, Assembly.GetExecutingAssembly().GetName().Version);
                if (Utility.NewVersionAvailible()) {
                    lblNewVer.Text = string.Format(Resources.strNewVersionAvailible, Utility.GetSetting("LatestVersion"));
                    btnUpdate.Text = Resources.btnUpdate;
                    toolTip1.SetToolTip(btnUpdate, Resources.btnUpdateToolTip);
                } else {
                    lblNewVer.Text = Resources.strGotLatest;
                    btnUpdate.Text = Resources.btnGitHub;
                    toolTip1.SetToolTip(btnUpdate, Resources.strGotoProjectSite);
                }

                ckShowOverlay.Text = Resources.strShowOverlayIcon;
                toolTip1.SetToolTip(ckShowOverlay, Resources.strShowOverlayIconToolTip);
                ckShowIPA.Text = Resources.strShowIpaIcon;
                ckShowAppxIcon.Text = Resources.strShowAppxIcon;
                ckStretchThumbnail.Text = Resources.strStretchThumbnail;
                ckEnableThumbnail.Text = Resources.strEnableThumbnail;
                ckAdaptiveIconSupport.Text = Resources.strSupportAdaptiveIcon;
                btnClearCache.Text = Resources.strClearCache;

                ckAlwaysShowStore.Text = Resources.strAlwaysShowGooglePlay;
                toolTip1.SetToolTip(ckAlwaysShowStore, Resources.strAlwaysShowGooglePlayToolTip);
                ckShowMenuIcon.Text = Resources.strShowContextMenuIcon;
                ckShowNewVersionInfo.Text = Resources.strShowNewVerInfo;
                ckShowGoogle.Text = Resources.strShowGooglePlay;
                ckShowAmazon.Text = Resources.strShowAmazonStore;
                ckShowApple.Text = Resources.strShowAppleStore;
                ckShowMS.Text = Resources.strShowMSStore;
                ckShowAM.Text = Resources.strShowApkMirror;

                lblRenamePattern.Text = Resources.strRenamePattern;
                ckReplaceSpace.Text = Resources.strReplaceSpaceWith_;
                lblInfoTipPattern.Text = Resources.strInfoTipPattern;
                lblPatternVariablesHint.Text = Resources.strPatternVariablesHint;
                lblInfoTipVariablesHint.Text = Resources.strPatternVariablesHint;
                btnResetRenamePattern.Text = Resources.btnResetPattern;
                btnResetInfoTipPattern.Text = Resources.btnResetPattern;
                btnOK.Text = Resources.btnOK;
            } finally {
                ResumeLayoutTree(this);
            }
        }

        // Suspend nested auto-sizing table layouts while localized text changes.
        private static void SuspendLayoutTree(Control control) {
            control.SuspendLayout();
            foreach (Control child in control.Controls)
                SuspendLayoutTree(child);
        }

        private static void ResumeLayoutTree(Control control) {
            foreach (Control child in control.Controls)
                ResumeLayoutTree(child);
            control.ResumeLayout(true);
        }

        private void combLanguage_SelectedIndexChanged(object sender, EventArgs e) {
            if (!formLoaded || combLanguage.SelectedIndex < 0)
                return;
            CultureInfo[] languages = Utility.getSupportedLanguages();
            if (combLanguage.SelectedIndex >= languages.Length)
                return;

            Utility.SaveSetting("Language", languages[combLanguage.SelectedIndex].Name);
            Utility.Localize();
            RefreshLocalizedText();
        }

        private void ckShowIPA_CheckedChanged(object sender, EventArgs e) {
            if (!formLoaded) return;
            Utility.SaveSetting("ShowIpaIcon", ckShowIPA.Checked);
            Utility.refreshShell();
        }

        private void ckShowAppxIcon_CheckedChanged(object sender, EventArgs e) {
            if (!formLoaded) return;
            Utility.SaveSetting("ShowAppxIcon", ckShowAppxIcon.Checked);
            SharpShell.Interop.Shell32.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        }

        private void ckShowOverlay_CheckedChanged(object sender, EventArgs e) {
            if (!formLoaded) return;
            Utility.SaveSetting("ShowOverLayIcon", ckShowOverlay.Checked);
            SharpShell.Interop.Shell32.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
            if (Utility.GetSetting("EnableThumbnail", "True") == "True")
                needClearThumbnailCache = true;
        }

        private void ckStretchIcon_CheckedChanged(object sender, EventArgs e) {
            if (!formLoaded) return;
            Utility.SaveSetting("StretchThumbnail", ckStretchThumbnail.Checked);
            needClearThumbnailCache = true;
        }

        private void ckEnableThumbnail_CheckedChanged(object sender, EventArgs e) {
            if (!formLoaded) return;
            Utility.SaveSetting("EnableThumbnail", ckEnableThumbnail.Checked);
            needClearThumbnailCache = true;
        }

        private void CkAdaptiveIconSupport_CheckedChanged(object sender, EventArgs e) {
            if (!formLoaded) return;
            Utility.SaveSetting("SupportAdaptiveIcon", ckAdaptiveIconSupport.Checked);
            needClearThumbnailCache = true;
        }

        private void ckShowMenuIcon_CheckedChanged(object sender, EventArgs e) {
            if (formLoaded) Utility.SaveSetting("ShowMenuIcon", ckShowMenuIcon.Checked);
        }

        private void CkShowNewVersionInfo_CheckedChanged(object sender, EventArgs e) {
            if (formLoaded) Utility.SaveSetting("ShowNewVersion", ckShowNewVersionInfo.Checked);
        }

        private void ckShowPlay_CheckedChanged(object sender, EventArgs e) {
            if (formLoaded) Utility.SaveSetting("ShowAppStoreWhenMultiSelected", ckAlwaysShowStore.Checked);
        }

        private void ckbShowGoogle_CheckedChanged(object sender, EventArgs e) {
            if (formLoaded) Utility.SaveSetting("ShowGooglePlay", ckShowGoogle.Checked);
        }

        private void ckShowAmazon_CheckedChanged(object sender, EventArgs e) {
            if (formLoaded) Utility.SaveSetting("ShowAmazonStore", ckShowAmazon.Checked);
        }

        private void ckShowApple_CheckedChanged(object sender, EventArgs e) {
            if (formLoaded) Utility.SaveSetting("ShowAppleStore", ckShowApple.Checked);
        }

        private void ckShowMS_CheckedChanged(object sender, EventArgs e) {
            if (formLoaded) Utility.SaveSetting("ShowMSStore", ckShowMS.Checked);
        }

        private void ckShowAM_CheckedChanged(object sender, EventArgs e) {
            if (formLoaded) Utility.SaveSetting("ShowApkMirror", ckShowAM.Checked);
        }

        private void ckReplaceSpace_CheckedChanged(object sender, EventArgs e) {
            txtReplaceWhiteSpace.Enabled = ckReplaceSpace.Checked;
            if (formLoaded) Utility.SaveSetting("ReplaceSpace", ckReplaceSpace.Checked);
        }

        private void txtRename_TextChanged(object sender, EventArgs e) {
            if (formLoaded) Utility.SaveSetting("RenamePattern", txtRenamePattern.Text);
        }

        private void txtToolTipPattern_TextChanged(object sender, EventArgs e) {
            if (formLoaded) Utility.SaveSetting("ToolTipPattern", txtToolTipPattern.Text);
        }

        private void TxtReplaceWhiteSpace_TextChanged(object sender, EventArgs e) {
            if (formLoaded) Utility.SaveSetting("ReplaceSpaceChar", txtReplaceWhiteSpace.Text);
        }

        private void btnResetRenamePattern_Click(object sender, EventArgs e) {
            txtRenamePattern.Text = NonLocalizeResources.strRenamePatternDefault;
        }

        private void btnResetTooltipPattern_Click(object sender, EventArgs e) {
            txtToolTipPattern.Text = NonLocalizeResources.strInfoTipDefault;
        }

        private void btnUpdate_Click(object sender, EventArgs e) {
            Process.Start(NonLocalizeResources.urlGithubHomeWithVersion);
        }

        private void btnOK_Click(object sender, EventArgs e) {
            // All settings are applied as changed, including the text patterns.
            Close();
        }

        private void Preferences_FormClosed(object sender, FormClosedEventArgs e) {
            if (needClearThumbnailCache &&
                MessageBox.Show(Resources.dialogNeedClearCache, Resources.strClearCache,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
                ClearThumbnailCache();
        }

        private void btnClearCache_Click(object sender, EventArgs e) {
            if (MessageBox.Show(Resources.dialogClearCache, Resources.strClearCache,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
                ClearThumbnailCache();
        }

        // Called after a single confirmation, whether from the button or on close.
        private void ClearThumbnailCache() {
            string path = Path.Combine(Path.GetTempPath(), "clearcache.bat");
            File.WriteAllText(path, NonLocalizeResources.cmdClearCache);
            using (var process = new Process()) {
                process.StartInfo.FileName = path;
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.Start();
            }
        }

    }
}
