using Avalonia.Threading;
using ODProxl.EntityModels;
using ODProxl.Utils;
using Prism.Dialogs;
using System;
using System.Dynamic;
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
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        public UpdateService(IDialogService dialogService)
        {
            _dialogService = dialogService;
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ODProxl/UpdateChecker");
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
                await CheckAndUpdateForChinaAsync();
            }
            else
            {
                await CheckAndUpdateForGitHubAsync();
            }
        }

        // ==================== 以下为原有更新逻辑（无需修改）====================
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
                    Console.WriteLine($"[Velopack CN] 目前已是最新版本 或無法取得 releases.{rid}.json");
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

        private static async Task<string> GetLatestVersionFromGitHubAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("https://api.github.com/repos/cypwlp/ODProxl/releases/latest");
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("tag_name", out var tagElement))
                {
                    string tag = tagElement.GetString() ?? "";
                    string version = tag.StartsWith("v") ? tag.Substring(1) : tag;
                    Console.WriteLine($"[GetLatestVersion] 從 GitHub 取得最新版本: {version}");
                    return version;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetLatestVersion] 取得 GitHub 最新版本失敗：{ex.Message}");
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

        private async Task DLLUpdateAsync()
        {
            // （你的原始 DLLUpdateAsync 代码保持不变）
            string rid = PlatformHelper.GetCurrentRid();
            string startupPath = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            string dllUpdateBaseUrl = $"http://interior.topmix.net/info/system/software/ODProxl/{rid}/";
            try
            {
                Console.WriteLine($"[DLL Update] 开始独立检查 DLL 更新");
                Console.WriteLine($"[DLL Update] 启动路径: {startupPath}");
                Console.WriteLine($"[DLL Update] Base URL: {dllUpdateBaseUrl}");

                string manifestUrl = $"{dllUpdateBaseUrl}dlls.json";
                var response = await _httpClient.GetAsync(manifestUrl);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                var manifest = JsonSerializer.Deserialize<DllManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (manifest?.Dlls == null || manifest.Dlls.Count == 0)
                {
                    Console.WriteLine("[DLL Update] 清单为空或解析失败，跳过");
                    return;
                }

                Console.WriteLine($"[DLL Update] 清单版本: {manifest.Version}，共检查 {manifest.Dlls.Count} 个文件");
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
                        {
                            needsUpdate = false;
                            Console.WriteLine($"[DLL Update] {dll.FileName} 已是最新");
                        }
                    }
                    if (needsUpdate)
                    {
                        updateList.Add(dll);
                        Console.WriteLine($"[DLL Update] 发现需要更新: {dll.FileName}");
                    }
                }

                if (updateList.Count == 0)
                {
                    Console.WriteLine("[DLL Update] 所有 DLL 均为最新版本");
                    return;
                }

                bool shouldUpdate = false;
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var parameters = new DialogParameters
                    {
                        { "Title", "DLL 更新" },
                        { "Message", $"发现 {updateList.Count} 个 DLL 文件需要更新，是否立即更新？\n\n（此更新与主程序更新完全独立）" }
                    };
                    var result = await _dialogService.ShowDialogAsync("ConfirmDialog", parameters);
                    shouldUpdate = result?.Result == ButtonResult.OK;
                });

                if (!shouldUpdate) return;

                foreach (var dll in updateList)
                {
                    string downloadUrl = string.IsNullOrEmpty(dll.Url) ? $"{dllUpdateBaseUrl}{dll.FileName}" : dll.Url;
                    string localPath = Path.Combine(startupPath, dll.FileName);
                    string tempPath = localPath + ".tmp";

                    Console.WriteLine($"[DLL Update] 正在下载 → {dll.FileName}");
                    var dllResponse = await _httpClient.GetAsync(downloadUrl);
                    dllResponse.EnsureSuccessStatusCode();
                    await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                    {
                        await dllResponse.Content.CopyToAsync(fs);
                    }

                    string downloadedHash = await ComputeFileHashAsync(tempPath);
                    if (!downloadedHash.Equals(dll.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(tempPath);
                        Console.WriteLine($"[DLL Update] {dll.FileName} 下载哈希验证失败，跳过");
                        continue;
                    }

                    if (File.Exists(localPath))
                    {
                        try { File.Delete(localPath); }
                        catch
                        {
                            File.Move(localPath, localPath + ".bak", true);
                        }
                    }
                    File.Move(tempPath, localPath);
                    Console.WriteLine($"[DLL Update] {dll.FileName} 更新成功");
                }

                Console.WriteLine("[DLL Update] DLL 独立更新全部完成！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DLL Update] 独立更新失败：{ex.Message}");
            }
        }

        // ==================== 【新增】支持全平台上传的核心方法（已去重）====================
        public async Task<bool> PublishNewDllVersionAsync(string version, string dllFilePaths,
            string updateDescription, string codeDescription, string targetRid)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                Console.WriteLine("[DLL Upload] 版本号不能为空");
                return false;
            }
            if (string.IsNullOrWhiteSpace(targetRid))
            {
                Console.WriteLine("[DLL Upload] 必须选择目标平台 RID");
                return false;
            }

            string baseUrl = $"http://interior.topmix.net/info/system/software/ODProxl/{targetRid}/";
            string manifestUrl = $"{baseUrl}dlls.json";

            Console.WriteLine($"[DLL Upload] 开始发布 DLL 版本 {version}，目标平台: {targetRid}");

            DllManifest manifest = await GetOrCreateManifestAsync(manifestUrl);

            var paths = dllFilePaths.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();

            string localManifestPath = paths.FirstOrDefault(p => p.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

            if (localManifestPath != null)
            {
                string json = await File.ReadAllTextAsync(localManifestPath);
                manifest = JsonSerializer.Deserialize<DllManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new DllManifest();
                manifest.Version = version;
                Console.WriteLine($"[DLL Upload] 使用本地清单文件 {localManifestPath}");
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

            if (!string.IsNullOrWhiteSpace(updateDescription) || !string.IsNullOrWhiteSpace(codeDescription))
            {
                Console.WriteLine($"[DLL Upload] 更新说明: {updateDescription}");
                Console.WriteLine($"[DLL Upload] 代码说明: {codeDescription}");
            }

            Console.WriteLine($"[DLL Upload] ✅ DLL 版本 {version} 已成功发布到 {targetRid}！");
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
                    return JsonSerializer.Deserialize<DllManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new DllManifest();
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
            Console.WriteLine($"[DLL Upload] 已上传 {fileName} (hash: {hash.Substring(0, 8)}...)");
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
    }
}