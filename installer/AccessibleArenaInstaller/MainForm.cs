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
        private bool _updateOnly;
        private string _language;
        private Label _titleLabel;
        private Label _statusLabel;
        private Label _pathLabel;
        private TextBox _pathTextBox;
        private Button _browseButton;
        private Button _installButton;
        private Button _cancelButton;
        private ProgressBar _progressBar;
        private CheckBox _launchCheckBox;
        private CheckBox _readmeCheckBox;

        public MainForm(string detectedMtgaPath, bool updateOnly = false, string language = null)
        {
            _mtgaPath = detectedMtgaPath;
            _updateOnly = updateOnly;
            _language = language;
            InitializeComponents();
            Logger.Info($"MainForm initialized (updateOnly: {updateOnly}, language: {language ?? "none"})");
        }

        private void InitializeComponents()
        {
            // Form settings
            Text = InstallerLocale.Get(_updateOnly ? "Main_TitleUpdater" : "Main_TitleInstaller");
            Size = new Size(500, 350);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            // Title
            _titleLabel = new Label
            {
                Text = InstallerLocale.Get(_updateOnly ? "Main_TitleUpdater" : "Main_TitleInstaller"),
                Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(440, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Status label
            _statusLabel = new Label
            {
                Text = InstallerLocale.Get(_updateOnly ? "Main_StatusUpdate" : "Main_StatusInstall"),
                Location = new Point(20, 60),
                Size = new Size(440, 50),
                TextAlign = ContentAlignment.TopLeft
            };

            // Path label
            _pathLabel = new Label
            {
                Text = InstallerLocale.Get("Main_PathLabel"),
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
                Text = InstallerLocale.Get("Main_BrowseButton"),
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
                Text = InstallerLocale.Get("Main_LaunchCheckBox"),
                Location = new Point(20, 220),
                Size = new Size(250, 25),
                Checked = false
            };

            // Readme checkbox
            _readmeCheckBox = new CheckBox
            {
                Text = InstallerLocale.Get("Main_ReadmeCheckBox"),
                Location = new Point(20, 245),
                Size = new Size(350, 25),
                Checked = true
            };

            // Install button
            _installButton = new Button
            {
                Text = InstallerLocale.Get(_updateOnly ? "Main_UpdateButton" : "Main_InstallButton"),
                Location = new Point(280, 275),
                Size = new Size(90, 30)
            };
            _installButton.Click += InstallButton_Click;

            // Cancel button
            _cancelButton = new Button
            {
                Text = InstallerLocale.Get("Main_CancelButton"),
                Location = new Point(380, 275),
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
                _readmeCheckBox,
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
                dialog.Description = InstallerLocale.Get("Main_BrowseDialogDescription");
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
                _statusLabel.Text = InstallerLocale.Get("Main_PathNotFound");
                _statusLabel.ForeColor = Color.Red;
            }
            else
            {
                _statusLabel.Text = InstallerLocale.Get(_updateOnly ? "Main_StatusUpdate" : "Main_StatusInstall");
                _statusLabel.ForeColor = SystemColors.ControlText;
            }
        }

        private async void InstallButton_Click(object sender, EventArgs e)
        {
            _mtgaPath = _pathTextBox.Text;

            // Confirm installation
            string confirmMessage = InstallerLocale.Format(
                _updateOnly ? "Main_ConfirmUpdate_Format" : "Main_ConfirmInstall_Format", _mtgaPath);
            string confirmTitle = InstallerLocale.Get(
                _updateOnly ? "Main_ConfirmUpdate_Title" : "Main_ConfirmInstall_Title");

            var result = MessageBox.Show(
                confirmMessage,
                confirmTitle,
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
                    Logger.Info($"Starting {(_updateOnly ? "update" : "installation")} to: {_mtgaPath}");
                    var installationManager = new InstallationManager(_mtgaPath);
                    bool melonLoaderInstalled = true; // Assume installed for update mode

                    if (_updateOnly)
                    {
                        // Update mode - skip Tolk DLLs and MelonLoader
                        UpdateStatus(InstallerLocale.Get("Main_StatusPreparing"));
                        UpdateProgress(70);
                    }
                    else
                    {
                        // Full install mode
                        var melonLoaderInstaller = new MelonLoaderInstaller(_mtgaPath, githubClient);

                        // Step 1: Copy Tolk DLLs
                        UpdateStatus(InstallerLocale.Get("Main_StatusCopyingLibraries"));
                        UpdateProgress(5);
                        await Task.Run(() => installationManager.CopyTolkDlls());
                        UpdateProgress(15);

                        // Step 2: Check and install MelonLoader
                        melonLoaderInstalled = melonLoaderInstaller.IsInstalled();
                        Logger.Info($"MelonLoader installed: {melonLoaderInstalled}");

                        if (!melonLoaderInstalled)
                        {
                            // Ask user if they want to install MelonLoader
                            var mlResult = MessageBox.Show(
                                InstallerLocale.Get("Main_MelonRequired_Text"),
                                InstallerLocale.Get("Main_MelonRequired_Title"),
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question);

                            if (mlResult == DialogResult.Yes)
                            {
                                UpdateStatus(InstallerLocale.Get("Main_StatusInstallingMelonLoader"));
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
                                    InstallerLocale.Get("Main_MelonSkipped_Text"),
                                    InstallerLocale.Get("Main_MelonSkipped_Title"),
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);
                            }
                        }
                        else
                        {
                            Logger.Info("MelonLoader already installed");

                            // Ask user if they want to reinstall or continue
                            var mlResult = MessageBox.Show(
                                InstallerLocale.Get("Main_MelonFound_Text"),
                                InstallerLocale.Get("Main_MelonFound_Title"),
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Information);

                            if (mlResult == DialogResult.Yes)
                            {
                                Logger.Info("User chose to reinstall MelonLoader");
                                UpdateStatus(InstallerLocale.Get("Main_StatusReinstallingMelonLoader"));
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
                                UpdateStatus(InstallerLocale.Get("Main_StatusKeepingMelonLoader"));
                                UpdateProgress(70);
                            }
                        }

                        // Step 3: Create Mods folder
                        UpdateStatus(InstallerLocale.Get("Main_StatusCreatingMods"));
                        UpdateProgress(72);
                        installationManager.EnsureModsFolderExists();
                    }

                    // Step 4: Download and install mod DLL
                    UpdateStatus(InstallerLocale.Get("Main_StatusCheckingVersion"));
                    UpdateProgress(75);

                    bool modInstalled = false;
                    bool shouldInstallMod = true;
                    string latestVersion = null;

                    if (_updateOnly)
                    {
                        // User already confirmed update from the Update Available dialog.
                        // Still fetch latest version tag for registry registration.
                        Logger.Info("Update mode - skipping version re-check, downloading latest");
                        try
                        {
                            latestVersion = await githubClient.GetLatestModVersionAsync(Config.ModRepositoryUrl);
                            Logger.Info($"Latest mod version for registry: {latestVersion ?? "unknown"}");
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"Could not fetch latest version: {ex.Message}");
                        }
                    }
                    else
                    {
                        // Full install/reinstall - user already confirmed in Program.cs.
                        // Just fetch latest version for registry registration.
                        try
                        {
                            latestVersion = await githubClient.GetLatestModVersionAsync(Config.ModRepositoryUrl);
                            Logger.Info($"Latest mod version for registry: {latestVersion ?? "unknown"}");
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"Could not fetch latest version: {ex.Message}");
                        }

                        if (latestVersion == null)
                        {
                            // Could not fetch version - ask user
                            var downloadResult = MessageBox.Show(
                                InstallerLocale.Get("Main_VersionCheckFailed_Text"),
                                InstallerLocale.Get("Main_VersionCheckFailed_Title"),
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Warning);

                            shouldInstallMod = (downloadResult == DialogResult.Yes);
                        }
                    }

                    if (shouldInstallMod)
                    {
                        try
                        {
                            UpdateStatus(InstallerLocale.Get("Main_StatusDownloading"));
                            string tempModPath = await githubClient.DownloadModDllAsync(
                                Config.ModRepositoryUrl,
                                Config.ModDllName,
                                p =>
                                {
                                    // Map download progress (0-100) to overall progress (75-95)
                                    int overallProgress = 75 + (p * 20 / 100);
                                    UpdateProgress(overallProgress);
                                });

                            UpdateStatus(InstallerLocale.Get("Main_StatusInstalling"));
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
                                InstallerLocale.Format("Main_ModDownloadFailed_Format",
                                    modEx.Message, Config.ModRepositoryUrl, Config.ModDllName),
                                InstallerLocale.Get("Main_ModDownloadFailed_Title"),
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                    }

                    // Step 5: Write mod settings with selected language
                    if (_language != null)
                    {
                        UpdateStatus(InstallerLocale.Get("Main_StatusConfiguringLanguage"));
                        installationManager.WriteModSettings(_language);
                    }

                    // Step 6: Hide MelonLoader console window
                    installationManager.ConfigureMelonLoaderConsole();

                    // Step 7: Register in Add/Remove Programs
                    UpdateStatus(InstallerLocale.Get("Main_StatusRegistering"));
                    string installedModVersion = latestVersion ?? installationManager.GetInstalledModVersion() ?? "1.0.0";
                    RegistryManager.Register(_mtgaPath, installedModVersion);

                    UpdateProgress(100);
                    UpdateStatus(InstallerLocale.Get(_updateOnly ? "Main_StatusUpdateComplete" : "Main_StatusInstallComplete"));

                    Logger.Info($"{(_updateOnly ? "Update" : "Installation")} completed successfully");

                    // Show completion message with first-launch warning
                    string completionMessage = InstallerLocale.Get(
                        _updateOnly ? "Main_CompleteUpdate_Text" : "Main_CompleteInstall_Text");

                    if (!_updateOnly && !melonLoaderInstalled)
                    {
                        completionMessage += InstallerLocale.Get("Main_CompleteFirstLaunch");
                    }

                    if (modInstalled)
                    {
                        completionMessage += InstallerLocale.Get(
                            _updateOnly ? "Main_CompleteModUpdated" : "Main_CompleteModInstalled");
                    }
                    else
                    {
                        completionMessage += InstallerLocale.Format(
                            "Main_CompleteModNotInstalled_Format", Config.ModRepositoryUrl);
                    }

                    MessageBox.Show(
                        completionMessage,
                        InstallerLocale.Get(_updateOnly ? "Main_CompleteUpdate_Title" : "Main_CompleteInstall_Title"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    // Ask about saving log file (only prompts if there were errors/warnings)
                    Logger.AskAndSave();

                    if (_readmeCheckBox.Checked)
                    {
                        OpenReadme();
                    }

                    if (_launchCheckBox.Checked)
                    {
                        LaunchMtga();
                    }

                    Close();
                }
                catch (Exception ex)
                {
                    Logger.Error($"{(_updateOnly ? "Update" : "Installation")} failed", ex);

                    MessageBox.Show(
                        InstallerLocale.Format(
                            _updateOnly ? "Main_ErrorUpdate_Format" : "Main_ErrorInstall_Format", ex.Message),
                        InstallerLocale.Get(_updateOnly ? "Main_ErrorUpdate_Title" : "Main_ErrorInstall_Title"),
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
            _readmeCheckBox.Enabled = enabled;
        }

        private void OpenReadme()
        {
            try
            {
                string url;
                if (_language == null || _language == "en")
                    url = $"{Config.ModRepositoryUrl}/blob/main/README.md";
                else
                    url = $"{Config.ModRepositoryUrl}/blob/main/docs/README.{_language}.md";

                Logger.Info($"Opening README: {url}");
                System.Diagnostics.Process.Start(url);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to open README", ex);
            }
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
        /// Delegates to Program.IsNewerVersion for consistent behavior.
        /// </summary>
        private bool IsVersionNewer(string newVersion, string oldVersion)
        {
            return Program.IsNewerVersion(newVersion, oldVersion);
        }
    }
}
