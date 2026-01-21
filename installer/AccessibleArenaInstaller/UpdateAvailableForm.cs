using System.Drawing;
using System.Windows.Forms;

namespace AccessibleArenaInstaller
{
    public class UpdateAvailableForm : Form
    {
        public UpdateChoice UserChoice { get; private set; } = UpdateChoice.Close;

        public UpdateAvailableForm(string installedVersion, string latestVersion)
        {
            InitializeComponents(installedVersion, latestVersion);
        }

        private void InitializeComponents(string installedVersion, string latestVersion)
        {
            // Form settings
            Text = "Update Available";
            Size = new Size(450, 250);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            // Title
            var titleLabel = new Label
            {
                Text = "An update is available!",
                Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(400, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Version info
            var versionLabel = new Label
            {
                Text = $"Installed version: {installedVersion}\nLatest version: {latestVersion}",
                Location = new Point(20, 60),
                Size = new Size(400, 50),
                TextAlign = ContentAlignment.TopCenter
            };

            // Update Mod button
            var updateButton = new Button
            {
                Text = "Update Mod",
                Location = new Point(40, 130),
                Size = new Size(110, 35)
            };
            updateButton.Click += (s, e) =>
            {
                UserChoice = UpdateChoice.UpdateOnly;
                Close();
            };

            // Full Install button
            var fullInstallButton = new Button
            {
                Text = "Full Install",
                Location = new Point(165, 130),
                Size = new Size(110, 35)
            };
            fullInstallButton.Click += (s, e) =>
            {
                UserChoice = UpdateChoice.FullInstall;
                Close();
            };

            // Close button
            var closeButton = new Button
            {
                Text = "Close",
                Location = new Point(290, 130),
                Size = new Size(110, 35)
            };
            closeButton.Click += (s, e) =>
            {
                UserChoice = UpdateChoice.Close;
                Close();
            };

            // Add controls
            Controls.AddRange(new Control[]
            {
                titleLabel,
                versionLabel,
                updateButton,
                fullInstallButton,
                closeButton
            });
        }
    }
}
