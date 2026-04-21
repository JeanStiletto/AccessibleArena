namespace AccessibleArenaInstaller
{
    /// <summary>
    /// Configuration constants for the installer.
    /// Update these values before building a release.
    /// </summary>
    public static class Config
    {
        /// <summary>
        /// GitHub repository URL for Accessible Arena.
        /// Format: "https://github.com/username/repo"
        /// </summary>
        public const string ModRepositoryUrl = "https://github.com/JeanStiletto/AccessibleArena";

        /// <summary>
        /// The filename of the mod DLL in GitHub releases.
        /// </summary>
        public const string ModDllName = "AccessibleArena.dll";

        /// <summary>
        /// Publisher name for registry entries.
        /// </summary>
        public const string Publisher = "Accessible Arena";

        /// <summary>
        /// Display name for Add/Remove Programs.
        /// </summary>
        public const string DisplayName = "Accessible Arena";

        /// <summary>
        /// Filename of the persistent uninstaller copied into the MTGA folder
        /// at install time so Add/Remove Programs keeps working after the
        /// original download is deleted.
        /// </summary>
        public const string UninstallerExeName = "AccessibleArena_Uninstaller.exe";
    }
}
