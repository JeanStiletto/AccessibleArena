using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace AccessibleArenaInstaller
{
    public class WelcomeForm : Form
    {
        private const string MtgaDownloadPageUrl = "https://magic.wizards.com/en/mtgarena";
        private const string MtgaDirectDownloadUrl = "https://mtgarena.downloads.wizards.com/Live/Windows64/MTGAInstaller.exe";

        public bool ProceedWithInstall { get; private set; } = false;

        public WelcomeForm()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // Form settings
            Text = Config.DisplayName;
            Size = new Size(500, 300);
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

            // Direct Download button
            var directDownloadButton = new Button
            {
                Text = "Direct Download",
                Location = new Point(20, 180),
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
                Location = new Point(175, 180),
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
                Location = new Point(330, 180),
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
