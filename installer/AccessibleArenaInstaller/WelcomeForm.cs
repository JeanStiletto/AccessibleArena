using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace AccessibleArenaInstaller
{
    public class WelcomeForm : Form
    {
        private const string MtgaDownloadUrl = "https://magic.wizards.com/en/mtgarena";

        public bool ProceedWithInstall { get; private set; } = false;

        public WelcomeForm()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // Form settings
            Text = Config.DisplayName;
            Size = new Size(450, 280);
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
                Size = new Size(400, 30),
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
                Size = new Size(400, 100),
                TextAlign = ContentAlignment.TopLeft
            };

            // Download MTGA button
            var downloadButton = new Button
            {
                Text = "Download MTGA",
                Location = new Point(80, 180),
                Size = new Size(130, 35)
            };
            downloadButton.Click += (s, e) =>
            {
                Logger.Info($"Opening MTGA download page: {MtgaDownloadUrl}");
                Process.Start(MtgaDownloadUrl);
            };

            // Install Mod button
            var installButton = new Button
            {
                Text = "Install Mod",
                Location = new Point(230, 180),
                Size = new Size(130, 35)
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
                downloadButton,
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
