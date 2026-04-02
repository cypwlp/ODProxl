using Avalonia.Threading;
using ODProxl.EntityModels;
using ODProxl.Utils;
using Prism.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;
using File = System.IO.File;

namespace ODProxl.Services.impls
{
    public class UpdateService : IUpdateService
    {
        private readonly IDialogService _dialogService;
        private readonly HttpClient _httpClient;

        public UpdateService(IDialogService dialogService, HttpClient httpClient)
        {
            _dialogService = dialogService;
            _httpClient = httpClient;
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ODProxl/UpdateService");
        }

        public async Task UpdateODProxlAsync(string countryCode)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine("[Debug 模式] 已跳過 Velopack 更新檢查");
                return;
            }

            if (countryCode == "CN")
            {
                //await CheckAndUpdateForChinaAsync();
                await DLLUpdateAsync();
            }
            else
            {
                await CheckAndUpdateForGitHubAsync();
            }
        }

        // ==================== 國內 Velopack 更新（完整保留）====================
        private async Task CheckAndUpdateForChinaAsync()
        {
            try
            {
                string rid = RuntimeInformation.RuntimeIdentifier;
                Console.WriteLine($"[Velopack CN] 開始檢查更新，RuntimeIdentifier: {rid}");

                string latestVersion = await GetLatestVersionFromGitHubAsync();
                if (string.IsNullOrEmpty(latestVersion))
                {
                    Console.WriteLine("[Velopack CN] 無法取得最新版本號，跳過更新");
                    return;
                }

                string baseUrl = $"http://interior.topmix.net/info/system/software/ODProxl/{latestVersion}/";
                Console.WriteLine($"[Velopack CN] baseUrl = {baseUrl}");

                var options = new UpdateOptions { ExplicitChannel = rid };
                var mgr = new UpdateManager(baseUrl, options);

                Console.WriteLine($"[Velopack CN] 目前安裝版本: {mgr.CurrentVersion}");

                var updateInfo = await mgr.CheckForUpdatesAsync();
                if (updateInfo == null)
                {
                    Console.WriteLine($"[Velopack CN] 目前已是最新版本");
                    return;
                }

                Console.WriteLine($"[Velopack CN] 發現新版本 {updateInfo.TargetFullRelease?.Version}");

                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var parameters = new DialogParameters { { "UpdateInfo", updateInfo } };
                    var result = await _dialogService.ShowDialogAsync("UpdateDialog", parameters);
                    if (result?.Result == ButtonResult.OK)
                    {
                        await mgr.DownloadUpdatesAsync(updateInfo);
                        mgr.ApplyUpdatesAndRestart(updateInfo);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Velopack CN] 更新檢查失敗：{ex.Message}");
            }
        }

        // ==================== GitHub 更新（國外）====================
        private async Task CheckAndUpdateForGitHubAsync()
        {
            try
            {
                var source = new GithubSource("https://github.com/cypwlp/ODProxl", "", false);
                var mgr = new UpdateManager(source);
                var updateInfo = await mgr.CheckForUpdatesAsync();

                if (updateInfo == null)
                {
                    Console.WriteLine("[Velopack GitHub] 目前已是最新版本");
                    return;
                }

                Console.WriteLine($"[Velopack GitHub] 發現新版本 {updateInfo.TargetFullRelease?.Version}");

                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var parameters = new DialogParameters { { "UpdateInfo", updateInfo } };
                    var result = await _dialogService.ShowDialogAsync("UpdateDialog", parameters);
                    if (result?.Result == ButtonResult.OK)
                    {
                        await mgr.DownloadUpdatesAsync(updateInfo);
                        mgr.ApplyUpdatesAndRestart(updateInfo);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Velopack GitHub] 更新檢查失敗：{ex.Message}");
            }
        }

        // ==================== 獨立 DLL 更新 ========================
        private async Task DLLUpdateAsync()
        {
            string rid = PlatformHelper.GetCurrentRid();
            string startupPath = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            string dllUpdateBaseUrl = $"http://interior.topmix.net/info/system/software/ODProxl/DLLUpdater/{rid}/";

            try
            {
                Console.WriteLine($"[DLL Update] 開始檢查 DLL 更新，平台: {rid}，路徑: {dllUpdateBaseUrl}");

                string manifestUrl = $"{dllUpdateBaseUrl}dlls.json";
                var response = await _httpClient.GetAsync(manifestUrl);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                var manifest = JsonSerializer.Deserialize<DllManifest>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (manifest?.Dlls == null || manifest.Dlls.Count == 0)
                {
                    Console.WriteLine("[DLL Update] 清單為空，跳過");
                    return;
                }

                var updateList = new List<DllInfo>();
                foreach (var dll in manifest.Dlls)
                {
                    string localPath = Path.Combine(startupPath, dll.FileName);
                    string? dir = Path.GetDirectoryName(localPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    bool needsUpdate = true;
                    if (File.Exists(localPath))
                    {
                        string localHash = await ComputeFileHashAsync(localPath);
                        if (localHash.Equals(dll.Hash, StringComparison.OrdinalIgnoreCase))
                            needsUpdate = false;
                    }

                    if (needsUpdate)
                        updateList.Add(dll);
                }

                if (updateList.Count == 0)
                {
                    Console.WriteLine("[DLL Update] 所有 DLL 均為最新版本");
                    return;
                }

                bool shouldUpdate = false;
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var parameters = new DialogParameters
                    {
                        { "DllUpdateList", updateList },
                        { "Version", manifest.Version }
                    };
                    var result = await _dialogService.ShowDialogAsync("UpdateDialog", parameters);
                    shouldUpdate = result?.Result == ButtonResult.OK;
                });

                if (!shouldUpdate) return;

                foreach (var dll in updateList)
                {
                    string downloadUrl = string.IsNullOrEmpty(dll.Url) ? $"{dllUpdateBaseUrl}{dll.FileName}" : dll.Url;
                    string localPath = Path.Combine(startupPath, dll.FileName);
                    string tempPath = localPath + ".tmp";

                    var dllResponse = await _httpClient.GetAsync(downloadUrl);
                    dllResponse.EnsureSuccessStatusCode();

                    await using (var fs = new FileStream(tempPath, FileMode.Create))
                    {
                        await dllResponse.Content.CopyToAsync(fs);
                    }

                    // === 修正點 1：使用 string.Equals ===
                    if (!string.Equals(await ComputeFileHashAsync(tempPath), dll.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(tempPath);
                        continue;
                    }

                    if (File.Exists(localPath))
                        try { File.Delete(localPath); } catch { File.Move(localPath, localPath + ".bak", true); }

                    File.Move(tempPath, localPath);
                    Console.WriteLine($"[DLL Update] {dll.FileName} 更新完成");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DLL Update] 更新失敗：{ex.Message}");
            }
        }

        // ==================== DLL 上傳核心方法 ========================
        public async Task<bool> PublishNewDllVersionAsync(string version, string dllFilePaths,
            string updateDescription, string codeDescription, string targetRid)
        {
            if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(targetRid))
            {
                Console.WriteLine("[DLL Upload] 版本號或目標平台不能為空");
                return false;
            }

            string baseUrl = $"http://interior.topmix.net/info/system/software/ODProxl/DLLUpdater/{targetRid}/";

            string manifestUrl = $"{baseUrl}dlls.json";
            Console.WriteLine($"[DLL Upload] 開始發布版本 {version} → 平台: {targetRid}，路徑: {baseUrl}");

            DllManifest manifest = await GetOrCreateManifestAsync(manifestUrl);

            var paths = dllFilePaths.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();

            string localManifestPath = paths.FirstOrDefault(p => p.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

            if (localManifestPath != null && File.Exists(localManifestPath))
            {
                string json = await File.ReadAllTextAsync(localManifestPath);
                manifest = JsonSerializer.Deserialize<DllManifest>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new DllManifest();
                manifest.Version = version;
            }
            else
            {
                manifest.Version = version;
                foreach (var localPath in paths.Where(File.Exists))
                {
                    await ProcessAndUploadSingleDllAsync(localPath, manifest, baseUrl);
                }
            }

            await UploadManifestAsync(manifestUrl, manifest);
            Console.WriteLine($"[DLL Upload] ✅ 版本 {version} 已成功發布到 {targetRid}！");
            return true;
        }

        private async Task<DllManifest> GetOrCreateManifestAsync(string manifestUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(manifestUrl);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<DllManifest>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new DllManifest();
                }
            }
            catch { }
            return new DllManifest();
        }

        private async Task ProcessAndUploadSingleDllAsync(string localPath, DllManifest manifest, string baseUrl)
        {
            string fileName = Path.GetFileName(localPath);
            string hash = await ComputeFileHashAsync(localPath);
            long size = new FileInfo(localPath).Length;

            var dllInfo = new DllInfo
            {
                FileName = fileName,
                Hash = hash,
                Size = size,
                Url = string.Empty
            };

            manifest.Dlls.RemoveAll(d => d.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            manifest.Dlls.Add(dllInfo);

            await UploadFileAsync($"{baseUrl}{fileName}", localPath);
            Console.WriteLine($"[DLL Upload] 已上傳 {fileName}");
        }

        private async Task UploadFileAsync(string url, string localFilePath)
        {
            using var fileStream = File.OpenRead(localFilePath);
            using var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PutAsync(url, content);
            response.EnsureSuccessStatusCode();
        }

        private async Task UploadManifestAsync(string url, DllManifest manifest)
        {
            string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(url, content);
            response.EnsureSuccessStatusCode();
        }

        // ==================== 修正點 2：改為非靜態方法 ========================
        private async Task<string> GetLatestVersionFromGitHubAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("https://api.github.com/repos/cypwlp/ODProxl/releases/latest");
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("tag_name", out var tag))
                {
                    string version = tag.GetString() ?? "";
                    return version.StartsWith("v") ? version.Substring(1) : version;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetLatestVersion] 失敗：{ex.Message}");
            }
            return string.Empty;
        }

        private static async Task<string> ComputeFileHashAsync(string filePath)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            await using var stream = File.OpenRead(filePath);
            byte[] hash = await sha256.ComputeHashAsync(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}