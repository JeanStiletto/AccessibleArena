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

        // Page 1 controls
        private Label _titleLabel;
        private Label _descriptionLabel;
        private Label _versionLabel;
        private Label _languageLabel;
        private Button _nextButton;

        // Page 2 controls
        private Label _downloadTitleLabel;
        private Label _downloadDescriptionLabel;
        private Button _directDownloadButton;
        private Button _downloadPageButton;
        private Button _installButton;
        private Button _backButton;

        private Panel _page1;
        private Panel _page2;

        public bool ProceedWithInstall { get; private set; } = false;

        /// <summary>
        /// The language code selected by the user (e.g. "en", "de", "pt-BR").
        /// </summary>
        public string SelectedLanguage { get; private set; }

        /// <summary>
        /// The latest mod version from GitHub, shown on page 1.
        /// Set by caller before showing the form.
        /// </summary>
        public string LatestModVersion { get; set; }

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

            // === Page 1: Welcome + Language ===
            _page1 = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(500, 340),
                Visible = true
            };

            _titleLabel = new Label
            {
                Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(450, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _descriptionLabel = new Label
            {
                Location = new Point(20, 60),
                Size = new Size(450, 75),
                TextAlign = ContentAlignment.TopLeft
            };

            _versionLabel = new Label
            {
                Location = new Point(20, 140),
                Size = new Size(450, 20),
                Font = new Font(Font.FontFamily, 9, FontStyle.Bold)
            };

            _languageLabel = new Label
            {
                Location = new Point(20, 170),
                Size = new Size(100, 20)
            };

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

            _nextButton = new Button
            {
                Location = new Point(330, 220),
                Size = new Size(140, 35)
            };
            _nextButton.Click += (s, e) => ShowPage2();

            _page1.Controls.AddRange(new Control[]
            {
                _titleLabel,
                _descriptionLabel,
                _versionLabel,
                _languageLabel,
                _languageComboBox,
                _nextButton
            });

            // === Page 2: MTGA Download + Install ===
            _page2 = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(500, 340),
                Visible = false
            };

            _downloadTitleLabel = new Label
            {
                Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(450, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _downloadDescriptionLabel = new Label
            {
                Location = new Point(20, 60),
                Size = new Size(450, 80),
                TextAlign = ContentAlignment.TopLeft
            };

            _directDownloadButton = new Button
            {
                Location = new Point(20, 160),
                Size = new Size(220, 35)
            };
            _directDownloadButton.Click += (s, e) =>
            {
                Logger.Info($"Starting direct MTGA download: {MtgaDirectDownloadUrl}");
                Process.Start(MtgaDirectDownloadUrl);
            };

            _downloadPageButton = new Button
            {
                Location = new Point(250, 160),
                Size = new Size(220, 35)
            };
            _downloadPageButton.Click += (s, e) =>
            {
                Logger.Info($"Opening MTGA download page: {MtgaDownloadPageUrl}");
                Process.Start(MtgaDownloadPageUrl);
            };

            _backButton = new Button
            {
                Location = new Point(20, 220),
                Size = new Size(140, 35)
            };
            _backButton.Click += (s, e) => ShowPage1();

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

            _page2.Controls.AddRange(new Control[]
            {
                _downloadTitleLabel,
                _downloadDescriptionLabel,
                _directDownloadButton,
                _downloadPageButton,
                _backButton,
                _installButton
            });

            Controls.Add(_page1);
            Controls.Add(_page2);

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

        private void ShowPage1()
        {
            _page2.Visible = false;
            _page1.Visible = true;
            _nextButton.Focus();
        }

        private void ShowPage2()
        {
            _page1.Visible = false;
            _page2.Visible = true;
            _installButton.Focus();
        }

        private void ApplyLocale()
        {
            // Page 1
            _titleLabel.Text = InstallerLocale.Get("Welcome_Title");
            _descriptionLabel.Text = InstallerLocale.Get("Welcome_Description");
            _languageLabel.Text = InstallerLocale.Get("Welcome_LanguageLabel");
            _nextButton.Text = InstallerLocale.Get("Welcome_NextButton");

            if (!string.IsNullOrEmpty(LatestModVersion))
                _versionLabel.Text = InstallerLocale.Format("Welcome_VersionInfo_Format", LatestModVersion);
            else
                _versionLabel.Text = InstallerLocale.Get("Welcome_VersionInfo_Unknown");

            // Page 2
            _downloadTitleLabel.Text = InstallerLocale.Get("Welcome_DownloadTitle");
            _downloadDescriptionLabel.Text = InstallerLocale.Get("Welcome_DownloadDescription");
            _directDownloadButton.Text = InstallerLocale.Get("Welcome_DirectDownload");
            _downloadPageButton.Text = InstallerLocale.Get("Welcome_DownloadPage");
            _installButton.Text = InstallerLocale.Get("Welcome_InstallMod");
            _backButton.Text = InstallerLocale.Get("Welcome_BackButton");
        }
    }
}
