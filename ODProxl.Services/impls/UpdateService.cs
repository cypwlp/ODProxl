using Avalonia.Threading;
using Prism.Dialogs;
using System;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace ODProxl.Services.impls
{
    /// <summary>
    /// 自訂的 HttpClientFileDownloader，用來支援 WebDAV Basic Authentication
    /// </summary>
    public class WebDavFileDownloader : HttpClientFileDownloader
    {
        private readonly string _username;
        private readonly string _password;

        public WebDavFileDownloader(string username, string password)
        {
            _username = username;
            _password = password;
        }

        protected override HttpClientHandler CreateHttpClientHandler()
        {
            return new HttpClientHandler
            {
                Credentials = new NetworkCredential(_username, _password),
                PreAuthenticate = true
            };
        }
    }

    public class UpdateService : IUpdateService
    {
        private readonly IDialogService _dialogService;

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
                // ==================== 國內更新（WebDAV）====================
                try
                {
                    string rid = RuntimeInformation.RuntimeIdentifier;

                    // ========== 請替換為實際的 WebDAV 帳號密碼 ==========
                    string username = "WebUser";
                    string password = "2549979631Wei@";
                    // ====================================================

                    // 使用自訂的 Downloader 支援 WebDAV 認證
                    var downloader = new WebDavFileDownloader(username, password);

                    var source = new SimpleWebSource("http://129.204.149.106:8080/ODProxl/", downloader);

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
    }
}