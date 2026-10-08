using System;
using System.Configuration;
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
        }

        private void Preferences_Load(object sender, EventArgs e) {
            if (formLoaded)
                return;

            Utility.Localize();
            Log("Using setting file from: " +
                ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath);

            CultureInfo[] languages = Utility.getSupportedLanguages();
            int selectedLanguage = 0;
            for (int i = 0; i < languages.Length; i++) {
                combLanguage.Items.Add(languages[i].NativeName);
                if (languages[i].Name == Thread.CurrentThread.CurrentUICulture.Name)
                    selectedLanguage = i;
            }
            if (combLanguage.Items.Count > 0)
                combLanguage.SelectedIndex = selectedLanguage;

            ckShowOverlay.Checked = Utility.GetSetting("ShowOverLayIcon", "False") == "True";
            ckShowIPA.Checked = Utility.GetSetting("ShowIpaIcon", "True") == "True";
            ckShowAppxIcon.Checked = Utility.GetSetting("ShowAppxIcon", "False") == "True";
            ckStretchThumbnail.Checked = Utility.GetSetting("StretchThumbnail", "True") == "True";
            ckEnableThumbnail.Checked = Utility.GetSetting("EnableThumbnail", "True") == "True";
            ckAdaptiveIconSupport.Checked = Utility.GetSetting("SupportAdaptiveIcon", "False") == "True";

            ckAlwaysShowStore.Checked = Utility.GetSetting("ShowAppStoreWhenMultiSelected", "True") == "True";
            ckShowMenuIcon.Checked = Utility.GetSetting("ShowMenuIcon", "True") == "True";
            ckShowNewVersionInfo.Checked = Utility.GetSetting("ShowNewVersion", "True") == "True";
            ckShowGoogle.Checked = Utility.GetSetting("ShowGooglePlay", "True") == "True";
            ckShowAmazon.Checked = Utility.GetSetting("ShowAmazonStore", "True") == "True";
            ckShowApple.Checked = Utility.GetSetting("ShowAppleStore", "True") == "True";
            ckShowMS.Checked = Utility.GetSetting("ShowMSStore", "True") == "True";
            ckShowAM.Checked = Utility.GetSetting("ShowApkMirror", "False") == "True";

            txtRenamePattern.Text = Utility.GetSetting("RenamePattern", NonLocalizeResources.strRenamePatternDefault);
            ckReplaceSpace.Checked = Utility.GetSetting("ReplaceSpace", "False") == "True";
            txtReplaceWhiteSpace.Text = Utility.GetSetting("ReplaceSpaceChar", "_");
            txtReplaceWhiteSpace.Enabled = ckReplaceSpace.Checked;
            txtToolTipPattern.Text = Utility.GetSetting("ToolTipPattern", NonLocalizeResources.strInfoTipDefault);

            btnUpdate.Image = Utility.ResizeBitmap(
                Utility.NewVersionAvailible() ? NonLocalizeResources.iconUpdate : NonLocalizeResources.iconGitHub, 16);
            btnUpdate.TextImageRelation = TextImageRelation.ImageBeforeText;

            RefreshLocalizedText();
            formLoaded = true;
        }

        // Changing the language updates labels without resetting the current field values.
        private void RefreshLocalizedText() {
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

        private void Log(string message) {
            Utility.Log(this, "", message);
        }
    }
}
