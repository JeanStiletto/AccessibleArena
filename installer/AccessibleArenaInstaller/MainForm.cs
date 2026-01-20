using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AccessibleArenaInstaller
{
    public class MainForm : Form
    {
        private string _mtgaPath;
        private Label _titleLabel;
        private Label _statusLabel;
        private Label _pathLabel;
        private TextBox _pathTextBox;
        private Button _browseButton;
        private Button _installButton;
        private Button _cancelButton;
        private ProgressBar _progressBar;
        private CheckBox _launchCheckBox;

        public MainForm(string detectedMtgaPath)
        {
            _mtgaPath = detectedMtgaPath;
            InitializeComponents();
            Logger.Info("MainForm initialized");
        }

        private void InitializeComponents()
        {
            // Form settings
            Text = "Accessible Arena Installer";
            Size = new Size(500, 320);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            // Title
            _titleLabel = new Label
            {
                Text = "Accessible Arena Installer",
                Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(440, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Status label
            _statusLabel = new Label
            {
                Text = "This will install the accessibility mod for Magic: The Gathering Arena.\nThe mod enables blind players to play using NVDA screen reader.",
                Location = new Point(20, 60),
                Size = new Size(440, 50),
                TextAlign = ContentAlignment.TopLeft
            };

            // Path label
            _pathLabel = new Label
            {
                Text = "MTGA Installation Path:",
                Location = new Point(20, 120),
                Size = new Size(150, 20)
            };

            // Path text box
            _pathTextBox = new TextBox
            {
                Text = _mtgaPath ?? Program.DefaultMtgaPath,
                Location = new Point(20, 145),
                Size = new Size(350, 25),
                ReadOnly = true
            };

            // Browse button
            _browseButton = new Button
            {
                Text = "Browse...",
                Location = new Point(380, 143),
                Size = new Size(80, 27)
            };
            _browseButton.Click += BrowseButton_Click;

            // Progress bar
            _progressBar = new ProgressBar
            {
                Location = new Point(20, 185),
                Size = new Size(440, 25),
                Style = ProgressBarStyle.Continuous,
                Visible = false
            };

            // Launch checkbox
            _launchCheckBox = new CheckBox
            {
                Text = "Launch MTGA after installation",
                Location = new Point(20, 220),
                Size = new Size(250, 25),
                Checked = false
            };

            // Install button
            _installButton = new Button
            {
                Text = "Install",
                Location = new Point(280, 245),
                Size = new Size(90, 30)
            };
            _installButton.Click += InstallButton_Click;

            // Cancel button
            _cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(380, 245),
                Size = new Size(80, 30)
            };
            _cancelButton.Click += (s, e) => Close();

            // Add controls
            Controls.AddRange(new Control[]
            {
                _titleLabel,
                _statusLabel,
                _pathLabel,
                _pathTextBox,
                _browseButton,
                _progressBar,
                _launchCheckBox,
                _installButton,
                _cancelButton
            });

            // Validate initial path
            ValidatePath();
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select MTGA installation folder (contains MTGA.exe)";
                dialog.ShowNewFolderButton = false;

                if (!string.IsNullOrEmpty(_pathTextBox.Text) && Directory.Exists(_pathTextBox.Text))
                {
                    dialog.SelectedPath = _pathTextBox.Text;
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _pathTextBox.Text = dialog.SelectedPath;
                    _mtgaPath = dialog.SelectedPath;
                    Logger.Info($"User selected path: {_mtgaPath}");
                    ValidatePath();
                }
            }
        }

        private void ValidatePath()
        {
            bool isValid = Program.IsValidMtgaPath(_pathTextBox.Text);
            _installButton.Enabled = isValid;

            if (!isValid && !string.IsNullOrEmpty(_pathTextBox.Text))
            {
                _statusLabel.Text = "MTGA.exe not found in the selected folder.\nPlease select the correct MTGA installation folder.";
                _statusLabel.ForeColor = Color.Red;
            }
            else
            {
                _statusLabel.Text = "This will install the accessibility mod for Magic: The Gathering Arena.\nThe mod enables blind players to play using NVDA screen reader.";
                _statusLabel.ForeColor = SystemColors.ControlText;
            }
        }

        private async void InstallButton_Click(object sender, EventArgs e)
        {
            _mtgaPath = _pathTextBox.Text;

            // Confirm installation
            var result = MessageBox.Show(
                $"Install Accessible Arena to:\n{_mtgaPath}\n\nContinue?",
                "Confirm Installation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            // Disable controls during installation
            SetControlsEnabled(false);
            _progressBar.Visible = true;
            _progressBar.Value = 0;

            using (var githubClient = new GitHubClient())
            {
                try
                {
                    Logger.Info($"Starting installation to: {_mtgaPath}");
                    var installationManager = new InstallationManager(_mtgaPath);
                    var melonLoaderInstaller = new MelonLoaderInstaller(_mtgaPath, githubClient);

                    // Step 1: Copy Tolk DLLs
                    UpdateStatus("Copying screen reader libraries...");
                    UpdateProgress(5);
                    await Task.Run(() => installationManager.CopyTolkDlls());
                    UpdateProgress(15);

                    // Step 2: Check and install MelonLoader
                    bool melonLoaderInstalled = melonLoaderInstaller.IsInstalled();
                    Logger.Info($"MelonLoader installed: {melonLoaderInstalled}");

                    if (!melonLoaderInstalled)
                    {
                        // Ask user if they want to install MelonLoader
                        var mlResult = MessageBox.Show(
                            "MelonLoader is required but not installed.\n\n" +
                            "MelonLoader is a mod loader that allows the accessibility mod to work.\n\n" +
                            "Do you want to download and install MelonLoader now?",
                            "MelonLoader Required",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        if (mlResult == DialogResult.Yes)
                        {
                            UpdateStatus("Installing MelonLoader...");
                            await melonLoaderInstaller.InstallAsync((progress, status) =>
                            {
                                // Map MelonLoader progress (0-100) to overall progress (15-70)
                                int overallProgress = 15 + (progress * 55 / 100);
                                UpdateProgress(overallProgress);
                                UpdateStatus(status);
                            });
                        }
                        else
                        {
                            Logger.Warning("User declined MelonLoader installation");
                            MessageBox.Show(
                                "MelonLoader is required for the accessibility mod to work.\n\n" +
                                "The installer will continue, but the mod will not function until MelonLoader is installed.\n\n" +
                                "You can install MelonLoader manually from:\nhttps://github.com/LavaGang/MelonLoader/releases",
                                "MelonLoader Skipped",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                    }
                    else
                    {
                        Logger.Info("MelonLoader already installed");

                        // Ask user if they want to reinstall or continue
                        var mlResult = MessageBox.Show(
                            "MelonLoader is already installed.\n\n" +
                            "Do you want to reinstall MelonLoader?\n\n" +
                            "Click 'Yes' to reinstall, or 'No' to continue with the existing installation.",
                            "MelonLoader Found",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information);

                        if (mlResult == DialogResult.Yes)
                        {
                            Logger.Info("User chose to reinstall MelonLoader");
                            UpdateStatus("Reinstalling MelonLoader...");
                            await melonLoaderInstaller.InstallAsync((progress, status) =>
                            {
                                int overallProgress = 15 + (progress * 55 / 100);
                                UpdateProgress(overallProgress);
                                UpdateStatus(status);
                            });
                        }
                        else
                        {
                            Logger.Info("User chose to keep existing MelonLoader");
                            UpdateStatus("Keeping existing MelonLoader");
                            UpdateProgress(70);
                        }
                    }

                    // Step 3: Create Mods folder
                    UpdateStatus("Creating Mods folder...");
                    UpdateProgress(72);
                    installationManager.EnsureModsFolderExists();

                    // Step 4: Download and install mod DLL
                    UpdateStatus("Checking mod version...");
                    UpdateProgress(75);

                    bool modInstalled = false;
                    string installedVersion = installationManager.GetInstalledModVersion();
                    string latestVersion = await githubClient.GetLatestModVersionAsync(Config.ModRepositoryUrl);

                    Logger.Info($"Installed mod version: {installedVersion ?? "not installed"}");
                    Logger.Info($"Latest mod version: {latestVersion ?? "unknown"}");

                    bool shouldInstallMod = true;

                    if (installedVersion != null && latestVersion != null)
                    {
                        // Mod is already installed - check if update needed
                        if (IsVersionNewer(latestVersion, installedVersion))
                        {
                            var updateResult = MessageBox.Show(
                                $"A newer version of the mod is available.\n\n" +
                                $"Installed: v{installedVersion}\n" +
                                $"Available: v{latestVersion}\n\n" +
                                "Do you want to update?",
                                "Update Available",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question);

                            shouldInstallMod = (updateResult == DialogResult.Yes);
                        }
                        else
                        {
                            Logger.Info("Mod is up to date");
                            shouldInstallMod = false;
                            modInstalled = true;
                        }
                    }
                    else if (latestVersion == null)
                    {
                        // Could not fetch version - ask user
                        var downloadResult = MessageBox.Show(
                            "Could not check for the latest mod version.\n\n" +
                            "This may be because:\n" +
                            "- No internet connection\n" +
                            "- GitHub repository not found\n" +
                            "- No releases published yet\n\n" +
                            "Do you want to try downloading the mod anyway?",
                            "Version Check Failed",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);

                        shouldInstallMod = (downloadResult == DialogResult.Yes);
                    }

                    if (shouldInstallMod)
                    {
                        try
                        {
                            UpdateStatus("Downloading mod...");
                            string tempModPath = await githubClient.DownloadModDllAsync(
                                Config.ModRepositoryUrl,
                                Config.ModDllName,
                                p =>
                                {
                                    // Map download progress (0-100) to overall progress (75-95)
                                    int overallProgress = 75 + (p * 20 / 100);
                                    UpdateProgress(overallProgress);
                                });

                            UpdateStatus("Installing mod...");
                            UpdateProgress(96);
                            installationManager.InstallModDll(tempModPath);

                            // Clean up temp file
                            try { File.Delete(tempModPath); } catch { }

                            modInstalled = true;
                            Logger.Info("Mod installed successfully");
                        }
                        catch (Exception modEx)
                        {
                            Logger.Error("Failed to download/install mod", modEx);

                            MessageBox.Show(
                                $"Could not download the mod: {modEx.Message}\n\n" +
                                "The installer will continue, but you will need to install the mod manually.\n\n" +
                                $"Download from: {Config.ModRepositoryUrl}/releases\n" +
                                $"Copy {Config.ModDllName} to the Mods folder.",
                                "Mod Download Failed",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                    }

                    // Step 5: Register in Add/Remove Programs
                    UpdateStatus("Registering installation...");
                    string installedModVersion = installationManager.GetInstalledModVersion() ?? latestVersion ?? "1.0.0";
                    RegistryManager.Register(_mtgaPath, installedModVersion);

                    UpdateProgress(100);
                    UpdateStatus("Installation complete!");

                    Logger.Info("Installation completed successfully");

                    // Show completion message with first-launch warning
                    string completionMessage = "Accessible Arena installation complete!\n\n";

                    if (!melonLoaderInstalled)
                    {
                        completionMessage +=
                            "IMPORTANT: The first time you launch MTGA after installing MelonLoader,\n" +
                            "the game will take longer to start (1-2 minutes) while MelonLoader\n" +
                            "generates necessary files. This is normal and only happens once.\n\n";
                    }

                    if (modInstalled)
                    {
                        completionMessage += "The accessibility mod has been installed successfully.";
                    }
                    else
                    {
                        completionMessage +=
                            $"Note: The mod DLL was not installed.\n" +
                            $"Please download it manually from:\n{Config.ModRepositoryUrl}/releases";
                    }

                    MessageBox.Show(
                        completionMessage,
                        "Installation Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    // Ask about saving log file (only prompts if there were errors/warnings)
                    Logger.AskAndSave();

                    if (_launchCheckBox.Checked)
                    {
                        LaunchMtga();
                    }

                    Close();
                }
                catch (Exception ex)
                {
                    Logger.Error("Installation failed", ex);

                    MessageBox.Show(
                        $"Installation failed: {ex.Message}",
                        "Installation Error",
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
        }

        private void UpdateStatus(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateStatus(message)));
                return;
            }

            _statusLabel.Text = message;
            _statusLabel.ForeColor = SystemColors.ControlText;
            Logger.Info(message);
        }

        private void UpdateProgress(int value)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateProgress(value)));
                return;
            }

            _progressBar.Value = Math.Min(value, 100);
        }

        private void SetControlsEnabled(bool enabled)
        {
            _browseButton.Enabled = enabled;
            _installButton.Enabled = enabled;
            _pathTextBox.Enabled = enabled;
            _launchCheckBox.Enabled = enabled;
        }

        private void LaunchMtga()
        {
            try
            {
                string exePath = Path.Combine(_mtgaPath, Program.MtgaExeName);
                if (File.Exists(exePath))
                {
                    Logger.Info($"Launching MTGA: {exePath}");
                    System.Diagnostics.Process.Start(exePath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to launch MTGA", ex);
            }
        }

        /// <summary>
        /// Compares two version strings to determine if the new version is newer.
        /// </summary>
        private bool IsVersionNewer(string newVersion, string oldVersion)
        {
            try
            {
                // Clean up version strings (remove 'v' prefix, extra text after version)
                newVersion = CleanVersionString(newVersion);
                oldVersion = CleanVersionString(oldVersion);

                var newVer = new Version(newVersion);
                var oldVer = new Version(oldVersion);

                return newVer > oldVer;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not compare versions '{newVersion}' and '{oldVersion}': {ex.Message}");
                // If we can't parse versions, assume update is available
                return newVersion != oldVersion;
            }
        }

        /// <summary>
        /// Cleans a version string for parsing.
        /// </summary>
        private string CleanVersionString(string version)
        {
            if (string.IsNullOrEmpty(version))
                return "0.0.0";

            // Remove 'v' prefix
            if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                version = version.Substring(1);

            // Take only the version part (before any dash or space)
            int dashIndex = version.IndexOf('-');
            if (dashIndex > 0)
                version = version.Substring(0, dashIndex);

            int spaceIndex = version.IndexOf(' ');
            if (spaceIndex > 0)
                version = version.Substring(0, spaceIndex);

            return version.Trim();
        }
    }
}
