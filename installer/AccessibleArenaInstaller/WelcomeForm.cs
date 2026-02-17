using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AccessibleArenaInstaller
{
    public class WelcomeForm : Form
    {
        private const string MtgaDownloadPageUrl = "https://magic.wizards.com/en/mtgarena";
        private const string MtgaDirectDownloadUrl = "https://mtgarena.downloads.wizards.com/Live/Windows64/MTGAInstaller.exe";

        private ComboBox _languageComboBox;
        private Label _titleLabel;
        private Label _descriptionLabel;
        private Label _languageLabel;
        private Button _directDownloadButton;
        private Button _downloadPageButton;
        private Button _installButton;

        public bool ProceedWithInstall { get; private set; } = false;

        /// <summary>
        /// The language code selected by the user (e.g. "en", "de", "pt-BR").
        /// </summary>
        public string SelectedLanguage { get; private set; }

        public WelcomeForm()
        {
            SelectedLanguage = LanguageDetector.DetectLanguage();
            InitializeComponents();
            ApplyLocale();
            InstallerLocale.OnLanguageChanged += ApplyLocale;
        }

        private void InitializeComponents()
        {
            // Form settings
            Text = Config.DisplayName;
            Size = new Size(500, 340);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            // Welcome title
            _titleLabel = new Label
            {
                Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(450, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Description
            _descriptionLabel = new Label
            {
                Location = new Point(20, 60),
                Size = new Size(450, 100),
                TextAlign = ContentAlignment.TopLeft
            };

            // Language label
            _languageLabel = new Label
            {
                Location = new Point(20, 170),
                Size = new Size(100, 20)
            };

            // Language dropdown
            _languageComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(125, 167),
                Size = new Size(345, 25)
            };

            // Populate with display names, select detected language
            foreach (var code in LanguageDetector.SupportedLanguages)
            {
                string displayName = LanguageDetector.DisplayNames.TryGetValue(code, out string name)
                    ? name : code;
                _languageComboBox.Items.Add(displayName);
            }

            int defaultIndex = Array.IndexOf(LanguageDetector.SupportedLanguages, SelectedLanguage);
            _languageComboBox.SelectedIndex = defaultIndex >= 0 ? defaultIndex : 0;

            _languageComboBox.SelectedIndexChanged += (s, e) =>
            {
                int idx = _languageComboBox.SelectedIndex;
                if (idx >= 0 && idx < LanguageDetector.SupportedLanguages.Length)
                {
                    SelectedLanguage = LanguageDetector.SupportedLanguages[idx];
                    Logger.Info($"User selected language: {SelectedLanguage}");
                    InstallerLocale.SetLanguage(SelectedLanguage);
                }
            };

            // Direct Download button
            _directDownloadButton = new Button
            {
                Location = new Point(20, 220),
                Size = new Size(140, 35)
            };
            _directDownloadButton.Click += (s, e) =>
            {
                Logger.Info($"Starting direct MTGA download: {MtgaDirectDownloadUrl}");
                Process.Start(MtgaDirectDownloadUrl);
            };

            // Download Page button
            _downloadPageButton = new Button
            {
                Location = new Point(175, 220),
                Size = new Size(140, 35)
            };
            _downloadPageButton.Click += (s, e) =>
            {
                Logger.Info($"Opening MTGA download page: {MtgaDownloadPageUrl}");
                Process.Start(MtgaDownloadPageUrl);
            };

            // Install Mod button
            _installButton = new Button
            {
                Location = new Point(330, 220),
                Size = new Size(140, 35)
            };
            _installButton.Click += (s, e) =>
            {
                ProceedWithInstall = true;
                Close();
            };

            // Add controls
            Controls.AddRange(new Control[]
            {
                _titleLabel,
                _descriptionLabel,
                _languageLabel,
                _languageComboBox,
                _directDownloadButton,
                _downloadPageButton,
                _installButton
            });

            // Handle form closing with X button
            FormClosing += (s, e) =>
            {
                InstallerLocale.OnLanguageChanged -= ApplyLocale;
                if (!ProceedWithInstall)
                {
                    Logger.Info("User closed welcome dialog without proceeding");
                }
            };
        }

        private void ApplyLocale()
        {
            _titleLabel.Text = InstallerLocale.Get("Welcome_Title");
            _descriptionLabel.Text = InstallerLocale.Get("Welcome_Description");
            _languageLabel.Text = InstallerLocale.Get("Welcome_LanguageLabel");
            _directDownloadButton.Text = InstallerLocale.Get("Welcome_DirectDownload");
            _downloadPageButton.Text = InstallerLocale.Get("Welcome_DownloadPage");
            _installButton.Text = InstallerLocale.Get("Welcome_InstallMod");
        }
    }
}
