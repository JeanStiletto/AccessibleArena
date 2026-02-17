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

        public bool ProceedWithInstall { get; private set; } = false;

        /// <summary>
        /// The language code selected by the user (e.g. "en", "de", "pt-BR").
        /// </summary>
        public string SelectedLanguage { get; private set; }

        public WelcomeForm()
        {
            SelectedLanguage = LanguageDetector.DetectLanguage();
            InitializeComponents();
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
            var titleLabel = new Label
            {
                Text = $"Welcome to {Config.DisplayName}",
                Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(450, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Description
            var descriptionLabel = new Label
            {
                Text = "This will install the accessibility mod for Magic: The Gathering Arena.\n\n" +
                       "The mod enables blind and visually impaired players to play MTGA " +
                       "using the NVDA screen reader.\n\n" +
                       "If you don't have MTGA installed yet, please download and install it first.",
                Location = new Point(20, 60),
                Size = new Size(450, 100),
                TextAlign = ContentAlignment.TopLeft
            };

            // Language label
            var languageLabel = new Label
            {
                Text = "Mod language:",
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
                }
            };

            // Direct Download button
            var directDownloadButton = new Button
            {
                Text = "Direct Download",
                Location = new Point(20, 220),
                Size = new Size(140, 35)
            };
            directDownloadButton.Click += (s, e) =>
            {
                Logger.Info($"Starting direct MTGA download: {MtgaDirectDownloadUrl}");
                Process.Start(MtgaDirectDownloadUrl);
            };

            // Download Page button
            var downloadPageButton = new Button
            {
                Text = "Download Page",
                Location = new Point(175, 220),
                Size = new Size(140, 35)
            };
            downloadPageButton.Click += (s, e) =>
            {
                Logger.Info($"Opening MTGA download page: {MtgaDownloadPageUrl}");
                Process.Start(MtgaDownloadPageUrl);
            };

            // Install Mod button
            var installButton = new Button
            {
                Text = "Install Mod",
                Location = new Point(330, 220),
                Size = new Size(140, 35)
            };
            installButton.Click += (s, e) =>
            {
                ProceedWithInstall = true;
                Close();
            };

            // Add controls
            Controls.AddRange(new Control[]
            {
                titleLabel,
                descriptionLabel,
                languageLabel,
                _languageComboBox,
                directDownloadButton,
                downloadPageButton,
                installButton
            });

            // Handle form closing with X button
            FormClosing += (s, e) =>
            {
                if (!ProceedWithInstall)
                {
                    Logger.Info("User closed welcome dialog without proceeding");
                }
            };
        }
    }
}
