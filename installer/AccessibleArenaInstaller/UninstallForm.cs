using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AccessibleArenaInstaller
{
    public class UninstallForm : Form
    {
        private readonly string _mtgaPath;
        private Label _titleLabel;
        private Label _statusLabel;
        private Label _pathLabel;
        private CheckBox _removeMelonLoaderCheckBox;
        private Button _uninstallButton;
        private Button _cancelButton;
        private ProgressBar _progressBar;

        public UninstallForm(string mtgaPath)
        {
            _mtgaPath = mtgaPath;
            InitializeComponents();
            Logger.Info("UninstallForm initialized");
        }

        private void InitializeComponents()
        {
            // Form settings
            Text = InstallerLocale.Get("Uninstall_Title");
            Size = new Size(450, 280);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            // Title
            _titleLabel = new Label
            {
                Text = InstallerLocale.Get("Uninstall_Heading"),
                Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(400, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Status label
            _statusLabel = new Label
            {
                Text = InstallerLocale.Get("Uninstall_Description"),
                Location = new Point(20, 60),
                Size = new Size(400, 40),
                TextAlign = ContentAlignment.TopLeft
            };

            // Path label
            _pathLabel = new Label
            {
                Text = InstallerLocale.Format("Uninstall_PathLabel_Format", _mtgaPath),
                Location = new Point(20, 100),
                Size = new Size(400, 20),
                ForeColor = SystemColors.GrayText
            };

            // MelonLoader checkbox
            _removeMelonLoaderCheckBox = new CheckBox
            {
                Text = InstallerLocale.Get("Uninstall_MelonLoaderCheckBox"),
                Location = new Point(20, 130),
                Size = new Size(400, 25),
                Checked = false
            };

            // Progress bar
            _progressBar = new ProgressBar
            {
                Location = new Point(20, 165),
                Size = new Size(400, 25),
                Style = ProgressBarStyle.Continuous,
                Visible = false
            };

            // Uninstall button
            _uninstallButton = new Button
            {
                Text = InstallerLocale.Get("Uninstall_UninstallButton"),
                Location = new Point(230, 200),
                Size = new Size(90, 30)
            };
            _uninstallButton.Click += UninstallButton_Click;

            // Cancel button
            _cancelButton = new Button
            {
                Text = InstallerLocale.Get("Uninstall_CancelButton"),
                Location = new Point(330, 200),
                Size = new Size(80, 30)
            };
            _cancelButton.Click += (s, e) => Close();

            // Add controls
            Controls.AddRange(new Control[]
            {
                _titleLabel,
                _statusLabel,
                _pathLabel,
                _removeMelonLoaderCheckBox,
                _progressBar,
                _uninstallButton,
                _cancelButton
            });
        }

        private async void UninstallButton_Click(object sender, EventArgs e)
        {
            // Build confirmation message with optional MelonLoader line
            string confirmText = InstallerLocale.Get("Uninstall_Confirm_Text");
            if (_removeMelonLoaderCheckBox.Checked)
            {
                // Insert MelonLoader line before the last item (Registry entries)
                string mlLine = InstallerLocale.Get("Uninstall_Confirm_MelonLoader");
                int lastDash = confirmText.LastIndexOf("\n- Registry");
                if (lastDash >= 0)
                {
                    confirmText = confirmText.Insert(lastDash, "\n" + mlLine.TrimEnd('\n'));
                }
            }

            // Confirm uninstallation
            var result = MessageBox.Show(
                confirmText,
                InstallerLocale.Get("Uninstall_Confirm_Title"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            // Disable controls
            SetControlsEnabled(false);
            _progressBar.Visible = true;
            _progressBar.Value = 0;

            try
            {
                UpdateStatus(InstallerLocale.Get("Uninstall_StatusRemoving"));
                _progressBar.Value = 20;

                await Task.Run(() => Program.PerformUninstall(_mtgaPath));

                _progressBar.Value = 60;

                if (_removeMelonLoaderCheckBox.Checked)
                {
                    UpdateStatus(InstallerLocale.Get("Uninstall_StatusRemovingMelonLoader"));
                    await Task.Run(() => Program.UninstallMelonLoader(_mtgaPath));
                }

                _progressBar.Value = 100;
                UpdateStatus(InstallerLocale.Get("Uninstall_StatusComplete"));

                Logger.Info("Uninstallation completed successfully");

                MessageBox.Show(
                    InstallerLocale.Get("Uninstall_Complete_Text"),
                    InstallerLocale.Get("Uninstall_Complete_Title"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                // Ask about saving log file (only prompts if there were errors/warnings)
                Logger.AskAndSave();

                Close();
            }
            catch (Exception ex)
            {
                Logger.Error("Uninstallation failed", ex);

                MessageBox.Show(
                    InstallerLocale.Format("Uninstall_Error_Format", ex.Message),
                    InstallerLocale.Get("Uninstall_Error_Title"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                // Always ask about log file on error
                if (Logger.AskAndSave(alwaysAsk: true))
                {
                    Logger.OpenLogFile();
                }

                SetControlsEnabled(true);
                _progressBar.Visible = false;
            }
        }

        private void UpdateStatus(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateStatus(message)));
                return;
            }

            _statusLabel.Text = message;
            Logger.Info(message);
        }

        private void SetControlsEnabled(bool enabled)
        {
            _uninstallButton.Enabled = enabled;
            _cancelButton.Enabled = enabled;
            _removeMelonLoaderCheckBox.Enabled = enabled;
        }
    }
}
