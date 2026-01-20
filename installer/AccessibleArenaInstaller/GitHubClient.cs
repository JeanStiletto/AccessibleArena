using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AccessibleArenaInstaller
{
    /// <summary>
    /// Handles downloading files from GitHub releases.
    /// </summary>
    public class GitHubClient : IDisposable
    {
        private readonly HttpClient _httpClient;

        // MelonLoader releases
        private const string MelonLoaderReleasesUrl = "https://github.com/LavaGang/MelonLoader/releases/latest";
        private const string MelonLoaderDownloadPattern = "https://github.com/LavaGang/MelonLoader/releases/download/{0}/MelonLoader.x64.zip";

        // Accessible Arena releases - note: actual URL comes from Config.ModRepositoryUrl
        private const string ModReleasesUrl = "https://github.com/JeanStiletto/AccessibleArena/releases/latest";

        public GitHubClient()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AccessibleArenaInstaller/1.0");
            _httpClient.Timeout = TimeSpan.FromMinutes(5); // Allow time for large downloads
        }

        /// <summary>
        /// Gets the latest MelonLoader version tag from GitHub.
        /// </summary>
        public async Task<string> GetLatestMelonLoaderVersionAsync()
        {
            try
            {
                Logger.Info("Fetching latest MelonLoader version...");

                // GitHub redirects /releases/latest to /releases/tag/vX.X.X
                // We can use HEAD request to get the redirect URL
                var request = new HttpRequestMessage(HttpMethod.Head, MelonLoaderReleasesUrl);
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                // The final URL will contain the version tag
                string finalUrl = response.RequestMessage.RequestUri.ToString();

                // Extract version from URL like: https://github.com/LavaGang/MelonLoader/releases/tag/v0.6.6
                var match = Regex.Match(finalUrl, @"/tag/(v[\d.]+)");
                if (match.Success)
                {
                    string version = match.Groups[1].Value;
                    Logger.Info($"Latest MelonLoader version: {version}");
                    return version;
                }

                Logger.Warning($"Could not parse version from URL: {finalUrl}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get MelonLoader version", ex);
                return null;
            }
        }

        /// <summary>
        /// Downloads the MelonLoader x64 ZIP to a temporary file.
        /// </summary>
        /// <param name="version">Version tag (e.g., "v0.6.6")</param>
        /// <param name="progress">Optional progress callback (0-100)</param>
        /// <returns>Path to downloaded ZIP file</returns>
        public async Task<string> DownloadMelonLoaderAsync(string version, Action<int> progress = null)
        {
            string downloadUrl = string.Format(MelonLoaderDownloadPattern, version);
            Logger.Info($"Downloading MelonLoader from: {downloadUrl}");

            string tempFile = Path.Combine(Path.GetTempPath(), $"MelonLoader_{version}.zip");

            try
            {
                await DownloadFileAsync(downloadUrl, tempFile, progress);
                Logger.Info($"MelonLoader downloaded to: {tempFile}");
                return tempFile;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to download MelonLoader", ex);
                throw;
            }
        }

        /// <summary>
        /// Downloads a file from URL to local path with progress reporting.
        /// </summary>
        public async Task DownloadFileAsync(string url, string destinationPath, Action<int> progress = null)
        {
            using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                long? totalBytes = response.Content.Headers.ContentLength;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    long totalRead = 0;
                    int bytesRead;
                    int lastReportedProgress = -1;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;

                        if (totalBytes.HasValue && progress != null)
                        {
                            int currentProgress = (int)((totalRead * 100) / totalBytes.Value);
                            if (currentProgress != lastReportedProgress)
                            {
                                progress(currentProgress);
                                lastReportedProgress = currentProgress;
                            }
                        }
                    }
                }
            }

            Logger.Info($"Download complete: {destinationPath}");
        }

        /// <summary>
        /// Gets the latest mod version from GitHub releases.
        /// </summary>
        /// <param name="repoUrl">GitHub repo URL</param>
        /// <returns>Version string (e.g., "1.0.0") or null if not found</returns>
        public async Task<string> GetLatestModVersionAsync(string repoUrl)
        {
            try
            {
                string apiUrl = repoUrl.Replace("github.com", "api.github.com/repos") + "/releases/latest";
                Logger.Info($"Fetching latest mod version from: {apiUrl}");

                var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                request.Headers.Add("Accept", "application/vnd.github.v3+json");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warning($"GitHub API returned {response.StatusCode}");
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync();

                // Extract tag_name from JSON (e.g., "tag_name": "v1.0.0")
                var match = Regex.Match(json, @"""tag_name""\s*:\s*""v?([^""]+)""");
                if (match.Success)
                {
                    string version = match.Groups[1].Value;
                    Logger.Info($"Latest mod version: {version}");
                    return version;
                }

                Logger.Warning("Could not parse version from GitHub API response");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get mod version", ex);
                return null;
            }
        }

        /// <summary>
        /// Downloads the latest mod DLL from GitHub releases.
        /// </summary>
        /// <param name="repoUrl">GitHub repo URL (e.g., "https://github.com/user/repo")</param>
        /// <param name="assetName">Asset filename to download (e.g., "AccessibleArena.dll")</param>
        /// <param name="progress">Optional progress callback</param>
        /// <returns>Path to downloaded file</returns>
        public async Task<string> DownloadModDllAsync(string repoUrl, string assetName, Action<int> progress = null)
        {
            try
            {
                // Get latest release info via GitHub API
                string apiUrl = repoUrl.Replace("github.com", "api.github.com/repos") + "/releases/latest";
                Logger.Info($"Fetching release info from: {apiUrl}");

                var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                request.Headers.Add("Accept", "application/vnd.github.v3+json");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();

                // Simple JSON parsing to find the asset download URL
                // Looking for: "browser_download_url": "...AccessibleArena.dll"
                string pattern = $"\"browser_download_url\"\\s*:\\s*\"([^\"]*{Regex.Escape(assetName)}[^\"]*)\"";
                var match = Regex.Match(json, pattern);

                if (!match.Success)
                {
                    throw new Exception($"Asset '{assetName}' not found in latest release.\n\nMake sure the mod DLL is uploaded to the GitHub release.");
                }

                string downloadUrl = match.Groups[1].Value;
                Logger.Info($"Downloading mod from: {downloadUrl}");

                string tempFile = Path.Combine(Path.GetTempPath(), assetName);
                await DownloadFileAsync(downloadUrl, tempFile, progress);

                return tempFile;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to download mod DLL", ex);
                throw;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
