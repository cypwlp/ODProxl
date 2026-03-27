using Avalonia.Threading;
using Prism.Dialogs;
using System;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace ODProxl.Services.impls
{
    public class UpdateService : IUpdateService
    {
        private readonly IDialogService _dialogService;
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        public UpdateService(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        public async Task UpdateODProxlAsync(string countryCode)
        {
            // Debug 模式下跳過更新檢查
            if (System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine("[Debug 模式] 已跳過 Velopack 更新檢查");
                return;
            }

            if (countryCode == "CN")
            {
                // ==================== 國內更新（WebDAV - 每個版本獨立資料夾）====================
                try
                {
                    string rid = RuntimeInformation.RuntimeIdentifier;

                    // 1. 從 GitHub 取得最新版本號（公開 API，無需 Token）
                    string latestVersion = await GetLatestVersionFromGitHubAsync();
                    if (string.IsNullOrEmpty(latestVersion))
                    {
                        Console.WriteLine("[Velopack CN] 無法取得最新版本號");
                        return;
                    }

                    // 2. 指向對應的版本子資料夾
                    string baseUrl = $"http://interior.topmix.net/info/system/software/ODProxl/{latestVersion}/";
                    var source = new SimpleWebSource(baseUrl);

                    var options = new UpdateOptions
                    {
                        ExplicitChannel = rid
                    };

                    var mgr = new UpdateManager(source, options);
                    var updateInfo = await mgr.CheckForUpdatesAsync();

                    if (updateInfo == null)
                    {
                        Console.WriteLine($"[Velopack CN] 目前已是最新版本 (channel: {rid})");
                        return;
                    }

                    Console.WriteLine($"[Velopack CN] 發現新版本 {updateInfo.TargetFullRelease?.Version} (channel: {rid})");

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
            else
            {
                // ==================== 國外更新（GitHub）====================
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
                    return tag.StartsWith("v") ? tag.Substring(1) : tag;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetLatestVersion] 取得 GitHub 最新版本失敗：{ex.Message}");
            }
            return string.Empty;
        }
    }
}