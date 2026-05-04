using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SHALLControl.Services
{
    public class UpdateService
    {
        private const string REPO_OWNER = "StalinoAJ";
        private const string REPO_NAME = "XShall_Telemetry_app";
        private const string CURRENT_VERSION = "v1.1"; // This is your current version
        private readonly HttpClient _httpClient;

        public UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SHALLControl-Updater");
        }

        public async Task CheckAndUpdateAsync()
        {
            try
            {
                // 1. Check for latest release
                var url = $"https://api.github.com/repos/{REPO_OWNER}/{REPO_NAME}/releases/latest";
                var response = await _httpClient.GetStringAsync(url);
                
                // Very basic JSON parsing without external dependencies
                string tagName = ExtractJsonValue(response, "tag_name");
                string releaseName = ExtractJsonValue(response, "name");
                
                // If it's a newer version (e.g. v1.2 vs v1.1)
                // Here we just do a simple string comparison or check if names are different
                // Adjust logic as necessary to match your versioning scheme
                if (releaseName != null && releaseName != CURRENT_VERSION && releaseName != "v1.0") 
                {
                    // Find the browser_download_url for the zip file
                    string downloadUrl = ExtractJsonValue(response, "browser_download_url");
                    if (string.IsNullOrEmpty(downloadUrl)) return;
                    
                    var result = MessageBox.Show(
                        $"A new update ({releaseName}) is available!\nDo you want to download and install it now?", 
                        "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                        
                    if (result == DialogResult.Yes)
                    {
                        await PerformUpdateAsync(downloadUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Update check failed: " + ex.Message);
            }
        }

        private async Task PerformUpdateAsync(string downloadUrl)
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "SHALLControlUpdate");
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);

                string zipPath = Path.Combine(tempDir, "update.zip");

                // Download the update
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "SHALLControl-Updater");
                    await client.DownloadFileTaskAsync(new Uri(downloadUrl), zipPath);
                }

                // Extract the zip
                string extractDir = Path.Combine(tempDir, "Extracted");
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                // Create an updater batch script
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
                MessageBox.Show("Failed to apply update: " + ex.Message, "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
