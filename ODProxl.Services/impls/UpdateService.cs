using Avalonia.Threading;
using ODProxl.EntityModels;
using ODProxl.Utils;
using Prism.Dialogs;
using System;
using System.Dynamic;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;
using static System.Net.WebRequestMethods;
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
            // Debug 模式下跳過更新檢查
            if (System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine("[Debug 模式] 已跳過 Velopack 更新檢查");
                return;
            }
            //Velopack增量更新
            if (countryCode == "CN")
            {
                // ==================== 國內更新（WebDAV - 每個版本獨立資料夾）====================
                await CheckAndUpdateForChinaAsync();
            }
            else
            {
                // ==================== 國外更新（GitHub）====================
                await CheckAndUpdateForGitHubAsync();
            }
        }

        private async Task CheckAndUpdateForChinaAsync()
        {
            try
            {
                string rid = RuntimeInformation.RuntimeIdentifier;
                Console.WriteLine($"[Velopack CN] 開始檢查更新，RuntimeIdentifier: {rid}");

                // 1. 從 GitHub 取得最新版本號
                string latestVersion = await GetLatestVersionFromGitHubAsync();
                if (string.IsNullOrEmpty(latestVersion))
                {
                    Console.WriteLine("[Velopack CN] 無法取得最新版本號，跳過更新");
                    return;
                }

                // 2. 指向對應的版本子資料夾（確保結尾有 /）
                string baseUrl = $"http://interior.topmix.net/info/system/software/ODProxl/{latestVersion}/";
                Console.WriteLine($"[Velopack CN] baseUrl = {baseUrl}");

                // 關鍵修改：直接傳 baseUrl 給 UpdateManager，並使用 ExplicitChannel
                var options = new UpdateOptions
                {
                    ExplicitChannel = rid
                };

                var mgr = new UpdateManager(baseUrl, options);

                Console.WriteLine($"[Velopack CN] 目前安裝版本: {mgr.CurrentVersion}");

                var updateInfo = await mgr.CheckForUpdatesAsync();

                if (updateInfo == null)
                {
                    Console.WriteLine($"[Velopack CN] 目前已是最新版本 或無法取得 releases.{rid}.json (channel: {rid})");
                    return;
                }

                Console.WriteLine($"[Velopack CN] 發現新版本 {updateInfo.TargetFullRelease?.Version} (channel: {rid})");

                // 在 UI 執行緒顯示更新對話框
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var parameters = new DialogParameters { { "UpdateInfo", updateInfo } };
                    var result = await _dialogService.ShowDialogAsync("UpdateDialog", parameters);

                    if (result?.Result == ButtonResult.OK)
                    {
                        Console.WriteLine("[Velopack CN] 用戶確認更新，開始下載...");
                        await mgr.DownloadUpdatesAsync(updateInfo);
                        Console.WriteLine("[Velopack CN] 下載完成，準備重新啟動...");
                        mgr.ApplyUpdatesAndRestart(updateInfo);
                    }
                    else
                    {
                        Console.WriteLine("[Velopack CN] 用戶取消更新");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Velopack CN] 更新檢查失敗：{ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"[Velopack CN] InnerException: {ex.InnerException.Message}");
                Console.WriteLine($"[Velopack CN] StackTrace: {ex.StackTrace}");
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

        /// <summary>
        /// 從 GitHub 公開 API 取得最新 Release 的版本號（去掉 v）
        /// </summary>
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
            string rid = PlatformHelper.GetCurrentRid();
            string startupPath = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar); // 干净路径
            string dllUpdateBaseUrl = $"http://interior.topmix.net/info/system/software/ODProxl/{rid}/";

            try
            {
                Console.WriteLine($"[DLL Update] 开始独立检查 DLL 更新");
                Console.WriteLine($"[DLL Update] 启动路径: {startupPath}");
                Console.WriteLine($"[DLL Update] Base URL: {dllUpdateBaseUrl}");

                // 1. 下载 DLL 清单文件
                string manifestUrl = $"{dllUpdateBaseUrl}dlls.json";
                var response = await _httpClient.GetAsync(manifestUrl);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                var manifest = JsonSerializer.Deserialize<DllManifest>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

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

                    // 创建目录（支持子文件夹）
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

                // 2. 在 UI 线程提示用户（完全独立对话框）
                bool shouldUpdate = false;
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var parameters = new DialogParameters
            {
                { "Title", "DLL 更新" },
                { "Message", $"发现 {updateList.Count} 个 DLL 文件需要更新，是否立即更新？\n\n" +
                             "（此更新与主程序更新完全独立）" }
            };

                    var result = await _dialogService.ShowDialogAsync("ConfirmDialog", parameters); // 或使用你的 UpdateDialog 并调整参数
                    shouldUpdate = result?.Result == ButtonResult.OK;
                });

                if (!shouldUpdate)
                {
                    Console.WriteLine("[DLL Update] 用户取消 DLL 更新");
                    return;
                }

                // 3. 下载并更新文件
                foreach (var dll in updateList)
                {
                    string downloadUrl = string.IsNullOrEmpty(dll.Url)
                        ? $"{dllUpdateBaseUrl}{dll.FileName}"
                        : dll.Url;

                    string localPath = Path.Combine(startupPath, dll.FileName);
                    string tempPath = localPath + ".tmp";

                    Console.WriteLine($"[DLL Update] 正在下载 → {dll.FileName}");

                    var dllResponse = await _httpClient.GetAsync(downloadUrl);
                    dllResponse.EnsureSuccessStatusCode();

                    await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                    {
                        await dllResponse.Content.CopyToAsync(fs);
                    }

                    // 哈希验证
                    string downloadedHash = await ComputeFileHashAsync(tempPath);
                    if (!downloadedHash.Equals(dll.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(tempPath);
                        Console.WriteLine($"[DLL Update] {dll.FileName} 下载哈希验证失败，跳过");
                        continue;
                    }

                    // 替换文件（先删除旧文件，避免锁定问题）
                    if (File.Exists(localPath))
                    {
                        try
                        {
                            File.Delete(localPath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[DLL Update] 删除旧文件失败 {dll.FileName}: {ex.Message}，尝试重命名备份");
                            File.Move(localPath, localPath + ".bak", true);
                        }
                    }

                    File.Move(tempPath, localPath);
                    Console.WriteLine($"[DLL Update] {dll.FileName} 更新成功");
                }

                Console.WriteLine("[DLL Update] DLL 独立更新全部完成！建议重启程序以确保新 DLL 生效。");

                // 可选：在更新完成后提示重启
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var restartParams = new DialogParameters { { "Message", "DLL 更新完成，是否立即重启程序？" } };
                    var restartResult = await _dialogService.ShowDialogAsync("ConfirmDialog", restartParams);
                    if (restartResult?.Result == ButtonResult.OK)
                    {
                        // 重启应用（推荐方式）
                        var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                        System.Diagnostics.Process.Start(currentProcess.MainModule?.FileName ?? "");
                        Environment.Exit(0);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DLL Update] 独立更新失败：{ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Inner: {ex.InnerException.Message}");
            }
        }
    }
}