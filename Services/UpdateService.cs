using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SHALLControl.Services
{
    public class UpdateService
    {
        private const string REPO_OWNER = "StalinoAJ";
        private const string REPO_NAME = "XShall_Telemetry_app";
        private readonly HttpClient _httpClient;

        // Events for UI progress reporting
        public event Action<string, string> UpdateAvailable;      // (version, downloadUrl)
        public event Action<int, long, long> DownloadProgress;    // (percent, received, total)
        public event Action<string> StatusChanged;                // status message
        public event Action<string> UpdateFailed;                 // error message
        public event Action UpdateCompleting;                     // about to restart

        public UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SHALLControl-Updater");
        }

        public async Task CheckAndUpdateAsync()
        {
            try
            {
                StatusChanged?.Invoke("Checking for updates…");

                var url = $"https://api.github.com/repos/{REPO_OWNER}/{REPO_NAME}/releases/latest";
                var response = await _httpClient.GetStringAsync(url);

                string tagName = ExtractJsonValue(response, "tag_name");
                string releaseName = ExtractJsonValue(response, "name");

                string currentVersion = Application.ProductVersion; // e.g. "1.1.3.0"
                string remoteClean = (releaseName ?? tagName ?? "").TrimStart('v', 'V').Trim();

                if (string.IsNullOrEmpty(remoteClean)) return;

                if (!IsNewerVersion(remoteClean, currentVersion)) return;

                string downloadUrl = ExtractJsonValue(response, "browser_download_url");
                if (string.IsNullOrEmpty(downloadUrl)) return;

                // Notify that an update is available
                UpdateAvailable?.Invoke(releaseName ?? tagName, downloadUrl);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Update check failed: " + ex.Message);
            }
        }

        private bool IsNewerVersion(string remote, string local)
        {
            try
            {
                // Normalize: strip trailing zeros for comparison
                // remote might be "1.1.3", local might be "1.1.3.0"
                var rv = new Version(NormalizeVersion(remote));
                var lv = new Version(NormalizeVersion(local));
                return rv > lv;
            }
            catch
            {
                // Fallback: string comparison
                return remote != local && remote != local.TrimEnd('.', '0');
            }
        }

        private string NormalizeVersion(string v)
        {
            v = v.TrimStart('v', 'V').Trim();
            var parts = v.Split('.');
            // Ensure at least 2 parts for Version class
            if (parts.Length == 1) return v + ".0";
            return v;
        }

        public async Task PerformUpdateAsync(string downloadUrl)
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "SHALLControlUpdate");
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);

                string zipPath = Path.Combine(tempDir, "update.zip");

                // Download with progress
                StatusChanged?.Invoke("Downloading update…");

                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "SHALLControl-Updater");
                    client.DownloadProgressChanged += (s, e) =>
                        DownloadProgress?.Invoke(e.ProgressPercentage, e.BytesReceived, e.TotalBytesToReceive);

                    await client.DownloadFileTaskAsync(new Uri(downloadUrl), zipPath);
                }

                // Extract
                StatusChanged?.Invoke("Extracting files…");
                DownloadProgress?.Invoke(100, 0, 0); // Keep bar full

                string extractDir = Path.Combine(tempDir, "Extracted");
                await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, extractDir));

                // Prepare updater script
                StatusChanged?.Invoke("Preparing installation…");

                string batPath = Path.Combine(tempDir, "updater.bat");
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string exeName = AppDomain.CurrentDomain.FriendlyName;

                string batContent = $@"
@echo off
timeout /t 2 /nobreak > nul
xcopy ""{extractDir}\*"" ""{appDir}"" /E /Y /V
start """" ""{Path.Combine(appDir, exeName)}""
del ""%~f0""
";
                File.WriteAllText(batPath, batContent);

                // Signal UI we're about to restart
                StatusChanged?.Invoke("Restarting application…");
                UpdateCompleting?.Invoke();

                await Task.Delay(800); // Brief pause so user sees the final status

                // Run the batch file
                var psi = new ProcessStartInfo
                {
                    FileName = batPath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);

                // Exit current application
                Application.Exit();
            }
            catch (Exception ex)
            {
                UpdateFailed?.Invoke("Update failed: " + ex.Message);
            }
        }

        private string ExtractJsonValue(string json, string key)
        {
            string search = "\"" + key + "\":";
            int idx = json.IndexOf(search);
            if (idx < 0) return null;

            idx += search.Length;
            // Skip spaces
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t')) idx++;

            if (json[idx] == '"')
            {
                idx++;
                int endIdx = json.IndexOf('"', idx);
                if (endIdx > idx)
                {
                    return json.Substring(idx, endIdx - idx);
                }
            }
            return null;
        }
    }
}
