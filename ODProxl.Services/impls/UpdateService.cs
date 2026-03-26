using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Threading;
using Prism.Dialogs;
using Velopack;
using Velopack.Sources;

namespace ODProxl.Services.impls
{
    public class UpdateService : IUpdateService
    {
        private readonly IDialogService _dialogService;

        public UpdateService(IDialogService dialogService)
        {
            _dialogService = dialogService;
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
                // ==================== 國內更新（WebDAV）====================
                try
                {
                    // 取得當前平台的 RID（win-x64、osx-arm64、linux-x64 等）
                    string rid = RuntimeInformation.RuntimeIdentifier;

                    var source = new SimpleWebSource("http://129.204.149.106:8080/ODProxl/");

                    // 關鍵修正：強制指定 channel，避免請求 releases.win.json 等錯誤檔案
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